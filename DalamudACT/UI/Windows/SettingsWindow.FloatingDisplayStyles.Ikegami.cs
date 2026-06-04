using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal sealed partial class SettingsWindow
{
    private void DrawIkegamiFloatingDisplayStyleSection()
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
        ImGui.TextDisabled("Ikegami 专属布局微调");
        DrawCompactHelp("这些参数只影响 Ikegami 样式。", "用于微调名字行、色块、正文、footer、滚动条与字号。");

        if (ImGui.CollapsingHeader("结构与显示", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var ikegamiPanelRaise = config.FloatingStatsIkegamiPanelRaise;
            var ikegamiDetailRaise = config.FloatingStatsIkegamiDetailRaise;
            var ikegamiFooterRaise = config.FloatingStatsIkegamiFooterRaise;
            var ikegamiMinimalMode = config.FloatingStatsIkegamiMinimalMode;
            var ikegamiShowMaxHitDetail = config.FloatingStatsIkegamiShowMaxHitDetail;
            var ikegamiShowNameLine = config.FloatingStatsIkegamiShowNameLine;
            var ikegamiShowScrollbar = config.FloatingStatsIkegamiShowScrollbar;
            var ikegamiShowVerticalScrollbar = config.FloatingStatsIkegamiShowVerticalScrollbar;

            if (ImGui.BeginTable("##ikegami_structure_grid", 2, compactTableFlags))
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("色块上移", "##ikegami_panel_raise", ref ikegamiPanelRaise, 0f, 60f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiPanelRaise = ikegamiPanelRaise;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("最高伤害行上移", "##ikegami_detail_raise", ref ikegamiDetailRaise, 0f, 60f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiDetailRaise = ikegamiDetailRaise;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("footer 上移距离", "##ikegami_footer_raise", ref ikegamiFooterRaise, 0f, 80f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiFooterRaise = ikegamiFooterRaise;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledCheckbox("显示最高伤害技能", "##ikegami_show_max_hit_detail", ref ikegamiShowMaxHitDetail))
                {
                    config.FloatingStatsIkegamiShowMaxHitDetail = ikegamiShowMaxHitDetail;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledCheckbox("显示姓名行", "##ikegami_show_name_line", ref ikegamiShowNameLine))
                {
                    config.FloatingStatsIkegamiShowNameLine = ikegamiShowNameLine;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledCheckbox("显示横向滚动条", "##ikegami_show_scrollbar", ref ikegamiShowScrollbar))
                {
                    config.FloatingStatsIkegamiShowScrollbar = ikegamiShowScrollbar;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledCheckbox("显示纵向滚动条", "##ikegami_show_vertical_scrollbar", ref ikegamiShowVerticalScrollbar))
                {
                    config.FloatingStatsIkegamiShowVerticalScrollbar = ikegamiShowVerticalScrollbar;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledCheckbox("极简模式", "##ikegami_minimal_mode", ref ikegamiMinimalMode))
                {
                    config.FloatingStatsIkegamiMinimalMode = ikegamiMinimalMode;
                    config.Save();
                }
                ImGui.SameLine(0f, 6f);
                DrawHelpMarker("开启后隐藏页签，只显示当前内容。关闭后恢复 DPS / HPS / 承伤 / 概览 / 历史记录页签。");
                ImGui.EndTable();
            }

            DrawCompactHelp("控制条带布局与显示开关。", "这里集中调整色块、最高伤害文本、footer 的纵向位置，以及 Ikegami 模式的显示开关。开启“极简模式”后会隐藏页签，只显示当前统计内容。");
        }

        if (ImGui.CollapsingHeader("尺寸与对齐", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var ikegamiBoxWidth = config.FloatingStatsIkegamiBoxWidth;
            var ikegamiBoxHeight = config.FloatingStatsIkegamiBoxHeight;
            var ikegamiBoxAlignment = config.FloatingStatsIkegamiBoxAlignment;
            var ikegamiBoxAlignmentLabel = GetIkegamiBoxAlignmentLabel(ikegamiBoxAlignment);
            var ikegamiNameHeight = config.FloatingStatsIkegamiNameHeight;
            var ikegamiNameLeftPadding = config.FloatingStatsIkegamiNameLeftPadding;
            var ikegamiNameRightPadding = config.FloatingStatsIkegamiNameRightPadding;
            var ikegamiJobBadgeSize = config.FloatingStatsIkegamiJobBadgeSize;
            var ikegamiHeaderHeight = config.FloatingStatsIkegamiHeaderHeight;
            var ikegamiHeaderLeftPadding = config.FloatingStatsIkegamiHeaderLeftPadding;
            var ikegamiDetailLeftPadding = config.FloatingStatsIkegamiDetailLeftPadding;

            if (ImGui.BeginTable("##ikegami_size_grid", 2, compactTableFlags))
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("小框宽度", "##ikegami_box_width", ref ikegamiBoxWidth, 1f, 260f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiBoxWidth = ikegamiBoxWidth;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("小框高度", "##ikegami_box_height", ref ikegamiBoxHeight, 1f, 140f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiBoxHeight = ikegamiBoxHeight;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (BeginLabeledCombo("小框对齐", "##ikegami_box_alignment", ikegamiBoxAlignmentLabel))
                {
                    foreach (var alignment in Enum.GetValues<IkegamiBoxAlignment>())
                    {
                        var isSelected = alignment == ikegamiBoxAlignment;
                        if (ImGui.Selectable(GetIkegamiBoxAlignmentLabel(alignment), isSelected))
                        {
                            config.FloatingStatsIkegamiBoxAlignment = alignment;
                            ikegamiBoxAlignment = alignment;
                            ikegamiBoxAlignmentLabel = GetIkegamiBoxAlignmentLabel(alignment);
                            config.Save();
                        }

                        if (isSelected)
                            ImGui.SetItemDefaultFocus();
                    }

                    ImGui.EndCombo();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("姓名行高度", "##ikegami_name_height", ref ikegamiNameHeight, 16f, 40f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiNameHeight = ikegamiNameHeight;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("姓名左边距", "##ikegami_name_left_padding", ref ikegamiNameLeftPadding, 0f, 40f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiNameLeftPadding = ikegamiNameLeftPadding;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("姓名右边距", "##ikegami_name_right_padding", ref ikegamiNameRightPadding, 0f, 40f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiNameRightPadding = ikegamiNameRightPadding;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("职业框尺寸", "##ikegami_job_badge_size", ref ikegamiJobBadgeSize, 12f, 36f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiJobBadgeSize = ikegamiJobBadgeSize;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("色块高度", "##ikegami_header_height", ref ikegamiHeaderHeight, 20f, 80f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiHeaderHeight = ikegamiHeaderHeight;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("色块左内边距", "##ikegami_header_left_padding", ref ikegamiHeaderLeftPadding, 0f, 32f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiHeaderLeftPadding = ikegamiHeaderLeftPadding;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("正文左内边距", "##ikegami_detail_left_padding", ref ikegamiDetailLeftPadding, 0f, 32f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiDetailLeftPadding = ikegamiDetailLeftPadding;
                    config.Save();
                }

                ImGui.EndTable();
            }

            DrawCompactHelp("小框居中相对于整个悬浮窗。", "这里可同时调小框尺寸、对齐方式、名字行高度、职业框尺寸，以及色块和正文的内边距。");
        }

        if (ImGui.CollapsingHeader("透明度"))
        {
            var ikegamiNameAlpha = config.FloatingStatsIkegamiNameAlpha;
            var ikegamiHeaderAlpha = config.FloatingStatsIkegamiHeaderAlpha;
            var ikegamiPanelBackgroundAlpha = config.FloatingStatsIkegamiPanelBackgroundAlpha;
            var ikegamiBodyAlpha = config.FloatingStatsIkegamiBodyAlpha;
            var ikegamiFooterAlpha = config.FloatingStatsIkegamiFooterAlpha;
            var ikegamiNameBackgroundAlpha = config.FloatingStatsIkegamiNameBackgroundAlpha;
            var ikegamiBodyBackgroundAlpha = config.FloatingStatsIkegamiBodyBackgroundAlpha;
            var ikegamiContentBackgroundAlpha = config.FloatingStatsIkegamiContentBackgroundAlpha;

            if (ImGui.BeginTable("##ikegami_alpha_grid", 3, compactTableFlags))
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("姓名字透", "##ikegami_name_alpha", ref ikegamiNameAlpha, 0f, 1f, "%.2f"))
                {
                    config.FloatingStatsIkegamiNameAlpha = ikegamiNameAlpha;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("色块字透", "##ikegami_header_alpha", ref ikegamiHeaderAlpha, 0f, 1f, "%.2f"))
                {
                    config.FloatingStatsIkegamiHeaderAlpha = ikegamiHeaderAlpha;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(2);
                if (DrawLabeledSliderFloat("外层底透", "##ikegami_panel_background_alpha", ref ikegamiPanelBackgroundAlpha, 0f, 1f, "%.2f"))
                {
                    config.FloatingStatsIkegamiPanelBackgroundAlpha = ikegamiPanelBackgroundAlpha;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("正文字透", "##ikegami_body_alpha", ref ikegamiBodyAlpha, 0f, 1f, "%.2f"))
                {
                    config.FloatingStatsIkegamiBodyAlpha = ikegamiBodyAlpha;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("Footer字透", "##ikegami_footer_alpha", ref ikegamiFooterAlpha, 0f, 1f, "%.2f"))
                {
                    config.FloatingStatsIkegamiFooterAlpha = ikegamiFooterAlpha;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(2);
                if (DrawLabeledSliderFloat("姓名底透", "##ikegami_name_background_alpha", ref ikegamiNameBackgroundAlpha, 0f, 1f, "%.2f"))
                {
                    config.FloatingStatsIkegamiNameBackgroundAlpha = ikegamiNameBackgroundAlpha;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("正文底透", "##ikegami_body_background_alpha", ref ikegamiBodyBackgroundAlpha, 0f, 1f, "%.2f"))
                {
                    config.FloatingStatsIkegamiBodyBackgroundAlpha = ikegamiBodyBackgroundAlpha;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("内容底透", "##ikegami_content_background_alpha", ref ikegamiContentBackgroundAlpha, 0f, 1f, "%.2f"))
                {
                    config.FloatingStatsIkegamiContentBackgroundAlpha = ikegamiContentBackgroundAlpha;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(2);
                ImGui.Dummy(Vector2.Zero);

                ImGui.EndTable();
            }

            DrawCompactHelp("分别控制文字与底色透明度。", "内容区底色是整块滚动内容背景；外层底板是单个小框外轮廓；footer 文字透明度单独控制底部条。");
        }

        if (ImGui.CollapsingHeader("Footer 与字号"))
        {
            var ikegamiFooterHeight = config.FloatingStatsIkegamiFooterHeight;
            var ikegamiFooterTimeZoneSpacing = config.FloatingStatsIkegamiFooterTimeZoneSpacing;
            var ikegamiFooterRightPadding = config.FloatingStatsIkegamiFooterRightPadding;
            var ikegamiTabFontScale = config.FloatingStatsIkegamiTabFontScale;
            var ikegamiNameFontScale = config.FloatingStatsIkegamiNameFontScale;
            var ikegamiHeaderFontScale = config.FloatingStatsIkegamiHeaderFontScale;
            var ikegamiBodyFontScale = config.FloatingStatsIkegamiBodyFontScale;
            var ikegamiFooterFontScale = config.FloatingStatsIkegamiFooterFontScale;
            var ikegamiTooltipFontScale = config.FloatingStatsIkegamiTooltipFontScale;

            if (ImGui.BeginTable("##ikegami_footer_font_grid", 3, compactTableFlags))
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("Footer高", "##ikegami_footer_height", ref ikegamiFooterHeight, 18f, 48f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiFooterHeight = ikegamiFooterHeight;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("时间区域距", "##ikegami_footer_time_zone_spacing", ref ikegamiFooterTimeZoneSpacing, 0f, 32f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiFooterTimeZoneSpacing = ikegamiFooterTimeZoneSpacing;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(2);
                if (DrawLabeledSliderFloat("DPS右边距", "##ikegami_footer_right_padding", ref ikegamiFooterRightPadding, 0f, 40f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiFooterRightPadding = ikegamiFooterRightPadding;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("页签字号", "##ikegami_tab_font_scale", ref ikegamiTabFontScale, 0.6f, 2.0f, "%.2f x"))
                {
                    config.FloatingStatsIkegamiTabFontScale = ikegamiTabFontScale;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("姓名字号", "##ikegami_name_font_scale", ref ikegamiNameFontScale, 0.6f, 2.0f, "%.2f x"))
                {
                    config.FloatingStatsIkegamiNameFontScale = ikegamiNameFontScale;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(2);
                if (DrawLabeledSliderFloat("色块字号", "##ikegami_header_font_scale", ref ikegamiHeaderFontScale, 0.6f, 2.0f, "%.2f x"))
                {
                    config.FloatingStatsIkegamiHeaderFontScale = ikegamiHeaderFontScale;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("正文字号", "##ikegami_body_font_scale", ref ikegamiBodyFontScale, 0.6f, 2.0f, "%.2f x"))
                {
                    config.FloatingStatsIkegamiBodyFontScale = ikegamiBodyFontScale;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("Footer字号", "##ikegami_footer_font_scale", ref ikegamiFooterFontScale, 0.6f, 2.0f, "%.2f x"))
                {
                    config.FloatingStatsIkegamiFooterFontScale = ikegamiFooterFontScale;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(2);
                if (DrawLabeledSliderFloat("Tooltip字号", "##ikegami_tooltip_font_scale", ref ikegamiTooltipFontScale, 0.6f, 2.0f, "%.2f x"))
                {
                    config.FloatingStatsIkegamiTooltipFontScale = ikegamiTooltipFontScale;
                    config.Save();
                }
                ImGui.EndTable();
            }

            DrawCompactHelp("统一调整 footer 与各区字号。", "页签、姓名行、色块、正文、footer 与 tooltip 的字号倍率都在这里。");
        }

        DrawCompactHelp("修改后会立即保存并实时生效。", "这些参数只写入 Ikegami 配置；切回 Classic 时不会覆盖经典样式参数。");
        }
        finally
        {
            ImGui.PopStyleVar(3);
        }
    }

}
