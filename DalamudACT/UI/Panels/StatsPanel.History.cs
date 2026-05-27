using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal static partial class StatsPanel
{
    private static bool DrawHistoryTab(LocalStatsService statsService, PluginConfiguration config)
    {
        if (ImGui.Button("导出历史记录"))
            statsService.ExportHistoricalRecords();

        ImGui.SameLine();
        if (ImGui.Button("导入历史记录"))
            statsService.ImportHistoricalRecords();

        ImGui.SameLine();
        if (ImGui.Button("清空历史"))
            statsService.ClearHistory();

        ImGui.TextDisabled($"文件: {statsService.HistoryTransferFilePath}");
        if (!string.IsNullOrWhiteSpace(statsService.HistoryTransferStatusText))
            ImGui.TextDisabled(statsService.HistoryTransferStatusText);

        ImGui.Spacing();
        ImGui.TextDisabled($"未进入战斗时，点击历史记录会无限预览；进入战斗后，才按 {config.HistoryPreviewSeconds} 秒开始倒计时并自动回到当前 DPS 统计。");

        var history = statsService.HistoricalRecords;
        if (history.Count == 0)
        {
            ImGui.TextDisabled("暂无历史记录。");
            return false;
        }

        if (!ImGui.BeginChild("##history_scroll", new Vector2(0f, 320f), false))
        {
            ImGui.EndChild();
            return false;
        }

        var historyTableFlags = ImGuiTableFlags.RowBg
                                | ImGuiTableFlags.BordersInnerH
                                | ImGuiTableFlags.ScrollX
                                | ImGuiTableFlags.Resizable
                                | ImGuiTableFlags.NoSavedSettings;

        if (!ImGui.BeginTable(BuildHistoryTableId(historyTableResetVersion), 4, historyTableFlags))
        {
            ImGui.EndChild();
            return false;
        }

        ImGui.TableSetupColumn("区域", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("开始时间", ImGuiTableColumnFlags.WidthFixed, ResolveSavedOrDefaultColumnWidth(config.HistoryStartTimeColumnWidth, config, 180f, "开始时间"));
        ImGui.TableSetupColumn("结束时间", ImGuiTableColumnFlags.WidthFixed, ResolveSavedOrDefaultColumnWidth(config.HistoryEndTimeColumnWidth, config, 180f, "结束时间"));
        ImGui.TableSetupColumn("时长", ImGuiTableColumnFlags.WidthFixed, ResolveSavedOrDefaultColumnWidth(config.HistoryDurationColumnWidth, config, 100f, "时长"));
        DrawTableHeadersRow(config);

        var rowHeight = ResolveRowHeight(config);
        var selectedIndex = statsService.SelectedHistoricalRecordIndex;
        var historyRecordClicked = false;
        for (var index = history.Count - 1; index >= 0; index--)
        {
            var record = history[index];
            TableNextRow(rowHeight);
            if (index == selectedIndex)
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0.22f, 0.34f, 0.54f, 0.35f)));

            ImGui.TableSetColumnIndex(0);
            historyRecordClicked |= DrawHistoryCell(record.ZoneName, index, statsService);

            ImGui.TableSetColumnIndex(1);
            historyRecordClicked |= DrawHistoryCell(FormatHistoryTimestamp(record.StartTimeUtc), index, statsService);

            ImGui.TableSetColumnIndex(2);
            historyRecordClicked |= DrawHistoryCell(FormatHistoryTimestamp(record.EndTimeUtc), index, statsService);

            ImGui.TableSetColumnIndex(3);
            historyRecordClicked |= DrawHistoryCell(record.Duration, index, statsService);
        }

        PersistHistoryColumnWidths(config);

        ImGui.EndTable();
        ImGui.EndChild();
        return historyRecordClicked;
    }

    private static bool DrawHistoryCell(string? value, int index, LocalStatsService statsService)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "--" : value;
        ImGui.TextUnformatted(text);

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted(text);
            ImGui.EndTooltip();
        }

        if (ImGui.IsItemClicked())
            return statsService.PreviewHistoricalRecord(index);

        return false;
    }
}
