using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal sealed partial class SettingsWindow
{
    private string GetFloatingStatsButtonLabel()
        => config.ShowStatsPanel ? "隐藏悬浮DPS统计面板" : "打开悬浮DPS统计面板";

    private void DrawSettingCard(string id, string title, string description, float heightInLines, Action drawContent)
    {
        var style = ImGui.GetStyle();
        var lineHeight = ImGui.GetTextLineHeightWithSpacing();
        const float cardVerticalPadding = 6f;
        const float cardContentGap = 2f;
        const float cardMinLines = 2.2f;
        var minHeight = (lineHeight * cardMinLines) + cardVerticalPadding * 2f;
        var fallbackHeight = (lineHeight * Math.Max(heightInLines, cardMinLines)) + cardVerticalPadding * 2f;
        var height = GetAdaptiveCardHeight(id, fallbackHeight, minHeight);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(style.WindowPadding.X, cardVerticalPadding));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(style.ItemSpacing.X, 4f));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(1f, 1f, 1f, 0.035f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 1f, 0.12f));
        try
        {
            ImGui.BeginChild(id, new Vector2(0f, height), true);
            try
            {
                ImGui.TextUnformatted(title);
                if (!string.IsNullOrWhiteSpace(description))
                {
                    ImGui.SameLine(0f, 6f);
                    DrawHelpMarker(description);
                }
                ImGui.Dummy(new Vector2(0f, cardContentGap));
                drawContent();
                RememberAdaptiveCardHeight(
                    id,
                    ImGui.GetCursorPosY() + cardVerticalPadding,
                    minHeight);
            }
            finally
            {
                ImGui.EndChild();
            }
        }
        finally
        {
            ImGui.PopStyleColor(2);
            ImGui.PopStyleVar(4);
        }
    }

    private float GetAdaptiveCardHeight(string key, float fallbackHeight, float minHeight)
    {
        if (adaptiveChildHeights.TryGetValue(key, out var cachedHeight))
            return Math.Max(minHeight, cachedHeight);

        return Math.Max(minHeight, fallbackHeight);
    }

    private void RememberAdaptiveCardHeight(string key, float contentHeight, float minHeight)
    {
        var resolvedHeight = Math.Max(minHeight, contentHeight);
        if (adaptiveChildHeights.TryGetValue(key, out var currentHeight)
            && Math.Abs(currentHeight - resolvedHeight) < 0.5f)
            return;

        adaptiveChildHeights[key] = resolvedHeight;
    }

    private void DrawHelpMarker(string tooltip)
    {
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            try
            {
                ImGui.PushTextWrapPos(Math.Min(ImGui.GetFontSize() * 26f, 560f));
                try
                {
                    ImGui.TextUnformatted(tooltip);
                }
                finally
                {
                    ImGui.PopTextWrapPos();
                }
            }
            finally
            {
                ImGui.EndTooltip();
            }
        }
    }

    private void DrawCompactHelp(string summary, string tooltip)
    {
        ImGui.TextDisabled(summary);
        ImGui.SameLine(0f, 2f);
        DrawHelpMarker(tooltip);
    }

    private bool DrawLabeledSliderFloat(
        string label,
        string id,
        ref float value,
        float minValue,
        float maxValue,
        string format)
    {
        ImGui.TextDisabled(label);
        ImGui.SetCursorPosY(Math.Max(0f, ImGui.GetCursorPosY() - 1f));
        ImGui.SetNextItemWidth(-1f);
        return ImGui.SliderFloat(id, ref value, minValue, maxValue, format);
    }

    private bool DrawLabeledSliderInt(
        string label,
        string id,
        ref int value,
        int minValue,
        int maxValue,
        string format)
    {
        ImGui.TextDisabled(label);
        ImGui.SetCursorPosY(Math.Max(0f, ImGui.GetCursorPosY() - 1f));
        ImGui.SetNextItemWidth(-1f);
        return ImGui.SliderInt(id, ref value, minValue, maxValue, format);
    }

    private bool BeginLabeledCombo(string label, string id, string previewValue)
    {
        ImGui.TextDisabled(label);
        ImGui.SetCursorPosY(Math.Max(0f, ImGui.GetCursorPosY() - 1f));
        ImGui.SetNextItemWidth(-1f);
        return ImGui.BeginCombo(id, previewValue);
    }

    private bool DrawLabeledCheckbox(
        string label,
        string id,
        ref bool value,
        string enabledText = "已开启",
        string disabledText = "已关闭")
    {
        ImGui.TextDisabled(label);
        ImGui.SetCursorPosY(Math.Max(0f, ImGui.GetCursorPosY() - 1f));
        var changed = ImGui.Checkbox(id, ref value);
        ImGui.SameLine(0f, 6f);
        ImGui.TextDisabled(value ? enabledText : disabledText);
        return changed;
    }

    private float GetAdaptiveChildHeight(string key, float minHeight, float maxHeight)
    {
        if (adaptiveChildHeights.TryGetValue(key, out var cachedHeight))
            return Math.Clamp(cachedHeight, minHeight, maxHeight);

        return maxHeight;
    }

    private void RememberAdaptiveChildHeight(string key, float contentHeight, float minHeight, float maxHeight)
    {
        var clampedHeight = Math.Clamp(contentHeight, minHeight, maxHeight);
        if (adaptiveChildHeights.TryGetValue(key, out var currentHeight)
            && Math.Abs(currentHeight - clampedHeight) < 0.5f)
            return;

        adaptiveChildHeights[key] = clampedHeight;
    }

    private static bool DrawFirstLevelHeader(string label, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None)
    {
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.25f, 0.45f, 0.75f, 0.45f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.25f, 0.45f, 0.75f, 0.55f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.25f, 0.45f, 0.75f, 0.65f));
        try
        {
            return ImGui.CollapsingHeader(label, flags);
        }
        finally
        {
            ImGui.PopStyleColor(3);
        }
    }

    private void DrawToggle(string label, ref bool value)
    {
        var current = value;
        if (ImGui.Checkbox(label, ref current))
        {
            value = current;
            config.Save();
        }
    }

    private static void DrawSemanticRow(string setting, string dps, string hps, string taken)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(setting);
        ImGui.TableSetColumnIndex(1);
        ImGui.TextUnformatted(dps);
        ImGui.TableSetColumnIndex(2);
        ImGui.TextUnformatted(hps);
        ImGui.TableSetColumnIndex(3);
        ImGui.TextUnformatted(taken);
    }

    private void DrawStoredWidthTable()
    {
        const ImGuiTableFlags flags =
            ImGuiTableFlags.RowBg
            | ImGuiTableFlags.BordersInnerH
            | ImGuiTableFlags.SizingFixedFit
            | ImGuiTableFlags.NoSavedSettings;

        if (!ImGui.BeginTable("##stored_widths", 4, flags))
            return;

        try
        {
            ImGui.TableSetupColumn("统计页");
            ImGui.TableSetupColumn("当前值");
            ImGui.TableSetupColumn("历史页");
            ImGui.TableSetupColumn("当前值");
            ImGui.TableHeadersRow();

            DrawStoredWidthPairRow("玩家列", config.FloatingStatsPlayerColumnWidth, "开始时间", config.HistoryStartTimeColumnWidth);
            DrawStoredWidthPairRow("职业列", config.FloatingStatsJobColumnWidth, "结束时间", config.HistoryEndTimeColumnWidth);
            DrawStoredWidthPairRow("伤害列", config.FloatingStatsDamageColumnWidth, "时长", config.HistoryDurationColumnWidth);
            DrawStoredWidthPairRow("秒伤列", config.FloatingStatsValueColumnWidth, null, 0f);
            DrawStoredWidthPairRow("死亡列", config.FloatingStatsDeathsColumnWidth, null, 0f);
        }
        finally
        {
            ImGui.EndTable();
        }
    }

    private void DrawSharedColumnCheckbox(string label, ref bool value, bool syncSharedSettings)
    {
        var current = value;
        if (!ImGui.Checkbox(label, ref current))
            return;

        value = current;
        if (syncSharedSettings)
            config.SyncSharedColumnSettings();

        config.Save();
    }

    private static void DrawStoredWidthPairRow(string? leftLabel, float leftWidth, string? rightLabel, float rightWidth)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(string.IsNullOrWhiteSpace(leftLabel) ? "-" : leftLabel);
        ImGui.TableSetColumnIndex(1);
        ImGui.TextUnformatted(string.IsNullOrWhiteSpace(leftLabel) ? "-" : FormatStoredWidth(leftWidth));
        ImGui.TableSetColumnIndex(2);
        ImGui.TextUnformatted(string.IsNullOrWhiteSpace(rightLabel) ? "-" : rightLabel);
        ImGui.TableSetColumnIndex(3);
        ImGui.TextUnformatted(string.IsNullOrWhiteSpace(rightLabel) ? "-" : FormatStoredWidth(rightWidth));
    }

    private static string FormatStoredWidth(float width)
        => width > 0f ? $"{width:0}px" : "自动";
}
