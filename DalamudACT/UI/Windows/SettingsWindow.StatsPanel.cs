using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal sealed partial class SettingsWindow
{
    private void DrawFloatingPanelSection()
    {
        if (!DrawFirstLevelHeader("统计面板"))
            return;

        ImGui.Dummy(new Vector2(0f, 2f));

        DrawCombatSection();
        DrawVisibleTabsSection();
        DrawColumnsSection();
        DrawBarColorsSection();
        DrawThemePaletteSection();
    }

    private void DrawCombatSection()
    {
        if (!ImGui.CollapsingHeader("战斗结束设置"))
            return;

        DrawSettingCard(
            "##combat_end_rule_card",
            "战斗结束判定",
            "控制本地统计何时把当前战斗视为结束，并决定何时生成战斗快照。",
            6.4f,
            () =>
            {
                var currentRule = config.CombatEndRule;
                if (ImGui.RadioButton("全队脱战（PartyList）即为战斗结束", currentRule == CombatEndRule.PartyList))
                {
                    config.CombatEndRule = CombatEndRule.PartyList;
                    config.Save();
                }

                if (ImGui.RadioButton("全队脱战，且延迟 X 秒为战斗结束", currentRule == CombatEndRule.PartyListWithDelay))
                {
                    config.CombatEndRule = CombatEndRule.PartyListWithDelay;
                    config.Save();
                }

                if (config.CombatEndRule == CombatEndRule.PartyListWithDelay)
                {
                    var timeoutSeconds = config.EncounterTimeoutSeconds;
                    if (ImGui.SliderInt("X（秒）", ref timeoutSeconds, 5, 180))
                    {
                        config.EncounterTimeoutSeconds = timeoutSeconds;
                        config.Save();
                    }

                    DrawCompactHelp("延迟结束", "全队脱战后，额外等待 X 秒再视为战斗结束。");
                    return;
                }

                DrawCompactHelp("默认立即结束", "当前使用 PartyList 规则：全队脱战后立即视为战斗结束。");
            });
    }

    private void DrawVisibleTabsSection()
    {
        if (!ImGui.CollapsingHeader("悬浮面板样式管理"))
            return;

        DrawSettingCard(
            "##visible_tabs_card",
            "标签页显示",
            "控制悬浮面板中哪些标签页可见，关闭后对应页签不会在悬浮面板中显示。",
            6.2f,
            () =>
            {
                DrawVisibleTabToggleGrid();

                if (!config.HasAnyVisibleStatsTab())
                    ImGui.TextDisabled("当前所有页面都已隐藏。");
            });

        ImGui.Dummy(new Vector2(0f, 2f));
        DrawSettingCard(
            "##floating_display_style_card",
            "悬浮窗展示模式",
            "切换悬浮统计的展示样式；后续如果继续新增样式，也会从这里扩展。",
            10.8f,
            DrawFloatingDisplayStyleSection);

        ImGui.Dummy(new Vector2(0f, 2f));
        DrawSettingCard(
            "##floating_participant_card",
            "悬浮对象显示",
            "控制悬浮窗统计列表中显示玩家、友方 NPC 与敌方 NPC 的组合。",
            6.8f,
            DrawFloatingParticipantModeSection);
    }

    private void DrawColumnsSection()
    {
        if (!ImGui.CollapsingHeader("页面列显示"))
            return;

        DrawCompactHelp("以下设置同时作用于 DPS / HPS / 承伤。", "这里只改共享列；三页会一起生效。");
        ImGui.Dummy(new Vector2(0f, 2f));

        DrawSettingCard(
            "##columns_toggle_card",
            "共享列开关",
            "控制 DPS / HPS / 承伤 三个页面的共享列显示与显示人数。",
            6.6f,
            () =>
            {
                DrawSharedColumnToggleGrid();

                var visibleCount = config.DpsVisibleCount;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.SliderInt("显示人数", ref visibleCount, 1, 24))
                {
                    config.DpsVisibleCount = visibleCount;
                    config.Save();
                }
            });

        ImGui.Dummy(new Vector2(0f, 2f));
        DrawSettingCard(
            "##columns_semantic_card",
            "列语义映射",
            "下表说明同一组共享设置在 DPS / HPS / 承伤 中分别对应的实际列含义。",
            6.4f,
            () =>
            {
                if (ImGui.CollapsingHeader("查看列语义映射"))
                {
                    DrawColumnSemanticTable();
                }
                else
                {
                    ImGui.TextDisabled("默认先收起这张对照表；只有在核对 DPS / HPS / 承伤 列含义时再展开。");
                }
            });

        ImGui.Dummy(new Vector2(0f, 2f));
        DrawSettingCard(
            "##columns_width_memory_card",
            "列宽记忆",
            "统计页与历史记录页的可拖拽列宽会自动写入配置，并在下次打开插件时恢复。",
            6.8f,
            () =>
            {
                DrawColumnWidthResetButtons();
                if (!PluginConfiguration.UsesLegacyFloatingTableLayout(config.FloatingStatsDisplayStyle))
                    DrawCompactHelp("当前样式不使用统计页列宽记忆。", "历史页列宽记忆仍然有效。");

                if (ImGui.CollapsingHeader("查看当前列宽记忆"))
                {
                    ImGui.TextDisabled("占比列与历史记录页的区域列保持自动拉伸，不参与固定宽度记忆。");
                    DrawStoredWidthTable();
                }
                else
                {
                    ImGui.TextDisabled("默认先收起当前列宽数值；需要核对保存结果时再展开查看。");
                }
            });
    }

    private void DrawBarColorsSection()
    {
        if (!ImGui.CollapsingHeader("占比条配色"))
            return;

        DrawSettingCard(
            "##bar_color_mode_card",
            "占比条颜色模式",
            "可在主题色和单色之间切换；单色模式下所有职业共用同一种颜色。",
            5.2f,
            () =>
            {
                var isThemeMode = config.BarColorMode == StatsBarColorMode.Theme;
                if (ImGui.RadioButton("主题色", isThemeMode))
                {
                    config.BarColorMode = StatsBarColorMode.Theme;
                    config.Save();
                }

                ImGui.SameLine();
                if (ImGui.RadioButton("单色", !isThemeMode))
                {
                    config.BarColorMode = StatsBarColorMode.Single;
                    config.Save();
                }

                if (config.BarColorMode == StatsBarColorMode.Single)
                {
                    var singleColor = config.GetSingleBarColor();
                    if (ImGui.ColorEdit4("单色条颜色", ref singleColor, ImGuiColorEditFlags.AlphaBar))
                    {
                        config.SetSingleBarColor(singleColor);
                        config.Save();
                    }

                    DrawCompactHelp("单色模式会忽略职业主题色。", "切回主题色后会恢复下面的职业配色。");
                    return;
                }

                DrawCompactHelp("主题色模式按职业使用各自颜色。", "可在下方调色板里自定义。");
            });
    }

    private void DrawThemePaletteSection()
    {
        if (!ImGui.CollapsingHeader("主题色调色板"))
            return;

        DrawSettingCard(
            "##theme_palette_card",
            "职业主题色调色板",
            "可统一调整主题色透明度，并分别调整每个职业的 RGB 颜色；主题色模式下，占比条会使用这里的配置。",
            10.8f,
            () =>
            {
                var themeBarOpacity = config.ThemeBarOpacity;
                if (ImGui.SliderFloat("主题色透明度", ref themeBarOpacity, 0.2f, 1f, "%.2f"))
                {
                    config.ThemeBarOpacity = themeBarOpacity;
                    config.Save();
                }

                DrawCompactHelp("主题色透明度只影响统一 Alpha。", "这里统一控制所有职业主题色的透明度；下方单职业颜色编辑只调整 RGB。");
                ImGui.Dummy(new Vector2(0f, 2f));

                if (ImGui.Button("恢复默认主题色"))
                {
                    config.ThemeBarOpacity = PluginConfiguration.DefaultThemeBarOpacity;
                    config.ResetThemeBarColors();
                    config.Save();
                }

                ImGui.SameLine();
                DrawHelpMarker("恢复默认时会同时重置主题色透明度和所有职业颜色。");

                ImGui.SameLine(0f, 12f);
                if (ImGui.Button("职能单色"))
                {
                    config.ThemeBarOpacity = PluginConfiguration.DefaultThemeBarOpacity;
                    config.ApplyRoleThemeBarColors();
                    config.Save();
                }

                ImGui.SameLine();
                DrawHelpMarker("按职能写入高对比主题色：坦克蓝、治疗绿、输出红。近战、远敏、法系统一使用输出色。");

                ImGui.Dummy(new Vector2(0f, 4f));
                var highlightSelfBar = config.HighlightSelfBar;
                if (ImGui.Checkbox("高亮自身", ref highlightSelfBar))
                {
                    config.HighlightSelfBar = highlightSelfBar;
                    config.Save();
                }

                ImGui.SameLine(0f, 6f);
                DrawHelpMarker("开启后，统计面板里的本地玩家名字前会显示 ★ 标记，占比条颜色保持原职业颜色。");

                if (!ImGui.CollapsingHeader("职业颜色列表"))
                {
                    ImGui.TextDisabled("默认先收起职业颜色列表；需要微调单职业 RGB 时再展开。");
                    return;
                }

                foreach (var group in JobThemePalette.GroupedEntries)
                {
                    if (!ImGui.CollapsingHeader(group.Key))
                        continue;

                    foreach (var entry in group)
                    {
                        var color = config.GetThemeBarColor(entry.JobName);
                        if (ImGui.ColorEdit4(
                                $"{entry.JobName}##{entry.JobName}",
                                ref color,
                                ImGuiColorEditFlags.NoAlpha))
                        {
                            config.SetThemeBarColor(entry.JobName, color);
                            config.Save();
                        }
                    }
                }
            });
    }

    private void DrawSharedColumnToggleGrid()
    {
        const ImGuiTableFlags flags =
            ImGuiTableFlags.SizingStretchSame
            | ImGuiTableFlags.NoSavedSettings;

        if (!ImGui.BeginTable("##shared_column_toggle_grid", 2, flags))
            return;

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        DrawSharedColumnCheckbox("显示玩家列", ref config.ShowDpsPlayerColumn, true);
        ImGui.TableSetColumnIndex(1);
        DrawSharedColumnCheckbox("显示职业列", ref config.ShowDpsJobColumn, true);

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        DrawSharedColumnCheckbox("显示伤害列", ref config.ShowDpsDamageColumn, true);
        ImGui.TableSetColumnIndex(1);
        DrawSharedColumnCheckbox("显示秒伤列", ref config.ShowDpsValueColumn, true);

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        DrawSharedColumnCheckbox("显示死亡列", ref config.ShowDpsDeathsColumn, false);
        ImGui.TableSetColumnIndex(1);
        ImGui.TextDisabled("同时作用于三页");

        ImGui.EndTable();
    }

    private void DrawVisibleTabToggleGrid()
    {
        const ImGuiTableFlags flags =
            ImGuiTableFlags.SizingStretchSame
            | ImGuiTableFlags.NoSavedSettings;

        if (!ImGui.BeginTable("##visible_tab_toggle_grid", 2, flags))
            return;

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        DrawToggle("显示 DPS", ref config.ShowDpsTab);
        ImGui.TableSetColumnIndex(1);
        DrawToggle("显示 HPS", ref config.ShowHpsTab);

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        DrawToggle("显示 承伤", ref config.ShowTakenTab);
        ImGui.TableSetColumnIndex(1);
        DrawToggle("显示 概览", ref config.ShowOverviewTab);

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        DrawToggle("显示 历史记录", ref config.ShowHistoryTab);
        ImGui.TableSetColumnIndex(1);
        ImGui.TextDisabled("至少保留一个页签");

        ImGui.EndTable();
    }

    private void DrawColumnWidthResetButtons()
    {
        if (ImGui.Button("重置统计页列宽记忆"))
        {
            config.ResetSharedMetricColumnWidths();
            StatsPanel.RequestMetricColumnWidthReset();
            config.Save();
            LogHelper.PrintWithModule("设置", "列宽记忆", "已重置统计页列宽记忆。");
        }

        ImGui.SameLine();
        if (ImGui.Button("重置历史页列宽记忆"))
        {
            config.ResetHistoryColumnWidths();
            StatsPanel.RequestHistoryColumnWidthReset();
            config.Save();
            LogHelper.PrintWithModule("设置", "列宽记忆", "已重置历史页列宽记忆。");
        }
    }

    private void DrawColumnSemanticTable()
    {
        const ImGuiTableFlags flags =
            ImGuiTableFlags.RowBg
            | ImGuiTableFlags.BordersInnerH
            | ImGuiTableFlags.SizingFixedFit
            | ImGuiTableFlags.NoSavedSettings;

        if (!ImGui.BeginTable("##column_semantics", 4, flags))
            return;

        ImGui.TableSetupColumn("设置项");
        ImGui.TableSetupColumn("DPS");
        ImGui.TableSetupColumn("HPS");
        ImGui.TableSetupColumn("承伤");
        ImGui.TableHeadersRow();

        DrawSemanticRow("玩家列", "玩家", "玩家", "玩家");
        DrawSemanticRow("职业列", "职业", "职业", "职业");
        DrawSemanticRow("伤害列", "伤害量", "治疗量", "承伤量");
        DrawSemanticRow("秒伤列", "秒伤", "秒疗", "秒承伤");
        DrawSemanticRow("死亡列", "死亡", "死亡", "死亡");
        DrawSemanticRow("显示人数", "限制显示条目数", "限制显示条目数", "限制显示条目数");

        ImGui.EndTable();
    }

    private void DrawFloatingParticipantModeSection()
    {
        var currentMode = config.FloatingStatsParticipantDisplayMode;

        if (ImGui.RadioButton("智能：多人仅玩家，单人可含友方 NPC", currentMode == FloatingStatsParticipantDisplayMode.Auto))
        {
            config.FloatingStatsParticipantDisplayMode = FloatingStatsParticipantDisplayMode.Auto;
            currentMode = config.FloatingStatsParticipantDisplayMode;
            config.Save();
        }

        if (ImGui.RadioButton("仅玩家", currentMode == FloatingStatsParticipantDisplayMode.PlayersOnly))
        {
            config.FloatingStatsParticipantDisplayMode = FloatingStatsParticipantDisplayMode.PlayersOnly;
            currentMode = config.FloatingStatsParticipantDisplayMode;
            config.Save();
        }

        if (ImGui.RadioButton("玩家 + 友方 NPC", currentMode == FloatingStatsParticipantDisplayMode.PlayersAndFriendlyNpc))
        {
            config.FloatingStatsParticipantDisplayMode = FloatingStatsParticipantDisplayMode.PlayersAndFriendlyNpc;
            currentMode = config.FloatingStatsParticipantDisplayMode;
            config.Save();
        }

        if (ImGui.RadioButton("玩家 + 敌方 NPC", currentMode == FloatingStatsParticipantDisplayMode.PlayersAndHostileNpc))
        {
            config.FloatingStatsParticipantDisplayMode = FloatingStatsParticipantDisplayMode.PlayersAndHostileNpc;
            currentMode = config.FloatingStatsParticipantDisplayMode;
            config.Save();
        }

        ImGui.Dummy(new Vector2(0f, 2f));
        var hostileNpcMinHpMultiplier = config.HostileNpcMinHpMultiplier;
        if (ImGui.SliderInt("敌方 NPC 最低血量倍率", ref hostileNpcMinHpMultiplier, 1, 100, "%d x"))
        {
            config.HostileNpcMinHpMultiplier = hostileNpcMinHpMultiplier;
            config.Save();
        }

        var highlightNpcRows = config.HighlightNpcRows;
        if (ImGui.Checkbox("高亮 NPC 行", ref highlightNpcRows))
        {
            config.HighlightNpcRows = highlightNpcRows;
            config.Save();
        }

        if (ImGui.CollapsingHeader("规则说明"))
        {
            ImGui.TextDisabled("友方 NPC 包括信赖/NPC 队友、Buddy、幻体等可识别的友方对象。");
            ImGui.TextDisabled("“玩家 + 敌方 NPC” 模式下会隐藏友方 NPC，只保留玩家与敌方对象。");
            ImGui.TextDisabled("敌方 NPC 只有在最大生命值达到本地玩家最大生命值指定倍率后，才会进入悬浮统计。");
            ImGui.TextDisabled("关闭“高亮 NPC 行”后，NPC 会回退到普通条形配色与默认文本颜色。");
        }
        else
        {
            ImGui.TextDisabled("默认先收起规则说明；需要核对 NPC 纳入规则时再展开。");
        }
    }
}
