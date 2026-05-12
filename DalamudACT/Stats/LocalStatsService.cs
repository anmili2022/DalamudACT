using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dalamud.Game.ClientState.Buddy;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Objects.SubKinds;

namespace DalamudACT;

/// <summary>
/// 本地战斗统计核心，负责跟踪队伍成员、聚合伤害/治疗/承伤、生成历史记录和历史预览。
/// 相关参考：
/// - https://dalamud.dev/
/// - https://dalamud.dev/api/
/// 调整 ObjectTable、PartyList、BuddyList、BattleChara / GameObject 相关访问逻辑前，先对照 Dalamud 文档。
/// </summary>
internal sealed class LocalStatsService
{
    private const uint InvalidActorId = 0xE0000000;
    private const double MinimumHistoricalEncounterSeconds = 30d;
    private static readonly TimeSpan OwnerCacheTtl = TimeSpan.FromMilliseconds(2500);
    private static readonly TimeSpan OwnerCacheWarmupInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PlayerDotStatusPollInterval = TimeSpan.FromMilliseconds(100);
    // DoT 只在对应状态存续且上一次结算满 3 秒后才进入下一次归因。
    private static readonly TimeSpan PlayerDotTickInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PlayerDotTickJitterAllowance = TimeSpan.FromMilliseconds(250);
    // 2.5 秒对 DoT 来说太短：状态确认和首跳补算容易在种子过期后才命中，
    // 结果就会退回到 1000 这类兜底值。这里放宽到 8 秒，覆盖首跳前后的观察窗口。
    private static readonly TimeSpan PlayerDotRecentActionTtl = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan PlayerDotStatusGracePeriod = TimeSpan.FromSeconds(1.0);
    private static readonly TimeSpan PlayerDotDebugLogThrottle = TimeSpan.FromSeconds(1.0);
    private const double ObservedPlayerDotCriticalHitMultiplier = 1.6d;
    private const double ObservedPlayerDotDirectHitMultiplier = 1.25d;
    private const double SimulatedDotCriticalMultiplier = ObservedPlayerDotCriticalHitMultiplier;
    private static readonly JsonSerializerOptions HistoryJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false,
    };

    private readonly object gate = new();
    private readonly List<HistoricalCombatData> historicalRecords = new();
    private readonly List<CombatTimelineEntry> combatTimelineEntries = new();
    private readonly Dictionary<uint, OwnerCacheEntry> ownerCache = new();
    private readonly Dictionary<uint, TrackedActor> observedFriendlyActorCache = new();
    private readonly Dictionary<uint, uint> partyMemberHpCache = new();
    private readonly List<RecentHostilePlayerAction> recentHostilePlayerActions = new();
    private readonly Dictionary<PlayerDotKey, ActivePlayerDotState> activePlayerDots = new();
    private readonly Dictionary<uint, bool> dotStatusClassificationCache = new();
    private readonly PluginConfiguration config;

    private EncounterSession currentEncounter = new();
    private DateTime lastOwnerWarmupUtc;
    private DateTime lastPlayerDotStatusPollUtc;
    private DateTime lastPlayerDotDebugLogUtc;
    private DateTime partyOutOfCombatSinceUtc;
    private DateTime enteredCombatWithoutDataSinceUtc;
    private DateTime lastNoDataCombatDiagnosticUtc;
    private int encounterFinalizedVersion;
    private int selectedHistoricalRecordIndex = -1;
    private DateTime? historicalPreviewExpiresAtUtc;
    private bool latestInCombatHint;
    private bool suppressStaleDisplayUntilNextCombatStart;

    public LocalStatsService(PluginConfiguration config)
    {
        this.config = config;
    }

    public CombatDataWrapper? CurrentCombatData { get; private set; }

    public CombatDataWrapper? DisplayCombatData { get; private set; }

    public IReadOnlyList<HistoricalCombatData> HistoricalRecords
    {
        get
        {
            lock (gate)
                return historicalRecords.ToArray();
        }
    }

    public IReadOnlyList<CombatTimelineEntry> CombatTimelineEntries
    {
        get
        {
            lock (gate)
                return combatTimelineEntries.ToArray();
        }
    }

    public int SelectedHistoricalRecordIndex
    {
        get
        {
            lock (gate)
                return selectedHistoricalRecordIndex;
        }
    }

    public int EncounterFinalizedVersion
    {
        get
        {
            lock (gate)
                return encounterFinalizedVersion;
        }
    }

    public string DataSourceText => "本地事件采集 / ACTX 统计口径";

    public string StatusText { get; private set; } = "等待战斗数据...";

    public string HistoryTransferStatusText { get; private set; } = string.Empty;

    public string HistoryTransferFilePath
    {
        get
        {
            var configDirectory = DalamudApi.PluginInterface.GetPluginConfigDirectory();
            return Path.Combine(configDirectory, "history-records.json");
        }
    }

    public void ClearHistory()
    {
        lock (gate)
        {
            historicalRecords.Clear();
            combatTimelineEntries.Clear();
            ownerCache.Clear();
            observedFriendlyActorCache.Clear();
            partyMemberHpCache.Clear();
            recentHostilePlayerActions.Clear();
            activePlayerDots.Clear();
            dotStatusClassificationCache.Clear();
            CurrentCombatData = null;
            DisplayCombatData = null;
            ClearHistoricalPreviewLocked();
            currentEncounter = new EncounterSession();
            partyOutOfCombatSinceUtc = default;
            enteredCombatWithoutDataSinceUtc = default;
            lastNoDataCombatDiagnosticUtc = default;
            lastPlayerDotStatusPollUtc = default;
            HistoryTransferStatusText = string.Empty;
            suppressStaleDisplayUntilNextCombatStart = false;
            StatusText = "等待战斗数据...";
            LogHelper.PrintWithModule("统计", "历史", "已清空历史记录并重置当前战斗状态。");
        }
    }

    public void LoadTestData()
    {
        lock (gate)
        {
            var syntheticAnchorUtc = DateTime.UtcNow.Date.AddHours(12);
            var firstSnapshot = BuildRaidEightPlayerTestCombatData();
            var secondSnapshot = BuildTrialTestCombatData();
            var thirdSnapshot = BuildTrainingTestCombatData();
            UpsertHistoricalRecord(CreateSyntheticHistoricalRecord(firstSnapshot, syntheticAnchorUtc.AddMinutes(-36)));
            UpsertHistoricalRecord(CreateSyntheticHistoricalRecord(secondSnapshot, syntheticAnchorUtc.AddMinutes(-22)));
            UpsertHistoricalRecord(CreateSyntheticHistoricalRecord(thirdSnapshot, syntheticAnchorUtc.AddMinutes(-8)));
            SortHistoricalRecords();

            ownerCache.Clear();
            observedFriendlyActorCache.Clear();
            partyMemberHpCache.Clear();
            recentHostilePlayerActions.Clear();
            activePlayerDots.Clear();
            dotStatusClassificationCache.Clear();
            combatTimelineEntries.Clear();
            CurrentCombatData = firstSnapshot;
            DisplayCombatData = firstSnapshot;
            ClearHistoricalPreviewLocked();
            currentEncounter = new EncounterSession
            {
                ZoneName = CurrentCombatData.Msg?.Encounter?.CurrentZoneName ?? "零式测试场",
            };
            partyOutOfCombatSinceUtc = default;
            enteredCombatWithoutDataSinceUtc = default;
            lastNoDataCombatDiagnosticUtc = default;
            HistoryTransferStatusText = $"已导入测试数据，共 {historicalRecords.Count} 条历史记录。";
            StatusText = "已导入测试数据，可用于预览 DPS 统计面板。";
            LogHelper.PrintWithModule("统计", "测试数据", $"已导入测试数据，共 {historicalRecords.Count} 条历史记录。");
        }
    }

    public bool LoadHistoricalRecord(int index)
        => PreviewHistoricalRecord(index);

    public bool PreviewHistoricalRecord(int index)
    {
        var nowUtc = DateTime.UtcNow;
        lock (gate)
        {
            return PreviewHistoricalRecordLocked(index, nowUtc);
        }
    }

    private bool PreviewHistoricalRecordLocked(int index, DateTime nowUtc)
    {
        if ((uint)index >= (uint)historicalRecords.Count)
        {
            LogHelper.Warning("统计", $"历史预览请求被拒绝：索引 {index} 超出范围，当前记录数为 {historicalRecords.Count}。");
            return false;
        }

        selectedHistoricalRecordIndex = index;
        historicalPreviewExpiresAtUtc = ShouldHistoricalPreviewCountdownLocked()
            ? nowUtc.AddSeconds(Math.Clamp(config.HistoryPreviewSeconds, 1, 30))
            : null;
        RefreshDisplayCombatDataLocked(nowUtc, false);
        UpdateStatusText(nowUtc);
        var selected = historicalRecords[index];
        LogHelper.Debug(
            "统计",
            $"开始预览历史记录 #{index}：区域={selected.ZoneName}，时长={selected.Duration}，倒计时={(historicalPreviewExpiresAtUtc.HasValue ? config.HistoryPreviewSeconds : 0)} 秒。");
        return true;
    }

    private void ClearHistoricalPreviewLocked()
    {
        selectedHistoricalRecordIndex = -1;
        historicalPreviewExpiresAtUtc = null;
    }

    private bool HasSelectedHistoricalPreviewLocked()
        => (uint)selectedHistoricalRecordIndex < (uint)historicalRecords.Count;

    private bool ShouldHistoricalPreviewCountdownLocked()
        => latestInCombatHint || currentEncounter.Started;

    private void EnsureHistoricalPreviewCountdownStartedLocked(DateTime nowUtc)
    {
        if (!HasSelectedHistoricalPreviewLocked())
            return;

        if (historicalPreviewExpiresAtUtc.HasValue)
            return;

        if (!ShouldHistoricalPreviewCountdownLocked())
            return;

        historicalPreviewExpiresAtUtc = nowUtc.AddSeconds(Math.Clamp(config.HistoryPreviewSeconds, 1, 30));
    }

    private void RefreshDisplayCombatDataLocked(DateTime nowUtc, bool suppressStaleDisplay)
    {
        if (HasSelectedHistoricalPreviewLocked())
        {
            if (!historicalPreviewExpiresAtUtc.HasValue || nowUtc < historicalPreviewExpiresAtUtc.Value)
            {
                DisplayCombatData = historicalRecords[selectedHistoricalRecordIndex].Snapshot;
                return;
            }

            ClearHistoricalPreviewLocked();
        }

        if (suppressStaleDisplay)
        {
            DisplayCombatData = null;
            return;
        }

        DisplayCombatData = CurrentCombatData;
    }

    private int GetHistoricalPreviewRemainingSeconds(DateTime nowUtc)
    {
        if (!historicalPreviewExpiresAtUtc.HasValue)
            return 0;

        return Math.Max(0, (int)Math.Ceiling((historicalPreviewExpiresAtUtc.Value - nowUtc).TotalSeconds));
    }

    public void ExportHistoricalRecords()
    {
        lock (gate)
        {
            try
            {
                if (historicalRecords.Count == 0)
                {
                    HistoryTransferStatusText = "没有可导出的历史记录。";
                    LogHelper.PrintWithModule("统计", "导出", "没有可导出的历史记录。");
                    return;
                }

                var exportPath = HistoryTransferFilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(exportPath)!);

                var payload = new HistoricalRecordsExportPayload
                {
                    Version = 1,
                    ExportedAtUtc = DateTime.UtcNow,
                    Records = historicalRecords.ToList(),
                };

                var json = JsonSerializer.Serialize(payload, HistoryJsonOptions);
                File.WriteAllText(exportPath, json);
                TrySelectLatestHistoricalRecord();
                HistoryTransferStatusText = $"已导出 {historicalRecords.Count} 条历史记录到 {exportPath}";
                UpdateStatusText(DateTime.UtcNow);
                LogHelper.PrintWithModule("统计", "导出", $"已导出 {historicalRecords.Count} 条历史记录到 {exportPath}");
            }
            catch (Exception ex)
            {
                LogHelper.Error("统计", ex, "导出历史战斗记录失败。");
                HistoryTransferStatusText = $"导出失败: {ex.Message}";
                LogHelper.PrintErrorWithModule("统计", "导出", $"导出失败: {ex.Message}");
            }
        }
    }

    public void ImportHistoricalRecords()
    {
        lock (gate)
        {
            try
            {
                var importPath = HistoryTransferFilePath;
                if (!File.Exists(importPath))
                {
                    HistoryTransferStatusText = $"导入失败: 未找到文件 {importPath}";
                    LogHelper.PrintErrorWithModule("统计", "导入", $"导入失败: 未找到文件 {importPath}");
                    return;
                }

                var json = File.ReadAllText(importPath);
                var importedRecords = DeserializeHistoricalRecords(json);
                if (importedRecords.Count == 0)
                {
                    HistoryTransferStatusText = "导入完成，但文件中没有可用的历史记录。";
                    LogHelper.PrintWithModule("统计", "导入", "导入完成，但文件中没有可用的历史记录。");
                    return;
                }

                var importedCount = 0;
                foreach (var record in importedRecords)
                {
                    if (!IsValidHistoricalRecord(record))
                        continue;

                    UpsertHistoricalRecord(record);
                    importedCount++;
                }

                SortHistoricalRecords();
                if (importedCount > 0)
                    TrySelectLatestHistoricalRecord();
                else
                    ClearHistoricalPreviewLocked();

                HistoryTransferStatusText = importedCount > 0
                    ? $"已导入 {importedCount} 条历史记录，已自动打开最新记录。"
                    : "导入完成，但没有可写入的历史记录。";
                RefreshDisplayCombatDataLocked(DateTime.UtcNow, false);
                UpdateStatusText(DateTime.UtcNow);
                if (importedCount > 0)
                    LogHelper.PrintWithModule("统计", "导入", $"已导入 {importedCount} 条历史记录，已自动打开最新记录。");
                else
                    LogHelper.PrintWithModule("统计", "导入", "导入完成，但没有可写入的历史记录。");
            }
            catch (Exception ex)
            {
                LogHelper.Error("统计", ex, "导入历史战斗记录失败。");
                HistoryTransferStatusText = $"导入失败: {ex.Message}";
                LogHelper.PrintErrorWithModule("统计", "导入", $"导入失败: {ex.Message}");
            }
        }
    }

    public void WarmOwnerCacheFromObjectTable()
    {
        var nowUtc = DateTime.UtcNow;
        if (nowUtc - lastOwnerWarmupUtc < OwnerCacheWarmupInterval)
            return;

        lastOwnerWarmupUtc = nowUtc;

        var entries = new List<(uint EntityId, uint OwnerId)>();
        foreach (var obj in DalamudApi.ObjectTable)
        {
            if (obj == null || obj.ObjectKind != ObjectKind.BattleNpc)
                continue;

            if (obj.EntityId is 0 or <= 0x40000000)
                continue;

            if (!ShouldResolveOwnerForObject(obj))
                continue;

            if (obj.OwnerId is 0 or InvalidActorId)
                continue;

            entries.Add((obj.EntityId, obj.OwnerId));
        }

        if (entries.Count == 0)
            return;

        lock (gate)
        {
            foreach (var (entityId, ownerId) in entries)
                ownerCache[entityId] = new OwnerCacheEntry(ownerId, nowUtc);
        }
    }

    public void RecordEncounterActivity(string zoneName, DateTime timeUtc)
    {
        lock (gate)
        {
            var wasStarted = currentEncounter.Started;
            currentEncounter.ZoneName = NormalizeZoneName(zoneName);
            currentEncounter.MarkActivity(timeUtc);
            AppendEncounterStartIfNeededLocked(wasStarted, timeUtc);
        }
    }

    public void RecordDamage(
        uint sourceId,
        uint targetId,
        uint actionId,
        string actionName,
        long amount,
        bool critical,
        bool directHit,
        DateTime timeUtc,
        string zoneName)
    {
        if (amount <= 0)
            return;

        lock (gate)
        {
            var wasStarted = currentEncounter.Started;
            currentEncounter.ZoneName = NormalizeZoneName(zoneName);

            var loggedSourceName = ResolveCombatTimelineSourceName(sourceId, timeUtc);
            var loggedTargetName = ResolveCombatTimelineTargetName(targetId, timeUtc);
            var shouldAppendTimelineEntry = false;
            var sourceIsFriendly = false;
            var targetIsFriendly = false;

            if (TryResolveCombatantSource(sourceId, timeUtc, out var source, out var resolvedSourceIsFriendly))
            {
                currentEncounter.RecordOutgoingDamage(source, actionName, amount, critical, directHit, timeUtc);
                loggedSourceName = source.Name;
                shouldAppendTimelineEntry = true;
                sourceIsFriendly = resolvedSourceIsFriendly;
            }

            if (TryGetTrackedActor(targetId, out var target))
            {
                currentEncounter.RecordIncomingDamage(target, amount, timeUtc);
                loggedTargetName = target.Name;
                shouldAppendTimelineEntry = true;
                targetIsFriendly = true;
            }

            AppendEncounterStartIfNeededLocked(wasStarted, timeUtc);
            if (shouldAppendTimelineEntry)
            {
                AppendCombatTimelineEntryLocked(
                    timeUtc,
                    CombatTimelineEntryKind.Damage,
                    $"{loggedSourceName} 使用{FormatActionNameWithId(actionName, actionId)} 攻击 {loggedTargetName}，造成 {CreateDamageString(amount, useSuffix: true, useDecimals: true)} 伤害{FormatCriticalSuffix(critical)}。",
                    loggedSourceName,
                    loggedTargetName,
                    sourceIsFriendly,
                    targetIsFriendly,
                    FormatActionNameWithId(actionName, actionId));
            }
        }
    }

    public void RecordHeal(
        uint sourceId,
        uint targetId,
        uint actionId,
        string actionName,
        long amount,
        bool critical,
        DateTime timeUtc,
        string zoneName)
    {
        if (amount <= 0)
            return;

        lock (gate)
        {
            var wasStarted = currentEncounter.Started;
            currentEncounter.ZoneName = NormalizeZoneName(zoneName);

            var loggedSourceName = ResolveCombatTimelineSourceName(sourceId, timeUtc);
            var loggedTargetName = ResolveCombatTimelineTargetName(targetId, timeUtc);
            var shouldAppendTimelineEntry = false;
            var sourceIsFriendly = false;
            var targetIsFriendly = false;

            if (TryResolveCombatantSource(sourceId, timeUtc, out var source, out var resolvedSourceIsFriendly))
            {
                currentEncounter.RecordOutgoingHeal(source, amount, critical, timeUtc);
                loggedSourceName = source.Name;
                shouldAppendTimelineEntry = true;
                sourceIsFriendly = resolvedSourceIsFriendly;
            }

            if (TryGetTrackedActor(targetId, out var target))
            {
                currentEncounter.RecordIncomingHeal(target, amount, timeUtc);
                loggedTargetName = target.Name;
                shouldAppendTimelineEntry = true;
                targetIsFriendly = true;
            }

            AppendEncounterStartIfNeededLocked(wasStarted, timeUtc);
            if (shouldAppendTimelineEntry)
            {
                AppendCombatTimelineEntryLocked(
                    timeUtc,
                    CombatTimelineEntryKind.Heal,
                    $"{loggedSourceName} 使用{FormatActionNameWithId(actionName, actionId)} 治疗 {loggedTargetName}，恢复 {CreateDamageString(amount, useSuffix: true, useDecimals: true)} 生命{FormatCriticalSuffix(critical)}。",
                    loggedSourceName,
                    loggedTargetName,
                    sourceIsFriendly,
                    targetIsFriendly,
                    FormatActionNameWithId(actionName, actionId));
            }
        }
    }

    public void RecordFailure(
        uint sourceId,
        uint targetId,
        uint actionId,
        string actionName,
        bool isMiss,
        DateTime timeUtc,
        string zoneName)
    {
        lock (gate)
        {
            var wasStarted = currentEncounter.Started;
            currentEncounter.ZoneName = NormalizeZoneName(zoneName);

            var loggedSourceName = ResolveCombatTimelineSourceName(sourceId, timeUtc);
            var loggedTargetName = ResolveCombatTimelineTargetName(targetId, timeUtc);
            var shouldAppendTimelineEntry = false;
            var sourceIsFriendly = false;
            var targetIsFriendly = false;

            if (TryResolveCombatantSource(sourceId, timeUtc, out var source, out var resolvedSourceIsFriendly))
            {
                currentEncounter.RecordFailedSwing(source, isMiss, timeUtc);
                loggedSourceName = source.Name;
                shouldAppendTimelineEntry = true;
                sourceIsFriendly = resolvedSourceIsFriendly;
            }

            if (TryGetTrackedActor(targetId, out var target))
            {
                loggedTargetName = target.Name;
                shouldAppendTimelineEntry = true;
                targetIsFriendly = true;
            }

            AppendEncounterStartIfNeededLocked(wasStarted, timeUtc);
            if (shouldAppendTimelineEntry)
            {
                AppendCombatTimelineEntryLocked(
                    timeUtc,
                    CombatTimelineEntryKind.Failure,
                    isMiss
                        ? $"{loggedSourceName} 对 {loggedTargetName} 使用{FormatActionNameWithId(actionName, actionId)}，但未命中。"
                        : $"{loggedSourceName} 对 {loggedTargetName} 使用{FormatActionNameWithId(actionName, actionId)}，但效果被抵抗或目标免疫。",
                    loggedSourceName,
                    loggedTargetName,
                    sourceIsFriendly,
                    targetIsFriendly,
                    FormatActionNameWithId(actionName, actionId));
            }
        }
    }

    public void RecordDeath(uint targetId, DateTime timeUtc, string zoneName)
    {
        lock (gate)
        {
            var wasStarted = currentEncounter.Started;
            currentEncounter.ZoneName = NormalizeZoneName(zoneName);
            if (TryGetTrackedActor(targetId, out var target))
            {
                currentEncounter.RecordDeath(target, timeUtc);
                AppendEncounterStartIfNeededLocked(wasStarted, timeUtc);
                AppendCombatTimelineEntryLocked(
                    timeUtc,
                    CombatTimelineEntryKind.Death,
                    $"{target.Name} 战斗不能。",
                    target.Name,
                    target.Name,
                    true,
                    true);
            }
        }
    }

    public void ClearCombatTimeline()
    {
        lock (gate)
            combatTimelineEntries.Clear();
    }

    public void ApplyCombatTimelineRetentionLimit()
    {
        lock (gate)
            TrimCombatTimelineEntriesLocked();
    }

    public bool ObservePotentialPlayerDotApplication(
        uint sourceId,
        uint targetId,
        uint actionId,
        string actionName,
        DateTime timeUtc)
    {
        if (!PlayerDotCatalog.IsKnownPlayerDotAction(actionId))
            return false;

        lock (gate)
        {
            try
            {
                if (!TryResolveTrackedSource(sourceId, timeUtc, out var source) || source.Kind != TrackedActorKind.Player)
                    return false;

                TrimRecentHostilePlayerActionsLocked(timeUtc);
                recentHostilePlayerActions.Add(new RecentHostilePlayerAction(
                    source,
                    targetId,
                    actionId,
                    NormalizeActionName(actionName),
                    timeUtc));

                if (!TryGetHostileBattleTarget(targetId, out var hostileTarget))
                    return false;

                return CapturePlayerDotStatusesForHostileTargetLocked(
                    hostileTarget,
                    timeUtc,
                    preferredSourceActorId: source.ActorId,
                    preferredActionId: actionId,
                    preferredActionName: NormalizeActionName(actionName));
            }
            catch (Exception ex)
            {
                LogHelper.Error(
                    "统计",
                    ex,
                    $"记录玩家 DOT 挂载候选失败：sourceId=0x{sourceId:X8}，targetId=0x{targetId:X8}，actionId=0x{actionId:X8}。");
                return false;
            }
        }
    }

    public void ObservePotentialPlayerHostileActionSample(
        uint sourceId,
        uint targetId,
        uint actionId,
        string actionName,
        long amount,
        bool critical,
        bool directHit,
        DateTime timeUtc)
    {
        if (amount <= 0)
            return;

        lock (gate)
        {
            try
            {
                if (!TryResolveTrackedSource(sourceId, timeUtc, out var source) || source.Kind != TrackedActorKind.Player)
                    return;

                TrimRecentHostilePlayerActionsLocked(timeUtc);

                var matchedAction = recentHostilePlayerActions
                    .Where(action =>
                        AreEquivalentActorIds(action.Source.ActorId, source.ActorId)
                        && AreEquivalentActorIds(action.TargetActorId, targetId)
                        && action.ActionId == actionId
                        && string.Equals(action.ActionName, NormalizeActionName(actionName), StringComparison.Ordinal)
                        && timeUtc - action.ObservedAtUtc <= PlayerDotRecentActionTtl)
                    .OrderByDescending(action => action.ObservedAtUtc)
                    .FirstOrDefault();

                if (matchedAction != null)
                {
                    matchedAction.ObservedDamageAmount = amount;
                    matchedAction.ObservedCritical = critical;
                    matchedAction.ObservedDirectHit = directHit;
                    RefreshActivePlayerDotEstimatedDamageLocked(source.ActorId, targetId, actionId, NormalizeActionName(actionName), amount, critical, directHit, timeUtc);
                    return;
                }

                recentHostilePlayerActions.Add(new RecentHostilePlayerAction(
                    source,
                    targetId,
                    actionId,
                    NormalizeActionName(actionName),
                    timeUtc)
                {
                    ObservedDamageAmount = amount,
                    ObservedCritical = critical,
                    ObservedDirectHit = directHit,
                });
                RefreshActivePlayerDotEstimatedDamageLocked(source.ActorId, targetId, actionId, NormalizeActionName(actionName), amount, critical, directHit, timeUtc);
            }
            catch (Exception ex)
            {
                LogHelper.Debug(
                    "统计",
                    ex,
                    $"记录玩家 DOT 伤害种子失败：sourceId=0x{sourceId:X8}，targetId=0x{targetId:X8}，actionId=0x{actionId:X8}，amount={amount}。");
            }
        }
    }

    public void ObservePotentialPlayerDotDamageSeed(
        uint sourceId,
        uint targetId,
        uint actionId,
        string actionName,
        long amount,
        bool critical,
        bool directHit,
        DateTime timeUtc)
        => ObservePotentialPlayerHostileActionSample(sourceId, targetId, actionId, actionName, amount, critical, directHit, timeUtc);

    public bool TryRecordPlayerDotDamage(
        uint sourceId,
        uint targetId,
        uint actionId,
        string actionName,
        long amount,
        bool critical,
        DateTime timeUtc,
        string zoneName)
    {
        if (amount <= 0)
            return false;

        lock (gate)
        {
            try
            {
                currentEncounter.ZoneName = NormalizeZoneName(zoneName);
                TrimRecentHostilePlayerActionsLocked(timeUtc);
                TrimInactivePlayerDotsLocked(timeUtc);

                if (!TryResolvePlayerDotAttributionLocked(sourceId, targetId, actionId, actionName, timeUtc, out var dotState))
                    return false;

                var source = dotState.Source;
                var loggedTargetName = ResolveCombatTimelineTargetName(targetId, timeUtc);
                var encounterActionName = NormalizeActionName(dotState.ActionName);
                var dotActionName = FormatActionNameWithId(encounterActionName, dotState.ActionId);
                var wasStarted = currentEncounter.Started;
                var resolvedCritical = ResolvePlayerDotCritical(source.ActorId, dotState, critical);

                currentEncounter.RecordOutgoingDamage(source, encounterActionName, amount, resolvedCritical, false, timeUtc, isDotDamage: true);
                AppendEncounterStartIfNeededLocked(wasStarted, timeUtc);
                AppendCombatTimelineEntryLocked(
                    timeUtc,
                    CombatTimelineEntryKind.Damage,
                    $"{source.Name} 使用{dotActionName} 攻击 {loggedTargetName}，造成 {CreateDamageString(amount, useSuffix: true, useDecimals: true)} 伤害{FormatCriticalSuffix(resolvedCritical)}。",
                    source.Name,
                    loggedTargetName,
                    actorIsFriendly: true,
                    targetIsFriendly: false,
                    actionText: dotActionName);

                dotState.LastAttributedTickUtc = timeUtc;
                dotState.TickCount++;
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Error(
                    "统计",
                    ex,
                    $"回补玩家 DOT 伤害失败：sourceId=0x{sourceId:X8}，targetId=0x{targetId:X8}，actionId=0x{actionId:X8}，amount={amount}。");
                return false;
            }
        }
    }

    public void Update(string zoneName, bool inCombat)
    {
        var nowUtc = DateTime.UtcNow;

        lock (gate)
        {
            latestInCombatHint = inCombat;
            currentEncounter.ZoneName = NormalizeZoneName(zoneName);
            PollPartyMemberDeaths(nowUtc, currentEncounter.ZoneName, inCombat);
            PollActivePlayerDots(nowUtc, inCombat);
            var allPartyMembersOutOfCombat = AreAllPartyMembersOutOfCombat(inCombat);
            UpdatePartyOutOfCombatTimer(nowUtc, allPartyMembersOutOfCombat);
            UpdateNoDataCombatDiagnostics(nowUtc, inCombat);

            if (ShouldFinalizeEncounter(nowUtc, allPartyMembersOutOfCombat))
            {
                FinalizeEncounter(nowUtc);
            }
            else if (currentEncounter.Started)
            {
                CurrentCombatData = ActxSnapshotFormatter.Build(currentEncounter, isActive: true);
                suppressStaleDisplayUntilNextCombatStart = false;
            }

            EnsureHistoricalPreviewCountdownStartedLocked(nowUtc);
            var shouldSuppressStaleDisplay = suppressStaleDisplayUntilNextCombatStart
                && inCombat
                && !currentEncounter.Started
                && !HasSelectedHistoricalPreviewLocked();

            RefreshDisplayCombatDataLocked(nowUtc, shouldSuppressStaleDisplay);
            UpdateStatusText(nowUtc);
        }
    }

    private void PollActivePlayerDots(DateTime nowUtc, bool inCombat)
    {
        try
        {
            TrimRecentHostilePlayerActionsLocked(nowUtc);

            if (!inCombat && !currentEncounter.Started)
            {
                activePlayerDots.Clear();
                return;
            }

            if (nowUtc - lastPlayerDotStatusPollUtc < PlayerDotStatusPollInterval)
                return;

            lastPlayerDotStatusPollUtc = nowUtc;
            var targetActorIds = activePlayerDots.Keys
                .Select(static key => key.TargetActorId)
                .Concat(recentHostilePlayerActions.Select(static action => action.TargetActorId))
                .Where(static actorId => actorId is not 0 and not InvalidActorId)
                .Distinct()
                .ToList();
            if (targetActorIds.Count == 0)
            {
                TrimInactivePlayerDotsLocked(nowUtc);
                return;
            }

            foreach (var targetActorId in targetActorIds)
            {
                try
                {
                    if (!TryGetHostileBattleTarget(targetActorId, out var hostileBattleNpc))
                    {
                        RemoveActivePlayerDotsForTargetLocked(targetActorId);
                        continue;
                    }

                    if (!hostileBattleNpc.IsTargetable)
                    {
                        RemoveActivePlayerDotsForTargetLocked(targetActorId);
                        continue;
                    }

                    var preferredRecentActions = recentHostilePlayerActions
                        .Where(action => AreEquivalentActorIds(action.TargetActorId, targetActorId))
                        .OrderByDescending(action => action.ObservedAtUtc)
                        .GroupBy(action => action.Source.ActorId)
                        .Select(static group => group.First())
                        .ToList();
                    if (preferredRecentActions.Count == 0)
                    {
                        CapturePlayerDotStatusesForHostileTargetLocked(hostileBattleNpc, nowUtc);
                    }
                    else
                    {
                        foreach (var recentAction in preferredRecentActions)
                        {
                            CapturePlayerDotStatusesForHostileTargetLocked(
                                hostileBattleNpc,
                                nowUtc,
                                preferredSourceActorId: recentAction.Source.ActorId,
                                preferredActionId: recentAction.ActionId,
                                preferredActionName: recentAction.ActionName);
                        }
                    }
                }
                catch (Exception ex)
                {
                    RemoveActivePlayerDotsForTargetLocked(targetActorId);
                    LogHelper.Error(
                        "统计",
                        ex,
                        $"轮询玩家 DOT 目标失败：targetId=0x{targetActorId:X8}，异常={ex.GetType().Name}: {ex.Message}");
                }
            }

            try
            {
                SimulateActivePlayerDotTicksLocked(nowUtc);
            }
            catch (Exception ex)
            {
                LogHelper.Error(
                    "统计",
                    ex,
                    $"模拟玩家 DOT tick 失败：异常={ex.GetType().Name}: {ex.Message}");
            }

            try
            {
                TrimInactivePlayerDotsLocked(nowUtc);
            }
            catch (Exception ex)
            {
                LogHelper.Error(
                    "统计",
                    ex,
                    $"清理玩家 DOT 活跃状态失败：异常={ex.GetType().Name}: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(
                "统计",
                ex,
                $"轮询玩家 DOT 状态失败，已自动跳过本轮刷新。异常={ex.GetType().Name}: {ex.Message}");
        }
    }

    private bool CapturePlayerDotStatusesForHostileTargetLocked(
        IBattleNpc hostileTarget,
        DateTime nowUtc,
        uint? preferredSourceActorId = null,
        uint? preferredActionId = null,
        string? preferredActionName = null)
    {
        var targetActorId = ResolveBattleCharaActorId(hostileTarget);
        if (targetActorId is 0 or InvalidActorId)
            return false;

        var observedNewOrRefreshedState = false;
        var normalizedPreferredActionName = string.IsNullOrWhiteSpace(preferredActionName)
            ? string.Empty
            : NormalizeActionName(preferredActionName);

        try
        {
            foreach (var status in EnumerateStatusEntries(hostileTarget))
            {
                try
                {
                    if (!TryCreateActivePlayerDotStateLocked(
                            status,
                            targetActorId,
                            nowUtc,
                            preferredSourceActorId,
                            preferredActionId,
                            preferredActionName,
                            out var key,
                            out var state))
                    {
                        continue;
                    }

                    if (activePlayerDots.TryGetValue(key, out var existing))
                    {
                        var shouldRefreshTickSchedule = state.RemainingTimeSeconds > existing.RemainingTimeSeconds + 0.5f;
                        var matchesPreferredApplication = (!preferredSourceActorId.HasValue || AreEquivalentActorIds(key.SourceActorId, preferredSourceActorId.Value))
                            && (string.IsNullOrWhiteSpace(normalizedPreferredActionName)
                                || string.Equals(state.ActionName, normalizedPreferredActionName, StringComparison.Ordinal)
                                || string.Equals(state.StatusName, normalizedPreferredActionName, StringComparison.Ordinal));
                        existing.ActionName = ResolvePreferredDotActionName(existing.ActionName, state.ActionName, state.StatusName);
                        existing.StatusName = string.IsNullOrWhiteSpace(state.StatusName) ? existing.StatusName : state.StatusName;
                        existing.LastSeenUtc = nowUtc;
                        existing.RemainingTimeSeconds = state.RemainingTimeSeconds;
                        if (existing.SkillEntry == null && state.SkillEntry != null)
                            existing.SkillEntry = state.SkillEntry;
                        if (state.EstimatedTickDamage > 0
                            && (state.EstimatedTickDamageFromObservedSeed
                                || !existing.EstimatedTickDamageFromObservedSeed
                                || existing.EstimatedTickDamage <= 0))
                        {
                            existing.EstimatedTickDamage = state.EstimatedTickDamage;
                            existing.EstimatedTickDamageFromObservedSeed = state.EstimatedTickDamageFromObservedSeed;
                        }

                        if (shouldRefreshTickSchedule)
                        {
                            existing.LastAttributedTickUtc = nowUtc;
                            existing.TickCount = 0;
                            existing.NextTickRemainingTimeSeconds = state.NextTickRemainingTimeSeconds;
                            if (matchesPreferredApplication)
                                observedNewOrRefreshedState = true;
                        }
                        continue;
                    }

                    activePlayerDots[key] = state;
                    if ((!preferredSourceActorId.HasValue || AreEquivalentActorIds(key.SourceActorId, preferredSourceActorId.Value))
                        && (string.IsNullOrWhiteSpace(normalizedPreferredActionName)
                            || string.Equals(state.ActionName, normalizedPreferredActionName, StringComparison.Ordinal)
                            || string.Equals(state.StatusName, normalizedPreferredActionName, StringComparison.Ordinal)))
                    {
                        observedNewOrRefreshedState = true;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Debug(
                        "统计",
                        ex,
                        $"读取 DOT 状态条目失败：targetId=0x{targetActorId:X8}。");
                }
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(
                "统计",
                ex,
                $"读取敌方目标状态列表失败：targetId=0x{targetActorId:X8}。");
        }

        var hasMatchingActiveState = false;
        try
        {
                hasMatchingActiveState = preferredActionId.HasValue
                && activePlayerDots.Values.Any(state =>
                    AreEquivalentActorIds(state.Key.TargetActorId, targetActorId)
                    && (!preferredSourceActorId.HasValue || AreEquivalentActorIds(state.Key.SourceActorId, preferredSourceActorId.Value))
                    && (state.ActionId == preferredActionId.Value
                        || state.Key.StatusId == preferredActionId.Value
                        || (!string.IsNullOrWhiteSpace(normalizedPreferredActionName)
                            && (string.Equals(state.ActionName, normalizedPreferredActionName, StringComparison.Ordinal)
                                || string.Equals(state.StatusName, normalizedPreferredActionName, StringComparison.Ordinal)))));
        }
        catch (Exception ex)
        {
            LogHelper.Debug(
                "统计",
                ex,
                $"检查 DOT 活跃状态匹配失败：targetId=0x{targetActorId:X8}。");
        }

        if (!observedNewOrRefreshedState
            && !hasMatchingActiveState
            && preferredActionId.HasValue
            && PlayerDotCatalog.IsKnownPlayerDotAction(preferredActionId.Value)
            && LogHelper.EnableDebugLog
            && nowUtc - lastPlayerDotDebugLogUtc >= PlayerDotDebugLogThrottle)
        {
            try
            {
                lastPlayerDotDebugLogUtc = nowUtc;
                var preferredSourceText = preferredSourceActorId.HasValue
                    ? ResolveCombatTimelineSourceName(preferredSourceActorId.Value, nowUtc)
                    : "未知来源";
                var preferredActionText = FormatActionNameWithId(preferredActionName, preferredActionId.Value);
                var targetName = ResolveCombatTimelineTargetName(targetActorId, nowUtc);
                var statusSummary = BuildPlayerDotStatusSummary(hostileTarget);
                LogHelper.DebugRecent(
                    "统计",
                    $"DOT 状态未确认：source={preferredSourceText}，target={targetName}，action={preferredActionText}，status={statusSummary}。");
            }
            catch (Exception ex)
            {
                LogHelper.Debug(
                    "统计",
                    ex,
                    $"输出 DOT 状态调试摘要失败：targetId=0x{targetActorId:X8}，actionId=0x{preferredActionId.Value:X8}。");
            }
        }

        return observedNewOrRefreshedState;
    }

    private bool TryCreateActivePlayerDotStateLocked(
        object status,
        uint targetActorId,
        DateTime nowUtc,
        uint? preferredSourceActorId,
        uint? preferredActionId,
        string? preferredActionName,
        out PlayerDotKey key,
        out ActivePlayerDotState state)
    {
        key = default;
        state = default!;

        var statusId = GetStatusId(status);
        if (statusId == 0 || !IsPlayerDamageOverTimeStatus(status))
            return false;

        var rawSourceActorId = ResolveStatusSourceActorId(status);
        var hasRawSourceActorId = rawSourceActorId is > 0 and not InvalidActorId;
        if (!TryResolveTrackedSource(rawSourceActorId, nowUtc, out var source) || source.Kind != TrackedActorKind.Player)
        {
            // Only fall back to the event-derived source when the status itself has no usable source.
            // If the status already points to someone else, do not reassign that DoT to self or party.
            if (hasRawSourceActorId
                || !preferredSourceActorId.HasValue
                || !TryResolveTrackedSource(preferredSourceActorId.Value, nowUtc, out source)
                || source.Kind != TrackedActorKind.Player)
            {
                return false;
            }
        }

        if (source.Kind != TrackedActorKind.Player)
            return false;

        var actionId = preferredActionId.HasValue
            ? preferredActionId.Value
            : ResolveRecentPlayerDotActionIdLocked(source.ActorId, targetActorId, nowUtc);

        var actionName = preferredSourceActorId.HasValue
                         && preferredSourceActorId.Value == source.ActorId
                         && !string.IsNullOrWhiteSpace(preferredActionName)
            ? NormalizeActionName(preferredActionName)
            : ResolveRecentPlayerDotActionNameLocked(source.ActorId, targetActorId, nowUtc);

        var rawStatusName = TryGetStatusGameDataText(status, "Name");
        var statusName = string.IsNullOrWhiteSpace(rawStatusName)
            ? string.Empty
            : NormalizeActionName(rawStatusName);
        if (string.IsNullOrWhiteSpace(actionName))
            actionName = !string.IsNullOrWhiteSpace(statusName)
                ? statusName
                : "\u672A\u77E5\u6301\u7EED\u4F24\u5BB3";

        var statusPotency = TryGetStatusGameDataInt(status, "ParamModifier");
        var catalogSkill = PlayerDotCatalog.ResolveSkill(actionId, statusId);
        actionId = ResolvePreferredPlayerDotActionId(actionId, catalogSkill);
        actionName = ResolvePreferredPlayerDotActionName(actionName, statusName, catalogSkill);
        var recentAction = ResolveRecentPlayerDotObservedActionLocked(source.ActorId, targetActorId, actionName, nowUtc, catalogSkill);
        var estimatedTickDamage = ResolvePlayerDotEstimatedTickDamageLocked(source, targetActorId, actionName, statusPotency, nowUtc, catalogSkill);
        var estimatedTickDamageFromObservedSeed = recentAction?.ObservedDamageAmount > 0;

        key = new PlayerDotKey(targetActorId, source.ActorId, statusId);
        state = new ActivePlayerDotState(
            key,
            source,
            actionId,
            actionName,
            statusName,
            statusPotency,
            catalogSkill,
            estimatedTickDamage,
            estimatedTickDamageFromObservedSeed,
            nowUtc,
            nowUtc,
            Math.Max(0f, GetStatusRemainingTime(status)));
        return true;
    }

    private bool TryResolvePlayerDotAttributionLocked(
        uint sourceId,
        uint targetId,
        uint actionId,
        string actionName,
        DateTime nowUtc,
        out ActivePlayerDotState dotState)
    {
        dotState = default!;

        if (!TryGetHostileBattleTarget(targetId, out var hostileTarget))
            return false;

        var canonicalTargetActorId = ResolveBattleCharaActorId(hostileTarget);
        if (canonicalTargetActorId is 0 or InvalidActorId)
            return false;

        if (!hostileTarget.IsTargetable)
        {
            RemoveActivePlayerDotsForTargetLocked(canonicalTargetActorId);
            return false;
        }

        var resolvedSourceActorId = 0u;
        if (TryResolveTrackedSource(sourceId, nowUtc, out var resolvedSource) && resolvedSource.Kind == TrackedActorKind.Player)
            resolvedSourceActorId = resolvedSource.ActorId;

        CapturePlayerDotStatusesForHostileTargetLocked(
            hostileTarget,
            nowUtc,
            preferredSourceActorId: resolvedSourceActorId == 0 ? null : resolvedSourceActorId,
            preferredActionId: actionId,
            preferredActionName: actionName);
        TrimInactivePlayerDotsLocked(nowUtc);

        var normalizedActionName = NormalizeActionName(actionName);
        var candidates = activePlayerDots
            .Where(pair =>
                AreEquivalentActorIds(pair.Key.TargetActorId, canonicalTargetActorId)
                && (resolvedSourceActorId == 0 || AreEquivalentActorIds(pair.Key.SourceActorId, resolvedSourceActorId)))
            .Select(pair => pair.Value)
            .ToList();
        if (candidates.Count == 0 && resolvedSourceActorId != 0)
        {
            candidates = activePlayerDots
                .Where(pair => AreEquivalentActorIds(pair.Key.TargetActorId, canonicalTargetActorId))
                .Select(pair => pair.Value)
                .ToList();
        }

        if (candidates.Count == 0)
            return false;

        var matureCandidates = candidates
            .Where(candidate => nowUtc - candidate.FirstSeenUtc >= PlayerDotStatusGracePeriod)
            .ToList();
        if (matureCandidates.Count > 0)
            candidates = matureCandidates;
        else
            return false;

        candidates = candidates
            .Where(candidate => IsPlayerDotTickReady(candidate, nowUtc))
            .ToList();
        if (candidates.Count == 0)
            return false;

        if (actionId != 0)
        {
            var statusIdMatch = candidates.Where(candidate => candidate.Key.StatusId == actionId).ToList();
            if (statusIdMatch.Count == 1)
            {
                dotState = statusIdMatch[0];
                return true;
            }
        }

        if (!IsUnknownActionName(normalizedActionName))
        {
            var actionNameMatch = candidates
                .Where(candidate =>
                    string.Equals(candidate.ActionName, normalizedActionName, StringComparison.Ordinal)
                    || (!string.IsNullOrWhiteSpace(candidate.StatusName)
                        && string.Equals(candidate.StatusName, normalizedActionName, StringComparison.Ordinal)))
                .ToList();
            if (actionNameMatch.Count == 1)
            {
                dotState = actionNameMatch[0];
                return true;
            }
        }

        if (candidates.Count == 1)
        {
            dotState = candidates[0];
            return true;
        }

        return false;
    }

    private void RemoveActivePlayerDotsForTargetLocked(uint targetActorId)
    {
        if (targetActorId is 0 or InvalidActorId || activePlayerDots.Count == 0)
            return;

        var staleKeys = activePlayerDots.Keys
            .Where(key => AreEquivalentActorIds(key.TargetActorId, targetActorId))
            .ToList();
        foreach (var key in staleKeys)
            activePlayerDots.Remove(key);
    }

    private void TrimRecentHostilePlayerActionsLocked(DateTime nowUtc)
        => recentHostilePlayerActions.RemoveAll(action => nowUtc - action.ObservedAtUtc > PlayerDotRecentActionTtl);

    private void SimulateActivePlayerDotTicksLocked(DateTime nowUtc)
    {
        if (activePlayerDots.Count == 0)
            return;

        var activeDots = activePlayerDots.Values.ToList();
        foreach (var dotState in activeDots)
        {
            if (dotState.RemainingTimeSeconds <= 0f)
                continue;

            if (!TryResolveTrackedSource(dotState.Key.SourceActorId, nowUtc, out var source) || source.Kind != TrackedActorKind.Player)
                continue;

            var ticksDue = ResolvePlayerDotTicksDue(dotState);
            if (ticksDue <= 0)
                continue;

            var tickTimeUtc = dotState.LastAttributedTickUtc;
            for (var index = 0; index < ticksDue; index++)
            {
                tickTimeUtc = tickTimeUtc == default
                    ? nowUtc
                    : tickTimeUtc + PlayerDotTickInterval;

                if (!TryRecordSimulatedPlayerDotTickLocked(dotState, source, tickTimeUtc))
                    break;
            }
        }
    }

    private static int ResolvePlayerDotTicksDue(ActivePlayerDotState dotState)
    {
        var currentRemaining = dotState.RemainingTimeSeconds;
        if (currentRemaining <= 0f)
            return 0;

        var tickThreshold = dotState.NextTickRemainingTimeSeconds;
        var allowance = (float)PlayerDotTickJitterAllowance.TotalSeconds;
        var tickInterval = (float)PlayerDotTickInterval.TotalSeconds;
        var ticksDue = 0;

        while (currentRemaining <= tickThreshold + allowance)
        {
            ticksDue++;
            tickThreshold -= tickInterval;

            if (ticksDue >= 16)
                break;
        }

        return ticksDue;
    }

    private bool TryRecordSimulatedPlayerDotTickLocked(ActivePlayerDotState dotState, TrackedActor source, DateTime tickTimeUtc)
    {
        try
        {
            var amount = dotState.EstimatedTickDamage;
            if (amount <= 0)
                amount = ResolvePlayerDotEstimatedTickDamageLocked(source, dotState.Key.TargetActorId, dotState.ActionName, dotState.StatusPotency, tickTimeUtc, dotState.SkillEntry);

            if (amount <= 0)
                amount = 1;

            var loggedTargetName = ResolveCombatTimelineTargetName(dotState.Key.TargetActorId, tickTimeUtc);
            var encounterActionName = NormalizeActionName(dotState.ActionName);
            var dotActionName = FormatActionNameWithId(encounterActionName, dotState.ActionId);
            var wasStarted = currentEncounter.Started;
            var resolvedCritical = ResolvePlayerDotCritical(source.ActorId, dotState, reportedCritical: false);
            if (resolvedCritical)
                amount = Math.Max(amount + 1L, (long)Math.Round(amount * SimulatedDotCriticalMultiplier));

            currentEncounter.RecordOutgoingDamage(source, encounterActionName, amount, resolvedCritical, false, tickTimeUtc, isDotDamage: true);
            AppendEncounterStartIfNeededLocked(wasStarted, tickTimeUtc);
            AppendCombatTimelineEntryLocked(
                tickTimeUtc,
                CombatTimelineEntryKind.Damage,
                $"{source.Name} 使用{dotActionName} 攻击 {loggedTargetName}，造成 {CreateDamageString(amount, useSuffix: true, useDecimals: true)} 伤害{FormatSimulatedCriticalSuffix(resolvedCritical)}。",
                source.Name,
                loggedTargetName,
                actorIsFriendly: true,
                targetIsFriendly: false,
                actionText: dotActionName);

            dotState.LastAttributedTickUtc = tickTimeUtc;
            dotState.TickCount++;
            dotState.NextTickRemainingTimeSeconds -= (float)PlayerDotTickInterval.TotalSeconds;
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Error(
                "统计",
                ex,
                $"补算玩家 DOT 伤害失败：sourceId=0x{source.ActorId:X8}，targetId=0x{dotState.Key.TargetActorId:X8}，statusId=0x{dotState.Key.StatusId:X8}。");
            return false;
        }
    }

    private void RefreshActivePlayerDotEstimatedDamageLocked(
        uint sourceId,
        uint targetId,
        uint actionId,
        string actionName,
        long observedDamage,
        bool observedCritical,
        bool observedDirectHit,
        DateTime nowUtc)
    {
        if (activePlayerDots.Count == 0)
            return;

        var normalizedActionName = NormalizeActionName(actionName);
        var matchingStates = activePlayerDots.Values
            .Where(state =>
                AreEquivalentActorIds(state.Key.SourceActorId, sourceId)
                && AreEquivalentActorIds(state.Key.TargetActorId, targetId)
                && (state.ActionId == actionId
                    || state.Key.StatusId == actionId
                    || IsUnknownActionName(normalizedActionName)
                    || IsUnknownActionName(state.ActionName)
                    || string.Equals(state.ActionName, normalizedActionName, StringComparison.Ordinal)
                    || string.Equals(state.StatusName, normalizedActionName, StringComparison.Ordinal)))
            .ToList();

        if (matchingStates.Count == 0)
        {
            matchingStates = activePlayerDots.Values
                .Where(state =>
                    AreEquivalentActorIds(state.Key.TargetActorId, targetId)
                    && (state.ActionId == actionId
                        || state.Key.StatusId == actionId
                        || IsUnknownActionName(normalizedActionName)
                        || IsUnknownActionName(state.ActionName)
                        || string.Equals(state.ActionName, normalizedActionName, StringComparison.Ordinal)
                        || string.Equals(state.StatusName, normalizedActionName, StringComparison.Ordinal)))
                .ToList();
        }

        if (matchingStates.Count == 0)
        {
            matchingStates = activePlayerDots.Values
                .Where(state =>
                    AreEquivalentActorIds(state.Key.SourceActorId, sourceId)
                    && AreEquivalentActorIds(state.Key.TargetActorId, targetId)
                    && state.SkillEntry?.Anchors.Any(anchor => anchor.ActionIds.Contains(actionId)) == true)
                .ToList();
        }

        foreach (var state in matchingStates)
        {
            var sourceAverageDamage = ResolveObservedAverageDamage(state.Source.ActorId);
            var estimatedTickDamage = observedDamage > 0
                ? EstimatePlayerDotTickDamageFromObservedDamage(observedDamage, actionId, observedCritical, observedDirectHit, sourceAverageDamage, state.SkillEntry)
                : ResolvePlayerDotEstimatedTickDamageLocked(state.Source, targetId, state.ActionName, state.StatusPotency, nowUtc, state.SkillEntry);

            if (estimatedTickDamage > 0)
            {
                state.EstimatedTickDamage = estimatedTickDamage;
                state.EstimatedTickDamageFromObservedSeed = observedDamage > 0;
            }
        }
    }

    private static long EstimatePlayerDotTickDamageFromObservedDamage(
        long observedDamage,
        uint observedActionId,
        bool observedCritical,
        bool observedDirectHit,
        long sourceAverageDamage,
        PlayerDotSkillEntry? skillEntry)
    {
        if (observedDamage <= 0)
            return 0L;

        if (TryEstimatePlayerDotTickDamageFromPotencyRatio(observedDamage, observedActionId, observedCritical, observedDirectHit, skillEntry, out var potencyEstimatedTickDamage))
            return potencyEstimatedTickDamage;

        var divisor = observedCritical ? 4d : 3d;
        if (observedDirectHit)
            divisor *= ObservedPlayerDotDirectHitMultiplier;

        var estimatedFromObserved = (long)Math.Round(observedDamage / divisor);
        if (sourceAverageDamage > 0)
        {
            var estimatedFromAverage = (long)Math.Round(sourceAverageDamage / 3d);
            if (estimatedFromAverage > 0)
                estimatedFromObserved = (long)Math.Round((estimatedFromObserved + estimatedFromAverage) / 2d);
        }

        return Math.Max(1L, estimatedFromObserved);
    }

    private static bool TryEstimatePlayerDotTickDamageFromPotencyRatio(
        long observedDamage,
        uint observedActionId,
        bool observedCritical,
        bool observedDirectHit,
        PlayerDotSkillEntry? skillEntry,
        out long estimatedTickDamage)
    {
        estimatedTickDamage = 0L;
        if (observedDamage <= 0 || skillEntry == null)
            return false;

        double potencyRatio;
        if (skillEntry.ActionIds.Contains(observedActionId))
        {
            if (!skillEntry.TryGetPotencyRatio(out potencyRatio))
                return false;
        }
        else
        {
            var matchedAnchor = skillEntry.Anchors.FirstOrDefault(anchor => anchor.ActionIds.Contains(observedActionId));
            if (matchedAnchor == null || !skillEntry.DotTickPotency.HasValue || matchedAnchor.Potency <= 0 || skillEntry.DotTickPotency.Value <= 0)
                return false;

            potencyRatio = skillEntry.DotTickPotency.Value / (double)matchedAnchor.Potency;
        }

        var normalizedObservedDamage = observedDamage / (observedCritical ? ObservedPlayerDotCriticalHitMultiplier : 1d);
        if (observedDirectHit)
            normalizedObservedDamage /= ObservedPlayerDotDirectHitMultiplier;

        estimatedTickDamage = Math.Max(1L, (long)Math.Round(normalizedObservedDamage * potencyRatio));
        return estimatedTickDamage > 0;
    }

    private static bool TryEstimatePlayerDotTickDamageFromAveragePotencyRatio(
        long sourceAverageDamage,
        PlayerDotSkillEntry? skillEntry,
        out long estimatedTickDamage)
    {
        estimatedTickDamage = 0L;
        if (sourceAverageDamage <= 0 || skillEntry == null)
            return false;

        double potencyRatio;
        if (skillEntry.TryGetPotencyRatio(out potencyRatio))
        {
        }
        else
        {
            var matchedAnchor = skillEntry.Anchors.FirstOrDefault(anchor => anchor.Potency > 0);
            if (matchedAnchor == null || !skillEntry.DotTickPotency.HasValue || skillEntry.DotTickPotency.Value <= 0)
                return false;

            potencyRatio = skillEntry.DotTickPotency.Value / (double)matchedAnchor.Potency;
        }

        estimatedTickDamage = Math.Max(1L, (long)Math.Round(sourceAverageDamage * potencyRatio));
        return estimatedTickDamage > 0;
    }

    private RecentHostilePlayerAction? ResolveRecentPlayerDotActionLocked(uint sourceActorId, uint targetActorId, string actionName, DateTime nowUtc)
    {
        var normalizedActionName = NormalizeActionName(actionName);
        var recentActions = recentHostilePlayerActions
            .Where(action =>
                AreEquivalentActorIds(action.Source.ActorId, sourceActorId)
                && AreEquivalentActorIds(action.TargetActorId, targetActorId)
                && nowUtc - action.ObservedAtUtc <= PlayerDotRecentActionTtl);

        if (!IsUnknownActionName(normalizedActionName))
        {
            var namedMatch = recentActions
                .Where(action => string.Equals(action.ActionName, normalizedActionName, StringComparison.Ordinal))
                .OrderByDescending(action => action.ObservedAtUtc)
                .FirstOrDefault();
            if (namedMatch != null)
                return namedMatch;
        }

        return recentActions
            .OrderByDescending(action => action.ObservedAtUtc)
            .FirstOrDefault();
    }

    private RecentHostilePlayerAction? ResolveRecentPlayerDotObservedActionLocked(
        uint sourceActorId,
        uint targetActorId,
        string actionName,
        DateTime nowUtc,
        PlayerDotSkillEntry? skillEntry)
    {
        var recentAction = ResolveRecentPlayerDotActionLocked(sourceActorId, targetActorId, actionName, nowUtc);
        if (recentAction?.ObservedDamageAmount > 0)
            return recentAction;

        return ResolveRecentPlayerDotAnchorActionLocked(sourceActorId, targetActorId, nowUtc, skillEntry);
    }

    private RecentHostilePlayerAction? ResolveRecentPlayerDotAnchorActionLocked(
        uint sourceActorId,
        uint targetActorId,
        DateTime nowUtc,
        PlayerDotSkillEntry? skillEntry)
    {
        if (skillEntry?.Anchors == null || skillEntry.Anchors.Count == 0)
            return null;

        foreach (var anchor in skillEntry.Anchors)
        {
            var targetMatch = recentHostilePlayerActions
                .Where(action =>
                    AreEquivalentActorIds(action.Source.ActorId, sourceActorId)
                    && AreEquivalentActorIds(action.TargetActorId, targetActorId)
                    && action.ObservedDamageAmount > 0
                    && anchor.ActionIds.Contains(action.ActionId)
                    && nowUtc - action.ObservedAtUtc <= PlayerDotRecentActionTtl)
                .OrderByDescending(action => action.ObservedAtUtc)
                .FirstOrDefault();
            if (targetMatch != null)
                return targetMatch;

            var sourceOnlyMatch = recentHostilePlayerActions
                .Where(action =>
                    AreEquivalentActorIds(action.Source.ActorId, sourceActorId)
                    && action.ObservedDamageAmount > 0
                    && anchor.ActionIds.Contains(action.ActionId)
                    && nowUtc - action.ObservedAtUtc <= PlayerDotRecentActionTtl)
                .OrderByDescending(action => action.ObservedAtUtc)
                .FirstOrDefault();
            if (sourceOnlyMatch != null)
                return sourceOnlyMatch;
        }

        return null;
    }

    private uint ResolveRecentPlayerDotActionIdLocked(uint sourceActorId, uint targetActorId, DateTime nowUtc)
    {
        return recentHostilePlayerActions
            .Where(action =>
                AreEquivalentActorIds(action.Source.ActorId, sourceActorId)
                && AreEquivalentActorIds(action.TargetActorId, targetActorId)
                && nowUtc - action.ObservedAtUtc <= PlayerDotRecentActionTtl)
            .OrderByDescending(action => action.ObservedAtUtc)
            .Select(action => action.ActionId)
            .FirstOrDefault(actionId => actionId != 0);
    }

    private long ResolvePlayerDotEstimatedTickDamageLocked(
        TrackedActor source,
        uint targetActorId,
        string actionName,
        int statusPotency,
        DateTime nowUtc,
        PlayerDotSkillEntry? skillEntry = null)
    {
        var normalizedActionName = NormalizeActionName(actionName);
        var recentAction = ResolveRecentPlayerDotObservedActionLocked(source.ActorId, targetActorId, normalizedActionName, nowUtc, skillEntry);

        var sourceAverageDamage = ResolveObservedAverageDamage(source.ActorId);

        if (recentAction?.ObservedDamageAmount > 0)
        {
            return EstimatePlayerDotTickDamageFromObservedDamage(
                recentAction.ObservedDamageAmount,
                recentAction.ActionId,
                recentAction.ObservedCritical == true,
                recentAction.ObservedDirectHit == true,
                sourceAverageDamage,
                skillEntry);
        }

        if (TryEstimatePlayerDotTickDamageFromAveragePotencyRatio(sourceAverageDamage, skillEntry, out var averagePotencyEstimatedTickDamage))
            return averagePotencyEstimatedTickDamage;

        if (sourceAverageDamage > 0)
            return Math.Max(1L, (long)Math.Round(sourceAverageDamage / 3d));

        if (statusPotency > 0)
            return Math.Max(1L, Math.Max(500L, statusPotency * 100L));

        return 500L;
    }

    private long ResolveObservedAverageDamage(uint sourceActorId)
    {
        var combatant = currentEncounter.Combatants
            .FirstOrDefault(combatant => combatant.ActorId == sourceActorId);

        if (combatant == null || combatant.Hits < 20)
            return 0L;

        return Math.Max(1L, (long)Math.Round(combatant.Damage / (double)Math.Max(1, combatant.Hits)));
    }

    private void TrimInactivePlayerDotsLocked(DateTime nowUtc)
    {
        if (activePlayerDots.Count == 0)
            return;

        var staleKeys = new List<PlayerDotKey>();
        foreach (var pair in activePlayerDots)
        {
            try
            {
                var state = pair.Value;
                if (state.RemainingTimeSeconds <= 0f)
                {
                    staleKeys.Add(pair.Key);
                    continue;
                }

                if (nowUtc - state.LastSeenUtc > PlayerDotStatusGracePeriod)
                {
                    staleKeys.Add(pair.Key);
                    continue;
                }

                var targetObject = FindObjectByActorId(pair.Key.TargetActorId);
                if (targetObject == null || !targetObject.IsTargetable)
                {
                    staleKeys.Add(pair.Key);
                    continue;
                }
            }
            catch
            {
                staleKeys.Add(pair.Key);
            }
        }

        foreach (var key in staleKeys)
            activePlayerDots.Remove(key);
    }

    private string? ResolveRecentPlayerDotActionNameLocked(uint sourceActorId, uint targetActorId, DateTime nowUtc)
    {
        return recentHostilePlayerActions
            .Where(action =>
                AreEquivalentActorIds(action.Source.ActorId, sourceActorId)
                && AreEquivalentActorIds(action.TargetActorId, targetActorId)
                && nowUtc - action.ObservedAtUtc <= PlayerDotRecentActionTtl)
            .OrderByDescending(action => action.ObservedAtUtc)
            .Select(action => action.ActionName)
            .FirstOrDefault(actionName => !string.IsNullOrWhiteSpace(actionName));
    }

    private static bool IsPlayerDotTickReady(ActivePlayerDotState dotState, DateTime nowUtc)
        => nowUtc - dotState.LastAttributedTickUtc >= PlayerDotTickInterval - PlayerDotTickJitterAllowance;

    private bool ResolvePlayerDotCritical(uint sourceActorId, ActivePlayerDotState dotState, bool reportedCritical)
    {
        if (reportedCritical)
            return true;

        var critRate = ResolveObservedCritRate(sourceActorId);
        return IsSimulatedCritical(sourceActorId, dotState.Key.TargetActorId, dotState.Key.StatusId, dotState.TickCount, critRate);
    }

    private double ResolveObservedCritRate(uint sourceActorId)
    {
        var combatant = currentEncounter.Combatants
            .FirstOrDefault(combatant => combatant.ActorId == sourceActorId);

        if (combatant == null || combatant.Hits < 20)
            return 0.25d;

        var critRate = combatant.CritHits / (double)Math.Max(1, combatant.Hits);
        return Math.Clamp(critRate, 0.05d, 0.95d);
    }

    private static bool IsSimulatedCritical(uint sourceActorId, uint targetActorId, uint statusId, int tickIndex, double critRate)
    {
        if (critRate <= 0d)
            return false;

        if (critRate >= 1d)
            return true;

        unchecked
        {
            uint hash = 2166136261;
            hash = (hash ^ sourceActorId) * 16777619;
            hash = (hash ^ targetActorId) * 16777619;
            hash = (hash ^ statusId) * 16777619;
            hash = (hash ^ (uint)tickIndex) * 16777619;

            var sample = hash / (double)uint.MaxValue;
            return sample < critRate;
        }
    }

    private static string ResolvePreferredDotActionName(string existingActionName, string newActionName, string statusName)
    {
        if (!IsUnknownActionName(existingActionName))
            return existingActionName;

        if (!IsUnknownActionName(newActionName))
            return newActionName;

        return !string.IsNullOrWhiteSpace(statusName)
            ? statusName
            : "\u672A\u77E5\u6301\u7EED\u4F24\u5BB3";
    }

    private static uint ResolvePreferredPlayerDotActionId(uint observedActionId, PlayerDotSkillEntry? skillEntry)
    {
        if (skillEntry == null)
            return observedActionId;

        var preferredActionId = skillEntry.GetPreferredActionId(observedActionId);
        return preferredActionId != 0 ? preferredActionId : observedActionId;
    }

    private static string ResolvePreferredPlayerDotActionName(string observedActionName, string statusName, PlayerDotSkillEntry? skillEntry)
    {
        if (!string.IsNullOrWhiteSpace(skillEntry?.SkillName))
            return NormalizeActionName(skillEntry.SkillName);

        if (!string.IsNullOrWhiteSpace(observedActionName))
            return NormalizeActionName(observedActionName);

        if (!string.IsNullOrWhiteSpace(statusName))
            return NormalizeActionName(statusName);

        return "\u672A\u77E5\u6301\u7EED\u4F24\u5BB3";
    }

    private bool IsPlayerDamageOverTimeStatus(object status)
    {
        var statusId = GetStatusId(status);
        if (statusId == 0)
            return false;

        if (dotStatusClassificationCache.TryGetValue(statusId, out var cached))
            return cached;

        var result = PlayerDotCatalog.IsKnownPlayerDotStatus(statusId);
        dotStatusClassificationCache[statusId] = result;
        return result;
    }

    private static uint ResolveStatusSourceActorId(object status)
    {
        var sourceId = TryConvertActorId(GetReflectedStatusValue(status, "SourceId"));
        if (sourceId is > 0 and not InvalidActorId)
            return sourceId;

        var sourceObject = GetReflectedStatusValue(status, "SourceObject") as IGameObject;
        return sourceObject == null
            ? 0
            : GetGameObjectIdentity(sourceObject).ResolveActorId();
    }

    private static string? TryGetStatusGameDataText(object status, string propertyName)
    {
        try
        {
            var gameData = GetReflectedStatusValue(status, "GameData");
            if (gameData == null)
                return null;

            var row = gameData.GetType().GetProperty("Value")?.GetValue(gameData);
            if (row == null)
                return null;

            var value = row.GetType().GetProperty(propertyName)?.GetValue(row);
            return TryExtractGameDataText(value);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryExtractGameDataText(object? value)
    {
        if (value == null)
            return null;

        if (value is string text)
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        try
        {
            var extractTextMethod = value.GetType().GetMethod("ExtractText", Type.EmptyTypes);
            if (extractTextMethod?.Invoke(value, null) is string extracted && !string.IsNullOrWhiteSpace(extracted))
                return extracted.Trim();
        }
        catch
        {
        }

        try
        {
            if (value.GetType().GetProperty("TextValue")?.GetValue(value) is string textValue && !string.IsNullOrWhiteSpace(textValue))
                return textValue.Trim();
        }
        catch
        {
        }

        var fallback = value.ToString();
        return string.IsNullOrWhiteSpace(fallback) ? null : fallback.Trim();
    }

    private static int TryGetStatusGameDataInt(object status, string propertyName)
    {
        try
        {
            var gameData = GetReflectedStatusValue(status, "GameData");
            if (gameData == null)
                return 0;

            var row = gameData.GetType().GetProperty("Value")?.GetValue(gameData);
            if (row == null)
                return 0;

            var value = row.GetType().GetProperty(propertyName)?.GetValue(row);
            if (value == null)
                return 0;

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static string FormatPlayerDotActionName(string actionName)
        => $"{NormalizeActionName(actionName)}\uFF08\u6301\u7EED\u4F24\u5BB3\uFF09";

    private static string FormatActionNameWithId(string? actionName, uint actionId)
    {
        var normalizedActionName = NormalizeActionName(actionName);
        return actionId == 0
            ? normalizedActionName
            : $"{normalizedActionName}[{actionId}]";
    }

    private static uint GetStatusId(object status)
        => TryConvertActorId(GetReflectedStatusValue(status, "StatusId"));

    private static float GetStatusRemainingTime(object status)
    {
        try
        {
            var remainingTime = GetReflectedStatusValue(status, "RemainingTime");
            return remainingTime == null
                ? 0f
                : Convert.ToSingle(remainingTime, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0f;
        }
    }

    private static object? GetReflectedStatusValue(object status, string propertyName)
    {
        try
        {
            return status.GetType().GetProperty(propertyName)?.GetValue(status);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsUnknownActionName(string? actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
            return true;

        var normalized = actionName.Trim();
        return string.Equals(normalized, "鏈煡鎶€鑳?", StringComparison.Ordinal)
               || string.Equals(normalized, "\u672A\u77E5\u6280\u80FD", StringComparison.Ordinal)
               || normalized.StartsWith("\u6280\u80FD", StringComparison.Ordinal);
    }

    private static string BuildPlayerDotStatusSummary(IBattleNpc hostileTarget)
    {
        var statusSummaries = new List<string>();
        foreach (var status in EnumerateStatusEntries(hostileTarget))
        {
            try
            {
                var statusId = GetStatusId(status);
                if (statusId == 0)
                    continue;

                var statusName = TryGetStatusGameDataText(status, "Name") ?? "未知状态";
                var remainingTime = GetStatusRemainingTime(status);
                var sourceActorId = ResolveStatusSourceActorId(status);
                var sourceText = sourceActorId is 0 or InvalidActorId
                    ? "source=?"
                    : $"source=0x{sourceActorId:X8}";
                statusSummaries.Add($"{statusName}[{statusId}] {remainingTime:0.0}s {sourceText}");
                if (statusSummaries.Count >= 8)
                    break;
            }
            catch
            {
                // Ignore reflection issues while building debug summaries.
            }
        }

        return statusSummaries.Count == 0
            ? "无有效状态"
            : string.Join("；", statusSummaries);
    }

    private static IReadOnlyList<object> EnumerateStatusEntries(object statusOwner)
    {
        var entries = new List<object>();
        if (statusOwner == null)
            return entries;

        object? statusList = null;
        try
        {
            statusList = statusOwner.GetType().GetProperty("StatusList")?.GetValue(statusOwner);
        }
        catch
        {
            return entries;
        }

        if (statusList == null)
            return entries;

        if (statusList is System.Collections.IEnumerable enumerable)
        {
            try
            {
                foreach (var entry in enumerable)
                {
                    if (entry != null)
                        entries.Add(entry);
                }
            }
            catch
            {
                // Fall through to reflection-based enumerator below.
            }

            if (entries.Count > 0)
                return entries;
        }

        try
        {
            var enumerator = statusList.GetType().GetMethod("GetEnumerator", Type.EmptyTypes)?.Invoke(statusList, null);
            if (enumerator == null)
                return entries;

            var moveNextMethod = enumerator.GetType().GetMethod("MoveNext", Type.EmptyTypes);
            var currentProperty = enumerator.GetType().GetProperty("Current");
            if (moveNextMethod == null || currentProperty == null)
                return entries;

            while (true)
            {
                var moved = moveNextMethod.Invoke(enumerator, null);
                if (moved is not bool hasNext || !hasNext)
                    break;

                var current = currentProperty.GetValue(enumerator);
                if (current != null)
                    entries.Add(current);
            }
        }
        catch
        {
            // Ignore incompatible runtime status enumerators and return what we already collected.
        }

        return entries;
    }

    private static bool TryGetHostileBattleTarget(uint targetId, out IBattleNpc hostileTarget)
    {
        hostileTarget = default!;
        try
        {
            var targetObject = FindObjectByActorId(targetId);
            if (targetObject is not IBattleNpc battleNpc)
                return false;

            if ((battleNpc.StatusFlags & StatusFlags.Hostile) == 0)
                return false;

            hostileTarget = battleNpc;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public bool IsTrackedActor(uint actorId)
    {
        lock (gate)
            return TryGetTrackedActor(actorId, out _);
    }

    public bool CanResolveTrackedSource(uint actorId, DateTime nowUtc)
    {
        lock (gate)
            return TryResolveTrackedSource(actorId, nowUtc, out _);
    }

    private bool TryResolveCombatantSource(uint actorId, DateTime nowUtc, out TrackedActor actor, out bool isFriendly)
    {
        if (TryResolveTrackedSource(actorId, nowUtc, out actor))
        {
            isFriendly = actor.Kind != TrackedActorKind.HostileNpc;
            return true;
        }

        if (TryGetHostileBattleNpcTrackedActor(actorId, out actor))
        {
            isFriendly = false;
            return true;
        }

        isFriendly = false;
        return false;
    }

    public bool TryResolveTrackedSourceFromGameObject(IGameObject? gameObject, DateTime nowUtc, out uint actorId)
    {
        lock (gate)
        {
            actorId = 0;
            if (gameObject == null)
                return false;

            var identity = GetGameObjectIdentity(gameObject);
            var directActorId = identity.ResolveActorId();
            if (TryResolveTrackedSource(directActorId, nowUtc, out var resolvedActor))
            {
                actorId = resolvedActor.ActorId;
                return true;
            }

            if (TryGetTrackedActor(gameObject, out resolvedActor))
            {
                actorId = resolvedActor.ActorId;
                return true;
            }

            var ownerActorId = ResolveOwner(directActorId, nowUtc);
            if (TryResolveTrackedSource(ownerActorId, nowUtc, out resolvedActor))
            {
                actorId = resolvedActor.ActorId;
                return true;
            }

            return false;
        }
    }

    public bool ObserveFriendlyCombatantFromGameObject(IGameObject? gameObject, out uint actorId)
    {
        lock (gate)
        {
            actorId = 0;
            if (!TryCreateObservedFriendlyActor(gameObject, out var actor))
                return false;

            var shouldLog = !observedFriendlyActorCache.ContainsKey(actor.ActorId);
            observedFriendlyActorCache[actor.ActorId] = actor;
            actorId = actor.ActorId;
            if (shouldLog)
                LogHelper.DebugRecent("统计", $"已纳入可跟踪友方对象：name={actor.Name}，actorId=0x{actor.ActorId:X8}。");
            return true;
        }
    }

    public bool ObserveFriendlyCombatantIdentity(uint actorId, string? name)
    {
        lock (gate)
        {
            if (!TryCreateObservedFriendlyActor(actorId, name, out var actor))
                return false;

            var shouldLog = !observedFriendlyActorCache.ContainsKey(actor.ActorId);
            observedFriendlyActorCache[actor.ActorId] = actor;
            if (shouldLog)
                LogHelper.DebugRecent("统计", $"已按事件身份纳入可跟踪友方对象：name={actor.Name}，actorId=0x{actor.ActorId:X8}。");
            return true;
        }
    }

    private void UpdateNoDataCombatDiagnostics(DateTime nowUtc, bool inCombat)
    {
        if (!LogHelper.EnableDebugLog)
        {
            enteredCombatWithoutDataSinceUtc = default;
            lastNoDataCombatDiagnosticUtc = default;
            return;
        }

        if (!inCombat || currentEncounter.Started || combatTimelineEntries.Count > 0)
        {
            enteredCombatWithoutDataSinceUtc = default;
            lastNoDataCombatDiagnosticUtc = default;
            return;
        }

        if (enteredCombatWithoutDataSinceUtc == default)
        {
            enteredCombatWithoutDataSinceUtc = nowUtc;
            return;
        }

        var noDataDuration = nowUtc - enteredCombatWithoutDataSinceUtc;
        if (noDataDuration < TimeSpan.FromSeconds(3))
            return;

        if (lastNoDataCombatDiagnosticUtc != default
            && nowUtc - lastNoDataCombatDiagnosticUtc < TimeSpan.FromSeconds(5))
        {
            return;
        }

        lastNoDataCombatDiagnosticUtc = nowUtc;
        LogHelper.DebugRecent(
            "统计",
            $"已进入战斗 {Math.Floor(noDataDuration.TotalSeconds)} 秒但仍未记录任何战斗流水或统计事件：区域={currentEncounter.ZoneName}，localPlayerGameObjectId=0x{DalamudApi.GetLocalPlayerGameObjectId():X16}，localPlayerObjectId=0x{DalamudApi.GetLocalPlayerObjectId():X8}，localPlayerEntityId=0x{DalamudApi.GetLocalPlayerEntityId():X8}，partyCount={DalamudApi.PartyList.Count()}，buddyCount={DalamudApi.BuddyList.Count()}。");
    }

    private void PollPartyMemberDeaths(DateTime nowUtc, string zoneName, bool inCombat)
    {
        var activePartyActorIds = new HashSet<uint>();

        foreach (var actor in EnumerateTrackedPartyBattleCharas())
        {
            var actorId = ResolveBattleCharaActorId(actor);
            if (actorId is 0 or InvalidActorId)
                continue;

            activePartyActorIds.Add(actorId);
            UpdateTrackedActorHp(actorId, actor.CurrentHp, nowUtc, zoneName, inCombat);
        }

        if (partyMemberHpCache.Count == 0)
            return;

        var staleActorIds = new List<uint>();
        foreach (var actorId in partyMemberHpCache.Keys)
        {
            if (!activePartyActorIds.Contains(actorId))
                staleActorIds.Add(actorId);
        }

        foreach (var actorId in staleActorIds)
            partyMemberHpCache.Remove(actorId);
    }

    private void UpdateTrackedActorHp(uint actorId, uint currentHp, DateTime nowUtc, string zoneName, bool inCombat)
    {
        if (partyMemberHpCache.TryGetValue(actorId, out var previousHp)
            && previousHp > 0
            && currentHp == 0
            && (inCombat || currentEncounter.Started)
            && TryGetTrackedActor(actorId, out var actor))
        {
            currentEncounter.ZoneName = zoneName;
            currentEncounter.RecordDeath(actor, nowUtc);
            AppendCombatTimelineEntryLocked(
                nowUtc,
                CombatTimelineEntryKind.Death,
                $"{actor.Name} 战斗不能。",
                actor.Name,
                actor.Name,
                true,
                true);
        }

        partyMemberHpCache[actorId] = currentHp;
    }

    private void UpdatePartyOutOfCombatTimer(DateTime nowUtc, bool allPartyMembersOutOfCombat)
    {
        if (!currentEncounter.Started)
        {
            partyOutOfCombatSinceUtc = default;
            return;
        }

        if (!allPartyMembersOutOfCombat)
        {
            partyOutOfCombatSinceUtc = default;
            return;
        }

        if (partyOutOfCombatSinceUtc == default)
            partyOutOfCombatSinceUtc = nowUtc;
    }

    private bool ShouldFinalizeEncounter(DateTime nowUtc, bool allPartyMembersOutOfCombat)
    {
        if (!currentEncounter.Started)
            return false;

        return config.CombatEndRule switch
        {
            CombatEndRule.PartyList => allPartyMembersOutOfCombat,
            CombatEndRule.PartyListWithDelay => allPartyMembersOutOfCombat
                && partyOutOfCombatSinceUtc != default
                && nowUtc - partyOutOfCombatSinceUtc >= TimeSpan.FromSeconds(config.EncounterTimeoutSeconds),
            _ => allPartyMembersOutOfCombat,
        };
    }

    private void FinalizeEncounter(DateTime finalizedAtUtc)
    {
        if (!currentEncounter.HasMeaningfulData)
        {
            AppendCombatTimelineEntryLocked(finalizedAtUtc, CombatTimelineEntryKind.CombatEnd, $"战斗结束：{currentEncounter.ZoneName}（未记录到有效战斗数据）。");
            LogHelper.Debug("统计", $"已丢弃区域 {currentEncounter.ZoneName} 的战斗结算：未记录到有效战斗数据。");
            currentEncounter = new EncounterSession
            {
                ZoneName = currentEncounter.ZoneName,
            };
            observedFriendlyActorCache.Clear();
            recentHostilePlayerActions.Clear();
            activePlayerDots.Clear();
            partyOutOfCombatSinceUtc = default;
            lastPlayerDotStatusPollUtc = default;
            encounterFinalizedVersion++;
            suppressStaleDisplayUntilNextCombatStart = true;
            return;
        }

        CurrentCombatData = ActxSnapshotFormatter.Build(currentEncounter, isActive: false);

        var history = new HistoricalCombatData(
            currentEncounter.ZoneName,
            ActxSnapshotFormatter.FormatDuration(currentEncounter.DurationSeconds),
            CurrentCombatData,
            currentEncounter.StartUtc,
            finalizedAtUtc);

        if (currentEncounter.DurationSeconds >= MinimumHistoricalEncounterSeconds)
        {
            if (historicalRecords.Count == 0 || !HasSameHistoryIdentity(historicalRecords[^1], history))
                historicalRecords.Add(history);
            else
                historicalRecords[^1] = history;

            SortHistoricalRecords();
        }

        var totalDamage = currentEncounter.Combatants.Sum(static combatant => combatant.Damage);
        var totalHealing = currentEncounter.Combatants.Sum(static combatant => combatant.Healed);
        var totalDamageTaken = currentEncounter.Combatants.Sum(static combatant => combatant.DamageTaken);
        LogHelper.Debug(
            "统计",
            $"战斗已结算：区域={history.ZoneName}，时长={history.Duration}，参战对象={currentEncounter.Combatants.Count}，伤害={totalDamage}，治疗={totalHealing}，承伤={totalDamageTaken}，已写入历史={currentEncounter.DurationSeconds >= MinimumHistoricalEncounterSeconds}。");
        AppendCombatTimelineEntryLocked(finalizedAtUtc, CombatTimelineEntryKind.CombatEnd, $"战斗结束：{history.ZoneName}，持续 {history.Duration}。");

        currentEncounter = new EncounterSession
        {
            ZoneName = history.ZoneName,
        };
        observedFriendlyActorCache.Clear();
        recentHostilePlayerActions.Clear();
        activePlayerDots.Clear();
        partyOutOfCombatSinceUtc = default;
        lastPlayerDotStatusPollUtc = default;
        encounterFinalizedVersion++;
        suppressStaleDisplayUntilNextCombatStart = true;
    }

    private bool TryResolveTrackedSource(uint actorId, DateTime nowUtc, out TrackedActor actor)
    {
        actor = default;
        if (TryGetTrackedActor(actorId, out actor))
            return true;

        var resolvedActorId = ResolveOwner(actorId, nowUtc);
        if (resolvedActorId is 0 or InvalidActorId || resolvedActorId == actorId)
            return false;

        return TryGetTrackedActor(resolvedActorId, out actor);
    }

    private uint ResolveOwner(uint actorId, DateTime nowUtc)
    {
        if (actorId == 0 || actorId == InvalidActorId)
            return InvalidActorId;

        var obj = FindObjectByActorId(actorId);
        if (obj != null
            && ShouldResolveOwnerForObject(obj)
            && obj.OwnerId is > 0 and not InvalidActorId)
        {
            ownerCache[actorId] = new OwnerCacheEntry(obj.OwnerId, nowUtc);
            return obj.OwnerId;
        }

        if (ownerCache.TryGetValue(actorId, out var cached) && nowUtc - cached.UpdatedAtUtc <= OwnerCacheTtl)
            return cached.OwnerId;

        return InvalidActorId;
    }

    private static uint ResolvePartyMemberActorId(Dalamud.Game.ClientState.Party.IPartyMember member)
        => ResolvePartyMemberActorId(member, member.GameObject);

    private static uint ResolvePartyMemberActorId(Dalamud.Game.ClientState.Party.IPartyMember member, IGameObject? gameObject)
        => GetPartyMemberIdentity(member, gameObject).ResolveActorId();

    private bool TryGetTrackedActor(uint actorId, out TrackedActor actor)
    {
        actor = default;
        if (actorId is 0 or InvalidActorId)
            return false;

        if (TryGetTrackedPartyBattleCharaActor(actorId, out actor))
            return true;

        if (TryGetPartyMemberTrackedActor(actorId, out actor))
            return true;

        if (TryGetBuddyTrackedActor(actorId, out actor))
            return true;

        if (observedFriendlyActorCache.TryGetValue(actorId, out actor))
            return true;

        if (TryGetFriendlyBattleNpcTrackedActor(actorId, out actor))
            return true;


        return TryGetLocalPlayerTrackedActor(actorId, out actor);
    }

    private static bool TryGetTrackedActor(IGameObject? gameObject, out TrackedActor actor)
    {
        actor = default;
        if (gameObject == null)
            return false;

        if (gameObject is IBattleChara battleChara && TryGetTrackedBattleCharaActor(battleChara, out actor))
            return true;


        var identity = GetGameObjectIdentity(gameObject);
        if (identity.ResolveActorId() is var actorId && actorId != 0 && TryGetLocalPlayerTrackedActor(actorId, out actor))
            return true;

        return false;
    }

    private static bool TryGetTrackedBattleCharaActor(IBattleChara battleChara, out TrackedActor actor)
    {
        foreach (var trackedBattleChara in EnumerateTrackedPartyBattleCharas())
        {
            if (!AreSameGameObject(trackedBattleChara, battleChara))
                continue;

            var trackedActor = CreateTrackedActor(trackedBattleChara, ResolveBattleCharaActorId(battleChara));
            if (trackedActor == null)
                continue;

            actor = trackedActor.Value;
            return true;
        }

        var battleCharaActorId = ResolveBattleCharaActorId(battleChara);
        if (battleCharaActorId != 0 && TryGetLocalPlayerTrackedActor(battleCharaActorId, out actor))
            return true;


        actor = default;
        return false;
    }

    private static bool TryGetPartyMemberTrackedActor(uint actorId, out TrackedActor actor)
    {
        foreach (var member in DalamudApi.PartyList)
        {
            if (!MatchesPartyMemberActor(member, actorId))
                continue;

            var name = member.Name.TextValue ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var jobId = member.ClassJob.RowId;
            var canonicalActorId = ResolvePartyMemberActorId(member);
            actor = new TrackedActor(
                canonicalActorId is 0 or InvalidActorId ? actorId : canonicalActorId,
                name.Trim(),
                jobId,
                ResolveJobName(jobId),
                ResolvePartyMemberTrackedActorKind(member, member.GameObject));
            return true;
        }

        actor = default;
        return false;
    }

    private static bool TryGetTrackedPartyBattleCharaActor(uint actorId, out TrackedActor actor)
    {
        foreach (var battleChara in EnumerateTrackedPartyBattleCharas())
        {
            if (!MatchesBattleCharaActor(battleChara, actorId))
                continue;

            var trackedActor = CreateTrackedActor(battleChara, actorId);
            if (trackedActor == null)
                continue;

            actor = trackedActor.Value;
            return true;
        }

        actor = default;
        return false;
    }

    private static bool TryGetBuddyTrackedActor(uint actorId, out TrackedActor actor)
    {
        foreach (var buddy in DalamudApi.BuddyList)
        {
            if (!MatchesBuddyActor(buddy, actorId))
                continue;

            var gameObject = buddy.GameObject;
            var canonicalActorId = ResolveBuddyActorId(buddy, gameObject);
            var name = gameObject?.Name.TextValue?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                name = FindObjectByActorId(actorId)?.Name.TextValue?.Trim();

            if (string.IsNullOrWhiteSpace(name))
                continue;

            actor = new TrackedActor(
                canonicalActorId is 0 or InvalidActorId ? actorId : canonicalActorId,
                name,
                0,
                string.Empty,
                TrackedActorKind.FriendlyNpc);
            return true;
        }

        actor = default;
        return false;
    }

    private static bool TryGetFriendlyBattleNpcTrackedActor(uint actorId, out TrackedActor actor)
    {
        foreach (var obj in DalamudApi.ObjectTable)
        {
            if (obj is not IBattleNpc battleNpc)
                continue;

            if (!IsFriendlyTrackedBattleNpc(battleNpc))
                continue;

            if (!MatchesBattleCharaActor(battleNpc, actorId))
                continue;

            var trackedActor = CreateTrackedActor(battleNpc, actorId);
            if (trackedActor == null)
                continue;

            actor = trackedActor.Value;
            return true;
        }

        actor = default;
        return false;
    }

    private bool TryGetHostileBattleNpcTrackedActor(uint actorId, out TrackedActor actor)
    {
        foreach (var obj in DalamudApi.ObjectTable)
        {
            if (obj is not IBattleNpc battleNpc)
                continue;

            if ((battleNpc.StatusFlags & StatusFlags.Hostile) == 0)
                continue;

            if (!ShouldTrackHostileBattleNpc(battleNpc))
                continue;

            if (!MatchesBattleCharaActor(battleNpc, actorId))
                continue;

            var trackedActor = CreateTrackedActor(battleNpc, actorId);
            if (trackedActor == null)
                continue;

            actor = trackedActor.Value;
            return true;
        }

        actor = default;
        return false;
    }

    private bool ShouldTrackHostileBattleNpc(IBattleNpc battleNpc)
    {
        var localPlayerMaxHp = DalamudApi.GetLocalPlayerMaxHp();
        if (localPlayerMaxHp == 0)
            return false;

        var multiplier = Math.Clamp(config.HostileNpcMinHpMultiplier <= 0 ? 10 : config.HostileNpcMinHpMultiplier, 1, 100);
        return (ulong)battleNpc.MaxHp >= (ulong)localPlayerMaxHp * (ulong)multiplier;
    }

    private static bool MatchesPartyMemberActor(Dalamud.Game.ClientState.Party.IPartyMember member, uint actorId)
    {
        var gameObject = member.GameObject;
        var identity = GetPartyMemberIdentity(member, gameObject);
        // 这里只按 ID 口径匹配，不再按名字兜底，避免误把队外同名对象算进来。
        return identity.MatchesActorId(actorId);
    }

    private static bool MatchesBuddyActor(IBuddyMember buddy, uint actorId)
    {
        var gameObject = buddy.GameObject;
        var identity = GetBuddyIdentity(buddy, gameObject);
        return identity.MatchesActorId(actorId);
    }

    private static bool MatchesBattleCharaActor(IBattleChara battleChara, uint actorId)
    {
        var identity = GetGameObjectIdentity(battleChara);
        return identity.MatchesActorId(actorId);
    }

    private static bool AreSameGameObject(IGameObject? left, IGameObject? right)
    {
        if (left == null || right == null)
            return false;

        if (left.Address != nint.Zero && right.Address != nint.Zero && left.Address == right.Address)
            return true;

        var leftIdentity = GetGameObjectIdentity(left);
        var rightIdentity = GetGameObjectIdentity(right);

        return (leftIdentity.GameObjectId != 0 && leftIdentity.GameObjectId == rightIdentity.GameObjectId)
               || (leftIdentity.ActorId != 0 && leftIdentity.ActorId == rightIdentity.ActorId)
               || (leftIdentity.ObjectId != 0 && leftIdentity.ObjectId == rightIdentity.ObjectId)
               || (leftIdentity.EntityId != 0 && leftIdentity.EntityId == rightIdentity.EntityId);
    }

    private static bool AreEquivalentActorIds(uint leftActorId, uint rightActorId)
    {
        if (leftActorId is 0 or InvalidActorId || rightActorId is 0 or InvalidActorId)
            return false;

        if (leftActorId == rightActorId)
            return true;

        var leftObject = FindObjectByActorId(leftActorId);
        var rightObject = FindObjectByActorId(rightActorId);
        if (leftObject != null && rightObject != null && AreSameGameObject(leftObject, rightObject))
            return true;

        if (leftObject != null && GetGameObjectIdentity(leftObject).MatchesActorId(rightActorId))
            return true;

        if (rightObject != null && GetGameObjectIdentity(rightObject).MatchesActorId(leftActorId))
            return true;

        return false;
    }

    private static IGameObject? FindObjectByActorId(uint actorId)
    {
        if (actorId is 0 or InvalidActorId)
            return null;

        var entityMatch = DalamudApi.ObjectTable.SearchByEntityId(actorId);
        if (entityMatch != null)
            return entityMatch;

        foreach (var obj in DalamudApi.ObjectTable)
        {
            if (obj == null)
                continue;

            var identity = GetGameObjectIdentity(obj);
            if (identity.MatchesActorId(actorId))
                return obj;
        }

        return null;
    }

    private static uint ResolveBattleCharaActorId(IBattleChara battleChara)
    {
        return GetGameObjectIdentity(battleChara).ResolveActorId();
    }

    private static TrackedActor? CreateTrackedActor(IBattleChara battleChara, uint fallbackActorId)
    {
        var name = battleChara.Name.TextValue?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var actorId = ResolveBattleCharaActorId(battleChara);
        var jobId = battleChara.ClassJob.RowId;
        return new TrackedActor(
            actorId is 0 or InvalidActorId ? fallbackActorId : actorId,
            name,
            jobId,
            ResolveJobName(jobId),
            ResolveTrackedActorKind(battleChara));
    }

    private static TrackedActorKind ResolvePartyMemberTrackedActorKind(Dalamud.Game.ClientState.Party.IPartyMember member, IGameObject? gameObject)
    {
        if (gameObject is IPlayerCharacter)
            return TrackedActorKind.Player;

        var name = member.Name.TextValue?.Trim();
        return LooksLikeDutyCompanionName(name)
            ? TrackedActorKind.FriendlyNpc
            : gameObject is IBattleNpc or IBattleChara ? TrackedActorKind.FriendlyNpc : TrackedActorKind.Player;
    }

    private static TrackedActorKind ResolveTrackedActorKind(IGameObject? gameObject)
    {
        return gameObject switch
        {
            null => TrackedActorKind.Unknown,
            IPlayerCharacter => TrackedActorKind.Player,
            IBattleNpc battleNpc => (battleNpc.StatusFlags & StatusFlags.Hostile) != 0
                ? TrackedActorKind.HostileNpc
                : TrackedActorKind.FriendlyNpc,
            IBattleChara => TrackedActorKind.FriendlyNpc,
            _ => TrackedActorKind.Unknown,
        };
    }

    private static IEnumerable<IBattleChara> EnumerateTrackedPartyBattleCharas()
    {
        var seen = new HashSet<ulong>();

        var localPlayerBattleChara = TryResolveBattleCharaFromIdentity(GetLocalPlayerIdentity());
        if (localPlayerBattleChara != null && TryMarkUniqueBattleChara(localPlayerBattleChara, seen))
            yield return localPlayerBattleChara;

        foreach (var member in DalamudApi.PartyList)
        {
            // 不再依赖 PronounModule.ResolvePlaceholder。
            // 旧版构建在新版运行时这里会触发 MissingMethodException，优先保证稳定性。
            var battleChara = ResolvePartyMemberBattleChara(member);
            if (battleChara == null || !TryMarkUniqueBattleChara(battleChara, seen))
                continue;

            yield return battleChara;
        }

        foreach (var buddy in DalamudApi.BuddyList)
        {
            var battleChara = ResolveBuddyBattleChara(buddy);
            if (battleChara == null || !TryMarkUniqueBattleChara(battleChara, seen))
                continue;

            yield return battleChara;
        }

        foreach (var obj in DalamudApi.ObjectTable)
        {
            if (obj is not IBattleChara battleChara)
                continue;

            // Do not treat ObjectTable PartyMember flags as authoritative party membership here.
            // In 24-man duties, alliance members may look friendly and cause out-of-party DoT attribution.
            if (!IsFriendlyTrackedBattleNpc(battleChara) || !TryMarkUniqueBattleChara(battleChara, seen))
                continue;

            yield return battleChara;
        }
    }

    private static IBattleChara? ResolvePartyMemberBattleChara(Dalamud.Game.ClientState.Party.IPartyMember member)
    {
        var gameObject = member.GameObject;
        if (gameObject is IBattleChara battleChara)
            return battleChara;

        return TryResolveBattleCharaFromIdentity(GetPartyMemberIdentity(member, gameObject));
    }

    private static IBattleChara? ResolveBuddyBattleChara(IBuddyMember buddy)
    {
        var gameObject = buddy.GameObject;
        if (gameObject is IBattleChara battleChara)
            return battleChara;

        return TryResolveBattleCharaFromIdentity(GetBuddyIdentity(buddy, gameObject));
    }

    private static bool TryMarkUniqueBattleChara(IBattleChara battleChara, ISet<ulong> seen)
    {
        var uniqueId = ResolveBattleCharaUniqueId(battleChara);
        return uniqueId != 0 && seen.Add(uniqueId);
    }

    private static ulong ResolveBattleCharaUniqueId(IBattleChara battleChara)
    {
        var address = battleChara.Address;
        if (address != nint.Zero)
            return unchecked((ulong)address);

        try
        {
            var gameObjectId = TryGetGameObjectId(battleChara);
            return gameObjectId != 0 ? gameObjectId : battleChara.EntityId;
        }
        catch
        {
            return battleChara.EntityId;
        }
    }

    private static uint ResolveBuddyActorId(IBuddyMember buddy)
        => ResolveBuddyActorId(buddy, buddy.GameObject);

    private static uint ResolveBuddyActorId(IBuddyMember buddy, IGameObject? gameObject)
    {
        return GetBuddyIdentity(buddy, gameObject).ResolveActorId();
    }

    private static ulong TryGetGameObjectId(IGameObject? gameObject)
    {
        if (gameObject == null)
            return 0UL;

        try
        {
            return Convert.ToUInt64(gameObject.GameObjectId, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0UL;
        }
    }

    private static ActorIdentity GetGameObjectIdentity(IGameObject? gameObject)
    {
        var gameObjectId = TryGetGameObjectId(gameObject);
        // ActionEffectHandler 这条统计链路里拿到的是低 32 位 ID。
        // 因此内部 actorId 口径继续保留 uint，但对象回查优先使用完整的 ulong GameObjectId。
        var actorId = gameObjectId == 0 ? 0 : unchecked((uint)(gameObjectId & uint.MaxValue));
        // 某些运行时/对象实现会额外暴露 ObjectId，但接口层不一定直接声明。
        // 单人解限、NPC 队友、信赖等场景里，ActionEffect 的 sourceId / targetId
        // 可能更接近这个 ObjectId，而不是低 32 位 GameObjectId。
        var objectId = TryGetPropertyActorId(gameObject, "ObjectId");
        var entityId = gameObject?.EntityId ?? 0;
        return new ActorIdentity(gameObjectId, actorId, objectId != 0 ? objectId : entityId, entityId);
    }

    private static ActorIdentity GetPartyMemberIdentity(Dalamud.Game.ClientState.Party.IPartyMember member, IGameObject? gameObject)
    {
        var gameObjectIdentity = GetGameObjectIdentity(gameObject);
        var objectId = member.ObjectId;
        var entityId = TryGetPropertyActorId(member, "EntityId");
        return new ActorIdentity(
            gameObjectIdentity.GameObjectId,
            gameObjectIdentity.ActorId,
            objectId,
            entityId != 0 ? entityId : gameObjectIdentity.EntityId);
    }

    private static ActorIdentity GetBuddyIdentity(IBuddyMember buddy, IGameObject? gameObject)
    {
        var gameObjectIdentity = GetGameObjectIdentity(gameObject);
        var objectId = buddy.ObjectId;
        var entityId = TryGetPropertyActorId(buddy, "EntityId");
        return new ActorIdentity(
            gameObjectIdentity.GameObjectId,
            gameObjectIdentity.ActorId,
            objectId,
            entityId != 0 ? entityId : gameObjectIdentity.EntityId);
    }

    private static IBattleChara? TryResolveBattleCharaFromIdentity(ActorIdentity identity)
    {
        if (identity.GameObjectId != 0)
        {
            var objectTableMatch = DalamudApi.ObjectTable.SearchById(identity.GameObjectId) as IBattleChara;
            if (objectTableMatch != null)
                return objectTableMatch;
        }

        var actorId = identity.ResolveActorId();
        return actorId is 0 or InvalidActorId ? null : FindObjectByActorId(actorId) as IBattleChara;
    }

    private static uint TryGetPropertyActorId(object? instance, string propertyName)
    {
        if (instance == null)
            return 0;

        try
        {
            var property = instance.GetType().GetProperty(propertyName);
            return TryConvertActorId(property?.GetValue(instance));
        }
        catch
        {
            return 0;
        }
    }

    private static uint TryConvertActorId(object? rawValue)
    {
        if (rawValue == null)
            return 0;

        try
        {
            return unchecked((uint)(Convert.ToUInt64(rawValue, CultureInfo.InvariantCulture) & uint.MaxValue));
        }
        catch
        {
            return 0;
        }
    }

    private static ActorIdentity GetLocalPlayerIdentity()
    {
        var gameObjectId = DalamudApi.GetLocalPlayerGameObjectId();
        var actorId = gameObjectId == 0 ? 0 : unchecked((uint)(gameObjectId & uint.MaxValue));
        var objectId = DalamudApi.GetLocalPlayerObjectId();
        var entityId = DalamudApi.GetLocalPlayerEntityId();
        return new ActorIdentity(gameObjectId, actorId, objectId, entityId);
    }

    private static bool TryGetLocalPlayerTrackedActor(uint actorId, out TrackedActor actor)
    {
        var identity = GetLocalPlayerIdentity();
        if (!identity.MatchesActorId(actorId))
        {
            actor = default;
            return false;
        }

        var name = DalamudApi.GetLocalPlayerName();
        if (string.IsNullOrWhiteSpace(name))
        {
            actor = default;
            return false;
        }

        var jobId = DalamudApi.GetLocalPlayerClassJobId();
        var canonicalActorId = identity.ResolveActorId();
        actor = new TrackedActor(
            canonicalActorId is 0 or InvalidActorId ? actorId : canonicalActorId,
            name.Trim(),
            jobId,
            ResolveJobName(jobId),
            TrackedActorKind.Player);
        return true;
    }

    private static bool IsFriendlyTrackedBattleNpc(IBattleChara battleChara)
    {
        if (battleChara is not IBattleNpc battleNpc)
            return false;

        var statusFlags = battleNpc.StatusFlags;
        if ((statusFlags & StatusFlags.Hostile) != 0)
            return false;

        if (ShouldResolveOwnerForObject(battleNpc) && battleNpc.OwnerId is > 0 and not InvalidActorId)
            return false;

        return HasFriendlyBattleNpcIndicators(battleNpc);
    }

    private static bool TryCreateObservedFriendlyActor(IGameObject? gameObject, out TrackedActor actor)
    {
        actor = default;
        IBattleChara? battleChara = gameObject as IBattleChara;
        if (battleChara == null && gameObject != null)
            battleChara = TryResolveBattleCharaFromIdentity(GetGameObjectIdentity(gameObject));

        if (battleChara == null)
            return TryCreateNamedFriendlyActorFromGameObject(gameObject, out actor);

        if (battleChara.ObjectKind != ObjectKind.BattleNpc)
            return TryCreateNamedFriendlyActorFromGameObject(gameObject, out actor);

        if ((battleChara.StatusFlags & StatusFlags.Hostile) != 0)
            return false;

        if (ShouldResolveOwnerForObject(battleChara) && battleChara.OwnerId is > 0 and not InvalidActorId)
            return false;

        if (battleChara is IBattleNpc battleNpc && !HasFriendlyBattleNpcIndicators(battleNpc))
            return false;

        var trackedActor = CreateTrackedActor(battleChara, ResolveBattleCharaActorId(battleChara));
        if (trackedActor == null)
            return TryCreateNamedFriendlyActorFromGameObject(gameObject, out actor);

        actor = trackedActor.Value;
        return true;
    }

    private static bool TryCreateObservedFriendlyActor(uint actorId, string? name, out TrackedActor actor)
    {
        actor = default;
        if (actorId is 0 or InvalidActorId)
            return false;

        var normalizedName = name?.Trim();
        if (!LooksLikeDutyCompanionName(normalizedName))
            return false;

        actor = new TrackedActor(actorId, normalizedName!, 0, string.Empty, TrackedActorKind.FriendlyNpc);
        return true;
    }

    private static bool TryCreateNamedFriendlyActorFromGameObject(IGameObject? gameObject, out TrackedActor actor)
    {
        actor = default;
        if (gameObject == null)
            return false;

        var name = gameObject.Name.TextValue?.Trim();
        if (!LooksLikeDutyCompanionName(name))
            return false;

        var actorId = GetGameObjectIdentity(gameObject).ResolveActorId();
        if (actorId is 0 or InvalidActorId)
            return false;

        actor = new TrackedActor(actorId, name!, 0, string.Empty, TrackedActorKind.FriendlyNpc);
        return true;
    }

    private static bool LooksLikeDutyCompanionName(string? name)
        => !string.IsNullOrWhiteSpace(name)
           && name!.EndsWith("的幻体", StringComparison.Ordinal);

    private static bool HasFriendlyBattleNpcIndicators(IBattleNpc battleNpc)
    {
        var statusFlags = battleNpc.StatusFlags;
        if ((statusFlags & (StatusFlags.PartyMember | StatusFlags.Friend)) != 0)
            return true;

        if (IsDutyNpcPartyMemberKind(battleNpc))
            return true;

        var name = battleNpc.Name.TextValue?.Trim();
        return LooksLikeDutyCompanionName(name);
    }

    private static bool ShouldResolveOwnerForObject(IGameObject? gameObject)
    {
        if (gameObject is not IBattleNpc battleNpc)
            return gameObject != null;

        var kindName = battleNpc.BattleNpcKind.ToString();
        return string.Equals(kindName, "Pet", StringComparison.Ordinal)
               || string.Equals(kindName, "Buddy", StringComparison.Ordinal)
               || string.Equals(kindName, "RaceChocobo", StringComparison.Ordinal);
    }

    private static bool IsDutyNpcPartyMemberKind(IBattleNpc battleNpc)
        => string.Equals(battleNpc.BattleNpcKind.ToString(), "NpcPartyMember", StringComparison.Ordinal);

    private bool AreAllPartyMembersOutOfCombat(bool fallbackInCombat)
    {
        var hasUsablePartyState = false;

        foreach (var character in EnumerateTrackedPartyBattleCharas())
        {
            if (!ShouldCountBattleCharaForCombatEnd(character))
                continue;

            hasUsablePartyState = true;
            if ((character.StatusFlags & StatusFlags.InCombat) != 0)
                return false;
        }

        return hasUsablePartyState
            ? true
            : !fallbackInCombat;
    }

    private bool ShouldCountBattleCharaForCombatEnd(IBattleChara battleChara)
    {
        var actorId = ResolveBattleCharaActorId(battleChara);
        if (actorId != 0)
        {
            if (TryGetLocalPlayerTrackedActor(actorId, out _))
                return true;

            if (TryGetPartyMemberTrackedActor(actorId, out _))
                return true;

            if (TryGetBuddyTrackedActor(actorId, out _))
                return true;
        }


        if (battleChara is not IBattleNpc battleNpc)
            return true;

        if (IsDutyNpcPartyMemberKind(battleNpc))
            return true;

        return actorId != 0 && observedFriendlyActorCache.ContainsKey(actorId);
    }

    private static string NormalizeZoneName(string? zoneName)
        => string.IsNullOrWhiteSpace(zoneName) ? "未知区域" : zoneName.Trim();

    private void AppendEncounterStartIfNeededLocked(bool wasStarted, DateTime timeUtc)
    {
        if (wasStarted || !currentEncounter.Started)
            return;

        AppendCombatTimelineEntryLocked(timeUtc, CombatTimelineEntryKind.CombatStart, $"进入战斗：{currentEncounter.ZoneName}");
    }

    private void AppendCombatTimelineEntryLocked(
        DateTime timeUtc,
        CombatTimelineEntryKind kind,
        string message,
        string? actorName = null,
        string? targetName = null,
        bool actorIsFriendly = false,
        bool targetIsFriendly = false,
        string? actionText = null)
    {
        combatTimelineEntries.Add(new CombatTimelineEntry(
            timeUtc.ToLocalTime(),
            kind,
            message,
            actorName,
            targetName,
            actorIsFriendly,
            targetIsFriendly,
            actionText));
        TrimCombatTimelineEntriesLocked();
    }

    private void TrimCombatTimelineEntriesLocked()
    {
        var maxEntryCount = config.CombatTimelineMaxEntries <= 0
            ? 0
            : Math.Clamp(config.CombatTimelineMaxEntries, 100, 50000);
        if (maxEntryCount == 0)
            return;

        if (combatTimelineEntries.Count > maxEntryCount)
            combatTimelineEntries.RemoveRange(0, combatTimelineEntries.Count - maxEntryCount);
    }

    private string ResolveCombatTimelineSourceName(uint actorId, DateTime nowUtc)
    {
        if (TryGetTrackedActor(actorId, out var trackedActor))
            return trackedActor.Name;

        var obj = FindObjectByActorId(actorId);
        var objectName = obj?.Name.TextValue?.Trim();
        if (!string.IsNullOrWhiteSpace(objectName))
            return objectName;

        return TryResolveTrackedSource(actorId, nowUtc, out trackedActor)
            ? trackedActor.Name
            : BuildUnknownActorName(actorId, "未知来源");
    }

    private string ResolveCombatTimelineTargetName(uint actorId, DateTime nowUtc)
    {
        _ = nowUtc;

        if (TryGetTrackedActor(actorId, out var trackedActor))
            return trackedActor.Name;

        var obj = FindObjectByActorId(actorId);
        var objectName = obj?.Name.TextValue?.Trim();
        if (!string.IsNullOrWhiteSpace(objectName))
            return objectName;

        return BuildUnknownActorName(actorId, "未知目标");
    }

    private static string NormalizeActionName(string? actionName)
        => string.IsNullOrWhiteSpace(actionName) ? "未知技能" : actionName.Trim();

    private static string FormatCriticalSuffix(bool critical)
        => critical ? "（暴击）" : string.Empty;

    private static string FormatSimulatedCriticalSuffix(bool critical)
        => critical ? "（模拟，暴击）" : "（模拟）";

    private static string BuildUnknownActorName(uint actorId, string fallbackLabel)
        => actorId is 0 or InvalidActorId ? fallbackLabel : $"{fallbackLabel}(0x{actorId:X8})";

    private void UpdateStatusText(DateTime nowUtc)
    {
        if (HasSelectedHistoricalPreviewLocked())
        {
            var selected = historicalRecords[selectedHistoricalRecordIndex];
            if (historicalPreviewExpiresAtUtc.HasValue && nowUtc < historicalPreviewExpiresAtUtc.Value)
            {
                StatusText = $"预览历史记录: {selected.ZoneName} {selected.Duration}（剩余 {GetHistoricalPreviewRemainingSeconds(nowUtc)} 秒）";
                return;
            }

            StatusText = $"预览历史记录: {selected.ZoneName} {selected.Duration}（未进入战斗，预览无限）";
            return;
        }

        if (currentEncounter.Started)
        {
            StatusText = $"战斗中: {currentEncounter.ZoneName} {ActxSnapshotFormatter.FormatDuration(currentEncounter.DurationSeconds)}";
            return;
        }

        if (latestInCombatHint && suppressStaleDisplayUntilNextCombatStart)
        {
            StatusText = "已进入战斗，正在收集新战斗数据...";
            return;
        }

        if (DisplayCombatData?.Msg?.Encounter != null)
        {
            StatusText = "上一场战斗已结束，等待下一场战斗...";
            return;
        }

        StatusText = "等待战斗数据...";
    }

    private static bool HasSameHistoryIdentity(HistoricalCombatData left, HistoricalCombatData right)
    {
        if (left.StartTimeUtc.HasValue
            && right.StartTimeUtc.HasValue
            && left.EndTimeUtc.HasValue
            && right.EndTimeUtc.HasValue)
        {
            return string.Equals(left.ZoneName, right.ZoneName, StringComparison.Ordinal)
                   && left.StartTimeUtc.Value == right.StartTimeUtc.Value
                   && left.EndTimeUtc.Value == right.EndTimeUtc.Value;
        }

        return string.Equals(left.ZoneName, right.ZoneName, StringComparison.Ordinal)
               && string.Equals(left.Duration, right.Duration, StringComparison.Ordinal);
    }

    private void UpsertHistoricalRecord(HistoricalCombatData record)
    {
        for (var i = 0; i < historicalRecords.Count; i++)
        {
            if (!HasSameHistoryIdentity(historicalRecords[i], record))
                continue;

            historicalRecords[i] = record;
            return;
        }

        historicalRecords.Add(record);
    }

    private bool TrySelectLatestHistoricalRecord()
    {
        if (historicalRecords.Count == 0)
            return false;

        return PreviewHistoricalRecordLocked(historicalRecords.Count - 1, DateTime.UtcNow);
    }

    private void SortHistoricalRecords()
        => historicalRecords.Sort(static (left, right) =>
        {
            var timeComparison = Nullable.Compare(GetHistorySortTime(left), GetHistorySortTime(right));
            if (timeComparison != 0)
                return timeComparison;

            var zoneComparison = string.Compare(left.ZoneName, right.ZoneName, StringComparison.Ordinal);
            if (zoneComparison != 0)
                return zoneComparison;

            return string.Compare(left.Duration, right.Duration, StringComparison.Ordinal);
        });

    private static DateTime? GetHistorySortTime(HistoricalCombatData record)
        => record.EndTimeUtc ?? record.StartTimeUtc;

    private static HistoricalCombatData CreateHistoricalRecord(
        CombatDataWrapper snapshot,
        DateTime? startTimeUtc = null,
        DateTime? endTimeUtc = null)
    {
        var encounter = snapshot.Msg?.Encounter;
        return new HistoricalCombatData(
            encounter?.CurrentZoneName ?? "未知区域",
            encounter?.DurationText ?? "00:00",
            snapshot,
            startTimeUtc,
            endTimeUtc);
    }

    private static HistoricalCombatData CreateSyntheticHistoricalRecord(CombatDataWrapper snapshot, DateTime endTimeUtc)
    {
        var duration = ParseDurationText(snapshot.Msg?.Encounter?.DurationText);
        var startTimeUtc = endTimeUtc - duration;
        return CreateHistoricalRecord(snapshot, startTimeUtc, endTimeUtc);
    }

    private static TimeSpan ParseDurationText(string? durationText)
    {
        if (string.IsNullOrWhiteSpace(durationText))
            return TimeSpan.FromSeconds(1);

        return TimeSpan.TryParseExact(
                durationText.Trim(),
                new[] { @"hh\:mm\:ss", @"mm\:ss" },
                CultureInfo.InvariantCulture,
                out var parsed)
            ? parsed
            : TimeSpan.FromSeconds(1);
    }

    private static List<HistoricalCombatData> DeserializeHistoricalRecords(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<HistoricalCombatData>();

        try
        {
            var payload = JsonSerializer.Deserialize<HistoricalRecordsExportPayload>(json, HistoryJsonOptions);
            if (payload?.Records != null)
                return payload.Records;
        }
        catch
        {
            // Fall back to direct array deserialization for compatibility.
        }

        return JsonSerializer.Deserialize<List<HistoricalCombatData>>(json, HistoryJsonOptions)
               ?? new List<HistoricalCombatData>();
    }

    private static bool IsValidHistoricalRecord(HistoricalCombatData record)
        => !string.IsNullOrWhiteSpace(record.ZoneName)
           && record.Snapshot?.Msg?.Encounter != null
           && record.Snapshot.Msg.Combatant.Count > 0;

    private static string ResolveJobName(uint jobId)
    {
        return jobId switch
        {
            1 => "剑术师",
            2 => "格斗家",
            3 => "斧术师",
            4 => "枪术师",
            5 => "弓箭手",
            6 => "幻术师",
            7 => "咒术师",
            19 => "骑士",
            20 => "武僧",
            21 => "战士",
            22 => "龙骑士",
            23 => "吟游诗人",
            24 => "白魔法师",
            25 => "黑魔法师",
            26 => "秘术师",
            27 => "召唤师",
            28 => "学者",
            29 => "双剑师",
            30 => "忍者",
            31 => "机工士",
            32 => "暗黑骑士",
            33 => "占星术士",
            34 => "武士",
            35 => "赤魔法师",
            37 => "绝枪战士",
            38 => "舞者",
            39 => "钐镰客",
            40 => "贤者",
            41 => "蝰蛇剑士",
            42 => "绘灵法师",
            43 => "青魔法师",
            _ => string.Empty,
        };
    }

    private static CombatDataWrapper BuildRaidTestCombatData()
    {
        var combatants = new Dictionary<string, Combatant>(StringComparer.Ordinal)
        {
            ["测试骑士#E0000001"] = CreateTestCombatant(
                name: "测试骑士",
                job: "骑士",
                damagePercentText: "19%",
                damageText: "98.00万",
                encDpsText: "2121",
                encHpsText: "145",
                dtpsText: "964",
                maxHitText: "圣灵-4.82万",
                hitsText: "168",
                critHitsText: "42",
                toHitText: "95.5",
                damageTakenText: "44.54万",
                deathsText: "0"),
            ["古·拉哈·提亚#E0000002"] = CreateTestCombatant(
                name: "古·拉哈·提亚",
                job: "武士",
                damagePercentText: "29%",
                damageText: "145.00万",
                encDpsText: "3139",
                encHpsText: "0",
                dtpsText: "221",
                maxHitText: "照破-9.68万",
                hitsText: "214",
                critHitsText: "63",
                toHitText: "97.3",
                damageTakenText: "10.21万",
                deathsText: "1"),
            ["阿莉塞#E0000003"] = CreateTestCombatant(
                name: "阿莉塞",
                job: "赤魔法师",
                damagePercentText: "26%",
                damageText: "131.00万",
                encDpsText: "2837",
                encHpsText: "38",
                dtpsText: "172",
                maxHitText: "决断-8.84万",
                hitsText: "196",
                critHitsText: "57",
                toHitText: "96.0",
                damageTakenText: "7.96万",
                deathsText: "0"),
            ["阿尔菲诺#E0000004"] = CreateTestCombatant(
                name: "阿尔菲诺",
                job: "贤者",
                damagePercentText: "8%",
                damageText: "42.00万",
                encDpsText: "909",
                encHpsText: "2514",
                dtpsText: "145",
                maxHitText: "注药-3.12万",
                hitsText: "88",
                critHitsText: "18",
                toHitText: "94.6",
                damageTakenText: "6.71万",
                deathsText: "0"),
            ["于里昂热#E0000005"] = CreateTestCombatant(
                name: "于里昂热",
                job: "占星术士",
                damagePercentText: "6%",
                damageText: "31.00万",
                encDpsText: "671",
                encHpsText: "1898",
                dtpsText: "131",
                maxHitText: "重力-2.69万",
                hitsText: "75",
                critHitsText: "14",
                toHitText: "93.8",
                damageTakenText: "6.08万",
                deathsText: "0"),
            ["机工支援兵#E0000006"] = CreateTestCombatant(
                name: "机工支援兵",
                job: "机工士",
                damagePercentText: "10%",
                damageText: "55.00万",
                encDpsText: "1190",
                encHpsText: "0",
                dtpsText: "84",
                maxHitText: "钻头-5.40万",
                hitsText: "102",
                critHitsText: "24",
                toHitText: "95.1",
                damageTakenText: "3.87万",
                deathsText: "0"),
        };

        return BuildTestCombatData(
            zoneName: "零式测试场",
            durationText: "07:42",
            damageText: "502.00万",
            encDpsText: "10867",
            hitsText: "843",
            hitFailedText: "31",
            critHitsText: "218",
            critHitPercentText: "25%",
            maxHitText: "古·拉哈·提亚-照破-9.68万",
            maxHitValueText: "古·拉哈·提亚-9.7万",
            damageTakenText: "79.37万",
            combatants: combatants);
    }

    private static CombatDataWrapper BuildRaidEightPlayerTestCombatData()
    {
        var combatants = new Dictionary<string, Combatant>(StringComparer.Ordinal)
        {
            ["测试骑士#E0000001"] = CreateTestCombatant(
                name: "测试骑士",
                job: "骑士",
                damagePercentText: "17%",
                damageText: "98.00万",
                encDpsText: "2121",
                encHpsText: "145",
                dtpsText: "964",
                maxHitText: "圣灵-4.82万",
                hitsText: "168",
                critHitsText: "42",
                toHitText: "95.5",
                damageTakenText: "44.54万",
                deathsText: "0"),
            ["古·拉哈·提亚#E0000002"] = CreateTestCombatant(
                name: "古·拉哈·提亚",
                job: "武士",
                damagePercentText: "26%",
                damageText: "145.00万",
                encDpsText: "3139",
                encHpsText: "0",
                dtpsText: "221",
                maxHitText: "照破-9.68万",
                hitsText: "214",
                critHitsText: "63",
                toHitText: "97.3",
                damageTakenText: "10.21万",
                deathsText: "1"),
            ["阿莉塞#E0000003"] = CreateTestCombatant(
                name: "阿莉塞",
                job: "赤魔法师",
                damagePercentText: "23%",
                damageText: "131.00万",
                encDpsText: "2837",
                encHpsText: "38",
                dtpsText: "172",
                maxHitText: "决断-8.84万",
                hitsText: "196",
                critHitsText: "57",
                toHitText: "96.0",
                damageTakenText: "7.96万",
                deathsText: "0"),
            ["阿尔菲诺#E0000004"] = CreateTestCombatant(
                name: "阿尔菲诺",
                job: "贤者",
                damagePercentText: "7%",
                damageText: "42.00万",
                encDpsText: "909",
                encHpsText: "2514",
                dtpsText: "145",
                maxHitText: "注药-3.12万",
                hitsText: "88",
                critHitsText: "18",
                toHitText: "94.6",
                damageTakenText: "6.71万",
                deathsText: "0"),
            ["于里昂热#E0000005"] = CreateTestCombatant(
                name: "于里昂热",
                job: "占星术士",
                damagePercentText: "5%",
                damageText: "31.00万",
                encDpsText: "671",
                encHpsText: "1898",
                dtpsText: "131",
                maxHitText: "重力-2.69万",
                hitsText: "75",
                critHitsText: "14",
                toHitText: "93.8",
                damageTakenText: "6.08万",
                deathsText: "0"),
            ["机工支援兵#E0000006"] = CreateTestCombatant(
                name: "机工支援兵",
                job: "机工士",
                damagePercentText: "10%",
                damageText: "55.00万",
                encDpsText: "1190",
                encHpsText: "0",
                dtpsText: "84",
                maxHitText: "钻头-5.40万",
                hitsText: "102",
                critHitsText: "24",
                toHitText: "95.1",
                damageTakenText: "3.87万",
                deathsText: "0"),
            ["雅·修特拉#E0000007"] = CreateTestCombatant(
                name: "雅·修特拉",
                job: "黑魔法师",
                damagePercentText: "6%",
                damageText: "36.00万",
                encDpsText: "779",
                encHpsText: "0",
                dtpsText: "57",
                maxHitText: "耀星-4.11万",
                hitsText: "66",
                critHitsText: "15",
                toHitText: "94.2",
                damageTakenText: "2.61万",
                deathsText: "0"),
            ["爱梅特赛尔克#E0000008"] = CreateTestCombatant(
                name: "爱梅特赛尔克",
                job: "召唤师",
                damagePercentText: "5%",
                damageText: "27.00万",
                encDpsText: "584",
                encHpsText: "22",
                dtpsText: "31",
                maxHitText: "灵泉之炎-3.48万",
                hitsText: "59",
                critHitsText: "13",
                toHitText: "95.0",
                damageTakenText: "1.44万",
                deathsText: "0"),
        };

        return BuildTestCombatData(
            zoneName: "零式测试场",
            durationText: "07:42",
            damageText: "565.00万",
            encDpsText: "12230",
            hitsText: "968",
            hitFailedText: "36",
            critHitsText: "246",
            critHitPercentText: "25%",
            maxHitText: "古·拉哈·提亚-照破-9.68万",
            maxHitValueText: "古·拉哈·提亚-9.7万",
            damageTakenText: "83.42万",
            combatants: combatants);
    }

    private static CombatDataWrapper BuildTrialTestCombatData()
    {
        var combatants = new Dictionary<string, Combatant>(StringComparer.Ordinal)
        {
            ["测试骑士#E0000001"] = CreateTestCombatant(
                name: "测试骑士",
                job: "骑士",
                damagePercentText: "17%",
                damageText: "118.60万",
                encDpsText: "2274",
                encHpsText: "182",
                dtpsText: "1178",
                maxHitText: "赎罪剑-6.24万",
                hitsText: "181",
                critHitsText: "39",
                toHitText: "96.8",
                damageTakenText: "61.44万",
                deathsText: "0"),
            ["古·拉哈·提亚#E0000002"] = CreateTestCombatant(
                name: "古·拉哈·提亚",
                job: "武士",
                damagePercentText: "28%",
                damageText: "191.20万",
                encDpsText: "3666",
                encHpsText: "0",
                dtpsText: "264",
                maxHitText: "雪月花-12.46万",
                hitsText: "243",
                critHitsText: "79",
                toHitText: "97.5",
                damageTakenText: "13.78万",
                deathsText: "0"),
            ["阿莉塞#E0000003"] = CreateTestCombatant(
                name: "阿莉塞",
                job: "赤魔法师",
                damagePercentText: "24%",
                damageText: "163.80万",
                encDpsText: "3141",
                encHpsText: "41",
                dtpsText: "238",
                maxHitText: "决断-10.38万",
                hitsText: "221",
                critHitsText: "66",
                toHitText: "96.3",
                damageTakenText: "12.41万",
                deathsText: "1"),
            ["阿尔菲诺#E0000004"] = CreateTestCombatant(
                name: "阿尔菲诺",
                job: "贤者",
                damagePercentText: "11%",
                damageText: "77.50万",
                encDpsText: "1486",
                encHpsText: "2988",
                dtpsText: "189",
                maxHitText: "注药-4.18万",
                hitsText: "108",
                critHitsText: "23",
                toHitText: "95.6",
                damageTakenText: "9.89万",
                deathsText: "0"),
            ["于里昂热#E0000005"] = CreateTestCombatant(
                name: "于里昂热",
                job: "占星术士",
                damagePercentText: "8%",
                damageText: "56.30万",
                encDpsText: "1080",
                encHpsText: "2473",
                dtpsText: "153",
                maxHitText: "重力-3.31万",
                hitsText: "92",
                critHitsText: "19",
                toHitText: "95.1",
                damageTakenText: "8.02万",
                deathsText: "0"),
            ["机工支援兵#E0000006"] = CreateTestCombatant(
                name: "机工支援兵",
                job: "机工士",
                damagePercentText: "12%",
                damageText: "81.10万",
                encDpsText: "1555",
                encHpsText: "0",
                dtpsText: "141",
                maxHitText: "空气锚-5.77万",
                hitsText: "126",
                critHitsText: "31",
                toHitText: "96.0",
                damageTakenText: "7.37万",
                deathsText: "0"),
        };

        return BuildTestCombatData(
            zoneName: "极神兵破坏作战",
            durationText: "08:41",
            damageText: "688.50万",
            encDpsText: "13215",
            hitsText: "971",
            hitFailedText: "22",
            critHitsText: "257",
            critHitPercentText: "26%",
            maxHitText: "古·拉哈·提亚-雪月花-12.46万",
            maxHitValueText: "古·拉哈·提亚-12.5万",
            damageTakenText: "112.91万",
            combatants: combatants);
    }

    private static CombatDataWrapper BuildTrainingTestCombatData()
    {
        var combatants = new Dictionary<string, Combatant>(StringComparer.Ordinal)
        {
            ["测试骑士#E0000001"] = CreateTestCombatant(
                name: "测试骑士",
                job: "骑士",
                damagePercentText: "61%",
                damageText: "64.80万",
                encDpsText: "3375",
                encHpsText: "0",
                dtpsText: "201",
                maxHitText: "圣灵-7.21万",
                hitsText: "116",
                critHitsText: "31",
                toHitText: "98.2",
                damageTakenText: "3.86万",
                deathsText: "0"),
            ["机工支援兵#E0000006"] = CreateTestCombatant(
                name: "机工支援兵",
                job: "机工士",
                damagePercentText: "26%",
                damageText: "27.40万",
                encDpsText: "1427",
                encHpsText: "0",
                dtpsText: "96",
                maxHitText: "钻头-4.88万",
                hitsText: "74",
                critHitsText: "18",
                toHitText: "97.0",
                damageTakenText: "1.84万",
                deathsText: "0"),
            ["阿尔菲诺#E0000004"] = CreateTestCombatant(
                name: "阿尔菲诺",
                job: "贤者",
                damagePercentText: "13%",
                damageText: "13.60万",
                encDpsText: "708",
                encHpsText: "642",
                dtpsText: "318",
                maxHitText: "注药-2.42万",
                hitsText: "41",
                critHitsText: "9",
                toHitText: "95.3",
                damageTakenText: "6.12万",
                deathsText: "0"),
        };

        return BuildTestCombatData(
            zoneName: "木人演练场",
            durationText: "03:12",
            damageText: "105.80万",
            encDpsText: "5510",
            hitsText: "231",
            hitFailedText: "6",
            critHitsText: "58",
            critHitPercentText: "25%",
            maxHitText: "测试骑士-圣灵-7.21万",
            maxHitValueText: "测试骑士-7.2万",
            damageTakenText: "11.82万",
            combatants: combatants);
    }

    private static CombatDataWrapper BuildTestCombatData(
        string zoneName,
        string durationText,
        string damageText,
        string encDpsText,
        string hitsText,
        string hitFailedText,
        string critHitsText,
        string critHitPercentText,
        string maxHitText,
        string maxHitValueText,
        string damageTakenText,
        Dictionary<string, Combatant> combatants)
    {
        PopulateDerivedTestCombatantMetrics(combatants, durationText);

        return new CombatDataWrapper
        {
            Type = "broadcast",
            MsgType = "CombatData",
            Msg = new CombatData
            {
                Type = "CombatData",
                IsActive = "false",
                Encounter = new Encounter
                {
                    CurrentZoneName = zoneName,
                    DurationText = durationText,
                    DamageText = damageText,
                    EncDpsText = encDpsText,
                    HitsText = hitsText,
                    HitFailedText = hitFailedText,
                    CritHitsText = critHitsText,
                    CritHitPercentText = critHitPercentText,
                    MaxHitText = maxHitText,
                    MaxHitValueText = maxHitValueText,
                    DamageTakenText = damageTakenText,
                },
                Combatant = combatants,
            },
        };
    }

    private static void PopulateDerivedTestCombatantMetrics(
        Dictionary<string, Combatant> combatants,
        string durationText)
    {
        var durationSeconds = ParseDurationTextToSeconds(durationText);
        if (durationSeconds <= 0d)
            return;

        foreach (var combatant in combatants.Values)
        {
            if (!string.IsNullOrWhiteSpace(combatant.HealedText))
                continue;

            if (!double.TryParse(combatant.EncHpsText, NumberStyles.Float, CultureInfo.InvariantCulture, out var hps)
                || hps <= 0d)
            {
                continue;
            }

            var totalHealed = (long)Math.Round(hps * durationSeconds, MidpointRounding.AwayFromZero);
            combatant.HealedText = CreateDamageString(totalHealed, useSuffix: true, useDecimals: true);
        }
    }

    private static double ParseDurationTextToSeconds(string? durationText)
    {
        if (string.IsNullOrWhiteSpace(durationText))
            return 0d;

        return TimeSpan.TryParseExact(durationText, @"mm\:ss", CultureInfo.InvariantCulture, out var mmss)
            ? mmss.TotalSeconds
            : TimeSpan.TryParseExact(durationText, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var hhmmss)
                ? hhmmss.TotalSeconds
                : 0d;
    }

    private static Combatant CreateTestCombatant(
        string name,
        string job,
        string damagePercentText,
        string damageText,
        string encDpsText,
        string encHpsText,
        string dtpsText,
        string maxHitText,
        string hitsText,
        string critHitsText,
        string toHitText,
        string damageTakenText,
        string deathsText,
        string? healedText = null)
    {
        return new Combatant
        {
            Name = name,
            Job = job,
            DamagePercentText = damagePercentText,
            DamageText = damageText,
            EncDpsText = encDpsText,
            EncHpsText = encHpsText,
            HealedText = healedText,
            DtpsText = dtpsText,
            MaxHitText = maxHitText,
            HitsText = hitsText,
            CritHitsText = critHitsText,
            CritDirectHitsText = "0",
            ToHitText = toHitText,
            DamageTakenText = damageTakenText,
            BlockPctText = "--",
            ParryPctText = "--",
            DeathsText = deathsText,
        };
    }

    private sealed class HistoricalRecordsExportPayload
    {
        public int Version { get; set; }

        public DateTime ExportedAtUtc { get; set; }

        public List<HistoricalCombatData> Records { get; set; } = new();
    }

    public sealed record CombatTimelineEntry(
        DateTime TimestampLocal,
        CombatTimelineEntryKind Kind,
        string Message,
        string? ActorName,
        string? TargetName,
        bool ActorIsFriendly,
        bool TargetIsFriendly,
        string? ActionText = null);

    public enum CombatTimelineEntryKind
    {
        CombatStart,
        Damage,
        Heal,
        Failure,
        Death,
        CombatEnd,
    }

    private readonly record struct ActorIdentity(ulong GameObjectId, uint ActorId, uint ObjectId, uint EntityId)
    {
        public uint ResolveActorId()
        {
            if (ActorId > 0 && ActorId != InvalidActorId)
                return ActorId;

            if (ObjectId > 0 && ObjectId != InvalidActorId)
                return ObjectId;

            if (EntityId > 0 && EntityId != InvalidActorId)
                return EntityId;

            return 0;
        }

        public bool MatchesActorId(uint actorId)
        {
            if (actorId is 0 or InvalidActorId)
                return false;

            return (ActorId > 0 && ActorId != InvalidActorId && ActorId == actorId)
                   || (ObjectId > 0 && ObjectId != InvalidActorId && ObjectId == actorId)
                   || (EntityId > 0 && EntityId != InvalidActorId && EntityId == actorId);
        }
    }

    private readonly record struct OwnerCacheEntry(uint OwnerId, DateTime UpdatedAtUtc);

    private sealed class RecentHostilePlayerAction
    {
        public RecentHostilePlayerAction(
            TrackedActor source,
            uint targetActorId,
            uint actionId,
            string actionName,
            DateTime observedAtUtc)
        {
            Source = source;
            TargetActorId = targetActorId;
            ActionId = actionId;
            ActionName = actionName;
            ObservedAtUtc = observedAtUtc;
        }

        public TrackedActor Source { get; }

        public uint TargetActorId { get; }

        public uint ActionId { get; }

        public string ActionName { get; }

        public DateTime ObservedAtUtc { get; }

        public long ObservedDamageAmount { get; set; }

        public bool? ObservedCritical { get; set; }

        public bool? ObservedDirectHit { get; set; }
    }

    private readonly record struct PlayerDotKey(uint TargetActorId, uint SourceActorId, uint StatusId);

    private enum TrackedActorKind
    {
        Unknown,
        Player,
        FriendlyNpc,
        HostileNpc,
    }

    private readonly record struct TrackedActor(uint ActorId, string Name, uint JobId, string JobName, TrackedActorKind Kind);

    private sealed class ActivePlayerDotState
    {
        public ActivePlayerDotState(
            PlayerDotKey key,
            TrackedActor source,
            uint actionId,
            string actionName,
            string statusName,
            int statusPotency,
            PlayerDotSkillEntry? skillEntry,
            long estimatedTickDamage,
            bool estimatedTickDamageFromObservedSeed,
            DateTime firstSeenUtc,
            DateTime lastSeenUtc,
            float remainingTimeSeconds)
        {
            Key = key;
            Source = source;
            ActionId = actionId;
            ActionName = actionName;
            StatusName = statusName;
            StatusPotency = statusPotency;
            SkillEntry = skillEntry;
            EstimatedTickDamage = estimatedTickDamage;
            EstimatedTickDamageFromObservedSeed = estimatedTickDamageFromObservedSeed;
            FirstSeenUtc = firstSeenUtc;
            LastSeenUtc = lastSeenUtc;
            RemainingTimeSeconds = remainingTimeSeconds;
            LastAttributedTickUtc = firstSeenUtc;
            NextTickRemainingTimeSeconds = Math.Max(0f, remainingTimeSeconds - (float)PlayerDotTickInterval.TotalSeconds);
        }

        public PlayerDotKey Key { get; }

        public TrackedActor Source { get; }

        public uint ActionId { get; }

        public string ActionName { get; set; }

        public string StatusName { get; set; }

        public int StatusPotency { get; }

        public PlayerDotSkillEntry? SkillEntry { get; set; }

        public long EstimatedTickDamage { get; set; }

        public bool EstimatedTickDamageFromObservedSeed { get; set; }

        public DateTime FirstSeenUtc { get; }

        public DateTime LastSeenUtc { get; set; }

        public float RemainingTimeSeconds { get; set; }

        public DateTime LastAttributedTickUtc { get; set; }

        public int TickCount { get; set; }

        public float NextTickRemainingTimeSeconds { get; set; }
    }

    private sealed class EncounterSession
    {
        private readonly Dictionary<uint, CombatantSession> combatants = new();

        public DateTime StartUtc { get; private set; }

        public DateTime LastEventUtc { get; private set; }

        public DateTime EndUtc { get; private set; }

        public string ZoneName { get; set; } = "未知区域";

        public bool Started => StartUtc != default;

        public bool HasMeaningfulData => combatants.Values.Any(static combatant =>
            combatant.Damage > 0
            || combatant.Healed > 0
            || combatant.DamageTaken > 0
            || combatant.HealsTaken > 0
            || combatant.Deaths > 0
            || combatant.Swings > 0
            || combatant.Heals > 0);

        public IReadOnlyCollection<CombatantSession> Combatants => combatants.Values;

        public double DurationSeconds
        {
            get
            {
                if (!Started)
                    return 1d;

                var endUtc = EndUtc == default ? LastEventUtc : EndUtc;
                var seconds = (endUtc - StartUtc).TotalSeconds;
                return seconds < 1d ? 1d : seconds;
            }
        }

        public void MarkActivity(DateTime timeUtc)
        {
            if (!Started)
                StartUtc = timeUtc;

            if (LastEventUtc < timeUtc)
                LastEventUtc = timeUtc;

            if (EndUtc < timeUtc)
                EndUtc = timeUtc;
        }

        public void RecordOutgoingDamage(
            TrackedActor source,
            string actionName,
            long amount,
            bool critical,
            bool directHit,
            DateTime timeUtc,
            bool isDotDamage = false)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(source).NoteOutgoingDamage(actionName, amount, critical, directHit, timeUtc, isDotDamage);
        }

        public void RecordIncomingDamage(TrackedActor target, long amount, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(target).NoteIncomingDamage(amount, timeUtc);
        }

        public void RecordOutgoingHeal(TrackedActor source, long amount, bool critical, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(source).NoteOutgoingHeal(amount, critical, timeUtc);
        }

        public void RecordIncomingHeal(TrackedActor target, long amount, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(target).NoteIncomingHeal(amount, timeUtc);
        }

        public void RecordFailedSwing(TrackedActor source, bool isMiss, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(source).NoteFailedSwing(isMiss, timeUtc);
        }

        public void RecordDeath(TrackedActor target, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(target).NoteDeath(timeUtc);
        }

        private CombatantSession EnsureCombatant(TrackedActor actor)
        {
            if (combatants.TryGetValue(actor.ActorId, out var existing))
            {
                existing.RefreshIdentity(actor);
                return existing;
            }

            var created = new CombatantSession(actor);
            combatants[actor.ActorId] = created;
            return created;
        }
    }

    private sealed class CombatantSession
    {
        public CombatantSession(TrackedActor actor)
        {
            ActorId = actor.ActorId;
            Name = actor.Name;
            JobId = actor.JobId;
            JobName = actor.JobName;
            Kind = actor.Kind;
        }

        public uint ActorId { get; }

        public string Name { get; private set; }

        public uint JobId { get; private set; }

        public string JobName { get; private set; }

        public TrackedActorKind Kind { get; private set; }

        public long Damage { get; private set; }

        public long Healed { get; private set; }

        public long DamageTaken { get; private set; }

        public long DotDamage { get; private set; }

        public long HealsTaken { get; private set; }

        public int Swings { get; private set; }

        public int Hits { get; private set; }

        public int CritHits { get; private set; }

        public int CritDirectHits { get; private set; }

        public int Misses { get; private set; }

        public int HitFailed { get; private set; }

        public int Heals { get; private set; }

        public int CritHeals { get; private set; }

        public int Deaths { get; private set; }

        public DateTime FirstEventUtc { get; private set; }

        public DateTime LastEventUtc { get; private set; }

        public long MaxHitValue { get; private set; }

        public string MaxHitActionName { get; private set; } = string.Empty;

        public double PersonalDurationSeconds
        {
            get
            {
                if (FirstEventUtc == default || LastEventUtc <= FirstEventUtc)
                    return 1d;

                var seconds = (LastEventUtc - FirstEventUtc).TotalSeconds;
                return seconds < 1d ? 1d : seconds;
            }
        }

        public void RefreshIdentity(TrackedActor actor)
        {
            if (!string.IsNullOrWhiteSpace(actor.Name))
                Name = actor.Name;

            if (actor.JobId != 0)
                JobId = actor.JobId;

            if (!string.IsNullOrWhiteSpace(actor.JobName))
                JobName = actor.JobName;

            if (actor.Kind != TrackedActorKind.Unknown)
                Kind = actor.Kind;
        }

        public void NoteOutgoingDamage(string actionName, long amount, bool critical, bool directHit, DateTime timeUtc, bool isDotDamage)
        {
            Touch(timeUtc);
            Damage += amount;
            if (isDotDamage)
                DotDamage += amount;
            Swings++;
            Hits++;
            if (critical)
                CritHits++;
            if (critical && directHit)
                CritDirectHits++;

            if (amount > MaxHitValue)
            {
                MaxHitValue = amount;
                MaxHitActionName = actionName;
            }
        }

        public void NoteIncomingDamage(long amount, DateTime timeUtc)
        {
            Touch(timeUtc);
            DamageTaken += amount;
        }

        public void NoteOutgoingHeal(long amount, bool critical, DateTime timeUtc)
        {
            Touch(timeUtc);
            Healed += amount;
            Heals++;
            if (critical)
                CritHeals++;
        }

        public void NoteIncomingHeal(long amount, DateTime timeUtc)
        {
            Touch(timeUtc);
            HealsTaken += amount;
        }

        public void NoteFailedSwing(bool isMiss, DateTime timeUtc)
        {
            Touch(timeUtc);
            Swings++;
            if (isMiss)
                Misses++;
            else
                HitFailed++;
        }

        public void NoteDeath(DateTime timeUtc)
        {
            Touch(timeUtc);
            Deaths++;
        }

        private void Touch(DateTime timeUtc)
        {
            if (FirstEventUtc == default || timeUtc < FirstEventUtc)
                FirstEventUtc = timeUtc;

            if (LastEventUtc < timeUtc)
                LastEventUtc = timeUtc;
        }
    }

    private static class ActxSnapshotFormatter
    {
        public static CombatDataWrapper Build(EncounterSession encounter, bool isActive)
        {
            var durationSeconds = encounter.DurationSeconds;
            var combatants = encounter.Combatants
                .OrderByDescending(combatant => combatant.Damage / durationSeconds)
                .ThenBy(combatant => combatant.Name, StringComparer.Ordinal)
                .ToList();

            var summaryCombatants = combatants
                .Where(static combatant => combatant.Kind != TrackedActorKind.HostileNpc)
                .ToList();
            if (summaryCombatants.Count == 0)
                summaryCombatants = combatants;

            var totalDamage = summaryCombatants.Sum(static combatant => combatant.Damage);
            var totalDamageTaken = summaryCombatants.Sum(static combatant => combatant.DamageTaken);
            var totalHits = summaryCombatants.Sum(static combatant => combatant.Hits);
            var totalHitFailed = summaryCombatants.Sum(static combatant => combatant.HitFailed);
            var totalCritHits = summaryCombatants.Sum(static combatant => combatant.CritHits);

            var maxHitCombatant = summaryCombatants
                .Where(static combatant => combatant.MaxHitValue > 0)
                .OrderByDescending(static combatant => combatant.MaxHitValue)
                .ThenBy(combatant => combatant.Name, StringComparer.Ordinal)
                .FirstOrDefault();

            var encounterMaxHit = "--";
            var encounterShortMaxHit = "--";
            if (maxHitCombatant != null)
            {
                var actionName = SafeActionName(maxHitCombatant.MaxHitActionName);
                encounterMaxHit =
                    $"{maxHitCombatant.Name}-{actionName}-{CreateDamageString(maxHitCombatant.MaxHitValue, useSuffix: true, useDecimals: true)}";
                encounterShortMaxHit =
                    $"{maxHitCombatant.Name}-{CreateDamageString(maxHitCombatant.MaxHitValue, useSuffix: true, useDecimals: false)}";
            }

            var combatantPayload = new Dictionary<string, Combatant>(combatants.Count, StringComparer.Ordinal);
            foreach (var combatant in combatants)
            {
                var damagePercent = totalDamage > 0
                    ? $"{(int)(combatant.Damage / (float)totalDamage * 100f)}%"
                    : "--";

                var encDps = combatant.Damage / durationSeconds;
                var encHps = combatant.Healed / durationSeconds;
                var dtps = combatant.DamageTaken / durationSeconds;
                var toHit = combatant.Swings > 0
                    ? combatant.Hits / (float)combatant.Swings * 100f
                    : 0f;

                combatantPayload[$"{combatant.Name}#{combatant.ActorId:X8}"] = new Combatant
                {
                    Name = combatant.Name,
                    ParticipantKind = FormatTrackedActorKind(combatant.Kind),
                    Job = FormatCombatantJobName(combatant),
                    DamagePercentText = damagePercent,
                    DamageText = CreateDamageString(combatant.Damage, useSuffix: true, useDecimals: true),
                    EncDpsText = encDps.ToString("0", CultureInfo.InvariantCulture),
                    EncHpsText = encHps.ToString("0", CultureInfo.InvariantCulture),
                    HealedText = CreateDamageString(combatant.Healed, useSuffix: true, useDecimals: true),
                    DtpsText = dtps.ToString("0", CultureInfo.InvariantCulture),
                    MaxHitText = combatant.MaxHitValue > 0
                        ? $"{SafeActionName(combatant.MaxHitActionName)}-{CreateDamageString(combatant.MaxHitValue, useSuffix: true, useDecimals: true)}"
                        : "--",
                    HitsText = combatant.Hits.ToString(CultureInfo.InvariantCulture),
                    CritHitsText = combatant.CritHits.ToString(CultureInfo.InvariantCulture),
                    CritDirectHitsText = combatant.CritDirectHits.ToString(CultureInfo.InvariantCulture),
                    ToHitText = toHit.ToString("F", CultureInfo.InvariantCulture),
                    DamageTakenText = CreateDamageString(combatant.DamageTaken, useSuffix: true, useDecimals: true),
                    BlockPctText = "--",
                    ParryPctText = "--",
                    DeathsText = combatant.Deaths.ToString(CultureInfo.InvariantCulture),
                    DotDamageText = CreateDamageString(combatant.DotDamage, useSuffix: true, useDecimals: true),
                };
            }

            return new CombatDataWrapper
            {
                Type = "broadcast",
                MsgType = "CombatData",
                Msg = new CombatData
                {
                    Type = "CombatData",
                    IsActive = isActive ? "true" : "false",
                    Encounter = new Encounter
                    {
                        CurrentZoneName = encounter.ZoneName,
                        DurationText = FormatDuration(durationSeconds),
                        DamageText = CreateDamageString(totalDamage, useSuffix: true, useDecimals: true),
                        EncDpsText = (totalDamage / durationSeconds).ToString("0", CultureInfo.InvariantCulture),
                        HitsText = totalHits.ToString(CultureInfo.InvariantCulture),
                        HitFailedText = totalHitFailed.ToString(CultureInfo.InvariantCulture),
                        CritHitsText = totalCritHits.ToString(CultureInfo.InvariantCulture),
                        CritHitPercentText = totalHits > 0
                            ? $"{(int)(totalCritHits / (float)totalHits * 100f)}%"
                            : "0%",
                        MaxHitText = encounterMaxHit,
                        MaxHitValueText = encounterShortMaxHit,
                        DamageTakenText = CreateDamageString(totalDamageTaken, useSuffix: true, useDecimals: true),
                    },
                    Combatant = combatantPayload,
                },
            };
        }

        public static string FormatDuration(double durationSeconds)
        {
            var wholeSeconds = durationSeconds < 1d
                ? 1
                : (int)Math.Round(durationSeconds, MidpointRounding.AwayFromZero);
            var span = TimeSpan.FromSeconds(wholeSeconds);
            return span.TotalHours >= 1d
                ? span.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
                : span.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        }

        private static string SafeActionName(string? actionName)
            => string.IsNullOrWhiteSpace(actionName) ? "未知技能" : actionName;

        private static string FormatCombatantJobName(CombatantSession combatant)
        {
            if (!string.IsNullOrWhiteSpace(combatant.JobName))
                return combatant.JobName;

            return combatant.Kind switch
            {
                TrackedActorKind.FriendlyNpc => "友方NPC",
                TrackedActorKind.HostileNpc => "敌方NPC",
                _ => "-",
            };
        }

        private static string? FormatTrackedActorKind(TrackedActorKind kind)
            => kind switch
            {
                TrackedActorKind.Player => "player",
                TrackedActorKind.FriendlyNpc => "friendlyNpc",
                TrackedActorKind.HostileNpc => "hostileNpc",
                _ => null,
            };

    }

    private static string CreateDamageString(long damage, bool useSuffix, bool useDecimals)
    {
        const long trillion = 1_000_000_000_000L;
        const long hundredMillion = 100_000_000L;
        const long tenThousand = 10_000L;

        if (!useSuffix)
            return damage.ToString(CultureInfo.InvariantCulture);

        var abs = Math.Abs(damage);
        if (abs >= trillion)
            return FormatChineseDamageUnit(damage, trillion, "兆", useDecimals ? "0.00" : "0.#");

        if (abs >= hundredMillion)
            return FormatChineseDamageUnit(damage, hundredMillion, "亿", useDecimals ? "0.00" : "0.#");

        if (abs >= tenThousand)
            return FormatChineseDamageUnit(damage, tenThousand, "万", useDecimals ? "0.00" : "0.#");

        return damage.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatChineseDamageUnit(long value, long unitBase, string unit, string numericFormat)
        => (value / (double)unitBase).ToString(numericFormat, CultureInfo.InvariantCulture) + unit;
}
