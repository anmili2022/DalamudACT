using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal enum StatsPanelTabId
{
    None = 0,
    Dps = 1,
    Hps = 2,
    Taken = 3,
    Overview = 4,
    History = 5,
}

internal enum MetricColumnSlot : uint
{
    Player = 0,
    Job = 1,
    Damage = 2,
    Value = 3,
    Deaths = 4,
    Share = 5,
}

internal readonly record struct VisibleMetricColumn(
    MetricColumnSlot Slot,
    int TableIndex,
    string Label,
    float Width,
    ImGuiTableColumnFlags Flags);

internal readonly record struct StatsPanelDrawResult(
    StatsPanelTabId ActiveTab,
    bool ToggleDpsCollapseRequested,
    bool OpenSettingsRequested,
    bool HideTabsWhenCollapsedRequested = false);

/// <summary>
/// 统计面板的 ImGui 绘制入口，负责 DPS/HPS/承伤/概览/历史记录各页签的表格与交互。
/// 相关参考：
/// - https://dalamud.dev/
/// - https://dalamud.dev/api/
/// 调整 ImGui 表格、Tab、窗口内交互或 Dalamud 绑定的 ImGui API 前，先对照上述文档。
/// </summary>
internal static class StatsPanel
{
    private const float MinimumDeathsColumnWidth = 20f;
    private static readonly Vector4 FrameBackgroundColor = new(0.10f, 0.10f, 0.10f, 0.65f);
    private static bool isResizingMetricColumns;
    private static int metricTableResetVersion;
    private static int historyTableResetVersion;
    private const ImGuiTableFlags ReadOnlyTableFlags =
        ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.NoSavedSettings;

    internal static void RequestMetricColumnWidthReset()
    {
        metricTableResetVersion++;
        isResizingMetricColumns = false;
    }

    internal static void RequestHistoryColumnWidthReset()
        => historyTableResetVersion++;

