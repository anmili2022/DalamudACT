using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal static partial class StatsPanel
{
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
            return ResolveSingleCharacterJobLabel(combatant.Job!);

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

    private static string ResolveSingleCharacterJobLabel(string job)
        => job switch
        {
            "武士" => "武",
            "武僧" => "僧",
            _ => job.Length > 0 ? job[0].ToString() : "?",
        };

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
        try
        {
            var footerStarted = ImGui.BeginChild(
                $"##ikegami_footer_{id}",
                new Vector2(footerWidth, footerHeight),
                true,
                ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
            try
            {
                if (!footerStarted)
                    return;

                if (footerFontScale != 1f)
                    ImGui.SetWindowFontScale(footerFontScale);

                try
                {
                    if (ImGui.BeginTable(
                            $"##ikegami_footer_table_{id}",
                            2,
                            ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
                    {
                        try
                        {
                            ImGui.TableSetupColumn("##left", ImGuiTableColumnFlags.WidthStretch);
                            ImGui.TableSetupColumn("##right", ImGuiTableColumnFlags.WidthFixed, Math.Max(ImGui.CalcTextSize(rightText).X + footerRightPadding + 4f, 84f));
                            ImGui.TableNextRow();
                            ImGui.TableSetColumnIndex(0);
                            ImGui.PushStyleColor(ImGuiCol.Text, WithAlphaMultiplier(IkegamiEncounterTimeTextColor, footerAlpha));
                            try
                            {
                                ImGui.TextUnformatted(durationText);
                            }
                            finally
                            {
                                ImGui.PopStyleColor();
                            }
                            ImGui.SameLine(0f, footerTimeZoneSpacing);
                            ImGui.PushStyleColor(ImGuiCol.Text, WithAlphaMultiplier(ImGui.GetStyle().Colors[(int)ImGuiCol.Text], footerAlpha));
                            try
                            {
                                ImGui.TextUnformatted(zoneName);
                            }
                            finally
                            {
                                ImGui.PopStyleColor();
                            }
                            ImGui.TableSetColumnIndex(1);
                            ImGui.PushStyleColor(ImGuiCol.Text, WithAlphaMultiplier(ImGui.GetStyle().Colors[(int)ImGuiCol.Text], footerAlpha));
                            try
                            {
                                DrawRightAlignedTextLine(rightText, footerRightPadding);
                            }
                            finally
                            {
                                ImGui.PopStyleColor();
                            }
                        }
                        finally
                        {
                            ImGui.EndTable();
                        }
                    }
                }
                finally
                {
                    if (footerFontScale != 1f)
                        ImGui.SetWindowFontScale(1f);
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
        try
        {
            if (!ImGui.BeginChild(
                    $"##ikegami_name_wrap_{id}",
                    new Vector2(innerWidth, nameHeight),
                    false,
                    ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                ImGui.EndChild();
                return;
            }

            try
            {
                if (fontScale != 1f)
                    ImGui.SetWindowFontScale(fontScale);

                try
                {
                    if (!ImGui.BeginTable(
                            $"##ikegami_name_{id}",
                            columnCount,
                            ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
                    {
                        ImGui.PushStyleColor(ImGuiCol.Text, textColor);
                        try
                        {
                            ImGui.TextUnformatted(resolvedTitle);
                        }
                        finally
                        {
                            ImGui.PopStyleColor();
                        }
                        return;
                    }

                    try
                    {
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
                        try
                        {
                            ImGui.SetCursorPosY(Math.Max(0f, ((nameHeight - ImGui.GetTextLineHeight()) * 0.5f) - 1f));
                            ImGui.TextUnformatted(resolvedTitle);
                        }
                        finally
                        {
                            ImGui.PopStyleColor();
                        }
                    }
                    finally
                    {
                        ImGui.EndTable();
                    }
                }
                finally
                {
                    if (fontScale != 1f)
                        ImGui.SetWindowFontScale(1f);
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
            ImGui.PopStyleVar();
            ImGui.SetCursorPos(new Vector2(startX, startY + nameHeight));
        }
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
        try
        {
            if (tooltipFontScale != 1f)
                ImGui.SetWindowFontScale(tooltipFontScale);
            try
            {
                ImGui.TextUnformatted($"{tooltipPrimaryLabel}: {FormatEmptyAsFallback(tooltipPrimaryText, "0")}");
                ImGui.TextUnformatted($"{tooltipRateLabel}: {FormatEmptyAsFallback(tooltipRateText, "0")}");
                var maxHitText = ResolveCombatantTooltipMaxHitText(combatant);
                if (!string.IsNullOrWhiteSpace(maxHitText))
                    ImGui.TextUnformatted($"最高伤害：{maxHitText}");
            }
            finally
            {
                if (tooltipFontScale != 1f)
                    ImGui.SetWindowFontScale(1f);
            }
        }
        finally
        {
            ImGui.EndTooltip();
        }
    }

    private static void DrawIkegamiDpsTooltip(Combatant combatant, float tooltipFontScale)
    {
        if (!ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        try
        {
            if (tooltipFontScale != 1f)
                ImGui.SetWindowFontScale(tooltipFontScale);
            try
            {
                ImGui.TextUnformatted($"总伤：{FormatEmptyAsFallback(combatant.DamageText, "0")}");
                ImGui.TextUnformatted($"暴击率：{ResolveCombatantCritRateText(combatant)}");
                ImGui.TextUnformatted($"直爆率：{ResolveCombatantCritDirectRateText(combatant)}");
                var maxHitText = ResolveCombatantTooltipMaxHitText(combatant);
                if (!string.IsNullOrWhiteSpace(maxHitText))
                    ImGui.TextUnformatted($"最高伤害：{maxHitText}");
            }
            finally
            {
                if (tooltipFontScale != 1f)
                    ImGui.SetWindowFontScale(1f);
            }
        }
        finally
        {
            ImGui.EndTooltip();
        }
    }
}
