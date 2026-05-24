using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

// debug 战斗记录模块：负责采集 Boss 读条/BUFF/debuff、友方技能/Buff/Debuff、特效标记，并维护调试日志。
internal sealed partial class LocalStatsService
{
    private static readonly TimeSpan DebugCombatRecordPollInterval = TimeSpan.FromMilliseconds(100);

    private readonly List<DebugCombatLogEntry> debugCombatLogEntries = new();
    private readonly HashSet<DebugObservedStatusKey> debugObservedStatusKeys = new();
    private readonly Dictionary<uint, uint> debugBossCastActionIds = new();
    private readonly Dictionary<uint, DebugObservedMarkerSnapshot> debugObservedNamePlateMarkerIds = new();

    private DateTime lastDebugCombatRecordPollUtc;
    private bool debugCombatRecorderPrimed;

    public IReadOnlyList<DebugCombatLogEntry> DebugCombatLogEntries
    {
        get
        {
            lock (gate)
                return debugCombatLogEntries.ToArray();
        }
    }

    public void ClearDebugCombatLog()
    {
        lock (gate)
        {
            debugCombatLogEntries.Clear();
            debugObservedStatusKeys.Clear();
            debugBossCastActionIds.Clear();
            debugObservedNamePlateMarkerIds.Clear();
            debugCombatRecorderPrimed = false;
        }
    }

    public void ApplyDebugCombatLogRetentionLimit()
    {
        lock (gate)
            TrimDebugCombatLogEntriesLocked();
    }

    public void SetDebugCombatRecordingEnabled(bool enabled)
    {
        lock (gate)
        {
            if (config.DebugCombatRecordingEnabled == enabled)
                return;

            config.DebugCombatRecordingEnabled = enabled;
            debugObservedStatusKeys.Clear();
            debugBossCastActionIds.Clear();
            debugObservedNamePlateMarkerIds.Clear();
            lastDebugCombatRecordPollUtc = default;
            debugCombatRecorderPrimed = false;

            AppendDebugCombatLogEntryLocked(
                DateTime.UtcNow,
                DebugCombatLogEntryKind.Recorder,
                enabled ? "debug 战斗记录：开始记录。" : "debug 战斗记录：停止记录。");

            config.Save();
        }
    }

    public void RecordDebugBossAbility(
        uint sourceId,
        uint actionId,
        string actionName,
        IReadOnlyCollection<uint> targetIds,
        long totalDamageToTrackedTargets,
        bool isAutoAttack,
        DateTime timeUtc,
        string zoneName)
    {
        if (!config.DebugCombatRecordingEnabled)
            return;

        if (actionId == 0)
            return;

        if (isAutoAttack && !config.DebugRecordBossAutoAttack)
            return;

        if (!isAutoAttack && !config.DebugRecordBossAction)
            return;

        lock (gate)
        {
            currentEncounter.ZoneName = NormalizeZoneName(zoneName);

            if (!TryGetHostileBattleNpcTrackedActor(sourceId, out var boss))
                return;

            var targetSummary = BuildDebugTargetSummary(targetIds, timeUtc);
            var targetMessageText = string.IsNullOrWhiteSpace(targetSummary) ? string.Empty : $"，目标 {targetSummary}";
            var actionText = FormatActionNameWithId(actionName, actionId);
            if (isAutoAttack)
            {
                var damageText = totalDamageToTrackedTargets > 0
                    ? $"，对我方造成 {CreateDamageString(totalDamageToTrackedTargets, useSuffix: true, useDecimals: true)} 伤害"
                    : string.Empty;
                AppendDebugCombatLogEntryLocked(
                    timeUtc,
                    DebugCombatLogEntryKind.BossAutoAttack,
                    $"{boss.Name} 平A{targetMessageText}{damageText}。",
                    boss.Name,
                    targetSummary,
                    actionId,
                    actionText);
                return;
            }

            AppendDebugCombatLogEntryLocked(
                timeUtc,
                DebugCombatLogEntryKind.BossAction,
                $"{boss.Name} 发动技能 {actionText}{targetMessageText}。",
                boss.Name,
                targetSummary,
                actionId,
                actionText);
        }
    }

