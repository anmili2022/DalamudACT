using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal static partial class StatsPanel
{
    private static StatsPanelDrawResult DrawMinimalPanel(LocalStatsService statsService, PluginConfiguration config)
    {
        var openSettingsRequested = false;
        var combatData = statsService.DisplayCombatData;
        var hasCombatData = combatData?.Msg?.Encounter != null;

        if (!config.ShowDpsTab)
        {
            ImGui.TextDisabled("极简样式只显示 DPS，请先在设置中启用 DPS 页。");
        }
        else if (hasCombatData)
        {
            DrawMinimalDpsTab(combatData!, config);
        }
        else
        {
            DrawNoCombatPlaceholder(statsService, hasHistory: false);
        }

        openSettingsRequested = ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows)
                               && ImGui.IsMouseClicked(ImGuiMouseButton.Right);
        return new StatsPanelDrawResult(StatsPanelTabId.Dps, false, openSettingsRequested, true);
    }

    private static void DrawMinimalDpsTab(CombatDataWrapper combatData, PluginConfiguration config)
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

        var totalDps = nonHostileCombatants.Sum(static c => ParseMetric(c.EncDpsText));
        var totalDamage = FormatCompactAmount(nonHostileCombatants.Sum(static c => ParseLocalizedAmount(c.DamageText)));
        var totalDeaths = nonHostileCombatants.Sum(static c => ParseCount(c.DeathsText));
        var durationText = combatData.Msg?.Encounter?.DurationText ?? "00:00";

        DrawMinimalMetricTab(
            id: "dps",
            combatData: combatData,
            config: config,
            selector: static c => ParseMetric(c.EncDpsText),
            textSelector: static c => c.EncDpsText ?? "0",
            sourceRows: orderedVisibleCombatants,
            showPlayerColumn: config.FloatingStatsMinimalShowPlayerColumn,
            showDamageColumn: config.FloatingStatsMinimalShowDamageColumn,
            damageColumnLabel: "伤害量",
            damageTextSelector: static c => c.DamageText ?? "0",
            showDeathsColumn: config.FloatingStatsMinimalShowDeathsColumn,
            maxRows: config.DpsVisibleCount,
            showSummaryRow: config.FloatingStatsMinimalShowSummaryRow,
            summaryName: "总DPS",
            summaryDamageText: totalDamage,
            summaryValueText: FormatMetricValue(totalDps),
            summaryDeathsText: totalDeaths.ToString(CultureInfo.InvariantCulture),
            summaryShareTextOverride: ResolveMinimalSummaryBarText(
                durationText,
                "总DPS",
                FormatMetricValue(totalDps),
                totalDamage,
                totalDeaths.ToString(CultureInfo.InvariantCulture),
                config),
            summaryRowInsertIndex: nonHostileCombatants.Count);
    }

    private static string ResolveMinimalShareText(
        Combatant combatant,
        string? valueText,
        string? damageText,
        string? deathsText,
        double totalRatio,
        PluginConfiguration config)
    {
        var segments = new List<string>(6);

        var highlightSelf = config.HighlightSelfBar && IsLocalPlayerCombatant(combatant);
        if (config.FloatingStatsMinimalShowPlayerNameInShareBar)
            segments.Add(ResolveCombatantDisplayName(combatant, config));
        else if (highlightSelf)
            segments.Add("★");

        if (config.FloatingStatsMinimalShowJobInShareBar)
        {
            var badgeText = ResolveIkegamiJobBadgeText(combatant);
            if (!string.IsNullOrWhiteSpace(badgeText))
                segments.Add($"[{badgeText}]");
        }

        segments.Add(FormatMinimalCompactValueText(valueText, "0"));

        if (config.FloatingStatsMinimalShowDamageInShareBar)
            segments.Add($"d{FormatMinimalCompactValueText(damageText, "0")}");

        if (config.FloatingStatsMinimalShowDeathsInShareBar)
            segments.Add($"x{FormatMinimalCompactValueText(deathsText, "0")}");

        if (config.FloatingStatsMinimalShowRatioInShareBar)
            segments.Add($"%{FormatMinimalCompactPercentText(totalRatio)}");

        return segments.Count > 0 ? string.Join("-", segments) : FormatMinimalCompactValueText(valueText, "0");
    }

    private static string ResolveMinimalSummaryShareText(string summaryName, string? summaryValueText)
    {
        var metricText = FormatEmptyAsFallback(summaryValueText, "0");
        return string.IsNullOrWhiteSpace(summaryName)
            ? metricText
            : $"{summaryName}-{metricText}";
    }

    private static string ResolveMinimalSummaryBarText(
        string durationText,
        string titleText,
        string totalDpsText,
        string totalDamageText,
        string totalDeathsText,
        PluginConfiguration config)
    {
        var segments = new List<string>(5);

        if (config.FloatingStatsMinimalShowDurationInSummaryBar)
            segments.Add($"[{FormatEmptyAsFallback(durationText, "00:00")}]");

        if (config.FloatingStatsMinimalShowTitleInSummaryBar)
            segments.Add(FormatEmptyAsFallback(titleText, "总DPS"));

        if (config.FloatingStatsMinimalShowDpsInSummaryBar)
            segments.Add(FormatMinimalCompactValueText(totalDpsText, "0"));

        if (config.FloatingStatsMinimalShowDamageInSummaryBar)
            segments.Add($"d{FormatMinimalCompactValueText(totalDamageText, "0")}");

        if (config.FloatingStatsMinimalShowDeathsInSummaryBar)
            segments.Add($"x{FormatMinimalCompactValueText(totalDeathsText, "0")}");

        return segments.Count > 0
            ? string.Join("-", segments)
            : FormatMinimalCompactValueText(totalDpsText, "0");
    }

    private static void DrawMinimalMetricTab(
        string id,
        CombatDataWrapper combatData,
        PluginConfiguration config,
        Func<Combatant, double> selector,
        Func<Combatant, string> textSelector,
        IReadOnlyList<Combatant>? sourceRows = null,
        bool showPlayerColumn = true,
        bool showDamageColumn = true,
        string damageColumnLabel = "伤害量",
        Func<Combatant, string>? damageTextSelector = null,
        bool showDeathsColumn = false,
        int? maxRows = null,
        bool showSummaryRow = false,
        string summaryName = "",
        string? summaryDamageText = null,
        string? summaryValueText = null,
        string? summaryDeathsText = null,
        string? summaryShareTextOverride = null,
        int? summaryRowInsertIndex = null)
    {
        var style = ImGui.GetStyle();
        var configuredMinimalFontScale = Math.Clamp(config.FloatingStatsMinimalFontScale, 0.6f, 2.0f);
        var rowHeight = ResolveMinimalRowHeight(config);
        var minimalFontScale = ResolveEffectiveMinimalFontScale(configuredMinimalFontScale, rowHeight);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(style.ItemSpacing.X, 1f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(style.FramePadding.X, 0f));
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(style.CellPadding.X, 0f));
        if (!ImGui.BeginChild($"##minimal_metric_{id}_scroll", new Vector2(0f, 0f), false))
        {
            ImGui.EndChild();
            ImGui.PopStyleVar(3);
            return;
        }

        if (minimalFontScale != 1f)
            ImGui.SetWindowFontScale(minimalFontScale);

        var sourceCombatants = sourceRows ?? GetVisibleCombatants(combatData, config);
        var allRows = sourceRows != null
            ? sourceCombatants.ToList()
            : sourceCombatants
                .OrderByDescending(selector)
                .ToList();

        if (allRows.Count == 0)
        {
            ImGui.TextDisabled("没有可显示的数据。");
            if (minimalFontScale != 1f)
                ImGui.SetWindowFontScale(1f);
            ImGui.EndChild();
            ImGui.PopStyleVar(3);
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
        var playerColumnWidth = showPlayerColumn
            ? Math.Max(1f, config.FloatingStatsMinimalPlayerColumnWidth)
            : 0f;
        var damageColumnWidth = showDamageColumn
            ? Math.Max(1f, config.FloatingStatsMinimalDamageColumnWidth)
            : 0f;
        var deathsColumnWidth = showDeathsColumn
            ? Math.Max(1f, config.FloatingStatsMinimalDeathsColumnWidth)
            : 0f;
        var tooltipFontScale = Math.Clamp(config.FloatingStatsIkegamiTooltipFontScale, 0.6f, 2.0f);
        var tableFlags = ImGuiTableFlags.RowBg
                         | ImGuiTableFlags.BordersInnerH
                         | ImGuiTableFlags.SizingFixedFit
                         | ImGuiTableFlags.NoSavedSettings;
        var visibleColumns = new List<VisibleMetricColumn>(4);
        int? playerColumnIndex = null;
        int? damageColumnIndex = null;
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

        if (showDeathsColumn)
        {
            deathsColumnIndex = nextColumnIndex;
            visibleColumns.Add(new VisibleMetricColumn(
                MetricColumnSlot.Deaths,
                nextColumnIndex++,
                "死",
                deathsColumnWidth,
                ImGuiTableColumnFlags.WidthFixed));
        }

        var shareColumnIndex = nextColumnIndex;
        visibleColumns.Add(new VisibleMetricColumn(
            MetricColumnSlot.Share,
            shareColumnIndex,
            "占比",
            1f,
            ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.NoResize));

        if (!ImGui.BeginTable($"##minimal_metric_table_{id}", visibleColumns.Count, tableFlags))
        {
            if (minimalFontScale != 1f)
                ImGui.SetWindowFontScale(1f);
            ImGui.EndChild();
            ImGui.PopStyleVar(3);
            return;
        }

        foreach (var column in visibleColumns)
            ImGui.TableSetupColumn(column.Label, column.Flags, column.Width, (uint)column.Slot);
        if (config.FloatingStatsMinimalShowHeader)
            ImGui.TableHeadersRow();

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            if (showSummaryRow && rowIndex == effectiveSummaryRowInsertIndex)
            {
                DrawMinimalMetricSummaryRow(
                    rowHeight,
                    showPlayerColumn,
                    playerColumnIndex,
                    summaryName,
                    showDamageColumn,
                    damageColumnIndex,
                    summaryDamageText,
                    showDeathsColumn,
                    deathsColumnIndex,
                    summaryDeathsText,
                    shareColumnIndex,
                    summaryShareTextOverride ?? ResolveMinimalSummaryShareText(summaryName, summaryValueText));
            }

            var combatant = rows[rowIndex];
            var value = selector(combatant);
            var maxRatio = maxValue > 0 ? value / maxValue : 0d;
            var totalRatio = totalValue > 0 ? value / totalValue : 0d;
            var barColor = ResolveBarColor(combatant, config);
            var hasCustomTextColor = TryResolveCombatantTextColor(combatant, config, out var rowTextColor);
            var hasBarTextColor = TryResolveCombatantBarTextColor(combatant, config, out var barTextColor);

            TableNextRow(rowHeight);
            if (TryResolveCombatantRowBackgroundColor(combatant, config, out var rowBackgroundColor))
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(rowBackgroundColor));

            if (hasCustomTextColor)
                ImGui.PushStyleColor(ImGuiCol.Text, rowTextColor);
            try
            {
                if (showPlayerColumn)
                {
                    ImGui.TableSetColumnIndex(playerColumnIndex!.Value);
                    AlignMinimalCellContentY(rowHeight);
                    ImGui.TextUnformatted(ResolveCombatantDisplayName(combatant, config));
                }

                if (showDamageColumn)
                {
                    ImGui.TableSetColumnIndex(damageColumnIndex!.Value);
                    AlignMinimalCellContentY(rowHeight);
                    ImGui.TextUnformatted(damageTextSelector?.Invoke(combatant) ?? "0");
                }

                if (showDeathsColumn)
                {
                    ImGui.TableSetColumnIndex(deathsColumnIndex!.Value);
                    AlignMinimalCellContentY(rowHeight);
                    ImGui.TextUnformatted(string.IsNullOrWhiteSpace(combatant.DeathsText) ? "0" : combatant.DeathsText!);
                }

                ImGui.TableSetColumnIndex(shareColumnIndex);
                ImGui.PushStyleColor(ImGuiCol.FrameBg, FrameBackgroundColor);
                ImGui.PushStyleColor(ImGuiCol.PlotHistogram, barColor);
                if (hasBarTextColor)
                    ImGui.PushStyleColor(ImGuiCol.Text, barTextColor);
                try
                {
                    ImGui.ProgressBar(
                        (float)Math.Clamp(maxRatio, 0d, 1d),
                        new Vector2(-1f, Math.Max(1f, ResolveMinimalProgressBarHeight(rowHeight))),
                        ResolveMinimalShareText(
                            combatant,
                            textSelector(combatant),
                            damageTextSelector?.Invoke(combatant),
                            combatant.DeathsText,
                            totalRatio,
                            config));
                }
                finally
                {
                    ImGui.PopStyleColor(hasBarTextColor ? 3 : 2);
                }

                DrawIkegamiDpsTooltip(combatant, tooltipFontScale);
            }
            finally
            {
                if (hasCustomTextColor)
                    ImGui.PopStyleColor();
            }
        }

        if (showSummaryRow && effectiveSummaryRowInsertIndex >= rows.Count)
        {
            DrawMinimalMetricSummaryRow(
                rowHeight,
                showPlayerColumn,
                playerColumnIndex,
                summaryName,
                showDamageColumn,
                damageColumnIndex,
                summaryDamageText,
                showDeathsColumn,
                deathsColumnIndex,
                summaryDeathsText,
                shareColumnIndex,
                summaryShareTextOverride ?? ResolveMinimalSummaryShareText(summaryName, summaryValueText));
        }

        if (minimalFontScale != 1f)
            ImGui.SetWindowFontScale(1f);
        ImGui.EndTable();
        ImGui.EndChild();
        ImGui.PopStyleVar(3);
    }

    private static void DrawMinimalMetricSummaryRow(
        float rowHeight,
        bool showPlayerColumn,
        int? playerColumnIndex,
        string summaryName,
        bool showDamageColumn,
        int? damageColumnIndex,
        string? summaryDamageText,
        bool showDeathsColumn,
        int? deathsColumnIndex,
        string? summaryDeathsText,
        int shareColumnIndex,
        string summaryShareText)
    {
        TableNextRow(rowHeight);

        if (showPlayerColumn)
        {
            ImGui.TableSetColumnIndex(playerColumnIndex!.Value);
            AlignMinimalCellContentY(rowHeight);
            ImGui.TextUnformatted(summaryName);
        }

        if (showDamageColumn)
        {
            ImGui.TableSetColumnIndex(damageColumnIndex!.Value);
            AlignMinimalCellContentY(rowHeight);
            ImGui.TextUnformatted(string.IsNullOrWhiteSpace(summaryDamageText) ? "0" : summaryDamageText);
        }

        if (showDeathsColumn)
        {
            ImGui.TableSetColumnIndex(deathsColumnIndex!.Value);
            AlignMinimalCellContentY(rowHeight);
            ImGui.TextUnformatted(string.IsNullOrWhiteSpace(summaryDeathsText) ? "0" : summaryDeathsText);
        }

        ImGui.TableSetColumnIndex(shareColumnIndex);
        AlignMinimalCellContentY(rowHeight);
        ImGui.TextUnformatted(summaryShareText);
    }
}
