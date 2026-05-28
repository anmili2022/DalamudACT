using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace DalamudACT;

internal static class TimelineLogImporter
{
    public static IReadOnlyList<TimelineLogEncounterOption> GetEncounterOptions(PluginConfiguration config, out string message)
    {
        message = string.Empty;
        try
        {
            var logFile = ResolveLogFile(config);
            if (logFile == null)
            {
                message = "没有可用的 ACT 日志文件。";
                return [];
            }

            var parsed = ParseLog(logFile.FullName);
            var options = parsed
                .SelectMany(zone => SplitEncounters(zone).Select((encounter, index) => new TimelineLogEncounterOption(
                    encounter.Key,
                    $"{encounter.StartTime:HH:mm:ss}  时长 {FormatDuration(encounter.Duration)}  {encounter.ZoneName} / {encounter.PrimarySourceName} ({encounter.Events.Count}条)",
                    encounter.ZoneName,
                    encounter.PrimarySourceName,
                    encounter.Events.Count)))
                .OrderByDescending(option => option.Key, StringComparer.Ordinal)
                .ToList();
            message = options.Count == 0 ? "日志中没有找到可生成草稿的战斗段。" : $"已找到 {options.Count} 场可用战斗。";
            return options;
        }
        catch (Exception ex)
        {
            LogHelper.Warning("时间轴", ex, "刷新 ACT 日志战斗列表失败。 ");
            message = $"刷新战斗列表失败：{ex.Message}";
            return [];
        }
    }

    public static string GenerateLatestDraft(PluginConfiguration config)
    {
        try
        {
            return GenerateLatestDraftCore(config);
        }
        catch (Exception ex)
        {
            LogHelper.Warning("时间轴", ex, "从 ACT 日志生成时间轴草稿失败。");
            return $"生成时间轴草稿失败：{ex.Message}";
        }
    }

    private static string GenerateLatestDraftCore(PluginConfiguration config)
    {
        var logFile = ResolveLogFile(config);
        if (logFile == null)
            return "没有可用的 ACT 日志文件。请先选择日志文件，或填写包含 Network*.log 的 ACT 日志目录。";

        var parsed = ParseSelectedEncounter(logFile.FullName, config.ActLogEncounterKey);
        if (parsed.Events.Count == 0)
            return $"未能从日志提取Boss技能：{logFile.Name}";

        var aeAssistResources = new AeAssistResourceDownloader();
        aeAssistResources.RefreshNow();

        var generatedDirectory = Path.Combine(
            DalamudApi.PluginInterface.ConfigDirectory.FullName,
            "Timeline",
            "Generated");
        Directory.CreateDirectory(generatedDirectory);

        var fileName = $"{parsed.ZoneId:X}-{SanitizeFileName(parsed.ZoneName)}-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
        var outputPath = Path.Combine(generatedDirectory, fileName);
        File.WriteAllText(outputPath, BuildTimelineText(parsed, logFile.Name, aeAssistResources), new UTF8Encoding(false));
        return $"已生成时间轴草稿：{outputPath}";
    }

    private static FileInfo? ResolveLogFile(PluginConfiguration config)
    {
        var selectedFile = config.ActLogFilePath?.Trim();
        if (!string.IsNullOrWhiteSpace(selectedFile) && File.Exists(selectedFile))
            return new FileInfo(selectedFile);

        var logDirectory = config.ActLogDirectory?.Trim();
        if (string.IsNullOrWhiteSpace(logDirectory) || !Directory.Exists(logDirectory))
            return null;

        return Directory.EnumerateFiles(logDirectory, "Network*.log", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .FirstOrDefault();
    }

    public static string OpenGeneratedDirectory()
    {
        var generatedDirectory = Path.Combine(
            DalamudApi.PluginInterface.ConfigDirectory.FullName,
            "Timeline",
            "Generated");

        try
        {
            Directory.CreateDirectory(generatedDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = generatedDirectory,
                UseShellExecute = true,
                Verb = "open",
            });
            return "已打开时间轴草稿目录。";
        }
        catch (Exception ex)
        {
            LogHelper.Warning("时间轴", ex, $"打开时间轴草稿目录失败：{generatedDirectory}");
            return $"打开时间轴草稿目录失败：{ex.Message}";
        }
    }

