using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

// 当前战斗模块：负责实时战斗记录、战斗流水、结算、状态文本和 ACTX 快照构造。
internal sealed partial class LocalStatsService
{
    private readonly List<CombatTimelineEntry> combatTimelineEntries = new();

    private EncounterSession currentEncounter = new();
    private DateTime partyOutOfCombatSinceUtc;
    private DateTime enteredCombatWithoutDataSinceUtc;
    private DateTime lastNoDataCombatDiagnosticUtc;
    private DateTime lastCombatTimelineStatusPollUtc;
    private DateTime lastCombatTimelineCastPollUtc;
    private int encounterFinalizedVersion;
    private bool latestInCombatHint;
    private bool suppressStaleDisplayUntilNextCombatStart;
    private bool combatTimelineStatusRecorderPrimed;
    private readonly HashSet<CombatTimelineStatusKey> observedCombatTimelineStatusKeys = new();
    private readonly Dictionary<string, DateTime> observedCombatTimelineCastKeys = new(StringComparer.Ordinal);

    public CombatDataWrapper? CurrentCombatData { get; private set; }

    public CombatDataWrapper? DisplayCombatData { get; private set; }

    public IReadOnlyList<CombatTimelineEntry> CombatTimelineEntries
    {
        get
        {
            lock (gate)
                return combatTimelineEntries.ToArray();
        }
    }

    public int EncounterFinalizedVersion
    {
        get
        {
            lock (gate)
                return encounterFinalizedVersion;
        }
    }

    public string DataSourceText => "本地事件采集 / ACTX 统计口径";

    public string StatusText { get; private set; } = "等待战斗数据...";

    public void RecordEncounterActivity(string zoneName, DateTime timeUtc)
    {
        lock (gate)
        {
            var wasStarted = currentEncounter.Started;
            currentEncounter.ZoneName = NormalizeZoneName(zoneName);
            currentEncounter.MarkActivity(timeUtc);
            AppendEncounterStartIfNeededLocked(wasStarted, timeUtc);
        }
    }

    public void RecordDamage(
        uint sourceId,
        uint targetId,
        uint actionId,
        string actionName,
        long amount,
        bool critical,
        bool directHit,
        DateTime timeUtc,
        string zoneName)
    {
        if (amount <= 0)
            return;

        lock (gate)
        {
            var wasStarted = currentEncounter.Started;
            if (!wasStarted)
                return;

            currentEncounter.ZoneName = NormalizeZoneName(zoneName);

            var loggedSourceName = ResolveCombatTimelineSourceName(sourceId, timeUtc);
            var loggedTargetName = ResolveCombatTimelineTargetName(targetId, timeUtc);
            var shouldAppendTimelineEntry = false;
            var sourceIsFriendly = false;
            var targetIsFriendly = false;

            if (TryResolveCombatantSource(sourceId, timeUtc, out var source, out var resolvedSourceIsFriendly))
            {
                currentEncounter.RecordOutgoingDamage(source, actionName, amount, critical, directHit, timeUtc);
                loggedSourceName = source.Name;
                shouldAppendTimelineEntry = true;
                sourceIsFriendly = resolvedSourceIsFriendly;
            }

            if (TryGetTrackedActor(targetId, out var target))
            {
                currentEncounter.RecordIncomingDamage(target, amount, timeUtc);
                loggedTargetName = target.Name;
                shouldAppendTimelineEntry = true;
                targetIsFriendly = true;
            }

            AppendEncounterStartIfNeededLocked(wasStarted, timeUtc);
            if (shouldAppendTimelineEntry)
            {
                AppendCombatTimelineEntryLocked(
                    timeUtc,
                    CombatTimelineEntryKind.Damage,
                    $"{loggedSourceName} 使用{FormatActionNameWithId(actionName, actionId)} 攻击 {loggedTargetName}，造成 {CreateDamageString(amount, useSuffix: true, useDecimals: true)} 伤害{FormatCriticalSuffix(critical)}。",
                    loggedSourceName,
                    loggedTargetName,
                    sourceIsFriendly,
                    targetIsFriendly,
                    FormatActionNameWithId(actionName, actionId));
            }
        }
    }

