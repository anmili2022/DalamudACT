using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class Program
{
    private const string DefaultHistoryPath = @"C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\DalamudACT\history-records.json";
    private const string DefaultActLogDirectory = @"D:\ff14act\FFXIVLogs";

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

        var players = BuildPlayerEntries(encounter);
        players = ApplyPlayerFilters(players, options);

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
        var results = BuildResults(players, actAggregation);

        PrintEncounterHeader(historyPath, encounter, options, logPaths, excludedStatusIds, actAggregation);
        PrintResults(results, options.TopStatusCount);
        PrintAggregateFooter(results, actAggregation);

        WriteExports(options, historyPath, encounter, logPaths, excludedStatusIds, actAggregation, results);
        return 0;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("用法：");
        Console.WriteLine("  dotnet run --project tools/DotReconcile -- [--history <path>] [--log <path>] [--act-log-dir <dir>] [--latest] [--zone <text>] [--jobs <csv>] [--players <csv>] [--top-status <n>] [--include-special-dot] [--json-out <path>] [--csv-out <path>] [--csv-status-out <path>]");
        Console.WriteLine();
        Console.WriteLine("示例：");
        Console.WriteLine("  dotnet run --project tools/DotReconcile -- --latest --jobs whm,sge");
        Console.WriteLine("  dotnet run --project tools/DotReconcile -- --zone 缇坦妮雅 --players 阳介,在爱锈蚀之前 --top-status 5");
        Console.WriteLine("  dotnet run --project tools/DotReconcile -- --latest --json-out output\\dotreconcile.json --csv-out output\\dotreconcile.csv --csv-status-out output\\dotreconcile-status.csv");
        Console.WriteLine();
        Console.WriteLine("说明：");
        Console.WriteLine("  - 默认 history 路径：C:\\Users\\Administrator\\AppData\\Roaming\\XIVLauncherCN\\pluginConfigs\\DalamudACT\\history-records.json");
        Console.WriteLine("  - 默认 ACT 日志目录：D:\\ff14act\\FFXIVLogs");
        Console.WriteLine("  - ACT 口径默认只统计 hostile-only DoT：24|DoT 且目标 actorId 以 4 开头");
        Console.WriteLine("  - 玩家结果默认只统计 ACT 里 source 能归到玩家的那部分；如果扫描统计里出现“未归属 hostile DoT”，则下方玩家 ACT 值应视为下限");
        Console.WriteLine("  - 默认排除 status=35D（野火），因为插件 dotDamage-* 不计入它；如需包含，可加 --include-special-dot");
        Console.WriteLine("  - --top-status 0 表示不在终端打印 statusId 明细");
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

                if (!line.StartsWith("24|", StringComparison.Ordinal))
                    continue;

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

                if (IsUnresolvedHostileDotSource(targetId, targetName, sourceId, sourceName))
                {
                    result.UnresolvedHostileDotLines++;
                    result.UnresolvedHostileDotDamage += damage;

                    if (sourceId == 0 && string.IsNullOrWhiteSpace(sourceName))
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

        return sourceId == 0 && string.IsNullOrWhiteSpace(normalizedSourceName);
    }

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

    private static List<PlayerReconcileResult> BuildResults(
        IReadOnlyList<PlayerEncounterEntry> players,
        ActAggregationResult aggregation)
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

            results.Add(new PlayerReconcileResult(player, actDotDamage, status, diffPercent, matchMode, statusBreakdowns));
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
        }
    }

    private static void PrintAggregateFooter(
        IReadOnlyList<PlayerReconcileResult> results,
        ActAggregationResult aggregation)
    {
        var pluginTotal = results.Sum(static item => item.Player.PluginDotDamage);
        var actTotal = results.Sum(static item => item.ActDotDamage);
        var diff = CalculateDiffPercent(pluginTotal, actTotal);

        Console.WriteLine();
        Console.WriteLine($"显示玩家合计：插件 {FormatInteger(pluginTotal)} | ACT已归属 {FormatInteger(actTotal)} | 差异 {FormatDiffPercent(diff)}");
        Console.WriteLine($"ACT hostile-only 总体：已归属 {FormatInteger(aggregation.ResolvedHostileDotDamage)} 伤害 / {FormatInteger(aggregation.ResolvedHostileDotLines)} 行，未归属 {FormatInteger(aggregation.UnresolvedHostileDotDamage)} 伤害 / {FormatInteger(aggregation.UnresolvedHostileDotLines)} 行");
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
        IReadOnlySet<uint> excludedStatusIds,
        ActAggregationResult aggregation,
        IReadOnlyList<PlayerReconcileResult> results)
    {
        if (!string.IsNullOrWhiteSpace(options.JsonOutPath))
        {
            var exportModel = new
            {
                generatedAtUtc = DateTimeOffset.UtcNow,
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
                },
                encounter = new
                {
                    zoneName = encounter.ZoneName,
                    duration = encounter.Duration,
                    startTimeUtc = encounter.StartTimeUtc,
                    endTimeUtc = encounter.EndTimeUtc,
                },
                actScan = new
                {
                    totalLines = aggregation.TotalLines,
                    dotEventLines = aggregation.DotEventLines,
                    dotEventLinesInEncounterWindow = aggregation.DotEventLinesInEncounterWindow,
                    hostileDotLines = aggregation.HostileDotLines,
                    resolvedHostileDotLines = aggregation.ResolvedHostileDotLines,
                    resolvedHostileDotDamage = aggregation.ResolvedHostileDotDamage,
                    unresolvedHostileDotLines = aggregation.UnresolvedHostileDotLines,
                    unresolvedHostileDotDamage = aggregation.UnresolvedHostileDotDamage,
                    hostileOrSelfSourcedDotLines = aggregation.HostileOrSelfSourcedDotLines,
                    hostileOrSelfSourcedDotDamage = aggregation.HostileOrSelfSourcedDotDamage,
                    missingSourceHostileDotLines = aggregation.MissingSourceHostileDotLines,
                    missingSourceHostileDotDamage = aggregation.MissingSourceHostileDotDamage,
                    nonHostileTargetLines = aggregation.NonHostileTargetLines,
                    excludedStatusLines = aggregation.ExcludedStatusLines,
                    parseFailures = aggregation.ParseFailures,
                },
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

        if (!string.IsNullOrWhiteSpace(options.CsvOutPath))
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,Job,ActorIdHex,PluginDotDamage,ActAttributedDotDamage,DiffPercent,Status,MatchMode,TopStatuses");
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
                sb.Append(EscapeCsv(topStatuses)).AppendLine();
            }

            WriteTextFile(options.CsvOutPath!, sb.ToString());
            Console.WriteLine($"已写出 CSV：{options.CsvOutPath}");
        }

        if (!string.IsNullOrWhiteSpace(options.CsvStatusOutPath))
        {
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
    }

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

    private static string FormatDateTime(DateTimeOffset? value)
    {
        if (!value.HasValue)
            return "未知";

        return value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
    }

    private sealed record Options(
        string? HistoryPath,
        List<string> LogPaths,
        string? ActLogDirectory,
        bool Latest,
        string? Zone,
        HashSet<string> JobFilters,
        HashSet<string> PlayerFilters,
        bool IncludeSpecialDot,
        int TopStatusCount,
        string? JsonOutPath,
        string? CsvOutPath,
        string? CsvStatusOutPath)
    {
        public static Options? Parse(string[] args)
        {
            string? historyPath = null;
            var logPaths = new List<string>();
            string? actLogDirectory = null;
            var latest = false;
            string? zone = null;
            var jobFilters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var playerFilters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var includeSpecialDot = false;
            var topStatusCount = 3;
            string? jsonOutPath = null;
            string? csvOutPath = null;
            string? csvStatusOutPath = null;

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
                else if (arg.Equals("--help", StringComparison.OrdinalIgnoreCase) || arg.Equals("-h", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            return new Options(
                historyPath,
                logPaths,
                actLogDirectory,
                latest,
                zone,
                jobFilters,
                playerFilters,
                includeSpecialDot,
                topStatusCount,
                jsonOutPath,
                csvOutPath,
                csvStatusOutPath);
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
        IReadOnlyList<PlayerStatusBreakdown> StatusBreakdowns);

    private sealed record PlayerStatusBreakdown(
        uint StatusId,
        long Damage,
        int EventCount,
        decimal SharePercent);

    private enum ReconcileStatus
    {
        Green,
        Yellow,
        Red,
    }

    private sealed class DotStatusAggregate
    {
        public long Damage { get; set; }

        public int EventCount { get; set; }
    }

    private sealed class ActAggregationResult
    {
        public Dictionary<uint, long> DamageBySourceId { get; } = [];

        public Dictionary<string, long> DamageBySourceName { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<uint, Dictionary<uint, DotStatusAggregate>> StatusBySourceId { get; } = [];

        public Dictionary<string, Dictionary<uint, DotStatusAggregate>> StatusBySourceName { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> LogsWithEncounterData { get; } = [];

        public List<string> LogReadErrors { get; } = [];

        public DateTimeOffset? EncounterStartUtc { get; set; }

        public DateTimeOffset? EncounterEndUtc { get; set; }

        public long TotalLines { get; set; }

        public long DotEventLines { get; set; }

        public long DotEventLinesInEncounterWindow { get; set; }

        public long HostileDotLines { get; set; }

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
}