    public static StatsPanelDrawResult Draw(
        LocalStatsService statsService,
        PluginConfiguration config,
        StatsPanelTabId previousActiveTab = StatsPanelTabId.None,
        bool collapseToTabBar = false)
    {
        if (!config.HasAnyVisibleStatsTab())
        {
            ImGui.TextDisabled("当前没有启用任何页面，请在设置中勾选。");
            return new StatsPanelDrawResult(StatsPanelTabId.None, false, false, false);
        }

        var combatData = statsService.DisplayCombatData;
        var hasCombatData = combatData?.Msg?.Encounter != null;
        if (!collapseToTabBar && !hasCombatData)
        {
            var history = statsService.HistoricalRecords;
            var toggleNoCombatCollapseRequested = false;
            var historyRecordClicked = false;
            ImGui.TextDisabled(history.Count > 0
                ? "当前没有实时战斗数据，可点击下方历史记录查看。"
                : "等待战斗数据...");

            toggleNoCombatCollapseRequested = config.ShowDpsTab && ImGui.IsItemClicked();

            if (config.ShowHistoryTab)
            {
                ImGui.Spacing();
                historyRecordClicked = DrawHistoryTab(statsService, config);
            }

            return new StatsPanelDrawResult(
                historyRecordClicked && config.ShowDpsTab ? StatsPanelTabId.Dps : previousActiveTab,
                toggleNoCombatCollapseRequested,
                false,
                toggleNoCombatCollapseRequested);
        }

        if (!ImGui.BeginTabBar("##stats_tabs"))
            return new StatsPanelDrawResult(previousActiveTab, false, false, false);

        var activeTab = previousActiveTab;
        var toggleDpsCollapseRequested = false;
        var openSettingsRequested = false;

        if (config.ShowDpsTab && ImGui.BeginTabItem("DPS"))
        {
            var clickedCurrentDpsTab = ImGui.IsItemClicked() && previousActiveTab == StatsPanelTabId.Dps;
            var rightClickedDpsTab = ImGui.IsItemClicked(ImGuiMouseButton.Right);
            activeTab = StatsPanelTabId.Dps;
            toggleDpsCollapseRequested |= clickedCurrentDpsTab;
            openSettingsRequested |= rightClickedDpsTab;

            if (!collapseToTabBar)
            {
                if (hasCombatData)
                    DrawDpsTab(combatData!, config);
                else
                    ImGui.TextDisabled("等待战斗数据...");
            }

            ImGui.EndTabItem();
        }

        if (config.ShowHpsTab && ImGui.BeginTabItem("HPS"))
        {
            activeTab = StatsPanelTabId.Hps;

            if (!collapseToTabBar)
            {
                if (hasCombatData)
                {
                    DrawMetricTab(
                        id: "hps",
                        valueColumnLabel: "秒疗",
                        combatData: combatData!,
                        config: config,
                        selector: static c => ParseMetric(c.EncHpsText),
                        textSelector: static c => c.EncHpsText ?? "0",
                        tooltipPrimaryLabel: "治疗量",
                        tooltipPrimaryTextSelector: static c => c.HealedText ?? "0",
                        tooltipRateLabel: "秒疗",
                        tooltipRateTextSelector: static c => c.EncHpsText ?? "0",
                        showPlayerColumn: config.ShowDpsPlayerColumn,
                        showJobColumn: config.ShowDpsJobColumn,
                        showDamageColumn: config.ShowDpsDamageColumn,
                        damageColumnLabel: "治疗量",
                        damageTextSelector: static c => c.HealedText ?? "0",
                        showValueColumn: config.ShowDpsValueColumn,
                        showDeathsColumn: config.ShowDpsDeathsColumn,
                        maxRows: config.DpsVisibleCount);
                }
                else
                {
                    ImGui.TextDisabled("等待战斗数据...");
                }
            }

            ImGui.EndTabItem();
        }

        if (config.ShowTakenTab && ImGui.BeginTabItem("承伤"))
        {
            activeTab = StatsPanelTabId.Taken;

            if (!collapseToTabBar)
            {
                if (hasCombatData)
                {
                    DrawMetricTab(
                        id: "taken",
                        valueColumnLabel: "秒承伤",
                        combatData: combatData!,
                        config: config,
                        selector: static c => ParseMetric(c.DtpsText),
                        textSelector: static c => c.DtpsText ?? "0",
                        tooltipPrimaryLabel: "承伤量",
                        tooltipPrimaryTextSelector: static c => c.DamageTakenText ?? "0",
                        tooltipRateLabel: "秒承伤",
                        tooltipRateTextSelector: static c => c.DtpsText ?? "0",
                        showPlayerColumn: config.ShowDpsPlayerColumn,
                        showJobColumn: config.ShowDpsJobColumn,
                        showDamageColumn: config.ShowDpsDamageColumn,
                        damageColumnLabel: "承伤量",
                        damageTextSelector: static c => c.DamageTakenText ?? "0",
                        showValueColumn: config.ShowDpsValueColumn,
                        showDeathsColumn: config.ShowDpsDeathsColumn,
                        maxRows: config.DpsVisibleCount);
                }
                else
                {
                    ImGui.TextDisabled("等待战斗数据...");
                }
            }

            ImGui.EndTabItem();
        }

        if (config.ShowOverviewTab && ImGui.BeginTabItem("概览"))
        {
            activeTab = StatsPanelTabId.Overview;

            if (!collapseToTabBar)
            {
                if (hasCombatData)
                    DrawOverviewTab(combatData!, config);
                else
                    ImGui.TextDisabled("等待战斗数据...");
            }

            ImGui.EndTabItem();
        }

        if (config.ShowHistoryTab && ImGui.BeginTabItem("历史记录"))
        {
            activeTab = StatsPanelTabId.History;
            if (!collapseToTabBar && DrawHistoryTab(statsService, config) && config.ShowDpsTab)
                activeTab = StatsPanelTabId.Dps;
            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
        return new StatsPanelDrawResult(activeTab, toggleDpsCollapseRequested, openSettingsRequested, false);
    }

    private static void DrawDpsTab(CombatDataWrapper combatData, PluginConfiguration config)
    {
        var totalDps = combatData.Msg!.Combatant.Values
            .Where(static c => !string.IsNullOrWhiteSpace(c.Name))
            .Sum(static c => ParseMetric(c.EncDpsText));
        var totalDamage = combatData.Msg.Encounter?.DamageText ?? "0";
        var totalDeaths = combatData.Msg.Combatant.Values
            .Where(static c => !string.IsNullOrWhiteSpace(c.Name))
            .Sum(static c => ParseCount(c.DeathsText));

        DrawMetricTab(
            id: "dps",
            valueColumnLabel: "秒伤",
            combatData: combatData,
            config: config,
            selector: static c => ParseMetric(c.EncDpsText),
            textSelector: static c => c.EncDpsText ?? "0",
            tooltipPrimaryLabel: "伤害量",
            tooltipPrimaryTextSelector: static c => c.DamageText ?? "0",
            tooltipRateLabel: "秒伤",
            tooltipRateTextSelector: static c => c.EncDpsText ?? "0",
            showPlayerColumn: config.ShowDpsPlayerColumn,
            showJobColumn: config.ShowDpsJobColumn,
            showDamageColumn: config.ShowDpsDamageColumn,
            damageColumnLabel: "伤害量",
            damageTextSelector: static c => c.DamageText ?? "0",
            showValueColumn: config.ShowDpsValueColumn,
            showDeathsColumn: config.ShowDpsDeathsColumn,
            maxRows: config.DpsVisibleCount,
            showSummaryRow: true,
            summaryName: "总DPS",
            summaryJob: "全队",
            summaryDamageText: totalDamage,
            summaryValueText: FormatMetricValue(totalDps),
            summaryDeathsText: totalDeaths.ToString(CultureInfo.InvariantCulture));
    }

    private static void DrawMetricTab(
        string id,
        string valueColumnLabel,
        CombatDataWrapper combatData,
        PluginConfiguration config,
        Func<Combatant, double> selector,
        Func<Combatant, string> textSelector,
        string tooltipPrimaryLabel = "伤害量",
        Func<Combatant, string>? tooltipPrimaryTextSelector = null,
        string tooltipRateLabel = "秒伤",
        Func<Combatant, string>? tooltipRateTextSelector = null,
        bool showPlayerColumn = true,
        bool showJobColumn = true,
        bool showDamageColumn = false,
        string damageColumnLabel = "",
        Func<Combatant, string>? damageTextSelector = null,
        bool showValueColumn = true,
        bool showDeathsColumn = false,
        int? maxRows = null,
        bool showSummaryRow = false,
        string summaryName = "",
        string summaryJob = "",
        string? summaryDamageText = null,
        string? summaryValueText = null,
        string? summaryDeathsText = null)
    {
        if (!ImGui.BeginChild($"##metric_{id}_scroll", new Vector2(0f, 0f), false))
            return;

        var allRows = combatData.Msg!.Combatant.Values
            .Where(static c => !string.IsNullOrWhiteSpace(c.Name))
            .OrderByDescending(selector)
            .ToList();

        if (allRows.Count == 0)
        {
            ImGui.TextDisabled("没有可显示的数据。");
            ImGui.EndChild();
            return;
        }

        var rows = maxRows.HasValue
            ? allRows.Take(Math.Max(maxRows.Value, 1)).ToList()
            : allRows;
        var maxValue = allRows.Max(selector);
        var totalValue = allRows.Sum(selector);
        var playerColumnWidth = ResolvePlayerColumnWidth(rows, showSummaryRow ? summaryName : null, config);
        var jobColumnWidth = ResolveMetricColumnWidth(config.FloatingStatsJobColumnWidth, config, 88f, "职业");
        var damageColumnWidth = ResolveMetricColumnWidth(config.FloatingStatsDamageColumnWidth, config, 88f, damageColumnLabel);
        var fixedColumnWidth = ResolveMetricColumnWidth(config.FloatingStatsValueColumnWidth, config, 88f, valueColumnLabel);
        var deathColumnWidth = ResolveDeathColumnWidth(config.FloatingStatsDeathsColumnWidth, config);
        var rowHeight = ResolveRowHeight(config);
        var layoutSignature = BuildMetricLayoutSignature(
            showPlayerColumn,
            showJobColumn,
            showDamageColumn,
            showValueColumn,
            showDeathsColumn);

        var metricTableFlags = ImGuiTableFlags.RowBg
                               | ImGuiTableFlags.BordersInnerH
                               | ImGuiTableFlags.Resizable
                               | ImGuiTableFlags.SizingFixedFit
                               | ImGuiTableFlags.NoSavedSettings;
        var visibleColumns = new List<VisibleMetricColumn>(6);
        int? playerColumnIndex = null;
        int? jobColumnIndex = null;
        int? damageColumnIndex = null;
        int? valueColumnIndex = null;
        int? deathsColumnIndex = null;
        var nextColumnIndex = 0;

        if (showPlayerColumn)
        {
            playerColumnIndex = nextColumnIndex;
            visibleColumns.Add(new VisibleMetricColumn(
                MetricColumnSlot.Player,
                nextColumnIndex++,
                "玩家",
                playerColumnWidth,
                ImGuiTableColumnFlags.WidthFixed));
        }

        if (showJobColumn)
        {
            jobColumnIndex = nextColumnIndex;
            visibleColumns.Add(new VisibleMetricColumn(
                MetricColumnSlot.Job,
                nextColumnIndex++,
                "职业",
                jobColumnWidth,
                ImGuiTableColumnFlags.WidthFixed));
        }

        if (showDamageColumn)
        {
            damageColumnIndex = nextColumnIndex;
            visibleColumns.Add(new VisibleMetricColumn(
                MetricColumnSlot.Damage,
                nextColumnIndex++,
                damageColumnLabel,
                damageColumnWidth,
                ImGuiTableColumnFlags.WidthFixed));
        }

        if (showValueColumn)
        {
            valueColumnIndex = nextColumnIndex;
            visibleColumns.Add(new VisibleMetricColumn(
                MetricColumnSlot.Value,
                nextColumnIndex++,
                valueColumnLabel,
                fixedColumnWidth,
                ImGuiTableColumnFlags.WidthFixed));
        }

        if (showDeathsColumn)
        {
            deathsColumnIndex = nextColumnIndex;
            visibleColumns.Add(new VisibleMetricColumn(
                MetricColumnSlot.Deaths,
                nextColumnIndex++,
                "死",
                deathColumnWidth,
                ImGuiTableColumnFlags.WidthFixed));
        }

        var shareColumnIndex = nextColumnIndex;
        visibleColumns.Add(new VisibleMetricColumn(
            MetricColumnSlot.Share,
            shareColumnIndex,
            "占比",
            1f,
            ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoResize));

        if (!ImGui.BeginTable(
                BuildMetricTableId(id, layoutSignature, metricTableResetVersion),
                visibleColumns.Count,
                metricTableFlags))
        {
            ImGui.EndChild();
            return;
        }

        foreach (var column in visibleColumns)
            ImGui.TableSetupColumn(column.Label, column.Flags, column.Width, (uint)column.Slot);

        var measuredPlayerColumnWidth = playerColumnWidth;
        var measuredJobColumnWidth = jobColumnWidth;
        var measuredDamageColumnWidth = damageColumnWidth;
        var measuredValueColumnWidth = fixedColumnWidth;
        var measuredDeathsColumnWidth = deathColumnWidth;
        DrawMetricTableHeadersRow(
            config,
            visibleColumns,
            ref measuredPlayerColumnWidth,
            ref measuredJobColumnWidth,
            ref measuredDamageColumnWidth,
            ref measuredValueColumnWidth,
            ref measuredDeathsColumnWidth);

        foreach (var combatant in rows)
        {
            var value = selector(combatant);
            var maxRatio = maxValue > 0 ? value / maxValue : 0d;
            var totalRatio = totalValue > 0 ? value / totalValue : 0d;
            var barColor = ResolveBarColor(combatant, config);

            TableNextRow(rowHeight);

            if (showPlayerColumn)
            {
                ImGui.TableSetColumnIndex(playerColumnIndex!.Value);
                ImGui.TextUnformatted(combatant.Name ?? string.Empty);
            }

            if (showJobColumn)
            {
                ImGui.TableSetColumnIndex(jobColumnIndex!.Value);
                ImGui.TextUnformatted(string.IsNullOrWhiteSpace(combatant.Job) ? "-" : combatant.Job!);
            }

            if (showDamageColumn)
            {
                ImGui.TableSetColumnIndex(damageColumnIndex!.Value);
                ImGui.TextUnformatted(damageTextSelector?.Invoke(combatant) ?? "0");
            }

            if (showValueColumn)
            {
                ImGui.TableSetColumnIndex(valueColumnIndex!.Value);
                ImGui.TextUnformatted(textSelector(combatant));
            }

            if (showDeathsColumn)
            {
                ImGui.TableSetColumnIndex(deathsColumnIndex!.Value);
                ImGui.TextUnformatted(string.IsNullOrWhiteSpace(combatant.DeathsText) ? "0" : combatant.DeathsText!);
            }

            ImGui.TableSetColumnIndex(shareColumnIndex);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, FrameBackgroundColor);
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, barColor);
            ImGui.ProgressBar((float)Math.Clamp(maxRatio, 0d, 1d), new Vector2(-1f, 0f), $"{totalRatio:P1}");
            DrawCombatantBarTooltip(
                combatant,
                tooltipPrimaryLabel,
                tooltipPrimaryTextSelector?.Invoke(combatant),
                tooltipRateLabel,
                tooltipRateTextSelector?.Invoke(combatant));
            ImGui.PopStyleColor(2);
        }

