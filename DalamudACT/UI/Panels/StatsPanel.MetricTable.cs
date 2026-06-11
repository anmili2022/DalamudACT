using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal static partial class StatsPanel
{
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
        {
            ImGui.EndChild();
            return;
        }

        var sourceCombatants = sourceRows ?? GetVisibleCombatants(combatData, config);
        IReadOnlyList<Combatant> allRows = keepSourceOrder && sourceRows != null
            ? sourceRows
            : sourceCombatants
                .OrderByDescending(selector)
                .ToList();

        if (allRows.Count == 0)
        {
            ImGui.TextDisabled("\u6ca1\u6709\u53ef\u663e\u793a\u7684\u6570\u636e\u3002");
            ImGui.EndChild();
            return;
        }

        IReadOnlyList<Combatant> rows = maxRows.HasValue && allRows.Count > Math.Max(maxRows.Value, 1)
            ? allRows.Take(Math.Max(maxRows.Value, 1)).ToList()
            : allRows;
        var maxValue = 0d;
        var totalValue = 0d;
        foreach (var combatant in allRows)
        {
            var value = selector(combatant);
            if (value > maxValue)
                maxValue = value;

            totalValue += value;
        }
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

        try
        {
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
                        ImGui.TextUnformatted(ResolveCombatantDisplayName(combatant, config));
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
                    if (hasBarTextColor)
                        ImGui.PushStyleColor(ImGuiCol.Text, barTextColor);
                    try
                    {
                        ImGui.ProgressBar((float)Math.Clamp(maxRatio, 0d, 1d), new Vector2(-1f, 0f), $"{totalRatio:P1}");
                    }
                    finally
                    {
                        ImGui.PopStyleColor(hasBarTextColor ? 3 : 2);
                    }

                    DrawCombatantBarTooltip(
                        combatant,
                        tooltipPrimaryLabel,
                        tooltipPrimaryTextSelector?.Invoke(combatant),
                        tooltipRateLabel,
                        tooltipRateTextSelector?.Invoke(combatant));
                }
                finally
                {
                    if (hasCustomTextColor)
                        ImGui.PopStyleColor();
                }
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
        }
        finally
        {
            ImGui.EndTable();
            ImGui.EndChild();
        }
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
}
