namespace DalamudACT;

internal sealed record StatusObserverEntry(
    uint StatusId,
    string Name,
    float RemainingSeconds,
    uint Param,
    uint StackCount,
    uint SourceId,
    bool SourceIsSelf,
    bool IsFavorite);