    public void RecordHeal(
        uint sourceId,
        uint targetId,
        uint actionId,
        string actionName,
        long amount,
        bool critical,
        DateTime timeUtc,
        string zoneName)
    {
        if (amount <= 0)
            return;

        lock (gate)
        {
            var wasStarted = currentEncounter.Started;
            if (!wasStarted)
                return;

            currentEncounter.ZoneName = NormalizeZoneName(zoneName);

            var loggedSourceName = ResolveCombatTimelineSourceName(sourceId, timeUtc);
            var loggedTargetName = ResolveCombatTimelineTargetName(targetId, timeUtc);
            var shouldAppendTimelineEntry = false;
            var sourceIsFriendly = false;
            var targetIsFriendly = false;

            if (TryResolveCombatantSource(sourceId, timeUtc, out var source, out var resolvedSourceIsFriendly))
            {
                currentEncounter.RecordOutgoingHeal(source, amount, critical, timeUtc);
                loggedSourceName = source.Name;
                shouldAppendTimelineEntry = true;
                sourceIsFriendly = resolvedSourceIsFriendly;
            }

            if (TryGetTrackedActor(targetId, out var target))
            {
                currentEncounter.RecordIncomingHeal(target, amount, timeUtc);
                loggedTargetName = target.Name;
                shouldAppendTimelineEntry = true;
                targetIsFriendly = true;
            }

            AppendEncounterStartIfNeededLocked(wasStarted, timeUtc);
            if (shouldAppendTimelineEntry)
            {
                AppendCombatTimelineEntryLocked(
                    timeUtc,
                    CombatTimelineEntryKind.Heal,
                    $"{loggedSourceName} 使用{FormatActionNameWithId(actionName, actionId)} 治疗 {loggedTargetName}，恢复 {CreateDamageString(amount, useSuffix: true, useDecimals: true)} 生命{FormatCriticalSuffix(critical)}。",
                    loggedSourceName,
                    loggedTargetName,
                    sourceIsFriendly,
                    targetIsFriendly,
                    FormatActionNameWithId(actionName, actionId));
            }
        }
    }

    public void RecordFailure(
        uint sourceId,
        uint targetId,
        uint actionId,
        string actionName,
        bool isMiss,
        DateTime timeUtc,
        string zoneName)
    {
        lock (gate)
        {
            var wasStarted = currentEncounter.Started;
            if (!wasStarted)
                return;

            currentEncounter.ZoneName = NormalizeZoneName(zoneName);

            var loggedSourceName = ResolveCombatTimelineSourceName(sourceId, timeUtc);
            var loggedTargetName = ResolveCombatTimelineTargetName(targetId, timeUtc);
            var shouldAppendTimelineEntry = false;
            var sourceIsFriendly = false;
            var targetIsFriendly = false;

            if (TryResolveCombatantSource(sourceId, timeUtc, out var source, out var resolvedSourceIsFriendly))
            {
                currentEncounter.RecordFailedSwing(source, isMiss, timeUtc);
                loggedSourceName = source.Name;
                shouldAppendTimelineEntry = true;
                sourceIsFriendly = resolvedSourceIsFriendly;
            }

            if (TryGetTrackedActor(targetId, out var target))
            {
                loggedTargetName = target.Name;
                shouldAppendTimelineEntry = true;
                targetIsFriendly = true;
            }

            AppendEncounterStartIfNeededLocked(wasStarted, timeUtc);
            if (shouldAppendTimelineEntry)
            {
                AppendCombatTimelineEntryLocked(
                    timeUtc,
                    CombatTimelineEntryKind.Failure,
                    isMiss
                        ? $"{loggedSourceName} 对 {loggedTargetName} 使用{FormatActionNameWithId(actionName, actionId)}，但未命中。"
                        : $"{loggedSourceName} 对 {loggedTargetName} 使用{FormatActionNameWithId(actionName, actionId)}，但效果被抵抗或目标免疫。",
                    loggedSourceName,
                    loggedTargetName,
                    sourceIsFriendly,
                    targetIsFriendly,
                    FormatActionNameWithId(actionName, actionId));
            }
        }
    }

