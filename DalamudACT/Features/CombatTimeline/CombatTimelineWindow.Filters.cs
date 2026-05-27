using System;
using System.Collections.Generic;
using System.Linq;

namespace DalamudACT;

internal sealed partial class CombatTimelineWindow
{
    private static IReadOnlyList<LocalStatsService.CombatTimelineEntry> FilterEntries(
        IReadOnlyList<LocalStatsService.CombatTimelineEntry> entries,
        string actorName,
        TimelineCampFilter actorCampFilter,
        string targetName,
        TimelineCampFilter targetCampFilter,
        TimelineKindFilter kindFilter,
        string actionText)
    {
        if (entries.Count == 0)
            return entries;

        var hasActorFilter = !string.IsNullOrWhiteSpace(actorName);
        var hasActorCampFilter = actorCampFilter != TimelineCampFilter.All;
        var hasTargetFilter = !string.IsNullOrWhiteSpace(targetName);
        var hasTargetCampFilter = targetCampFilter != TimelineCampFilter.All;
        var hasKindFilter = kindFilter != TimelineKindFilter.All;
        var hasActionFilter = !string.IsNullOrWhiteSpace(actionText);
        var hasContextFilter = hasActorFilter || hasActorCampFilter || hasTargetFilter || hasTargetCampFilter || hasActionFilter;
        if (!hasActorFilter && !hasActorCampFilter && !hasTargetFilter && !hasTargetCampFilter && !hasKindFilter && !hasActionFilter)
            return entries;

        var filtered = new List<LocalStatsService.CombatTimelineEntry>(entries.Count);
        foreach (var entry in entries)
        {
            if (hasKindFilter && !MatchesKindFilter(entry, kindFilter))
                continue;

            if (hasContextFilter && IsCombatBoundaryEntry(entry))
            {
                filtered.Add(entry);
                continue;
            }

            if (hasActorFilter && !string.Equals(entry.ActorName, actorName, StringComparison.Ordinal))
                continue;

            if (hasActorCampFilter && !MatchesActorCampFilter(entry, actorCampFilter))
                continue;

            if (hasTargetFilter && !string.Equals(entry.TargetName, targetName, StringComparison.Ordinal))
                continue;

            if (hasTargetCampFilter && !MatchesTargetCampFilter(entry, targetCampFilter))
                continue;

            if (hasActionFilter && !string.Equals(entry.ActionText, actionText, StringComparison.Ordinal))
                continue;

            filtered.Add(entry);
        }

        return filtered;
    }

    private static bool IsCombatBoundaryEntry(LocalStatsService.CombatTimelineEntry entry)
        => entry.Kind is LocalStatsService.CombatTimelineEntryKind.CombatStart or LocalStatsService.CombatTimelineEntryKind.CombatEnd;

    private static IReadOnlyList<string> BuildDistinctNameOptions(
        IReadOnlyList<LocalStatsService.CombatTimelineEntry> entries,
        Func<LocalStatsService.CombatTimelineEntry, string?> selector,
        string currentValue)
    {
        var names = entries
            .Select(selector)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();

        if (!string.IsNullOrWhiteSpace(currentValue) && !names.Contains(currentValue, StringComparer.Ordinal))
            names.Insert(0, currentValue);

        return names;
    }

