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
internal static partial class StatsPanel
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
    private static readonly Vector4 HostileNpcTextColor = new(1.00f, 0.72f, 0.60f, 1.00f);
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
    private static readonly Dictionary<uint, FloatingCombatantKind> CombatantKindCache = new();
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

        if (config.FloatingStatsDisplayStyle == FloatingStatsDisplayStyle.Minimal)
            return DrawMinimalPanel(statsService, config);

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
        if (config.FloatingStatsDisplayStyle == FloatingStatsDisplayStyle.Ikegami && config.FloatingStatsIkegamiMinimalMode)
        {
            if (!collapseToTabBar)
            {
                if (hasCombatData)
                    DrawDpsTab(combatData!, config);
                else
                    DrawNoCombatPlaceholder(statsService, hasHistory: false);
            }

            return new StatsPanelDrawResult(StatsPanelTabId.Dps, false, false, false);
        }

        if (ikegamiTabFontScale != 1f)
            ImGui.SetWindowFontScale(ikegamiTabFontScale);
        try
        {
            if (!ImGui.BeginTabBar("##stats_tabs"))
                return new StatsPanelDrawResult(previousActiveTab, false, false, false);

            var activeTab = previousActiveTab;
            var toggleDpsCollapseRequested = false;
            var openSettingsRequested = false;

            if (config.ShowDpsTab && ImGui.BeginTabItem("DPS"))
            {
                try
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
                }
                finally
                {
                    ImGui.EndTabItem();
                }
            }

            if (config.ShowHpsTab && ImGui.BeginTabItem("HPS"))
            {
                try
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
                }
                finally
                {
                    ImGui.EndTabItem();
                }
            }

            if (config.ShowTakenTab && ImGui.BeginTabItem("承伤"))
            {
                try
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
                }
                finally
                {
                    ImGui.EndTabItem();
                }
            }

            if (config.ShowOverviewTab && ImGui.BeginTabItem("概览"))
            {
                try
                {
                    activeTab = StatsPanelTabId.Overview;

                    if (!collapseToTabBar)
                    {
                        if (hasCombatData)
                            DrawOverviewTab(combatData!, config);
                        else
                            DrawNoCombatPlaceholder(statsService, hasHistory: false);
                    }
                }
                finally
                {
                    ImGui.EndTabItem();
                }
            }

            if (config.ShowHistoryTab && ImGui.BeginTabItem("历史记录"))
            {
                try
                {
                    activeTab = StatsPanelTabId.History;
                    if (!collapseToTabBar && DrawHistoryTab(statsService, config) && config.ShowDpsTab)
                        activeTab = StatsPanelTabId.Dps;
                }
                finally
                {
                    ImGui.EndTabItem();
                }
            }

            ImGui.EndTabBar();
            return new StatsPanelDrawResult(activeTab, toggleDpsCollapseRequested, openSettingsRequested, false);
        }
        finally
        {
            if (ikegamiTabFontScale != 1f)
                ImGui.SetWindowFontScale(1f);
        }
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


}