    public void RecordDeath(uint targetId, DateTime timeUtc, string zoneName)
    {
        lock (gate)
        {
            var wasStarted = currentEncounter.Started;
            currentEncounter.ZoneName = NormalizeZoneName(zoneName);
            if (TryGetTrackedActor(targetId, out var target))
            {
                currentEncounter.RecordDeath(target, timeUtc);
                AppendEncounterStartIfNeededLocked(wasStarted, timeUtc);
                AppendCombatTimelineEntryLocked(
                    timeUtc,
                    CombatTimelineEntryKind.Death,
                    $"{target.Name} 战斗不能。",
                    target.Name,
                    target.Name,
                    true,
                    true);
            }
        }
    }


    public void Update(string zoneName, bool inCombat)
    {
        var nowUtc = DateTime.UtcNow;

        lock (gate)
        {
            latestInCombatHint = inCombat;
            currentEncounter.ZoneName = NormalizeZoneName(zoneName);
            PollPartyMemberDeaths(nowUtc, currentEncounter.ZoneName, inCombat);
            PollCombatTimelineFriendlyStatusesLocked(nowUtc, inCombat);
            PollActivePlayerDots(nowUtc, inCombat);
            var allPartyMembersOutOfCombat = AreAllPartyMembersOutOfCombat(inCombat);
            UpdatePartyOutOfCombatTimer(nowUtc, allPartyMembersOutOfCombat);
            UpdateNoDataCombatDiagnostics(nowUtc, inCombat);

            if (ShouldFinalizeEncounter(nowUtc, allPartyMembersOutOfCombat))
            {
                FinalizeEncounter(nowUtc);
            }
            else if (currentEncounter.Started)
            {
                CurrentCombatData = ActxSnapshotFormatter.Build(currentEncounter, isActive: true);
                suppressStaleDisplayUntilNextCombatStart = false;
            }

            EnsureHistoricalPreviewCountdownStartedLocked(nowUtc);
            var shouldSuppressStaleDisplay = suppressStaleDisplayUntilNextCombatStart
                && inCombat
                && !currentEncounter.Started
                && !HasSelectedHistoricalPreviewLocked();

            RefreshDisplayCombatDataLocked(nowUtc, shouldSuppressStaleDisplay);
            UpdateStatusText(nowUtc);
        }
    }

    private void UpdateNoDataCombatDiagnostics(DateTime nowUtc, bool inCombat)
    {
        if (!LogHelper.EnableDebugLog)
        {
            enteredCombatWithoutDataSinceUtc = default;
            lastNoDataCombatDiagnosticUtc = default;
            return;
        }

        if (!inCombat || currentEncounter.Started || combatTimelineEntries.Count > 0)
        {
            enteredCombatWithoutDataSinceUtc = default;
            lastNoDataCombatDiagnosticUtc = default;
            return;
        }

        if (enteredCombatWithoutDataSinceUtc == default)
        {
            enteredCombatWithoutDataSinceUtc = nowUtc;
            return;
        }

        var noDataDuration = nowUtc - enteredCombatWithoutDataSinceUtc;
        if (noDataDuration < TimeSpan.FromSeconds(3))
            return;

        if (lastNoDataCombatDiagnosticUtc != default
            && nowUtc - lastNoDataCombatDiagnosticUtc < TimeSpan.FromSeconds(5))
        {
            return;
        }

        lastNoDataCombatDiagnosticUtc = nowUtc;
        LogHelper.DebugRecent(
            "统计",
            $"已进入战斗 {Math.Floor(noDataDuration.TotalSeconds)} 秒但仍未记录任何战斗流水或统计事件：区域={currentEncounter.ZoneName}，localPlayerGameObjectId=0x{DalamudApi.GetLocalPlayerGameObjectId():X16}，localPlayerObjectId=0x{DalamudApi.GetLocalPlayerObjectId():X8}，localPlayerEntityId=0x{DalamudApi.GetLocalPlayerEntityId():X8}，partyCount={DalamudApi.PartyList.Count()}，buddyCount={DalamudApi.BuddyList.Count()}。");
    }

