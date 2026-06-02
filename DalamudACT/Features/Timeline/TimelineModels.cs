using System.Collections.Generic;

namespace DalamudACT;

internal sealed record TimelineEntry(
    float TimeSeconds,
    string Text,
    string DisplayText,
    string EventType,
    IReadOnlyList<uint> ActionIds,
    string? Source,
    IReadOnlyList<string> Sources,
    float? DurationSeconds,
    string? MechanicHint,
    string? SystemLogId,
    string? SystemLogParam1,
    string? SystemLogTextHint,
    string? NpcYellText,
    string? MapEffectFlags,
    string? MapEffectLocation,
    bool Hidden,
    bool IsSync,
    string? JumpLabel,
    float? JumpTimeSeconds,
    IReadOnlyDictionary<uint, TimelineActionResponse> ActionResponses,
    float WindowFirst = -2.5f,
    float WindowLast = 2.5f,
    int SourceLineNumber = 0);

internal sealed record TimelineActionResponse(string Text, TimelineActionResponseTiming Timing);

internal enum TimelineActionResponseTiming
{
    Ability,
    StartsUsing,
}

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
