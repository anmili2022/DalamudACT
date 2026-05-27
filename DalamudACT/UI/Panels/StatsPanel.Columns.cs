using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal static partial class StatsPanel
{
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

    private static float ResolveMinimalRowHeight(PluginConfiguration config)
        => Math.Max(1f, config.FloatingStatsMinimalRowHeight);

    private static float ResolveEffectiveMinimalFontScale(float configuredScale, float rowHeight)
    {
        var baseFontSize = Math.Max(1f, ImGui.GetFontSize());
        var maxScaleByRowHeight = Math.Max(0.2f, rowHeight / baseFontSize);
        return Math.Min(configuredScale, maxScaleByRowHeight);
    }

    private static float ResolveMinimalProgressBarHeight(float rowHeight)
        => Math.Max(1f, rowHeight);

    private static void AlignMinimalCellContentY(float rowHeight)
    {
        if (rowHeight <= 0f)
            return;

        var offsetY = Math.Max(0f, (rowHeight - ImGui.GetTextLineHeight()) * 0.5f);
        if (offsetY > 0f)
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + offsetY);
    }

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

    internal static float CalculateMinimalAutoWindowHeight(LocalStatsService statsService, PluginConfiguration config)
    {
        const float maxHeight = 4000f;
        var style = ImGui.GetStyle();
        var windowPaddingHeight = style.WindowPadding.Y * 2f;
        var singleLineHeight = ImGui.GetTextLineHeightWithSpacing() + 2f;
        var minimumHeight = Math.Clamp(windowPaddingHeight + singleLineHeight, 1f, maxHeight);

        if (!config.ShowDpsTab)
            return minimumHeight;

        var combatData = statsService.DisplayCombatData;
        if (combatData?.Msg?.Encounter == null)
            return minimumHeight;

        var visibleRowCount = GetVisibleCombatantRows(combatData, config).Count;
        if (visibleRowCount <= 0)
            return minimumHeight;

        visibleRowCount = Math.Min(visibleRowCount, Math.Max(config.DpsVisibleCount, 1));

        var rowHeight = ResolveMinimalRowHeight(config);
        var headerHeight = config.FloatingStatsMinimalShowHeader
            ? Math.Max(rowHeight, ImGui.GetTextLineHeight() + 2f)
            : 0f;
        var summaryRowCount = config.FloatingStatsMinimalShowSummaryRow ? 1 : 0;
        var bodyRowCount = visibleRowCount + summaryRowCount;
        var separatorCount = Math.Max(0, bodyRowCount - 1);
        if (config.FloatingStatsMinimalShowHeader && bodyRowCount > 0)
            separatorCount += 1;

        var contentHeight = headerHeight + (bodyRowCount * rowHeight) + separatorCount + 2f;
        return Math.Clamp(windowPaddingHeight + contentHeight, minimumHeight, maxHeight);
    }

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
