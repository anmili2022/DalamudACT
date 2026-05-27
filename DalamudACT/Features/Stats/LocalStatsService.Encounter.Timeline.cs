using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
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


    private void PollCombatTimelineFriendlyStatusesLocked(DateTime nowUtc, bool inCombat)
    {
        if (!config.CombatTimelineRecordingEnabled || !currentEncounter.Started)
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

            var statusName = GetStatusName(status, statusId);
            var statusText = FormatStatusNameWithId(statusName, statusId);
            var sourceName = sourceActorId == 0 ? "未知来源" : ResolveCombatTimelineSourceName(sourceActorId, nowUtc);
            var statusKindText = isDebuff ? "debuff" : "BUFF";
            var remainingText = FormatStatusRemaining(status);
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
}