    public void RecordDebugFriendlyAbility(
        uint sourceId,
        uint actionId,
        string actionName,
        IReadOnlyCollection<uint> targetIds,
        bool isAutoAttack,
        DateTime timeUtc,
        string zoneName)
    {
        if (!config.DebugCombatRecordingEnabled)
            return;

        if (actionId == 0 || isAutoAttack)
            return;

        lock (gate)
        {
            currentEncounter.ZoneName = NormalizeZoneName(zoneName);

            if (!TryGetTrackedActor(sourceId, out var actor) || actor.Kind == TrackedActorKind.HostileNpc)
                return;

            var isSelf = IsLocalPlayerActor(actor.ActorId);
            if (isSelf)
            {
                if (!config.DebugRecordSelfAction)
                    return;
            }
            else if (!config.DebugRecordPartyAction)
            {
                return;
            }

            var targetSummary = BuildDebugTargetSummary(targetIds, timeUtc);
            var targetMessageText = string.IsNullOrWhiteSpace(targetSummary) ? string.Empty : $"，目标 {targetSummary}";
            var actionText = FormatActionNameWithId(actionName, actionId);
            var kind = isSelf ? DebugCombatLogEntryKind.SelfAction : DebugCombatLogEntryKind.PartyAction;

            AppendDebugCombatLogEntryLocked(
                timeUtc,
                kind,
                $"友方 {actor.Name} 发动技能 {actionText}{targetMessageText}。",
                actor.Name,
                targetSummary,
                actionId,
                actionText);
        }
    }

    public void RecordDebugMarker(
        uint entityId,
        ulong targetId,
        uint markerId,
        uint category,
        uint param1,
        uint param2,
        uint param3,
        uint param4,
        uint param5,
        uint param6,
        uint param7,
        uint param8,
        DateTime timeUtc)
    {
        if (!config.DebugCombatRecordingEnabled)
            return;

        if (markerId == 0)
            return;

        lock (gate)
        {
            var candidateTargetActorIds = BuildDebugMarkerTargetCandidates(
                entityId,
                targetId,
                param1,
                param2,
                param3,
                param4,
                param5,
                param6,
                param7,
                param8);

            foreach (var targetActorId in candidateTargetActorIds)
            {
                if (targetActorId == 0)
                    continue;

                var isSelf = IsLocalPlayerActor(targetActorId);
                if (isSelf)
                {
                    if (!config.DebugRecordSelfMarker)
                        continue;
                }
                else
                {
                    if (!config.DebugRecordPartyMarker)
                        continue;

                    if (!TryGetTrackedActor(targetActorId, out var trackedActor) || trackedActor.Kind == TrackedActorKind.HostileNpc)
                        continue;
                }

                var targetName = ResolveCombatTimelineTargetName(targetActorId, timeUtc);
                var kind = isSelf ? DebugCombatLogEntryKind.SelfMarker : DebugCombatLogEntryKind.PartyMarker;
                AppendDebugCombatLogEntryLocked(
                    timeUtc,
                    kind,
                    $"友方 {targetName} 身上出现特效标记：id={markerId}（category={category}，target={NormalizeEventActorId(targetId)}，param1={param1}，param2={param2}，param3={param3}，param4={param4}）。",
                    targetName,
                    targetName,
                    markerId,
                    FormatMarkerNameWithId(markerId));
                return;
            }
        }
    }

    private static IReadOnlyList<uint> BuildDebugMarkerTargetCandidates(
        uint entityId,
        ulong targetId,
        params uint[] parameters)
    {
        var candidates = new List<uint>(parameters.Length + 2);

        AddDebugMarkerTargetCandidate(candidates, NormalizeEventActorId(targetId));

        // 有些 TargetIcon/ActorControl 事件的 entityId 是施放者或 Boss，
        // 也有些事件会直接把被点名对象放在 entityId。这里先加入，后面会按
        // “友方 / hostile” 再过滤。
        AddDebugMarkerTargetCandidate(candidates, NormalizeEventActorId(entityId));

        foreach (var parameter in parameters)
        {
            if (LooksLikeCombatActorId(parameter))
                AddDebugMarkerTargetCandidate(candidates, NormalizeEventActorId(parameter));
        }

        return candidates;
    }

