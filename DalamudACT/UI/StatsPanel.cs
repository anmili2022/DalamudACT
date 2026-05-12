using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

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
    private enum FloatingCombatantKind
    {
        Unknown,
        Player,
        FriendlyNpc,
        HostileNpc,
    }

    private readonly record struct DisplayCombatantRow(Combatant Combatant, FloatingCombatantKind Kind);

    private const uint InvalidActorId = 0xE0000000;
    private const float MinimumDeathsColumnWidth = 20f;
    private static readonly Vector4 FrameBackgroundColor = new(0.10f, 0.10f, 0.10f, 0.65f);
    private static readonly Vector4 FriendlyNpcBarColor = new(0.34f, 0.78f, 0.49f, 0.92f);
    private static readonly Vector4 HostileNpcBarColor = new(0.95f, 0.40f, 0.25f, 0.92f);
    private static readonly Vector4 FriendlyNpcTextColor = new(0.72f, 1.00f, 0.78f, 1.00f);
    private static readonly Vector4 HostileNpcTextColor = new(1.00f, 0.72f, 0.60f, 1.00f);
    private static readonly Vector4 FriendlyNpcRowBackgroundColor = new(0.08f, 0.26f, 0.10f, 0.22f);
    private static readonly Vector4 HostileNpcRowBackgroundColor = new(0.36f, 0.10f, 0.08f, 0.28f);
    private static readonly Vector4 IkegamiCardBackgroundColor = new(1.00f, 1.00f, 1.00f, 0.035f);
    private static readonly Vector4 IkegamiNameBackgroundColor = new(1.00f, 1.00f, 1.00f, 0.06f);
    private static readonly Vector4 IkegamiBodyBackgroundColor = new(0.00f, 0.00f, 0.00f, 0.14f);
    private static readonly Vector4 IkegamiContentBackgroundColor = new(0.07f, 0.09f, 0.14f, 0.78f);
    private static readonly Vector4 IkegamiCardBorderColor = new(1.00f, 1.00f, 1.00f, 0.12f);
    private static readonly Vector4 IkegamiHeaderTextColor = new(1.00f, 1.00f, 1.00f, 0.98f);
    private static readonly Vector4 IkegamiMutedTextColor = new(1.00f, 1.00f, 1.00f, 0.88f);
    private static readonly Vector4 IkegamiFooterBackgroundColor = new(0.05f, 0.07f, 0.11f, 0.75f);
    private static readonly Vector4 IkegamiEncounterTimeTextColor = new(0.49f, 0.83f, 0.99f, 1.00f);
    private const float IkegamiCardSpacing = 8f;
    private const float IkegamiNameBottomSpacing = 1f;
    private const float IkegamiEncounterFooterHeight = 24f;
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
            DrawNoCombatPlaceholder(statsService, history.Count > 0);

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

        var ikegamiTabFontScale = config.FloatingStatsDisplayStyle == FloatingStatsDisplayStyle.Ikegami
            ? Math.Clamp(config.FloatingStatsIkegamiTabFontScale, 0.6f, 2.0f)
            : 1f;
        if (ikegamiTabFontScale != 1f)
            ImGui.SetWindowFontScale(ikegamiTabFontScale);

        if (!ImGui.BeginTabBar("##stats_tabs"))
        {
            if (ikegamiTabFontScale != 1f)
                ImGui.SetWindowFontScale(1f);

            return new StatsPanelDrawResult(previousActiveTab, false, false, false);
        }

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
                    DrawNoCombatPlaceholder(statsService, hasHistory: false);
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
                    DrawNoCombatPlaceholder(statsService, hasHistory: false);
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
                    DrawNoCombatPlaceholder(statsService, hasHistory: false);
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
                    DrawNoCombatPlaceholder(statsService, hasHistory: false);
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
        if (ikegamiTabFontScale != 1f)
            ImGui.SetWindowFontScale(1f);
        return new StatsPanelDrawResult(activeTab, toggleDpsCollapseRequested, openSettingsRequested, false);
    }

    private static void DrawNoCombatPlaceholder(LocalStatsService statsService, bool hasHistory)
    {
        ImGui.TextDisabled(statsService.StatusText);

        if (!hasHistory || statsService.StatusText.Contains("正在收集新战斗数据", StringComparison.Ordinal))
            return;

        ImGui.TextDisabled("可点击下方历史记录查看。");
    }

    private static void DrawDpsTab(CombatDataWrapper combatData, PluginConfiguration config)
    {
        var visibleRows = GetVisibleCombatantRows(combatData, config);
        var nonHostileCombatants = visibleRows
            .Where(static row => row.Kind != FloatingCombatantKind.HostileNpc)
            .Select(static row => row.Combatant)
            .OrderByDescending(static combatant => ParseMetric(combatant.EncDpsText))
            .ThenBy(static combatant => combatant.Name, StringComparer.Ordinal)
            .ToList();
        var hostileCombatants = visibleRows
            .Where(static row => row.Kind == FloatingCombatantKind.HostileNpc)
            .Select(static row => row.Combatant)
            .OrderByDescending(static combatant => ParseMetric(combatant.EncDpsText))
            .ThenBy(static combatant => combatant.Name, StringComparer.Ordinal)
            .ToList();
        var orderedVisibleCombatants = nonHostileCombatants
            .Concat(hostileCombatants)
            .ToList();

        var totalDps = nonHostileCombatants
            .Sum(static c => ParseMetric(c.EncDpsText));
        var totalDamage = FormatCompactAmount(nonHostileCombatants.Sum(static c => ParseLocalizedAmount(c.DamageText)));
        var totalDeaths = nonHostileCombatants
            .Sum(static c => ParseCount(c.DeathsText));

        DrawMetricTab(
            id: "dps",
            valueColumnLabel: "\u79d2\u4f24",
            combatData: combatData,
            config: config,
            sourceRows: orderedVisibleCombatants,
            selector: static c => ParseMetric(c.EncDpsText),
            textSelector: static c => c.EncDpsText ?? "0",
            tooltipPrimaryLabel: "\u4f24\u5bb3\u91cf",
            tooltipPrimaryTextSelector: static c => c.DamageText ?? "0",
            tooltipRateLabel: "\u79d2\u4f24",
            tooltipRateTextSelector: static c => c.EncDpsText ?? "0",
            showPlayerColumn: config.ShowDpsPlayerColumn,
            showJobColumn: config.ShowDpsJobColumn,
            showDamageColumn: config.ShowDpsDamageColumn,
            damageColumnLabel: "\u4f24\u5bb3\u91cf",
            damageTextSelector: static c => c.DamageText ?? "0",
            showValueColumn: config.ShowDpsValueColumn,
            showDeathsColumn: config.ShowDpsDeathsColumn,
            maxRows: config.DpsVisibleCount,
            showSummaryRow: true,
            summaryName: "\u603bDPS",
            summaryJob: "\u5168\u961f",
            summaryDamageText: totalDamage,
            summaryValueText: FormatMetricValue(totalDps),
            summaryDeathsText: totalDeaths.ToString(CultureInfo.InvariantCulture),
            keepSourceOrder: true,
            summaryRowInsertIndex: nonHostileCombatants.Count);
    }

    private static void DrawMetricTab(
        string id,
        string valueColumnLabel,
        CombatDataWrapper combatData,
        PluginConfiguration config,
        Func<Combatant, double> selector,
        Func<Combatant, string> textSelector,
        string tooltipPrimaryLabel = "\u4f24\u5bb3\u91cf",
        Func<Combatant, string>? tooltipPrimaryTextSelector = null,
        string tooltipRateLabel = "\u79d2\u4f24",
        Func<Combatant, string>? tooltipRateTextSelector = null,
        IReadOnlyList<Combatant>? sourceRows = null,
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
        string? summaryDeathsText = null,
        bool keepSourceOrder = false,
        int? summaryRowInsertIndex = null)
    {
        switch (config.FloatingStatsDisplayStyle)
        {
            case FloatingStatsDisplayStyle.Ikegami:
                DrawIkegamiMetricTab(
                    id,
                    valueColumnLabel,
                    combatData,
                    config,
                    selector,
                    textSelector,
                    tooltipPrimaryLabel,
                    tooltipPrimaryTextSelector,
                    tooltipRateLabel,
                    tooltipRateTextSelector,
                    sourceRows,
                    showPlayerColumn,
                    showJobColumn,
                    showDamageColumn,
                    damageColumnLabel,
                    damageTextSelector,
                    showValueColumn,
                    showDeathsColumn,
                    maxRows,
                    showSummaryRow,
                    summaryName,
                    summaryJob,
                    summaryDamageText,
                    summaryValueText,
                    summaryDeathsText,
                    keepSourceOrder,
                    summaryRowInsertIndex);
                return;
            default:
                DrawClassicMetricTab(
                    id,
                    valueColumnLabel,
                    combatData,
                    config,
                    selector,
                    textSelector,
                    tooltipPrimaryLabel,
                    tooltipPrimaryTextSelector,
                    tooltipRateLabel,
                    tooltipRateTextSelector,
                    sourceRows,
                    showPlayerColumn,
                    showJobColumn,
                    showDamageColumn,
                    damageColumnLabel,
                    damageTextSelector,
                    showValueColumn,
                    showDeathsColumn,
                    maxRows,
                    showSummaryRow,
                    summaryName,
                    summaryJob,
                    summaryDamageText,
                    summaryValueText,
                    summaryDeathsText,
                    keepSourceOrder,
                    summaryRowInsertIndex);
                return;
        }
    }

    private static void DrawClassicMetricTab(
        string id,
        string valueColumnLabel,
        CombatDataWrapper combatData,
        PluginConfiguration config,
        Func<Combatant, double> selector,
        Func<Combatant, string> textSelector,
        string tooltipPrimaryLabel = "\u4f24\u5bb3\u91cf",
        Func<Combatant, string>? tooltipPrimaryTextSelector = null,
        string tooltipRateLabel = "\u79d2\u4f24",
        Func<Combatant, string>? tooltipRateTextSelector = null,
        IReadOnlyList<Combatant>? sourceRows = null,
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
        string? summaryDeathsText = null,
        bool keepSourceOrder = false,
        int? summaryRowInsertIndex = null)
    {
        if (!ImGui.BeginChild($"##metric_{id}_scroll", new Vector2(0f, 0f), false))
            return;

        var sourceCombatants = sourceRows ?? GetVisibleCombatants(combatData, config);
        var allRows = keepSourceOrder
            ? sourceCombatants.ToList()
            : sourceCombatants
                .OrderByDescending(selector)
                .ToList();

        if (allRows.Count == 0)
        {
            ImGui.TextDisabled("\u6ca1\u6709\u53ef\u663e\u793a\u7684\u6570\u636e\u3002");
            ImGui.EndChild();
            ImGui.PopStyleColor();
            return;
        }

        var rows = maxRows.HasValue
            ? allRows.Take(Math.Max(maxRows.Value, 1)).ToList()
            : allRows;
        var maxValue = allRows.Max(selector);
        var totalValue = allRows.Sum(selector);
        var effectiveSummaryRowInsertIndex = showSummaryRow
            ? Math.Clamp(summaryRowInsertIndex ?? rows.Count, 0, rows.Count)
            : rows.Count;
        var playerColumnWidth = ResolvePlayerColumnWidth(rows, showSummaryRow ? summaryName : null, config);
        var jobColumnWidth = ResolveMetricColumnWidth(config.FloatingStatsJobColumnWidth, config, 88f, "\u804c\u4e1a");
        var damageColumnWidth = ResolveMetricColumnWidth(config.FloatingStatsDamageColumnWidth, config, 88f, damageColumnLabel);
        var fixedColumnWidth = ResolveMetricColumnWidth(config.FloatingStatsValueColumnWidth, config, 88f, valueColumnLabel);
        var deathColumnWidth = ResolveDeathColumnWidth(config.FloatingStatsDeathsColumnWidth, config);
        var rowHeight = ResolveRowHeight(config);
        var layoutSignature = BuildMetricLayoutSignature(
            config.FloatingStatsDisplayStyle,
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
                "\u73a9\u5bb6",
                playerColumnWidth,
                ImGuiTableColumnFlags.WidthFixed));
        }

        if (showJobColumn)
        {
            jobColumnIndex = nextColumnIndex;
            visibleColumns.Add(new VisibleMetricColumn(
                MetricColumnSlot.Job,
                nextColumnIndex++,
                "\u804c\u4e1a",
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
                "\u6b7b",
                deathColumnWidth,
                ImGuiTableColumnFlags.WidthFixed));
        }

        var shareColumnIndex = nextColumnIndex;
        visibleColumns.Add(new VisibleMetricColumn(
            MetricColumnSlot.Share,
            shareColumnIndex,
            "\u5360\u6bd4",
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

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            if (showSummaryRow && rowIndex == effectiveSummaryRowInsertIndex)
            {
                DrawMetricSummaryRow(
                    rowHeight,
                    showPlayerColumn,
                    playerColumnIndex,
                    summaryName,
                    showJobColumn,
                    jobColumnIndex,
                    summaryJob,
                    showDamageColumn,
                    damageColumnIndex,
                    summaryDamageText,
                    showValueColumn,
                    valueColumnIndex,
                    summaryValueText,
                    showDeathsColumn,
                    deathsColumnIndex,
                    summaryDeathsText,
                    shareColumnIndex);
            }

            var combatant = rows[rowIndex];
            var value = selector(combatant);
            var maxRatio = maxValue > 0 ? value / maxValue : 0d;
            var totalRatio = totalValue > 0 ? value / totalValue : 0d;
            var barColor = ResolveBarColor(combatant, config);
            var hasCustomTextColor = TryResolveCombatantTextColor(combatant, config, out var rowTextColor);

            TableNextRow(rowHeight);
            if (TryResolveCombatantRowBackgroundColor(combatant, config, out var rowBackgroundColor))
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(rowBackgroundColor));

            if (hasCustomTextColor)
                ImGui.PushStyleColor(ImGuiCol.Text, rowTextColor);

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

            if (hasCustomTextColor)
                ImGui.PopStyleColor();
        }

        if (showSummaryRow && effectiveSummaryRowInsertIndex >= rows.Count)
        {
            DrawMetricSummaryRow(
                rowHeight,
                showPlayerColumn,
                playerColumnIndex,
                summaryName,
                showJobColumn,
                jobColumnIndex,
                summaryJob,
                showDamageColumn,
                damageColumnIndex,
                summaryDamageText,
                showValueColumn,
                valueColumnIndex,
                summaryValueText,
                showDeathsColumn,
                deathsColumnIndex,
                summaryDeathsText,
                shareColumnIndex);
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

    private static void DrawMetricSummaryRow(
        float rowHeight,
        bool showPlayerColumn,
        int? playerColumnIndex,
        string summaryName,
        bool showJobColumn,
        int? jobColumnIndex,
        string summaryJob,
        bool showDamageColumn,
        int? damageColumnIndex,
        string? summaryDamageText,
        bool showValueColumn,
        int? valueColumnIndex,
        string? summaryValueText,
        bool showDeathsColumn,
        int? deathsColumnIndex,
        string? summaryDeathsText,
        int shareColumnIndex)
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

    private static void DrawIkegamiMetricTab(
        string id,
        string valueColumnLabel,
        CombatDataWrapper combatData,
        PluginConfiguration config,
        Func<Combatant, double> selector,
        Func<Combatant, string> textSelector,
        string tooltipPrimaryLabel = "\u4f24\u5bb3\u91cf",
        Func<Combatant, string>? tooltipPrimaryTextSelector = null,
        string tooltipRateLabel = "\u79d2\u4f24",
        Func<Combatant, string>? tooltipRateTextSelector = null,
        IReadOnlyList<Combatant>? sourceRows = null,
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
        string? summaryDeathsText = null,
        bool keepSourceOrder = false,
        int? summaryRowInsertIndex = null)
    {
        var ikegamiContentBackgroundAlpha = Math.Clamp(config.FloatingStatsIkegamiContentBackgroundAlpha, 0f, 1f);
        var metricScrollFlags = config.FloatingStatsIkegamiShowVerticalScrollbar ? ImGuiWindowFlags.None : ImGuiWindowFlags.NoScrollbar;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, WithAlphaMultiplier(IkegamiContentBackgroundColor, ikegamiContentBackgroundAlpha));
        if (!ImGui.BeginChild($"##metric_{id}_scroll", new Vector2(0f, 0f), false, metricScrollFlags))
        {
            ImGui.PopStyleColor();
            return;
        }

        var sourceCombatants = sourceRows ?? GetVisibleCombatants(combatData, config);
        var allRows = keepSourceOrder
            ? sourceCombatants.ToList()
            : sourceCombatants
                .OrderByDescending(selector)
                .ToList();

        if (allRows.Count == 0)
        {
            ImGui.TextDisabled("\u6ca1\u6709\u53ef\u663e\u793a\u7684\u6570\u636e\u3002");
            ImGui.EndChild();
            ImGui.PopStyleColor();
            return;
        }

        var rows = maxRows.HasValue
            ? allRows.Take(Math.Max(maxRows.Value, 1)).ToList()
            : allRows;
        var totalValue = allRows.Sum(selector);
        var totalValueText = !string.IsNullOrWhiteSpace(summaryValueText)
            ? summaryValueText
            : FormatMetricValue(totalValue);
        var footerMetricText = ResolveIkegamiFooterMetricText(id, valueColumnLabel, totalValueText);
        var ikegamiPanelRaise = Math.Clamp(config.FloatingStatsIkegamiPanelRaise, 0f, 60f);
        var ikegamiDetailRaise = Math.Clamp(config.FloatingStatsIkegamiDetailRaise, 0f, 60f);
        var ikegamiFooterRaise = Math.Clamp(config.FloatingStatsIkegamiFooterRaise, 0f, 80f);
        var ikegamiShowScrollbar = config.FloatingStatsIkegamiShowScrollbar;
        var ikegamiShowMaxHitDetail = config.FloatingStatsIkegamiShowMaxHitDetail;
        var ikegamiShowNameLine = config.FloatingStatsIkegamiShowNameLine;
        var ikegamiBoxWidth = Math.Clamp(config.FloatingStatsIkegamiBoxWidth, 1f, 260f);
        var ikegamiBoxHeight = Math.Clamp(config.FloatingStatsIkegamiBoxHeight, 1f, 140f);
        var ikegamiNameHeight = Math.Clamp(config.FloatingStatsIkegamiNameHeight, 16f, 40f);
        var ikegamiHeaderHeight = Math.Clamp(config.FloatingStatsIkegamiHeaderHeight, 20f, 80f);
        ikegamiHeaderHeight = Math.Min(ikegamiHeaderHeight, Math.Max(1f, ikegamiBoxHeight));
        var ikegamiHeaderLeftPadding = Math.Clamp(config.FloatingStatsIkegamiHeaderLeftPadding, 0f, 32f);
        var ikegamiDetailLeftPadding = Math.Clamp(config.FloatingStatsIkegamiDetailLeftPadding, 0f, 32f);
        var ikegamiNameLeftPadding = Math.Clamp(config.FloatingStatsIkegamiNameLeftPadding, 0f, 40f);
        var ikegamiNameRightPadding = Math.Clamp(config.FloatingStatsIkegamiNameRightPadding, 0f, 40f);
        var ikegamiJobBadgeSize = Math.Clamp(config.FloatingStatsIkegamiJobBadgeSize, 12f, 36f);
        var ikegamiNameAlpha = Math.Clamp(config.FloatingStatsIkegamiNameAlpha, 0f, 1f);
        var ikegamiNameBackgroundAlpha = Math.Clamp(config.FloatingStatsIkegamiNameBackgroundAlpha, 0f, 1f);
        var ikegamiHeaderAlpha = Math.Clamp(config.FloatingStatsIkegamiHeaderAlpha, 0f, 1f);
        var ikegamiPanelBackgroundAlpha = Math.Clamp(config.FloatingStatsIkegamiPanelBackgroundAlpha, 0f, 1f);
        var ikegamiBodyAlpha = Math.Clamp(config.FloatingStatsIkegamiBodyAlpha, 0f, 1f);
        var ikegamiBodyBackgroundAlpha = Math.Clamp(config.FloatingStatsIkegamiBodyBackgroundAlpha, 0f, 1f);
        var ikegamiFooterAlpha = Math.Clamp(config.FloatingStatsIkegamiFooterAlpha, 0f, 1f);
        var ikegamiFooterHeight = Math.Clamp(config.FloatingStatsIkegamiFooterHeight, 18f, 48f);
        var ikegamiFooterTimeZoneSpacing = Math.Clamp(config.FloatingStatsIkegamiFooterTimeZoneSpacing, 0f, 32f);
        var ikegamiFooterRightPadding = Math.Clamp(config.FloatingStatsIkegamiFooterRightPadding, 0f, 40f);
        var ikegamiNameFontScale = Math.Clamp(config.FloatingStatsIkegamiNameFontScale, 0.6f, 2.0f);
        var ikegamiHeaderFontScale = Math.Clamp(config.FloatingStatsIkegamiHeaderFontScale, 0.6f, 2.0f);
        var ikegamiBodyFontScale = Math.Clamp(config.FloatingStatsIkegamiBodyFontScale, 0.6f, 2.0f);
        var ikegamiFooterFontScale = Math.Clamp(config.FloatingStatsIkegamiFooterFontScale, 0.6f, 2.0f);
        var ikegamiTooltipFontScale = Math.Clamp(config.FloatingStatsIkegamiTooltipFontScale, 0.6f, 2.0f);
        var ikegamiBoxAlignment = Enum.IsDefined(typeof(IkegamiBoxAlignment), config.FloatingStatsIkegamiBoxAlignment)
            ? config.FloatingStatsIkegamiBoxAlignment
            : IkegamiBoxAlignment.Left;
        var ikegamiCardHeight = (ikegamiShowNameLine ? ikegamiNameHeight + IkegamiNameBottomSpacing : 0f) + ikegamiBoxHeight;
        var stripHeight = ikegamiCardHeight + (ikegamiShowScrollbar ? ImGui.GetStyle().ScrollbarSize + 1f : 1f);
        var stripFlags = ikegamiShowScrollbar
            ? ImGuiWindowFlags.HorizontalScrollbar
            : ImGuiWindowFlags.NoScrollbar;
        var footerAlignedContentWidth = Math.Max(
            0f,
            Math.Max(
                ImGui.GetContentRegionAvail().X,
                ImGui.GetWindowWidth() - (ImGui.GetStyle().WindowPadding.X * 2f)));

        if (ImGui.BeginChild(
                $"##ikegami_strip_{id}",
                new Vector2(0f, stripHeight),
                false,
                stripFlags))
        {
            var stripStartX = ImGui.GetCursorPosX();
            var stripAvailableWidth = footerAlignedContentWidth > 0f
                ? footerAlignedContentWidth
                : Math.Max(
                    0f,
                    Math.Max(
                        ImGui.GetContentRegionAvail().X,
                        ImGui.GetWindowWidth() - (ImGui.GetStyle().WindowPadding.X * 2f)));
            var totalStripWidth = rows.Count > 0
                ? (rows.Count * ikegamiBoxWidth) + ((rows.Count - 1) * IkegamiCardSpacing)
                : 0f;
            var stripOffsetX = ResolveIkegamiStripOffset(ikegamiBoxAlignment, stripAvailableWidth, totalStripWidth);
            if (stripOffsetX > 0f)
                ImGui.SetCursorPosX(stripStartX + stripOffsetX);

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var combatant = rows[rowIndex];
                var barColor = ResolveBarColor(combatant, config);
                var hasCustomTextColor = TryResolveCombatantTextColor(combatant, config, out var rowTextColor);
                var primaryLabel = combatant.Name ?? string.Empty;
                var secondaryLabel = string.IsNullOrWhiteSpace(combatant.Job) ? null : combatant.Job;
                var jobBadgeText = ResolveIkegamiJobBadgeText(combatant);
                var detailText = ResolveIkegamiDetailText(
                    id,
                    combatant,
                    showJobColumn,
                    showDamageColumn,
                    damageColumnLabel,
                    damageTextSelector?.Invoke(combatant),
                    showDeathsColumn);
                if (id == "dps" && !ikegamiShowMaxHitDetail)
                    detailText = null;

                DrawIkegamiMetricCard(
                    $"{id}_{rowIndex}",
                    showJobColumn ? jobBadgeText : string.Empty,
                    ResolveIkegamiTitle(primaryLabel, secondaryLabel, showPlayerColumn, showJobColumn),
                    detailText,
                    ResolveIkegamiHeaderMetricText(
                        jobBadgeText,
                        textSelector(combatant),
                        ResolveIkegamiPrimaryMetricSuffix(id, valueColumnLabel)),
                    ikegamiBoxWidth,
                    ikegamiCardHeight,
                    ikegamiShowNameLine,
                    ikegamiNameHeight,
                    ikegamiBoxHeight,
                    ikegamiHeaderHeight,
                    ikegamiHeaderLeftPadding,
                    ikegamiDetailLeftPadding,
                    ikegamiNameLeftPadding,
                    ikegamiNameRightPadding,
                    ikegamiJobBadgeSize,
                    ikegamiPanelRaise,
                    ikegamiDetailRaise,
                    barColor,
                    TryResolveCombatantRowBackgroundColor(combatant, config, out var rowBackgroundColor)
                        ? rowBackgroundColor
                        : IkegamiCardBackgroundColor,
                    ikegamiNameAlpha,
                    ikegamiNameBackgroundAlpha,
                    ikegamiHeaderAlpha,
                    ikegamiPanelBackgroundAlpha,
                    ikegamiBodyAlpha,
                    ikegamiBodyBackgroundAlpha,
                    ikegamiNameFontScale,
                    ikegamiHeaderFontScale,
                    ikegamiBodyFontScale,
                    hasCustomTextColor,
                    rowTextColor,
                    id == "dps"
                        ? () => DrawIkegamiDpsTooltip(combatant, ikegamiTooltipFontScale)
                        : () => DrawIkegamiMetricTooltip(
                            combatant,
                            tooltipPrimaryLabel,
                            tooltipPrimaryTextSelector?.Invoke(combatant),
                            tooltipRateLabel,
                            tooltipRateTextSelector?.Invoke(combatant),
                            ikegamiTooltipFontScale));

                if (rowIndex < rows.Count - 1)
                    ImGui.SameLine(0f, IkegamiCardSpacing);
            }

            ImGui.EndChild();
        }

        ImGui.SetCursorPosY(Math.Max(0f, ImGui.GetCursorPosY() - ikegamiFooterRaise));
        DrawIkegamiEncounterFooter(
            id,
            combatData,
            footerMetricText,
            footerAlignedContentWidth,
            ikegamiFooterAlpha,
            ikegamiFooterHeight,
            ikegamiFooterTimeZoneSpacing,
            ikegamiFooterRightPadding,
            ikegamiFooterFontScale);

        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    private static void DrawIkegamiMetricCard(
        string id,
        string badgeText,
        string title,
        string? detailText,
        string primaryMetricDisplayText,
        float boxWidth,
        float cardHeight,
        bool showNameLine,
        float nameHeight,
        float boxHeight,
        float headerHeight,
        float headerLeftPadding,
        float detailLeftPadding,
        float nameLeftPadding,
        float nameRightPadding,
        float jobBadgeSize,
        float panelRaise,
        float detailRaise,
        Vector4 barColor,
        Vector4 backgroundColor,
        float nameAlpha,
        float nameBackgroundAlpha,
        float headerAlpha,
        float panelBackgroundAlpha,
        float bodyAlpha,
        float bodyBackgroundAlpha,
        float nameFontScale,
        float headerFontScale,
        float bodyFontScale,
        bool hasCustomTextColor,
        Vector4 rowTextColor,
        Action drawTooltip)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        if (ImGui.BeginChild(
                $"##ikegami_card_{id}",
                new Vector2(boxWidth, cardHeight),
                false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (showNameLine)
            {
                DrawIkegamiNameLine(
                id,
                badgeText,
                    title,
                    nameHeight,
                    nameAlpha,
                    nameBackgroundAlpha,
                    nameLeftPadding,
                    nameRightPadding,
                    jobBadgeSize,
                    nameFontScale,
                    hasCustomTextColor,
                    rowTextColor);

                ImGui.Dummy(new Vector2(0f, IkegamiNameBottomSpacing));
            }

            if (panelRaise > 0f)
                ImGui.SetCursorPosY(Math.Max(0f, ImGui.GetCursorPosY() - panelRaise));

            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 7f);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1f);
            ImGui.PushStyleColor(ImGuiCol.ChildBg, WithAlphaMultiplier(backgroundColor, panelBackgroundAlpha));
            ImGui.PushStyleColor(ImGuiCol.Border, IkegamiCardBorderColor);
            if (ImGui.BeginChild(
                    $"##ikegami_card_panel_{id}",
                    new Vector2(-1f, boxHeight),
                    true,
                    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 7f);
                ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 0f);
                ImGui.PushStyleColor(ImGuiCol.ChildBg, WithAlphaMultiplier(barColor, headerAlpha));
                if (ImGui.BeginChild(
                        $"##ikegami_card_header_{id}",
                        new Vector2(-1f, headerHeight),
                        false,
                        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
                {
                    if (headerFontScale != 1f)
                        ImGui.SetWindowFontScale(headerFontScale);

                    ImGui.PushStyleColor(ImGuiCol.Text, IkegamiHeaderTextColor);
                    ImGui.SetCursorPosY(Math.Max(0f, ((headerHeight - ImGui.GetTextLineHeight()) * 0.5f) - 1f));
                    DrawLeftAlignedTextLine(primaryMetricDisplayText, headerLeftPadding);
                    ImGui.PopStyleColor();

                    if (headerFontScale != 1f)
                        ImGui.SetWindowFontScale(1f);
                }

                ImGui.EndChild();
                drawTooltip();
                ImGui.PopStyleColor();
                ImGui.PopStyleVar(2);

                var bodyHeight = Math.Max(0f, boxHeight - headerHeight);
                if (!string.IsNullOrWhiteSpace(detailText) && bodyHeight > 0f)
                {
                    ImGui.PushStyleColor(ImGuiCol.ChildBg, WithAlphaMultiplier(IkegamiBodyBackgroundColor, bodyBackgroundAlpha));
                    if (ImGui.BeginChild(
                            $"##ikegami_card_body_{id}",
                            new Vector2(-1f, bodyHeight),
                            false,
                            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
                    {
                        if (bodyFontScale != 1f)
                            ImGui.SetWindowFontScale(bodyFontScale);

                        var bodyTextY = Math.Max(0f, ((bodyHeight - ImGui.GetTextLineHeight()) * 0.5f) - 1f) - detailRaise;
                        var bodyTextColor = hasCustomTextColor ? rowTextColor : IkegamiMutedTextColor;
                        ImGui.PushStyleColor(ImGuiCol.Text, WithAlphaMultiplier(bodyTextColor, bodyAlpha));
                        ImGui.SetCursorPos(new Vector2(detailLeftPadding, Math.Max(0f, bodyTextY)));
                        ImGui.TextUnformatted(detailText);
                        ImGui.PopStyleColor();

                        if (bodyFontScale != 1f)
                            ImGui.SetWindowFontScale(1f);
                    }

                    ImGui.EndChild();
                    ImGui.PopStyleColor();
                }
            }

            ImGui.EndChild();
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(2);
        }

        ImGui.EndChild();
        ImGui.PopStyleVar();
    }

    private static string ResolveIkegamiTitle(
        string? primaryLabel,
        string? secondaryLabel,
        bool showPlayerColumn,
        bool showJobColumn)
    {
        if (showPlayerColumn && !string.IsNullOrWhiteSpace(primaryLabel))
            return primaryLabel!;

        if (showJobColumn && !string.IsNullOrWhiteSpace(secondaryLabel))
            return secondaryLabel!;

        if (!string.IsNullOrWhiteSpace(primaryLabel))
            return primaryLabel!;

        return string.IsNullOrWhiteSpace(secondaryLabel) ? "-" : secondaryLabel!;
    }

    private static float ResolveIkegamiStripOffset(
        IkegamiBoxAlignment alignment,
        float availableWidth,
        float totalWidth)
    {
        if (availableWidth <= 0f || totalWidth <= 0f || totalWidth >= availableWidth)
            return 0f;

        var remainingWidth = availableWidth - totalWidth;
        return alignment switch
        {
            IkegamiBoxAlignment.Center => remainingWidth * 0.5f,
            IkegamiBoxAlignment.Right => remainingWidth,
            _ => 0f,
        };
    }

    private static string? ResolveIkegamiSubtitle(
        string? primaryLabel,
        string? secondaryLabel,
        bool showPlayerColumn,
        bool showJobColumn,
        bool showDamageColumn,
        string damageColumnLabel,
        string? damageText,
        bool showDeathsColumn,
        string? deathsText)
    {
        var segments = new List<string>(4);

        if (showPlayerColumn && showJobColumn && !string.IsNullOrWhiteSpace(secondaryLabel))
            segments.Add(secondaryLabel!);
        else if (!showPlayerColumn && showJobColumn && !string.IsNullOrWhiteSpace(primaryLabel))
            segments.Add(primaryLabel!);

        if (showDamageColumn && !string.IsNullOrWhiteSpace(damageText))
            segments.Add($"{damageColumnLabel} {damageText}");

        if (showDeathsColumn)
            segments.Add($"死亡 {FormatEmptyAsZero(deathsText)}");

        return segments.Count > 0 ? string.Join(" · ", segments) : null;
    }

    private static string? ResolveIkegamiDetailText(
        string id,
        Combatant combatant,
        bool showJobColumn,
        bool showDamageColumn,
        string damageColumnLabel,
        string? damageText,
        bool showDeathsColumn)
    {
        _ = showJobColumn;
        _ = showDeathsColumn;
        return ResolveIkegamiPrimaryDetailText(id, combatant, showDamageColumn, damageColumnLabel, damageText);
    }

    private static string ResolveIkegamiJobBadgeText(Combatant combatant)
    {
        if (!string.IsNullOrWhiteSpace(combatant.Job))
            return combatant.Job![0].ToString();

        if (TryParseFloatingCombatantKind(combatant.ParticipantKind, out var kind))
        {
            return kind switch
            {
                FloatingCombatantKind.FriendlyNpc => "友",
                FloatingCombatantKind.HostileNpc => "敌",
                _ => "?"
            };
        }

        return "?";
    }

    private static string ResolveIkegamiPrimaryMetricSuffix(string id, string valueColumnLabel)
        => id switch
        {
            "dps" => "DPS",
            "hps" => "HPS",
            "taken" => "DTPS",
            _ => valueColumnLabel,
        };

    private static string ResolveIkegamiPrimaryMetricText(
        string? valueText,
        string metricSuffix)
    {
        var metricText = FormatEmptyAsFallback(valueText, "0");
        return string.IsNullOrWhiteSpace(metricSuffix)
            ? metricText
            : $"{metricText} {metricSuffix}";
    }

    private static string ResolveIkegamiHeaderMetricText(
        string? badgeText,
        string? valueText,
        string metricSuffix)
    {
        var metricText = ResolveIkegamiPrimaryMetricText(valueText, metricSuffix);
        return string.IsNullOrWhiteSpace(badgeText)
            ? metricText
            : $"[{badgeText}] - {metricText}";
    }

    private static string ResolveIkegamiFooterMetricText(string id, string valueColumnLabel, string totalValueText)
    {
        var metricSuffix = ResolveIkegamiPrimaryMetricSuffix(id, valueColumnLabel);
        return string.IsNullOrWhiteSpace(metricSuffix)
            ? totalValueText
            : $"{totalValueText} {metricSuffix}";
    }

    private static string? ResolveIkegamiSummaryDetailText(
        string id,
        CombatDataWrapper combatData,
        bool showDamageColumn,
        string damageColumnLabel,
        string? summaryDamageText)
    {
        var encounter = combatData.Msg?.Encounter;
        var maxHit = JoinPair(encounter?.MaxHitText, encounter?.MaxHitValueText);
        if (id == "dps" && maxHit != "--")
            return maxHit;

        if (showDamageColumn && !string.IsNullOrWhiteSpace(summaryDamageText))
            return $"{damageColumnLabel} {summaryDamageText}";

        return maxHit == "--" ? null : maxHit;
    }

    private static string? ResolveIkegamiPrimaryDetailText(
        string id,
        Combatant combatant,
        bool showDamageColumn,
        string damageColumnLabel,
        string? damageText)
    {
        if (id == "dps")
        {
            if (!string.IsNullOrWhiteSpace(combatant.MaxHitText) && combatant.MaxHitText != "--")
                return combatant.MaxHitText;

            return null;
        }

        if (showDamageColumn && !string.IsNullOrWhiteSpace(damageText))
            return $"{damageColumnLabel} {damageText}";

        if (!string.IsNullOrWhiteSpace(combatant.MaxHitText) && combatant.MaxHitText != "--")
            return combatant.MaxHitText;

        return null;
    }

    private static void DrawIkegamiEncounterFooter(
        string id,
        CombatDataWrapper combatData,
        string footerMetricText,
        float alignedContentWidth,
        float footerAlpha,
        float footerHeight,
        float footerTimeZoneSpacing,
        float footerRightPadding,
        float footerFontScale)
    {
        const float IkegamiFooterWidth = 380f;
        var encounter = combatData.Msg?.Encounter;
        var durationText = encounter?.DurationText ?? "00:00";
        var zoneName = encounter?.CurrentZoneName ?? "Unknown";
        var rightText = footerMetricText;
        var startX = ImGui.GetCursorPosX();
        var availableWidth = alignedContentWidth > 0f
            ? alignedContentWidth
            : Math.Max(1f, ImGui.GetContentRegionAvail().X);
        var footerWidth = Math.Min(IkegamiFooterWidth, availableWidth);
        var footerOffsetX = Math.Max(0f, (availableWidth - footerWidth) * 0.5f);
        if (footerOffsetX > 0f)
            ImGui.SetCursorPosX(startX + footerOffsetX);

        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 0f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, WithAlphaMultiplier(IkegamiFooterBackgroundColor, footerAlpha));
        if (ImGui.BeginChild(
                $"##ikegami_footer_{id}",
                new Vector2(footerWidth, footerHeight),
                true,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (footerFontScale != 1f)
                ImGui.SetWindowFontScale(footerFontScale);

            if (ImGui.BeginTable(
                    $"##ikegami_footer_table_{id}",
                    2,
                    ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
            {
                ImGui.TableSetupColumn("##left", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("##right", ImGuiTableColumnFlags.WidthFixed, Math.Max(ImGui.CalcTextSize(rightText).X + footerRightPadding + 4f, 84f));
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.PushStyleColor(ImGuiCol.Text, WithAlphaMultiplier(IkegamiEncounterTimeTextColor, footerAlpha));
                ImGui.TextUnformatted(durationText);
                ImGui.PopStyleColor();
                ImGui.SameLine(0f, footerTimeZoneSpacing);
                ImGui.PushStyleColor(ImGuiCol.Text, WithAlphaMultiplier(ImGui.GetStyle().Colors[(int)ImGuiCol.Text], footerAlpha));
                ImGui.TextUnformatted(zoneName);
                ImGui.PopStyleColor();
                ImGui.TableSetColumnIndex(1);
                ImGui.PushStyleColor(ImGuiCol.Text, WithAlphaMultiplier(ImGui.GetStyle().Colors[(int)ImGuiCol.Text], footerAlpha));
                DrawRightAlignedTextLine(rightText, footerRightPadding);
                ImGui.PopStyleColor();
                ImGui.EndTable();
            }

            if (footerFontScale != 1f)
                ImGui.SetWindowFontScale(1f);
        }

        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar(2);
    }

    private static void DrawIkegamiJobBadge(string badgeText, float alpha, float badgeSize)
    {
        var drawList = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var size = new Vector2(badgeSize, badgeSize);
        var max = min + size;
        var background = WithAlphaMultiplier(new Vector4(1f, 1f, 1f, 0.12f), alpha);
        var border = WithAlphaMultiplier(new Vector4(1f, 1f, 1f, 0.35f), alpha);
        var rounding = MathF.Min(5f, badgeSize * 0.25f);
        drawList.AddRectFilled(min, max, ImGui.GetColorU32(background), rounding);
        drawList.AddRect(min, max, ImGui.GetColorU32(border), rounding);
        var textSize = ImGui.CalcTextSize(badgeText);
        var textPos = new Vector2(
            min.X + Math.Max(0f, (size.X - textSize.X) * 0.5f),
            min.Y + Math.Max(0f, (size.Y - textSize.Y) * 0.5f) - 1f);
        drawList.AddText(textPos, ImGui.GetColorU32(WithAlphaMultiplier(IkegamiHeaderTextColor, alpha)), badgeText);
        ImGui.Dummy(size);
    }

    private static void DrawIkegamiNameLine(
        string id,
        string badgeText,
        string title,
        float nameHeight,
        float alpha,
        float backgroundAlpha,
        float leftPadding,
        float rightPadding,
        float jobBadgeSize,
        float fontScale,
        bool hasCustomTextColor,
        Vector4 rowTextColor)
    {
        var resolvedTitle = string.IsNullOrWhiteSpace(title) ? "-" : title;
        var textColor = WithAlphaMultiplier(hasCustomTextColor ? rowTextColor : IkegamiHeaderTextColor, alpha);
        var hasBadge = !string.IsNullOrWhiteSpace(badgeText);
        var columnCount = hasBadge ? 2 : 1;
        var startX = ImGui.GetCursorPosX();
        var startY = ImGui.GetCursorPosY();
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var innerWidth = Math.Max(1f, availableWidth - leftPadding - rightPadding);
        ImGui.SetCursorPosX(startX + leftPadding);
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, Vector2.Zero);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, WithAlphaMultiplier(IkegamiNameBackgroundColor, backgroundAlpha));
        if (!ImGui.BeginChild(
                $"##ikegami_name_wrap_{id}",
                new Vector2(innerWidth, nameHeight),
                false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();
            ImGui.SetCursorPos(new Vector2(startX, startY + nameHeight));
            return;
        }

        if (fontScale != 1f)
            ImGui.SetWindowFontScale(fontScale);

        if (!ImGui.BeginTable(
                $"##ikegami_name_{id}",
                columnCount,
                ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
        {
            if (fontScale != 1f)
                ImGui.SetWindowFontScale(1f);
            ImGui.EndChild();
            ImGui.PopStyleColor();
            ImGui.PopStyleVar();
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            ImGui.TextUnformatted(resolvedTitle);
            ImGui.PopStyleColor();
            ImGui.SetCursorPos(new Vector2(startX, startY + nameHeight));
            return;
        }

        if (hasBadge)
            ImGui.TableSetupColumn("##badge", ImGuiTableColumnFlags.WidthFixed, jobBadgeSize + 4f);

        ImGui.TableSetupColumn("##label", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextRow();

        if (hasBadge)
        {
            ImGui.TableSetColumnIndex(0);
            DrawIkegamiJobBadge(badgeText, alpha, jobBadgeSize);
            ImGui.TableSetColumnIndex(1);
        }
        else
        {
            ImGui.TableSetColumnIndex(0);
        }

        ImGui.PushStyleColor(ImGuiCol.Text, textColor);
        ImGui.SetCursorPosY(Math.Max(0f, ((nameHeight - ImGui.GetTextLineHeight()) * 0.5f) - 1f));
        ImGui.TextUnformatted(resolvedTitle);
        ImGui.PopStyleColor();
        ImGui.EndTable();
        if (fontScale != 1f)
            ImGui.SetWindowFontScale(1f);
        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
        ImGui.SetCursorPos(new Vector2(startX, startY + nameHeight));
    }

    private static void DrawCenteredTextLine(string text)
    {
        var currentX = ImGui.GetCursorPosX();
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var textWidth = ImGui.CalcTextSize(text).X;
        ImGui.SetCursorPosX(currentX + Math.Max(0f, (availableWidth - textWidth) * 0.5f));
        ImGui.TextUnformatted(text);
    }

    private static Vector4 WithAlphaMultiplier(Vector4 color, float alphaMultiplier)
        => new(color.X, color.Y, color.Z, Math.Clamp(color.W * alphaMultiplier, 0f, 1f));

    private static void DrawLeftAlignedTextLine(string text, float leftPadding)
    {
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0f, leftPadding));
        ImGui.TextUnformatted(text);
    }

    private static void DrawRightAlignedTextLine(string text, float rightPadding)
    {
        var currentX = ImGui.GetCursorPosX();
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var textWidth = ImGui.CalcTextSize(text).X;
        var targetX = currentX + Math.Max(0f, availableWidth - textWidth - rightPadding);
        ImGui.SetCursorPosX(targetX);
        ImGui.TextUnformatted(text);
    }

    private static void DrawIkegamiMetricTooltip(
        Combatant combatant,
        string tooltipPrimaryLabel,
        string? tooltipPrimaryText,
        string tooltipRateLabel,
        string? tooltipRateText,
        float tooltipFontScale)
    {
        if (!ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        if (tooltipFontScale != 1f)
            ImGui.SetWindowFontScale(tooltipFontScale);
        ImGui.TextUnformatted($"{tooltipPrimaryLabel}: {FormatEmptyAsFallback(tooltipPrimaryText, "0")}");
        ImGui.TextUnformatted($"{tooltipRateLabel}: {FormatEmptyAsFallback(tooltipRateText, "0")}");
        var maxHitText = ResolveCombatantTooltipMaxHitText(combatant);
        if (!string.IsNullOrWhiteSpace(maxHitText))
            ImGui.TextUnformatted($"最高伤害：{maxHitText}");
        if (tooltipFontScale != 1f)
            ImGui.SetWindowFontScale(1f);
        ImGui.EndTooltip();
    }

    private static void DrawIkegamiDpsTooltip(Combatant combatant, float tooltipFontScale)
    {
        if (!ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        if (tooltipFontScale != 1f)
            ImGui.SetWindowFontScale(tooltipFontScale);
        ImGui.TextUnformatted($"总伤：{FormatEmptyAsFallback(combatant.DamageText, "0")}");
        ImGui.TextUnformatted($"暴击率：{ResolveCombatantCritRateText(combatant)}");
        ImGui.TextUnformatted($"直爆率：{ResolveCombatantCritDirectRateText(combatant)}");
        var maxHitText = ResolveCombatantTooltipMaxHitText(combatant);
        if (!string.IsNullOrWhiteSpace(maxHitText))
            ImGui.TextUnformatted($"最高伤害：{maxHitText}");
        if (tooltipFontScale != 1f)
            ImGui.SetWindowFontScale(1f);
        ImGui.EndTooltip();
    }

    private static string ResolveCombatantCritRateText(Combatant combatant)
    {
        var totalHits = ParseCount(combatant.HitsText);
        if (totalHits <= 0)
            return "0%";

        var critHits = ParseCount(combatant.CritHitsText);
        var critRate = Math.Clamp((critHits / (double)totalHits) * 100d, 0d, 100d);
        return $"{critRate:0.0}%";
    }

    private static string ResolveCombatantCritDirectRateText(Combatant combatant)
    {
        if (string.IsNullOrWhiteSpace(combatant.CritDirectHitsText))
            return "--";

        var totalHits = ParseCount(combatant.HitsText);
        if (totalHits <= 0)
            return "0%";

        var critDirectHits = ParseCount(combatant.CritDirectHitsText);
        var critDirectRate = Math.Clamp((critDirectHits / (double)totalHits) * 100d, 0d, 100d);
        return $"{critDirectRate:0.0}%";
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
        foreach (var combatant in GetVisibleCombatants(combatData, config))
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
            DrawOverviewRow("DoT总伤害", combatant.DotDamageText, config);
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
        if (config.HighlightNpcRows && TryParseFloatingCombatantKind(combatant.ParticipantKind, out var kind))
        {
            if (kind == FloatingCombatantKind.FriendlyNpc)
                return FriendlyNpcBarColor;

            if (kind == FloatingCombatantKind.HostileNpc)
                return HostileNpcBarColor;
        }

        if (config.BarColorMode == StatsBarColorMode.Single)
            return config.GetSingleBarColor();

        return config.GetThemeBarColor(combatant.Job);
    }

    private static bool TryResolveCombatantTextColor(Combatant combatant, PluginConfiguration config, out Vector4 color)
    {
        if (config.HighlightNpcRows && TryParseFloatingCombatantKind(combatant.ParticipantKind, out var kind))
        {
            if (kind == FloatingCombatantKind.FriendlyNpc)
            {
                color = FriendlyNpcTextColor;
                return true;
            }

            if (kind == FloatingCombatantKind.HostileNpc)
            {
                color = HostileNpcTextColor;
                return true;
            }
        }

        color = default;
        return false;
    }

    private static bool TryResolveCombatantRowBackgroundColor(Combatant combatant, PluginConfiguration config, out Vector4 color)
    {
        if (config.HighlightNpcRows && TryParseFloatingCombatantKind(combatant.ParticipantKind, out var kind))
        {
            if (kind == FloatingCombatantKind.FriendlyNpc)
            {
                color = FriendlyNpcRowBackgroundColor;
                return true;
            }

            if (kind == FloatingCombatantKind.HostileNpc)
            {
                color = HostileNpcRowBackgroundColor;
                return true;
            }
        }

        color = default;
        return false;
    }

    private static string JoinPair(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left))
            return string.IsNullOrWhiteSpace(right) ? "--" : right!;

        if (string.IsNullOrWhiteSpace(right))
            return left;

        return $"{left} ({right})";
    }

    private static string FormatEmptyAsZero(string? value)
        => FormatEmptyAsFallback(value, "0");

    private static string FormatEmptyAsFallback(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

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

    private static IReadOnlyList<Combatant> GetVisibleCombatants(CombatDataWrapper combatData, PluginConfiguration config)
        => GetVisibleCombatantRows(combatData, config)
            .Select(static row => row.Combatant)
            .ToList();

    private static IReadOnlyList<DisplayCombatantRow> GetVisibleCombatantRows(CombatDataWrapper combatData, PluginConfiguration config)
    {
        var combatants = combatData.Msg?.Combatant;
        if (combatants == null || combatants.Count == 0)
            return Array.Empty<DisplayCombatantRow>();

        var rows = combatants
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Value.Name))
            .Select(static pair => new DisplayCombatantRow(pair.Value, ResolveFloatingCombatantKind(pair.Value, ParseCombatantActorId(pair.Key))))
            .ToList();

        var playerCount = rows.Count(static row => row.Kind == FloatingCombatantKind.Player);
        rows = config.FloatingStatsParticipantDisplayMode switch
        {
            FloatingStatsParticipantDisplayMode.PlayersOnly => rows
                .Where(static row => row.Kind != FloatingCombatantKind.FriendlyNpc && row.Kind != FloatingCombatantKind.HostileNpc)
                .ToList(),
            FloatingStatsParticipantDisplayMode.PlayersAndFriendlyNpc => rows
                .Where(static row => row.Kind != FloatingCombatantKind.HostileNpc)
                .ToList(),
            FloatingStatsParticipantDisplayMode.PlayersAndHostileNpc => rows
                .Where(static row => row.Kind != FloatingCombatantKind.FriendlyNpc)
                .ToList(),
            _ when playerCount >= 2 => rows
                .Where(static row => row.Kind != FloatingCombatantKind.FriendlyNpc && row.Kind != FloatingCombatantKind.HostileNpc)
                .ToList(),
            _ => rows
                .Where(static row => row.Kind != FloatingCombatantKind.HostileNpc)
                .ToList(),
        };

        return rows;
    }

    private static FloatingCombatantKind ResolveFloatingCombatantKind(Combatant combatant, uint actorId)
    {
        if (TryParseFloatingCombatantKind(combatant.ParticipantKind, out var metadataKind))
            return metadataKind;

        if (actorId is 0 or InvalidActorId)
            return FloatingCombatantKind.Unknown;

        var localPlayerActorIds = new[]
        {
            NormalizeActorId(DalamudApi.GetLocalPlayerActorId()),
            NormalizeActorId(DalamudApi.GetLocalPlayerObjectId()),
            NormalizeActorId(DalamudApi.GetLocalPlayerEntityId()),
        };
        if (localPlayerActorIds.Any(id => id != 0 && id == actorId))
            return FloatingCombatantKind.Player;

        var gameObject = FindObjectByActorId(actorId);
        if (gameObject == null)
            return FloatingCombatantKind.Unknown;

        if (gameObject is IPlayerCharacter)
            return FloatingCombatantKind.Player;

        if (gameObject is not IBattleNpc battleNpc)
            return FloatingCombatantKind.Unknown;

        return (battleNpc.StatusFlags & StatusFlags.Hostile) != 0
            ? FloatingCombatantKind.HostileNpc
            : FloatingCombatantKind.FriendlyNpc;
    }

    private static bool TryParseFloatingCombatantKind(string? participantKind, out FloatingCombatantKind kind)
    {
        kind = participantKind switch
        {
            "player" => FloatingCombatantKind.Player,
            "friendlyNpc" => FloatingCombatantKind.FriendlyNpc,
            "hostileNpc" => FloatingCombatantKind.HostileNpc,
            _ => FloatingCombatantKind.Unknown,
        };

        return kind != FloatingCombatantKind.Unknown;
    }

    private static uint ParseCombatantActorId(string? combatantKey)
    {
        if (string.IsNullOrWhiteSpace(combatantKey))
            return 0;

        var separatorIndex = combatantKey.LastIndexOf('#');
        if (separatorIndex < 0 || separatorIndex >= combatantKey.Length - 1)
            return 0;

        var actorText = combatantKey[(separatorIndex + 1)..];
        return uint.TryParse(actorText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var actorId)
            ? NormalizeActorId(actorId)
            : 0;
    }

    private static IGameObject? FindObjectByActorId(uint actorId)
    {
        foreach (var gameObject in DalamudApi.ObjectTable)
        {
            if (MatchesObjectActorId(gameObject, actorId))
                return gameObject;
        }

        return null;
    }

    private static bool MatchesObjectActorId(IGameObject? gameObject, uint actorId)
    {
        if (gameObject == null || actorId is 0 or InvalidActorId)
            return false;

        var gameObjectLow32 = gameObject.GameObjectId == 0
            ? 0
            : NormalizeActorId(unchecked((uint)(gameObject.GameObjectId & uint.MaxValue)));
        if (gameObjectLow32 != 0 && gameObjectLow32 == actorId)
            return true;

        var objectId = TryGetReflectedActorId(gameObject, "ObjectId");
        if (objectId != 0 && objectId == actorId)
            return true;

        var entityId = NormalizeActorId(gameObject.EntityId);
        return entityId != 0 && entityId == actorId;
    }

    private static uint TryGetReflectedActorId(object? instance, string propertyName)
    {
        if (instance == null)
            return 0;

        try
        {
            var property = instance.GetType().GetProperty(propertyName);
            var rawValue = property?.GetValue(instance);
            return rawValue == null ? 0 : NormalizeActorId(Convert.ToUInt32(rawValue, CultureInfo.InvariantCulture));
        }
        catch
        {
            return 0;
        }
    }

    private static uint NormalizeActorId(uint actorId)
        => actorId is 0 or InvalidActorId ? 0 : actorId;

    private static long ParseLocalizedAmount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0L;

        var text = value.Trim();
        if (text is "---" or "--")
            return 0L;

        long multiplier = 1L;
        if (text.EndsWith("兆", StringComparison.Ordinal))
        {
            multiplier = 1_000_000_000_000L;
            text = text[..^1];
        }
        else if (text.EndsWith("亿", StringComparison.Ordinal))
        {
            multiplier = 100_000_000L;
            text = text[..^1];
        }
        else if (text.EndsWith("万", StringComparison.Ordinal))
        {
            multiplier = 10_000L;
            text = text[..^1];
        }

        text = text.Replace(",", string.Empty, StringComparison.Ordinal);
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return 0L;

        return (long)Math.Round(parsed * multiplier, MidpointRounding.AwayFromZero);
    }

    private static string FormatCompactAmount(long value)
    {
        const long trillion = 1_000_000_000_000L;
        const long hundredMillion = 100_000_000L;
        const long tenThousand = 10_000L;

        var abs = Math.Abs(value);
        if (abs >= trillion)
            return FormatChineseUnit(value, trillion, "兆");
        if (abs >= hundredMillion)
            return FormatChineseUnit(value, hundredMillion, "亿");
        if (abs >= tenThousand)
            return FormatChineseUnit(value, tenThousand, "万");

        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatChineseUnit(long value, long unitBase, string unit)
        => (value / (double)unitBase).ToString("0.00", CultureInfo.InvariantCulture) + unit;

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
        var maxHitText = ResolveCombatantTooltipMaxHitText(combatant);
        if (!string.IsNullOrWhiteSpace(maxHitText))
            ImGui.TextUnformatted($"最高伤害: {maxHitText}");
        ImGui.EndTooltip();
    }

    private static string? ResolveCombatantTooltipMaxHitText(Combatant combatant)
    {
        if (string.IsNullOrWhiteSpace(combatant.MaxHitText) || combatant.MaxHitText == "--")
            return null;

        return combatant.MaxHitText;
    }

    private static string FormatMetricValue(double value)
        => value.ToString("0", CultureInfo.InvariantCulture);

    private static string FallbackText(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string BuildMetricLayoutSignature(
        FloatingStatsDisplayStyle displayStyle,
        bool showPlayerColumn,
        bool showJobColumn,
        bool showDamageColumn,
        bool showValueColumn,
        bool showDeathsColumn)
        => $"{(int)displayStyle}:{(showPlayerColumn ? 'p' : '-')}{(showJobColumn ? 'j' : '-')}{(showDamageColumn ? 'd' : '-')}{(showValueColumn ? 'v' : '-')}{(showDeathsColumn ? 'x' : '-')}";

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
