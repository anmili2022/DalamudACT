using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
    private static string ResolvePreferredDotActionName(string existingActionName, string newActionName, string statusName)
    {
        if (!IsUnknownActionName(existingActionName))
            return existingActionName;

        if (!IsUnknownActionName(newActionName))
            return newActionName;

        return !string.IsNullOrWhiteSpace(statusName)
            ? statusName
            : "\u672A\u77E5\u6301\u7EED\u4F24\u5BB3";
    }

    private static uint ResolvePreferredPlayerDotActionId(uint observedActionId, PlayerDotSkillEntry? skillEntry)
    {
        if (skillEntry == null)
            return observedActionId;

        var preferredActionId = skillEntry.GetPreferredActionId(observedActionId);
        return preferredActionId != 0 ? preferredActionId : observedActionId;
    }

    private static string ResolvePreferredPlayerDotActionName(string observedActionName, string statusName, PlayerDotSkillEntry? skillEntry)
    {
        if (!string.IsNullOrWhiteSpace(skillEntry?.SkillName))
            return NormalizeActionName(skillEntry.SkillName);

        if (!string.IsNullOrWhiteSpace(observedActionName))
            return NormalizeActionName(observedActionName);

        if (!string.IsNullOrWhiteSpace(statusName))
            return NormalizeActionName(statusName);

        return "\u672A\u77E5\u6301\u7EED\u4F24\u5BB3";
    }

    private static bool PreferredPlayerDotFallbackMatchesStatus(
        uint statusId,
        string statusName,
        uint? preferredActionId,
        string? preferredActionName)
    {
        if (!preferredActionId.HasValue || !PlayerDotCatalog.IsKnownPlayerDotAction(preferredActionId.Value))
            return false;

        var preferredSkill = PlayerDotCatalog.GetSkillByActionId(preferredActionId.Value);
        if (preferredSkill == null)
            return false;

        if (preferredSkill.StatusIds.Contains(statusId))
            return true;

        if (string.IsNullOrWhiteSpace(statusName))
            return false;

        var normalizedPreferredActionName = string.IsNullOrWhiteSpace(preferredActionName)
            ? string.Empty
            : NormalizeActionName(preferredActionName);
        var normalizedPreferredSkillName = NormalizeActionName(preferredSkill.SkillName);

        return string.Equals(statusName, normalizedPreferredActionName, StringComparison.Ordinal)
               || string.Equals(statusName, normalizedPreferredSkillName, StringComparison.Ordinal);
    }

    private bool IsPlayerDamageOverTimeStatus(object status)
    {
        var statusId = GetStatusId(status);
        if (statusId == 0)
            return false;

        if (dotStatusClassificationCache.TryGetValue(statusId, out var cached))
            return cached;

        var result = PlayerDotCatalog.IsKnownPlayerDotStatus(statusId);
        dotStatusClassificationCache[statusId] = result;
        return result;
    }

    private static string FormatPlayerDotActionName(string actionName)
        => $"{NormalizeActionName(actionName)}\uFF08\u6301\u7EED\u4F24\u5BB3\uFF09";

    private static bool IsUnknownActionName(string? actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
            return true;

        var normalized = actionName.Trim();
        return string.Equals(normalized, "鏈煡鎶€鑳?", StringComparison.Ordinal)
               || string.Equals(normalized, "\u672A\u77E5\u6280\u80FD", StringComparison.Ordinal)
               || normalized.StartsWith("\u6280\u80FD", StringComparison.Ordinal);
    }

    private static string BuildPlayerDotStatusSummary(IBattleNpc hostileTarget)
    {
        var statusSummaries = new List<string>();
        foreach (var status in EnumerateStatusEntries(hostileTarget))
        {
            try
            {
                var statusId = GetStatusId(status);
                if (statusId == 0)
                    continue;

                var statusName = TryGetStatusGameDataText(status, "Name") ?? "未知状态";
                var remainingTime = GetStatusRemainingTime(status);
                var sourceActorId = ResolveStatusSourceActorId(status);
                var sourceText = sourceActorId is 0 or InvalidActorId
                    ? "source=?"
                    : $"source=0x{sourceActorId:X8}";
                statusSummaries.Add($"{statusName}[{statusId}] {remainingTime:0.0}s {sourceText}");
                if (statusSummaries.Count >= 8)
                    break;
            }
            catch
            {
                // Ignore reflection issues while building debug summaries.
            }
        }

        return statusSummaries.Count == 0
            ? "无有效状态"
            : string.Join("；", statusSummaries);
    }

    private static bool TryGetHostileBattleTarget(uint targetId, out IBattleNpc hostileTarget)
    {
        hostileTarget = default!;
        try
        {
            var targetObject = FindObjectByActorId(targetId);
            if (targetObject is not IBattleNpc battleNpc)
                return false;

            if ((battleNpc.StatusFlags & StatusFlags.Hostile) == 0)
                return false;

            hostileTarget = battleNpc;
            return true;
        }
        catch
        {
            return false;
        }
    }

}