    private static void AddDebugMarkerTargetCandidate(List<uint> candidates, uint actorId)
    {
        if (actorId == 0)
            return;

        if (!candidates.Contains(actorId))
            candidates.Add(actorId);
    }

    private void PollDebugCombatRecorderLocked(DateTime nowUtc, string zoneName, bool inCombat)
    {
        if (!config.DebugCombatRecordingEnabled)
        {
            debugObservedStatusKeys.Clear();
            debugBossCastActionIds.Clear();
            debugObservedNamePlateMarkerIds.Clear();
            lastDebugCombatRecordPollUtc = default;
            debugCombatRecorderPrimed = false;
            return;
        }

        if (!inCombat && !currentEncounter.Started)
        {
            debugObservedStatusKeys.Clear();
            debugBossCastActionIds.Clear();
            debugObservedNamePlateMarkerIds.Clear();
            debugCombatRecorderPrimed = false;
            return;
        }

        if (nowUtc - lastDebugCombatRecordPollUtc < DebugCombatRecordPollInterval)
            return;

        lastDebugCombatRecordPollUtc = nowUtc;

        try
        {
            var seenStatusKeys = new HashSet<DebugObservedStatusKey>();
            var seenCastingBossIds = new HashSet<uint>();
            var seenNamePlateMarkerActorIds = new HashSet<uint>();

            foreach (var boss in EnumerateDebugBossBattleNpcs())
            {
                var bossActorId = ResolveBattleCharaActorId(boss);
                if (bossActorId is 0 or InvalidActorId)
                    continue;

                if (config.DebugRecordBossCast)
                    CaptureDebugBossCastLocked(boss, bossActorId, nowUtc, zoneName, seenCastingBossIds);

                if (config.DebugRecordBossBuff)
                    CaptureDebugBossBuffsLocked(boss, bossActorId, nowUtc, seenStatusKeys);
            }

            if (config.DebugRecordSelfBuff || config.DebugRecordPartyBuff)
            {
                foreach (var friendlyActor in EnumerateTrackedPartyBattleCharas())
                    CaptureDebugFriendlyBuffsLocked(friendlyActor, nowUtc, seenStatusKeys);
            }

            if (config.DebugRecordPartyDebuff || config.DebugRecordSelfDebuff)
            {
                foreach (var friendlyActor in EnumerateTrackedPartyBattleCharas())
                    CaptureDebugFriendlyDebuffsLocked(friendlyActor, nowUtc, seenStatusKeys);
            }

            if (config.DebugRecordPartyMarker || config.DebugRecordSelfMarker)
            {
                foreach (var friendlyActor in EnumerateTrackedPartyBattleCharas())
                    CaptureDebugFriendlyNamePlateMarkerLocked(friendlyActor, nowUtc, seenNamePlateMarkerActorIds);
            }

            debugObservedStatusKeys.RemoveWhere(key => !seenStatusKeys.Contains(key));

            var staleCastingBossIds = debugBossCastActionIds.Keys
                .Where(actorId => !seenCastingBossIds.Contains(actorId))
                .ToList();
            foreach (var actorId in staleCastingBossIds)
                debugBossCastActionIds.Remove(actorId);

            var staleNamePlateMarkerActorIds = debugObservedNamePlateMarkerIds.Keys
                .Where(actorId => !seenNamePlateMarkerActorIds.Contains(actorId))
                .ToList();
            foreach (var actorId in staleNamePlateMarkerActorIds)
                debugObservedNamePlateMarkerIds.Remove(actorId);

            if (!debugCombatRecorderPrimed)
            {
                debugCombatRecorderPrimed = true;
                AppendDebugCombatLogEntryLocked(nowUtc, DebugCombatLogEntryKind.Recorder, $"debug 战斗记录已就绪：区域={NormalizeZoneName(zoneName)}。");
            }
        }
        catch (Exception ex)
        {
            LogHelper.Warning("debug战斗记录", ex, "轮询 debug 战斗记录失败。");
        }
    }

