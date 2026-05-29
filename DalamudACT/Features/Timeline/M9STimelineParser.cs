using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace DalamudACT;

internal static partial class M9STimelineParser
{
    private static readonly Regex TimelineBlockRegex = new(@"timeline\s*:\s*`(?<body>[\s\S]*?)`", RegexOptions.Compiled);
    private static readonly Regex TimelineLineRegex = new(@"^\s*(?<time>\d+(?:\.\d+)?)\s+\""(?<text>[^\""\\]*(?:\\.[^\""\\]*)*)\""(?<rest>.*)$", RegexOptions.Compiled);
    private static readonly Regex LabelLineRegex = new(@"^\s*(?<time>\d+(?:\.\d+)?)\s+label\s+\""(?<label>[^\""\\]*(?:\\.[^\""\\]*)*)\""", RegexOptions.Compiled);
    private static readonly Regex EventTypeRegex = new(@"(?<!#)\b(?<type>StartsUsing|Ability|InCombat|ActorControl|SystemLogMessage|AddedCombatant)\b", RegexOptions.Compiled);
    private static readonly Regex IdListRegex = new(@"id\s*:\s*\[(?<ids>[^\]]+)\]", RegexOptions.Compiled);
    private static readonly Regex IdRegex = new(@"id\s*:\s*\""(?<id>[0-9A-Fa-f]+)\""", RegexOptions.Compiled);
    private static readonly Regex Param1Regex = new(@"param1\s*:\s*\""(?<param1>[0-9A-Fa-f]+)\""", RegexOptions.Compiled);
    private static readonly Regex DurationRegex = new(@"duration\s+(?<duration>\d+(?:\.\d+)?)", RegexOptions.Compiled);
    private static readonly Regex SourceRegex = new(@"source\s*:\s*\""(?<source>[^\""\\]*(?:\\.[^\""\\]*)*)\""", RegexOptions.Compiled);
    private static readonly Regex JumpRegex = new(@"jump\s+(?:\""(?<label>[^\""\\]*(?:\\.[^\""\\]*)*)\""|(?<label>\S+))", RegexOptions.Compiled);
    private static readonly Regex MechanicHintRegex = new(@"#\s*(?<hint>AOE|范围|死刑|分散|分摊|远离|靠近|背对|击退|踩塔|停止|移动)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ReplaceTextBlockRegex = new(@"'locale'\s*:\s*'cn'[\s\S]*?'replaceText'\s*:\s*\{(?<body>[\s\S]*?)\}\s*,", RegexOptions.Compiled);
    private static readonly Regex ReplaceEntryRegex = new(@"'(?<from>[^']+)'\s*:\s*'(?<to>[^']*)'", RegexOptions.Compiled);

    public static TimelineDefinition ParseFile(string path)
    {
        var script = File.ReadAllText(path);
        var timelineMatch = TimelineBlockRegex.Match(script);
        if (!timelineMatch.Success)
            throw new InvalidDataException("未找到 timeline 模板字符串。");

        var replacements = ParseChineseReplacements(script);
        var entries = new List<TimelineEntry>();
        var labels = new Dictionary<string, float>(StringComparer.Ordinal);
        string? pendingComment = null;
        using var reader = new StringReader(timelineMatch.Groups["body"].Value.Replace("\r\n", "\n"));
        while (reader.ReadLine() is { } line)
        {
            if (TryCaptureComment(line, ref pendingComment))
                continue;

            ParseLabelLine(line, labels);
            var entry = ParseTimelineLine(line, replacements, pendingComment);
            if (entry != null)
            {
                entries.Add(entry);
                pendingComment = null;
            }
        }

        entries.Sort(static (left, right) => left.TimeSeconds.CompareTo(right.TimeSeconds));
        return new TimelineDefinition("m9s", "M9S", entries, labels);
    }

    public static TimelineDefinition ParseTimelineTextFile(string id, string name, string path)
    {
        var entries = new List<TimelineEntry>();
        var labels = new Dictionary<string, float>(StringComparer.Ordinal);
        string? pendingComment = null;
        using var reader = new StringReader(File.ReadAllText(path).Replace("\r\n", "\n"));
        while (reader.ReadLine() is { } line)
        {
            if (TryCaptureComment(line, ref pendingComment))
                continue;

            ParseLabelLine(line, labels);
            var entry = ParseTimelineLine(line, new Dictionary<string, string>(), pendingComment);
            if (entry != null)
            {
                entries.Add(entry);
                pendingComment = null;
            }
        }

        entries.Sort(static (left, right) => left.TimeSeconds.CompareTo(right.TimeSeconds));
        return new TimelineDefinition(id, name, entries, labels);
    }

    private static TimelineEntry? ParseTimelineLine(string line, IReadOnlyDictionary<string, string> replacements, string? pendingComment)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#') || trimmed.StartsWith("hideall", StringComparison.Ordinal))
            return null;

        var lineMatch = TimelineLineRegex.Match(line);
        if (!lineMatch.Success)
            return null;

        if (!float.TryParse(lineMatch.Groups["time"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var timeSeconds))
            return null;

        var text = Unescape(lineMatch.Groups["text"].Value);
        var rest = lineMatch.Groups["rest"].Value;
        var eventMatch = EventTypeRegex.Match(rest);
        var eventType = eventMatch.Success ? eventMatch.Groups["type"].Value : string.Empty;
        var actionIds = ParseActionIds(rest);
        var duration = ParseOptionalFloat(DurationRegex.Match(rest), "duration");
        var sourceMatch = SourceRegex.Match(rest);
        var source = sourceMatch.Success ? Unescape(sourceMatch.Groups["source"].Value) : null;
        var displayText = ApplyChineseReplacements(text, replacements);
        var mechanicHint = ParseMechanicHint(rest);
        var systemLogId = ParseSystemLogId(rest);
        var systemLogParam1 = ParseSystemLogParam1(rest);
        var systemLogTextHint = eventType == "SystemLogMessage" ? pendingComment : null;
        var isInternal = text.StartsWith("--", StringComparison.Ordinal);
        var isSync = text.Contains("sync", StringComparison.OrdinalIgnoreCase);
        var hidden = isInternal || isSync || rest.TrimStart().StartsWith('#');
        var jumpMatch = JumpRegex.Match(rest);
        var jumpLabel = jumpMatch.Success ? Unescape(jumpMatch.Groups["label"].Value) : null;

        return new TimelineEntry(timeSeconds, text, displayText, eventType, actionIds, source, duration, mechanicHint, systemLogId, systemLogParam1, systemLogTextHint, hidden, isSync, jumpLabel);
    }

    private static bool TryCaptureComment(string line, ref string? pendingComment)
    {
        var trimmed = line.Trim();
        if (!trimmed.StartsWith('#'))
            return false;

        pendingComment = trimmed.TrimStart('#').Trim();
        return true;
    }

    private static void ParseLabelLine(string line, Dictionary<string, float> labels)
    {
        var match = LabelLineRegex.Match(line);
        if (!match.Success)
            return;

        if (!float.TryParse(match.Groups["time"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var timeSeconds))
            return;

        labels[Unescape(match.Groups["label"].Value)] = timeSeconds;
    }

    private static IReadOnlyList<uint> ParseActionIds(string rest)
    {
        var ids = new List<uint>();
        var listMatch = IdListRegex.Match(rest);
        if (listMatch.Success)
        {
            foreach (Match quotedId in Regex.Matches(listMatch.Groups["ids"].Value, @"\""(?<id>[0-9A-Fa-f]+)\"""))
                AddActionId(ids, quotedId.Groups["id"].Value);
            return ids;
        }

        var idMatch = IdRegex.Match(rest);
        if (idMatch.Success)
            AddActionId(ids, idMatch.Groups["id"].Value);

        return ids;
    }

    private static string? ParseSystemLogId(string rest)
    {
        var match = IdRegex.Match(rest);
        return match.Success ? match.Groups["id"].Value.ToUpperInvariant() : null;
    }

    private static string? ParseSystemLogParam1(string rest)
    {
        var match = Param1Regex.Match(rest);
        return match.Success ? match.Groups["param1"].Value.ToUpperInvariant() : null;
    }

    private static void AddActionId(List<uint> ids, string rawId)
    {
        if (uint.TryParse(rawId, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id))
            ids.Add(id);
    }

    private static float? ParseOptionalFloat(Match match, string groupName)
    {
        if (!match.Success)
            return null;

        return float.TryParse(match.Groups[groupName].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string? ParseMechanicHint(string rest)
    {
        var match = MechanicHintRegex.Match(rest);
        if (!match.Success)
            return null;

        var hint = match.Groups["hint"].Value.Trim();
        return hint.Equals("范围", StringComparison.OrdinalIgnoreCase) ? "AOE" : hint;
    }

    private static Dictionary<string, string> ParseChineseReplacements(string script)
    {
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
        var block = ReplaceTextBlockRegex.Match(script);
        if (!block.Success)
            return replacements;

        foreach (Match match in ReplaceEntryRegex.Matches(block.Groups["body"].Value))
            replacements[match.Groups["from"].Value] = match.Groups["to"].Value;

        return replacements;
    }

    private static string ApplyChineseReplacements(string text, IReadOnlyDictionary<string, string> replacements)
    {
        foreach (var (from, to) in replacements)
        {
            try
            {
                text = Regex.Replace(text, from, to);
            }
            catch
            {
                text = text.Replace(from, to, StringComparison.Ordinal);
            }
        }

        return text;
    }

    private static string Unescape(string text)
        => text.Replace("\\\"", "\"", StringComparison.Ordinal);
}