    private void PollPartyMemberDeaths(DateTime nowUtc, string zoneName, bool inCombat)
    {
        var activePartyActorIds = new HashSet<uint>();

        foreach (var actor in EnumerateTrackedPartyBattleCharas())
        {
            var actorId = ResolveBattleCharaActorId(actor);
            if (actorId is 0 or InvalidActorId)
                continue;

            activePartyActorIds.Add(actorId);
            UpdateTrackedActorHp(actorId, actor.CurrentHp, nowUtc, zoneName, inCombat);
        }

        if (partyMemberHpCache.Count == 0)
            return;

        var staleActorIds = new List<uint>();
        foreach (var actorId in partyMemberHpCache.Keys)
        {
            if (!activePartyActorIds.Contains(actorId))
                staleActorIds.Add(actorId);
        }

        foreach (var actorId in staleActorIds)
            partyMemberHpCache.Remove(actorId);
    }


    private void UpdateTrackedActorHp(uint actorId, uint currentHp, DateTime nowUtc, string zoneName, bool inCombat)
    {
        if (partyMemberHpCache.TryGetValue(actorId, out var previousHp)
            && previousHp > 0
            && currentHp == 0
            && (inCombat || currentEncounter.Started)
            && TryGetTrackedActor(actorId, out var actor))
        {
            currentEncounter.ZoneName = zoneName;
            currentEncounter.RecordDeath(actor, nowUtc);
            AppendCombatTimelineEntryLocked(
                nowUtc,
                CombatTimelineEntryKind.Death,
                $"{actor.Name} 战斗不能。",
                actor.Name,
                actor.Name,
                true,
                true);
        }

        partyMemberHpCache[actorId] = currentHp;
    }

    private void UpdatePartyOutOfCombatTimer(DateTime nowUtc, bool allPartyMembersOutOfCombat)
    {
        if (!currentEncounter.Started)
        {
            partyOutOfCombatSinceUtc = default;
            return;
        }

        if (!allPartyMembersOutOfCombat)
        {
            partyOutOfCombatSinceUtc = default;
            return;
        }

        if (partyOutOfCombatSinceUtc == default)
            partyOutOfCombatSinceUtc = nowUtc;
    }

    private bool ShouldFinalizeEncounter(DateTime nowUtc, bool allPartyMembersOutOfCombat)
    {
        if (!currentEncounter.Started)
            return false;

        return config.CombatEndRule switch
        {
            CombatEndRule.PartyList => allPartyMembersOutOfCombat,
            CombatEndRule.PartyListWithDelay => allPartyMembersOutOfCombat
                && partyOutOfCombatSinceUtc != default
                && nowUtc - partyOutOfCombatSinceUtc >= TimeSpan.FromSeconds(config.EncounterTimeoutSeconds),
            _ => allPartyMembersOutOfCombat,
        };
    }