    private IEnumerable<IBattleNpc> EnumerateDebugBossBattleNpcs()
    {
        foreach (var obj in DalamudApi.ObjectTable)
        {
            if (obj is not IBattleNpc battleNpc)
                continue;

            if ((battleNpc.StatusFlags & StatusFlags.Hostile) == 0)
                continue;

            if (!ShouldTrackHostileBattleNpc(battleNpc))
                continue;

            yield return battleNpc;
        }
    }

    private void CaptureDebugBossCastLocked(
        IBattleNpc boss,
        uint bossActorId,
        DateTime nowUtc,
        string zoneName,
        ISet<uint> seenCastingBossIds)
    {
        if (!boss.IsCasting || boss.CastActionId == 0)
            return;

        seenCastingBossIds.Add(bossActorId);

        if (debugBossCastActionIds.TryGetValue(bossActorId, out var previousActionId)
            && previousActionId == boss.CastActionId)
        {
            return;
        }

        debugBossCastActionIds[bossActorId] = boss.CastActionId;
        if (!debugCombatRecorderPrimed)
            return;

        var actionId = boss.CastActionId;
        var actionName = GetDebugActionName(actionId);
        var actionText = FormatActionNameWithId(actionName, actionId);
        var targetActorId = ResolveCastTargetActorId(boss.CastTargetObjectId);
        var targetName = targetActorId == 0
            ? "未知目标"
            : ResolveCombatTimelineTargetName(targetActorId, nowUtc);
        var castTimeText = boss.TotalCastTime > 0f
            ? $"，读条 {boss.CurrentCastTime:0.0}/{boss.TotalCastTime:0.0}s"
            : string.Empty;

        currentEncounter.ZoneName = NormalizeZoneName(zoneName);
        AppendDebugCombatLogEntryLocked(
            nowUtc,
            DebugCombatLogEntryKind.BossCast,
            $"{boss.Name.TextValue?.Trim()} 开始读条技能 {actionText}，目标 {targetName}{castTimeText}。",
            boss.Name.TextValue?.Trim(),
            targetName,
            actionId,
            actionText);
    }

    private void CaptureDebugBossBuffsLocked(
        IBattleNpc boss,
        uint bossActorId,
        DateTime nowUtc,
        ISet<DebugObservedStatusKey> seenStatusKeys)
    {
        foreach (var status in EnumerateStatusEntries(boss))
        {
            var statusId = GetStatusId(status);
            if (statusId == 0)
                continue;

            var isBuff = IsBuffStatus(status);
            var isDebuff = IsDebuffStatus(status);
            if (!isBuff && !isDebuff)
                continue;

            var sourceActorId = ResolveStatusSourceActorId(status);
            var key = new DebugObservedStatusKey(bossActorId, statusId, sourceActorId, DebugCombatLogEntryKind.BossBuff);
            seenStatusKeys.Add(key);
            if (!debugCombatRecorderPrimed)
            {
                debugObservedStatusKeys.Add(key);
                continue;
            }

            if (!debugObservedStatusKeys.Add(key))
                continue;

            var bossName = boss.Name.TextValue?.Trim();
            var statusName = GetDebugStatusName(status, statusId);
            var statusText = FormatStatusNameWithId(statusName, statusId);
            var sourceName = sourceActorId == 0 ? "未知来源" : ResolveCombatTimelineSourceName(sourceActorId, nowUtc);
            var remainingText = FormatDebugStatusRemaining(status);
            var statusKindText = isBuff ? "BUFF" : "debuff";

            AppendDebugCombatLogEntryLocked(
                nowUtc,
                DebugCombatLogEntryKind.BossBuff,
                $"{bossName} 身上出现 {statusKindText} {statusText}，来源 {sourceName}{remainingText}。",
                bossName,
                bossName,
                statusId,
                statusText);
        }
    }

