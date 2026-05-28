using System;
using System.Collections.Generic;
using System.Linq;
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

    public void PollCombatTimelineHostileCasts(DateTime nowUtc, bool inCombat)
    {
        lock (gate)
        {
            PollCombatTimelineHostileCastsLocked(nowUtc, inCombat);
        }
    }

    private void PollCombatTimelineHostileCastsLocked(DateTime nowUtc, bool inCombat)
    {
        if (!config.CombatTimelineRecordingEnabled || !inCombat || !currentEncounter.Started)
        {
            observedCombatTimelineCastKeys.Clear();
            lastCombatTimelineCastPollUtc = default;
            return;
        }

        if (nowUtc - lastCombatTimelineCastPollUtc < TimeSpan.FromMilliseconds(100))
            return;

        lastCombatTimelineCastPollUtc = nowUtc;
        foreach (var battleChara in DalamudApi.ObjectTable.OfType<IBattleChara>())
            CaptureCombatTimelineHostileCastLocked(battleChara, nowUtc);
    }

    private void CaptureCombatTimelineHostileCastLocked(IBattleChara battleChara, DateTime nowUtc)
    {
        if (!IsLikelyHostileBattleNpcForTimeline(battleChara))
            return;

        var actorId = ResolveBattleCharaActorId(battleChara);
        if (actorId is 0 or InvalidActorId)
            return;

        var actionId = TryGetCastingActionIdForTimeline(battleChara);
        if (actionId == 0)
            return;

        var key = $"{actorId:X8}:{actionId:X8}";
        if (observedCombatTimelineCastKeys.TryGetValue(key, out var lastSeen)
            && (nowUtc - lastSeen).TotalSeconds < 3)
            return;

        observedCombatTimelineCastKeys[key] = nowUtc;
        var actorName = ResolveCombatTimelineSourceName(actorId, nowUtc);
        var actionName = ResolveActionNameForCombatTimeline(actionId);
        var actionText = FormatActionNameWithId(actionName, actionId);
        AppendCombatTimelineEntryLocked(
            nowUtc,
            CombatTimelineEntryKind.Cast,
            $"{actorName} 开始读条 {actionText}。",
            actorName,
            null,
            false,
            false,
            actionText);
    }

    private static bool IsLikelyHostileBattleNpcForTimeline(object battleChara)
    {
        var objectKind = GetObjectPropertyValue(battleChara, "ObjectKind")?.ToString();
        if (!string.Equals(objectKind, "BattleNpc", StringComparison.OrdinalIgnoreCase))
            return false;

        var subKind = GetObjectPropertyValue(battleChara, "SubKind")?.ToString();
        return string.IsNullOrWhiteSpace(subKind)
               || subKind.Contains("Enemy", StringComparison.OrdinalIgnoreCase)
               || subKind.Contains("BattleNpc", StringComparison.OrdinalIgnoreCase)
               || subKind == "5";
    }

    private static uint TryGetCastingActionIdForTimeline(object battleChara)
    {
        if (GetObjectPropertyValue(battleChara, "IsCasting") is not true)
            return 0;

        return TryGetUInt32ObjectProperty(battleChara, "CastActionId", "CastActionID", "CurrentCastActionId", "CurrentCastId");
    }

    private static object? GetObjectPropertyValue(object? instance, string propertyName)
    {
        try
        {
            return instance?.GetType().GetProperty(propertyName)?.GetValue(instance);
        }
        catch
        {
            return null;
        }
    }

    private static uint TryGetUInt32ObjectProperty(object instance, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            try
            {
                var value = GetObjectPropertyValue(instance, propertyName);
                if (value != null)
                    return Convert.ToUInt32(value);
            }
            catch
            {
                // Runtime Dalamud object shapes differ between versions.
            }
        }

        return 0;
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

    private string ResolveActionNameForCombatTimeline(uint actionId)
    {
        if (actionId == 0)
            return "未知技能";

        try
        {
            var sheet = DalamudApi.GameData.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            if (sheet != null && sheet.TryGetRow(actionId, out var row) && !row.Name.IsEmpty)
                return row.Name.ExtractText();
        }
        catch
        {
            // Fall through to the id label if sheet access fails.
        }

        return $"技能 {actionId:X}";
    }
}
