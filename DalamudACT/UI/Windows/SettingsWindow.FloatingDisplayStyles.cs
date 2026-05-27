using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal sealed partial class SettingsWindow
{
    private void DrawFloatingDisplayStyleSection()
    {
        var currentStyle = config.FloatingStatsDisplayStyle;
        var currentLabel = GetFloatingDisplayStyleLabel(currentStyle);
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo("展示模式", currentLabel))
        {
            foreach (FloatingStatsDisplayStyle style in Enum.GetValues(typeof(FloatingStatsDisplayStyle)))
            {
                var isSelected = currentStyle == style;
                if (ImGui.Selectable(GetFloatingDisplayStyleLabel(style), isSelected))
                {
                    config.SwitchFloatingStatsDisplayStyle(style);
                    currentStyle = config.FloatingStatsDisplayStyle;
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        DrawCompactHelp("当前样式说明", GetFloatingDisplayStyleDescription(currentStyle));
        DrawFloatingStyleFileManagementSection();

        if (currentStyle == FloatingStatsDisplayStyle.Ikegami)
        {
            DrawIkegamiFloatingDisplayStyleSection();
            return;
        }

        if (currentStyle == FloatingStatsDisplayStyle.Minimal)
        {
            DrawMinimalFloatingDisplayStyleSection();
            return;
        }

        if (!PluginConfiguration.UsesLegacyFloatingTableLayout(currentStyle))
        {
            ImGui.Dummy(new Vector2(0f, 2f));
            DrawCompactHelp("当前样式不再使用旧表格参数。", "如果后续为该样式补专属参数，也会放在这里。");
            return;
        }

        ImGui.Dummy(new Vector2(0f, 2f));
        ImGui.Separator();
        ImGui.TextDisabled("经典表格样式参数");

        var playerColumnMinWidth = config.FloatingStatsPlayerColumnMinWidth;
        if (ImGui.SliderFloat("玩家列最小宽度", ref playerColumnMinWidth, 0f, 360f, "%.0f"))
        {
            config.FloatingStatsPlayerColumnMinWidth = playerColumnMinWidth;
            config.Save();
        }

        var metricColumnWidth = config.FloatingStatsMetricColumnWidth;
        if (ImGui.SliderFloat("固定列宽", ref metricColumnWidth, 48f, 220f, "%.0f"))
        {
            config.FloatingStatsMetricColumnWidth = metricColumnWidth;
            config.Save();
        }

        var rowHeight = config.FloatingStatsRowHeight;
        if (ImGui.SliderFloat("表格行高", ref rowHeight, 0f, 60f, "%.0f"))
        {
            config.FloatingStatsRowHeight = rowHeight;
            config.Save();
        }

        DrawCompactHelp("玩家列宽 / 行高设为 0 时自动取值。", "把玩家列最小宽度或表格行高拖到 0，会回退到自动布局。");
    }


}