    private static ParsedLog ParseSelectedEncounter(string logPath, string? selectedEncounterKey)
    {
        var zones = ParseLog(logPath);
        var encounters = zones.SelectMany(SplitEncounters).ToList();
        var selected = !string.IsNullOrWhiteSpace(selectedEncounterKey)
            ? encounters.FirstOrDefault(encounter => encounter.Key == selectedEncounterKey)
            : null;

        return selected ?? encounters.LastOrDefault() ?? new ParsedLog(0, "Unknown", [], [], []);
    }

    private static List<ParsedLog> ParseLog(string logPath)
    {
        ParsedLog current = new(0, "Unknown", [], [], []);
        var zones = new List<ParsedLog>();

        using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        while (reader.ReadLine() is { } line)
        {
            var parts = line.Split('|');
            if (parts.Length < 3)
                continue;

            if (!DateTimeOffset.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
                continue;

            switch (parts[0])
            {
                case "01":
                    if (parts.Length >= 4 && uint.TryParse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var zoneId))
                    {
                        AddZoneIfUseful(zones, current);
                        current = new ParsedLog(zoneId, parts[3], [], [], []);
                    }
                    break;
                case "260":
                    TryAddCombatState(current, parts, timestamp);
                    break;
                case "03":
                    TryAddHostileNpc(current, parts);
                    break;
                case "20":
                    TryAddStartsUsing(current, parts, timestamp);
                    break;
                case "21":
                    TryAddAbility(current, parts, timestamp);
                    break;
            }
        }

        AddZoneIfUseful(zones, current);
        return zones;
    }

    private static void AddZoneIfUseful(List<ParsedLog> zones, ParsedLog candidate)
    {
        var filteredEvents = FilterEvents(candidate.Events);
        if (filteredEvents.Count > 0)
            zones.Add(candidate with { Events = filteredEvents });
    }

    private static List<ParsedLog> SplitEncounters(ParsedLog zone)
    {
        List<ParsedLog> encounters = [];
        List<DraftEvent> currentEvents = [];
        DateTimeOffset? lastTimestamp = null;

        foreach (var ev in zone.Events.OrderBy(item => item.Timestamp))
        {
            if (lastTimestamp.HasValue && (ev.Timestamp - lastTimestamp.Value).TotalSeconds > 45 && currentEvents.Count > 0)
            {
                encounters.Add(zone with { Events = currentEvents, CombatStartTime = ResolveCombatStartTime(zone, currentEvents) });
                currentEvents = [];
            }

            currentEvents.Add(ev);
            lastTimestamp = ev.Timestamp;
        }

        if (currentEvents.Count > 0)
            encounters.Add(zone with { Events = currentEvents, CombatStartTime = ResolveCombatStartTime(zone, currentEvents) });

        return encounters;
    }

    private static DateTimeOffset? ResolveCombatStartTime(ParsedLog zone, List<DraftEvent> events)
    {
        if (events.Count == 0)
            return null;

        var firstEvent = events.Min(static ev => ev.Timestamp);
        return zone.CombatStartTimes
            .Where(time => time <= firstEvent && (firstEvent - time).TotalMinutes <= 20)
            .OrderByDescending(static time => time)
            .Select(time => (DateTimeOffset?)time)
            .FirstOrDefault();
    }

