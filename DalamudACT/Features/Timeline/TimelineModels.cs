using System.Collections.Generic;

namespace DalamudACT;

internal sealed record TimelineEntry(
    float TimeSeconds,
    string Text,
    string DisplayText,
    string EventType,
    IReadOnlyList<uint> ActionIds,
    string? Source,
    float? DurationSeconds,
    string? MechanicHint,
    bool Hidden,
    bool IsSync,
    string? JumpLabel);

internal sealed record TimelineDefinition(
    string Id,
    string Name,
    IReadOnlyList<TimelineEntry> Entries,
    IReadOnlyDictionary<string, float> Labels);

internal sealed record TimelineVisibleEntry(
    TimelineEntry Entry,
    float RelativeSeconds,
    string? MechanicHint = null)
{
    public string DisplayText => string.IsNullOrWhiteSpace(MechanicHint)
        ? Entry.DisplayText
        : $"[{MechanicHint}] {Entry.DisplayText}";
}
