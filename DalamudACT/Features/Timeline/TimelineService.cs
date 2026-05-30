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
    private const double InitialAbilitySyncConfirmWindowSeconds = 20d;
    private const double InitialAbilitySyncPairToleranceSeconds = 3d;
    private readonly PluginConfiguration config;
    private readonly TimelineMechanicHintProvider mechanicHints = new();
    private readonly AeAssistResourceDownloader aeAssistResources = new();
    private readonly TimelineRemoteResourceDownloader remoteResources = new();
    private TimelineDefinition? definition;
    private IReadOnlyList<TimelineIndexEntry>? timelineIndex;
    private DateTime? startedAtUtc;
    private float displayOffsetSeconds;
    private string statusText = "尚未加载时间轴。";
    private string sourcePath = string.Empty;
    private uint loadedZoneId;
    private string loadedZoneName = string.Empty;
    private float lastSystemLogSyncTimeSeconds;
    private float lastMapEffectSyncTimeSeconds;
    private readonly HashSet<string> spokenTtsKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> spokenActionResponseKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> lastInstantTtsByActionKey = new(StringComparer.Ordinal);
    private readonly List<ObservedTimelineAbility> pendingInitialAbilitySyncs = [];
    private string? forcedTimelinePath;

    public TimelineService(PluginConfiguration config)
    {
        this.config = config;
    }

    public string StatusText => statusText;

    public string DefinitionName => definition?.Name ?? "时间轴";

    public bool HasTimeline => definition != null;

    public bool HasForcedTimeline => !string.IsNullOrWhiteSpace(forcedTimelinePath);

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

    public Task<string> RefreshCurrentZoneTimelineAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        => remoteResources.RefreshCurrentZoneAsync(loadedZoneId, loadedZoneName, progress, cancellationToken);

    public Task<string> DownloadAllTimelinesAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        => remoteResources.DownloadAllAsync(progress, cancellationToken);

    public string ForceLoadTimelineFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "请选择要强制加载的时间轴文件。";

        path = path.Trim().Trim('"');
        if (!File.Exists(path))
            return $"时间轴文件不存在：{path}";

        forcedTimelinePath = path;
        LoadForcedTimeline(Path.GetFileNameWithoutExtension(path), path);
        return statusText;
    }

    public string ClearForcedTimeline()
    {
        forcedTimelinePath = null;
        ReloadCurrentTimeline();
        return "已取消强制加载时间轴。";
    }

    public void Update(bool inCombat, uint zoneId, string zoneName)
    {
        EnsureTimelineForZone(zoneId, zoneName);

        if (definition == null)
        {
            if (inCombat)
                PollInstantCastTtsWithoutTimeline();
            else
                lastInstantTtsByActionKey.Clear();
            return;
        }

        if (inCombat)
        {
            if (!startedAtUtc.HasValue && RequiresSyncBeforeStart())
            {
                statusText = $"已加载 {definition.Entries.Count} 条：{definition.Name}，等待首个 Boss 技能同步。";
                return;
            }

            startedAtUtc ??= DateTime.UtcNow;
            ProcessTimelineTts();
            return;
        }

        startedAtUtc = null;
        displayOffsetSeconds = 0f;
        spokenTtsKeys.Clear();
        spokenActionResponseKeys.Clear();
        lastInstantTtsByActionKey.Clear();
        pendingInitialAbilitySyncs.Clear();
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
        displayOffsetSeconds = 0f;
        lastMapEffectSyncTimeSeconds = Math.Max(syncEntry.TimeSeconds, targetTime);
        spokenTtsKeys.Clear();
        spokenActionResponseKeys.Clear();
        pendingInitialAbilitySyncs.Clear();
        LogHelper.Debug("时间轴", $"MapEffect 同步命中：time={syncEntry.TimeSeconds:0.0}, target={targetTime:0.0}, flags={flags:X}, location={location:X}, entityId={entityId:X}");
        statusText = $"已同步：{definition.Name} / 地图特效";
    }

    private TimelineEntry? ResolveMapEffectSyncEntry(uint flags, uint location)
    {
        if (definition == null)
            return null;

        var flagsHex = flags.ToString("X");
        var locationHex = location.ToString("X");

        return definition.Entries
            .Where(entry => entry.EventType == "MapEffect"
                            && entry.TimeSeconds > Math.Max(0f, lastMapEffectSyncTimeSeconds) + 1f
                            && entry.MapEffectFlags != null
                            && entry.MapEffectLocation != null)
            .OrderBy(entry => entry.TimeSeconds)
            .FirstOrDefault(entry =>
            {
                var flagMatch = string.Equals(entry.MapEffectFlags, flagsHex, StringComparison.OrdinalIgnoreCase);
                var locationMatch = IsMapEffectLocationMatch(locationHex, entry.MapEffectLocation);
                return flagMatch && locationMatch;
            });
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
        if (definition == null || string.IsNullOrWhiteSpace(message))
            return;

        if (startedAtUtc == null && timelineLoadedAtUtc != null && (nowUtc - timelineLoadedAtUtc.Value).TotalSeconds < 3d)
            return;

        var syncEntry = ResolveSystemLogSyncEntry(message);
        if (syncEntry == null)
            return;

        var targetTime = ResolveJumpTargetTime(syncEntry) ?? syncEntry.TimeSeconds;
        startedAtUtc = nowUtc - TimeSpan.FromSeconds(targetTime);
        displayOffsetSeconds = 0f;
        lastSystemLogSyncTimeSeconds = Math.Max(syncEntry.TimeSeconds, targetTime);
        spokenTtsKeys.Clear();
        spokenActionResponseKeys.Clear();
        pendingInitialAbilitySyncs.Clear();
        LogHelper.Debug("时间轴", $"SystemLogMessage 同步命中：time={syncEntry.TimeSeconds:0.0}, target={targetTime:0.0}, id={syncEntry.SystemLogId ?? "-"}, param1={syncEntry.SystemLogParam1 ?? "-"}, hint={syncEntry.SystemLogTextHint ?? "-"}, message={message}");
        statusText = string.IsNullOrWhiteSpace(syncEntry.SystemLogTextHint)
            ? $"已同步：{definition.Name} / 区域封锁提示"
            : $"已同步：{definition.Name} / {syncEntry.SystemLogTextHint}";
    }

    private static readonly ConcurrentDictionary<string, Regex?> LogMessageRegexCache = new();
    private DateTime? timelineLoadedAtUtc;

    private TimelineEntry? ResolveSystemLogSyncEntry(string message)
    {
        if (definition == null)
            return null;

        var candidates = definition.Entries
            .Where(entry => entry.EventType == "SystemLogMessage" && (IsSystemLogResetEntry(entry) || entry.TimeSeconds > Math.Max(0f, lastSystemLogSyncTimeSeconds) + 1f))
            .OrderBy(entry => entry.TimeSeconds)
            .ToList();

        if (candidates.Count == 0)
            return null;

        var matched = candidates.FirstOrDefault(entry => IsLogMessageMatch(entry, message));
        if (matched != null)
        {
            LogHelper.Debug("时间轴", $"SystemLogMessage 匹配：time={matched.TimeSeconds:0.0}, id={matched.SystemLogId}, message={message}");
            return matched;
        }

        return null;
    }

    private static bool IsSystemLogResetEntry(TimelineEntry entry)
        => entry.JumpTimeSeconds is <= 0.5f;

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
        var candidates = definition.Entries
            .Where(entry => entry.ActionIds.Contains(actionId))
            .ToList();
        if (candidates.Count == 0)
            return;

        ProcessActionResponseTts(actionId, candidates, nowUtc, current, wasRunning);

        if (TryConfirmInitialAbilitySync(actionId, nowUtc, out var confirmedEntry))
        {
            ApplyAbilitySync(confirmedEntry, nowUtc, $"已确认同步：{definition.Name} / {confirmedEntry.DisplayText}");
            pendingInitialAbilitySyncs.Clear();
            return;
        }

        var syncEntry = candidates
            .OrderBy(entry => Math.Abs(entry.TimeSeconds - current))
            .First();

        var targetTime = ResolveJumpTargetTime(syncEntry) ?? syncEntry.TimeSeconds;
        if (wasRunning && Math.Abs(targetTime - current) > AbilitySyncMaxDriftSeconds)
        {
            LogHelper.Debug(
                "时间轴",
                $"忽略技能同步大幅回跳：actionId={actionId:X}, current={current:0.0}, target={targetTime:0.0}, entry={syncEntry.DisplayText}");
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
        if (definition == null || pendingInitialAbilitySyncs.Count == 0)
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

            var firstEntries = definition.Entries.Where(entry => entry.ActionIds.Contains(observed.ActionId));
            var secondEntries = definition.Entries.Where(entry => entry.ActionIds.Contains(actionId));
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
        displayOffsetSeconds = 0f;
        lastSystemLogSyncTimeSeconds = Math.Max(lastSystemLogSyncTimeSeconds, targetTime);
        spokenTtsKeys.Clear();
        statusText = message;
    }

    private void ProcessActionResponseTts(uint actionId, IReadOnlyList<TimelineEntry> candidates, DateTime nowUtc, float current, bool wasRunning)
    {
        if (!config.EnableTimelineDailyRoutinesTts)
            return;

        if (!config.TimelineTtsResponse)
            return;

        var entry = wasRunning
            ? candidates.OrderBy(entry => Math.Abs(entry.TimeSeconds - current)).FirstOrDefault(entry => entry.ActionResponses.ContainsKey(actionId))
            : candidates.OrderBy(static entry => entry.TimeSeconds).FirstOrDefault(entry => entry.ActionResponses.ContainsKey(actionId));
        if (entry == null || !entry.ActionResponses.TryGetValue(actionId, out var response) || string.IsNullOrWhiteSpace(response))
            return;

        var key = $"{entry.TimeSeconds:0.0}|{actionId:X}|{response}";
        if (!spokenActionResponseKeys.Add(key))
            return;

        var text = SanitizeTtsText(ApplyTtsCorrections($"{entry.DisplayText}，{response}"));
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (!DalamudApi.TrySendChatCommand($"/pdr tts {text}"))
            LogHelper.Debug("时间轴", $"发送技能应对方案 TTS 命令失败：actionId={actionId:X}, text={text}");
    }

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
        var ttsHint = ApplyTtsCorrections(hint);
        if (!DalamudApi.TrySendChatCommand($"/pdr tts {ttsHint}"))
            LogHelper.Debug("时间轴", $"无时间轴即时 TTS 发送失败：{ttsHint} / actionId={actionId:X}");
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

        var current = DisplayTimeSeconds;
        var visibleSeconds = Math.Clamp(config.TimelineVisibleSeconds, 10, 600);
        var maxEntries = Math.Clamp(config.TimelineMaxVisibleEntries, 1, 30);
        return definition.Entries
            .Where(static entry => !entry.Hidden)
            .Select(entry => new TimelineVisibleEntry(entry, entry.TimeSeconds - current, GetMechanicHint(entry)))
            .Where(entry => entry.RelativeSeconds > 0f && entry.RelativeSeconds <= visibleSeconds)
            .OrderBy(entry => entry.Entry.TimeSeconds)
            .Take(maxEntries)
            .ToList();
    }

    public void ReloadCurrentTimeline()
    {
        var zoneId = loadedZoneId;
        var zoneName = loadedZoneName;
        definition = null;
        sourcePath = string.Empty;
        displayOffsetSeconds = 0f;
        lastSystemLogSyncTimeSeconds = 0f;
        spokenTtsKeys.Clear();
        spokenActionResponseKeys.Clear();
        pendingInitialAbilitySyncs.Clear();
        timelineIndex = null;
        loadedZoneId = 0;
        loadedZoneName = string.Empty;
        EnsureTimelineForZone(zoneId, zoneName);
    }

    private void EnsureTimelineForZone(uint zoneId, string zoneName)
    {
        if (!string.IsNullOrWhiteSpace(forcedTimelinePath))
        {
            loadedZoneId = zoneId;
            loadedZoneName = zoneName;
            if (definition == null)
                LoadForcedTimeline(Path.GetFileNameWithoutExtension(forcedTimelinePath), forcedTimelinePath);
            return;
        }

        if (loadedZoneId == zoneId && string.Equals(loadedZoneName, zoneName, StringComparison.Ordinal))
            return;

        loadedZoneId = zoneId;
        loadedZoneName = zoneName;
        startedAtUtc = null;
        definition = null;
        sourcePath = string.Empty;
        displayOffsetSeconds = 0f;
        lastSystemLogSyncTimeSeconds = 0f;
        spokenTtsKeys.Clear();
        spokenActionResponseKeys.Clear();
        pendingInitialAbilitySyncs.Clear();

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
            try
            {
                if (!File.Exists(path))
                    continue;

                definition = TimelineParser.ParseTimelineTextFile(candidate.Id, candidate.Name, path);
                sourcePath = path;
                timelineLoadedAtUtc = DateTime.UtcNow;
                LogHelper.Debug("时间轴", $"加载时间轴文件：{path}");
                statusText = $"已加载 {definition.Entries.Count} 条：{candidate.Name}";
                return;
            }
            catch (Exception ex)
            {
                statusText = $"加载 {candidate.Name} 失败：{ex.Message}";
            }
        }

        statusText = $"未找到时间轴文件：{candidate.FileName}";
    }

    private void LoadForcedTimeline(string name, string path)
    {
        startedAtUtc = null;
        displayOffsetSeconds = 0f;
        lastSystemLogSyncTimeSeconds = 0f;
        spokenTtsKeys.Clear();
        spokenActionResponseKeys.Clear();
        pendingInitialAbilitySyncs.Clear();
        definition = TimelineParser.ParseTimelineTextFile("forced", name, path);
        sourcePath = path;
        timelineLoadedAtUtc = DateTime.UtcNow;
        LogHelper.Debug("时间轴", $"加载时间轴文件：{path}");
        statusText = $"已强制加载 {definition.Entries.Count} 条：{name}";
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

        var current = DisplayTimeSeconds;
        var leadSeconds = Math.Clamp(config.TimelineTtsLeadSeconds, 1, 30);
        foreach (var entry in definition.Entries.Where(static entry => !entry.Hidden))
        {
            var relative = entry.TimeSeconds - current;
            if (relative <= 0f || relative > leadSeconds)
                continue;

            var key = $"{entry.TimeSeconds:0.0}|{entry.DisplayText}";
            if (!spokenTtsKeys.Add(key))
                continue;

            var hint = GetMechanicHint(entry);
            var text = SanitizeTtsText(ApplyTtsCorrections(BuildTtsText(entry, hint)));
            if (string.IsNullOrWhiteSpace(text))
                continue;

            if (!DalamudApi.TrySendChatCommand($"/pdr tts {text}"))
                LogHelper.Debug("时间轴", $"发送 DailyRoutines TTS 命令失败：{text}");
        }
    }

    private static string SanitizeTtsText(string text)
        => Regex.Replace(text, @"\sx\s?\d+", "", RegexOptions.IgnoreCase)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

    private string BuildTtsText(TimelineEntry entry, string? hint)
    {
        if (entry.EventType == "Timer")
            return config.TimelineTtsResponse ? entry.DisplayText : string.Empty;

        var parts = new List<string>();
        if (config.TimelineTtsMechanic && !string.IsNullOrWhiteSpace(hint))
            parts.Add(hint);
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
        => string.IsNullOrWhiteSpace(entry.MechanicHint)
            ? mechanicHints.GetHint(entry)
            : entry.MechanicHint;

    private bool RequiresSyncBeforeStart()
    {
        if (definition == null)
            return false;

        var firstVisible = definition.Entries
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

        var entries = new List<TimelineIndexEntry>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                    if (string.IsNullOrWhiteSpace(entry.Id) || !seenIds.Add(entry.Id))
                        continue;

                    entries.Add(entry);
                }
            }
            catch (Exception ex)
            {
                statusText = $"读取时间轴索引失败：{ex.Message}";
            }
        }

        timelineIndex = entries;
        return timelineIndex;
    }

    private static IEnumerable<string> GetTimelineIndexCandidatePaths()
    {
        yield return Path.Combine(DalamudApi.PluginInterface.ConfigDirectory.FullName, "Timeline", "Data", "timeline-index.json");
        foreach (var sourceDirectory in GetSourceTimelineDataDirectories())
            yield return Path.Combine(sourceDirectory, "timeline-index.json");
        yield return Path.Combine(AppContext.BaseDirectory, "Timeline", "Data", "timeline-index.json");
        yield return Path.Combine(TimelineRemoteResourceDownloader.GetCacheRootDirectory(), "timeline-index.json");
    }

    private static IEnumerable<string> GetTimelineTextCandidatePaths(string fileName)
    {
        foreach (var candidateFileName in GetLocalizedFileNameCandidates(fileName))
        {
            yield return Path.Combine(DalamudApi.PluginInterface.ConfigDirectory.FullName, "Timeline", "Data", candidateFileName);
            foreach (var sourceDirectory in GetSourceTimelineDataDirectories())
                yield return Path.Combine(sourceDirectory, candidateFileName);
            yield return Path.Combine(AppContext.BaseDirectory, "Timeline", "Data", candidateFileName);
            yield return Path.Combine(TimelineRemoteResourceDownloader.GetCacheRootDirectory(), candidateFileName);
        }
    }

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

}
