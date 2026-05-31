using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

internal static class Program
{
    private static int Main(string[] args)
    {
        var options = Options.Parse(args);
        if (options == null)
        {
            PrintUsage();
            return 2;
        }

        var files = ResolveInputFiles(options).ToList();
        if (files.Count == 0)
        {
            Console.Error.WriteLine("No input files found.");
            return 1;
        }

        foreach (var file in files)
        {
            if (!TryExtract(file, options.ZoneId, out var extracted))
                continue;

            var output = ApplyChineseReplacements(extracted.Timeline, extracted.Replacements);
            output = AppendTriggerHints(output, extracted.TriggerHints);
            if (!string.IsNullOrWhiteSpace(options.OutputPath))
            {
                File.WriteAllText(options.OutputPath, output, new UTF8Encoding(false));
                Console.WriteLine($"Wrote {options.OutputPath}");
            }
            else
            {
                Console.WriteLine(output);
            }

            Console.Error.WriteLine($"Source: {file}");
            Console.Error.WriteLine($"Zone: {options.ZoneId}");
            Console.Error.WriteLine($"replaceText entries: {extracted.Replacements.Count}");
            return 0;
        }

        Console.Error.WriteLine($"No timeline found for zone {options.ZoneId}. Bundled cactbot files may not contain source timeline strings.");
        return 1;
    }