    private static IReadOnlyList<string> BuildActorOptions(
        IReadOnlyList<LocalStatsService.CombatTimelineEntry> entries,
        string currentValue,
        TimelineCampFilter actorCampFilter)
    {
        var names = entries
            .Where(entry => MatchesActorCampFilter(entry, actorCampFilter))
            .Select(static entry => entry.ActorName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();

        if (!string.IsNullOrWhiteSpace(currentValue) && !names.Contains(currentValue, StringComparer.Ordinal))
            names.Insert(0, currentValue);

        return names;
    }

    private static IReadOnlyList<string> BuildTargetOptions(
        IReadOnlyList<LocalStatsService.CombatTimelineEntry> entries,
        string currentValue,
        TimelineCampFilter targetCampFilter)
    {
        var names = entries
            .Where(entry => MatchesTargetCampFilter(entry, targetCampFilter))
            .Select(static entry => entry.TargetName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();

        if (!string.IsNullOrWhiteSpace(currentValue) && !names.Contains(currentValue, StringComparer.Ordinal))
            names.Insert(0, currentValue);

        return names;
    }

    private static IReadOnlyList<string> BuildActionOptions(
        IReadOnlyList<LocalStatsService.CombatTimelineEntry> entries,
        string currentValue,
        string actorName,
        TimelineCampFilter actorCampFilter,
        string targetName,
        TimelineCampFilter targetCampFilter,
        TimelineKindFilter kindFilter,
        string searchText)
    {
        var names = FilterEntries(entries, actorName, actorCampFilter, targetName, targetCampFilter, kindFilter, string.Empty)
            .Select(static entry => entry.ActionText)
            .Where(static actionText => !string.IsNullOrWhiteSpace(actionText))
            .Select(static actionText => actionText!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var trimmedSearchText = searchText.Trim();
            names = names
                .Where(actionText => actionText.IndexOf(trimmedSearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        names = names
            .OrderBy(static actionText => actionText, StringComparer.Ordinal)
            .ToList();

        if (!string.IsNullOrWhiteSpace(currentValue) && !names.Contains(currentValue, StringComparer.Ordinal))
            names.Insert(0, currentValue);

        return names;
    }

    private static bool MatchesTargetCampFilter(
        LocalStatsService.CombatTimelineEntry entry,
        TimelineCampFilter targetCampFilter)
    {
        if (targetCampFilter != TimelineCampFilter.All && string.IsNullOrWhiteSpace(entry.TargetName))
            return false;

        return targetCampFilter switch
        {
            TimelineCampFilter.All => true,
            TimelineCampFilter.Friendly => entry.TargetIsFriendly,
            TimelineCampFilter.Hostile => !entry.TargetIsFriendly,
            _ => true,
        };
    }

    private static bool MatchesActorCampFilter(
        LocalStatsService.CombatTimelineEntry entry,
        TimelineCampFilter actorCampFilter)
    {
        if (actorCampFilter != TimelineCampFilter.All && string.IsNullOrWhiteSpace(entry.ActorName))
            return false;

        return actorCampFilter switch
        {
            TimelineCampFilter.All => true,
            TimelineCampFilter.Friendly => entry.ActorIsFriendly,
            TimelineCampFilter.Hostile => !entry.ActorIsFriendly,
            _ => true,
        };
    }

    private static bool MatchesKindFilter(
        LocalStatsService.CombatTimelineEntry entry,
        TimelineKindFilter kindFilter)
    {
        return kindFilter switch
        {
            TimelineKindFilter.All => true,
            TimelineKindFilter.Damage => entry.Kind == LocalStatsService.CombatTimelineEntryKind.Damage,
            TimelineKindFilter.Heal => entry.Kind == LocalStatsService.CombatTimelineEntryKind.Heal,
            TimelineKindFilter.Status => entry.Kind == LocalStatsService.CombatTimelineEntryKind.Status,
            TimelineKindFilter.Failure => entry.Kind == LocalStatsService.CombatTimelineEntryKind.Failure,
            TimelineKindFilter.Death => entry.Kind == LocalStatsService.CombatTimelineEntryKind.Death,
            TimelineKindFilter.CombatBoundary => entry.Kind is LocalStatsService.CombatTimelineEntryKind.CombatStart or LocalStatsService.CombatTimelineEntryKind.CombatEnd,
            _ => true,
        };
    }

    private static string GetCampFilterLabel(TimelineCampFilter filter)
        => filter switch
        {
            TimelineCampFilter.Friendly => "友方",
            TimelineCampFilter.Hostile => "敌方",
            _ => "全部",
        };

    private static string GetKindFilterLabel(TimelineKindFilter filter)
        => filter switch
        {
            TimelineKindFilter.Damage => "伤害",
            TimelineKindFilter.Heal => "治疗",
            TimelineKindFilter.Status => "状态",
            TimelineKindFilter.Failure => "未命中/抵抗",
            TimelineKindFilter.Death => "死亡",
            TimelineKindFilter.CombatBoundary => "进战/结算",
            _ => "全部",
        };

    private void ApplyQuickFilterPlayerOutput()
    {
        ClearAllFilters();
        actorCampFilter = TimelineCampFilter.Friendly;
        targetCampFilter = TimelineCampFilter.Hostile;
        kindFilter = TimelineKindFilter.Damage;

        var localPlayerName = DalamudApi.GetLocalPlayerName()?.Trim();
        if (!string.IsNullOrWhiteSpace(localPlayerName))
            actorFilter = localPlayerName;
    }

    private void ApplyQuickFilterEnemyHitFriendly()
    {
        ClearAllFilters();
        actorCampFilter = TimelineCampFilter.Hostile;
        targetCampFilter = TimelineCampFilter.Friendly;
        kindFilter = TimelineKindFilter.Damage;
    }

    private void ApplyQuickFilterKind(TimelineKindFilter filter)
    {
        ClearAllFilters();
        kindFilter = filter;
    }

    private void ClearAllFilters()
    {
        actorFilter = string.Empty;
        actionFilter = string.Empty;
        actionSearchText = string.Empty;
        targetFilter = string.Empty;
        actorCampFilter = TimelineCampFilter.All;
        targetCampFilter = TimelineCampFilter.All;
        kindFilter = TimelineKindFilter.All;
    }

    private bool HasAnyActiveFilter()
        => !string.IsNullOrWhiteSpace(actorFilter)
           || !string.IsNullOrWhiteSpace(actionFilter)
           || !string.IsNullOrWhiteSpace(targetFilter)
           || actorCampFilter != TimelineCampFilter.All
           || targetCampFilter != TimelineCampFilter.All
           || kindFilter != TimelineKindFilter.All;
}
