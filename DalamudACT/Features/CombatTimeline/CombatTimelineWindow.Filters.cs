using System;
using System.Collections.Generic;
using System.Linq;

namespace DalamudACT;

internal sealed partial class CombatTimelineWindow
{
    private static IReadOnlyList<LocalStatsService.CombatTimelineEntry> FilterEntries(
        IReadOnlyList<LocalStatsService.CombatTimelineEntry> entries,
        string characterName,
        TimelineContentFilter contentFilter,
        string textSearch)
    {
        if (entries.Count == 0)
            return entries;

        var normalizedContentFilter = contentFilter == TimelineContentFilter.None
            ? DefaultContentFilters
            : contentFilter;
        var hasCharacterFilter = !string.IsNullOrWhiteSpace(characterName);
        var trimmedTextSearch = textSearch.Trim();
        var hasTextSearch = !string.IsNullOrWhiteSpace(trimmedTextSearch);

        var filtered = new List<LocalStatsService.CombatTimelineEntry>(entries.Count);
        foreach (var entry in entries)
        {
            if (hasTextSearch && entry.Message.IndexOf(trimmedTextSearch, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (!MatchesContentFilter(entry, characterName, hasCharacterFilter, normalizedContentFilter))
                continue;

            filtered.Add(entry);
        }

        return filtered;
    }

    private static bool MatchesContentFilter(
        LocalStatsService.CombatTimelineEntry entry,
        string characterName,
        bool hasCharacterFilter,
        TimelineContentFilter contentFilter)
    {
        if (contentFilter.HasFlag(TimelineContentFilter.MapEffect) && entry.Kind == LocalStatsService.CombatTimelineEntryKind.MapEffect)
            return true;

        if (contentFilter.HasFlag(TimelineContentFilter.CombatBoundary)
            && entry.Kind is LocalStatsService.CombatTimelineEntryKind.CombatStart or LocalStatsService.CombatTimelineEntryKind.CombatEnd)
        {
            return true;
        }

        if (contentFilter.HasFlag(TimelineContentFilter.Output) && IsOutputEntry(entry, characterName, hasCharacterFilter))
            return true;

        if (contentFilter.HasFlag(TimelineContentFilter.TakenDamage) && IsTakenDamageEntry(entry, characterName, hasCharacterFilter))
            return true;

        if (contentFilter.HasFlag(TimelineContentFilter.Mitigation) && IsMitigationAnalysisEntry(entry, characterName, hasCharacterFilter))
            return true;

        if (contentFilter.HasFlag(TimelineContentFilter.Heal) && IsHealEntry(entry, characterName, hasCharacterFilter))
            return true;

        if (contentFilter.HasFlag(TimelineContentFilter.Death) && IsDeathEntry(entry, characterName, hasCharacterFilter))
            return true;

        if (contentFilter.HasFlag(TimelineContentFilter.Cast) && IsCastEntry(entry, characterName, hasCharacterFilter))
            return true;

        if (contentFilter.HasFlag(TimelineContentFilter.Status) && IsStatusEntry(entry, characterName, hasCharacterFilter))
            return true;

        return false;
    }

    private static bool IsOutputEntry(LocalStatsService.CombatTimelineEntry entry, string characterName, bool hasCharacterFilter)
        => entry.Kind == LocalStatsService.CombatTimelineEntryKind.Damage
           && entry.ActorIsFriendly
           && !entry.TargetIsFriendly
           && (!hasCharacterFilter || IsEntryActor(entry, characterName));

    private static bool IsTakenDamageEntry(LocalStatsService.CombatTimelineEntry entry, string characterName, bool hasCharacterFilter)
        => entry.Kind == LocalStatsService.CombatTimelineEntryKind.Damage
           && !entry.ActorIsFriendly
           && entry.TargetIsFriendly
           && (!hasCharacterFilter || IsEntryTarget(entry, characterName));

    private static bool IsMitigationAnalysisEntry(LocalStatsService.CombatTimelineEntry entry, string characterName, bool hasCharacterFilter)
        => IsTakenDamageEntry(entry, characterName, hasCharacterFilter);

    private static bool IsHealEntry(LocalStatsService.CombatTimelineEntry entry, string characterName, bool hasCharacterFilter)
        => entry.Kind == LocalStatsService.CombatTimelineEntryKind.Heal
           && (!hasCharacterFilter || IsEntryActor(entry, characterName) || IsEntryTarget(entry, characterName));

    private static bool IsDeathEntry(LocalStatsService.CombatTimelineEntry entry, string characterName, bool hasCharacterFilter)
        => entry.Kind == LocalStatsService.CombatTimelineEntryKind.Death
           && (!hasCharacterFilter || IsEntryTarget(entry, characterName) || entry.Message.Contains(characterName, StringComparison.Ordinal));

    private static bool IsCastEntry(LocalStatsService.CombatTimelineEntry entry, string characterName, bool hasCharacterFilter)
        => entry.Kind == LocalStatsService.CombatTimelineEntryKind.Cast
           && (!hasCharacterFilter || IsEntryActor(entry, characterName));

    private static bool IsStatusEntry(LocalStatsService.CombatTimelineEntry entry, string characterName, bool hasCharacterFilter)
        => entry.Kind == LocalStatsService.CombatTimelineEntryKind.Status
           && (!hasCharacterFilter || IsEntryActor(entry, characterName) || IsEntryTarget(entry, characterName) || entry.Message.Contains(characterName, StringComparison.Ordinal));

    private static bool IsEntryActor(LocalStatsService.CombatTimelineEntry entry, string characterName)
        => string.Equals(entry.ActorName, characterName, StringComparison.Ordinal);

    private static bool IsEntryTarget(LocalStatsService.CombatTimelineEntry entry, string characterName)
        => string.Equals(entry.TargetName, characterName, StringComparison.Ordinal);

    private static bool IsUnmitigatedTakenDamageEntry(LocalStatsService.CombatTimelineEntry entry)
        => entry.Kind == LocalStatsService.CombatTimelineEntryKind.Damage
           && !entry.ActorIsFriendly
           && entry.TargetIsFriendly
           && entry.Message.Contains("减伤 无", StringComparison.Ordinal);

    private static IReadOnlyList<string> BuildCharacterOptions(
        IReadOnlyList<LocalStatsService.CombatTimelineEntry> entries,
        string currentValue)
    {
        var names = entries
            .SelectMany(static entry => new[] { entry.ActorName, entry.TargetName })
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();

        if (!string.IsNullOrWhiteSpace(currentValue) && !names.Contains(currentValue, StringComparer.Ordinal))
            names.Insert(0, currentValue);

        return names;
    }

    private void ClearAllFilters()
    {
        characterFilter = string.Empty;
        textSearchFilter = string.Empty;
        contentFilters = DefaultContentFilters;
        config.CombatTimelineCharacterFilter = string.Empty;
        config.CombatTimelineTextSearchFilter = string.Empty;
        config.CombatTimelineContentFilterMask = (int)DefaultContentFilters;
        config.Save();
    }

    private bool HasAnyActiveFilter()
        => !string.IsNullOrWhiteSpace(characterFilter)
           || !string.IsNullOrWhiteSpace(textSearchFilter)
           || contentFilters != DefaultContentFilters;
}