        if (showSummaryRow)
        {
            TableNextRow(rowHeight);

            if (showPlayerColumn)
            {
                ImGui.TableSetColumnIndex(playerColumnIndex!.Value);
                ImGui.TextUnformatted(summaryName);
            }

            if (showJobColumn)
            {
                ImGui.TableSetColumnIndex(jobColumnIndex!.Value);
                ImGui.TextUnformatted(summaryJob);
            }

            if (showDamageColumn)
            {
                ImGui.TableSetColumnIndex(damageColumnIndex!.Value);
                ImGui.TextUnformatted(string.IsNullOrWhiteSpace(summaryDamageText) ? "0" : summaryDamageText);
            }

            if (showValueColumn)
            {
                ImGui.TableSetColumnIndex(valueColumnIndex!.Value);
                ImGui.TextUnformatted(string.IsNullOrWhiteSpace(summaryValueText) ? "0" : summaryValueText);
            }

            if (showDeathsColumn)
            {
                ImGui.TableSetColumnIndex(deathsColumnIndex!.Value);
                ImGui.TextUnformatted(string.IsNullOrWhiteSpace(summaryDeathsText) ? "0" : summaryDeathsText);
            }

            ImGui.TableSetColumnIndex(shareColumnIndex);
            ImGui.TextUnformatted(string.Empty);
        }