    private void FinalizeEncounter(DateTime finalizedAtUtc)
    {
        if (!currentEncounter.HasMeaningfulData)
        {
            LogHelper.Debug("统计", $"已丢弃区域 {currentEncounter.ZoneName} 的战斗结算：未记录到有效战斗数据。");
            RemoveLastCombatStartTimelineEntryLocked();
            currentEncounter = new EncounterSession
            {
                ZoneName = currentEncounter.ZoneName,
            };
            observedFriendlyActorCache.Clear();
            recentHostilePlayerActions.Clear();
            activePlayerDots.Clear();
            activeWildfires.Clear();
            partyOutOfCombatSinceUtc = default;
            lastPlayerDotStatusPollUtc = default;
            encounterFinalizedVersion++;
            suppressStaleDisplayUntilNextCombatStart = true;
            return;
        }

        CurrentCombatData = ActxSnapshotFormatter.Build(currentEncounter, isActive: false);

        var history = new HistoricalCombatData(
            currentEncounter.ZoneName,
            ActxSnapshotFormatter.FormatDuration(currentEncounter.DurationSeconds),
            CurrentCombatData,
            currentEncounter.StartUtc,
            finalizedAtUtc);

        if (currentEncounter.DurationSeconds >= MinimumHistoricalEncounterSeconds)
        {
            if (historicalRecords.Count == 0 || !HasSameHistoryIdentity(historicalRecords[^1], history))
                historicalRecords.Add(history);
            else
                historicalRecords[^1] = history;

            SortHistoricalRecords();
        }

        var totalDamage = currentEncounter.Combatants.Sum(static combatant => combatant.Damage);
        var totalHealing = currentEncounter.Combatants.Sum(static combatant => combatant.Healed);
        var totalDamageTaken = currentEncounter.Combatants.Sum(static combatant => combatant.DamageTaken);
        LogHelper.Debug(
            "统计",
            $"战斗已结算：区域={history.ZoneName}，时长={history.Duration}，参战对象={currentEncounter.Combatants.Count}，伤害={totalDamage}，治疗={totalHealing}，承伤={totalDamageTaken}，已写入历史={currentEncounter.DurationSeconds >= MinimumHistoricalEncounterSeconds}。");
        AppendCombatTimelineEntryLocked(finalizedAtUtc, CombatTimelineEntryKind.CombatEnd, $"战斗结束：{history.ZoneName}，持续 {history.Duration}。");

        currentEncounter = new EncounterSession
        {
            ZoneName = history.ZoneName,
        };
        observedFriendlyActorCache.Clear();
        recentHostilePlayerActions.Clear();
        activePlayerDots.Clear();
        activeWildfires.Clear();
        partyOutOfCombatSinceUtc = default;
        lastPlayerDotStatusPollUtc = default;
        encounterFinalizedVersion++;
        suppressStaleDisplayUntilNextCombatStart = true;
    }

    private bool AreAllPartyMembersOutOfCombat(bool fallbackInCombat)
    {
        var hasUsablePartyState = false;

        foreach (var character in EnumerateTrackedPartyBattleCharas())
        {
            if (!ShouldCountBattleCharaForCombatEnd(character))
                continue;

            hasUsablePartyState = true;
            if ((character.StatusFlags & StatusFlags.InCombat) != 0)
                return false;
        }

        return hasUsablePartyState
            ? true
            : !fallbackInCombat;
    }

    private bool ShouldCountBattleCharaForCombatEnd(IBattleChara battleChara)
    {
        var actorId = ResolveBattleCharaActorId(battleChara);
        if (actorId != 0)
        {
            if (TryGetLocalPlayerTrackedActor(actorId, out _))
                return true;

            if (TryGetPartyMemberTrackedActor(actorId, out _))
                return true;

            if (TryGetBuddyTrackedActor(actorId, out _))
                return true;
        }


        if (battleChara is not IBattleNpc battleNpc)
            return true;

        if (IsDutyNpcPartyMemberKind(battleNpc))
            return true;

        return actorId != 0 && observedFriendlyActorCache.ContainsKey(actorId);
    }


    private void UpdateStatusText(DateTime nowUtc)
    {
        if (HasSelectedHistoricalPreviewLocked())
        {
            var selected = historicalRecords[selectedHistoricalRecordIndex];
            if (historicalPreviewExpiresAtUtc.HasValue && nowUtc < historicalPreviewExpiresAtUtc.Value)
            {
                StatusText = $"预览历史记录: {selected.ZoneName} {selected.Duration}（剩余 {GetHistoricalPreviewRemainingSeconds(nowUtc)} 秒）";
                return;
            }

            StatusText = $"预览历史记录: {selected.ZoneName} {selected.Duration}（未进入战斗，预览无限）";
            return;
        }

        if (currentEncounter.Started)
        {
            StatusText = $"战斗中: {currentEncounter.ZoneName} {ActxSnapshotFormatter.FormatDuration(currentEncounter.DurationSeconds)}";
            return;
        }

        if (latestInCombatHint && suppressStaleDisplayUntilNextCombatStart)
        {
            StatusText = "已进入战斗，正在收集新战斗数据...";
            return;
        }

        if (DisplayCombatData?.Msg?.Encounter != null)
        {
            StatusText = "上一场战斗已结束，等待下一场战斗...";
            return;
        }

        StatusText = "等待战斗数据...";
    }

}
