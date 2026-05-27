using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal sealed partial class SettingsWindow
{
    private void DrawMinimalFloatingDisplayStyleSection()
    {
        const ImGuiTableFlags compactTableFlags =
            ImGuiTableFlags.SizingStretchSame
            | ImGuiTableFlags.NoSavedSettings;
        var style = ImGui.GetStyle();

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(style.ItemSpacing.X, 2f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(style.FramePadding.X, 2f));
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(style.CellPadding.X, 2f));
        try
        {

        ImGui.Dummy(new Vector2(0f, 2f));
        ImGui.Separator();
        ImGui.TextDisabled("极简样式参数");
        DrawCompactHelp("极简样式固定隐藏页签与秒伤列。", "现在可更细地控制占比条里显示职业、伤害、死亡、占比，以及总DPS条里的时间、标题、总DPS、总伤和总死亡。");

        var showHeader = config.FloatingStatsMinimalShowHeader;
        var showSummaryRow = config.FloatingStatsMinimalShowSummaryRow;
        var showPlayerColumn = config.FloatingStatsMinimalShowPlayerColumn;
        var showDamageColumn = config.FloatingStatsMinimalShowDamageColumn;
        var showDeathsColumn = config.FloatingStatsMinimalShowDeathsColumn;
        var showPlayerNameInShareBar = config.FloatingStatsMinimalShowPlayerNameInShareBar;
        var showJobInShareBar = config.FloatingStatsMinimalShowJobInShareBar;
        var showDamageInShareBar = config.FloatingStatsMinimalShowDamageInShareBar;
        var showDeathsInShareBar = config.FloatingStatsMinimalShowDeathsInShareBar;
        var showRatioInShareBar = config.FloatingStatsMinimalShowRatioInShareBar;
        var showDurationInSummaryBar = config.FloatingStatsMinimalShowDurationInSummaryBar;
        var showTitleInSummaryBar = config.FloatingStatsMinimalShowTitleInSummaryBar;
        var showDpsInSummaryBar = config.FloatingStatsMinimalShowDpsInSummaryBar;
        var showDamageInSummaryBar = config.FloatingStatsMinimalShowDamageInSummaryBar;
        var showDeathsInSummaryBar = config.FloatingStatsMinimalShowDeathsInSummaryBar;
        var minimalAutoWindowHeight = config.FloatingStatsMinimalAutoWindowHeight;

        if (DrawLabeledCheckbox("高度自动适配条目数", "##minimal_auto_window_height", ref minimalAutoWindowHeight))
        {
            config.FloatingStatsMinimalAutoWindowHeight = minimalAutoWindowHeight;
            config.Save();
        }

        ImGui.TextDisabled("基础显示");
        if (ImGui.BeginTable("##minimal_basic_toggle_grid", 3, compactTableFlags))
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Checkbox("显示表头", ref showHeader))
            {
                config.FloatingStatsMinimalShowHeader = showHeader;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            if (ImGui.Checkbox("显示总DPS行", ref showSummaryRow))
            {
                config.FloatingStatsMinimalShowSummaryRow = showSummaryRow;
                config.Save();
            }

            ImGui.TableSetColumnIndex(2);
            if (ImGui.Checkbox("显示玩家列", ref showPlayerColumn))
            {
                config.FloatingStatsMinimalShowPlayerColumn = showPlayerColumn;
                config.Save();
            }

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Checkbox("显示伤害量列", ref showDamageColumn))
            {
                config.FloatingStatsMinimalShowDamageColumn = showDamageColumn;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            if (ImGui.Checkbox("显示死亡列", ref showDeathsColumn))
            {
                config.FloatingStatsMinimalShowDeathsColumn = showDeathsColumn;
                config.Save();
            }

            ImGui.EndTable();
        }

        ImGui.TextDisabled("占比条内容");
        if (ImGui.BeginTable("##minimal_share_toggle_grid", 3, compactTableFlags))
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Checkbox("占比条显示玩家名", ref showPlayerNameInShareBar))
            {
                config.FloatingStatsMinimalShowPlayerNameInShareBar = showPlayerNameInShareBar;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            if (ImGui.Checkbox("占比条显示职业", ref showJobInShareBar))
            {
                config.FloatingStatsMinimalShowJobInShareBar = showJobInShareBar;
                config.Save();
            }

            ImGui.TableSetColumnIndex(2);
            if (ImGui.Checkbox("占比条显示伤害", ref showDamageInShareBar))
            {
                config.FloatingStatsMinimalShowDamageInShareBar = showDamageInShareBar;
                config.Save();
            }

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Checkbox("占比条显示死亡", ref showDeathsInShareBar))
            {
                config.FloatingStatsMinimalShowDeathsInShareBar = showDeathsInShareBar;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            if (ImGui.Checkbox("占比条显示占比", ref showRatioInShareBar))
            {
                config.FloatingStatsMinimalShowRatioInShareBar = showRatioInShareBar;
                config.Save();
            }

            ImGui.EndTable();
        }

        ImGui.TextDisabled("总DPS条内容");
        if (ImGui.BeginTable("##minimal_summary_toggle_grid", 3, compactTableFlags))
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Checkbox("显示时间", ref showDurationInSummaryBar))
            {
                config.FloatingStatsMinimalShowDurationInSummaryBar = showDurationInSummaryBar;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            if (ImGui.Checkbox("显示标题", ref showTitleInSummaryBar))
            {
                config.FloatingStatsMinimalShowTitleInSummaryBar = showTitleInSummaryBar;
                config.Save();
            }

            ImGui.TableSetColumnIndex(2);
            if (ImGui.Checkbox("显示总DPS", ref showDpsInSummaryBar))
            {
                config.FloatingStatsMinimalShowDpsInSummaryBar = showDpsInSummaryBar;
                config.Save();
            }

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Checkbox("显示总伤害", ref showDamageInSummaryBar))
            {
                config.FloatingStatsMinimalShowDamageInSummaryBar = showDamageInSummaryBar;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            if (ImGui.Checkbox("显示总死亡数", ref showDeathsInSummaryBar))
            {
                config.FloatingStatsMinimalShowDeathsInSummaryBar = showDeathsInSummaryBar;
                config.Save();
            }

            ImGui.EndTable();
        }

        var minimalRowHeight = config.FloatingStatsMinimalRowHeight;
        var minimalFontScale = config.FloatingStatsMinimalFontScale;
        var minimalPlayerColumnWidth = config.FloatingStatsMinimalPlayerColumnWidth;
        var minimalDamageColumnWidth = config.FloatingStatsMinimalDamageColumnWidth;
        var minimalDeathsColumnWidth = config.FloatingStatsMinimalDeathsColumnWidth;

        ImGui.TextDisabled("尺寸与字号");
        if (ImGui.BeginTable("##minimal_size_grid", 2, compactTableFlags))
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (DrawLabeledSliderFloat("表格行高", "##minimal_row_height", ref minimalRowHeight, 1f, 60f, "%.0f"))
            {
                config.FloatingStatsMinimalRowHeight = minimalRowHeight;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            if (DrawLabeledSliderFloat("字号缩放", "##minimal_font_scale", ref minimalFontScale, 0.6f, 1.2f, "%.2f x"))
            {
                config.FloatingStatsMinimalFontScale = minimalFontScale;
                config.Save();
            }

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (DrawLabeledSliderFloat("玩家列宽", "##minimal_player_width", ref minimalPlayerColumnWidth, 1f, 400f, "%.0f"))
            {
                config.FloatingStatsMinimalPlayerColumnWidth = minimalPlayerColumnWidth;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            if (DrawLabeledSliderFloat("伤害量列宽", "##minimal_damage_width", ref minimalDamageColumnWidth, 1f, 400f, "%.0f"))
            {
                config.FloatingStatsMinimalDamageColumnWidth = minimalDamageColumnWidth;
                config.Save();
            }

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (DrawLabeledSliderFloat("死亡列宽", "##minimal_deaths_width", ref minimalDeathsColumnWidth, 1f, 200f, "%.0f"))
            {
                config.FloatingStatsMinimalDeathsColumnWidth = minimalDeathsColumnWidth;
                config.Save();
            }

            ImGui.EndTable();
        }

        DrawCompactHelp(
            "以上都是极简样式专属属性。",
            "开启“高度自动适配条目数”后，极简悬浮窗会按当前显示条目数、表头和总DPS行自动伸缩高度；关闭后可继续手动拖拽窗口高度。表格行高会同时影响单元格高度、占比条高度，并自动限制极简字号上限。");
        }
        finally
        {
            ImGui.PopStyleVar(3);
        }
    }
}