    private void CaptureDebugFriendlyBuffsLocked(
        IBattleChara friendlyActor,
        DateTime nowUtc,
        ISet<DebugObservedStatusKey> seenStatusKeys)
    {
        var actorId = ResolveBattleCharaActorId(friendlyActor);
        if (actorId is 0 or InvalidActorId)
            return;

        var isSelf = IsLocalPlayerActor(actorId);
        if (isSelf)
        {
            if (!config.DebugRecordSelfBuff)
                return;
        }
        else if (!config.DebugRecordPartyBuff)
        {
            return;
        }

        foreach (var status in EnumerateStatusEntries(friendlyActor))
        {
            var statusId = GetStatusId(status);
            if (statusId == 0)
                continue;

            if (!IsBuffStatus(status))
                continue;

            var sourceActorId = ResolveStatusSourceActorId(status);
            var kind = isSelf ? DebugCombatLogEntryKind.SelfBuff : DebugCombatLogEntryKind.PartyBuff;
            var key = new DebugObservedStatusKey(actorId, statusId, sourceActorId, kind);
            seenStatusKeys.Add(key);
            if (!debugCombatRecorderPrimed)
            {
                debugObservedStatusKeys.Add(key);
                continue;
            }

            if (!debugObservedStatusKeys.Add(key))
                continue;

            var actorName = friendlyActor.Name.TextValue?.Trim();
            var statusName = GetDebugStatusName(status, statusId);
            var statusText = FormatStatusNameWithId(statusName, statusId);
            var sourceName = sourceActorId == 0 ? "\u672a\u77e5\u6765\u6e90" : ResolveCombatTimelineSourceName(sourceActorId, nowUtc);
            var remainingText = FormatDebugStatusRemaining(status);

            AppendDebugCombatLogEntryLocked(
                nowUtc,
                kind,
                $"友方 {actorName} 身上出现 BUFF {statusText}，来源 {sourceName}{remainingText}。",
                actorName,
                actorName,
                statusId,
                statusText);
        }
    }

    private void CaptureDebugFriendlyDebuffsLocked(
        IBattleChara friendlyActor,
        DateTime nowUtc,
        ISet<DebugObservedStatusKey> seenStatusKeys)
    {
        var actorId = ResolveBattleCharaActorId(friendlyActor);
        if (actorId is 0 or InvalidActorId)
            return;

        var isSelf = IsLocalPlayerActor(actorId);
        if (isSelf)
        {
            if (!config.DebugRecordSelfDebuff)
                return;
        }
        else if (!config.DebugRecordPartyDebuff)
        {
            return;
        }

        foreach (var status in EnumerateStatusEntries(friendlyActor))
        {
            var statusId = GetStatusId(status);
            if (statusId == 0)
                continue;

            if (!IsDebuffStatus(status))
                continue;

            var sourceActorId = ResolveStatusSourceActorId(status);
            var kind = isSelf ? DebugCombatLogEntryKind.SelfDebuff : DebugCombatLogEntryKind.PartyDebuff;
            var key = new DebugObservedStatusKey(actorId, statusId, sourceActorId, kind);
            seenStatusKeys.Add(key);
            if (!debugCombatRecorderPrimed)
            {
                debugObservedStatusKeys.Add(key);
                continue;
            }

            if (!debugObservedStatusKeys.Add(key))
                continue;

            var actorName = friendlyActor.Name.TextValue?.Trim();
            var statusName = GetDebugStatusName(status, statusId);
            var statusText = FormatStatusNameWithId(statusName, statusId);
            var sourceName = sourceActorId == 0 ? "未知来源" : ResolveCombatTimelineSourceName(sourceActorId, nowUtc);
            var remainingText = FormatDebugStatusRemaining(status);

            AppendDebugCombatLogEntryLocked(
                nowUtc,
                kind,
                $"友方 {actorName} 身上出现 debuff {statusText}，来源 {sourceName}{remainingText}。",
                actorName,
                actorName,
                statusId,
                statusText);
        }
    }