    private static string FormatDuration(TimeSpan duration)
        => duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours:00}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";

    private static void TryAddStartsUsing(ParsedLog parsed, string[] parts, DateTimeOffset timestamp)
    {
        if (parts.Length < 7 || !IsLikelyNpcId(parts[2]) || !parsed.HostileNpcIds.Contains(parts[2]) || !IsUsefulActionId(parts[4]))
            return;

        parsed.Events.Add(new DraftEvent(timestamp, "StartsUsing", parts[4], parts[5], parts[3]));
    }

    private static void TryAddAbility(ParsedLog parsed, string[] parts, DateTimeOffset timestamp)
    {
        if (parts.Length < 7 || !IsLikelyNpcId(parts[2]) || !parsed.HostileNpcIds.Contains(parts[2]) || !IsUsefulActionId(parts[4]))
            return;

        parsed.Events.Add(new DraftEvent(timestamp, "Ability", parts[4], parts[5], parts[3]));
    }

    private static void TryAddHostileNpc(ParsedLog parsed, string[] parts)
    {
        if (parts.Length < 8 || !IsLikelyNpcId(parts[2]))
            return;

        var classJob = parts[4];
        var ownerId = parts[6];
        if (classJob == "00" && (ownerId == "0000" || ownerId == "00000000"))
            parsed.HostileNpcIds.Add(parts[2]);
    }

    private static void TryAddCombatState(ParsedLog parsed, string[] parts, DateTimeOffset timestamp)
    {
        if (parts.Length < 6)
            return;

        if (parts[2] == "1" && parts[3] == "1")
            parsed.CombatStartTimes.Add(timestamp);
    }

    private static List<DraftEvent> FilterEvents(List<DraftEvent> events)
    {
        List<DraftEvent> result = [];
        Dictionary<string, DateTimeOffset> lastSeen = [];

        foreach (var ev in events.OrderBy(item => item.Timestamp))
        {
            var key = $"{ev.Kind}|{ev.ActionId}|{ev.SourceName}";
            if (lastSeen.TryGetValue(key, out var previous) && (ev.Timestamp - previous).TotalSeconds < 2.5)
                continue;

            lastSeen[key] = ev.Timestamp;
            result.Add(ev);
            if (result.Count >= 500)
                break;
        }

        return result;
    }

    private static string BuildTimelineText(ParsedLog parsed, string sourceLogName, AeAssistResourceDownloader aeAssistResources)
    {
        var firstTimestamp = parsed.CombatStartTime ?? parsed.Events[0].Timestamp;
        var builder = new StringBuilder();
        builder.AppendLine($"# 自动生成：{parsed.ZoneName}");
        builder.AppendLine($"# ZoneId: {parsed.ZoneId}");
        builder.AppendLine($"# Source: {sourceLogName}");
        builder.AppendLine($"# GeneratedAt: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine("# 这是 ACT 网络日志生成的草稿，请人工删除小怪/玩家/NPC杂项并校正分段同步点。");
        builder.AppendLine();
        builder.AppendLine("hideall \"--Reset--\"");
        builder.AppendLine("hideall \"--sync--\"");
        builder.AppendLine();
        builder.AppendLine("0.0 \"--sync--\" InCombat { inGameCombat: \"1\" } window 0,1");

        foreach (var group in DropAbilityAfterStartsUsing(MergeDuplicateEvents(parsed.Events, aeAssistResources, parsed.PrimarySourceName)))
        {
            var seconds = Math.Max(0, (group.Timestamp - firstTimestamp).TotalSeconds);
            var hint = group.Hint;
            var hintSuffix = string.IsNullOrWhiteSpace(hint) ? string.Empty : $" # {hint}";
            builder.AppendLine(FormattableString.Invariant($"{seconds:0.0} \"{Escape(group.ActionName)}\" {group.Kind} {{ id: {FormatActionIds(group.ActionIds)}, source: \"{Escape(group.SourceName)}\" }}{hintSuffix}"));
        }

        return builder.ToString();
    }

    private static List<MergedDraftEvent> MergeDuplicateEvents(List<DraftEvent> events, AeAssistResourceDownloader aeAssistResources, string primarySourceName)
    {
        List<MergedDraftEvent> result = [];

        foreach (var ev in events.OrderBy(item => item.Timestamp))
        {
            var hint = GetAeAssistHint(ev.ActionId, aeAssistResources);
            if (IsUnknownActionName(ev.ActionName) && string.IsNullOrWhiteSpace(hint))
                continue;

            var existingIndex = result.FindIndex(item => IsMergeCandidate(item, ev));
            if (existingIndex >= 0)
            {
                var existing = result[existingIndex];
                if (!existing.ActionIds.Contains(ev.ActionId, StringComparer.OrdinalIgnoreCase))
                    existing.ActionIds.Add(ev.ActionId);

                var sourceName = ChooseMergedSource(existing.SourceName, ev.SourceName, primarySourceName);
                var mergedHint = string.IsNullOrWhiteSpace(existing.Hint) ? hint : existing.Hint;
                if (!string.Equals(sourceName, existing.SourceName, StringComparison.Ordinal) || !string.Equals(mergedHint, existing.Hint, StringComparison.Ordinal))
                    result[existingIndex] = existing with { SourceName = sourceName, Hint = mergedHint };
                continue;
            }

            result.Add(new MergedDraftEvent(ev.Timestamp, ev.Kind, ev.ActionName, ev.SourceName, [ev.ActionId], hint));
        }

        return result;
    }

    private static bool IsMergeCandidate(MergedDraftEvent existing, DraftEvent candidate)
        => existing.Kind == candidate.Kind
           && string.Equals(existing.ActionName, candidate.ActionName, StringComparison.Ordinal)
           && Math.Abs((existing.Timestamp - candidate.Timestamp).TotalSeconds) <= GetMergeWindowSeconds(candidate.Kind);

    private static double GetMergeWindowSeconds(string kind)
        => kind == "Ability" ? 1.0 : 0.25;

    private static string ChooseMergedSource(string existingSource, string candidateSource, string primarySourceName)
    {
        if (!string.IsNullOrWhiteSpace(primarySourceName))
        {
            if (string.Equals(candidateSource, primarySourceName, StringComparison.Ordinal))
                return candidateSource;

            if (string.Equals(existingSource, primarySourceName, StringComparison.Ordinal))
                return existingSource;
        }

        return existingSource;
    }

    private static string FormatActionIds(IReadOnlyList<string> actionIds)
    {
        if (actionIds.Count == 1)
            return $"\"{actionIds[0].ToUpperInvariant()}\"";

        return "[" + string.Join(", ", actionIds.Select(id => $"\"{id.ToUpperInvariant()}\"")) + "]";
    }

    private static List<MergedDraftEvent> DropAbilityAfterStartsUsing(List<MergedDraftEvent> events)
    {
        List<MergedDraftEvent> result = [];
        foreach (var ev in events.OrderBy(item => item.Timestamp))
        {
            if (ev.Kind == "Ability" && HasMatchingRecentStartsUsing(result, ev))
                continue;

            result.Add(ev);
        }

        return result;
    }

    private static bool HasMatchingRecentStartsUsing(List<MergedDraftEvent> previousEvents, MergedDraftEvent ability)
        => previousEvents.Any(ev => ev.Kind == "StartsUsing"
                                   && string.Equals(ev.ActionName, ability.ActionName, StringComparison.Ordinal)
                                   && ev.ActionIds.Any(id => ability.ActionIds.Contains(id, StringComparer.OrdinalIgnoreCase))
                                   && (ability.Timestamp - ev.Timestamp).TotalSeconds is >= 0 and <= 15);

    private static bool IsUnknownActionName(string actionName)
        => actionName.StartsWith("unknown_", StringComparison.OrdinalIgnoreCase)
           || string.IsNullOrWhiteSpace(actionName);

    private static string? GetAeAssistHint(string actionId, AeAssistResourceDownloader aeAssistResources)
    {
        return uint.TryParse(actionId, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsedActionId)
            ? aeAssistResources.GetHint(parsedActionId)
            : null;
    }

    private static bool IsLikelyNpcId(string id)
        => id.StartsWith("4", StringComparison.OrdinalIgnoreCase)
           || id.StartsWith("8", StringComparison.OrdinalIgnoreCase);

    private static bool IsUsefulActionId(string actionId)
        => !string.IsNullOrWhiteSpace(actionId)
           && actionId != "0"
           && actionId != "0000"
           && !string.Equals(actionId, "07", StringComparison.OrdinalIgnoreCase);

    private static string Escape(string value)
        => (value ?? string.Empty).Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string((value ?? "Unknown").Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(safe) ? "Unknown" : safe;
    }

    private sealed record ParsedLog(uint ZoneId, string ZoneName, List<DraftEvent> Events, HashSet<string> HostileNpcIds, List<DateTimeOffset> CombatStartTimes)
    {
        public DateTimeOffset? CombatStartTime { get; init; }

        public DateTimeOffset StartTime => Events.Count == 0 ? DateTimeOffset.MinValue : Events.Min(static ev => ev.Timestamp);

        public DateTimeOffset EndTime => Events.Count == 0 ? DateTimeOffset.MinValue : Events.Max(static ev => ev.Timestamp);

        public TimeSpan Duration => EndTime >= StartTime ? EndTime - StartTime : TimeSpan.Zero;

        public string Key => $"{ZoneId:X}:{StartTime:yyyyMMddHHmmss}";

        public string PrimarySourceName => Events
            .GroupBy(static ev => ev.SourceName)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .FirstOrDefault()?.Key ?? "Unknown";
    }

    private sealed record DraftEvent(DateTimeOffset Timestamp, string Kind, string ActionId, string ActionName, string SourceName);

    private sealed record MergedDraftEvent(DateTimeOffset Timestamp, string Kind, string ActionName, string SourceName, List<string> ActionIds, string? Hint);
}
