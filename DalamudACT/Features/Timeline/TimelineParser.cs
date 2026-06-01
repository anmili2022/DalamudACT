using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace DalamudACT;

internal static partial class TimelineParser
{
    private static readonly Regex TimelineBlockRegex = new(@"timeline\s*:\s*`(?<body>[\s\S]*?)`", RegexOptions.Compiled);
    private static readonly Regex TimelineLineRegex = new(@"^\s*(?<time>\d+(?:\.\d+)?)\s+\""(?<text>[^\""\\]*(?:\\.[^\""\\]*)*)\""(?<rest>.*)$", RegexOptions.Compiled);
    private static readonly Regex LabelLineRegex = new(@"^\s*(?<time>\d+(?:\.\d+)?)\s+label\s+\""(?<label>[^\""\\]*(?:\\.[^\""\\]*)*)\""", RegexOptions.Compiled);
    private static readonly Regex EventTypeRegex = new(@"(?<!#)\b(?<type>StartsUsing|Ability|InCombat|ActorControl|SystemLogMessage|AddedCombatant|MapEffect|Timer)\b", RegexOptions.Compiled);
    private static readonly Regex IdListRegex = new(@"id\s*:\s*\[(?<ids>[^\]]+)\]", RegexOptions.Compiled);
    private static readonly Regex IdRegex = new(@"id\s*:\s*['\""`]*(?<id>[0-9A-Fa-f]+)['\""`]*", RegexOptions.Compiled);
    private static readonly Regex Param1Regex = new(@"param1\s*:\s*['\""`]*(?<param1>[0-9A-Fa-f]+)['\""`]*", RegexOptions.Compiled);
    private static readonly Regex DurationRegex = new(@"duration\s+(?<duration>\d+(?:\.\d+)?)", RegexOptions.Compiled);
    private static readonly Regex SourceListRegex = new(@"source\s*:\s*\[(?<sources>[^\]]+)\]", RegexOptions.Compiled);
    private static readonly Regex SourceRegex = new(@"source\s*:\s*['\""`](?<source>[^'\""`\\]*(?:\\.[^'\""`\\]*)*)['\""`]", RegexOptions.Compiled);
    private static readonly Regex MapEffectFlagsRegex = new(@"flags\s*:\s*\""(?<flags>[0-9A-Fa-f]+)\""", RegexOptions.Compiled);
    private static readonly Regex MapEffectLocationRegex = new(@"location\s*:\s*\""(?<location>[^\""\\]*(?:\\.[^\""\\]*)*)\""", RegexOptions.Compiled);
    private static readonly Regex JumpRegex = new(@"(?:forcejump|jump)\s+(?:\""(?<label>[^\""\\]*(?:\\.[^\""\\]*)*)\""|'(?<label>[^'\\]*(?:\\.[^'\\]*)*)'|(?<label>\S+))", RegexOptions.Compiled);
    private static readonly Regex WindowRegex = new(@"window\s+(?<first>-?\d+(?:\.\d+)?)\s*,\s*(?<last>-?\d+(?:\.\d+)?)", RegexOptions.Compiled);
    private static readonly Regex TaggedResponseRegex = new(@"(?<kind>读条ID|结算ID)\s*(?<id>[0-9A-Fa-f]+)\s*(?<text>.*?)(?=\s*(?:[#;；]\s*)?(?:读条ID|结算ID)\s*[0-9A-Fa-f]+\b|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
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
        var sources = ParseSources(rest);
        var source = sources.Count == 1 ? sources[0] : null;
        var displayText = ApplyChineseReplacements(text, replacements);
        var mechanicHint = ParseMechanicHint(rest);
        var actionResponses = ParseActionResponses(rest, actionIds);
        var systemLogId = ParseSystemLogId(rest);
        var systemLogParam1 = ParseSystemLogParam1(rest);
        var systemLogTextHint = eventType == "SystemLogMessage" ? pendingComment : null;
        var mapEffectFlags = eventType == "MapEffect" ? ParseMapEffectFlags(rest) : null;
        var mapEffectLocation = eventType == "MapEffect" ? ParseMapEffectLocation(rest) : null;
        var isInternal = text.StartsWith("--", StringComparison.Ordinal);
        var isSync = text.Contains("sync", StringComparison.OrdinalIgnoreCase);
        var hidden = isInternal || isSync || rest.TrimStart().StartsWith('#');
        var jumpMatch = JumpRegex.Match(rest);
        var jumpRaw = jumpMatch.Success ? Unescape(jumpMatch.Groups["label"].Value) : null;
        var jumpTimeSeconds = TryParseJumpTime(jumpRaw);
        var jumpLabel = jumpTimeSeconds.HasValue ? null : jumpRaw;

        var windowMatch = WindowRegex.Match(rest);
        var windowFirst = windowMatch.Success
            ? float.TryParse(windowMatch.Groups["first"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var wf) ? wf : -2.5f
            : -2.5f;
        var windowLast = windowMatch.Success
            ? float.TryParse(windowMatch.Groups["last"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var wl) ? wl : 2.5f
            : 2.5f;

        return new TimelineEntry(timeSeconds, text, displayText, eventType, actionIds, source, sources, duration, mechanicHint, systemLogId, systemLogParam1, systemLogTextHint, mapEffectFlags, mapEffectLocation, hidden, isSync, jumpLabel, jumpTimeSeconds, actionResponses, windowFirst, windowLast);
    }

    private static float? TryParseJumpTime(string? rawJump)
    {
        if (string.IsNullOrWhiteSpace(rawJump))
            return null;

        return float.TryParse(rawJump, NumberStyles.Float, CultureInfo.InvariantCulture, out var timeSeconds)
            ? timeSeconds
            : null;
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
            foreach (Match quotedId in Regex.Matches(listMatch.Groups["ids"].Value, @"['\""`](?<id>[0-9A-Fa-f]+)['\""`]"))
                AddActionId(ids, quotedId.Groups["id"].Value);
            return ids;
        }

        var idMatch = IdRegex.Match(rest);
        if (idMatch.Success)
            AddActionId(ids, idMatch.Groups["id"].Value);

        return ids;
    }

    private static IReadOnlyList<string> ParseSources(string rest)
    {
        var sources = new List<string>();
        var listMatch = SourceListRegex.Match(rest);
        if (listMatch.Success)
        {
            foreach (Match quotedSource in Regex.Matches(listMatch.Groups["sources"].Value, @"['\""`](?<source>[^'\""`\\]*(?:\\.[^'\""`\\]*)*)['\""`]"))
                AddSource(sources, quotedSource.Groups["source"].Value);
            return sources;
        }

        var sourceMatch = SourceRegex.Match(rest);
        if (sourceMatch.Success)
            AddSource(sources, sourceMatch.Groups["source"].Value);

        return sources;
    }

    private static void AddSource(List<string> sources, string rawSource)
    {
        var source = Unescape(rawSource).Trim();
        if (!string.IsNullOrWhiteSpace(source))
            sources.Add(source);
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

    private static string? ParseMapEffectFlags(string rest)
    {
        var match = MapEffectFlagsRegex.Match(rest);
        return match.Success ? match.Groups["flags"].Value.ToUpperInvariant() : null;
    }

    private static string? ParseMapEffectLocation(string rest)
    {
        var match = MapEffectLocationRegex.Match(rest);
        return match.Success ? match.Groups["location"].Value : null;
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
        var commentIndex = rest.IndexOf('#');
        if (commentIndex < 0 || commentIndex >= rest.Length - 1)
            return null;

        var hint = RemoveTaggedResponseSegments(rest[(commentIndex + 1)..]).Trim();
        if (string.IsNullOrWhiteSpace(hint) || ContainsTaggedResponseMarker(hint) || !LooksLikeMechanicHint(hint))
            return null;

        return hint.Equals("范围", StringComparison.OrdinalIgnoreCase) ? "AOE" : hint;
    }

    private static bool LooksLikeMechanicHint(string hint)
    {
        if (Regex.IsMatch(hint, @"[\u3400-\u9FFF]"))
            return true;

        return Regex.IsMatch(hint, @"^(AOE|范围|死刑|分散|分摊|远离|靠近|背对|击退|踩塔|停止|移动)\b", RegexOptions.IgnoreCase);
    }

    private static IReadOnlyDictionary<uint, TimelineActionResponse> ParseActionResponses(string rest, IReadOnlyList<uint> actionIds)
    {
        if (actionIds.Count == 0)
            return new Dictionary<uint, TimelineActionResponse>();

        var commentIndex = rest.IndexOf('#');
        if (commentIndex < 0 || commentIndex >= rest.Length - 1)
            return new Dictionary<uint, TimelineActionResponse>();

        var rawComment = rest[(commentIndex + 1)..];
        var responses = new Dictionary<uint, TimelineActionResponse>();
        var actionIdSet = actionIds.ToHashSet();
        foreach (Match match in TaggedResponseRegex.Matches(rawComment))
        {
            if (!uint.TryParse(match.Groups["id"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var id) || !actionIdSet.Contains(id))
                continue;

            var response = NormalizeResponseText(match.Groups["text"].Value);
            if (string.IsNullOrWhiteSpace(response))
                continue;

            var timing = match.Groups["kind"].Value == "读条ID"
                ? TimelineActionResponseTiming.StartsUsing
                : TimelineActionResponseTiming.Ability;
            responses[id] = new TimelineActionResponse(response, timing);
        }

        var comment = RemoveTaggedResponseSegments(rawComment);
        var idMatches = new List<(uint Id, int Index, int Length, TimelineActionResponseTiming Timing)>();
        foreach (var actionId in actionIds)
        {
            if (responses.ContainsKey(actionId))
                continue;

            var hex = actionId.ToString("X");
            var match = Regex.Match(comment, $@"(?<kind>读条ID|结算ID)?\s*(?<![0-9A-Fa-f]){Regex.Escape(hex)}(?![0-9A-Fa-f])", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var timing = match.Groups["kind"].Value == "读条ID"
                    ? TimelineActionResponseTiming.StartsUsing
                    : TimelineActionResponseTiming.Ability;
                idMatches.Add((actionId, match.Index, match.Length, timing));
            }
        }

        if (idMatches.Count == 0)
            return responses;

        idMatches.Sort(static (left, right) => left.Index.CompareTo(right.Index));
        for (var i = 0; i < idMatches.Count; i++)
        {
            var current = idMatches[i];
            var responseStart = current.Index + current.Length;
            var responseEnd = i + 1 < idMatches.Count ? idMatches[i + 1].Index : comment.Length;
            var response = NormalizeResponseText(comment[responseStart..responseEnd]);
            if (!string.IsNullOrWhiteSpace(response))
                responses[current.Id] = new TimelineActionResponse(response, current.Timing);
        }

        return responses;
    }

    private static string NormalizeResponseText(string response)
        => response.Trim(' ', '#', '，', ',', ';', '；', '。');

    private static string RemoveTaggedResponseSegments(string comment)
        => string.Join(
            " # ",
            comment.Split('#')
                .Select(static part => part.Trim())
                .Select(static part => TaggedResponseRegex.Replace(part, string.Empty).Trim())
                .Where(static part => !ContainsTaggedResponseMarker(part))
                .Where(static part => !string.IsNullOrWhiteSpace(part)));

    private static bool ContainsTaggedResponseMarker(string text)
        => text.Contains("读条ID", StringComparison.OrdinalIgnoreCase)
           || text.Contains("结算ID", StringComparison.OrdinalIgnoreCase);

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