    private static IEnumerable<string> ResolveInputFiles(Options options)
    {
        if (!string.IsNullOrWhiteSpace(options.FilePath))
        {
            if (File.Exists(options.FilePath))
                yield return options.FilePath;
            yield break;
        }

        if (string.IsNullOrWhiteSpace(options.RootPath) || !Directory.Exists(options.RootPath))
            yield break;

        foreach (var pattern in new[] { "*.ts", "*.js", "*.txt" })
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(options.RootPath, pattern, SearchOption.AllDirectories);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
                yield return file;
        }
    }

    private static bool TryExtract(string file, uint zoneId, out ExtractedTimeline extracted)
    {
        extracted = default;
        var script = File.ReadAllText(file);

        if (TryExtractBundledTimeline(script, zoneId, out var bundledTimeline, out var bundledReplacements, out var bundledHints))
        {
            extracted = new ExtractedTimeline(bundledTimeline, bundledReplacements, bundledHints);
            return true;
        }

        var timelineMatches = Regex.Matches(script, @"timeline\s*:\s*`(?<timeline>[\s\S]*?)`", RegexOptions.Compiled);
        if (timelineMatches.Count == 0)
            return false;

        var zoneMarkerIndex = FindZoneMarkerIndex(script, zoneId);
        Match? selected = null;
        if (zoneMarkerIndex >= 0)
        {
            selected = timelineMatches
                .Cast<Match>()
                .Where(match => match.Index > zoneMarkerIndex)
                .OrderBy(match => match.Index - zoneMarkerIndex)
                .FirstOrDefault();
        }

        selected ??= timelineMatches.Count == 1 ? timelineMatches[0] : null;
        if (selected == null)
            return false;

        var blockStart = Math.Max(0, selected.Index - 20000);
        var blockLength = Math.Min(script.Length - blockStart, selected.Index + selected.Length + 20000 - blockStart);
        var context = script.Substring(blockStart, blockLength);
        extracted = new ExtractedTimeline(selected.Groups["timeline"].Value.Replace("\r\n", "\n"), ParseChineseReplacements(context), ParseTriggerHints(context));
        return true;
    }

    private static bool TryExtractBundledTimeline(string script, uint zoneId, out string timeline, out Dictionary<string, string> replacements, out Dictionary<string, string> triggerHints)
    {
        timeline = string.Empty;
        replacements = new Dictionary<string, string>(StringComparer.Ordinal);
        triggerHints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var zoneMarker = $"# ZoneId: {zoneId.ToString(CultureInfo.InvariantCulture)}";
        var timelineFileName = FindTimelineFileNameForZone(script, zoneId);

        foreach (Match match in Regex.Matches(script, "const\\s+(?<name>\\w+)_namespaceObject\\s*=\\s*\"(?<timeline>(?:\\\\.|[^\\\\\"])*)\"", RegexOptions.Compiled))
        {
            var candidate = Regex.Unescape(match.Groups["timeline"].Value).Replace("\r\n", "\n");
            if (!candidate.Contains(zoneMarker, StringComparison.Ordinal) && !IsTimelineFileMatch(script, match, timelineFileName))
                continue;

            timeline = NormalizeZoneMarker(candidate.Trim(), zoneId);
            var contextStart = Math.Max(0, match.Index - 50000);
            var context = script.Substring(contextStart, match.Index - contextStart);
            replacements = ParseChineseReplacements(context);
            triggerHints = ParseTriggerHints(context);
            return true;
        }

        return false;
    }

    private static string NormalizeZoneMarker(string timeline, uint zoneId)
    {
        var marker = $"# ZoneId: {zoneId.ToString(CultureInfo.InvariantCulture)}";
        if (Regex.IsMatch(timeline, @"(?m)^#\s*ZoneId\s*:\s*\S+\s*$"))
            return Regex.Replace(timeline, @"(?m)^#\s*ZoneId\s*:\s*\S+\s*$", marker);

        var lines = timeline.Replace("\r\n", "\n").Split('\n').ToList();
        var insertIndex = lines.Count > 0 && lines[0].StartsWith("###", StringComparison.Ordinal) ? 1 : 0;
        lines.Insert(insertIndex, marker);
        return string.Join("\n", lines);
    }

    private static string? FindTimelineFileNameForZone(string script, uint zoneId)
    {
        var decimalId = zoneId.ToString(CultureInfo.InvariantCulture);
        var zoneAliases = BuildZoneAliases(script);
        foreach (var zoneName in FindZoneNames(script, decimalId))
        {
            if (zoneAliases.TryGetValue(zoneName, out var fileName))
                return fileName;

            var triggerSetMatch = Regex.Match(
                script,
                $@"zoneId\s*:\s*zone_id[\s\S]{{0,120}}\.Z\.{Regex.Escape(zoneName)}[\s\S]{{0,500}}timelineFile\s*:\s*['""`](?<file>[^'""`]+)['""`]",
                RegexOptions.Compiled);
            if (triggerSetMatch.Success)
                return triggerSetMatch.Groups["file"].Value;
        }

        if (KnownZoneNames.TryGetValue(zoneId, out var knownZoneName))
        {
            if (zoneAliases.TryGetValue(knownZoneName, out var knownFileName))
                return knownFileName;

            // Fallback: find the CONCATENATED MODULE path near the zoneId reference in the bundle
            var zoneIdRef = Regex.Match(script, $@"zoneId\s*:\s*zone_id/\*\s*default\.{Regex.Escape(knownZoneName)}\s*\*/", RegexOptions.Compiled);
            if (zoneIdRef.Success)
            {
                var beforeText = script.Substring(0, zoneIdRef.Index);
                var lastModuleIdx = beforeText.LastIndexOf(";// CONCATENATED MODULE:", StringComparison.OrdinalIgnoreCase);
                if (lastModuleIdx >= 0)
                {
                    var moduleLine = beforeText.Substring(lastModuleIdx);
                    var fileMatch = Regex.Match(moduleLine, @"07-dt/raid/(?<file>[^./]+)\.txt\b", RegexOptions.Compiled);
                    if (fileMatch.Success)
                        return fileMatch.Groups["file"].Value + ".txt";
                    var tsMatch = Regex.Match(moduleLine, @"07-dt/raid/(?<file>[^./]+)\.ts\b", RegexOptions.Compiled);
                    if (tsMatch.Success)
                        return tsMatch.Groups["file"].Value + ".txt";
                }
            }
        }

        return null;
    }

    private static readonly Dictionary<uint, string> KnownZoneNames = new()
    {
        [1167] = "Ihuykatumu",
        [1196] = "WorqorLarDorExtreme",
        [1199] = "Alexandria",
        [1202] = "TheInterphos",
        [1204] = "TheStrayboroughDeadwalk",
        [1225] = "AacLightHeavyweightM1",
        [1226] = "AacLightHeavyweightM1Savage",
        [1227] = "AacLightHeavyweightM2",
        [1228] = "AacLightHeavyweightM2Savage",
        [1229] = "AacLightHeavyweightM3",
        [1230] = "AacLightHeavyweightM3Savage",
        [1231] = "AacLightHeavyweightM4",
        [1232] = "AacLightHeavyweightM4Savage",
        [1260] = "AacCruiserweightM3",
        [1262] = "AacCruiserweightM4",
        [1270] = "Recollection",
        [1372] = "ShinryusDomainUnreal",
    };

    private static Dictionary<string, string> BuildZoneAliases(string script)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(script, @"zoneId\s*:\s*zone_id/\*\s*default\.(?<zone>[A-Za-z0-9_]+)\s*\*/[\s\S]{0,120}?timelineFile\s*:\s*['""`](?<file>[^'""`]+)['""`]", RegexOptions.Compiled))
            result[match.Groups["zone"].Value] = match.Groups["file"].Value;

        return result;
    }

    private static IEnumerable<string> FindZoneNames(string script, string decimalId)
    {
        foreach (Match quotedMatch in Regex.Matches(script, $@"['""`](?<name>[A-Za-z0-9_]+)['""`]\s*:\s*{Regex.Escape(decimalId)}\b", RegexOptions.Compiled))
            yield return quotedMatch.Groups["name"].Value;

        foreach (Match bareMatch in Regex.Matches(script, $@"\b(?<name>[A-Za-z][A-Za-z0-9_]*)\s*:\s*{Regex.Escape(decimalId)}\b", RegexOptions.Compiled))
            yield return bareMatch.Groups["name"].Value;
    }

    private static bool IsTimelineFileMatch(string script, Match namespaceMatch, string? timelineFileName)
    {
        if (string.IsNullOrWhiteSpace(timelineFileName))
            return false;

        var normalizedFileName = Path.GetFileNameWithoutExtension(timelineFileName).Replace('-', '_');
        var namespaceName = namespaceMatch.Groups["name"].Value;
        if (namespaceName.EndsWith('_' + normalizedFileName, StringComparison.OrdinalIgnoreCase)
            || namespaceName.Equals(normalizedFileName, StringComparison.OrdinalIgnoreCase))
            return true;

        var contextStart = Math.Max(0, namespaceMatch.Index - 300);
        var context = script.Substring(contextStart, namespaceMatch.Index - contextStart);
        return context.Contains('/' + timelineFileName, StringComparison.OrdinalIgnoreCase)
            || context.Contains('\\' + timelineFileName, StringComparison.OrdinalIgnoreCase);
    }

    private static int FindZoneMarkerIndex(string script, uint zoneId)
    {
        var decimalId = zoneId.ToString(CultureInfo.InvariantCulture);
        var direct = Regex.Match(script, $@"zoneId\s*:\s*{Regex.Escape(decimalId)}\b");
        if (direct.Success)
            return direct.Index;

        direct = Regex.Match(script, $@"\b{Regex.Escape(decimalId)}\s*:\s*\{{");
        if (direct.Success)
            return direct.Index;

        return -1;
    }

    private static Dictionary<string, string> ParseChineseReplacements(string script)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match localeMatch in Regex.Matches(script, @"(?:locale\s*:\s*['""`]cn['""`]|['""`]locale['""`]\s*:\s*['""`]cn['""`])(?<locale>[\s\S]{0,20000}?)(?:\}\s*,\s*\{|\}\s*\])", RegexOptions.Compiled))
        {
            foreach (var sectionName in new[] { "replaceSync", "replaceText" })
            {
                var sectionMatch = Regex.Match(localeMatch.Groups["locale"].Value, $@"(?:{sectionName}|['""`]{sectionName}['""`])\s*:\s*\{{(?<body>[\s\S]*?)\}}\s*,?", RegexOptions.Compiled);
                if (!sectionMatch.Success)
                    continue;

                foreach (Match entryMatch in Regex.Matches(sectionMatch.Groups["body"].Value, @"['""`](?<from>(?:[^'""`\\]|\\.)+)['""`]\s*:\s*['""`](?<to>(?:[^'""`\\]|\\.)*)['""`]", RegexOptions.Compiled))
                {
                    var from = UnescapeJsString(entryMatch.Groups["from"].Value);
                    var to = UnescapeJsString(entryMatch.Groups["to"].Value);
                    result[from] = to;
                }
            }
        }

        return result;
    }

    private static string UnescapeJsString(string value)
    {
        return value.Replace("\\'", "'").Replace("\\\"", "\"").Replace("\\\\", "\\");
    }

    private static Dictionary<string, string> ParseTriggerHints(string script)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var triggerStarts = Regex.Matches(script, @"(?m)^\s*(?:\},\s*)?\{\s*\r?\n\s*id\s*:", RegexOptions.Compiled).Cast<Match>().ToList();
        for (var i = 0; i < triggerStarts.Count; i++)
        {
            var start = triggerStarts[i].Index;
            var end = i + 1 < triggerStarts.Count ? triggerStarts[i + 1].Index : FindTriggerSetEnd(script, start);
            if (end <= start)
                continue;

            var body = script.Substring(start, end - start);
            if (!body.Contains("netRegex", StringComparison.Ordinal) || !body.Contains("type: 'StartsUsing'", StringComparison.Ordinal))
                continue;

            var netRegexMatch = Regex.Match(body, @"netRegex\s*:\s*\{(?<body>[\s\S]*?)\}\s*,", RegexOptions.Compiled);
            if (!netRegexMatch.Success)
                continue;

            var idMatch = Regex.Match(netRegexMatch.Groups["body"].Value, @"\bid\s*:\s*['""`](?<id>[0-9A-Fa-f]+)['""`]", RegexOptions.Compiled);
            if (!idMatch.Success)
                continue;

            var hint = ParseTriggerHint(body);
            if (string.IsNullOrWhiteSpace(hint))
                continue;

            AddHint(result, idMatch.Groups["id"].Value, hint);
        }

        return result;
    }

    private static int FindTriggerSetEnd(string script, int start)
    {
        var endMatch = Regex.Match(script.Substring(start), @"(?m)^\s*\}\],", RegexOptions.Compiled);
        return endMatch.Success ? start + endMatch.Index : script.Length;
    }

    private static string? ParseTriggerHint(string body)
    {
        var responseMatch = Regex.Match(body, @"Responses\.(?<name>\w+)", RegexOptions.Compiled);
        if (responseMatch.Success)
        {
            return responseMatch.Groups["name"].Value switch
            {
                "aoe" => "AOE",
                "tankBuster" => "死刑",
                "spread" => "分散",
                "stackMarker" => "分摊",
                "stackMarkerOn" => body.Contains("targetIsYou", StringComparison.Ordinal) ? "点名分摊" : "分摊",
                "knockback" => "击退",
                "getBehind" => "去背后",
                "goFront" => "去正面",
                "awayFromFront" => "离开正面",
                _ => null,
            };
        }

        var outputKey = FindReturnedOutputKey(body);
        var cnMatch = !string.IsNullOrWhiteSpace(outputKey)
            ? Regex.Match(body, $@"\b{Regex.Escape(outputKey)}\s*:\s*\{{[\s\S]*?cn\s*:\s*['""`](?<text>[^'""`]*)['""`]", RegexOptions.Compiled)
            : Match.Empty;
        if (!cnMatch.Success)
            cnMatch = Regex.Match(body, @"cn\s*:\s*['""`](?<text>[^'""`]*)['""`]", RegexOptions.Compiled);
        return cnMatch.Success
            ? NormalizeDynamicHint(Regex.Unescape(cnMatch.Groups["text"].Value))
            : null;
    }

    private static string? FindReturnedOutputKey(string body)
    {
        var directReturns = Regex.Matches(body, @"return\s+output\.(?<key>\w+)\s*\(", RegexOptions.Compiled);
        if (directReturns.Count > 0)
            return directReturns[^1].Groups["key"].Value;

        var arrowReturns = Regex.Matches(body, @"=>\s*output\.(?<key>\w+)\s*\(", RegexOptions.Compiled);
        if (arrowReturns.Count > 0)
            return arrowReturns[^1].Groups["key"].Value;

        return null;
    }

    private static string NormalizeDynamicHint(string hint)
    {
        if (!hint.Contains("${", StringComparison.Ordinal))
            return hint;

        if (Regex.Matches(hint, @"\$\{[^}]+\}").Count >= 2)
            return "看安全格";

        if (hint.Contains("击退", StringComparison.Ordinal))
            return "击退";
        if (hint.Contains("分摊", StringComparison.Ordinal))
            return "分摊";
        if (hint.Contains("分散", StringComparison.Ordinal))
            return "分散";
        if (hint.Contains("死刑", StringComparison.Ordinal))
            return "死刑";
        if (hint.Contains("等手", StringComparison.Ordinal))
            return "等手看方向";
        return Regex.Replace(hint, @"\$\{[^}]+\}", string.Empty).Trim();
    }

    private static void AddHint(Dictionary<string, string> hints, string id, string hint)
    {
        if (!hints.TryGetValue(id, out var existing))
        {
            hints[id] = hint;
            return;
        }

        if (!existing.Split(" / ").Contains(hint, StringComparer.Ordinal))
            hints[id] = existing + " / " + hint;
    }

    private static string ApplyChineseReplacements(string timeline, IReadOnlyDictionary<string, string> replacements)
    {
        foreach (var (from, to) in replacements.OrderByDescending(static item => item.Key.Length))
        {
            try
            {
                timeline = Regex.Replace(timeline, from, to);
            }
            catch
            {
                timeline = timeline.Replace(from, to, StringComparison.Ordinal);
            }
        }

        return timeline.Trim();
    }

    private static string AppendTriggerHints(string timeline, IReadOnlyDictionary<string, string> triggerHints)
    {
        if (triggerHints.Count == 0)
            return timeline.Trim();

        var lines = timeline.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.Contains(" # ", StringComparison.Ordinal) || line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                continue;

            var ids = ExtractTimelineIds(line).ToList();
            var matchedHints = ids
                .Select(id => (Id: id, Hint: triggerHints.TryGetValue(id, out var hint) ? hint : null))
                .Where(match => !string.IsNullOrWhiteSpace(match.Hint))
                .Select(match => (match.Id, Hint: match.Hint!))
                .ToList();
            if (matchedHints.Count == 0)
                continue;

            lines[i] = line + " # " + string.Join(" # ", matchedHints.Select(match => $"读条ID {match.Id.ToUpperInvariant()} {NormalizeSingleHint(match.Hint)}"));
        }

        return string.Join("\n", lines).Trim();
    }

    private static string NormalizeSingleHint(string hint)
    {
        hint = hint.Replace(" => ", "再", StringComparison.Ordinal).Replace("=>", "再", StringComparison.Ordinal);
        return hint.Trim(' ', '+', '-', '>', '=', ':', '：');
    }

    private static IEnumerable<string> ExtractTimelineIds(string line)
    {
        var idPropertyMatch = Regex.Match(line, @"\bid\s*:\s*(?<value>\[[^\]]+\]|['""`][^'""`]+['""`])", RegexOptions.Compiled);
        if (!idPropertyMatch.Success)
            yield break;

        foreach (Match idMatch in Regex.Matches(idPropertyMatch.Groups["value"].Value, @"['""`](?<id>[0-9A-Fa-f]+)['""`]", RegexOptions.Compiled))
            yield return idMatch.Groups["id"].Value;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  CactbotTimelineExtractor --zone <id> --file <raidboss file> [--out <path>]");
        Console.Error.WriteLine("  CactbotTimelineExtractor --zone <id> --root <cactbot root> [--out <path>]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Example:");
        Console.Error.WriteLine("  dotnet run --project tools/CactbotTimelineExtractor -- --zone 1345 --file D:\\ff14act\\Plugins\\ACT.OverlayPlugin\\cactbot\\ui\\raidboss\\raidboss.bundle.js");
    }

    private readonly record struct ExtractedTimeline(string Timeline, Dictionary<string, string> Replacements, Dictionary<string, string> TriggerHints);

    private sealed record Options(uint ZoneId, string? FilePath, string? RootPath, string? OutputPath)
    {
        public static Options? Parse(string[] args)
        {
            uint zoneId = 0;
            string? file = null;
            string? root = null;
            string? output = null;
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                string? Next() => i + 1 < args.Length ? args[++i] : null;
                switch (arg)
                {
                    case "--zone":
                        var zoneText = Next();
                        if (!uint.TryParse(zoneText, NumberStyles.Integer, CultureInfo.InvariantCulture, out zoneId))
                            return null;
                        break;
                    case "--file":
                        file = Next();
                        break;
                    case "--root":
                        root = Next();
                        break;
                    case "--out":
                        output = Next();
                        break;
                    default:
                        return null;
                }
            }

            if (zoneId == 0 || (string.IsNullOrWhiteSpace(file) && string.IsNullOrWhiteSpace(root)))
                return null;

            return new Options(zoneId, file, root, output);
        }
    }
}
