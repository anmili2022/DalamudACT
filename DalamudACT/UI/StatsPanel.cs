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

internal readonly record struct StatsPanelDrawResult(
    StatsPanelTabId ActiveTab,
    bool ToggleDpsCollapseRequested,
    bool OpenSettingsRequested,
    bool HideTabsWhenCollapsedRequested = false);

internal static class StatsPanel
{
    private static readonly Vector4 FrameBackgroundColor = new(0.10f, 0.10f, 0.10f, 0.65f);

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
            ImGui.TextDisabled(history.Count > 0
                ? "当前没有实时战斗数据，可点击下方历史记录查看。"
                : "等待战斗数据...");

            toggleNoCombatCollapseRequested = config.ShowDpsTab && ImGui.IsItemClicked();

            if (config.ShowHistoryTab)
            {
                ImGui.Spacing();
                DrawHistoryTab(statsService, config);
            }

            return new StatsPanelDrawResult(previousActiveTab, toggleNoCombatCollapseRequested, false, toggleNoCombatCollapseRequested);
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
                        textSelector: static c => c.EncHpsText ?? "0");
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
                        textSelector: static c => c.DtpsText ?? "0");
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
            if (!collapseToTabBar)
                DrawHistoryTab(statsService, config);
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
        var allRows = combatData.Msg!.Combatant.Values
            .Where(static c => !string.IsNullOrWhiteSpace(c.Name))
            .OrderByDescending(selector)
            .ToList();

        if (allRows.Count == 0)
        {
            ImGui.TextDisabled("没有可显示的数据。");
            return;
        }

        var rows = maxRows.HasValue
            ? allRows.Take(Math.Max(maxRows.Value, 1)).ToList()
            : allRows;
        var maxValue = allRows.Max(selector);
        var totalValue = allRows.Sum(selector);
        var playerColumnWidth = ResolvePlayerColumnWidth(rows, showSummaryRow ? summaryName : null, config);
        var deathColumnWidth = CalculateFixedTextColumnWidth("死亡");
        var fixedColumnWidth = ResolveFixedColumnWidth(config, 88f, valueColumnLabel);
        var rowHeight = ResolveRowHeight(config);
        var columnCount = 2;
        if (showJobColumn)
            columnCount++;
        if (showDamageColumn)
            columnCount++;
        if (showValueColumn)
            columnCount++;
        if (showDeathsColumn)
            columnCount++;

        if (!ImGui.BeginTable(
                $"##metric_{id}",
                columnCount,
                ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.Resizable))
        {
            return;
        }

        ImGui.TableSetupColumn("玩家", ImGuiTableColumnFlags.WidthFixed, playerColumnWidth);
        if (showJobColumn)
            ImGui.TableSetupColumn("职业", ImGuiTableColumnFlags.WidthFixed, ResolveFixedColumnWidth(config, 88f, "职业"));
        if (showDamageColumn)
            ImGui.TableSetupColumn(damageColumnLabel, ImGuiTableColumnFlags.WidthFixed, ResolveFixedColumnWidth(config, 88f, damageColumnLabel));
        if (showValueColumn)
            ImGui.TableSetupColumn(valueColumnLabel, ImGuiTableColumnFlags.WidthFixed, fixedColumnWidth);
        if (showDeathsColumn)
            ImGui.TableSetupColumn("死亡", ImGuiTableColumnFlags.WidthFixed, deathColumnWidth);
        ImGui.TableSetupColumn("占比", ImGuiTableColumnFlags.WidthStretch, Math.Max(180f, fixedColumnWidth * 1.8f));
        ImGui.TableHeadersRow();

        foreach (var combatant in rows)
        {
            var value = selector(combatant);
            var maxRatio = maxValue > 0 ? value / maxValue : 0d;
            var totalRatio = totalValue > 0 ? value / totalValue : 0d;
            var barColor = ResolveBarColor(combatant, config);

            TableNextRow(rowHeight);

            var columnIndex = 0;
            ImGui.TableSetColumnIndex(columnIndex++);
            ImGui.TextUnformatted(combatant.Name ?? string.Empty);

            if (showJobColumn)
            {
                ImGui.TableSetColumnIndex(columnIndex++);
                ImGui.TextUnformatted(string.IsNullOrWhiteSpace(combatant.Job) ? "-" : combatant.Job!);
            }

            if (showDamageColumn)
            {
                ImGui.TableSetColumnIndex(columnIndex++);
                ImGui.TextUnformatted(damageTextSelector?.Invoke(combatant) ?? "0");
            }

            if (showValueColumn)
            {
                ImGui.TableSetColumnIndex(columnIndex++);
                ImGui.TextUnformatted(textSelector(combatant));
            }

            if (showDeathsColumn)
            {
                ImGui.TableSetColumnIndex(columnIndex++);
                ImGui.TextUnformatted(string.IsNullOrWhiteSpace(combatant.DeathsText) ? "0" : combatant.DeathsText!);
            }

            ImGui.TableSetColumnIndex(columnIndex);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, FrameBackgroundColor);
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, barColor);
            ImGui.ProgressBar((float)Math.Clamp(maxRatio, 0d, 1d), new Vector2(-1f, 0f), $"{totalRatio:P1}");
            DrawCombatantBarTooltip(combatant);
            ImGui.PopStyleColor(2);
        }

        if (showSummaryRow)
        {
            TableNextRow(rowHeight);

            var columnIndex = 0;
            ImGui.TableSetColumnIndex(columnIndex++);
            ImGui.TextUnformatted(summaryName);

            if (showJobColumn)
            {
                ImGui.TableSetColumnIndex(columnIndex++);
                ImGui.TextUnformatted(summaryJob);
            }

            if (showDamageColumn)
            {
                ImGui.TableSetColumnIndex(columnIndex++);
                ImGui.TextUnformatted(string.IsNullOrWhiteSpace(summaryDamageText) ? "0" : summaryDamageText);
            }

            if (showValueColumn)
            {
                ImGui.TableSetColumnIndex(columnIndex++);
                ImGui.TextUnformatted(string.IsNullOrWhiteSpace(summaryValueText) ? "0" : summaryValueText);
            }

            if (showDeathsColumn)
            {
                ImGui.TableSetColumnIndex(columnIndex++);
                ImGui.TextUnformatted(string.IsNullOrWhiteSpace(summaryDeathsText) ? "0" : summaryDeathsText);
            }

            ImGui.TableSetColumnIndex(columnIndex);
            ImGui.TextUnformatted(string.Empty);
        }

        ImGui.EndTable();
    }

    private static void DrawOverviewTab(CombatDataWrapper combatData, PluginConfiguration config)
    {
        if (!ImGui.BeginChild("##overview_scroll", new Vector2(0f, 0f), false))
            return;

        var encounter = combatData.Msg!.Encounter!;
        ImGui.TextUnformatted($"区域: {encounter.CurrentZoneName ?? "Unknown"}");
        ImGui.TextUnformatted($"战斗时长: {encounter.DurationText ?? "00:00"}");
        ImGui.Separator();

        if (ImGui.BeginTable("##overview_summary", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH))
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

            if (!ImGui.BeginTable($"##combatant_{combatant.Name}", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH))
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

    private static void DrawHistoryTab(LocalStatsService statsService, PluginConfiguration config)
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

        var history = statsService.HistoricalRecords;
        if (history.Count == 0)
        {
            ImGui.TextDisabled("暂无历史记录。");
            return;
        }

        if (!ImGui.BeginChild("##history_scroll", new Vector2(0f, 320f), false))
            return;

        if (!ImGui.BeginTable("##history", 4, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.ScrollX | ImGuiTableFlags.Resizable))
        {
            ImGui.EndChild();
            return;
        }

        ImGui.TableSetupColumn("区域", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("开始时间", ImGuiTableColumnFlags.WidthFixed, ResolveFixedColumnWidth(config, 180f, "开始时间"));
        ImGui.TableSetupColumn("结束时间", ImGuiTableColumnFlags.WidthFixed, ResolveFixedColumnWidth(config, 180f, "结束时间"));
        ImGui.TableSetupColumn("时长", ImGuiTableColumnFlags.WidthFixed, ResolveFixedColumnWidth(config, 100f, "时长"));
        ImGui.TableHeadersRow();

        var rowHeight = ResolveRowHeight(config);
        var selectedIndex = statsService.SelectedHistoricalRecordIndex;
        for (var index = history.Count - 1; index >= 0; index--)
        {
            var record = history[index];
            TableNextRow(rowHeight);
            if (index == selectedIndex)
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new Vector4(0.22f, 0.34f, 0.54f, 0.35f)));

            ImGui.TableSetColumnIndex(0);
            DrawHistoryCell(record.ZoneName, index, statsService);

            ImGui.TableSetColumnIndex(1);
            DrawHistoryCell(FormatHistoryTimestamp(record.StartTimeUtc), index, statsService);

            ImGui.TableSetColumnIndex(2);
            DrawHistoryCell(FormatHistoryTimestamp(record.EndTimeUtc), index, statsService);

            ImGui.TableSetColumnIndex(3);
            DrawHistoryCell(record.Duration, index, statsService);
        }

        ImGui.EndTable();
        ImGui.EndChild();
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

    private static void DrawHistoryCell(string? value, int index, LocalStatsService statsService)
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
            statsService.LoadHistoricalRecord(index);
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

    private static void DrawCombatantBarTooltip(Combatant combatant)
    {
        if (!ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        ImGui.TextUnformatted($"玩家: {FallbackText(combatant.Name, "-")}");
        ImGui.TextUnformatted($"职业: {FallbackText(combatant.Job, "-")}");
        ImGui.TextUnformatted($"伤害量: {FallbackText(combatant.DamageText, "0")}");
        ImGui.TextUnformatted($"秒伤: {FallbackText(combatant.EncDpsText, "0")}");
        ImGui.TextUnformatted($"死亡: {FallbackText(combatant.DeathsText, "0")}");
        ImGui.EndTooltip();
    }

    private static string FormatMetricValue(double value)
        => value.ToString("0", CultureInfo.InvariantCulture);

    private static string FallbackText(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static float ResolvePlayerColumnWidth(
        IReadOnlyCollection<Combatant> rows,
        string? extraLabel,
        PluginConfiguration config)
    {
        var autoWidth = CalculatePlayerColumnWidth(rows, extraLabel);
        return config.FloatingStatsPlayerColumnMinWidth > 0f
            ? Math.Max(autoWidth, config.FloatingStatsPlayerColumnMinWidth)
            : autoWidth;
    }

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
}