    private void CaptureDebugFriendlyNamePlateMarkerLocked(
        IBattleChara friendlyActor,
        DateTime nowUtc,
        ISet<uint> seenNamePlateMarkerActorIds)
    {
        var actorId = ResolveBattleCharaActorId(friendlyActor);
        if (actorId is 0 or InvalidActorId)
            return;

        var isSelf = IsLocalPlayerActor(actorId);
        if (isSelf)
        {
            if (!config.DebugRecordSelfMarker)
                return;
        }
        else
        {
            if (!config.DebugRecordPartyMarker)
                return;

            if (!TryGetTrackedActor(actorId, out var trackedActor) || trackedActor.Kind == TrackedActorKind.HostileNpc)
                return;
        }

        seenNamePlateMarkerActorIds.Add(actorId);

        // 2026-05-23：ActorControl Hook 因启动崩溃风险默认禁用。
        // 这里保留无 Hook 的轮询兜底：先读 GameObject.NamePlateIconId，
        // 再补充 Character.Icon / StatusLoopVfxId 作为“头顶图标/循环特效线索”。
        // 这些字段不一定覆盖所有机制箭头；如果截图里的红色箭头不写入这些字段，
        // 仍然需要后续改用安全的 ActorControl 或 VFX 事件采集。
        if (!TryGetDebugFriendlyMarkerSnapshot(friendlyActor, out var marker))
        {
            debugObservedNamePlateMarkerIds.Remove(actorId);
            return;
        }

        if (debugObservedNamePlateMarkerIds.TryGetValue(actorId, out var previousMarkerId)
            && previousMarkerId.Equals(marker))
        {
            return;
        }

        debugObservedNamePlateMarkerIds[actorId] = marker;

        var actorName = friendlyActor.Name.TextValue?.Trim();
        var kind = isSelf ? DebugCombatLogEntryKind.SelfMarker : DebugCombatLogEntryKind.PartyMarker;
        AppendDebugCombatLogEntryLocked(
            nowUtc,
            kind,
            $"友方 {actorName} 身上出现特效标记线索：{marker.SourceLabel}={marker.MarkerId}（无 Hook 轮询）。",
            actorName,
            actorName,
            marker.MarkerId,
            FormatMarkerNameWithId(marker.MarkerId, marker.SourceLabel));
    }

