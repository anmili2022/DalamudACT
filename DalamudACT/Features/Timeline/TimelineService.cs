using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DalamudACT;

internal sealed class TimelineService
{
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
    private readonly HashSet<string> spokenTtsKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> lastInstantTtsByActionKey = new(StringComparer.Ordinal);

    public TimelineService(PluginConfiguration config)
    {
        this.config = config;
    }

    public string StatusText => statusText;

    public string DefinitionName => definition?.Name ?? "时间轴";

    public bool HasTimeline => definition != null;

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
        lastInstantTtsByActionKey.Clear();
    }

    public void ObserveSystemLogMessage(string message, DateTime nowUtc)
    {
        if (definition == null || string.IsNullOrWhiteSpace(message))
            return;

        if (!message.Contains("被封锁", StringComparison.Ordinal) && !message.Contains("封锁", StringComparison.Ordinal))
            return;

        var syncEntry = definition.Entries
            .Where(entry => entry.EventType == "SystemLogMessage" && entry.TimeSeconds > Math.Max(0f, lastSystemLogSyncTimeSeconds) + 1f)
            .OrderBy(entry => entry.TimeSeconds)
            .FirstOrDefault();
        if (syncEntry == null)
            return;

        startedAtUtc = nowUtc - TimeSpan.FromSeconds(syncEntry.TimeSeconds);
        displayOffsetSeconds = 0f;
        lastSystemLogSyncTimeSeconds = syncEntry.TimeSeconds;
        spokenTtsKeys.Clear();
        statusText = $"已同步：{definition.Name} / 区域封锁提示";
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

        var current = startedAtUtc.HasValue ? (float)(nowUtc - startedAtUtc.Value).TotalSeconds : 0f;
        var syncEntry = definition.Entries
            .Where(entry => entry.ActionIds.Contains(actionId))
            .OrderBy(entry => Math.Abs(entry.TimeSeconds - current))
            .FirstOrDefault();
        if (syncEntry == null)
            return;

        var targetTime = ResolveJumpTargetTime(syncEntry) ?? syncEntry.TimeSeconds;
        startedAtUtc = nowUtc - TimeSpan.FromSeconds(targetTime);
        displayOffsetSeconds = 0f;
        lastSystemLogSyncTimeSeconds = Math.Max(lastSystemLogSyncTimeSeconds, targetTime);
        spokenTtsKeys.Clear();
        statusText = $"已同步：{definition.Name} / {syncEntry.DisplayText}";
    }

    private void ProcessInstantTtsWithoutTimeline(uint actionId, uint sourceId, DateTime nowUtc)
    {
        if (!config.EnableTimelineDailyRoutinesTts)
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
            if (!IsLikelyHostileBattleNpc(battleChara))
                continue;

            var actionId = TryGetCastingActionId(battleChara);
            if (actionId == 0)
                continue;

            ProcessInstantTtsWithoutTimeline(actionId, TryGetActorId(battleChara), DateTime.UtcNow);
        }
    }

    private static bool IsLikelyHostileBattleNpc(object battleChara)
    {
        var objectKind = GetPropertyValue(battleChara, "ObjectKind")?.ToString();
        if (!string.Equals(objectKind, "BattleNpc", StringComparison.OrdinalIgnoreCase))
            return false;

        var subKind = GetPropertyValue(battleChara, "SubKind")?.ToString();
        return string.IsNullOrWhiteSpace(subKind)
               || subKind.Contains("Enemy", StringComparison.OrdinalIgnoreCase)
               || subKind.Contains("BattleNpc", StringComparison.OrdinalIgnoreCase)
               || subKind == "5";
    }

    private static uint TryGetCastingActionId(object battleChara)
    {
        if (!TryGetBoolProperty(battleChara, "IsCasting"))
            return 0;

        return TryGetUInt32Property(battleChara, "CastActionId", "CastActionID", "CurrentCastActionId", "CurrentCastId");
    }

    private static uint TryGetActorId(object battleChara)
        => TryGetUInt32Property(battleChara, "EntityId", "ObjectId")
           is var entityId && entityId != 0
            ? entityId
            : unchecked((uint)TryGetUInt64Property(battleChara, "GameObjectId"));

    private static object? GetPropertyValue(object? instance, string propertyName)
    {
        try
        {
            return instance?.GetType().GetProperty(propertyName)?.GetValue(instance);
        }
        catch
        {
            return null;
        }
    }

    private static bool TryGetBoolProperty(object instance, string propertyName)
    {
        try
        {
            var value = GetPropertyValue(instance, propertyName);
            return value is bool boolValue && boolValue;
        }
        catch
        {
            return false;
        }
    }

    private static uint TryGetUInt32Property(object instance, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            try
            {
                var value = GetPropertyValue(instance, propertyName);
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

    private static ulong TryGetUInt64Property(object instance, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            try
            {
                var value = GetPropertyValue(instance, propertyName);
                if (value != null)
                    return Convert.ToUInt64(value);
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
        timelineIndex = null;
        loadedZoneId = 0;
        loadedZoneName = string.Empty;
        EnsureTimelineForZone(zoneId, zoneName);
    }

    private void EnsureTimelineForZone(uint zoneId, string zoneName)
    {
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

                definition = M9STimelineParser.ParseTimelineTextFile(candidate.Id, candidate.Name, path);
                sourcePath = path;
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

    private float? ResolveJumpTargetTime(TimelineEntry entry)
    {
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
            var text = SanitizeTtsText(ApplyTtsCorrections(BuildTtsText(hint, entry.DisplayText)));
            if (string.IsNullOrWhiteSpace(text))
                continue;

            if (!DalamudApi.TrySendChatCommand($"/pdr tts {text}"))
                LogHelper.Debug("时间轴", $"发送 DailyRoutines TTS 命令失败：{text}");
        }
    }

    private static string SanitizeTtsText(string text)
        => text
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

    private string BuildTtsText(string? hint, string skillName)
        => config.TimelineTtsContentMode switch
        {
            TimelineTtsContentMode.MechanicOnly => string.IsNullOrWhiteSpace(hint) ? string.Empty : hint,
            TimelineTtsContentMode.SkillOnly => skillName,
            _ => string.IsNullOrWhiteSpace(hint) ? skillName : $"{hint}，{skillName}",
        };

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

        foreach (var path in GetTimelineIndexCandidatePaths())
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                timelineIndex = JsonSerializer.Deserialize<List<TimelineIndexEntry>>(File.ReadAllText(path), TimelineJsonOptions)
                                ?? [];
                return timelineIndex;
            }
            catch (Exception ex)
            {
                statusText = $"读取时间轴索引失败：{ex.Message}";
            }
        }

        timelineIndex = [];
        return timelineIndex;
    }

    private static IEnumerable<string> GetTimelineIndexCandidatePaths()
    {
        yield return Path.Combine(DalamudApi.PluginInterface.ConfigDirectory.FullName, "Timeline", "Data", "timeline-index.json");
        yield return Path.Combine(TimelineRemoteResourceDownloader.GetCacheDataDirectory(), "timeline-index.json");
        yield return Path.Combine(AppContext.BaseDirectory, "Timeline", "Data", "timeline-index.json");
        yield return Path.Combine(Environment.CurrentDirectory, "DalamudACT", "Features", "Timeline", "Data", "timeline-index.json");
    }

    private static IEnumerable<string> GetTimelineTextCandidatePaths(string fileName)
    {
        foreach (var candidateFileName in GetLocalizedFileNameCandidates(fileName))
        {
            yield return Path.Combine(DalamudApi.PluginInterface.ConfigDirectory.FullName, "Timeline", "Data", candidateFileName);
            yield return Path.Combine(TimelineRemoteResourceDownloader.GetCacheDataDirectory(), candidateFileName);
            yield return Path.Combine(AppContext.BaseDirectory, "Timeline", "Data", candidateFileName);
            yield return Path.Combine(Environment.CurrentDirectory, "DalamudACT", "Features", "Timeline", "Data", candidateFileName);
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
}
