using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DalamudACT;

// 历史记录模块：负责历史列表、预览、导入导出和历史记录序列化。
internal sealed partial class LocalStatsService
{
    private const double MinimumHistoricalEncounterSeconds = 30d;
    private static readonly JsonSerializerOptions HistoryJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = false,
    };


    private readonly List<HistoricalCombatData> historicalRecords = new();

    private int selectedHistoricalRecordIndex = -1;
    private DateTime? historicalPreviewExpiresAtUtc;

    public IReadOnlyList<HistoricalCombatData> HistoricalRecords
    {
        get
        {
            lock (gate)
                return historicalRecords.ToArray();
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
            debugCombatLogEntries.Clear();
            debugObservedStatusKeys.Clear();
            debugBossCastActionIds.Clear();
            ownerCache.Clear();
            observedFriendlyActorCache.Clear();
            partyMemberHpCache.Clear();
            recentHostilePlayerActions.Clear();
            activePlayerDots.Clear();
            activeWildfires.Clear();
            dotStatusClassificationCache.Clear();
            actionDescriptionDotPotencyCache.Clear();
            actionDescriptionDotPotencyCacheMisses.Clear();
            actionDescriptionPotencyCache.Clear();
            actionDescriptionPotencyCacheMisses.Clear();
            CurrentCombatData = null;
            DisplayCombatData = null;
            ClearHistoricalPreviewLocked();
            currentEncounter = new EncounterSession();
            partyOutOfCombatSinceUtc = default;
            enteredCombatWithoutDataSinceUtc = default;
            lastNoDataCombatDiagnosticUtc = default;
            lastPlayerDotStatusPollUtc = default;
            lastDebugCombatRecordPollUtc = default;
            debugCombatRecorderPrimed = false;
            HistoryTransferStatusText = string.Empty;
            suppressStaleDisplayUntilNextCombatStart = false;
            StatusText = "等待战斗数据...";
            LogHelper.PrintWithModule("统计", "历史", "已清空历史记录并重置当前战斗状态。");
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

    private sealed class HistoricalRecordsExportPayload
    {
        public int Version { get; set; }

        public DateTime ExportedAtUtc { get; set; }

        public List<HistoricalCombatData> Records { get; set; } = new();
    }

}