    private static unsafe bool TryGetDebugFriendlyMarkerSnapshot(
        IBattleChara battleChara,
        out DebugObservedMarkerSnapshot marker)
    {
        marker = default;
        try
        {
            if (battleChara.Address == nint.Zero)
                return false;

            var gameObject = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)battleChara.Address;
            if (gameObject->NamePlateIconId != 0)
            {
                marker = new DebugObservedMarkerSnapshot(gameObject->NamePlateIconId, "NamePlateIconId");
                return true;
            }

            var character = (FFXIVClientStructs.FFXIV.Client.Game.Character.Character*)battleChara.Address;
            if (character->Icon != 0)
            {
                marker = new DebugObservedMarkerSnapshot(character->Icon, "Character.Icon");
                return true;
            }

            if (character->StatusLoopVfxId != 0)
            {
                marker = new DebugObservedMarkerSnapshot(character->StatusLoopVfxId, "StatusLoopVfxId");
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private void AppendDebugCombatLogEntryLocked(
        DateTime timeUtc,
        DebugCombatLogEntryKind kind,
        string message,
        string? actorName = null,
        string? targetName = null,
        uint primaryId = 0,
        string? primaryText = null)
    {
        debugCombatLogEntries.Add(new DebugCombatLogEntry(
            timeUtc.ToLocalTime(),
            kind,
            message,
            actorName,
            targetName,
            primaryId,
            primaryText));
        TrimDebugCombatLogEntriesLocked();
    }

    private void TrimDebugCombatLogEntriesLocked()
    {
        var maxEntryCount = config.DebugCombatLogMaxEntries <= 0
            ? 0
            : Math.Clamp(config.DebugCombatLogMaxEntries, 100, 50000);
        if (maxEntryCount == 0)
            return;

        if (debugCombatLogEntries.Count > maxEntryCount)
            debugCombatLogEntries.RemoveRange(0, debugCombatLogEntries.Count - maxEntryCount);
    }

    private string GetDebugActionName(uint actionId)
    {
        if (actionId == 0)
            return "未知技能";

        if (actionSheet != null
            && actionSheet.TryGetRow(actionId, out var actionRow)
            && !actionRow.Name.IsEmpty)
        {
            return actionRow.Name.ExtractText();
        }

        return $"技能 {actionId}";
    }

    private static string GetDebugStatusName(object status, uint statusId)
    {
        var statusName = TryGetStatusGameDataText(status, "Name");
        return string.IsNullOrWhiteSpace(statusName)
            ? $"状态 {statusId}"
            : statusName.Trim();
    }

    private static string FormatStatusNameWithId(string statusName, uint statusId)
        => $"{(string.IsNullOrWhiteSpace(statusName) ? "未知状态" : statusName.Trim())}[{statusId}]";

    private static string FormatMarkerNameWithId(uint markerId, string? sourceLabel = null)
    {
        var prefix = string.IsNullOrWhiteSpace(sourceLabel) ? "标记" : sourceLabel.Trim();
        return markerId == 0 ? prefix : $"{prefix}[{markerId}]";
    }

    private static string FormatDebugStatusRemaining(object status)
    {
        var remainingTime = GetStatusRemainingTime(status);
        return remainingTime > 0f ? $"，剩余 {remainingTime:0.0}s" : string.Empty;
    }

    private static bool IsBuffStatus(object status)
        => TryGetStatusGameDataInt(status, "StatusCategory") == 1;

    private static bool IsDebuffStatus(object status)
        => TryGetStatusGameDataInt(status, "StatusCategory") == 2;

    private static uint ResolveCastTargetActorId(ulong castTargetObjectId)
    {
        if (castTargetObjectId == 0)
            return 0;

        var targetObject = DalamudApi.ObjectTable.SearchById(castTargetObjectId);
        if (targetObject != null)
            return GetGameObjectIdentity(targetObject).ResolveActorId();

        return unchecked((uint)(castTargetObjectId & uint.MaxValue));
    }

    private string BuildDebugTargetSummary(IReadOnlyCollection<uint> targetIds, DateTime nowUtc)
    {
        if (targetIds.Count == 0)
            return string.Empty;

        var targetNames = targetIds
            .Where(static targetId => targetId is not 0 and not InvalidActorId)
            .Distinct()
            .Take(4)
            .Select(targetId => ResolveCombatTimelineTargetName(targetId, nowUtc))
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .ToList();

        if (targetNames.Count == 0)
            return string.Empty;

        var suffix = targetIds.Count > targetNames.Count ? $" 等 {targetIds.Count} 个目标" : string.Empty;
        return $"{string.Join("、", targetNames)}{suffix}";
    }

    public sealed record DebugCombatLogEntry(
        DateTime TimestampLocal,
        DebugCombatLogEntryKind Kind,
        string Message,
        string? ActorName,
        string? TargetName,
        uint PrimaryId,
        string? PrimaryText);

    public enum DebugCombatLogEntryKind
    {
        Recorder,
        BossAutoAttack,
        BossBuff,
        BossAction,
        BossCast,
        PartyAction,
        PartyBuff,
        PartyMarker,
        PartyDebuff,
        SelfAction,
        SelfBuff,
        SelfMarker,
        SelfDebuff,
    }

    private readonly record struct DebugObservedStatusKey(
        uint TargetActorId,
        uint StatusId,
        uint SourceActorId,
        DebugCombatLogEntryKind Kind);

    private readonly record struct DebugObservedMarkerSnapshot(
        uint MarkerId,
        string SourceLabel);

}