        PersistMetricColumnWidths(
            config,
            showPlayerColumn,
            playerColumnIndex ?? -1,
            showJobColumn,
            jobColumnIndex ?? -1,
            showDamageColumn,
            damageColumnIndex ?? -1,
            showValueColumn,
            valueColumnIndex ?? -1,
            showDeathsColumn,
            deathsColumnIndex ?? -1,
            measuredPlayerColumnWidth,
            measuredJobColumnWidth,
            measuredDamageColumnWidth,
            measuredValueColumnWidth,
            measuredDeathsColumnWidth,
            shareColumnIndex);

        ImGui.EndTable();
        ImGui.EndChild();
    }

    private static void DrawOverviewTab(CombatDataWrapper combatData, PluginConfiguration config)
    {
        if (!ImGui.BeginChild("##overview_scroll", new Vector2(0f, 0f), false))
            return;

        var encounter = combatData.Msg!.Encounter!;
        ImGui.TextUnformatted($"区域: {encounter.CurrentZoneName ?? "Unknown"}");
        ImGui.TextUnformatted($"战斗时长: {encounter.DurationText ?? "00:00"}");
        ImGui.Separator();

        if (ImGui.BeginTable("##overview_summary", 2, ReadOnlyTableFlags))
        {
            DrawOverviewRow("总伤害", encounter.DamageText, config);
            DrawOverviewRow("团队 DPS", encounter.EncDpsText, config);
            DrawOverviewRow("命中 / 失败", $"{encounter.HitsText ?? "0"}/{encounter.HitFailedText ?? "0"}", config);
            DrawOverviewRow("暴击次数", $"{encounter.CritHitsText ?? "0"} ({encounter.CritHitPercentText ?? "0%"})", config);
            DrawOverviewRow("最大伤害", JoinPair(encounter.MaxHitText, encounter.MaxHitValueText), config);
            DrawOverviewRow("总承伤", encounter.DamageTakenText, config);
            ImGui.EndTable();
        }

        ImGui.Separator();
        foreach (var combatant in combatData.Msg.Combatant.Values.Where(static c => !string.IsNullOrWhiteSpace(c.Name)))
        {
            var header = string.IsNullOrWhiteSpace(combatant.Job)
                ? combatant.Name!
                : $"{combatant.Name} ({combatant.Job})";
            if (!ImGui.CollapsingHeader(header))
                continue;

            if (!ImGui.BeginTable($"##combatant_{combatant.Name}", 2, ReadOnlyTableFlags))
                continue;

            DrawOverviewRow("伤害占比", combatant.DamagePercentText, config);
            DrawOverviewRow("总伤害", combatant.DamageText, config);
            DrawOverviewRow("DPS", combatant.EncDpsText, config);
            DrawOverviewRow("HPS", combatant.EncHpsText, config);
            DrawOverviewRow("DTPS", combatant.DtpsText, config);
            DrawOverviewRow("最大伤害技能", combatant.MaxHitText, config);
            DrawOverviewRow("命中次数", combatant.HitsText, config);
            DrawOverviewRow("暴击次数", combatant.CritHitsText, config);
            DrawOverviewRow("命中率", combatant.ToHitText, config);
            DrawOverviewRow("承受伤害", combatant.DamageTakenText, config);
            DrawOverviewRow("格挡率", combatant.BlockPctText, config);
            DrawOverviewRow("招架率", combatant.ParryPctText, config);
            DrawOverviewRow("死亡", combatant.DeathsText, config);
            ImGui.EndTable();
        }

        ImGui.EndChild();
    }

    private static void DrawOverviewRow(string label, string? value, PluginConfiguration config)
    {
        TableNextRow(ResolveRowHeight(config));
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(label);
        ImGui.TableSetColumnIndex(1);
        ImGui.TextUnformatted(string.IsNullOrWhiteSpace(value) ? "--" : value);
    }

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
            return false;

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

    private static Vector4 ResolveBarColor(Combatant combatant, PluginConfiguration config)
    {
        if (config.BarColorMode == StatsBarColorMode.Single)
            return config.GetSingleBarColor();

        return config.GetThemeBarColor(combatant.Job);
    }

    private static string JoinPair(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left))
            return string.IsNullOrWhiteSpace(right) ? "--" : right!;

        if (string.IsNullOrWhiteSpace(right))
            return left;

        return $"{left} ({right})";
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

    private static string FormatHistoryTimestamp(DateTime? utcTime)
        => utcTime.HasValue
            ? utcTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
            : "--";

    private static double ParseMetric(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0d;

        var text = value.Trim();
        if (text is "---" or "--")
            return 0d;

        text = text.Replace(",", string.Empty, StringComparison.Ordinal);
        text = text.Replace("%", string.Empty, StringComparison.Ordinal);
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0d;
    }

    private static int ParseCount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        var text = value.Trim();
        if (text is "---" or "--")
            return 0;

        text = text.Replace(",", string.Empty, StringComparison.Ordinal);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static void DrawCombatantBarTooltip(
        Combatant combatant,
        string primaryLabel,
        string? primaryValue,
        string rateLabel,
        string? rateValue)
    {
        if (!ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        ImGui.TextUnformatted($"玩家: {FallbackText(combatant.Name, "-")}");
        ImGui.TextUnformatted($"职业: {FallbackText(combatant.Job, "-")}");
        ImGui.TextUnformatted($"{primaryLabel}: {FallbackText(primaryValue, "0")}");
        ImGui.TextUnformatted($"{rateLabel}: {FallbackText(rateValue, "0")}");
        ImGui.TextUnformatted($"死亡: {FallbackText(combatant.DeathsText, "0")}");
        ImGui.EndTooltip();
    }

    private static string FormatMetricValue(double value)
        => value.ToString("0", CultureInfo.InvariantCulture);

    private static string FallbackText(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string BuildMetricLayoutSignature(
        bool showPlayerColumn,
        bool showJobColumn,
        bool showDamageColumn,
        bool showValueColumn,
        bool showDeathsColumn)
        => $"{(showPlayerColumn ? 'p' : '-')}{(showJobColumn ? 'j' : '-')}{(showDamageColumn ? 'd' : '-')}{(showValueColumn ? 'v' : '-')}{(showDeathsColumn ? 'x' : '-')}";

    private static string BuildMetricTableId(string id, string layoutSignature, int resetVersion)
        => $"##metric_{id}_{layoutSignature}_{resetVersion}";

    private static string BuildHistoryTableId(int resetVersion)
        => $"##history_{resetVersion}";

    private static float ResolvePlayerColumnWidth(
        IReadOnlyCollection<Combatant> rows,
        string? extraLabel,
        PluginConfiguration config)
    {
        if (config.FloatingStatsPlayerColumnWidth > 0f)
            return config.FloatingStatsPlayerColumnWidth;

        var autoWidth = CalculatePlayerColumnWidth(rows, extraLabel);
        return config.FloatingStatsPlayerColumnMinWidth > 0f
            ? Math.Max(autoWidth, config.FloatingStatsPlayerColumnMinWidth)
            : autoWidth;
    }

    private static float ResolveMetricColumnWidth(float savedWidth, PluginConfiguration config, float fallbackWidth, string headerText)
        => savedWidth > 0f
            ? savedWidth
            : Math.Max(Math.Max(fallbackWidth, config.FloatingStatsMetricColumnWidth), CalculateFixedTextColumnWidth(headerText));

    private static float ResolveDeathColumnWidth(float savedWidth, PluginConfiguration config)
        => savedWidth > 0f
            ? Math.Max(savedWidth, MinimumDeathsColumnWidth)
            : Math.Max(MinimumDeathsColumnWidth, CalculateFixedTextColumnWidth("死"));

    private static float ResolveSavedOrDefaultColumnWidth(float savedWidth, PluginConfiguration config, float fallbackWidth, string headerText)
        => savedWidth > 0f
            ? savedWidth
            : ResolveFixedColumnWidth(config, fallbackWidth, headerText);

    private static float ResolveFixedColumnWidth(PluginConfiguration config, float fallbackWidth, string headerText)
        => Math.Max(Math.Max(fallbackWidth, config.FloatingStatsMetricColumnWidth), CalculateFixedTextColumnWidth(headerText));

    private static float ResolveRowHeight(PluginConfiguration config)
        => config.FloatingStatsRowHeight > 0f ? config.FloatingStatsRowHeight : 0f;

    private static void TableNextRow(float rowHeight)
    {
        if (rowHeight > 0f)
        {
            ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight);
            return;
        }

        ImGui.TableNextRow();
    }

    private static float CalculatePlayerColumnWidth(IReadOnlyCollection<Combatant> rows, string? extraLabel)
    {
        var widestText = Math.Max(
            ImGui.CalcTextSize("玩家").X,
            rows.Max(static row => ImGui.CalcTextSize(row.Name ?? string.Empty).X));

        if (!string.IsNullOrWhiteSpace(extraLabel))
            widestText = Math.Max(widestText, ImGui.CalcTextSize(extraLabel).X);

        return widestText + CalculateColumnPadding();
    }

    private static float CalculateFixedTextColumnWidth(string text)
        => ImGui.CalcTextSize(text).X + CalculateColumnPadding();

    private static float CalculateColumnPadding()
    {
        var style = ImGui.GetStyle();
        return style.CellPadding.X * 2f + style.FramePadding.X * 2f + 4f;
    }

    private static void DrawMetricTableHeadersRow(
        PluginConfiguration config,
        IReadOnlyList<VisibleMetricColumn> visibleColumns,
        ref float measuredPlayerColumnWidth,
        ref float measuredJobColumnWidth,
        ref float measuredDamageColumnWidth,
        ref float measuredValueColumnWidth,
        ref float measuredDeathsColumnWidth)
    {
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);

        if (config.LockFloatingStatsWindow)
            ImGui.BeginDisabled();

        foreach (var column in visibleColumns)
        {
            ImGui.TableSetColumnIndex(column.TableIndex);
            ImGui.TableHeader(column.Label);

            if (column.Slot == MetricColumnSlot.Share)
                continue;

            var headerWidth = ImGui.GetItemRectSize().X;
            if (headerWidth <= 0f)
                continue;

            switch (column.Slot)
            {
                case MetricColumnSlot.Player:
                    measuredPlayerColumnWidth = headerWidth;
                    break;
                case MetricColumnSlot.Job:
                    measuredJobColumnWidth = headerWidth;
                    break;
                case MetricColumnSlot.Damage:
                    measuredDamageColumnWidth = headerWidth;
                    break;
                case MetricColumnSlot.Value:
                    measuredValueColumnWidth = headerWidth;
                    break;
                case MetricColumnSlot.Deaths:
                    measuredDeathsColumnWidth = headerWidth;
                    break;
            }
        }

        if (config.LockFloatingStatsWindow)
            ImGui.EndDisabled();
    }

    private static void PersistMetricColumnWidths(
        PluginConfiguration config,
        bool showPlayerColumn,
        int playerColumnIndex,
        bool showJobColumn,
        int jobColumnIndex,
        bool showDamageColumn,
        int damageColumnIndex,
        bool showValueColumn,
        int valueColumnIndex,
        bool showDeathsColumn,
        int deathsColumnIndex,
        float measuredPlayerColumnWidth,
        float measuredJobColumnWidth,
        float measuredDamageColumnWidth,
        float measuredValueColumnWidth,
        float measuredDeathsColumnWidth,
        int shareColumnIndex)
    {
        var isHoveringResizableMetricColumn = IsHoveringResizableMetricColumn(
            showPlayerColumn,
            playerColumnIndex,
            showJobColumn,
            jobColumnIndex,
            showDamageColumn,
            damageColumnIndex,
            showValueColumn,
            valueColumnIndex,
            showDeathsColumn,
            deathsColumnIndex,
            shareColumnIndex);

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
        {
            if (isHoveringResizableMetricColumn && ImGui.GetMouseCursor() == ImGuiMouseCursor.ResizeEw)
                isResizingMetricColumns = true;

            return;
        }

        if (!ImGui.IsMouseReleased(ImGuiMouseButton.Left) || !isResizingMetricColumns)
            return;

        var changed = false;
        if (showPlayerColumn)
            changed |= TryUpdateStoredColumnWidth(ref config.FloatingStatsPlayerColumnWidth, measuredPlayerColumnWidth);
        if (showJobColumn)
            changed |= TryUpdateStoredColumnWidth(ref config.FloatingStatsJobColumnWidth, measuredJobColumnWidth);
        if (showDamageColumn)
            changed |= TryUpdateStoredColumnWidth(ref config.FloatingStatsDamageColumnWidth, measuredDamageColumnWidth);
        if (showValueColumn)
            changed |= TryUpdateStoredColumnWidth(ref config.FloatingStatsValueColumnWidth, measuredValueColumnWidth);
        if (showDeathsColumn)
            changed |= TryUpdateStoredColumnWidth(ref config.FloatingStatsDeathsColumnWidth, measuredDeathsColumnWidth, MinimumDeathsColumnWidth);

        isResizingMetricColumns = false;

        if (changed)
            config.Save();
    }

    private static bool IsHoveringResizableMetricColumn(
        bool showPlayerColumn,
        int playerColumnIndex,
        bool showJobColumn,
        int jobColumnIndex,
        bool showDamageColumn,
        int damageColumnIndex,
        bool showValueColumn,
        int valueColumnIndex,
        bool showDeathsColumn,
        int deathsColumnIndex,
        int shareColumnIndex)
    {
        _ = shareColumnIndex;

        if (showPlayerColumn && IsTableColumnHovered(playerColumnIndex))
            return true;
        if (showJobColumn && IsTableColumnHovered(jobColumnIndex))
            return true;
        if (showDamageColumn && IsTableColumnHovered(damageColumnIndex))
            return true;
        if (showValueColumn && IsTableColumnHovered(valueColumnIndex))
            return true;
        if (showDeathsColumn && IsTableColumnHovered(deathsColumnIndex))
            return true;

        return false;
    }

    private static bool IsTableColumnHovered(int columnIndex)
        => (ImGui.TableGetColumnFlags(columnIndex) & ImGuiTableColumnFlags.IsHovered) != 0;

    private static bool TryUpdateStoredColumnWidth(ref float storedWidth, float currentWidth)
    {
        if (currentWidth <= 0f)
            return false;

        if (Math.Abs(storedWidth - currentWidth) <= 0.5f)
            return false;

        storedWidth = currentWidth;
        return true;
    }

    private static bool TryUpdateStoredColumnWidth(ref float storedWidth, float currentWidth, float minimumWidth)
        => TryUpdateStoredColumnWidth(ref storedWidth, Math.Max(currentWidth, minimumWidth));

    private static bool TryUpdateStoredColumnWidth(ref float storedWidth, int columnIndex)
        => TryUpdateStoredColumnWidth(ref storedWidth, ImGui.GetColumnWidth(columnIndex));

    private static void PersistHistoryColumnWidths(PluginConfiguration config)
    {
        if (ImGui.IsMouseDown(ImGuiMouseButton.Left))
            return;

        var changed = false;
        changed |= TryUpdateStoredColumnWidth(ref config.HistoryStartTimeColumnWidth, 1);
        changed |= TryUpdateStoredColumnWidth(ref config.HistoryEndTimeColumnWidth, 2);
        changed |= TryUpdateStoredColumnWidth(ref config.HistoryDurationColumnWidth, 3);

        if (changed)
            config.Save();
    }

    private static void DrawTableHeadersRow(PluginConfiguration config)
    {
        if (config.LockFloatingStatsWindow)
        {
            ImGui.BeginDisabled();
            ImGui.TableHeadersRow();
            ImGui.EndDisabled();
        }
        else
        {
            ImGui.TableHeadersRow();
        }
    }
}
