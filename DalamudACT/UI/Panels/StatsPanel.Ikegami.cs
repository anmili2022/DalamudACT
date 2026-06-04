using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal static partial class StatsPanel
{
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
            ImGui.EndChild();
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
        try
        {
            var stripStarted = ImGui.BeginChild(
                $"##ikegami_strip_{id}",
                new Vector2(0f, stripHeight),
                false,
                stripFlags);
            try
            {
                if (stripStarted)
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
                        var hasBarTextColor = TryResolveCombatantBarTextColor(combatant, config, out var barTextColor);
                        var highlightSelf = config.HighlightSelfBar && IsLocalPlayerCombatant(combatant);
                        var primaryLabel = ResolveCombatantDisplayName(combatant, config);
                        var secondaryLabel = string.IsNullOrWhiteSpace(combatant.Job) ? null : combatant.Job;
                        var jobBadgeText = ResolveIkegamiJobBadgeText(combatant);
                        var title = ResolveIkegamiTitle(primaryLabel, secondaryLabel, showPlayerColumn, showJobColumn);
                        if (highlightSelf && !title.StartsWith("★", StringComparison.Ordinal))
                            title = $"★ {title}";
                        var headerMetricText = ResolveIkegamiHeaderMetricText(
                            jobBadgeText,
                            textSelector(combatant),
                            ResolveIkegamiPrimaryMetricSuffix(id, valueColumnLabel));
                        if (highlightSelf && !ikegamiShowNameLine)
                            headerMetricText = $"★ {headerMetricText}";
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
                            title,
                            detailText,
                            headerMetricText,
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
                            hasBarTextColor,
                            barTextColor,
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
                }
            }
            finally
            {
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
        }
        finally
        {
            ImGui.EndChild();
            ImGui.PopStyleColor();
        }
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
        bool hasBarTextColor,
        Vector4 barTextColor,
        Action drawTooltip)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        var cardStarted = ImGui.BeginChild(
                $"##ikegami_card_{id}",
                new Vector2(boxWidth, cardHeight),
                false,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        try
        {
            if (cardStarted)
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
                try
                {
                    var panelStarted = ImGui.BeginChild(
                        $"##ikegami_card_panel_{id}",
                        new Vector2(-1f, boxHeight),
                        true,
                        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
                    try
                    {
                        if (panelStarted)
                        {
                            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 7f);
                            ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 0f);
                            ImGui.PushStyleColor(ImGuiCol.ChildBg, WithAlphaMultiplier(barColor, headerAlpha));
                            try
                            {
                                var headerStarted = ImGui.BeginChild(
                                    $"##ikegami_card_header_{id}",
                                    new Vector2(-1f, headerHeight),
                                    false,
                                    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
                                try
                                {
                                    if (headerStarted)
                                    {
                                        if (headerFontScale != 1f)
                                            ImGui.SetWindowFontScale(headerFontScale);

                                        ImGui.PushStyleColor(ImGuiCol.Text, hasBarTextColor ? barTextColor : IkegamiHeaderTextColor);
                                        try
                                        {
                                            ImGui.SetCursorPosY(Math.Max(0f, ((headerHeight - ImGui.GetTextLineHeight()) * 0.5f) - 1f));
                                            DrawLeftAlignedTextLine(primaryMetricDisplayText, headerLeftPadding);
                                        }
                                        finally
                                        {
                                            ImGui.PopStyleColor();
                                            if (headerFontScale != 1f)
                                                ImGui.SetWindowFontScale(1f);
                                        }
                                    }
                                }
                                finally
                                {
                                    ImGui.EndChild();
                                }
                            }
                            finally
                            {
                                ImGui.PopStyleColor();
                                ImGui.PopStyleVar(2);
                            }

                            drawTooltip();

                            var bodyHeight = Math.Max(0f, boxHeight - headerHeight);
                            if (!string.IsNullOrWhiteSpace(detailText) && bodyHeight > 0f)
                            {
                                ImGui.PushStyleColor(ImGuiCol.ChildBg, WithAlphaMultiplier(IkegamiBodyBackgroundColor, bodyBackgroundAlpha));
                                try
                                {
                                    var bodyStarted = ImGui.BeginChild(
                                        $"##ikegami_card_body_{id}",
                                        new Vector2(-1f, bodyHeight),
                                        false,
                                        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
                                    try
                                    {
                                        if (bodyStarted)
                                        {
                                            if (bodyFontScale != 1f)
                                                ImGui.SetWindowFontScale(bodyFontScale);

                                            var bodyTextY = Math.Max(0f, ((bodyHeight - ImGui.GetTextLineHeight()) * 0.5f) - 1f) - detailRaise;
                                            var bodyTextColor = hasCustomTextColor ? rowTextColor : IkegamiMutedTextColor;
                                            ImGui.PushStyleColor(ImGuiCol.Text, WithAlphaMultiplier(bodyTextColor, bodyAlpha));
                                            try
                                            {
                                                ImGui.SetCursorPos(new Vector2(detailLeftPadding, Math.Max(0f, bodyTextY)));
                                                ImGui.TextUnformatted(detailText);
                                            }
                                            finally
                                            {
                                                ImGui.PopStyleColor();
                                                if (bodyFontScale != 1f)
                                                    ImGui.SetWindowFontScale(1f);
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        ImGui.EndChild();
                                    }
                                }
                                finally
                                {
                                    ImGui.PopStyleColor();
                                }
                            }
                        }
                    }
                    finally
                    {
                        ImGui.EndChild();
                    }
                }
                finally
                {
                    ImGui.PopStyleColor(2);
                    ImGui.PopStyleVar(2);
                }
            }
        }
        finally
        {
            ImGui.EndChild();
            ImGui.PopStyleVar();
        }
    }

}
