using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class Program
{
    private const string DefaultHistoryPath = @"C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\DalamudACT\history-records.json";
    private const string DefaultActLogDirectory = @"D:\ff14act\FFXIVLogs";
    private const string DefaultDalamudLogPath = @"C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\dalamud.log";
    private const uint InvalidActorId = 0xE0000000;
    private static readonly TimeSpan ExperimentalWhmDiaDuration = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ExperimentalStatusApplyDedupWindow = TimeSpan.FromMilliseconds(500);
    private static readonly HashSet<uint> ExperimentalWhmDiaStatusIds = [0x74F, 0x7F3];

    private static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly Dictionary<string, string> JobAliasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["whm"] = "whm",
        ["白魔"] = "whm",
        ["白魔法师"] = "whm",

        ["sge"] = "sge",
        ["贤者"] = "sge",

        ["sch"] = "sch",
        ["学者"] = "sch",

        ["ast"] = "ast",
        ["占星"] = "ast",
        ["占星术士"] = "ast",

        ["pld"] = "pld",
        ["骑士"] = "pld",
        ["圣骑士"] = "pld",

        ["war"] = "war",
        ["战士"] = "war",

        ["drk"] = "drk",
        ["暗黑骑士"] = "drk",
        ["黑骑"] = "drk",

        ["gnb"] = "gnb",
        ["绝枪"] = "gnb",
        ["绝枪战士"] = "gnb",
        ["gunbreaker"] = "gnb",

        ["mnk"] = "mnk",
        ["武僧"] = "mnk",
        ["僧"] = "mnk",

        ["drg"] = "drg",
        ["龙骑"] = "drg",
        ["龙骑士"] = "drg",

        ["nin"] = "nin",
        ["忍者"] = "nin",

        ["sam"] = "sam",
        ["武士"] = "sam",
        ["武"] = "sam",

        ["brd"] = "brd",
        ["诗人"] = "brd",
        ["吟游诗人"] = "brd",
        ["bard"] = "brd",

        ["mch"] = "mch",
        ["机工"] = "mch",
        ["机工士"] = "mch",
        ["machinist"] = "mch",

        ["dnc"] = "dnc",
        ["舞者"] = "dnc",

        ["blm"] = "blm",
        ["黑魔"] = "blm",
        ["黑魔法师"] = "blm",

        ["smn"] = "smn",
        ["召唤"] = "smn",
        ["召唤师"] = "smn",

        ["rdm"] = "rdm",
        ["赤魔"] = "rdm",
        ["赤魔法师"] = "rdm",

        ["rpr"] = "rpr",
        ["钐镰客"] = "rpr",
        ["镰刀"] = "rpr",

        ["vpr"] = "vpr",
        ["蝰蛇"] = "vpr",

        ["pct"] = "pct",
        ["画家"] = "pct",
        ["绘灵法师"] = "pct",
        ["pictomancer"] = "pct",
    };

    private static readonly HashSet<uint> DefaultExcludedStatusIds =
    [
        0x35D, // 野火。当前插件 dotDamage-* 不计入它，默认排除以便和插件口径更接近。
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<uint>> KnownDotStatusIdsByJob =
        new Dictionary<string, IReadOnlySet<uint>>(StringComparer.OrdinalIgnoreCase)
        {
            ["pld"] = new HashSet<uint> { 0xF8 }, // 厄运流转
            ["drk"] = new HashSet<uint> { 0x2ED }, // 腐秽大地。源自挂 / 地面 DoT，通常不出现在敌方 26| 状态窗口里。
            ["gnb"] = new HashSet<uint> { 0x72D, 0x72E }, // 音速破 / 弓形冲波

            ["whm"] = new HashSet<uint> { 0x8F, 0x90, 0x74F, 0x7F3 }, // 疾风 / 烈风 / 天辉
            ["sch"] = new HashSet<uint> { 0xB3, 0xBD, 0x767, 0x7F7, 0xC11, 0xF2B }, // 毒菌 / 蛊毒法 / 埋伏之毒
            ["ast"] = new HashSet<uint> { 0x346, 0x34B, 0x759, 0x7F9 }, // 烧灼 / 炽灼 / 焚灼
            ["sge"] = new HashSet<uint> { 0xA36, 0xA37, 0xA38, 0xB30, 0xC24, 0xF28, 0xF39, 0xF88 }, // 均衡注药 / 均衡失衡

            ["drg"] = new HashSet<uint> { 0x76, 0x520, 0xA9F }, // 樱花怒放 / 樱花缭乱
            ["nin"] = new HashSet<uint> { 0xF09, 0xF42 }, // 介毒之术 / 百雷铳
            ["sam"] = new HashSet<uint> { 0x4CC, 0x527 }, // 彼岸花

            ["brd"] = new HashSet<uint> { 0x7C, 0x81, 0x4B0, 0x4B1, 0x529, 0x52A }, // 毒咬箭 / 风蚀箭等
            ["mch"] = new HashSet<uint> { 0x74A, 0x7E3 }, // 毒菌冲击；0x35D 野火另走特殊口径

            ["blm"] = new HashSet<uint> { 0xA1, 0xA2, 0xA3, 0x4BA, 0xF1F, 0xF20 }, // 雷系 DoT
            ["smn"] = new HashSet<uint> { 0xA92, 0xC99, 0xC9F }, // Slipstream / Scarlet Flame
        };

    private static readonly IReadOnlyDictionary<uint, string> KnownDotStatusNames = new Dictionary<uint, string>
    {
        [0xF8] = "厄运流转",
        [0x2ED] = "腐秽大地",
        [0x72D] = "音速破",
        [0x72E] = "弓形冲波",
        [0x8F] = "疾风",
        [0x90] = "烈风",
        [0x74F] = "天辉",
        [0x7F3] = "天辉",
        [0xB3] = "毒菌",
        [0xBD] = "猛毒菌",
        [0x767] = "蛊毒法",
        [0x7F7] = "蛊毒法",
        [0xC11] = "埋伏之毒",
        [0x346] = "烧灼",
        [0x34B] = "炽灼",
        [0x759] = "焚灼",
        [0xA36] = "均衡注药",
        [0xA37] = "均衡注药",
        [0xA38] = "均衡注药",
        [0xB30] = "均衡失衡",
        [0x76] = "樱花怒放",
        [0x520] = "樱花缭乱",
        [0x4CC] = "彼岸花",
        [0x527] = "彼岸花",
        [0x7C] = "毒咬箭",
        [0x81] = "风蚀箭",
        [0x4B0] = "烈毒咬箭",
        [0x4B1] = "狂风蚀箭",
        [0x529] = "烈毒咬箭",
        [0x52A] = "狂风蚀箭",
        [0x74A] = "毒菌冲击",
        [0x7E3] = "毒菌冲击",
        [0xA1] = "闪雷",
        [0xA2] = "震雷",
        [0xA3] = "暴雷",
        [0x4BA] = "霹雷",
        [0xF1F] = "高闪雷",
        [0xF20] = "高震雷",
        [0xA92] = "螺旋气流",
        [0xC99] = "猩红旋风",
        [0xC9F] = "烈日核爆",
    };

    public static int Main(string[] args)
    {
        var options = Options.Parse(args);
        if (options == null)
        {
            PrintUsage();
            return 1;
        }

        var historyPath = string.IsNullOrWhiteSpace(options.HistoryPath) ? DefaultHistoryPath : options.HistoryPath!;
        if (!File.Exists(historyPath))
        {
            Console.Error.WriteLine($"未找到 history 文件：{historyPath}");
            return 1;
        }

        HistoryExportPayload payload;
        try
        {
            payload = LoadHistoryPayload(historyPath);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"读取 history 失败：{ex.Message}");
            return 1;
        }

        if (payload.Records.Count == 0)
        {
            Console.Error.WriteLine("history-records.json 里没有可用记录。");
            return 1;
        }

        var encounter = SelectEncounter(payload.Records, options.Zone);
        if (encounter == null)
        {
            Console.Error.WriteLine(options.Zone == null
                ? "没有找到可用战斗记录。"
                : $"没有找到副本名包含“{options.Zone}”的战斗记录。");
            return 1;
        }

        var allPlayers = BuildPlayerEntries(encounter);
        var players = ApplyPlayerFilters(allPlayers, options);

        if (players.Count == 0)
        {
            Console.Error.WriteLine("按当前过滤条件没有选中任何玩家。");
            Console.Error.WriteLine("可尝试去掉 --jobs / --players，或先看最新记录里有哪些职业与玩家。");
            return 1;
        }

        var excludedStatusIds = options.IncludeSpecialDot
            ? new HashSet<uint>()
            : DefaultExcludedStatusIds;

        var logPaths = ResolveActLogPaths(options, encounter);
        var actAggregation = AggregateActHostileDot(logPaths, encounter, excludedStatusIds);
        var experimentalReconstructions = BuildExperimentalReconstructions(allPlayers, actAggregation);
        var results = BuildResults(players, actAggregation, experimentalReconstructions);
        var statusWindowSummaries = BuildStatusWindowSummaries(players, actAggregation);
        var dotWindowConsistencyResults = BuildDotWindowConsistencyResults(results, statusWindowSummaries);
        var dalamudLogPaths = ResolveDalamudLogPaths(options);
        var dotDiagnosticSummaries = string.IsNullOrWhiteSpace(options.CsvDotDiagnosticOutPath)
            ? new List<DotDiagnosticSummary>()
            : BuildDotDiagnosticSummaries(dalamudLogPaths, encounter);

        PrintEncounterHeader(historyPath, encounter, options, logPaths, excludedStatusIds, actAggregation);
        PrintResults(results, options.TopStatusCount);
        if (options.ShowStatusWindows)
        {
            PrintStatusWindowSummaries(statusWindowSummaries);
            PrintDotWindowConsistencyResults(dotWindowConsistencyResults);
        }

        PrintAggregateFooter(results, actAggregation, options);

        WriteExports(options, historyPath, encounter, logPaths, dalamudLogPaths, excludedStatusIds, actAggregation, results, statusWindowSummaries, dotWindowConsistencyResults, dotDiagnosticSummaries);
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("用法：");
        Console.WriteLine("  dotnet run --project tools/DotReconcile -- [--history <path>] [--log <path>] [--act-log-dir <dir>] [--dalamud-log <path>] [--latest] [--zone <text>] [--jobs <csv>] [--players <csv>] [--top-status <n>] [--status-windows] [--include-special-dot] [--summary-out <path>] [--json-out <path>] [--csv-out <path>] [--csv-status-out <path>] [--csv-windowcheck-out <path>] [--csv-known-dot-out <path>] [--csv-dotdiagnostic-out <path>]");
        Console.WriteLine();
        Console.WriteLine("示例：");
        Console.WriteLine("  dotnet run --project tools/DotReconcile -- --latest --jobs whm,sge");
        Console.WriteLine("  dotnet run --project tools/DotReconcile -- --zone 缇坦妮雅 --players 阳介,在爱锈蚀之前 --top-status 5");
        Console.WriteLine("  dotnet run --project tools/DotReconcile -- --latest --summary-out output\\dotreconcile-summary.csv --json-out output\\dotreconcile.json --csv-out output\\dotreconcile.csv --csv-status-out output\\dotreconcile-status.csv --csv-windowcheck-out output\\dotreconcile-windowcheck.csv --csv-known-dot-out output\\dotreconcile-known-dot.csv --csv-dotdiagnostic-out output\\dotreconcile-dotdiagnostic.csv");
        Console.WriteLine();
        Console.WriteLine("说明：");
        Console.WriteLine("  - 默认 history 路径：C:\\Users\\Administrator\\AppData\\Roaming\\XIVLauncherCN\\pluginConfigs\\DalamudACT\\history-records.json");
        Console.WriteLine("  - 默认 ACT 日志目录：D:\\ff14act\\FFXIVLogs");
        Console.WriteLine("  - ACT 口径默认只统计 hostile-only DoT：24|DoT 且目标 actorId 以 4 开头");
        Console.WriteLine("  - 玩家结果默认只统计 ACT 里 source 能归到玩家的那部分；如果扫描统计里出现“未归属 hostile DoT”，则下方玩家 ACT 值应视为下限");
        Console.WriteLine("  - 默认排除 status=35D（野火），因为插件 dotDamage-* 不计入它；如需包含，可加 --include-special-dot");
        Console.WriteLine("  - --top-status 0 表示不在终端打印 statusId 明细");
        Console.WriteLine("  - --status-windows 会额外打印 ACT 26| 状态应用摘要，方便解释 status=0 的 DoT tick");
        Console.WriteLine("  - --summary-out 会写出短汇总；扩展名为 .json 时写 JSON，否则写单行 CSV");
        Console.WriteLine("  - --csv-known-dot-out 会按职业已知 DoT 状态导出专项核对表");
        Console.WriteLine("  - --csv-dotdiagnostic-out 会读取 dalamud.log / dalamud.old.log 的 DOT诊断 Tick，并合并 ACT 状态输出总表");
    }

    private static HistoryExportPayload LoadHistoryPayload(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<HistoryExportPayload>(json, JsonReadOptions)
               ?? new HistoryExportPayload();
    }

    private static HistoricalCombatData? SelectEncounter(IEnumerable<HistoricalCombatData> records, string? zoneFilter)
    {
        var filtered = records
            .Where(static record => record.Snapshot?.Msg?.Combatant?.Count > 0)
            .Where(record =>
                string.IsNullOrWhiteSpace(zoneFilter)
                || (!string.IsNullOrWhiteSpace(record.ZoneName)
                    && record.ZoneName.Contains(zoneFilter, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(GetEncounterSortTime)
            .ToList();

        return filtered.FirstOrDefault();
    }

    private static DateTimeOffset GetEncounterSortTime(HistoricalCombatData record)
        => record.EndTimeUtc
           ?? record.StartTimeUtc
           ?? DateTimeOffset.MinValue;

    private static List<PlayerEncounterEntry> BuildPlayerEntries(HistoricalCombatData encounter)
    {
        var result = new List<PlayerEncounterEntry>();
        var combatants = encounter.Snapshot?.Msg?.Combatant;
        if (combatants == null)
            return result;

        foreach (var pair in combatants)
        {
            var combatant = pair.Value;
            if (combatant == null)
                continue;

            if (!string.Equals(combatant.ParticipantKind, "player", StringComparison.OrdinalIgnoreCase))
                continue;

            var name = !string.IsNullOrWhiteSpace(combatant.Name)
                ? combatant.Name!.Trim()
                : ExtractNameFromCombatantKey(pair.Key);

            if (string.IsNullOrWhiteSpace(name))
                continue;

            var actorId = TryParseActorIdFromCombatantKey(pair.Key, out var parsedActorId)
                ? parsedActorId
                : (uint?)null;

            var pluginDotDamage = ParseDisplayedAmount(combatant.DotDamageText);

            result.Add(new PlayerEncounterEntry(
                pair.Key,
                name,
                combatant.Job?.Trim() ?? string.Empty,
                actorId,
                pluginDotDamage));
        }

        return result;
    }

    private static List<PlayerEncounterEntry> ApplyPlayerFilters(List<PlayerEncounterEntry> players, Options options)
    {
        IEnumerable<PlayerEncounterEntry> filtered = players;

        if (options.PlayerFilters.Count > 0)
        {
            filtered = filtered.Where(player => options.PlayerFilters.Contains(player.Name));
        }

        if (options.JobFilters.Count > 0)
        {
            filtered = filtered.Where(player => options.JobFilters.Contains(CanonicalizeJob(player.Job)));
        }

        return filtered
            .OrderByDescending(static player => player.PluginDotDamage)
            .ThenBy(static player => player.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ResolveActLogPaths(Options options, HistoricalCombatData encounter)
    {
        if (options.LogPaths.Count > 0)
        {
            return options.LogPaths
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var directory = string.IsNullOrWhiteSpace(options.ActLogDirectory)
            ? DefaultActLogDirectory
            : options.ActLogDirectory!;

        if (!Directory.Exists(directory))
            return [];

        var candidates = Directory.EnumerateFiles(directory, "Network_*.log*")
            .Select(path => new FileInfo(path))
            .Where(file => file.Exists && file.Length > 0)
            .ToList();

        if (candidates.Count == 0)
            return [];

        var targetTokens = BuildDateSearchTokens(encounter).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matched = candidates
            .Where(file => targetTokens.Count == 0
                           || targetTokens.Any(token => file.Name.Contains(token, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(static file => file.LastWriteTimeUtc)
            .ThenByDescending(static file => file.Length)
            .Select(static file => file.FullName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        if (matched.Count > 0)
            return matched;

        return candidates
            .OrderByDescending(static file => file.LastWriteTimeUtc)
            .ThenByDescending(static file => file.Length)
            .Select(static file => file.FullName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
    }

    private static List<string> ResolveDalamudLogPaths(Options options)
    {
        if (options.DalamudLogPaths.Count > 0)
        {
            return options.DalamudLogPaths
                .Where(File.Exists)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var defaultPath = DefaultDalamudLogPath;
        var directory = Path.GetDirectoryName(defaultPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return File.Exists(defaultPath) ? [defaultPath] : [];

        var candidates = Directory.EnumerateFiles(directory, "dalamud*.log")
            .Select(path => new FileInfo(path))
            .Where(static file => file.Exists && file.Length > 0)
            .OrderByDescending(static file => file.LastWriteTimeUtc)
            .ThenByDescending(static file => file.Length)
            .Select(static file => file.FullName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        if (candidates.Count > 0)
            return candidates;

        return File.Exists(defaultPath) ? [defaultPath] : [];
    }

    private static IEnumerable<string> BuildDateSearchTokens(HistoricalCombatData encounter)
    {
        foreach (var value in new[] { encounter.StartTimeUtc, encounter.EndTimeUtc })
        {
            if (!value.HasValue)
                continue;

            var local = value.Value.ToLocalTime();
            yield return local.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            yield return local.ToString("yyyy.MM.dd", CultureInfo.InvariantCulture);
        }
    }

    private static ActAggregationResult AggregateActHostileDot(
        IReadOnlyList<string> logPaths,
        HistoricalCombatData encounter,
        IReadOnlySet<uint> excludedStatusIds)
    {
        var result = new ActAggregationResult();
        var (startUtc, endUtc) = ResolveEncounterWindow(encounter);
        result.EncounterStartUtc = startUtc;
        result.EncounterEndUtc = endUtc;

        foreach (var logPath in logPaths)
        {
            ScanSingleActLog(logPath, startUtc, endUtc, excludedStatusIds, result);
        }

        return result;
    }

    private static void ScanSingleActLog(
        string logPath,
        DateTimeOffset? startUtc,
        DateTimeOffset? endUtc,
        IReadOnlySet<uint> excludedStatusIds,
        ActAggregationResult result)
    {
        if (!File.Exists(logPath))
            return;

        var matchedThisFile = false;

        try
        {
            using var stream = new FileStream(
                logPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                result.TotalLines++;

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (line.StartsWith("24|", StringComparison.Ordinal))
                {
                    if (!line.Contains("|DoT|", StringComparison.Ordinal))
                        continue;

                    result.DotEventLines++;

                    var parts = line.Split('|');
                    if (parts.Length <= 18)
                    {
                        result.ParseFailures++;
                        continue;
                    }

                    if (!string.Equals(parts[4], "DoT", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!DateTimeOffset.TryParse(parts[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
                    {
                        result.ParseFailures++;
                        continue;
                    }

                    var timestampUtc = timestamp.ToUniversalTime();
                    if (startUtc.HasValue && timestampUtc < startUtc.Value)
                        continue;

                    if (endUtc.HasValue && timestampUtc > endUtc.Value)
                    {
                        if (matchedThisFile)
                            break;

                        continue;
                    }

                    result.DotEventLinesInEncounterWindow++;
                    matchedThisFile = true;

                    if (!TryParseHexOrDecimal(parts[2], out var targetId))
                    {
                        result.ParseFailures++;
                        continue;
                    }

                    if (!IsHostileActorId(targetId))
                    {
                        result.NonHostileTargetLines++;
                        continue;
                    }

                    if (!TryParseHexOrDecimal(parts[5], out var statusId))
                        statusId = 0;

                    if (excludedStatusIds.Contains(statusId))
                    {
                        result.ExcludedStatusLines++;
                        continue;
                    }

                    if (!TryParseHexOrDecimal(parts[6], out var damage))
                    {
                        result.ParseFailures++;
                        continue;
                    }

                    if (!TryParseHexOrDecimal(parts[17], out var sourceId))
                        sourceId = 0;

                    var targetName = parts[3].Trim();
                    var sourceName = parts[18].Trim();
                    result.HostileDotLines++;
                    result.HostileDotEvents.Add(new ActDotEvent(timestamp, targetId, targetName, statusId, damage, sourceId, sourceName));

                    if (IsUnresolvedHostileDotSource(targetId, targetName, sourceId, sourceName))
                    {
                        result.UnresolvedHostileDotLines++;
                        result.UnresolvedHostileDotDamage += damage;

                        if (IsMissingHostileDotSource(sourceId, sourceName))
                        {
                            result.MissingSourceHostileDotLines++;
                            result.MissingSourceHostileDotDamage += damage;
                        }
                        else
                        {
                            result.HostileOrSelfSourcedDotLines++;
                            result.HostileOrSelfSourcedDotDamage += damage;
                        }

                        continue;
                    }

                    result.ResolvedHostileDotLines++;
                    result.ResolvedHostileDotDamage += damage;

                    if (sourceId != 0)
                    {
                        result.DamageBySourceId[sourceId] = result.DamageBySourceId.GetValueOrDefault(sourceId) + damage;
                        AddStatusAggregate(result.StatusBySourceId, sourceId, statusId, damage);
                    }

                    if (!string.IsNullOrWhiteSpace(sourceName))
                    {
                        result.DamageBySourceName[sourceName] = result.DamageBySourceName.GetValueOrDefault(sourceName) + damage;
                        AddStatusAggregate(result.StatusBySourceName, sourceName, statusId, damage);
                    }

                    continue;
                }

                if (!line.StartsWith("26|", StringComparison.Ordinal))
                    continue;

                var statusApplyParts = line.Split('|');
                if (statusApplyParts.Length <= 8)
                {
                    result.ParseFailures++;
                    continue;
                }

                if (!DateTimeOffset.TryParse(statusApplyParts[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var statusApplyTimestamp))
                {
                    result.ParseFailures++;
                    continue;
                }

                var statusApplyTimestampUtc = statusApplyTimestamp.ToUniversalTime();
                if (startUtc.HasValue && statusApplyTimestampUtc < startUtc.Value)
                    continue;

                if (endUtc.HasValue && statusApplyTimestampUtc > endUtc.Value)
                {
                    if (matchedThisFile)
                        break;

                    continue;
                }

                matchedThisFile = true;

                if (!TryParseHexOrDecimal(statusApplyParts[2], out var statusApplyId))
                {
                    result.ParseFailures++;
                    continue;
                }

                if (!TryParseHexOrDecimal(statusApplyParts[5], out var statusApplySourceId)
                    || !TryParseHexOrDecimal(statusApplyParts[7], out var statusApplyTargetId))
                {
                    result.ParseFailures++;
                    continue;
                }

                if (!IsHostileActorId(statusApplyTargetId))
                    continue;

                result.StatusApplyEvents.Add(new ActStatusApplyEvent(
                    statusApplyTimestamp,
                    statusApplyId,
                    statusApplyParts[3].Trim(),
                    statusApplySourceId,
                    statusApplyParts[6].Trim(),
                    statusApplyTargetId,
                    statusApplyParts[8].Trim()));
            }
        }
        catch (IOException ex)
        {
            result.LogReadErrors.Add($"{logPath} | {ex.Message}");
            return;
        }

        if (matchedThisFile)
        {
            result.LogsWithEncounterData.Add(logPath);
        }
    }

    private static void AddStatusAggregate<TKey>(
        Dictionary<TKey, Dictionary<uint, DotStatusAggregate>> sourceMap,
        TKey sourceKey,
        uint statusId,
        uint damage)
        where TKey : notnull
    {
        if (!sourceMap.TryGetValue(sourceKey, out var statusMap))
        {
            statusMap = [];
            sourceMap[sourceKey] = statusMap;
        }

        if (!statusMap.TryGetValue(statusId, out var aggregate))
        {
            aggregate = new DotStatusAggregate();
            statusMap[statusId] = aggregate;
        }

        aggregate.Damage += damage;
        aggregate.EventCount++;
    }

    private static bool IsUnresolvedHostileDotSource(uint targetId, string? targetName, uint sourceId, string? sourceName)
    {
        if (IsMissingHostileDotSource(sourceId, sourceName))
            return true;

        if (sourceId != 0)
        {
            if (sourceId == targetId)
                return true;

            if (IsHostileActorId(sourceId))
                return true;
        }

        var normalizedTargetName = targetName?.Trim();
        var normalizedSourceName = sourceName?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedTargetName)
            && !string.IsNullOrWhiteSpace(normalizedSourceName)
            && string.Equals(normalizedTargetName, normalizedSourceName, StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static bool IsMissingHostileDotSource(uint sourceId, string? sourceName)
        => (sourceId == 0 || sourceId == InvalidActorId) && string.IsNullOrWhiteSpace(sourceName);

    private static (DateTimeOffset? StartUtc, DateTimeOffset? EndUtc) ResolveEncounterWindow(HistoricalCombatData encounter)
    {
        var start = encounter.StartTimeUtc;
        var end = encounter.EndTimeUtc;

        if (!start.HasValue && end.HasValue && TryParseEncounterDuration(encounter.Duration, out var durationFromEnd))
            start = end.Value - durationFromEnd;

        if (!end.HasValue && start.HasValue && TryParseEncounterDuration(encounter.Duration, out var durationFromStart))
            end = start.Value + durationFromStart;

        return (start?.ToUniversalTime(), end?.ToUniversalTime());
    }

    private static bool TryParseEncounterDuration(string? text, out TimeSpan duration)
    {
        duration = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        var segments = trimmed.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 2
            && int.TryParse(segments[0], out var minutes)
            && int.TryParse(segments[1], out var seconds))
        {
            duration = new TimeSpan(0, minutes, seconds);
            return true;
        }

        if (segments.Length == 3
            && int.TryParse(segments[0], out var hours)
            && int.TryParse(segments[1], out minutes)
            && int.TryParse(segments[2], out seconds))
        {
            duration = new TimeSpan(hours, minutes, seconds);
            return true;
        }

        return TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out duration);
    }

    private static IReadOnlyList<DotDiagnosticSummary> BuildDotDiagnosticSummaries(
        IReadOnlyList<string> dalamudLogPaths,
        HistoricalCombatData encounter)
    {
        if (dalamudLogPaths.Count == 0)
            return [];

        var (startUtc, endUtc) = ResolveEncounterWindow(encounter);
        var aggregates = new Dictionary<DotDiagnosticKey, DotDiagnosticAggregate>();

        foreach (var logPath in dalamudLogPaths)
        {
            ScanSingleDalamudLogForDotDiagnostics(logPath, startUtc, endUtc, aggregates);
        }

        return aggregates.Values
            .Select(static aggregate => aggregate.ToSummary())
            .OrderBy(static summary => summary.SourceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static summary => summary.ActionName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static summary => summary.StatusId)
            .ToList();
    }

    private static void ScanSingleDalamudLogForDotDiagnostics(
        string logPath,
        DateTimeOffset? startUtc,
        DateTimeOffset? endUtc,
        Dictionary<DotDiagnosticKey, DotDiagnosticAggregate> aggregates)
    {
        if (!File.Exists(logPath))
            return;

        var matchedThisFile = false;
        try
        {
            using var stream = new FileStream(
                logPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!line.Contains("DOT诊断：补算Tick：", StringComparison.Ordinal))
                    continue;

                if (!TryParseDalamudLogTimestamp(line, out var timestamp))
                    continue;

                var timestampUtc = timestamp.ToUniversalTime();
                if (startUtc.HasValue && timestampUtc < startUtc.Value)
                    continue;

                if (endUtc.HasValue && timestampUtc > endUtc.Value)
                {
                    if (matchedThisFile)
                        break;

                    continue;
                }

                matchedThisFile = true;
                if (!TryParseDotDiagnosticTick(line, timestamp, out var tick))
                    continue;

                var key = new DotDiagnosticKey(
                    tick.SourceId,
                    tick.SourceName,
                    tick.ActionId,
                    tick.StatusId);

                if (!aggregates.TryGetValue(key, out var aggregate))
                {
                    aggregate = new DotDiagnosticAggregate(
                        tick.SourceName,
                        tick.SourceId,
                        tick.ActionName,
                        tick.ActionId,
                        tick.StatusName,
                        tick.StatusId);
                    aggregates[key] = aggregate;
                }

                aggregate.AddTick(tick);
            }
        }
        catch (IOException)
        {
            // dalamud.log 可能正被启动器轮转或写入。离线诊断导出只作为辅助，不让读取失败中断 ACT 对账。
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static bool TryParseDalamudLogTimestamp(string line, out DateTimeOffset timestamp)
    {
        timestamp = default;
        var timestampEnd = line.IndexOf(" [", StringComparison.Ordinal);
        if (timestampEnd <= 0)
            return false;

        var timestampText = line[..timestampEnd].Trim();
        return DateTimeOffset.TryParse(
            timestampText,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces,
            out timestamp);
    }

    private static bool TryParseDotDiagnosticTick(
        string line,
        DateTimeOffset timestamp,
        out DotDiagnosticTick tick)
    {
        tick = default!;
        const string marker = "DOT诊断：补算Tick：";
        var markerIndex = line.IndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0)
            return false;

        var payload = line[(markerIndex + marker.Length)..].Trim().TrimEnd('。');
        var fields = payload
            .Split('，', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static field =>
            {
                var separatorIndex = field.IndexOf('=');
                return separatorIndex <= 0
                    ? new KeyValuePair<string, string>(field, string.Empty)
                    : new KeyValuePair<string, string>(field[..separatorIndex].Trim(), field[(separatorIndex + 1)..].Trim());
            })
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Key))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        if (!fields.TryGetValue("source", out var sourceText)
            || !fields.TryGetValue("target", out var targetText)
            || !fields.TryGetValue("action", out var actionText)
            || !fields.TryGetValue("status", out var statusText)
            || !fields.TryGetValue("amount", out var amountText)
            || !fields.TryGetValue("crit", out var critText)
            || !fields.TryGetValue("tick", out var tickIndexText))
        {
            return false;
        }

        if (!TryParseSlashHexRef(sourceText, out var sourceName, out var sourceId)
            || !TryParseSlashHexRef(targetText, out var targetName, out var targetId)
            || !TryParseBracketRef(actionText, out var actionName, out var actionId)
            || !TryParseSlashHexRef(statusText, out var statusName, out var statusId)
            || !long.TryParse(amountText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount)
            || !int.TryParse(tickIndexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var tickIndex))
        {
            return false;
        }

        var crit = bool.TryParse(critText, out var parsedCrit) && parsedCrit;
        tick = new DotDiagnosticTick(
            timestamp,
            sourceName,
            sourceId,
            targetName,
            targetId,
            actionName,
            actionId,
            statusName,
            statusId,
            amount,
            crit,
            tickIndex);
        return true;
    }

    private static bool TryParseSlashHexRef(string text, out string name, out uint id)
    {
        name = string.Empty;
        id = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        var slashIndex = trimmed.LastIndexOf("/0x", StringComparison.OrdinalIgnoreCase);
        if (slashIndex >= 0)
        {
            name = trimmed[..slashIndex].Trim();
            return TryParseHexOrDecimal(trimmed[(slashIndex + 1)..], out id);
        }

        if (TryParseHexOrDecimal(trimmed, out id))
            return true;

        return false;
    }

    private static bool TryParseBracketRef(string text, out string name, out uint id)
    {
        name = string.Empty;
        id = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        var openIndex = trimmed.LastIndexOf('[');
        var closeIndex = trimmed.LastIndexOf(']');
        if (openIndex < 0 || closeIndex <= openIndex)
            return false;

        name = trimmed[..openIndex].Trim();
        var idText = trimmed[(openIndex + 1)..closeIndex].Trim();
        return uint.TryParse(idText, NumberStyles.Integer, CultureInfo.InvariantCulture, out id)
               || TryParseHexOrDecimal(idText, out id);
    }

    private static IReadOnlyDictionary<uint, ExperimentalPlayerReconstruction> BuildExperimentalReconstructions(
        IReadOnlyList<PlayerEncounterEntry> allPlayers,
        ActAggregationResult aggregation)
    {
        var whmPlayers = allPlayers
            .Where(static player => player.ActorId.HasValue)
            .Where(player => CanonicalizeJob(player.Job) == "whm")
            .GroupBy(static player => player.ActorId!.Value)
            .ToDictionary(static group => group.Key, static group => group.First());

        if (whmPlayers.Count == 0
            || aggregation.StatusApplyEvents.Count == 0
            || aggregation.HostileDotEvents.Count == 0)
        {
            return new Dictionary<uint, ExperimentalPlayerReconstruction>();
        }

        var windows = BuildExperimentalWhmDiaWindows(whmPlayers.Keys.ToHashSet(), aggregation);
        if (windows.Count == 0)
            return new Dictionary<uint, ExperimentalPlayerReconstruction>();

        var builders = windows
            .GroupBy(static window => window.OwnerActorId)
            .ToDictionary(
                static group => group.Key,
                static group => new ExperimentalPlayerReconstructionBuilder(group.OrderBy(static item => item.StartTimestamp).ToList()));

        foreach (var dotEvent in aggregation.HostileDotEvents.Where(static item => item.StatusId == 0))
        {
            var matchedWindows = windows
                .Where(window => window.TargetId == dotEvent.TargetId
                                 && dotEvent.Timestamp >= window.StartTimestamp
                                 && dotEvent.Timestamp < window.EndTimestamp)
                .ToList();

            if (matchedWindows.Count == 0)
                continue;

            var matchedOwners = matchedWindows
                .Select(static window => window.OwnerActorId)
                .Distinct()
                .ToList();

            if (matchedOwners.Count != 1)
            {
                foreach (var ownerActorId in matchedOwners)
                {
                    builders[ownerActorId].AmbiguousEventCount++;
                }

                continue;
            }

            builders[matchedOwners[0]].AssignedEvents.Add(dotEvent);
        }

        return builders.ToDictionary(
            pair => pair.Key,
            pair => BuildExperimentalPlayerReconstruction(whmPlayers[pair.Key], pair.Value));
    }

    private static List<ExperimentalWhmDiaWindow> BuildExperimentalWhmDiaWindows(
        IReadOnlySet<uint> whmActorIds,
        ActAggregationResult aggregation)
    {
        var windows = new List<ExperimentalWhmDiaWindow>();
        if (whmActorIds.Count == 0)
            return windows;

        foreach (var group in aggregation.StatusApplyEvents
                     .Where(item => whmActorIds.Contains(item.SourceId)
                                    && ExperimentalWhmDiaStatusIds.Contains(item.StatusId))
                     .OrderBy(static item => item.SourceId)
                     .ThenBy(static item => item.TargetId)
                     .ThenBy(static item => item.Timestamp)
                     .GroupBy(static item => (item.SourceId, item.TargetId)))
        {
            var applies = new List<ActStatusApplyEvent>();
            foreach (var applyEvent in group)
            {
                if (applies.Count > 0
                    && applyEvent.Timestamp - applies[^1].Timestamp <= ExperimentalStatusApplyDedupWindow)
                {
                    continue;
                }

                applies.Add(applyEvent);
            }

            for (var i = 0; i < applies.Count; i++)
            {
                var startTimestamp = applies[i].Timestamp;
                var endTimestamp = startTimestamp + ExperimentalWhmDiaDuration;

                if (i + 1 < applies.Count && applies[i + 1].Timestamp < endTimestamp)
                {
                    endTimestamp = applies[i + 1].Timestamp;
                }

                if (aggregation.EncounterEndUtc.HasValue && aggregation.EncounterEndUtc.Value < endTimestamp)
                {
                    endTimestamp = aggregation.EncounterEndUtc.Value.ToOffset(startTimestamp.Offset);
                }

                if (endTimestamp <= startTimestamp)
                    continue;

                windows.Add(new ExperimentalWhmDiaWindow(
                    applies[i].SourceId,
                    applies[i].SourceName,
                    applies[i].TargetId,
                    applies[i].TargetName,
                    applies[i].StatusId,
                    applies[i].StatusName,
                    startTimestamp,
                    endTimestamp));
            }
        }

        return windows;
    }

    private static ExperimentalPlayerReconstruction BuildExperimentalPlayerReconstruction(
        PlayerEncounterEntry player,
        ExperimentalPlayerReconstructionBuilder builder)
    {
        var actDotDamage = builder.AssignedEvents.Sum(static item => item.Damage);
        var diffPercent = CalculateDiffPercent(player.PluginDotDamage, actDotDamage);

        var sourceBreakdowns = builder.AssignedEvents
            .GroupBy(item => new SourceBreakdownKey(item.SourceId, item.SourceName))
            .OrderByDescending(static group => group.Sum(static item => item.Damage))
            .ThenBy(static group => group.Key.SourceId)
            .Select(group =>
            {
                var damage = group.Sum(static item => item.Damage);
                var eventCount = group.Count();
                return new ExperimentalSourceBreakdown(
                    group.Key.SourceId,
                    group.Key.SourceName,
                    damage,
                    eventCount,
                    actDotDamage > 0 ? damage * 100m / actDotDamage : 0m);
            })
            .ToList();

        var windowSummaries = builder.Windows
            .Select(window =>
            {
                var windowEvents = builder.AssignedEvents
                    .Where(item => item.TargetId == window.TargetId
                                   && item.Timestamp >= window.StartTimestamp
                                   && item.Timestamp < window.EndTimestamp)
                    .ToList();

                return new ExperimentalWindowSummary(
                    window.TargetId,
                    window.TargetName,
                    window.StatusId,
                    window.StatusName,
                    window.StartTimestamp,
                    window.EndTimestamp,
                    windowEvents.Sum(static item => item.Damage),
                    windowEvents.Count);
            })
            .ToList();

        return new ExperimentalPlayerReconstruction(
            "WHM天辉状态窗 + hostile status=0",
            actDotDamage,
            diffPercent,
            builder.Windows.Count,
            builder.AssignedEvents.Count,
            builder.AmbiguousEventCount,
            windowSummaries,
            sourceBreakdowns);
    }

    private static IReadOnlyList<StatusWindowSummary> BuildStatusWindowSummaries(
        IReadOnlyList<PlayerEncounterEntry> players,
        ActAggregationResult aggregation)
    {
        if (players.Count == 0 || aggregation.StatusApplyEvents.Count == 0)
            return [];

        var playerOrder = players
            .Select(static (player, index) => new { player.CombatantKey, Index = index })
            .GroupBy(static item => item.CombatantKey, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First().Index, StringComparer.Ordinal);

        var playersByActorId = players
            .Where(static player => player.ActorId.HasValue)
            .GroupBy(static player => player.ActorId!.Value)
            .ToDictionary(static group => group.Key, static group => group.First());

        var playersByName = players
            .Where(static player => !string.IsNullOrWhiteSpace(player.Name))
            .GroupBy(static player => player.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);

        var matchedEvents = new List<(PlayerEncounterEntry Player, ActStatusApplyEvent Event)>();
        foreach (var applyEvent in aggregation.StatusApplyEvents)
        {
            PlayerEncounterEntry? player = null;
            if (applyEvent.SourceId != 0)
                playersByActorId.TryGetValue(applyEvent.SourceId, out player);

            if (player == null && !string.IsNullOrWhiteSpace(applyEvent.SourceName))
                playersByName.TryGetValue(applyEvent.SourceName, out player);

            if (player == null)
                continue;

            matchedEvents.Add((player, applyEvent));
        }

        return matchedEvents
            .GroupBy(item => new
            {
                item.Player.CombatantKey,
                item.Player.Name,
                item.Player.Job,
                item.Player.ActorId,
                item.Event.SourceId,
                item.Event.SourceName,
                item.Event.StatusId,
                item.Event.StatusName,
                item.Event.TargetId,
                item.Event.TargetName,
            })
            .Select(group =>
            {
                var orderedEvents = group.OrderBy(static item => item.Event.Timestamp).ToList();
                return new StatusWindowSummary(
                    group.Key.CombatantKey,
                    group.Key.Name,
                    group.Key.Job,
                    group.Key.ActorId,
                    group.Key.SourceId,
                    group.Key.SourceName,
                    group.Key.StatusId,
                    group.Key.StatusName,
                    group.Key.TargetId,
                    group.Key.TargetName,
                    orderedEvents.Count,
                    orderedEvents[0].Event.Timestamp,
                    orderedEvents[^1].Event.Timestamp);
            })
            .OrderBy(summary => playerOrder.TryGetValue(summary.CombatantKey, out var index) ? index : int.MaxValue)
            .ThenBy(static summary => summary.FirstApplied)
            .ThenBy(static summary => summary.StatusId)
            .ThenBy(static summary => summary.TargetId)
            .ToList();
    }

    private static IReadOnlyList<DotWindowConsistencyResult> BuildDotWindowConsistencyResults(
        IReadOnlyList<PlayerReconcileResult> results,
        IReadOnlyList<StatusWindowSummary> statusWindowSummaries)
    {
        if (results.Count == 0)
            return [];

        var windowsByCombatantKey = statusWindowSummaries
            .GroupBy(static summary => summary.CombatantKey, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.ToList(), StringComparer.Ordinal);

        var consistencyResults = new List<DotWindowConsistencyResult>(results.Count);
        foreach (var result in results)
        {
            var player = result.Player;
            var canonicalJob = CanonicalizeJob(player.Job);
            var knownStatusIds = ResolveKnownDotStatusIds(canonicalJob);
            windowsByCombatantKey.TryGetValue(player.CombatantKey, out var allWindows);
            allWindows ??= [];

            var knownWindows = allWindows
                .Where(window => knownStatusIds.Contains(window.StatusId))
                .OrderBy(static window => window.FirstApplied)
                .ThenBy(static window => window.StatusId)
                .ThenBy(static window => window.TargetId)
                .ToList();

            var knownActStatuses = result.StatusBreakdowns
                .Where(status => status.StatusId != 0 && knownStatusIds.Contains(status.StatusId))
                .OrderByDescending(static status => status.Damage)
                .ThenBy(static status => status.StatusId)
                .ToList();

            var zeroStatusDamage = result.StatusBreakdowns
                .Where(static status => status.StatusId == 0)
                .Sum(static status => status.Damage);
            var zeroStatusEventCount = result.StatusBreakdowns
                .Where(static status => status.StatusId == 0)
                .Sum(static status => status.EventCount);

            var hasKnownEvidence = knownWindows.Count > 0 || knownActStatuses.Count > 0;
            var state = DotWindowConsistencyState.None;
            var message = "没有 ACT 已归属 DoT。";

            if (result.ActDotDamage > 0 && hasKnownEvidence)
            {
                state = DotWindowConsistencyState.Ok;
                message = "ACT 有 DoT，且找到已知 DoT 状态窗口或非零已知 status。";
            }
            else if (result.ActDotDamage > 0)
            {
                state = DotWindowConsistencyState.Warning;
                message = "ACT 有 DoT，但没有找到该职业的已知 DoT 状态窗口或非零已知 status；这类 status=0 归属应视为可疑。";
            }
            else if (player.PluginDotDamage > 0 && hasKnownEvidence)
            {
                state = DotWindowConsistencyState.PluginOnly;
                message = "插件有 DoT，且 ACT 状态窗口存在，但 ACT 已归属 DoT 为 0。";
            }
            else if (player.PluginDotDamage > 0)
            {
                state = DotWindowConsistencyState.PluginOnly;
                message = "插件有 DoT，但没有找到 ACT 已归属 DoT 或已知状态窗口。";
            }

            consistencyResults.Add(new DotWindowConsistencyResult(
                player,
                result.Player.PluginDotDamage,
                result.ActDotDamage,
                state,
                message,
                knownWindows,
                knownActStatuses,
                zeroStatusDamage,
                zeroStatusEventCount));
        }

        return consistencyResults
            .OrderByDescending(static result => result.State == DotWindowConsistencyState.Warning)
            .ThenByDescending(static result => result.ActDotDamage)
            .ThenByDescending(static result => result.PluginDotDamage)
            .ToList();
    }

    private static IReadOnlySet<uint> ResolveKnownDotStatusIds(string canonicalJob)
        => KnownDotStatusIdsByJob.TryGetValue(canonicalJob, out var statusIds)
            ? statusIds
            : new HashSet<uint>();

    private static List<PlayerReconcileResult> BuildResults(
        IReadOnlyList<PlayerEncounterEntry> players,
        ActAggregationResult aggregation,
        IReadOnlyDictionary<uint, ExperimentalPlayerReconstruction> experimentalReconstructions)
    {
        var results = new List<PlayerReconcileResult>(players.Count);

        foreach (var player in players)
        {
            var matchMode = "未命中";
            long actDotDamage = 0;
            IReadOnlyList<PlayerStatusBreakdown> statusBreakdowns = [];

            if (player.ActorId.HasValue && aggregation.DamageBySourceId.TryGetValue(player.ActorId.Value, out var byActorId))
            {
                actDotDamage = byActorId;
                matchMode = "actorId";
                statusBreakdowns = BuildStatusBreakdowns(
                    actDotDamage,
                    aggregation.StatusBySourceId.TryGetValue(player.ActorId.Value, out var statusMap) ? statusMap : null);
            }
            else if (aggregation.DamageBySourceName.TryGetValue(player.Name, out var byName))
            {
                actDotDamage = byName;
                matchMode = "name";
                statusBreakdowns = BuildStatusBreakdowns(
                    actDotDamage,
                    aggregation.StatusBySourceName.TryGetValue(player.Name, out var statusMap) ? statusMap : null);
            }

            var diffPercent = CalculateDiffPercent(player.PluginDotDamage, actDotDamage);
            var status = EvaluateStatus(player.PluginDotDamage, actDotDamage, diffPercent);
            ExperimentalPlayerReconstruction? experimentalReconstruction = null;
            if (player.ActorId.HasValue
                && experimentalReconstructions.TryGetValue(player.ActorId.Value, out var byActorIdExperimental))
            {
                experimentalReconstruction = byActorIdExperimental;
            }

            results.Add(new PlayerReconcileResult(
                player,
                actDotDamage,
                status,
                diffPercent,
                matchMode,
                statusBreakdowns,
                experimentalReconstruction));
        }

        return results
            .OrderByDescending(static item => Math.Max(item.Player.PluginDotDamage, item.ActDotDamage))
            .ThenBy(static item => item.Player.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<PlayerStatusBreakdown> BuildStatusBreakdowns(
        long actDotDamage,
        IReadOnlyDictionary<uint, DotStatusAggregate>? statusMap)
    {
        if (statusMap == null || statusMap.Count == 0)
            return [];

        return statusMap
            .OrderByDescending(static pair => pair.Value.Damage)
            .ThenBy(static pair => pair.Key)
            .Select(pair => new PlayerStatusBreakdown(
                pair.Key,
                pair.Value.Damage,
                pair.Value.EventCount,
                actDotDamage > 0 ? pair.Value.Damage * 100m / actDotDamage : 0m))
            .ToList();
    }

    private static decimal? CalculateDiffPercent(long pluginDotDamage, long actDotDamage)
    {
        if (actDotDamage == 0)
            return pluginDotDamage == 0 ? 0m : null;

        return (pluginDotDamage - actDotDamage) * 100m / actDotDamage;
    }

    private static decimal CalculateSharePercent(long part, long total)
        => total > 0 ? part * 100m / total : 0m;

    private static string BuildSummaryConclusion(
        bool isFiltered,
        decimal? actHostileTotalDiffPercent,
        long unresolvedHostileDotDamage,
        decimal unresolvedHostileSharePercent)
    {
        if (isFiltered)
            return "已启用玩家/职业过滤：只比较显示玩家的 ACT 已归属；不能用整场 ACT hostile 总量直接判断总体。";

        if (!actHostileTotalDiffPercent.HasValue)
            return "ACT hostile 总量为 0：无法判断总体差异。";

        var direction = actHostileTotalDiffPercent.Value switch
        {
            > 5m => "插件高于 ACT hostile 总量",
            < -5m => "插件低于 ACT hostile 总量",
            _ => "插件与 ACT hostile 总量接近",
        };

        var unresolvedNote = unresolvedHostileDotDamage > 0
            ? $"；存在未归属 hostile DoT {FormatInteger(unresolvedHostileDotDamage)}（{FormatPercent(unresolvedHostileSharePercent)}），ACT 已归属个人数应视为下限。"
            : "；未发现未归属 hostile DoT。";

        return $"{direction}（{FormatDiffPercent(actHostileTotalDiffPercent)}）{unresolvedNote}";
    }

    private static string ResolveKnownDotStatusName(uint statusId, IReadOnlyList<StatusWindowSummary> windows)
    {
        var windowName = windows
            .Select(static window => window.StatusName)
            .FirstOrDefault(static name => !string.IsNullOrWhiteSpace(name));
        if (!string.IsNullOrWhiteSpace(windowName))
            return windowName;

        return KnownDotStatusNames.TryGetValue(statusId, out var knownName)
            ? knownName
            : string.Empty;
    }

    private static string ResolveKnownDotEvidence(
        PlayerStatusBreakdown? actKnownStatus,
        int knownWindowApplyCount,
        long zeroStatusDamage)
    {
        if ((actKnownStatus?.Damage ?? 0) > 0)
            return "ACT非零已知status";

        if (knownWindowApplyCount > 0 && zeroStatusDamage > 0)
            return "状态窗口+ACT status=0";

        if (knownWindowApplyCount > 0)
            return "仅状态窗口";

        return "无直接证据";
    }

    private static string BuildKnownDotNote(
        PlayerStatusBreakdown? actKnownStatus,
        int knownWindowApplyCount,
        long zeroStatusDamage,
        int zeroStatusEventCount)
    {
        if ((actKnownStatus?.Damage ?? 0) > 0)
            return "ACT 有非零已知 status；优先按该 status 做局部对账。";

        if (knownWindowApplyCount > 0 && zeroStatusDamage > 0)
            return $"ACT 有已知 DoT 状态窗口，但 DoT tick 主要/全部落在 status=0（{FormatInteger(zeroStatusDamage)} / {zeroStatusEventCount} 行）；个人 ACT 已归属不适合直接当作该 DoT 实值。";

        if (knownWindowApplyCount > 0)
            return "ACT 有已知 DoT 状态窗口，但没有对应非零 DoT tick；可作为插件状态驱动的旁证。";

        return "未找到 ACT 非零已知 status 或 26| 状态窗口；需要结合 DOT诊断或下一场继续观察。";
    }

    private static string ResolveDotDiagnosticEvidence(
        PlayerStatusBreakdown? actKnownStatus,
        int knownWindowApplyCount,
        long zeroStatusDamage)
    {
        if ((actKnownStatus?.Damage ?? 0) > 0)
            return "DOT诊断+ACT非零已知status";

        if (knownWindowApplyCount > 0 && zeroStatusDamage > 0)
            return "DOT诊断+状态窗口+ACT status=0";

        if (knownWindowApplyCount > 0)
            return "DOT诊断+状态窗口";

        if (zeroStatusDamage > 0)
            return "DOT诊断+ACT status=0";

        return "仅DOT诊断";
    }

    private static string BuildDotDiagnosticConclusion(
        PlayerReconcileResult? playerResult,
        PlayerStatusBreakdown? actKnownStatus,
        int knownWindowApplyCount,
        long zeroStatusDamage,
        int zeroStatusEventCount)
    {
        if (playerResult == null)
            return "DOT诊断来源不在当前 history 显示玩家中；可能是过滤条件、宠物/召唤物或非队伍来源。";

        if ((actKnownStatus?.Damage ?? 0) > 0)
            return "ACT 有非零已知 status；优先用“DOT诊断伤害 vs ACT该status伤害”做局部对账。";

        if (knownWindowApplyCount > 0 && zeroStatusDamage > 0)
            return $"ACT 有状态窗口，但 DoT tick 主要/全部落在 status=0（{FormatInteger(zeroStatusDamage)} / {zeroStatusEventCount} 行）；个人 ACT 已归属不能直接当作该 DoT 实值。";

        if (knownWindowApplyCount > 0)
            return "ACT 有状态窗口但没有非零 DoT tick；DOT诊断可作为插件内部 tick 依据，ACT 个人数可能缺失。";

        if (zeroStatusDamage > 0)
            return $"ACT 只有 status=0 DoT（{FormatInteger(zeroStatusDamage)} / {zeroStatusEventCount} 行），没有对应状态窗口；需要结合总量或下一场复核。";

        return "ACT 没有对应已归属 DoT 证据；如 DOT诊断伤害进入插件历史，优先检查 ACT 未归属/source缺失。";
    }

    private static ReconcileStatus EvaluateStatus(long pluginDotDamage, long actDotDamage, decimal? diffPercent)
    {
        if (pluginDotDamage == 0 && actDotDamage == 0)
            return ReconcileStatus.Green;

        if (actDotDamage == 0)
            return ReconcileStatus.Red;

        if (pluginDotDamage == 0 && actDotDamage > 0)
            return ReconcileStatus.Red;

        if (pluginDotDamage > actDotDamage * 2)
            return ReconcileStatus.Red;

        var absoluteDiff = Math.Abs(diffPercent ?? 100m);
        if (absoluteDiff > 40m)
            return ReconcileStatus.Red;

        if (absoluteDiff > 15m)
            return ReconcileStatus.Yellow;

        return ReconcileStatus.Green;
    }

    private static void PrintEncounterHeader(
        string historyPath,
        HistoricalCombatData encounter,
        Options options,
        IReadOnlyList<string> logPaths,
        IReadOnlySet<uint> excludedStatusIds,
        ActAggregationResult aggregation)
    {
        Console.WriteLine("=== DotReconcile ===");
        Console.WriteLine($"战斗：{encounter.ZoneName}");
        Console.WriteLine($"开始：{FormatDateTime(encounter.StartTimeUtc)}");
        Console.WriteLine($"结束：{FormatDateTime(encounter.EndTimeUtc)}");
        Console.WriteLine($"时长：{encounter.Duration}");
        Console.WriteLine($"history：{historyPath}");
        Console.WriteLine($"ACT 日志候选数：{logPaths.Count}");

        foreach (var path in logPaths.Take(5))
        {
            Console.WriteLine($"  - {path}");
        }

        if (logPaths.Count > 5)
        {
            Console.WriteLine($"  - ... 其余 {logPaths.Count - 5} 个文件已省略");
        }

        Console.WriteLine("ACT 口径：hostile-only 24|DoT（目标 actorId 以 4 开头；玩家结果仅统计 source 已归属部分）");
        Console.WriteLine("实验口径：对白魔额外尝试按 26|74F/7F3 天辉状态窗重建 hostile status=0 DoT（不替代默认口径）");
        Console.WriteLine(excludedStatusIds.Count == 0
            ? "特殊状态排除：无（已启用 --include-special-dot）"
            : $"特殊状态排除：{string.Join(", ", excludedStatusIds.Select(static id => $"0x{id:X}"))}");

        if (options.JobFilters.Count > 0)
        {
            Console.WriteLine($"职业过滤：{string.Join(", ", options.JobFilters)}");
        }

        if (options.PlayerFilters.Count > 0)
        {
            Console.WriteLine($"玩家过滤：{string.Join(", ", options.PlayerFilters)}");
        }

        Console.WriteLine($"status 明细：{(options.TopStatusCount > 0 ? $"终端显示前 {options.TopStatusCount} 条" : "关闭")} ");
        Console.WriteLine();
        Console.WriteLine($"ACT 扫描统计：总行数 {FormatInteger(aggregation.TotalLines)}，DoT 行 {FormatInteger(aggregation.DotEventLines)}，战斗窗内 DoT 行 {FormatInteger(aggregation.DotEventLinesInEncounterWindow)}，hostile-only 命中 {FormatInteger(aggregation.HostileDotLines)}");
        Console.WriteLine($"ACT 归属统计：已归属 {FormatInteger(aggregation.ResolvedHostileDotDamage)} 伤害 / {FormatInteger(aggregation.ResolvedHostileDotLines)} 行，未归属 {FormatInteger(aggregation.UnresolvedHostileDotDamage)} 伤害 / {FormatInteger(aggregation.UnresolvedHostileDotLines)} 行");
        Console.WriteLine($"ACT hostile 总量：{FormatInteger(aggregation.HostileDotTotalDamage)} 伤害 / {FormatInteger(aggregation.HostileDotTotalLines)} 行（已归属 + 未归属，用于判断总体是否真的偏高）");
        if (aggregation.UnresolvedHostileDotLines > 0)
        {
            Console.WriteLine($"ACT 未归属细分：source=target/hostile {FormatInteger(aggregation.HostileOrSelfSourcedDotDamage)} 伤害 / {FormatInteger(aggregation.HostileOrSelfSourcedDotLines)} 行，source 缺失 {FormatInteger(aggregation.MissingSourceHostileDotDamage)} 伤害 / {FormatInteger(aggregation.MissingSourceHostileDotLines)} 行");
            Console.WriteLine("注意：存在未归属 hostile DoT 时，下方每个玩家的 ACT 数值都应视为下限。");
        }
        Console.WriteLine($"ACT 额外统计：排除特殊状态 {FormatInteger(aggregation.ExcludedStatusLines)}，非 hostile 目标 {FormatInteger(aggregation.NonHostileTargetLines)}，解析失败 {FormatInteger(aggregation.ParseFailures)}");

        if (aggregation.LogsWithEncounterData.Count > 0)
        {
            Console.WriteLine("实际命中战斗窗口的 ACT 日志：");
            foreach (var path in aggregation.LogsWithEncounterData)
            {
                Console.WriteLine($"  - {path}");
            }
        }
        else
        {
            Console.WriteLine("警告：没有在 ACT 日志里命中这场战斗的 24|DoT 数据。");
        }

        if (aggregation.LogReadErrors.Count > 0)
        {
            Console.WriteLine("ACT 日志读取警告：");
            foreach (var error in aggregation.LogReadErrors)
            {
                Console.WriteLine($"  - {error}");
            }
        }

        Console.WriteLine();
    }

    private static void PrintResults(IReadOnlyList<PlayerReconcileResult> results, int topStatusCount)
    {
        foreach (var result in results)
        {
            var player = result.Player;
            Console.WriteLine(
                $"[{result.Status}] {player.Name} | {player.Job} | 插件 {FormatInteger(player.PluginDotDamage)} | ACT已归属 {FormatInteger(result.ActDotDamage)} | 差异 {FormatDiffPercent(result.DiffPercent)} | 匹配 {result.MatchMode} | actorId {FormatActorId(player.ActorId)}");

            if (topStatusCount <= 0 || result.StatusBreakdowns.Count == 0)
                continue;

            foreach (var breakdown in result.StatusBreakdowns.Take(topStatusCount))
            {
                Console.WriteLine(
                    $"    - status 0x{breakdown.StatusId:X} | ACT已归属 {FormatInteger(breakdown.Damage)} | 占比 {FormatPercent(breakdown.SharePercent)} | 事件 {breakdown.EventCount}");
            }

            if (result.ExperimentalReconstruction != null)
            {
                Console.WriteLine(
                    $"    - 实验口径 {result.ExperimentalReconstruction.Label} | ACT重建 {FormatInteger(result.ExperimentalReconstruction.ActDotDamage)} | 差异 {FormatDiffPercent(result.ExperimentalReconstruction.DiffPercent)} | 窗口 {result.ExperimentalReconstruction.WindowCount} | 事件 {result.ExperimentalReconstruction.EventCount} | 歧义 {result.ExperimentalReconstruction.AmbiguousEventCount}");

                foreach (var breakdown in result.ExperimentalReconstruction.SourceBreakdowns.Take(5))
                {
                    Console.WriteLine(
                        $"      · source {FormatActorId(breakdown.SourceId)} {breakdown.SourceName} | 重建 {FormatInteger(breakdown.Damage)} | 占比 {FormatPercent(breakdown.SharePercent)} | 事件 {breakdown.EventCount}");
                }
            }
        }
    }

    private static void PrintStatusWindowSummaries(IReadOnlyList<StatusWindowSummary> summaries)
    {
        Console.WriteLine();
        Console.WriteLine("=== ACT 状态应用摘要（26|，source=当前显示玩家，target=敌方）===");

        if (summaries.Count == 0)
        {
            Console.WriteLine("没有命中当前显示玩家对敌方目标的 26| 状态应用。");
            return;
        }

        foreach (var playerGroup in summaries.GroupBy(static summary => summary.CombatantKey))
        {
            var first = playerGroup.First();
            Console.WriteLine($"{first.PlayerName} | {first.Job} | actorId {FormatActorId(first.PlayerActorId)}");

            foreach (var summary in playerGroup)
            {
                Console.WriteLine(
                    $"    - status 0x{summary.StatusId:X} {summary.StatusName} -> {summary.TargetName} {FormatActorId(summary.TargetId)} | 应用 {summary.ApplyCount} | 首次 {FormatTimeOfDay(summary.FirstApplied)} | 最后 {FormatTimeOfDay(summary.LastApplied)}");
            }
        }
    }

    private static void PrintDotWindowConsistencyResults(IReadOnlyList<DotWindowConsistencyResult> results)
    {
        Console.WriteLine();
        Console.WriteLine("=== ACT DoT 与已知 DoT 状态窗口一致性检查 ===");

        if (results.Count == 0)
        {
            Console.WriteLine("没有可检查的玩家结果。");
            return;
        }

        foreach (var result in results.Where(static item =>
                     item.State != DotWindowConsistencyState.None
                     || item.KnownStatusWindows.Count > 0
                     || item.KnownActStatuses.Count > 0))
        {
            Console.WriteLine(
                $"[{FormatDotWindowConsistencyState(result.State)}] {result.Player.Name} | {result.Player.Job} | 插件 {FormatInteger(result.PluginDotDamage)} | ACT已归属 {FormatInteger(result.ActDotDamage)} | ACT status=0 {FormatInteger(result.ZeroStatusDamage)} / {FormatInteger(result.ZeroStatusEventCount)} 行");
            Console.WriteLine($"    - {result.Message}");

            if (result.KnownStatusWindows.Count > 0)
            {
                var windowSummary = string.Join("；", result.KnownStatusWindows
                    .GroupBy(static window => (window.StatusId, window.StatusName))
                    .Select(static group => $"0x{group.Key.StatusId:X} {group.Key.StatusName}×{group.Sum(static window => window.ApplyCount)}"));
                Console.WriteLine($"    - 已知状态窗口：{windowSummary}");
            }

            if (result.KnownActStatuses.Count > 0)
            {
                var statusSummary = string.Join("；", result.KnownActStatuses
                    .Select(static status => $"0x{status.StatusId:X}:{status.Damage}({status.EventCount})"));
                Console.WriteLine($"    - ACT 非零已知 status：{statusSummary}");
            }
        }
    }

    private static void PrintAggregateFooter(
        IReadOnlyList<PlayerReconcileResult> results,
        ActAggregationResult aggregation,
        Options options)
    {
        var pluginTotal = results.Sum(static item => item.Player.PluginDotDamage);
        var actTotal = results.Sum(static item => item.ActDotDamage);
        var attributedDiff = CalculateDiffPercent(pluginTotal, actTotal);
        var hostileTotalDiff = CalculateDiffPercent(pluginTotal, aggregation.HostileDotTotalDamage);
        var hasDisplayFilter = options.JobFilters.Count > 0 || options.PlayerFilters.Count > 0;
        var experimentalResults = results
            .Where(static item => item.ExperimentalReconstruction != null)
            .Select(static item => item.ExperimentalReconstruction!)
            .ToList();

        Console.WriteLine();
        Console.WriteLine($"显示玩家合计：插件 {FormatInteger(pluginTotal)} | ACT已归属 {FormatInteger(actTotal)} | 对已归属差异 {FormatDiffPercent(attributedDiff)}");
        if (hasDisplayFilter)
        {
            Console.WriteLine($"总体复核口径：当前启用了职业/玩家过滤，插件合计只覆盖显示玩家；完整 ACT hostile 总量 {FormatInteger(aggregation.HostileDotTotalDamage)} 不直接参与差异判定。");
        }
        else
        {
            Console.WriteLine($"总体复核口径：插件 {FormatInteger(pluginTotal)} | ACT hostile 总量 {FormatInteger(aggregation.HostileDotTotalDamage)} | 对 hostile 总量差异 {FormatDiffPercent(hostileTotalDiff)}");
        }
        if (experimentalResults.Count > 0)
        {
            var experimentalTotal = experimentalResults.Sum(static item => item.ActDotDamage);
            Console.WriteLine($"实验口径合计：ACT重建 {FormatInteger(experimentalTotal)} | 差异 {FormatDiffPercent(CalculateDiffPercent(pluginTotal, experimentalTotal))}");
        }
        Console.WriteLine($"ACT hostile-only 总体：已归属 {FormatInteger(aggregation.ResolvedHostileDotDamage)} 伤害 / {FormatInteger(aggregation.ResolvedHostileDotLines)} 行，未归属 {FormatInteger(aggregation.UnresolvedHostileDotDamage)} 伤害 / {FormatInteger(aggregation.UnresolvedHostileDotLines)} 行，总量 {FormatInteger(aggregation.HostileDotTotalDamage)} 伤害 / {FormatInteger(aggregation.HostileDotTotalLines)} 行");
        Console.WriteLine($"状态统计：GREEN={results.Count(static item => item.Status == ReconcileStatus.Green)}，YELLOW={results.Count(static item => item.Status == ReconcileStatus.Yellow)}，RED={results.Count(static item => item.Status == ReconcileStatus.Red)}");

        if (aggregation.LogsWithEncounterData.Count == 0)
        {
            Console.WriteLine("提示：如果你刚导出新 history，但这里 ACT 还是 0，优先检查当前轮转后的 Network_*.log 是否也一起保留了。");
        }
        else if (aggregation.UnresolvedHostileDotLines > 0)
        {
            Console.WriteLine("提示：这场日志里存在未归属 hostile DoT；对账时请优先把“插件高于 ACT已归属”理解成待复核，而不是直接判定插件虚高。");
        }
    }

    private static void WriteExports(
        Options options,
        string historyPath,
        HistoricalCombatData encounter,
        IReadOnlyList<string> logPaths,
        IReadOnlyList<string> dalamudLogPaths,
        IReadOnlySet<uint> excludedStatusIds,
        ActAggregationResult aggregation,
        IReadOnlyList<PlayerReconcileResult> results,
        IReadOnlyList<StatusWindowSummary> statusWindowSummaries,
        IReadOnlyList<DotWindowConsistencyResult> dotWindowConsistencyResults,
        IReadOnlyList<DotDiagnosticSummary> dotDiagnosticSummaries)
    {
        var context = BuildExportContext(options, aggregation, results);

        WriteJsonExport(
            options,
            historyPath,
            encounter,
            logPaths,
            excludedStatusIds,
            aggregation,
            results,
            statusWindowSummaries,
            dotWindowConsistencyResults,
            context);
        WriteSummaryExport(options, historyPath, encounter, aggregation, results, context);
        WriteMainCsvExport(options, results);
        WriteStatusCsvExport(options, results);
        WriteWindowCheckCsvExport(options, dotWindowConsistencyResults);
        WriteKnownDotCsvExport(options, dotWindowConsistencyResults);
        WriteDotDiagnosticCsvExport(options, dalamudLogPaths, results, statusWindowSummaries, dotDiagnosticSummaries);
    }

    private static ExportContext BuildExportContext(
        Options options,
        ActAggregationResult aggregation,
        IReadOnlyList<PlayerReconcileResult> results)
    {
        var pluginTotal = results.Sum(static item => item.Player.PluginDotDamage);
        var actAttributedTotal = results.Sum(static item => item.ActDotDamage);
        var actHostileTotal = aggregation.HostileDotTotalDamage;
        var hasDisplayFilter = options.JobFilters.Count > 0 || options.PlayerFilters.Count > 0;
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var actAttributedDiffPercent = CalculateDiffPercent(pluginTotal, actAttributedTotal);
        var actHostileTotalDiffPercent = hasDisplayFilter ? null : CalculateDiffPercent(pluginTotal, actHostileTotal);
        var unresolvedHostileSharePercent = CalculateSharePercent(aggregation.UnresolvedHostileDotDamage, actHostileTotal);
        var summaryConclusion = BuildSummaryConclusion(
            hasDisplayFilter,
            actHostileTotalDiffPercent,
            aggregation.UnresolvedHostileDotDamage,
            unresolvedHostileSharePercent);

        return new ExportContext(
            pluginTotal,
            actAttributedTotal,
            actHostileTotal,
            hasDisplayFilter,
            generatedAtUtc,
            actAttributedDiffPercent,
            actHostileTotalDiffPercent,
            unresolvedHostileSharePercent,
            summaryConclusion);
    }

    private static void WriteJsonExport(
        Options options,
        string historyPath,
        HistoricalCombatData encounter,
        IReadOnlyList<string> logPaths,
        IReadOnlySet<uint> excludedStatusIds,
        ActAggregationResult aggregation,
        IReadOnlyList<PlayerReconcileResult> results,
        IReadOnlyList<StatusWindowSummary> statusWindowSummaries,
        IReadOnlyList<DotWindowConsistencyResult> dotWindowConsistencyResults,
        ExportContext context)
    {
        if (string.IsNullOrWhiteSpace(options.JsonOutPath))
            return;

        var generatedAtUtc = context.GeneratedAtUtc;
        var pluginTotal = context.PluginTotal;
        var actAttributedTotal = context.ActAttributedTotal;
        var actHostileTotal = context.ActHostileTotal;
        var hasDisplayFilter = context.HasDisplayFilter;
        var actAttributedDiffPercent = context.ActAttributedDiffPercent;
        var actHostileTotalDiffPercent = context.ActHostileTotalDiffPercent;
        var unresolvedHostileSharePercent = context.UnresolvedHostileSharePercent;
        var summaryConclusion = context.SummaryConclusion;

        var exportModel = new
        {
            generatedAtUtc,
            historyPath,
            candidateActLogs = logPaths,
            hitActLogs = aggregation.LogsWithEncounterData,
            filters = new
            {
                zone = options.Zone,
                jobs = options.JobFilters.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase).ToArray(),
                players = options.PlayerFilters.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase).ToArray(),
                includeSpecialDot = options.IncludeSpecialDot,
                excludedStatusIds = excludedStatusIds.Select(static id => $"0x{id:X}").ToArray(),
                topStatusCount = options.TopStatusCount,
                showStatusWindows = options.ShowStatusWindows,
            },
            encounter = new
            {
                zoneName = encounter.ZoneName,
                duration = encounter.Duration,
                startTimeUtc = encounter.StartTimeUtc,
                endTimeUtc = encounter.EndTimeUtc,
            },
            summary = new
            {
                isFiltered = hasDisplayFilter,
                pluginDotDamageTotalScope = hasDisplayFilter ? "filteredPlayers" : "displayedEncounterPlayers",
                pluginDotDamageTotal = pluginTotal,
                actAttributedDotDamageTotal = actAttributedTotal,
                actAttributedDiffPercent,
                actHostileTotalDotDamage = actHostileTotal,
                actHostileTotalDotLines = aggregation.HostileDotTotalLines,
                actHostileTotalDiffPercent,
                actUnresolvedHostileDotDamage = aggregation.UnresolvedHostileDotDamage,
                actUnresolvedHostileDotLines = aggregation.UnresolvedHostileDotLines,
                actUnresolvedHostileDotDamageSharePercent = unresolvedHostileSharePercent,
                conclusion = summaryConclusion,
            },
            actScan = new
            {
                totalLines = aggregation.TotalLines,
                dotEventLines = aggregation.DotEventLines,
                dotEventLinesInEncounterWindow = aggregation.DotEventLinesInEncounterWindow,
                hostileDotLines = aggregation.HostileDotLines,
                hostileTotalDotLines = aggregation.HostileDotTotalLines,
                hostileTotalDotDamage = aggregation.HostileDotTotalDamage,
                resolvedHostileDotLines = aggregation.ResolvedHostileDotLines,
                resolvedHostileDotDamage = aggregation.ResolvedHostileDotDamage,
                unresolvedHostileDotLines = aggregation.UnresolvedHostileDotLines,
                unresolvedHostileDotDamage = aggregation.UnresolvedHostileDotDamage,
                unresolvedHostileDotDamageSharePercent = unresolvedHostileSharePercent,
                hostileOrSelfSourcedDotLines = aggregation.HostileOrSelfSourcedDotLines,
                hostileOrSelfSourcedDotDamage = aggregation.HostileOrSelfSourcedDotDamage,
                missingSourceHostileDotLines = aggregation.MissingSourceHostileDotLines,
                missingSourceHostileDotDamage = aggregation.MissingSourceHostileDotDamage,
                nonHostileTargetLines = aggregation.NonHostileTargetLines,
                excludedStatusLines = aggregation.ExcludedStatusLines,
                parseFailures = aggregation.ParseFailures,
                statusApplyLines = aggregation.StatusApplyEvents.Count,
                hostileDotEventObjects = aggregation.HostileDotEvents.Count,
            },
            statusWindows = statusWindowSummaries.Select(summary => new
            {
                combatantKey = summary.CombatantKey,
                playerName = summary.PlayerName,
                job = summary.Job,
                playerActorId = summary.PlayerActorId,
                playerActorIdHex = FormatActorId(summary.PlayerActorId),
                sourceId = summary.SourceId,
                sourceIdHex = FormatActorId(summary.SourceId),
                sourceName = summary.SourceName,
                statusId = summary.StatusId,
                statusIdHex = $"0x{summary.StatusId:X}",
                statusName = summary.StatusName,
                targetId = summary.TargetId,
                targetIdHex = FormatActorId(summary.TargetId),
                targetName = summary.TargetName,
                applyCount = summary.ApplyCount,
                firstApplied = summary.FirstApplied,
                lastApplied = summary.LastApplied,
            }).ToArray(),
            dotWindowConsistency = dotWindowConsistencyResults.Select(result => new
            {
                name = result.Player.Name,
                job = result.Player.Job,
                actorId = result.Player.ActorId,
                actorIdHex = FormatActorId(result.Player.ActorId),
                pluginDotDamage = result.PluginDotDamage,
                actAttributedDotDamage = result.ActDotDamage,
                state = result.State.ToString(),
                message = result.Message,
                zeroStatusDamage = result.ZeroStatusDamage,
                zeroStatusEventCount = result.ZeroStatusEventCount,
                knownStatusWindows = result.KnownStatusWindows.Select(window => new
                {
                    statusId = window.StatusId,
                    statusIdHex = $"0x{window.StatusId:X}",
                    statusName = window.StatusName,
                    targetId = window.TargetId,
                    targetIdHex = FormatActorId(window.TargetId),
                    targetName = window.TargetName,
                    applyCount = window.ApplyCount,
                    firstApplied = window.FirstApplied,
                    lastApplied = window.LastApplied,
                }).ToArray(),
                knownActStatuses = result.KnownActStatuses.Select(status => new
                {
                    statusId = status.StatusId,
                    statusIdHex = $"0x{status.StatusId:X}",
                    damage = status.Damage,
                    eventCount = status.EventCount,
                    sharePercent = status.SharePercent,
                }).ToArray(),
            }).ToArray(),
            players = results.Select(result => new
            {
                name = result.Player.Name,
                job = result.Player.Job,
                actorId = result.Player.ActorId,
                actorIdHex = FormatActorId(result.Player.ActorId),
                pluginDotDamage = result.Player.PluginDotDamage,
                actDotDamage = result.ActDotDamage,
                actAttributedDotDamage = result.ActDotDamage,
                diffPercent = result.DiffPercent,
                status = result.Status.ToString(),
                matchMode = result.MatchMode,
                experimental = result.ExperimentalReconstruction == null ? null : new
                {
                    label = result.ExperimentalReconstruction.Label,
                    actDotDamage = result.ExperimentalReconstruction.ActDotDamage,
                    diffPercent = result.ExperimentalReconstruction.DiffPercent,
                    windowCount = result.ExperimentalReconstruction.WindowCount,
                    eventCount = result.ExperimentalReconstruction.EventCount,
                    ambiguousEventCount = result.ExperimentalReconstruction.AmbiguousEventCount,
                    windowSummaries = result.ExperimentalReconstruction.WindowSummaries.Select(window => new
                    {
                        targetId = window.TargetId,
                        targetIdHex = $"0x{window.TargetId:X8}",
                        targetName = window.TargetName,
                        statusId = window.StatusId,
                        statusIdHex = $"0x{window.StatusId:X}",
                        statusName = window.StatusName,
                        startTime = window.StartTime,
                        endTime = window.EndTime,
                        damage = window.Damage,
                        eventCount = window.EventCount,
                    }).ToArray(),
                    sourceBreakdowns = result.ExperimentalReconstruction.SourceBreakdowns.Select(breakdown => new
                    {
                        sourceId = breakdown.SourceId,
                        sourceIdHex = breakdown.SourceId == 0 ? "0x00000000" : $"0x{breakdown.SourceId:X8}",
                        sourceName = breakdown.SourceName,
                        damage = breakdown.Damage,
                        eventCount = breakdown.EventCount,
                        sharePercent = breakdown.SharePercent,
                    }).ToArray(),
                },
                statusBreakdowns = result.StatusBreakdowns.Select(breakdown => new
                {
                    statusId = breakdown.StatusId,
                    statusIdHex = $"0x{breakdown.StatusId:X}",
                    damage = breakdown.Damage,
                    eventCount = breakdown.EventCount,
                    sharePercent = breakdown.SharePercent,
                }).ToArray(),
            }).ToArray(),
        };

        var json = JsonSerializer.Serialize(exportModel, JsonWriteOptions);
        WriteTextFile(options.JsonOutPath!, json);
        Console.WriteLine($"已写出 JSON：{options.JsonOutPath}");
    }

    private static void WriteSummaryExport(
        Options options,
        string historyPath,
        HistoricalCombatData encounter,
        ActAggregationResult aggregation,
        IReadOnlyList<PlayerReconcileResult> results,
        ExportContext context)
    {
        if (string.IsNullOrWhiteSpace(options.SummaryOutPath))
            return;

        var generatedAtUtc = context.GeneratedAtUtc;
        var pluginTotal = context.PluginTotal;
        var actAttributedTotal = context.ActAttributedTotal;
        var actHostileTotal = context.ActHostileTotal;
        var hasDisplayFilter = context.HasDisplayFilter;
        var actAttributedDiffPercent = context.ActAttributedDiffPercent;
        var actHostileTotalDiffPercent = context.ActHostileTotalDiffPercent;
        var unresolvedHostileSharePercent = context.UnresolvedHostileSharePercent;
        var summaryConclusion = context.SummaryConclusion;

        var statusGreen = results.Count(static item => item.Status == ReconcileStatus.Green);
        var statusYellow = results.Count(static item => item.Status == ReconcileStatus.Yellow);
        var statusRed = results.Count(static item => item.Status == ReconcileStatus.Red);
        var extension = Path.GetExtension(options.SummaryOutPath!);

        if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            var summaryModel = new
            {
                generatedAtUtc,
                historyPath,
                encounter = new
                {
                    zoneName = encounter.ZoneName,
                    duration = encounter.Duration,
                    startTimeUtc = encounter.StartTimeUtc,
                    endTimeUtc = encounter.EndTimeUtc,
                    startTimeLocal = FormatDateTime(encounter.StartTimeUtc),
                    endTimeLocal = FormatDateTime(encounter.EndTimeUtc),
                },
                scope = new
                {
                    isFiltered = hasDisplayFilter,
                    pluginDotDamageTotalScope = hasDisplayFilter ? "filteredPlayers" : "displayedEncounterPlayers",
                    jobs = options.JobFilters.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase).ToArray(),
                    players = options.PlayerFilters.OrderBy(static item => item, StringComparer.OrdinalIgnoreCase).ToArray(),
                },
                totals = new
                {
                    pluginDotDamage = pluginTotal,
                    actAttributedDotDamage = actAttributedTotal,
                    actAttributedDiffPercent,
                    actResolvedHostileDotDamage = aggregation.ResolvedHostileDotDamage,
                    actResolvedHostileDotLines = aggregation.ResolvedHostileDotLines,
                    actUnresolvedHostileDotDamage = aggregation.UnresolvedHostileDotDamage,
                    actUnresolvedHostileDotLines = aggregation.UnresolvedHostileDotLines,
                    actUnresolvedHostileDotDamageSharePercent = unresolvedHostileSharePercent,
                    actHostileTotalDotDamage = actHostileTotal,
                    actHostileTotalDotLines = aggregation.HostileDotTotalLines,
                    actHostileTotalDiffPercent,
                },
                unresolvedBreakdown = new
                {
                    missingSourceHostileDotDamage = aggregation.MissingSourceHostileDotDamage,
                    missingSourceHostileDotLines = aggregation.MissingSourceHostileDotLines,
                    hostileOrSelfSourcedDotDamage = aggregation.HostileOrSelfSourcedDotDamage,
                    hostileOrSelfSourcedDotLines = aggregation.HostileOrSelfSourcedDotLines,
                },
                scan = new
                {
                    totalLines = aggregation.TotalLines,
                    dotEventLines = aggregation.DotEventLines,
                    dotEventLinesInEncounterWindow = aggregation.DotEventLinesInEncounterWindow,
                    hostileDotLines = aggregation.HostileDotLines,
                    nonHostileTargetLines = aggregation.NonHostileTargetLines,
                    excludedStatusLines = aggregation.ExcludedStatusLines,
                    parseFailures = aggregation.ParseFailures,
                },
                resultStatus = new
                {
                    green = statusGreen,
                    yellow = statusYellow,
                    red = statusRed,
                },
                conclusion = summaryConclusion,
            };

            WriteTextFile(options.SummaryOutPath!, JsonSerializer.Serialize(summaryModel, JsonWriteOptions));
        }
        else
        {
            var sb = new StringBuilder();
            sb.AppendLine("GeneratedAtUtc,ZoneName,StartTimeLocal,EndTimeLocal,Duration,IsFiltered,PluginDotDamageTotal,ActAttributedDotDamageTotal,ActAttributedDiffPercent,ActResolvedHostileDotDamage,ActResolvedHostileDotLines,ActUnresolvedHostileDotDamage,ActUnresolvedHostileDotLines,ActUnresolvedHostileSharePercent,ActHostileTotalDotDamage,ActHostileTotalDotLines,ActHostileTotalDiffPercent,MissingSourceHostileDotDamage,MissingSourceHostileDotLines,StatusGreen,StatusYellow,StatusRed,Conclusion");
            sb.Append(EscapeCsv(generatedAtUtc.ToString("O", CultureInfo.InvariantCulture))).Append(',');
            sb.Append(EscapeCsv(encounter.ZoneName)).Append(',');
            sb.Append(EscapeCsv(FormatDateTime(encounter.StartTimeUtc))).Append(',');
            sb.Append(EscapeCsv(FormatDateTime(encounter.EndTimeUtc))).Append(',');
            sb.Append(EscapeCsv(encounter.Duration)).Append(',');
            sb.Append(hasDisplayFilter ? "true" : "false").Append(',');
            sb.Append(pluginTotal.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(actAttributedTotal.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(FormatDiffPercent(actAttributedDiffPercent))).Append(',');
            sb.Append(aggregation.ResolvedHostileDotDamage.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(aggregation.ResolvedHostileDotLines.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(aggregation.UnresolvedHostileDotDamage.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(aggregation.UnresolvedHostileDotLines.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(unresolvedHostileSharePercent.ToString("0.00", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(actHostileTotal.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(aggregation.HostileDotTotalLines.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(FormatDiffPercent(actHostileTotalDiffPercent))).Append(',');
            sb.Append(aggregation.MissingSourceHostileDotDamage.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(aggregation.MissingSourceHostileDotLines.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(statusGreen.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(statusYellow.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(statusRed.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(summaryConclusion)).AppendLine();
            WriteTextFile(options.SummaryOutPath!, sb.ToString());
        }

        Console.WriteLine($"已写出 summary：{options.SummaryOutPath}");
    }

    private static void WriteMainCsvExport(
        Options options,
        IReadOnlyList<PlayerReconcileResult> results)
    {
        if (string.IsNullOrWhiteSpace(options.CsvOutPath))
            return;

        var sb = new StringBuilder();
        sb.AppendLine("Name,Job,ActorIdHex,PluginDotDamage,ActAttributedDotDamage,DiffPercent,Status,MatchMode,ActExperimentalDotDamage,ActExperimentalDiffPercent,ExperimentalMode,ExperimentalWindows,ExperimentalEvents,ExperimentalAmbiguousEvents,TopStatuses");
        foreach (var result in results)
        {
            var topStatuses = string.Join("; ",
                result.StatusBreakdowns.Select(static breakdown =>
                    $"0x{breakdown.StatusId:X}:{breakdown.Damage}({breakdown.EventCount})"));

            sb.Append(EscapeCsv(result.Player.Name)).Append(',');
            sb.Append(EscapeCsv(result.Player.Job)).Append(',');
            sb.Append(EscapeCsv(FormatActorId(result.Player.ActorId))).Append(',');
            sb.Append(result.Player.PluginDotDamage.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(result.ActDotDamage.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(FormatDiffPercent(result.DiffPercent))).Append(',');
            sb.Append(result.Status).Append(',');
            sb.Append(EscapeCsv(result.MatchMode)).Append(',');
            sb.Append((result.ExperimentalReconstruction?.ActDotDamage ?? 0).ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(FormatDiffPercent(result.ExperimentalReconstruction?.DiffPercent))).Append(',');
            sb.Append(EscapeCsv(result.ExperimentalReconstruction?.Label ?? string.Empty)).Append(',');
            sb.Append((result.ExperimentalReconstruction?.WindowCount ?? 0).ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append((result.ExperimentalReconstruction?.EventCount ?? 0).ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append((result.ExperimentalReconstruction?.AmbiguousEventCount ?? 0).ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(topStatuses)).AppendLine();
        }

        WriteTextFile(options.CsvOutPath!, sb.ToString());
        Console.WriteLine($"已写出 CSV：{options.CsvOutPath}");
    }

    private static void WriteStatusCsvExport(
        Options options,
        IReadOnlyList<PlayerReconcileResult> results)
    {
        if (string.IsNullOrWhiteSpace(options.CsvStatusOutPath))
            return;

        var sb = new StringBuilder();
        sb.AppendLine("Name,Job,ActorIdHex,MatchMode,StatusId,StatusIdHex,Damage,EventCount,SharePercent");
        foreach (var result in results)
        {
            foreach (var breakdown in result.StatusBreakdowns)
            {
                sb.Append(EscapeCsv(result.Player.Name)).Append(',');
                sb.Append(EscapeCsv(result.Player.Job)).Append(',');
                sb.Append(EscapeCsv(FormatActorId(result.Player.ActorId))).Append(',');
                sb.Append(EscapeCsv(result.MatchMode)).Append(',');
                sb.Append(breakdown.StatusId.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(EscapeCsv($"0x{breakdown.StatusId:X}")).Append(',');
                sb.Append(breakdown.Damage.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(breakdown.EventCount.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(breakdown.SharePercent.ToString("0.00", CultureInfo.InvariantCulture)).AppendLine();
            }
        }

        WriteTextFile(options.CsvStatusOutPath!, sb.ToString());
        Console.WriteLine($"已写出 status CSV：{options.CsvStatusOutPath}");
    }

    private static void WriteWindowCheckCsvExport(
        Options options,
        IReadOnlyList<DotWindowConsistencyResult> dotWindowConsistencyResults)
    {
        if (string.IsNullOrWhiteSpace(options.CsvWindowCheckOutPath))
            return;

        var sb = new StringBuilder();
        sb.AppendLine("Name,Job,ActorIdHex,PluginDotDamage,ActAttributedDotDamage,State,ZeroStatusDamage,ZeroStatusEventCount,KnownWindows,KnownActStatuses,Message");
        foreach (var result in dotWindowConsistencyResults)
        {
            var knownWindows = string.Join("; ",
                result.KnownStatusWindows.Select(window =>
                    $"0x{window.StatusId:X} {window.StatusName}x{window.ApplyCount} -> {window.TargetName} {FormatActorId(window.TargetId)} {FormatTimeOfDay(window.FirstApplied)}~{FormatTimeOfDay(window.LastApplied)}"));
            var knownActStatuses = string.Join("; ",
                result.KnownActStatuses.Select(status =>
                    $"0x{status.StatusId:X}:{status.Damage}({status.EventCount})"));

            sb.Append(EscapeCsv(result.Player.Name)).Append(',');
            sb.Append(EscapeCsv(result.Player.Job)).Append(',');
            sb.Append(EscapeCsv(FormatActorId(result.Player.ActorId))).Append(',');
            sb.Append(result.PluginDotDamage.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(result.ActDotDamage.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(FormatDotWindowConsistencyState(result.State))).Append(',');
            sb.Append(result.ZeroStatusDamage.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(result.ZeroStatusEventCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(knownWindows)).Append(',');
            sb.Append(EscapeCsv(knownActStatuses)).Append(',');
            sb.Append(EscapeCsv(result.Message)).AppendLine();
        }

        WriteTextFile(options.CsvWindowCheckOutPath!, sb.ToString());
        Console.WriteLine($"已写出 windowcheck CSV：{options.CsvWindowCheckOutPath}");
    }

    private static void WriteKnownDotCsvExport(
        Options options,
        IReadOnlyList<DotWindowConsistencyResult> dotWindowConsistencyResults)
    {
        if (string.IsNullOrWhiteSpace(options.CsvKnownDotOutPath))
            return;

        var sb = new StringBuilder();
        sb.AppendLine("Name,Job,ActorIdHex,StatusId,StatusIdHex,StatusName,PluginDotDamageTotal,ActAttributedDotDamageTotal,ActKnownStatusDamage,ActKnownStatusEvents,ActZeroStatusDamage,ActZeroStatusEvents,KnownWindowApplyCount,KnownWindowTargetCount,KnownWindowTargets,FirstApplied,LastApplied,Evidence,Note");

        foreach (var result in dotWindowConsistencyResults)
        {
            var canonicalJob = CanonicalizeJob(result.Player.Job);
            var knownStatusIds = ResolveKnownDotStatusIds(canonicalJob);
            if (knownStatusIds.Count == 0)
                continue;

            var observedStatusIds = result.KnownStatusWindows
                .Select(static window => window.StatusId)
                .Concat(result.KnownActStatuses.Select(static status => status.StatusId))
                .Distinct()
                .OrderBy(static statusId => statusId)
                .ToList();

            foreach (var statusId in observedStatusIds)
            {
                var windows = result.KnownStatusWindows
                    .Where(window => window.StatusId == statusId)
                    .OrderBy(static window => window.FirstApplied)
                    .ThenBy(static window => window.TargetId)
                    .ToList();
                var actKnownStatus = result.KnownActStatuses.FirstOrDefault(status => status.StatusId == statusId);
                var applyCount = windows.Sum(static window => window.ApplyCount);
                var targetCount = windows.Select(static window => window.TargetId).Distinct().Count();
                var targets = string.Join("; ", windows.Select(window => $"{window.TargetName} {FormatActorId(window.TargetId)} x{window.ApplyCount}"));
                var firstApplied = windows.Count > 0 ? FormatTimeOfDay(windows.Min(static window => window.FirstApplied)) : string.Empty;
                var lastApplied = windows.Count > 0 ? FormatTimeOfDay(windows.Max(static window => window.LastApplied)) : string.Empty;
                var evidence = ResolveKnownDotEvidence(actKnownStatus, applyCount, result.ZeroStatusDamage);
                var note = BuildKnownDotNote(actKnownStatus, applyCount, result.ZeroStatusDamage, result.ZeroStatusEventCount);
                var statusName = ResolveKnownDotStatusName(statusId, windows);

                sb.Append(EscapeCsv(result.Player.Name)).Append(',');
                sb.Append(EscapeCsv(result.Player.Job)).Append(',');
                sb.Append(EscapeCsv(FormatActorId(result.Player.ActorId))).Append(',');
                sb.Append(statusId.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(EscapeCsv($"0x{statusId:X}")).Append(',');
                sb.Append(EscapeCsv(statusName)).Append(',');
                sb.Append(result.PluginDotDamage.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(result.ActDotDamage.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append((actKnownStatus?.Damage ?? 0).ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append((actKnownStatus?.EventCount ?? 0).ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(result.ZeroStatusDamage.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(result.ZeroStatusEventCount.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(applyCount.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(targetCount.ToString(CultureInfo.InvariantCulture)).Append(',');
                sb.Append(EscapeCsv(targets)).Append(',');
                sb.Append(EscapeCsv(firstApplied)).Append(',');
                sb.Append(EscapeCsv(lastApplied)).Append(',');
                sb.Append(EscapeCsv(evidence)).Append(',');
                sb.Append(EscapeCsv(note)).AppendLine();
            }
        }

        WriteTextFile(options.CsvKnownDotOutPath!, sb.ToString());
        Console.WriteLine($"已写出 known-dot CSV：{options.CsvKnownDotOutPath}");
    }

    private static void WriteDotDiagnosticCsvExport(
        Options options,
        IReadOnlyList<string> dalamudLogPaths,
        IReadOnlyList<PlayerReconcileResult> results,
        IReadOnlyList<StatusWindowSummary> statusWindowSummaries,
        IReadOnlyList<DotDiagnosticSummary> dotDiagnosticSummaries)
    {
        if (string.IsNullOrWhiteSpace(options.CsvDotDiagnosticOutPath))
            return;

        var resultsByActorId = results
            .Where(static result => result.Player.ActorId.HasValue)
            .GroupBy(static result => result.Player.ActorId!.Value)
            .ToDictionary(static group => group.Key, static group => group.First());
        var resultsByName = results
            .GroupBy(static result => result.Player.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
        var diagnosticTotalBySourceId = dotDiagnosticSummaries
            .GroupBy(static summary => summary.SourceId)
            .ToDictionary(static group => group.Key, static group => group.Sum(static summary => summary.TickDamageSum));

        var sb = new StringBuilder();
        sb.AppendLine("Name,Job,ActorIdHex,ActionId,Action,StatusId,StatusIdHex,Status,DiagnosticTickEvents,DiagnosticTickDamageSum,DiagnosticCritTicks,DiagnosticMaxTickIndex,DiagnosticTargets,FirstTick,LastTick,PlayerDiagnosticDamageTotal,PluginDotDamageTotal,DiagnosticTotalVsPluginTotalDiffPercent,ActAttributedDotDamageTotal,ActKnownStatusDamage,ActKnownStatusEvents,DiagnosticVsActKnownStatusDiffPercent,ActZeroStatusDamage,ActZeroStatusEvents,KnownWindowApplyCount,KnownWindowTargetCount,KnownWindowTargets,Evidence,Conclusion");

        foreach (var summary in dotDiagnosticSummaries)
        {
            PlayerReconcileResult? playerResult = null;
            if (summary.SourceId != 0)
                resultsByActorId.TryGetValue(summary.SourceId, out playerResult);

            if (playerResult == null && !string.IsNullOrWhiteSpace(summary.SourceName))
                resultsByName.TryGetValue(summary.SourceName, out playerResult);

            var actKnownStatus = playerResult?.StatusBreakdowns.FirstOrDefault(status => status.StatusId == summary.StatusId);
            var zeroStatusDamage = playerResult?.StatusBreakdowns
                .Where(static status => status.StatusId == 0)
                .Sum(static status => status.Damage) ?? 0;
            var zeroStatusEvents = playerResult?.StatusBreakdowns
                .Where(static status => status.StatusId == 0)
                .Sum(static status => status.EventCount) ?? 0;
            var windows = statusWindowSummaries
                .Where(window => window.SourceId == summary.SourceId && window.StatusId == summary.StatusId)
                .OrderBy(static window => window.FirstApplied)
                .ThenBy(static window => window.TargetId)
                .ToList();
            var windowApplyCount = windows.Sum(static window => window.ApplyCount);
            var windowTargetCount = windows.Select(static window => window.TargetId).Distinct().Count();
            var windowTargets = string.Join("; ", windows.Select(window => $"{window.TargetName} {FormatActorId(window.TargetId)} x{window.ApplyCount}"));
            var diagnosticTotal = diagnosticTotalBySourceId.GetValueOrDefault(summary.SourceId);
            var pluginTotalForPlayer = playerResult?.Player.PluginDotDamage ?? 0;
            var diagnosticTotalVsPluginDiff = CalculateDiffPercent(diagnosticTotal, pluginTotalForPlayer);
            var diagnosticVsActKnownDiff = CalculateDiffPercent(summary.TickDamageSum, actKnownStatus?.Damage ?? 0);
            var evidence = ResolveDotDiagnosticEvidence(actKnownStatus, windowApplyCount, zeroStatusDamage);
            var conclusion = BuildDotDiagnosticConclusion(playerResult, actKnownStatus, windowApplyCount, zeroStatusDamage, zeroStatusEvents);

            sb.Append(EscapeCsv(playerResult?.Player.Name ?? summary.SourceName)).Append(',');
            sb.Append(EscapeCsv(playerResult?.Player.Job ?? string.Empty)).Append(',');
            sb.Append(EscapeCsv(FormatActorId(playerResult?.Player.ActorId ?? summary.SourceId))).Append(',');
            sb.Append(summary.ActionId.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv($"{summary.ActionName}[{summary.ActionId}]")).Append(',');
            sb.Append(summary.StatusId.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv($"0x{summary.StatusId:X}")).Append(',');
            sb.Append(EscapeCsv($"{summary.StatusName}/0x{summary.StatusId:X}")).Append(',');
            sb.Append(summary.TickEvents.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(summary.TickDamageSum.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(summary.CritTicks.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(summary.MaxTickIndex.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(summary.TargetsText)).Append(',');
            sb.Append(EscapeCsv(FormatTimeOfDay(summary.FirstTick))).Append(',');
            sb.Append(EscapeCsv(FormatTimeOfDay(summary.LastTick))).Append(',');
            sb.Append(diagnosticTotal.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(pluginTotalForPlayer.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(FormatDiffPercent(diagnosticTotalVsPluginDiff))).Append(',');
            sb.Append((playerResult?.ActDotDamage ?? 0).ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append((actKnownStatus?.Damage ?? 0).ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append((actKnownStatus?.EventCount ?? 0).ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(FormatDiffPercent(diagnosticVsActKnownDiff))).Append(',');
            sb.Append(zeroStatusDamage.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(zeroStatusEvents.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(windowApplyCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(windowTargetCount.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(windowTargets)).Append(',');
            sb.Append(EscapeCsv(evidence)).Append(',');
            sb.Append(EscapeCsv(conclusion)).AppendLine();
        }

        if (dotDiagnosticSummaries.Count == 0)
        {
            Console.WriteLine($"提示：没有从 Dalamud 日志候选中解析到当前战斗窗口内的 DOT诊断 Tick；候选日志 {dalamudLogPaths.Count} 个。");
        }

        WriteTextFile(options.CsvDotDiagnosticOutPath!, sb.ToString());
        Console.WriteLine($"已写出 dotdiagnostic CSV：{options.CsvDotDiagnosticOutPath}");
    }

    private sealed record ExportContext(
        long PluginTotal,
        long ActAttributedTotal,
        long ActHostileTotal,
        bool HasDisplayFilter,
        DateTimeOffset GeneratedAtUtc,
        decimal? ActAttributedDiffPercent,
        decimal? ActHostileTotalDiffPercent,
        decimal UnresolvedHostileSharePercent,
        string SummaryConclusion);

    private static void WriteTextFile(string path, string content)
    {
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(fullPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string EscapeCsv(string? text)
    {
        var value = text ?? string.Empty;
        if (!value.Contains('"') && !value.Contains(',') && !value.Contains('\r') && !value.Contains('\n'))
            return value;

        return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }

    private static bool TryParseHexOrDecimal(string? text, out uint value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[2..];

        return uint.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value)
               || uint.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool IsHostileActorId(uint actorId)
        => (actorId >> 28) == 0x4;

    private static bool TryParseActorIdFromCombatantKey(string? combatantKey, out uint actorId)
    {
        actorId = 0;
        if (string.IsNullOrWhiteSpace(combatantKey))
            return false;

        var hashIndex = combatantKey.LastIndexOf('#');
        if (hashIndex < 0 || hashIndex == combatantKey.Length - 1)
            return false;

        return TryParseHexOrDecimal(combatantKey[(hashIndex + 1)..], out actorId);
    }

    private static string ExtractNameFromCombatantKey(string? combatantKey)
    {
        if (string.IsNullOrWhiteSpace(combatantKey))
            return string.Empty;

        var hashIndex = combatantKey.LastIndexOf('#');
        return hashIndex > 0 ? combatantKey[..hashIndex] : combatantKey;
    }

    private static long ParseDisplayedAmount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var trimmed = text.Trim()
            .Replace(",", string.Empty, StringComparison.Ordinal)
            .Replace("，", string.Empty, StringComparison.Ordinal);

        if (trimmed is "0" or "--")
            return 0;

        decimal multiplier = 1m;
        if (trimmed.EndsWith("万", StringComparison.Ordinal) || trimmed.EndsWith("萬", StringComparison.Ordinal))
        {
            multiplier = 10_000m;
            trimmed = trimmed[..^1];
        }
        else if (trimmed.EndsWith("亿", StringComparison.Ordinal) || trimmed.EndsWith("億", StringComparison.Ordinal))
        {
            multiplier = 100_000_000m;
            trimmed = trimmed[..^1];
        }
        else if (trimmed.EndsWith("兆", StringComparison.Ordinal))
        {
            multiplier = 1_000_000_000_000m;
            trimmed = trimmed[..^1];
        }

        if (!decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && !decimal.TryParse(trimmed, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
        {
            return 0;
        }

        return (long)Math.Round(value * multiplier, MidpointRounding.AwayFromZero);
    }

    private static string CanonicalizeJob(string? job)
    {
        if (string.IsNullOrWhiteSpace(job))
            return string.Empty;

        var normalized = NormalizeToken(job);
        return JobAliasMap.TryGetValue(normalized, out var canonical)
            ? canonical
            : normalized;
    }

    private static string NormalizeToken(string text)
    {
        var chars = text
            .Trim()
            .ToLowerInvariant()
            .Where(static ch => !char.IsWhiteSpace(ch) && ch is not '-' and not '_' and not '/' and not '·' and not '.')
            .ToArray();
        return new string(chars);
    }

    private static string FormatActorId(uint? actorId)
        => actorId.HasValue ? $"0x{actorId.Value:X8}" : "未知";

    private static string FormatInteger(long value)
        => value.ToString("#,0", CultureInfo.InvariantCulture);

    private static string FormatDiffPercent(decimal? diffPercent)
    {
        if (!diffPercent.HasValue)
            return "N/A";

        return $"{diffPercent.Value:+0.00;-0.00;0.00}%";
    }

    private static string FormatPercent(decimal value)
        => value.ToString("0.00", CultureInfo.InvariantCulture) + "%";

    private static string FormatDotWindowConsistencyState(DotWindowConsistencyState state)
        => state switch
        {
            DotWindowConsistencyState.Ok => "OK",
            DotWindowConsistencyState.Warning => "WARN",
            DotWindowConsistencyState.PluginOnly => "PLUGIN",
            _ => "INFO",
        };

    private static string FormatDateTime(DateTimeOffset? value)
    {
        if (!value.HasValue)
            return "未知";

        return value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
    }

    private static string FormatTimeOfDay(DateTimeOffset value)
        => value.ToLocalTime().ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);

    private sealed record Options(
        string? HistoryPath,
        List<string> LogPaths,
        List<string> DalamudLogPaths,
        string? ActLogDirectory,
        bool Latest,
        string? Zone,
        HashSet<string> JobFilters,
        HashSet<string> PlayerFilters,
        bool IncludeSpecialDot,
        bool ShowStatusWindows,
        int TopStatusCount,
        string? SummaryOutPath,
        string? JsonOutPath,
        string? CsvOutPath,
        string? CsvStatusOutPath,
        string? CsvWindowCheckOutPath,
        string? CsvKnownDotOutPath,
        string? CsvDotDiagnosticOutPath)
    {
        public static Options? Parse(string[] args)
        {
            string? historyPath = null;
            var logPaths = new List<string>();
            var dalamudLogPaths = new List<string>();
            string? actLogDirectory = null;
            var latest = false;
            string? zone = null;
            var jobFilters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var playerFilters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var includeSpecialDot = false;
            var showStatusWindows = false;
            var topStatusCount = 3;
            string? summaryOutPath = null;
            string? jsonOutPath = null;
            string? csvOutPath = null;
            string? csvStatusOutPath = null;
            string? csvWindowCheckOutPath = null;
            string? csvKnownDotOutPath = null;
            string? csvDotDiagnosticOutPath = null;

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.Equals("--history", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    historyPath = args[++i];
                }
                else if (arg.Equals("--log", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    logPaths.AddRange(SplitCsv(args[++i]));
                }
                else if (arg.Equals("--act-log-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    actLogDirectory = args[++i];
                }
                else if (arg.Equals("--dalamud-log", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    dalamudLogPaths.AddRange(SplitCsv(args[++i]));
                }
                else if (arg.Equals("--latest", StringComparison.OrdinalIgnoreCase))
                {
                    latest = true;
                }
                else if (arg.Equals("--zone", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    zone = args[++i];
                }
                else if (arg.Equals("--jobs", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    foreach (var item in SplitCsv(args[++i]))
                    {
                        jobFilters.Add(CanonicalizeJob(item));
                    }
                }
                else if (arg.Equals("--players", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    foreach (var item in SplitCsv(args[++i]))
                    {
                        playerFilters.Add(item);
                    }
                }
                else if (arg.Equals("--top-status", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    if (!int.TryParse(args[++i], NumberStyles.Integer, CultureInfo.InvariantCulture, out topStatusCount))
                    {
                        topStatusCount = 3;
                    }

                    if (topStatusCount < 0)
                    {
                        topStatusCount = 0;
                    }
                }
                else if (arg.Equals("--include-special-dot", StringComparison.OrdinalIgnoreCase))
                {
                    includeSpecialDot = true;
                }
                else if (arg.Equals("--status-windows", StringComparison.OrdinalIgnoreCase))
                {
                    showStatusWindows = true;
                }
                else if (arg.Equals("--summary-out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    summaryOutPath = args[++i];
                }
                else if (arg.Equals("--json-out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    jsonOutPath = args[++i];
                }
                else if (arg.Equals("--csv-out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    csvOutPath = args[++i];
                }
                else if (arg.Equals("--csv-status-out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    csvStatusOutPath = args[++i];
                }
                else if (arg.Equals("--csv-windowcheck-out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    csvWindowCheckOutPath = args[++i];
                }
                else if (arg.Equals("--csv-known-dot-out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    csvKnownDotOutPath = args[++i];
                }
                else if (arg.Equals("--csv-dotdiagnostic-out", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    csvDotDiagnosticOutPath = args[++i];
                }
                else if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            return new Options(
                historyPath,
                logPaths,
                dalamudLogPaths,
                actLogDirectory,
                latest,
                zone,
                jobFilters,
                playerFilters,
                includeSpecialDot,
                showStatusWindows,
                topStatusCount,
                summaryOutPath,
                jsonOutPath,
                csvOutPath,
                csvStatusOutPath,
                csvWindowCheckOutPath,
                csvKnownDotOutPath,
                csvDotDiagnosticOutPath);
        }

        private static IEnumerable<string> SplitCsv(string text)
        {
            return text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(static item => !string.IsNullOrWhiteSpace(item));
        }
    }

    private sealed class HistoryExportPayload
    {
        public int Version { get; set; }

        public DateTimeOffset? ExportedAtUtc { get; set; }

        public List<HistoricalCombatData> Records { get; set; } = [];
    }

    private sealed class HistoricalCombatData
    {
        public string ZoneName { get; set; } = string.Empty;

        public string Duration { get; set; } = string.Empty;

        public CombatDataWrapper? Snapshot { get; set; }

        public DateTimeOffset? StartTimeUtc { get; set; }

        public DateTimeOffset? EndTimeUtc { get; set; }
    }

    private sealed class CombatDataWrapper
    {
        [JsonPropertyName("msg")]
        public CombatData? Msg { get; set; }
    }

    private sealed class CombatData
    {
        [JsonPropertyName("Combatant")]
        public Dictionary<string, Combatant> Combatant { get; set; } = [];
    }

    private sealed class Combatant
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("participantKind")]
        public string? ParticipantKind { get; set; }

        [JsonPropertyName("Job")]
        public string? Job { get; set; }

        [JsonPropertyName("dotDamage-*")]
        public string? DotDamageText { get; set; }
    }

    private sealed record PlayerEncounterEntry(
        string CombatantKey,
        string Name,
        string Job,
        uint? ActorId,
        long PluginDotDamage);

    private sealed record PlayerReconcileResult(
        PlayerEncounterEntry Player,
        long ActDotDamage,
        ReconcileStatus Status,
        decimal? DiffPercent,
        string MatchMode,
        IReadOnlyList<PlayerStatusBreakdown> StatusBreakdowns,
        ExperimentalPlayerReconstruction? ExperimentalReconstruction);

    private sealed record PlayerStatusBreakdown(
        uint StatusId,
        long Damage,
        int EventCount,
        decimal SharePercent);

    private sealed record StatusWindowSummary(
        string CombatantKey,
        string PlayerName,
        string Job,
        uint? PlayerActorId,
        uint SourceId,
        string SourceName,
        uint StatusId,
        string StatusName,
        uint TargetId,
        string TargetName,
        int ApplyCount,
        DateTimeOffset FirstApplied,
        DateTimeOffset LastApplied);

    private sealed record DotWindowConsistencyResult(
        PlayerEncounterEntry Player,
        long PluginDotDamage,
        long ActDotDamage,
        DotWindowConsistencyState State,
        string Message,
        IReadOnlyList<StatusWindowSummary> KnownStatusWindows,
        IReadOnlyList<PlayerStatusBreakdown> KnownActStatuses,
        long ZeroStatusDamage,
        int ZeroStatusEventCount);

    private sealed record ExperimentalPlayerReconstruction(
        string Label,
        long ActDotDamage,
        decimal? DiffPercent,
        int WindowCount,
        int EventCount,
        int AmbiguousEventCount,
        IReadOnlyList<ExperimentalWindowSummary> WindowSummaries,
        IReadOnlyList<ExperimentalSourceBreakdown> SourceBreakdowns);

    private sealed record ExperimentalWindowSummary(
        uint TargetId,
        string TargetName,
        uint StatusId,
        string StatusName,
        DateTimeOffset StartTime,
        DateTimeOffset EndTime,
        long Damage,
        int EventCount);

    private sealed record ExperimentalSourceBreakdown(
        uint SourceId,
        string SourceName,
        long Damage,
        int EventCount,
        decimal SharePercent);

    private sealed record ActDotEvent(
        DateTimeOffset Timestamp,
        uint TargetId,
        string TargetName,
        uint StatusId,
        long Damage,
        uint SourceId,
        string SourceName);

    private sealed record ActStatusApplyEvent(
        DateTimeOffset Timestamp,
        uint StatusId,
        string StatusName,
        uint SourceId,
        string SourceName,
        uint TargetId,
        string TargetName);

    private sealed record DotDiagnosticTick(
        DateTimeOffset Timestamp,
        string SourceName,
        uint SourceId,
        string TargetName,
        uint TargetId,
        string ActionName,
        uint ActionId,
        string StatusName,
        uint StatusId,
        long Amount,
        bool Crit,
        int TickIndex);

    private sealed record DotDiagnosticSummary(
        string SourceName,
        uint SourceId,
        string ActionName,
        uint ActionId,
        string StatusName,
        uint StatusId,
        int TickEvents,
        long TickDamageSum,
        int CritTicks,
        int MaxTickIndex,
        string TargetsText,
        DateTimeOffset FirstTick,
        DateTimeOffset LastTick);

    private sealed record DotDiagnosticKey(
        uint SourceId,
        string SourceName,
        uint ActionId,
        uint StatusId);

    private sealed record ExperimentalWhmDiaWindow(
        uint OwnerActorId,
        string OwnerName,
        uint TargetId,
        string TargetName,
        uint StatusId,
        string StatusName,
        DateTimeOffset StartTimestamp,
        DateTimeOffset EndTimestamp);

    private sealed record SourceBreakdownKey(uint SourceId, string SourceName);

    private enum ReconcileStatus
    {
        Green,
        Yellow,
        Red,
    }

    private enum DotWindowConsistencyState
    {
        None,
        Ok,
        Warning,
        PluginOnly,
    }

    private sealed class DotStatusAggregate
    {
        public long Damage { get; set; }

        public int EventCount { get; set; }
    }

    private sealed class DotDiagnosticAggregate
    {
        private readonly Dictionary<uint, string> targets = [];

        public DotDiagnosticAggregate(
            string sourceName,
            uint sourceId,
            string actionName,
            uint actionId,
            string statusName,
            uint statusId)
        {
            SourceName = sourceName;
            SourceId = sourceId;
            ActionName = actionName;
            ActionId = actionId;
            StatusName = statusName;
            StatusId = statusId;
        }

        public string SourceName { get; }

        public uint SourceId { get; }

        public string ActionName { get; }

        public uint ActionId { get; }

        public string StatusName { get; }

        public uint StatusId { get; }

        public int TickEvents { get; private set; }

        public long TickDamageSum { get; private set; }

        public int CritTicks { get; private set; }

        public int MaxTickIndex { get; private set; }

        public DateTimeOffset FirstTick { get; private set; }

        public DateTimeOffset LastTick { get; private set; }

        public void AddTick(DotDiagnosticTick tick)
        {
            TickEvents++;
            TickDamageSum += tick.Amount;
            if (tick.Crit)
                CritTicks++;
            MaxTickIndex = Math.Max(MaxTickIndex, tick.TickIndex);
            targets[tick.TargetId] = tick.TargetName;

            if (TickEvents == 1 || tick.Timestamp < FirstTick)
                FirstTick = tick.Timestamp;

            if (TickEvents == 1 || tick.Timestamp > LastTick)
                LastTick = tick.Timestamp;
        }

        public DotDiagnosticSummary ToSummary()
        {
            var targetsText = string.Join("; ", targets
                .OrderBy(static pair => pair.Value, StringComparer.OrdinalIgnoreCase)
                .ThenBy(static pair => pair.Key)
                .Select(static pair => $"{pair.Value}/0x{pair.Key:X8}"));

            return new DotDiagnosticSummary(
                SourceName,
                SourceId,
                ActionName,
                ActionId,
                StatusName,
                StatusId,
                TickEvents,
                TickDamageSum,
                CritTicks,
                MaxTickIndex,
                targetsText,
                FirstTick,
                LastTick);
        }
    }

    private sealed class ActAggregationResult
    {
        public Dictionary<uint, long> DamageBySourceId { get; } = [];

        public Dictionary<string, long> DamageBySourceName { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<uint, Dictionary<uint, DotStatusAggregate>> StatusBySourceId { get; } = [];

        public Dictionary<string, Dictionary<uint, DotStatusAggregate>> StatusBySourceName { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> LogsWithEncounterData { get; } = [];

        public List<string> LogReadErrors { get; } = [];

        public List<ActDotEvent> HostileDotEvents { get; } = [];

        public List<ActStatusApplyEvent> StatusApplyEvents { get; } = [];

        public DateTimeOffset? EncounterStartUtc { get; set; }

        public DateTimeOffset? EncounterEndUtc { get; set; }

        public long TotalLines { get; set; }

        public long DotEventLines { get; set; }

        public long DotEventLinesInEncounterWindow { get; set; }

        public long HostileDotLines { get; set; }

        public long HostileDotTotalLines => ResolvedHostileDotLines + UnresolvedHostileDotLines;

        public long HostileDotTotalDamage => ResolvedHostileDotDamage + UnresolvedHostileDotDamage;

        public long ResolvedHostileDotLines { get; set; }

        public long ResolvedHostileDotDamage { get; set; }

        public long UnresolvedHostileDotLines { get; set; }

        public long UnresolvedHostileDotDamage { get; set; }

        public long HostileOrSelfSourcedDotLines { get; set; }

        public long HostileOrSelfSourcedDotDamage { get; set; }

        public long MissingSourceHostileDotLines { get; set; }

        public long MissingSourceHostileDotDamage { get; set; }

        public long NonHostileTargetLines { get; set; }

        public long ExcludedStatusLines { get; set; }

        public long ParseFailures { get; set; }
    }

    private sealed class ExperimentalPlayerReconstructionBuilder
    {
        public ExperimentalPlayerReconstructionBuilder(IReadOnlyList<ExperimentalWhmDiaWindow> windows)
        {
            Windows = windows;
        }

        public IReadOnlyList<ExperimentalWhmDiaWindow> Windows { get; }

        public List<ActDotEvent> AssignedEvents { get; } = [];

        public int AmbiguousEventCount { get; set; }
    }
}
