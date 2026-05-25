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
    private int encounterFinalizedVersion;
    private bool latestInCombatHint;
    private bool suppressStaleDisplayUntilNextCombatStart;
    private bool combatTimelineStatusRecorderPrimed;
    private readonly HashSet<CombatTimelineStatusKey> observedCombatTimelineStatusKeys = new();

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

    public void ClearCombatTimeline()
    {
        lock (gate)
            combatTimelineEntries.Clear();
    }

    public void SetCombatTimelineRecordingEnabled(bool enabled)
    {
        lock (gate)
        {
            if (config.CombatTimelineRecordingEnabled == enabled)
                return;

            config.CombatTimelineRecordingEnabled = enabled;
            config.Save();
        }
    }

    public void ApplyCombatTimelineRetentionLimit()
    {
        lock (gate)
            TrimCombatTimelineEntriesLocked();
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
            PollDebugCombatRecorderLocked(nowUtc, currentEncounter.ZoneName, inCombat);
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

    private void PollCombatTimelineFriendlyStatusesLocked(DateTime nowUtc, bool inCombat)
    {
        if (!config.CombatTimelineRecordingEnabled || (!inCombat && !currentEncounter.Started))
        {
            observedCombatTimelineStatusKeys.Clear();
            combatTimelineStatusRecorderPrimed = false;
            lastCombatTimelineStatusPollUtc = default;
            return;
        }

        if (nowUtc - lastCombatTimelineStatusPollUtc < TimeSpan.FromMilliseconds(100))
            return;

        lastCombatTimelineStatusPollUtc = nowUtc;

        var seenStatusKeys = new HashSet<CombatTimelineStatusKey>();
        foreach (var friendlyActor in EnumerateTrackedPartyBattleCharas())
            CaptureCombatTimelineFriendlyStatusesLocked(friendlyActor, nowUtc, seenStatusKeys);

        observedCombatTimelineStatusKeys.RemoveWhere(key => !seenStatusKeys.Contains(key));
        if (!combatTimelineStatusRecorderPrimed)
            combatTimelineStatusRecorderPrimed = true;
    }

    private void CaptureCombatTimelineFriendlyStatusesLocked(IBattleChara friendlyActor, DateTime nowUtc, ISet<CombatTimelineStatusKey> seenStatusKeys)
    {
        var actorId = ResolveBattleCharaActorId(friendlyActor);
        if (actorId is 0 or InvalidActorId)
            return;

        if (!TryGetTrackedActor(actorId, out var trackedActor) || trackedActor.Kind == TrackedActorKind.HostileNpc)
            return;

        foreach (var status in EnumerateStatusEntries(friendlyActor))
        {
            var statusId = GetStatusId(status);
            if (statusId == 0)
                continue;

            var isBuff = IsBuffStatus(status);
            var isDebuff = IsDebuffStatus(status);
            if (!isBuff && !isDebuff)
                continue;

            var sourceActorId = ResolveStatusSourceActorId(status);
            var key = new CombatTimelineStatusKey(actorId, statusId, sourceActorId, isDebuff);
            seenStatusKeys.Add(key);
            if (!combatTimelineStatusRecorderPrimed)
            {
                observedCombatTimelineStatusKeys.Add(key);
                continue;
            }

            if (!observedCombatTimelineStatusKeys.Add(key))
                continue;

            var statusName = GetDebugStatusName(status, statusId);
            var statusText = FormatStatusNameWithId(statusName, statusId);
            var sourceName = sourceActorId == 0 ? "未知来源" : ResolveCombatTimelineSourceName(sourceActorId, nowUtc);
            var statusKindText = isDebuff ? "debuff" : "BUFF";
            var remainingText = FormatDebugStatusRemaining(status);
            AppendCombatTimelineEntryLocked(
                nowUtc,
                CombatTimelineEntryKind.Status,
                $"{trackedActor.Name} 获得{statusKindText} {statusText}，来源 {sourceName}{remainingText}。",
                trackedActor.Name,
                trackedActor.Name,
                true,
                true,
                statusText);
        }
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

    private void AppendEncounterStartIfNeededLocked(bool wasStarted, DateTime timeUtc)
    {
        if (wasStarted || !currentEncounter.Started)
            return;

        AppendCombatTimelineEntryLocked(timeUtc, CombatTimelineEntryKind.CombatStart, $"进入战斗：{currentEncounter.ZoneName}");
    }

    private void RemoveLastCombatStartTimelineEntryLocked()
    {
        for (var i = combatTimelineEntries.Count - 1; i >= 0; i--)
        {
            if (combatTimelineEntries[i].Kind == CombatTimelineEntryKind.CombatStart)
            {
                combatTimelineEntries.RemoveAt(i);
                return;
            }
        }
    }

    private void AppendCombatTimelineEntryLocked(
        DateTime timeUtc,
        CombatTimelineEntryKind kind,
        string message,
        string? actorName = null,
        string? targetName = null,
        bool actorIsFriendly = false,
        bool targetIsFriendly = false,
        string? actionText = null)
    {
        if (!config.CombatTimelineRecordingEnabled)
            return;

        combatTimelineEntries.Add(new CombatTimelineEntry(
            timeUtc.ToLocalTime(),
            kind,
            message,
            actorName,
            targetName,
            actorIsFriendly,
            targetIsFriendly,
            actionText));
        TrimCombatTimelineEntriesLocked();
    }

    private void TrimCombatTimelineEntriesLocked()
    {
        var maxEntryCount = config.CombatTimelineMaxEntries <= 0
            ? 0
            : Math.Clamp(config.CombatTimelineMaxEntries, 100, 50000);
        if (maxEntryCount == 0)
            return;

        if (combatTimelineEntries.Count > maxEntryCount)
            combatTimelineEntries.RemoveRange(0, combatTimelineEntries.Count - maxEntryCount);
    }

    private string ResolveCombatTimelineSourceName(uint actorId, DateTime nowUtc)
    {
        if (TryGetTrackedActor(actorId, out var trackedActor))
            return trackedActor.Name;

        var obj = FindObjectByActorId(actorId);
        var objectName = obj?.Name.TextValue?.Trim();
        if (!string.IsNullOrWhiteSpace(objectName))
            return objectName;

        return TryResolveTrackedSource(actorId, nowUtc, out trackedActor)
            ? trackedActor.Name
            : BuildUnknownActorName(actorId, "未知来源");
    }

    private string ResolveCombatTimelineTargetName(uint actorId, DateTime nowUtc)
    {
        _ = nowUtc;

        if (TryGetTrackedActor(actorId, out var trackedActor))
            return trackedActor.Name;

        var obj = FindObjectByActorId(actorId);
        var objectName = obj?.Name.TextValue?.Trim();
        if (!string.IsNullOrWhiteSpace(objectName))
            return objectName;

        return BuildUnknownActorName(actorId, "未知目标");
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

    public sealed record CombatTimelineEntry(
        DateTime TimestampLocal,
        CombatTimelineEntryKind Kind,
        string Message,
        string? ActorName,
        string? TargetName,
        bool ActorIsFriendly,
        bool TargetIsFriendly,
        string? ActionText = null);

    public enum CombatTimelineEntryKind
    {
        CombatStart,
        Damage,
        Heal,
        Failure,
        Death,
        Status,
        CombatEnd,
    }

    private readonly record struct CombatTimelineStatusKey(
        uint TargetActorId,
        uint StatusId,
        uint SourceActorId,
        bool IsDebuff);

    private sealed class EncounterSession
    {
        private readonly Dictionary<uint, CombatantSession> combatants = new();

        public DateTime StartUtc { get; private set; }

        public DateTime LastEventUtc { get; private set; }

        public DateTime EndUtc { get; private set; }

        public string ZoneName { get; set; } = "未知区域";

        public bool Started => StartUtc != default;

        public bool HasMeaningfulData => combatants.Values.Any(static combatant =>
            combatant.Damage > 0
            || combatant.Healed > 0
            || combatant.DamageTaken > 0
            || combatant.HealsTaken > 0
            || combatant.Deaths > 0
            || combatant.Swings > 0
            || combatant.Heals > 0);

        public IReadOnlyCollection<CombatantSession> Combatants => combatants.Values;

        public double DurationSeconds
        {
            get
            {
                if (!Started)
                    return 1d;

                var endUtc = EndUtc == default ? LastEventUtc : EndUtc;
                var seconds = (endUtc - StartUtc).TotalSeconds;
                return seconds < 1d ? 1d : seconds;
            }
        }

        public void MarkActivity(DateTime timeUtc)
        {
            if (!Started)
                StartUtc = timeUtc;

            if (LastEventUtc < timeUtc)
                LastEventUtc = timeUtc;

            if (EndUtc < timeUtc)
                EndUtc = timeUtc;
        }

        public void RecordOutgoingDamage(
            TrackedActor source,
            string actionName,
            long amount,
            bool critical,
            bool directHit,
            DateTime timeUtc,
            bool isDotDamage = false)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(source).NoteOutgoingDamage(actionName, amount, critical, directHit, timeUtc, isDotDamage);
        }

        public void RecordIncomingDamage(TrackedActor target, long amount, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(target).NoteIncomingDamage(amount, timeUtc);
        }

        public void RecordOutgoingHeal(TrackedActor source, long amount, bool critical, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(source).NoteOutgoingHeal(amount, critical, timeUtc);
        }

        public void RecordIncomingHeal(TrackedActor target, long amount, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(target).NoteIncomingHeal(amount, timeUtc);
        }

        public void RecordFailedSwing(TrackedActor source, bool isMiss, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(source).NoteFailedSwing(isMiss, timeUtc);
        }

        public void RecordDeath(TrackedActor target, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(target).NoteDeath(timeUtc);
        }

        private CombatantSession EnsureCombatant(TrackedActor actor)
        {
            if (combatants.TryGetValue(actor.ActorId, out var existing))
            {
                existing.RefreshIdentity(actor);
                return existing;
            }

            var created = new CombatantSession(actor);
            combatants[actor.ActorId] = created;
            return created;
        }
    }

    private sealed class CombatantSession
    {
        public CombatantSession(TrackedActor actor)
        {
            ActorId = actor.ActorId;
            Name = actor.Name;
            JobId = actor.JobId;
            JobName = actor.JobName;
            Kind = actor.Kind;
        }

        public uint ActorId { get; }

        public string Name { get; private set; }

        public uint JobId { get; private set; }

        public string JobName { get; private set; }

        public TrackedActorKind Kind { get; private set; }

        public long Damage { get; private set; }

        public long Healed { get; private set; }

        public long DamageTaken { get; private set; }

        public long DotDamage { get; private set; }

        public long HealsTaken { get; private set; }

        public int Swings { get; private set; }

        public int Hits { get; private set; }

        public int CritHits { get; private set; }

        public int CritDirectHits { get; private set; }

        public int DirectDamageHits { get; private set; }

        public int DirectDamageCritHits { get; private set; }

        public int Misses { get; private set; }

        public int HitFailed { get; private set; }

        public int Heals { get; private set; }

        public int CritHeals { get; private set; }

        public int Deaths { get; private set; }

        public DateTime FirstEventUtc { get; private set; }

        public DateTime LastEventUtc { get; private set; }

        public long MaxHitValue { get; private set; }

        public string MaxHitActionName { get; private set; } = string.Empty;

        public double PersonalDurationSeconds
        {
            get
            {
                if (FirstEventUtc == default || LastEventUtc <= FirstEventUtc)
                    return 1d;

                var seconds = (LastEventUtc - FirstEventUtc).TotalSeconds;
                return seconds < 1d ? 1d : seconds;
            }
        }

        public void RefreshIdentity(TrackedActor actor)
        {
            if (!string.IsNullOrWhiteSpace(actor.Name))
                Name = actor.Name;

            if (actor.JobId != 0)
                JobId = actor.JobId;

            if (!string.IsNullOrWhiteSpace(actor.JobName))
                JobName = actor.JobName;

            if (actor.Kind != TrackedActorKind.Unknown)
                Kind = actor.Kind;
        }

        public void NoteOutgoingDamage(string actionName, long amount, bool critical, bool directHit, DateTime timeUtc, bool isDotDamage)
        {
            Touch(timeUtc);
            Damage += amount;
            if (isDotDamage)
                DotDamage += amount;
            Swings++;
            Hits++;
            if (critical)
                CritHits++;
            if (critical && directHit)
                CritDirectHits++;
            if (!isDotDamage)
            {
                DirectDamageHits++;
                if (critical)
                    DirectDamageCritHits++;
            }

            if (amount > MaxHitValue)
            {
                MaxHitValue = amount;
                MaxHitActionName = actionName;
            }
        }

        public void NoteIncomingDamage(long amount, DateTime timeUtc)
        {
            Touch(timeUtc);
            DamageTaken += amount;
        }

        public void NoteOutgoingHeal(long amount, bool critical, DateTime timeUtc)
        {
            Touch(timeUtc);
            Healed += amount;
            Heals++;
            if (critical)
                CritHeals++;
        }

        public void NoteIncomingHeal(long amount, DateTime timeUtc)
        {
            Touch(timeUtc);
            HealsTaken += amount;
        }

        public void NoteFailedSwing(bool isMiss, DateTime timeUtc)
        {
            Touch(timeUtc);
            Swings++;
            if (isMiss)
                Misses++;
            else
                HitFailed++;
        }

        public void NoteDeath(DateTime timeUtc)
        {
            Touch(timeUtc);
            Deaths++;
        }

        private void Touch(DateTime timeUtc)
        {
            if (FirstEventUtc == default || timeUtc < FirstEventUtc)
                FirstEventUtc = timeUtc;

            if (LastEventUtc < timeUtc)
                LastEventUtc = timeUtc;
        }
    }

    private static class ActxSnapshotFormatter
    {
        public static CombatDataWrapper Build(EncounterSession encounter, bool isActive)
        {
            var durationSeconds = encounter.DurationSeconds;
            var combatants = encounter.Combatants
                .OrderByDescending(combatant => combatant.Damage / durationSeconds)
                .ThenBy(combatant => combatant.Name, StringComparer.Ordinal)
                .ToList();

            var summaryCombatants = combatants
                .Where(static combatant => combatant.Kind != TrackedActorKind.HostileNpc)
                .ToList();
            if (summaryCombatants.Count == 0)
                summaryCombatants = combatants;

            var totalDamage = summaryCombatants.Sum(static combatant => combatant.Damage);
            var totalDamageTaken = summaryCombatants.Sum(static combatant => combatant.DamageTaken);
            var totalHits = summaryCombatants.Sum(static combatant => combatant.Hits);
            var totalHitFailed = summaryCombatants.Sum(static combatant => combatant.HitFailed);
            var totalCritHits = summaryCombatants.Sum(static combatant => combatant.CritHits);

            var maxHitCombatant = summaryCombatants
                .Where(static combatant => combatant.MaxHitValue > 0)
                .OrderByDescending(static combatant => combatant.MaxHitValue)
                .ThenBy(combatant => combatant.Name, StringComparer.Ordinal)
                .FirstOrDefault();

            var encounterMaxHit = "--";
            var encounterShortMaxHit = "--";
            if (maxHitCombatant != null)
            {
                var actionName = SafeActionName(maxHitCombatant.MaxHitActionName);
                encounterMaxHit =
                    $"{maxHitCombatant.Name}-{actionName}-{CreateDamageString(maxHitCombatant.MaxHitValue, useSuffix: true, useDecimals: true)}";
                encounterShortMaxHit =
                    $"{maxHitCombatant.Name}-{CreateDamageString(maxHitCombatant.MaxHitValue, useSuffix: true, useDecimals: false)}";
            }

            var combatantPayload = new Dictionary<string, Combatant>(combatants.Count, StringComparer.Ordinal);
            foreach (var combatant in combatants)
            {
                var damagePercent = totalDamage > 0
                    ? $"{(int)(combatant.Damage / (float)totalDamage * 100f)}%"
                    : "--";

                var encDps = combatant.Damage / durationSeconds;
                var encHps = combatant.Healed / durationSeconds;
                var dtps = combatant.DamageTaken / durationSeconds;
                var toHit = combatant.Swings > 0
                    ? combatant.Hits / (float)combatant.Swings * 100f
                    : 0f;

                combatantPayload[$"{combatant.Name}#{combatant.ActorId:X8}"] = new Combatant
                {
                    Name = combatant.Name,
                    ParticipantKind = FormatTrackedActorKind(combatant.Kind),
                    Job = FormatCombatantJobName(combatant),
                    DamagePercentText = damagePercent,
                    DamageText = CreateDamageString(combatant.Damage, useSuffix: true, useDecimals: true),
                    EncDpsText = encDps.ToString("0", CultureInfo.InvariantCulture),
                    EncHpsText = encHps.ToString("0", CultureInfo.InvariantCulture),
                    HealedText = CreateDamageString(combatant.Healed, useSuffix: true, useDecimals: true),
                    DtpsText = dtps.ToString("0", CultureInfo.InvariantCulture),
                    MaxHitText = combatant.MaxHitValue > 0
                        ? $"{SafeActionName(combatant.MaxHitActionName)}-{CreateDamageString(combatant.MaxHitValue, useSuffix: true, useDecimals: true)}"
                        : "--",
                    HitsText = combatant.Hits.ToString(CultureInfo.InvariantCulture),
                    CritHitsText = combatant.CritHits.ToString(CultureInfo.InvariantCulture),
                    CritDirectHitsText = combatant.CritDirectHits.ToString(CultureInfo.InvariantCulture),
                    ToHitText = toHit.ToString("F", CultureInfo.InvariantCulture),
                    DamageTakenText = CreateDamageString(combatant.DamageTaken, useSuffix: true, useDecimals: true),
                    BlockPctText = "--",
                    ParryPctText = "--",
                    DeathsText = combatant.Deaths.ToString(CultureInfo.InvariantCulture),
                    DotDamageText = CreateDamageString(combatant.DotDamage, useSuffix: true, useDecimals: true),
                };
            }

            return new CombatDataWrapper
            {
                Type = "broadcast",
                MsgType = "CombatData",
                Msg = new CombatData
                {
                    Type = "CombatData",
                    IsActive = isActive ? "true" : "false",
                    Encounter = new Encounter
                    {
                        CurrentZoneName = encounter.ZoneName,
                        DurationText = FormatDuration(durationSeconds),
                        DamageText = CreateDamageString(totalDamage, useSuffix: true, useDecimals: true),
                        EncDpsText = (totalDamage / durationSeconds).ToString("0", CultureInfo.InvariantCulture),
                        HitsText = totalHits.ToString(CultureInfo.InvariantCulture),
                        HitFailedText = totalHitFailed.ToString(CultureInfo.InvariantCulture),
                        CritHitsText = totalCritHits.ToString(CultureInfo.InvariantCulture),
                        CritHitPercentText = totalHits > 0
                            ? $"{(int)(totalCritHits / (float)totalHits * 100f)}%"
                            : "0%",
                        MaxHitText = encounterMaxHit,
                        MaxHitValueText = encounterShortMaxHit,
                        DamageTakenText = CreateDamageString(totalDamageTaken, useSuffix: true, useDecimals: true),
                    },
                    Combatant = combatantPayload,
                },
            };
        }

        public static string FormatDuration(double durationSeconds)
        {
            var wholeSeconds = durationSeconds < 1d
                ? 1
                : (int)Math.Round(durationSeconds, MidpointRounding.AwayFromZero);
            var span = TimeSpan.FromSeconds(wholeSeconds);
            return span.TotalHours >= 1d
                ? span.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
                : span.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        }

        private static string SafeActionName(string? actionName)
            => string.IsNullOrWhiteSpace(actionName) ? "未知技能" : actionName;

        private static string FormatCombatantJobName(CombatantSession combatant)
        {
            if (!string.IsNullOrWhiteSpace(combatant.JobName))
                return combatant.JobName;

            return combatant.Kind switch
            {
                TrackedActorKind.FriendlyNpc => "友方NPC",
                TrackedActorKind.HostileNpc => "敌方NPC",
                _ => "-",
            };
        }

        private static string? FormatTrackedActorKind(TrackedActorKind kind)
            => kind switch
            {
                TrackedActorKind.Player => "player",
                TrackedActorKind.FriendlyNpc => "friendlyNpc",
                TrackedActorKind.HostileNpc => "hostileNpc",
                _ => null,
            };

    }

}
