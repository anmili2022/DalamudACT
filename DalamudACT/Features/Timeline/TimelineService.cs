using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DalamudACT;

internal sealed class TimelineService
{
    private const float AbilitySyncMaxDriftSeconds = 12f;
    private const float AbilitySyncMinCorrectionSeconds = 1.0f;
    private const float InCombatSyncCompensationSeconds = 0.3f;
    private static readonly TimeSpan OutOfCombatResetGrace = TimeSpan.FromSeconds(1.5);
    private const double InitialAbilitySyncConfirmWindowSeconds = 20d;
    private const double InitialAbilitySyncPairToleranceSeconds = 3d;
    private const double TimelineTtsDuplicateSuppressSeconds = 2d;
    private const string HardcodedSourceTimelineDataDirectory = @"E:\git\DalamudACT\DalamudACT\Features\Timeline\Data";
    private static readonly ConcurrentDictionary<string, TimelineLoadResult> TimelineDefinitionCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly PluginConfiguration config;
    private readonly AeAssistResourceDownloader aeAssistResources = new();
    private readonly TimelineRemoteResourceDownloader remoteResources = new();
    private TimelineDefinition? definition;
    private IReadOnlyList<TimelineIndexEntry>? timelineIndex;
    private DateTime? startedAtUtc;
    private DateTime? outOfCombatSinceUtc;
    private bool startedFromInCombatSync;
    private float displayOffsetSeconds;
    private string statusText = "尚未加载时间轴。";
    private string sourcePath = string.Empty;
    private uint loadedZoneId;
    private string loadedZoneName = string.Empty;
    private float lastSystemLogSyncTimeSeconds;
    private float lastNpcYellSyncTimeSeconds;
    private float lastMapEffectSyncTimeSeconds;
    private readonly HashSet<string> spokenTtsKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> spokenActionResponseKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> lastTimelineTtsTextUtc = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> lastInstantTtsByActionKey = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> observedStartsUsingCasts = new(StringComparer.Ordinal);
    private readonly List<ObservedTimelineAbility> pendingInitialAbilitySyncs = [];
    private TimelineDefinitionRuntimeIndex? runtimeIndex;
    private IReadOnlyList<TimelineVisibleEntry> cachedVisibleEntries = Array.Empty<TimelineVisibleEntry>();
    private DateTime cachedVisibleEntriesAtUtc;
    private float cachedVisibleEntriesCurrentSeconds = float.NaN;
    private int cachedVisibleSeconds;
    private int cachedMaxVisibleEntries;
    private string? forcedTimelinePath;
    private string? suppressedSavedForcedTimelinePath;
    private uint lastSavedForcedTimelineCheckZoneId;
    private string lastSavedForcedTimelineCheckPath = string.Empty;
    private Task<TimelineLoadResult>? pendingTimelineLoad;
    private string pendingTimelineLoadKey = string.Empty;
    private bool pendingTimelineLoadForced;
    private string autoDownloadStatusText = string.Empty;
    private readonly Dictionary<uint, DateTime> autoDownloadTimestamps = new();
    private DateTime? lastAutoDownloadCheckUtc;

    public TimelineService(PluginConfiguration config)
    {
        this.config = config;
    }

    public string StatusText => statusText;

    public string DefinitionName => definition?.Name ?? "时间轴";

    public string SourcePath => sourcePath;

    public bool HasTimeline => definition != null;

    public bool HasForcedTimeline => !string.IsNullOrWhiteSpace(forcedTimelinePath);

    public string AutoDownloadStatusText => autoDownloadStatusText;

    public void TriggerAutoDownloadForZone()
    {
        if (!config.TimelineAutoDownloadOnEnter || loadedZoneId == 0)
            return;

        if (lastAutoDownloadCheckUtc.HasValue && (DateTime.UtcNow - lastAutoDownloadCheckUtc.Value).TotalMinutes < 5)
            return;

        if (autoDownloadTimestamps.TryGetValue(loadedZoneId, out var lastDl)
            && (DateTime.UtcNow - lastDl).TotalHours < 1)
        {
            autoDownloadStatusText = "已是最新";
            return;
        }

        lastAutoDownloadCheckUtc = DateTime.UtcNow;
        _ = AutoDownloadAsync(loadedZoneId, loadedZoneName);
    }

    private async Task AutoDownloadAsync(uint zoneId, string zoneName)
    {
        autoDownloadStatusText = "检查中...";
        var result = await remoteResources.AutoDownloadForZoneAsync(zoneId, zoneName).ConfigureAwait(true);
        autoDownloadStatusText = result;

        if (result.Contains("已下载") || result.Contains("已更新"))
        {
            autoDownloadTimestamps[zoneId] = DateTime.UtcNow;
            InvalidateTimelineIndex();
            ReloadCurrentTimeline();
        }
    }

    public IReadOnlyList<TimelineAvailableEntry> GetAvailableTimelineEntries()
    {
        return GetTimelineIndex()
            .Select(entry =>
            {
                var path = GetTimelineTextCandidatePaths(entry.FileName).FirstOrDefault(File.Exists) ?? string.Empty;
                return new TimelineAvailableEntry(entry.Id, entry.Name, entry.ZoneId, entry.FileName, path);
            })
            .OrderBy(entry => entry.ZoneId ?? uint.MaxValue)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ToList();
    }

    public string DebugText
    {
        get
        {
            var zone = string.IsNullOrWhiteSpace(loadedZoneName)
                ? $"ZoneId={loadedZoneId}"
                : $"{loadedZoneName} (ZoneId={loadedZoneId})";
            return string.IsNullOrWhiteSpace(sourcePath)
                ? zone
                : $"{zone}\n{sourcePath}";
        }
    }

    public float CurrentTimeSeconds => startedAtUtc is DateTime startedAt
        ? Math.Max(0f, (float)(DateTime.UtcNow - startedAt).TotalSeconds)
        : 0f;

    private float DisplayTimeSeconds => CurrentTimeSeconds + displayOffsetSeconds;

    public bool IsRunning => startedAtUtc.HasValue;

    public string CurrentTimelineLineDebugText
    {
        get
        {
            if (definition == null || !startedAtUtc.HasValue)
                return string.Empty;

            var current = DisplayTimeSeconds;
            var entry = definition.Entries
                .Where(entry => entry.TimeSeconds <= current)
                .OrderByDescending(entry => entry.TimeSeconds)
                .FirstOrDefault();
            if (entry == null || entry.SourceLineNumber <= 0)
                return $"[{Math.Max(0f, current):0.00}]当前运行第-行";

            return $"[{Math.Max(0f, current):0.00}]当前运行第{entry.SourceLineNumber}行";
        }
    }

    public async Task<string> RefreshCurrentZoneTimelineAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var result = await remoteResources.RefreshCurrentZoneAsync(loadedZoneId, loadedZoneName, progress, cancellationToken).ConfigureAwait(true);
        InvalidateTimelineIndex();
        ReloadCurrentTimeline();
        return result;
    }

    public async Task<string> DownloadAllTimelinesAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var result = await remoteResources.DownloadAllAsync(progress, cancellationToken).ConfigureAwait(true);
        InvalidateTimelineIndex();
        ReloadCurrentTimeline();
        return result;
    }

    public string ForceLoadTimelineFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "请选择要强制加载的时间轴文件。";

        path = path.Trim().Trim('"');
        if (!File.Exists(path))
            return $"时间轴文件不存在：{path}";

        suppressedSavedForcedTimelinePath = null;
        forcedTimelinePath = path;
        LoadForcedTimeline(Path.GetFileNameWithoutExtension(path), path);
        return statusText;
    }

    public string ClearForcedTimeline()
    {
        suppressedSavedForcedTimelinePath = forcedTimelinePath ?? config.TimelineForceLoadPath;
        forcedTimelinePath = null;
        ReloadCurrentTimeline();
        return "已取消强制加载时间轴。";
    }

    public void Update(bool inCombat, uint zoneId, string zoneName)
    {
        EnsureTimelineForZone(zoneId, zoneName, inCombat);
        TriggerAutoDownloadForZone();

        if (definition == null)
        {
            if (inCombat)
                PollInstantCastTtsWithoutTimeline();
            else
            {
                lastInstantTtsByActionKey.Clear();
                lastTimelineTtsTextUtc.Clear();
            }
            return;
        }

        if (inCombat)
        {
            outOfCombatSinceUtc = null;
            if (!startedAtUtc.HasValue && RequiresSyncBeforeStart())
            {
                if (TryStartFromInCombatSync(DateTime.UtcNow))
                {
                    ProcessTimelineTts();
                    return;
                }

                statusText = $"已加载 {definition.Entries.Count} 条：{definition.Name}，等待首个 Boss 技能同步。";
                return;
            }

            if (!startedAtUtc.HasValue)
            {
                startedAtUtc = DateTime.UtcNow;
                outOfCombatSinceUtc = null;
                startedFromInCombatSync = false;
            }
            ProcessTimelineTts();
            return;
        }

        if (startedAtUtc.HasValue)
        {
            var nowUtc = DateTime.UtcNow;
            outOfCombatSinceUtc ??= nowUtc;
            if (nowUtc - outOfCombatSinceUtc.Value < OutOfCombatResetGrace)
                return;
        }

        outOfCombatSinceUtc = null;
        startedAtUtc = null;
        outOfCombatSinceUtc = null;
        displayOffsetSeconds = 0f;
        spokenTtsKeys.Clear();
        spokenActionResponseKeys.Clear();
        lastTimelineTtsTextUtc.Clear();
        lastInstantTtsByActionKey.Clear();
        pendingInitialAbilitySyncs.Clear();
        observedStartsUsingCasts.Clear();
        ResetTimelineProgressCaches();
    }

    private bool TryStartFromInCombatSync(DateTime nowUtc)
    {
        if (definition == null)
            return false;

        var syncEntry = runtimeIndex?.InCombatEntries.FirstOrDefault()
                        ?? definition.Entries
                            .Where(entry => entry.EventType == "InCombat")
                            .OrderBy(static entry => entry.TimeSeconds)
                            .FirstOrDefault();
        if (syncEntry == null)
            return false;

        var targetTime = (ResolveJumpTargetTime(syncEntry) ?? syncEntry.TimeSeconds) + InCombatSyncCompensationSeconds;
        startedAtUtc = nowUtc - TimeSpan.FromSeconds(targetTime);
        outOfCombatSinceUtc = null;
        startedFromInCombatSync = true;
        displayOffsetSeconds = 0f;
        spokenTtsKeys.Clear();
        spokenActionResponseKeys.Clear();
        lastTimelineTtsTextUtc.Clear();
        pendingInitialAbilitySyncs.Clear();
        ResetTimelineProgressCaches();
        statusText = $"已同步：{definition.Name} / 进入战斗";
        LogHelper.Debug("时间轴", $"InCombat 同步命中：time={syncEntry.TimeSeconds:0.0}, target={targetTime:0.0}");
        return true;
    }

    public void PollStartsUsingCasts(DateTime nowUtc, bool inCombat, IEnumerable<Dalamud.Game.ClientState.Objects.Types.IBattleChara>? battleCharas = null)
    {
        if (definition == null || !inCombat)
        {
            observedStartsUsingCasts.Clear();
            return;
        }

        foreach (var battleChara in battleCharas ?? DalamudApi.ObjectTable.OfType<Dalamud.Game.ClientState.Objects.Types.IBattleChara>())
        {
            if (!BattleCharaReflectionAccessor.IsLikelyHostileBattleNpc(battleChara))
                continue;

            var actionId = BattleCharaReflectionAccessor.GetCastingActionId(battleChara);
            if (actionId == 0)
                continue;

            var sourceId = BattleCharaReflectionAccessor.GetActorId(battleChara);
            var sourceName = battleChara.Name.TextValue?.Trim() ?? string.Empty;
            ObserveStartsUsing(actionId, nowUtc, sourceId, sourceName);
        }
    }

    private void ObserveStartsUsing(uint actionId, DateTime nowUtc, uint sourceId, string sourceName)
    {
        if (definition == null || actionId == 0)
            return;

        var key = $"{sourceId:X8}:{actionId:X8}";
        if (observedStartsUsingCasts.TryGetValue(key, out var lastSeen) && (nowUtc - lastSeen).TotalSeconds < 3d)
            return;

        var wasRunning = startedAtUtc.HasValue;
        var current = wasRunning ? (float)(nowUtc - startedAtUtc!.Value).TotalSeconds : 0f;
        observedStartsUsingCasts[key] = nowUtc;

        var entriesByAction = runtimeIndex?.StartsUsingByActionId;
        var sourceCandidates = entriesByAction != null && entriesByAction.TryGetValue(actionId, out var indexedStartsUsingEntries)
            ? indexedStartsUsingEntries
            : Array.Empty<TimelineEntry>();
        var candidates = FilterSourceMatches(sourceCandidates, sourceName);
        if (candidates.Count == 0)
        {
            ProcessStartsUsingResponseTts(actionId, nowUtc, current, wasRunning, sourceName);
            return;
        }

        var syncEntry = FindNearestEntry(candidates, current);

        var targetTime = ResolveJumpTargetTime(syncEntry) ?? syncEntry.TimeSeconds;
        if (wasRunning)
        {
            var drift = Math.Abs(targetTime - current);
            if (drift > AbilitySyncMaxDriftSeconds)
            {
                LogHelper.Debug(
                    "时间轴",
                    $"忽略读条同步大幅回跳：actionId={actionId:X}, source={sourceName}, current={current:0.0}, target={targetTime:0.0}, entry={syncEntry.DisplayText}");
                ProcessStartsUsingResponseTts(actionId, nowUtc, current, wasRunning, sourceName);
                return;
            }

            if (!startedFromInCombatSync && drift < AbilitySyncMinCorrectionSeconds)
            {
                ProcessStartsUsingResponseTts(actionId, nowUtc, current, wasRunning, sourceName);
                return;
            }
        }

        ApplyAbilitySync(syncEntry, nowUtc, wasRunning
            ? $"已同步：{definition.Name} / {syncEntry.DisplayText}"
            : $"已读条同步：{definition.Name} / {syncEntry.DisplayText}");
        ProcessStartsUsingResponseTts(actionId, nowUtc, targetTime, true, sourceName);
        LogHelper.Debug("时间轴", $"StartsUsing 同步命中：actionId={actionId:X}, source={sourceName}, time={syncEntry.TimeSeconds:0.0}, target={targetTime:0.0}");
    }

    private void ProcessStartsUsingResponseTts(uint actionId, DateTime nowUtc, float current, bool wasRunning, string sourceName)
    {
        if (!config.EnableTimelineDailyRoutinesTts || !config.TimelineTtsResponse || definition == null)
            return;

        var entriesByAction = runtimeIndex?.StartsUsingResponsesByActionId;
        var sourceCandidates = entriesByAction != null && entriesByAction.TryGetValue(actionId, out var indexedResponseEntries)
            ? indexedResponseEntries
            : Array.Empty<TimelineEntry>();
        var candidates = FilterSourceMatches(sourceCandidates, sourceName);
        if (candidates.Count == 0)
            return;

        var entry = wasRunning
            ? FindNearestEntry(candidates, current)
            : candidates[0];
        if (!entry.ActionResponses.TryGetValue(actionId, out var response) || string.IsNullOrWhiteSpace(response.Text))
            return;

        var key = BuildActionResponseKey(entry, actionId, response.Text);
        if (!spokenActionResponseKeys.Add(key))
            return;

        TrySendDailyRoutinesTts(BuildActionResponseTtsText(entry, response.Text), nowUtc, $"读条应对方案 actionId={actionId:X}");
    }

    private static bool IsSourceMatch(TimelineEntry entry, string sourceName)
    {
        if (entry.Sources.Count == 0)
            return true;

        foreach (var source in entry.Sources)
        {
            if (string.Equals(source, sourceName, StringComparison.OrdinalIgnoreCase))
                return true;

            try
            {
                if (Regex.IsMatch(sourceName, source, RegexOptions.IgnoreCase))
                    return true;
            }
            catch
            {
                // Treat invalid source regexes as literal names.
            }
        }

        return false;
    }

    private static IReadOnlyList<TimelineEntry> FilterSourceMatches(IReadOnlyList<TimelineEntry> entries, string sourceName)
    {
        if (entries.Count == 0)
            return Array.Empty<TimelineEntry>();

        List<TimelineEntry>? result = null;
        foreach (var entry in entries)
        {
            if (!IsSourceMatch(entry, sourceName))
                continue;

            result ??= new List<TimelineEntry>();
            result.Add(entry);
        }

        return result is { Count: > 0 } ? result : Array.Empty<TimelineEntry>();
    }

    private static TimelineEntry FindNearestEntry(IReadOnlyList<TimelineEntry> entries, float current)
    {
        var best = entries[0];
        var bestDistance = Math.Abs(best.TimeSeconds - current);
        for (var i = 1; i < entries.Count; i++)
        {
            var entry = entries[i];
            var distance = Math.Abs(entry.TimeSeconds - current);
            if (distance >= bestDistance)
                continue;

            best = entry;
            bestDistance = distance;
        }

        return best;
    }

    private static TimelineEntry? FindActionResponseEntry(
        IReadOnlyList<TimelineEntry> entries,
        uint actionId,
        float current,
        bool wasRunning,
        TimelineActionResponseTiming timing)
    {
        TimelineEntry? best = null;
        var bestDistance = float.MaxValue;
        foreach (var entry in entries)
        {
            if (!entry.ActionResponses.TryGetValue(actionId, out var response) || response.Timing != timing)
                continue;

            if (!wasRunning)
                return entry;

            var distance = Math.Abs(entry.TimeSeconds - current);
            if (distance >= bestDistance)
                continue;

            best = entry;
            bestDistance = distance;
        }

        return best;
    }

    private static int FindFirstEntryAfter(IReadOnlyList<TimelineEntry> entries, float current)
    {
        var low = 0;
        var high = entries.Count;
        while (low < high)
        {
            var mid = low + ((high - low) / 2);
            if (entries[mid].TimeSeconds <= current)
                low = mid + 1;
            else
                high = mid;
        }

        return low;
    }

    public void ObserveMapEffect(uint entityId, uint flags, uint location, DateTime nowUtc)
    {
        if (definition == null)
            return;

        var syncEntry = ResolveMapEffectSyncEntry(flags, location);
        if (syncEntry == null)
            return;

        var targetTime = ResolveJumpTargetTime(syncEntry) ?? syncEntry.TimeSeconds;
        startedAtUtc = nowUtc - TimeSpan.FromSeconds(targetTime);
        outOfCombatSinceUtc = null;
        displayOffsetSeconds = 0f;
        lastMapEffectSyncTimeSeconds = Math.Max(syncEntry.TimeSeconds, targetTime);
        spokenTtsKeys.Clear();
        spokenActionResponseKeys.Clear();
        lastTimelineTtsTextUtc.Clear();
        pendingInitialAbilitySyncs.Clear();
        ResetTimelineProgressCaches();
        LogHelper.Debug("时间轴", $"MapEffect 同步命中：time={syncEntry.TimeSeconds:0.0}, target={targetTime:0.0}, flags={flags:X}, location={location:X}, entityId={entityId:X}");
        statusText = $"已同步：{definition.Name} / 地图特效";
    }

    private TimelineEntry? ResolveMapEffectSyncEntry(uint flags, uint location)
    {
        if (definition == null)
            return null;

        var flagsHex = flags.ToString("X");
        var locationHex = location.ToString("X");

        var entries = runtimeIndex?.MapEffectEntries ?? definition.Entries;
        foreach (var entry in entries)
        {
            if (entry.EventType != "MapEffect"
                || entry.TimeSeconds <= Math.Max(0f, lastMapEffectSyncTimeSeconds) + 1f
                || entry.MapEffectFlags == null
                || entry.MapEffectLocation == null)
            {
                continue;
            }

            var flagMatch = string.Equals(entry.MapEffectFlags, flagsHex, StringComparison.OrdinalIgnoreCase);
            var locationMatch = IsMapEffectLocationMatch(locationHex, entry.MapEffectLocation);
            if (flagMatch && locationMatch)
                return entry;
        }

        return null;
    }

    private static bool IsMapEffectLocationMatch(string locationHex, string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return true;

        try
        {
            return Regex.IsMatch(locationHex, pattern, RegexOptions.IgnoreCase);
        }
        catch
        {
            return string.Equals(locationHex, pattern, StringComparison.OrdinalIgnoreCase);
        }
    }

    public void ObserveSystemLogMessage(string message, DateTime nowUtc)
    {
        message = NormalizeSystemLogMessage(message);
        if (definition == null || string.IsNullOrWhiteSpace(message))
            return;

        if (startedAtUtc == null && timelineLoadedAtUtc != null && (nowUtc - timelineLoadedAtUtc.Value).TotalSeconds < 3d)
            return;

        var syncEntry = ResolveSystemLogSyncEntry(message);
        if (syncEntry == null)
            return;

        var targetTime = ResolveJumpTargetTime(syncEntry) ?? syncEntry.TimeSeconds;
        startedAtUtc = nowUtc - TimeSpan.FromSeconds(targetTime);
        outOfCombatSinceUtc = null;
        displayOffsetSeconds = 0f;
        lastSystemLogSyncTimeSeconds = Math.Max(syncEntry.TimeSeconds, targetTime);
        spokenTtsKeys.Clear();
        spokenActionResponseKeys.Clear();
        lastTimelineTtsTextUtc.Clear();
        pendingInitialAbilitySyncs.Clear();
        ResetTimelineProgressCaches();
        LogHelper.Debug("时间轴", $"SystemLogMessage 同步命中：time={syncEntry.TimeSeconds:0.0}, target={targetTime:0.0}, id={syncEntry.SystemLogId ?? "-"}, param1={syncEntry.SystemLogParam1 ?? "-"}, hint={syncEntry.SystemLogTextHint ?? "-"}, message={message}");
        statusText = string.IsNullOrWhiteSpace(syncEntry.SystemLogTextHint)
            ? $"已同步：{definition.Name} / 区域封锁提示"
            : $"已同步：{definition.Name} / {syncEntry.SystemLogTextHint}";
    }

    private static string NormalizeSystemLogMessage(string message)
        => Regex.Replace(message.Trim(), @"^\[\d{1,2}:\d{2}(?::\d{2})?\]\s*", string.Empty);

    public void ObserveNpcYell(string message, DateTime nowUtc)
    {
        message = NormalizeChatMessage(message);
        if (definition == null || string.IsNullOrWhiteSpace(message))
            return;

        var syncEntry = ResolveNpcYellSyncEntry(message);
        if (syncEntry == null)
            return;

        var targetTime = ResolveJumpTargetTime(syncEntry) ?? syncEntry.TimeSeconds;
        startedAtUtc = nowUtc - TimeSpan.FromSeconds(targetTime);
        outOfCombatSinceUtc = null;
        displayOffsetSeconds = 0f;
        lastNpcYellSyncTimeSeconds = Math.Max(syncEntry.TimeSeconds, targetTime);
        spokenTtsKeys.Clear();
        spokenActionResponseKeys.Clear();
        lastTimelineTtsTextUtc.Clear();
        pendingInitialAbilitySyncs.Clear();
        ResetTimelineProgressCaches();
        LogHelper.Debug("时间轴", $"NpcYell 同步命中：time={syncEntry.TimeSeconds:0.0}, target={targetTime:0.0}, text={syncEntry.NpcYellText ?? syncEntry.Text}, message={message}");
        statusText = $"已同步：{definition.Name} / Boss 台词";
    }

    private static string NormalizeChatMessage(string message)
        => Regex.Replace(message.Trim(), @"^\[\d{1,2}:\d{2}(?::\d{2})?\]\s*", string.Empty);

    private TimelineEntry? ResolveNpcYellSyncEntry(string message)
    {
        if (definition == null)
            return null;

        var entries = runtimeIndex?.NpcYellEntries ?? definition.Entries;
        foreach (var entry in entries)
        {
            if (entry.EventType == "NpcYell" && IsWithinNpcYellWindow(entry) && IsNpcYellMatch(entry, message))
                return entry;
        }

        return null;
    }

    private bool IsWithinNpcYellWindow(TimelineEntry entry)
    {
        if (startedAtUtc == null)
            return entry.TimeSeconds > Math.Max(0f, lastNpcYellSyncTimeSeconds) + 1f;

        var currentTime = (float)(DateTime.UtcNow - startedAtUtc.Value).TotalSeconds;
        var windowMin = entry.TimeSeconds - entry.WindowFirst;
        var windowMax = entry.TimeSeconds + entry.WindowLast;
        if (currentTime < windowMin || currentTime > windowMax)
            return false;

        return entry.TimeSeconds > lastNpcYellSyncTimeSeconds + 1f;
    }

    private static bool IsNpcYellMatch(TimelineEntry entry, string message)
    {
        var expected = string.IsNullOrWhiteSpace(entry.NpcYellText)
            ? entry.Text
            : entry.NpcYellText;
        if (string.IsNullOrWhiteSpace(expected))
            return false;

        return message.Contains(expected.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static readonly ConcurrentDictionary<string, Regex?> LogMessageRegexCache = new();
    private DateTime? timelineLoadedAtUtc;

    private TimelineEntry? ResolveSystemLogSyncEntry(string message)
    {
        if (definition == null)
            return null;

        var entries = runtimeIndex?.SystemLogEntries ?? definition.Entries;
        foreach (var entry in entries)
        {
            if (entry.EventType != "SystemLogMessage" || !IsSystemLogResetEntry(entry) && !IsWithinSystemLogWindow(entry))
                continue;

            if (!IsLogMessageMatch(entry, message))
                continue;

            LogHelper.Debug("时间轴", $"SystemLogMessage 匹配：time={entry.TimeSeconds:0.0}, id={entry.SystemLogId}, message={message}");
            return entry;
        }

        return null;
    }

    private static bool IsSystemLogResetEntry(TimelineEntry entry)
        => entry.JumpTimeSeconds is <= 0.5f;

    private bool IsWithinSystemLogWindow(TimelineEntry entry)
    {
        if (startedAtUtc == null)
            return entry.TimeSeconds > Math.Max(0f, lastSystemLogSyncTimeSeconds) + 1f;

        var currentTime = (float)(DateTime.UtcNow - startedAtUtc.Value).TotalSeconds;
        var windowMin = entry.TimeSeconds - entry.WindowFirst;
        var windowMax = entry.TimeSeconds + entry.WindowLast;
        if (currentTime < windowMin || currentTime > windowMax)
            return false;

        return entry.TimeSeconds > lastSystemLogSyncTimeSeconds + 1f;
    }

    private static bool IsLogMessageMatch(TimelineEntry entry, string message)
    {
        var logId = entry.SystemLogId;
        if (string.IsNullOrWhiteSpace(logId))
            return false;

        var param1 = entry.SystemLogParam1 ?? string.Empty;
        var cacheKey = $"{logId}:{param1}";
        var regex = LogMessageRegexCache.GetOrAdd(cacheKey, _ =>
        {
            try
            {
                var sheet = DalamudApi.GameData.GetExcelSheet<Lumina.Excel.Sheets.LogMessage>();
                if (sheet == null)
                    return null;

                var rowId = Convert.ToUInt32(logId, 16);
                if (!sheet.TryGetRow(rowId, out var row))
                    return null;

                var pattern = BuildSystemLogPattern(row.Text.ToMacroString(), entry);
                if (string.IsNullOrWhiteSpace(pattern))
                    return null;

                return new Regex(pattern, RegexOptions.Compiled);
            }
            catch
            {
                return null;
            }
        });

        return regex?.IsMatch(message) == true;
    }

    private static string? BuildSystemLogPattern(string macro, TimelineEntry entry)
    {
        if (string.IsNullOrWhiteSpace(macro))
            return null;

        var expanded = macro;
        var placeName = ResolvePlaceName(entry.SystemLogParam1);
        if (!string.IsNullOrWhiteSpace(placeName))
            expanded = Regex.Replace(expanded, @"<sheet\(PlaceName,lnum1,0\)>", placeName);

        var escaped = Regex.Escape(expanded);
        return "^" + Regex.Replace(escaped, @"<.+?>", "(.+)") + "$";
    }

    private static string? ResolvePlaceName(string? placeNameIdHex)
    {
        if (string.IsNullOrWhiteSpace(placeNameIdHex))
            return null;

        try
        {
            var placeNameSheet = DalamudApi.GameData.GetExcelSheet<Lumina.Excel.Sheets.PlaceName>();
            var placeNameId = Convert.ToUInt32(placeNameIdHex, 16);
            if (placeNameSheet != null && placeNameSheet.TryGetRow(placeNameId, out var placeNameRow))
                return placeNameRow.Name.ExtractText();
        }
        catch
        {
        }

        return null;
    }

    public void ObserveAbility(uint actionId, DateTime nowUtc, uint sourceId = 0)
    {
        if (actionId == 0)
            return;

        if (definition == null)
        {
            ProcessInstantTtsWithoutTimeline(actionId, sourceId, nowUtc);
            return;
        }

        var wasRunning = startedAtUtc.HasValue;
        var current = wasRunning ? (float)(nowUtc - startedAtUtc!.Value).TotalSeconds : 0f;
        var entriesByAction = runtimeIndex?.AbilityByActionId;
        var candidates = entriesByAction != null && entriesByAction.TryGetValue(actionId, out var indexedAbilityEntries)
            ? indexedAbilityEntries
            : Array.Empty<TimelineEntry>();
        if (candidates.Count == 0)
            return;

        ProcessActionResponseTts(actionId, candidates, nowUtc, current, wasRunning);

        if (TryConfirmInitialAbilitySync(actionId, nowUtc, out var confirmedEntry))
        {
            ApplyAbilitySync(confirmedEntry, nowUtc, $"已确认同步：{definition.Name} / {confirmedEntry.DisplayText}");
            pendingInitialAbilitySyncs.Clear();
            return;
        }

        var syncEntry = FindNearestEntry(candidates, current);

        var targetTime = ResolveJumpTargetTime(syncEntry) ?? syncEntry.TimeSeconds;
        if (wasRunning)
        {
            var drift = Math.Abs(targetTime - current);
            if (drift > AbilitySyncMaxDriftSeconds)
            {
                LogHelper.Debug(
                    "时间轴",
                    $"忽略技能同步大幅回跳：actionId={actionId:X}, current={current:0.0}, target={targetTime:0.0}, entry={syncEntry.DisplayText}");
                return;
            }

            if (!startedFromInCombatSync && drift < AbilitySyncMinCorrectionSeconds)
                return;
        }

        ApplyAbilitySync(syncEntry, nowUtc, wasRunning
            ? $"已同步：{definition.Name} / {syncEntry.DisplayText}"
            : $"已临时同步：{definition.Name} / {syncEntry.DisplayText}，等待下一个技能确认分段。 ");

        if (!wasRunning)
            AddPendingInitialAbilitySync(actionId, nowUtc);
    }

    private bool TryConfirmInitialAbilitySync(uint actionId, DateTime nowUtc, out TimelineEntry confirmedEntry)
    {
        confirmedEntry = null!;
        if (definition == null || runtimeIndex == null || pendingInitialAbilitySyncs.Count == 0)
            return false;

        pendingInitialAbilitySyncs.RemoveAll(item => (nowUtc - item.ObservedAtUtc).TotalSeconds > InitialAbilitySyncConfirmWindowSeconds);
        if (pendingInitialAbilitySyncs.Count == 0)
            return false;

        var bestError = double.MaxValue;
        TimelineEntry? bestEntry = null;
        foreach (var observed in pendingInitialAbilitySyncs)
        {
            var observedDelta = (nowUtc - observed.ObservedAtUtc).TotalSeconds;
            if (observedDelta <= 0.25d || observed.ActionId == actionId)
                continue;

            if (!runtimeIndex.AbilityByActionId.TryGetValue(observed.ActionId, out var firstEntries)
                || !runtimeIndex.AbilityByActionId.TryGetValue(actionId, out var secondEntries))
            {
                continue;
            }

            foreach (var first in firstEntries)
            foreach (var second in secondEntries)
            {
                var timelineDelta = second.TimeSeconds - first.TimeSeconds;
                if (timelineDelta <= 0f)
                    continue;

                var error = Math.Abs(timelineDelta - observedDelta);
                if (error > InitialAbilitySyncPairToleranceSeconds || error >= bestError)
                    continue;

                bestError = error;
                bestEntry = second;
            }
        }

        if (bestEntry == null)
            return false;

        confirmedEntry = bestEntry;
        LogHelper.Debug("时间轴", $"两技能确认初始同步：actionId={actionId:X}, entry={bestEntry.DisplayText}, error={bestError:0.0}s");
        return true;
    }

    private void AddPendingInitialAbilitySync(uint actionId, DateTime nowUtc)
    {
        pendingInitialAbilitySyncs.RemoveAll(item => (nowUtc - item.ObservedAtUtc).TotalSeconds > InitialAbilitySyncConfirmWindowSeconds);
        if (pendingInitialAbilitySyncs.Any(item => item.ActionId == actionId && (nowUtc - item.ObservedAtUtc).TotalSeconds < 1d))
            return;

        pendingInitialAbilitySyncs.Add(new ObservedTimelineAbility(actionId, nowUtc));
    }

    private void ApplyAbilitySync(TimelineEntry syncEntry, DateTime nowUtc, string message)
    {
        var targetTime = ResolveJumpTargetTime(syncEntry) ?? syncEntry.TimeSeconds;
        startedAtUtc = nowUtc - TimeSpan.FromSeconds(targetTime);
        outOfCombatSinceUtc = null;
        startedFromInCombatSync = false;
        displayOffsetSeconds = 0f;
        lastSystemLogSyncTimeSeconds = Math.Max(lastSystemLogSyncTimeSeconds, targetTime);
        spokenTtsKeys.Clear();
        lastTimelineTtsTextUtc.Clear();
        ResetTimelineProgressCaches();
        statusText = message;
    }

    private void ProcessActionResponseTts(uint actionId, IReadOnlyList<TimelineEntry> candidates, DateTime nowUtc, float current, bool wasRunning)
    {
        if (!config.EnableTimelineDailyRoutinesTts)
            return;

        if (!config.TimelineTtsResponse)
            return;

        var entry = FindActionResponseEntry(candidates, actionId, current, wasRunning, TimelineActionResponseTiming.Ability);
        if (entry == null || !entry.ActionResponses.TryGetValue(actionId, out var response) || response.Timing != TimelineActionResponseTiming.Ability || string.IsNullOrWhiteSpace(response.Text))
            return;

        var key = BuildActionResponseKey(entry, actionId, response.Text);
        if (!spokenActionResponseKeys.Add(key))
            return;

        TrySendDailyRoutinesTts(BuildActionResponseTtsText(entry, response.Text), nowUtc, $"技能应对方案 actionId={actionId:X}");
    }

    private string BuildActionResponseTtsText(TimelineEntry entry, string responseText)
        => config.TimelineTtsSkillName
            ? $"{entry.DisplayText}，{responseText}"
            : responseText;

    private void ProcessInstantTtsWithoutTimeline(uint actionId, uint sourceId, DateTime nowUtc)
    {
        if (!config.EnableTimelineDailyRoutinesTts)
            return;

        if (!config.TimelineTtsMechanic)
            return;

        var hint = aeAssistResources.GetHint(actionId);
        if (string.IsNullOrWhiteSpace(hint))
            return;

        var key = $"{sourceId:X8}:{actionId:X8}";
        var dedupeSeconds = GetInstantTtsDedupeSeconds(actionId);
        if (lastInstantTtsByActionKey.TryGetValue(key, out var lastSpoken)
            && (nowUtc - lastSpoken).TotalSeconds < dedupeSeconds)
            return;

        lastInstantTtsByActionKey[key] = nowUtc;
        TrySendDailyRoutinesTts(hint, nowUtc, $"无时间轴即时 actionId={actionId:X}");
    }

    private static double GetInstantTtsDedupeSeconds(uint actionId)
    {
        const double minDedupeSeconds = 8d;
        const double maxDedupeSeconds = 60d;
        const double castBufferSeconds = 3d;

        var castSeconds = TryGetActionCastSeconds(actionId);
        return Math.Clamp(Math.Max(minDedupeSeconds, castSeconds + castBufferSeconds), minDedupeSeconds, maxDedupeSeconds);
    }

    private static double TryGetActionCastSeconds(uint actionId)
    {
        try
        {
            var sheet = DalamudApi.GameData.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            if (sheet == null || !sheet.TryGetRow(actionId, out var row))
                return 0d;

            var boxed = (object)row;
            var cast100ms = TryGetUInt32Property(boxed, "Cast100ms", "Cast100Ms", "CastTime100ms", "CastTime100Ms");
            if (cast100ms != 0)
                return cast100ms / 10d;
        }
        catch (Exception ex)
        {
            LogHelper.Debug("时间轴", ex, $"读取技能读条时间失败：actionId={actionId:X}");
        }

        return 0d;
    }

    private void PollInstantCastTtsWithoutTimeline()
    {
        if (!config.EnableTimelineDailyRoutinesTts)
            return;

        foreach (var battleChara in DalamudApi.ObjectTable.OfType<Dalamud.Game.ClientState.Objects.Types.IBattleChara>())
        {
            if (!BattleCharaReflectionAccessor.IsLikelyHostileBattleNpc(battleChara))
                continue;

            var actionId = BattleCharaReflectionAccessor.GetCastingActionId(battleChara);
            if (actionId == 0)
                continue;

            ProcessInstantTtsWithoutTimeline(actionId, BattleCharaReflectionAccessor.GetActorId(battleChara), DateTime.UtcNow);
        }
    }

    private static uint TryGetUInt32Property(object instance, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            try
            {
                var value = instance.GetType().GetProperty(propertyName)?.GetValue(instance);
                if (value != null)
                    return Convert.ToUInt32(value);
            }
            catch
            {
                // Try the next runtime property name.
            }
        }

        return 0;
    }

    public IReadOnlyList<TimelineVisibleEntry> GetVisibleEntries()
    {
        if (definition == null || !startedAtUtc.HasValue && RequiresSyncBeforeStart())
            return Array.Empty<TimelineVisibleEntry>();

        var visibleEntries = runtimeIndex?.VisibleEntries;
        if (visibleEntries == null || visibleEntries.Count == 0)
            return Array.Empty<TimelineVisibleEntry>();

        var current = DisplayTimeSeconds;
        var visibleSeconds = Math.Clamp(config.TimelineVisibleSeconds, 10, 600);
        var maxEntries = Math.Clamp(config.TimelineMaxVisibleEntries, 1, 30);
        var nowUtc = DateTime.UtcNow;
        if (cachedVisibleSeconds == visibleSeconds
            && cachedMaxVisibleEntries == maxEntries
            && nowUtc - cachedVisibleEntriesAtUtc < TimeSpan.FromMilliseconds(100)
            && Math.Abs(current - cachedVisibleEntriesCurrentSeconds) < 0.1f)
        {
            return cachedVisibleEntries;
        }

        var result = new List<TimelineVisibleEntry>(maxEntries);
        var startIndex = FindFirstEntryAfter(visibleEntries, current);
        for (var i = startIndex; i < visibleEntries.Count && result.Count < maxEntries; i++)
        {
            var entry = visibleEntries[i];
            var relative = entry.TimeSeconds - current;
            if (relative > visibleSeconds)
                break;

            result.Add(new TimelineVisibleEntry(entry, relative, GetMechanicHint(entry)));
        }

        cachedVisibleEntries = result;
        cachedVisibleEntriesAtUtc = nowUtc;
        cachedVisibleEntriesCurrentSeconds = current;
        cachedVisibleSeconds = visibleSeconds;
        cachedMaxVisibleEntries = maxEntries;
        return cachedVisibleEntries;
    }

    private void ResetRuntimeCaches()
    {
        runtimeIndex = null;
        cachedVisibleEntries = Array.Empty<TimelineVisibleEntry>();
        cachedVisibleEntriesAtUtc = DateTime.MinValue;
        cachedVisibleEntriesCurrentSeconds = float.NaN;
        cachedVisibleSeconds = 0;
        cachedMaxVisibleEntries = 0;
    }

    private void ResetTimelineProgressCaches()
    {
        cachedVisibleEntries = Array.Empty<TimelineVisibleEntry>();
        cachedVisibleEntriesAtUtc = DateTime.MinValue;
        cachedVisibleEntriesCurrentSeconds = float.NaN;
    }

    public void ReloadCurrentTimeline()
    {
        var zoneId = loadedZoneId;
        var zoneName = loadedZoneName;
        definition = null;
        sourcePath = string.Empty;
        displayOffsetSeconds = 0f;
        lastSystemLogSyncTimeSeconds = 0f;
        lastNpcYellSyncTimeSeconds = 0f;
        spokenTtsKeys.Clear();
        spokenActionResponseKeys.Clear();
        lastTimelineTtsTextUtc.Clear();
        pendingInitialAbilitySyncs.Clear();
        timelineIndex = null;
        ResetRuntimeCaches();
        loadedZoneId = 0;
        loadedZoneName = string.Empty;
        EnsureTimelineForZone(zoneId, zoneName, inCombat: true);
    }

    private void EnsureTimelineForZone(uint zoneId, string zoneName, bool inCombat)
    {
        if (loadedZoneId == zoneId
            && string.Equals(loadedZoneName, zoneName, StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(forcedTimelinePath)
            && definition != null
            && pendingTimelineLoad == null)
        {
            return;
        }

        if (loadedZoneId == zoneId
            && string.Equals(loadedZoneName, zoneName, StringComparison.Ordinal)
            && pendingTimelineLoad != null)
        {
            var pendingName = definition?.Name ?? "时间轴";
            StartOrFinishPendingTimelineLoad(pendingName);
            return;
        }

        TryApplySavedForcedTimelineForZone(zoneId);

        if (!string.IsNullOrWhiteSpace(forcedTimelinePath))
        {
            loadedZoneId = zoneId;
            loadedZoneName = zoneName;
            if (definition == null)
                StartOrFinishTimelineLoad("forced", Path.GetFileNameWithoutExtension(forcedTimelinePath), forcedTimelinePath, forced: true);
            return;
        }

        if (loadedZoneId == zoneId
            && string.Equals(loadedZoneName, zoneName, StringComparison.Ordinal)
            && definition != null
            && pendingTimelineLoad == null)
        {
            return;
        }

        loadedZoneId = zoneId;
        loadedZoneName = zoneName;
        startedAtUtc = null;
        outOfCombatSinceUtc = null;
        definition = null;
        sourcePath = string.Empty;
        displayOffsetSeconds = 0f;
        lastSystemLogSyncTimeSeconds = 0f;
        lastNpcYellSyncTimeSeconds = 0f;
        spokenTtsKeys.Clear();
        spokenActionResponseKeys.Clear();
        lastTimelineTtsTextUtc.Clear();
        pendingInitialAbilitySyncs.Clear();
        ResetRuntimeCaches();

        var candidate = ResolveCandidate(zoneId, zoneName);
        if (candidate == null)
        {
            statusText = string.IsNullOrWhiteSpace(zoneName)
                ? "当前区域没有时间轴。"
                : $"当前区域没有时间轴：{zoneName} ({zoneId})";
            return;
        }

        foreach (var path in GetTimelineTextCandidatePaths(candidate.FileName))
        {
            if (!File.Exists(path))
                continue;

            StartOrFinishTimelineLoad(candidate.Id, candidate.Name, path, forced: false);
            return;
        }

        statusText = $"未找到时间轴文件：{candidate.FileName}";
    }

    private void TryApplySavedForcedTimelineForZone(uint zoneId)
    {
        if (zoneId == 0 || !string.IsNullOrWhiteSpace(forcedTimelinePath))
            return;

        var path = config.TimelineForceLoadPath?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        if (string.Equals(path, suppressedSavedForcedTimelinePath, StringComparison.OrdinalIgnoreCase))
            return;

        if (lastSavedForcedTimelineCheckZoneId == zoneId
            && string.Equals(lastSavedForcedTimelineCheckPath, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        lastSavedForcedTimelineCheckZoneId = zoneId;
        lastSavedForcedTimelineCheckPath = path;

        var matched = TryReadTimelineFileZoneId(path, out var fileZoneId) && fileZoneId == zoneId
                      || GetTimelineIndex().Any(entry => entry.ZoneId == zoneId);
        if (!matched)
            return;

        forcedTimelinePath = path;
    }

    private static bool TryReadTimelineFileZoneId(string path, out uint zoneId)
    {
        zoneId = 0;
        try
        {
            foreach (var line in File.ReadLines(path).Take(40))
            {
                var match = Regex.Match(line, @"^\s*#\s*ZoneId\s*:\s*(?<zoneId>\d+)\s*$", RegexOptions.IgnoreCase);
                if (!match.Success)
                    continue;

                return uint.TryParse(match.Groups["zoneId"].Value, out zoneId);
            }
        }
        catch
        {
        }

        return false;
    }

    private void LoadForcedTimeline(string name, string path)
    {
        startedAtUtc = null;
        outOfCombatSinceUtc = null;
        displayOffsetSeconds = 0f;
        lastSystemLogSyncTimeSeconds = 0f;
        lastNpcYellSyncTimeSeconds = 0f;
        spokenTtsKeys.Clear();
        spokenActionResponseKeys.Clear();
        lastTimelineTtsTextUtc.Clear();
        pendingInitialAbilitySyncs.Clear();
        observedStartsUsingCasts.Clear();
        ResetRuntimeCaches();
        StartOrFinishTimelineLoad("forced", name, path, forced: true);
    }

    private void StartOrFinishTimelineLoad(string id, string name, string path, bool forced)
    {
        var cacheKey = BuildTimelineLoadCacheKey(id, name, path);
        if (TimelineDefinitionCache.TryGetValue(cacheKey, out var cached))
        {
            ApplyTimelineLoadResult(cached, forced);
            return;
        }

        if (pendingTimelineLoad != null)
        {
            if (!string.Equals(pendingTimelineLoadKey, cacheKey, StringComparison.OrdinalIgnoreCase))
            {
                statusText = forced ? $"正在加载强制时间轴：{name}" : $"正在加载时间轴：{name}";
                return;
            }

            StartOrFinishPendingTimelineLoad(name);
            return;
        }

        pendingTimelineLoadKey = cacheKey;
        pendingTimelineLoadForced = forced;
        pendingTimelineLoad = Task.Run(() => LoadTimelineDefinition(id, name, path));
        statusText = forced ? $"正在加载强制时间轴：{name}" : $"正在加载时间轴：{name}";
    }

    private void StartOrFinishPendingTimelineLoad(string name)
    {
        if (pendingTimelineLoad == null)
            return;

        if (!pendingTimelineLoad.IsCompleted)
        {
            statusText = pendingTimelineLoadForced ? $"正在加载强制时间轴：{name}" : $"正在加载时间轴：{name}";
            return;
        }

        try
        {
            var result = pendingTimelineLoad.GetAwaiter().GetResult();
            TimelineDefinitionCache[pendingTimelineLoadKey] = result;
            ApplyTimelineLoadResult(result, pendingTimelineLoadForced);
        }
        catch (Exception ex)
        {
            statusText = $"加载 {name} 失败：{ex.Message}";
            LogHelper.Warning("时间轴", ex, $"后台加载时间轴失败：{name}");
        }
        finally
        {
            pendingTimelineLoad = null;
            pendingTimelineLoadKey = string.Empty;
            pendingTimelineLoadForced = false;
        }
    }

    private void ApplyTimelineLoadResult(TimelineLoadResult result, bool forced)
    {
        definition = result.Definition;
        sourcePath = result.Path;
        timelineLoadedAtUtc = DateTime.UtcNow;
        pendingTimelineLoad = null;
        pendingTimelineLoadKey = string.Empty;
        pendingTimelineLoadForced = false;
        runtimeIndex = TimelineDefinitionRuntimeIndex.Build(definition);
        ResetTimelineProgressCaches();
        LogHelper.Debug("时间轴", $"加载时间轴文件：{result.Path}");
        statusText = forced
            ? $"已强制加载 {definition.Entries.Count} 条：{definition.Name}"
            : $"已加载 {definition.Entries.Count} 条：{definition.Name}";
    }

    private static TimelineLoadResult LoadTimelineDefinition(string id, string name, string path)
        => new(TimelineParser.ParseTimelineTextFile(id, name, path), path);

    private static string BuildTimelineLoadCacheKey(string id, string name, string path)
    {
        var lastWriteTicks = File.GetLastWriteTimeUtc(path).Ticks;
        return $"{Path.GetFullPath(path)}|{lastWriteTicks}|{id}|{name}";
    }

    private float? ResolveJumpTargetTime(TimelineEntry entry)
    {
        if (entry.JumpTimeSeconds.HasValue)
            return entry.JumpTimeSeconds.Value;

        if (definition == null || string.IsNullOrWhiteSpace(entry.JumpLabel))
            return null;

        return definition.Labels.TryGetValue(entry.JumpLabel, out var labelTime)
            ? labelTime
            : null;
    }

    private void ProcessTimelineTts()
    {
        if (!config.EnableTimelineDailyRoutinesTts || definition == null || !startedAtUtc.HasValue)
            return;

        var entries = runtimeIndex?.VisibleEntries;
        if (entries == null || entries.Count == 0)
            return;

        var current = DisplayTimeSeconds;
        var nowUtc = DateTime.UtcNow;
        var leadSeconds = Math.Clamp(config.TimelineTtsLeadSeconds, 1, 30);
        var startIndex = FindFirstEntryAfter(entries, current);
        for (var i = startIndex; i < entries.Count; i++)
        {
            var entry = entries[i];
            var relative = entry.TimeSeconds - current;
            if (relative > leadSeconds)
                break;

            var key = $"{entry.TimeSeconds:0.0}|{entry.DisplayText}";
            if (!spokenTtsKeys.Add(key))
                continue;

            var mechanicHint = GetMechanicHint(entry);
            string? actionResponseHint = null;
            var text = PrepareDailyRoutinesTtsText(BuildTtsText(entry, mechanicHint, actionResponseHint));
            if (string.IsNullOrWhiteSpace(text))
                continue;

            TrySendPreparedDailyRoutinesTts(text, nowUtc, "时间轴提前播报");
        }
    }

    private string PrepareDailyRoutinesTtsText(string text)
        => SanitizeTtsText(ApplyTtsCorrections(text));

    private bool TrySendDailyRoutinesTts(string rawText, DateTime nowUtc, string context)
        => TrySendPreparedDailyRoutinesTts(PrepareDailyRoutinesTtsText(rawText), nowUtc, context);

    private bool TrySendPreparedDailyRoutinesTts(string text, DateTime nowUtc, string context)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        PruneTimelineTtsTextDedupe(nowUtc);
        if (lastTimelineTtsTextUtc.TryGetValue(text, out var lastSpoken)
            && (nowUtc - lastSpoken).TotalSeconds < TimelineTtsDuplicateSuppressSeconds)
        {
            LogHelper.Debug("时间轴", $"抑制重复 TTS（{context}）：{text}");
            return false;
        }

        lastTimelineTtsTextUtc[text] = nowUtc;
        if (DalamudApi.TrySendChatCommand($"/pdr tts {text}"))
            return true;

        lastTimelineTtsTextUtc.Remove(text);
        LogHelper.Debug("时间轴", $"发送 DailyRoutines TTS 命令失败（{context}）：{text}");
        return false;
    }

    private void PruneTimelineTtsTextDedupe(DateTime nowUtc)
    {
        if (lastTimelineTtsTextUtc.Count == 0)
            return;

        var expireBefore = nowUtc - TimeSpan.FromSeconds(TimelineTtsDuplicateSuppressSeconds * 4d);
        foreach (var key in lastTimelineTtsTextUtc.Where(pair => pair.Value < expireBefore).Select(pair => pair.Key).ToArray())
            lastTimelineTtsTextUtc.Remove(key);
    }

    private static string SanitizeTtsText(string text)
        => Regex.Replace(text, @"\sx\s?\d+", "", RegexOptions.IgnoreCase)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

    private string BuildTtsText(TimelineEntry entry, string? mechanicHint, string? actionResponseHint)
    {
        if (entry.EventType == "Timer")
            return config.TimelineTtsResponse ? entry.DisplayText : string.Empty;

        var parts = new List<string>();
        if (config.TimelineTtsMechanic && !string.IsNullOrWhiteSpace(mechanicHint))
            parts.Add(mechanicHint);
        if (config.TimelineTtsResponse && !string.IsNullOrWhiteSpace(actionResponseHint))
            parts.Add(actionResponseHint);
        if (config.TimelineTtsSkillName)
            parts.Add(entry.DisplayText);
        return string.Join("，", parts);
    }

    private string ApplyTtsCorrections(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return config.ApplyTimelineTtsCorrections(text);
    }

    private string? GetMechanicHint(TimelineEntry entry)
        => string.IsNullOrWhiteSpace(entry.MechanicHint) ? null : entry.MechanicHint;

    private static string? GetActionResponseTimelineHint(TimelineEntry entry)
    {
        if (entry.ActionResponses.Count == 0)
            return null;

        var responses = entry.ActionResponses.Values
            .Select(static response => response.Text)
            .Where(static response => !string.IsNullOrWhiteSpace(response))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return responses.Count == 0 ? null : string.Join(" / ", responses);
    }

    private static string BuildActionResponseKey(TimelineEntry entry, uint actionId, string response)
        => $"{entry.TimeSeconds:0.0}|{actionId:X}|{response}";

    private bool RequiresSyncBeforeStart()
    {
        if (definition == null)
            return false;

        if (!string.IsNullOrWhiteSpace(forcedTimelinePath))
            return true;

        var firstVisible = runtimeIndex?.VisibleEntries.FirstOrDefault()
                           ?? definition.Entries
                               .Where(static entry => !entry.Hidden)
                               .OrderBy(static entry => entry.TimeSeconds)
                               .FirstOrDefault();
        return firstVisible?.TimeSeconds >= 300f;
    }

    private TimelineIndexEntry? ResolveCandidate(uint zoneId, string zoneName)
    {
        var index = GetTimelineIndex();
        return index.FirstOrDefault(entry => entry.Matches(zoneId, zoneName));
    }

    private IReadOnlyList<TimelineIndexEntry> GetTimelineIndex()
    {
        if (timelineIndex != null)
            return timelineIndex;

        var entriesById = new Dictionary<string, TimelineIndexEntry>(StringComparer.OrdinalIgnoreCase);
        AddDiscoveredUserTimelineFiles(entriesById);
        foreach (var path in GetTimelineIndexCandidatePaths())
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                var loadedEntries = JsonSerializer.Deserialize<List<TimelineIndexEntry>>(File.ReadAllText(path), TimelineJsonOptions)
                                    ?? [];
                LogHelper.Debug("时间轴", $"加载时间轴索引：{path}，entries={loadedEntries.Count}");
                foreach (var entry in loadedEntries)
                {
                    if (string.IsNullOrWhiteSpace(entry.Id))
                        continue;

                    entriesById[entry.Id] = entry;
                }
            }
            catch (Exception ex)
            {
                statusText = $"读取时间轴索引失败：{ex.Message}";
            }
        }

        timelineIndex = entriesById.Values.ToList();
        return timelineIndex;
    }

    private void InvalidateTimelineIndex()
        => timelineIndex = null;

    private static void AddDiscoveredUserTimelineFiles(Dictionary<string, TimelineIndexEntry> entriesById)
    {
        var root = GetUserTimelineDataDirectory();
        if (!Directory.Exists(root))
            return;

        try
        {
            foreach (var path in Directory.EnumerateFiles(root, "*.txt", SearchOption.AllDirectories))
            {
                if (!TryReadTimelineFileZoneId(path, out var zoneId))
                    continue;

                var relativePath = Path.GetRelativePath(root, path);
                var normalizedPath = relativePath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/');
                var id = $"user:{normalizedPath}";
                if (entriesById.ContainsKey(id))
                    continue;

                var name = Path.GetFileNameWithoutExtension(path);
                entriesById[id] = new TimelineIndexEntry(id, name, zoneId, null, normalizedPath);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Debug("时间轴", ex, $"扫描用户时间轴目录失败：{root}");
        }
    }

    private static IEnumerable<string> GetTimelineIndexCandidatePaths()
    {
        yield return Path.Combine(GetUserTimelineDataDirectory(), "timeline-index.json");
        foreach (var sourceDirectory in GetSourceTimelineDataDirectories())
            yield return Path.Combine(sourceDirectory, "timeline-index.json");
        yield return Path.Combine(HardcodedSourceTimelineDataDirectory, "timeline-index.json");
        yield return Path.Combine(AppContext.BaseDirectory, "Timeline", "Data", "timeline-index.json");
        yield return Path.Combine(TimelineRemoteResourceDownloader.GetCacheDataDirectory(), "timeline-index.json");
    }

    private static IEnumerable<string> GetTimelineTextCandidatePaths(string fileName)
    {
        foreach (var candidateFileName in GetLocalizedFileNameCandidates(fileName))
        {
            yield return Path.Combine(GetUserTimelineDataDirectory(), candidateFileName);
            foreach (var sourceDirectory in GetSourceTimelineDataDirectories())
                yield return Path.Combine(sourceDirectory, candidateFileName);
            yield return Path.Combine(HardcodedSourceTimelineDataDirectory, candidateFileName);
            yield return Path.Combine(AppContext.BaseDirectory, "Timeline", "Data", candidateFileName);
            yield return Path.Combine(TimelineRemoteResourceDownloader.GetCacheDataDirectory(), candidateFileName);
        }
    }

    private static string GetUserTimelineDataDirectory()
        => Path.Combine(DalamudApi.PluginInterface.ConfigDirectory.FullName, "Timeline", "Data");

    private static IEnumerable<string> GetLocalizedFileNameCandidates(string fileName)
    {
        if (fileName.EndsWith(".cn.txt", StringComparison.OrdinalIgnoreCase))
        {
            yield return fileName;
            yield break;
        }

        if (fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            yield return fileName[..^4] + ".cn.txt";

        yield return fileName;
    }

    private static IEnumerable<string> GetSourceTimelineDataDirectories()
    {
        var directory = AppContext.BaseDirectory;
        for (var i = 0; i < 8 && !string.IsNullOrWhiteSpace(directory); i++)
        {
            var candidate = Path.Combine(directory, "DalamudACT", "Features", "Timeline", "Data");
            if (Directory.Exists(candidate))
                yield return candidate;

            directory = Directory.GetParent(directory)?.FullName;
        }
    }

    private static readonly JsonSerializerOptions TimelineJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed record TimelineIndexEntry(
        string Id,
        string Name,
        uint? ZoneId,
        string[]? ZoneNameContains,
        string File)
    {
        public string FileName => File;

        public bool Matches(uint zoneId, string zoneName)
        {
            if (ZoneId.HasValue && ZoneId.Value == zoneId)
                return true;

            if (ZoneNameContains == null || string.IsNullOrWhiteSpace(zoneName))
                return false;

            return ZoneNameContains.Any(fragment => zoneName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed record ObservedTimelineAbility(uint ActionId, DateTime ObservedAtUtc);

    private sealed class TimelineDefinitionRuntimeIndex
    {
        public IReadOnlyList<TimelineEntry> VisibleEntries { get; private init; } = Array.Empty<TimelineEntry>();
        public IReadOnlyList<TimelineEntry> InCombatEntries { get; private init; } = Array.Empty<TimelineEntry>();
        public IReadOnlyList<TimelineEntry> MapEffectEntries { get; private init; } = Array.Empty<TimelineEntry>();
        public IReadOnlyList<TimelineEntry> NpcYellEntries { get; private init; } = Array.Empty<TimelineEntry>();
        public IReadOnlyList<TimelineEntry> SystemLogEntries { get; private init; } = Array.Empty<TimelineEntry>();
        public IReadOnlyDictionary<uint, IReadOnlyList<TimelineEntry>> AbilityByActionId { get; private init; } = new Dictionary<uint, IReadOnlyList<TimelineEntry>>();
        public IReadOnlyDictionary<uint, IReadOnlyList<TimelineEntry>> StartsUsingByActionId { get; private init; } = new Dictionary<uint, IReadOnlyList<TimelineEntry>>();
        public IReadOnlyDictionary<uint, IReadOnlyList<TimelineEntry>> StartsUsingResponsesByActionId { get; private init; } = new Dictionary<uint, IReadOnlyList<TimelineEntry>>();

        public static TimelineDefinitionRuntimeIndex Build(TimelineDefinition definition)
        {
            var visibleEntries = new List<TimelineEntry>();
            var inCombatEntries = new List<TimelineEntry>();
            var mapEffectEntries = new List<TimelineEntry>();
            var npcYellEntries = new List<TimelineEntry>();
            var systemLogEntries = new List<TimelineEntry>();
            var abilityByActionId = new Dictionary<uint, List<TimelineEntry>>();
            var startsUsingByActionId = new Dictionary<uint, List<TimelineEntry>>();
            var startsUsingResponsesByActionId = new Dictionary<uint, List<TimelineEntry>>();

            foreach (var entry in definition.Entries)
            {
                if (!entry.Hidden)
                    visibleEntries.Add(entry);

                switch (entry.EventType)
                {
                    case "InCombat":
                        inCombatEntries.Add(entry);
                        break;
                    case "MapEffect":
                        mapEffectEntries.Add(entry);
                        break;
                    case "NpcYell":
                        npcYellEntries.Add(entry);
                        break;
                    case "SystemLogMessage":
                        systemLogEntries.Add(entry);
                        break;
                    case "Ability":
                        AddByActionIds(abilityByActionId, entry);
                        break;
                    case "StartsUsing":
                        AddByActionIds(startsUsingByActionId, entry);
                        break;
                }

                foreach (var (actionId, response) in entry.ActionResponses)
                {
                    if (response.Timing == TimelineActionResponseTiming.StartsUsing)
                        AddByActionId(startsUsingResponsesByActionId, actionId, entry);
                }
            }

            return new TimelineDefinitionRuntimeIndex
            {
                VisibleEntries = visibleEntries,
                InCombatEntries = inCombatEntries,
                MapEffectEntries = mapEffectEntries,
                NpcYellEntries = npcYellEntries,
                SystemLogEntries = systemLogEntries,
                AbilityByActionId = FreezeActionIndex(abilityByActionId),
                StartsUsingByActionId = FreezeActionIndex(startsUsingByActionId),
                StartsUsingResponsesByActionId = FreezeActionIndex(startsUsingResponsesByActionId),
            };
        }

        private static void AddByActionIds(Dictionary<uint, List<TimelineEntry>> index, TimelineEntry entry)
        {
            foreach (var actionId in entry.ActionIds)
                AddByActionId(index, actionId, entry);
        }

        private static void AddByActionId(Dictionary<uint, List<TimelineEntry>> index, uint actionId, TimelineEntry entry)
        {
            if (!index.TryGetValue(actionId, out var entries))
            {
                entries = new List<TimelineEntry>();
                index[actionId] = entries;
            }

            entries.Add(entry);
        }

        private static IReadOnlyDictionary<uint, IReadOnlyList<TimelineEntry>> FreezeActionIndex(Dictionary<uint, List<TimelineEntry>> index)
        {
            var frozen = new Dictionary<uint, IReadOnlyList<TimelineEntry>>(index.Count);
            foreach (var (actionId, entries) in index)
                frozen[actionId] = entries;
            return frozen;
        }
    }

    private sealed record TimelineLoadResult(TimelineDefinition Definition, string Path);

    public sealed record TimelineAvailableEntry(string Id, string Name, uint? ZoneId, string FileName, string ResolvedPath);

}
