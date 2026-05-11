using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DalamudACT;

/// <summary>
/// 设置窗口封装，负责插件配置项的 ImGui 编辑界面，包括窗口、战斗结束规则、页面显示、配色和历史记录操作。
/// 相关参考：
/// - https://dalamud.dev/
/// - https://dalamud.dev/api/
/// 调整 Window 生命周期、ImGui 控件交互或设置项保存行为前，先对照 Dalamud 文档。
/// </summary>
internal sealed class SettingsWindow : Window
{
    private static readonly string PluginVersion = typeof(SettingsWindow).Assembly.GetName().Version?.ToString() ?? "未知版本";
    private readonly PluginConfiguration config;
    private readonly LocalStatsService statsService;
    private readonly Action openMainWindow;
    private readonly Action toggleFloatingStatsPanel;
    private readonly Action openCombatTimelineWindow;

    public SettingsWindow(
        PluginConfiguration config,
        LocalStatsService statsService,
        Action openMainWindow,
        Action toggleFloatingStatsPanel,
        Action openCombatTimelineWindow)
        : base($"DPS统计 设置 v{PluginVersion}###SettingsWindow")
    {
        this.config = config;
        this.statsService = statsService;
        this.openMainWindow = openMainWindow;
        this.toggleFloatingStatsPanel = toggleFloatingStatsPanel;
        this.openCombatTimelineWindow = openCombatTimelineWindow;
        Size = new Vector2(620f, 760f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        BgAlpha = Math.Clamp(config.WindowOpacity, 0.2f, 1f);

        ImGui.TextUnformatted("设置");
        ImGui.Separator();

        if (ImGui.Button("打开主界面"))
            openMainWindow();

        ImGui.SameLine();
        if (ImGui.Button(GetFloatingStatsButtonLabel()))
            toggleFloatingStatsPanel();

        ImGui.SameLine();
        if (ImGui.Button("打开战斗流水"))
            openCombatTimelineWindow();

        ImGui.Spacing();

        DrawWindowSection();
        DrawCombatSection();
        DrawVisibleTabsSection();
        DrawColumnsSection();
        DrawBarColorsSection();
        DrawThemePaletteSection();
        DrawMaintenanceSection();
    }

    private void DrawWindowSection()
    {
        if (!ImGui.CollapsingHeader("窗口设置", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        DrawSettingCard(
            "##window_settings_card",
            "窗口与悬浮面板",
            "统一控制主窗口透明度、悬浮统计面板透明度与悬浮面板状态。",
            9.2f,
            () =>
            {
                var opacity = config.WindowOpacity;
                if (ImGui.SliderFloat("主界面透明度", ref opacity, 0.2f, 1f))
                {
                    config.WindowOpacity = opacity;
                    config.Save();
                }

                var statsPanelOpacity = config.FloatingStatsOpacity;
                if (ImGui.SliderFloat("DPS统计面板透明度", ref statsPanelOpacity, 0f, 1f))
                {
                    config.FloatingStatsOpacity = statsPanelOpacity;
                    config.Save();
                }

                var showStats = config.ShowStatsPanel;
                if (ImGui.Checkbox("显示悬浮DPS统计面板", ref showStats))
                {
                    config.ShowStatsPanel = showStats;
                    config.ShowDemoPanel = showStats;
                    config.Save();
                }

                var lockFloatingStatsWindow = config.LockFloatingStatsWindow;
                if (ImGui.Checkbox("锁定悬浮窗口", ref lockFloatingStatsWindow))
                {
                    config.LockFloatingStatsWindow = lockFloatingStatsWindow;
                    config.Save();
                }

                ImGui.TextDisabled("启用后，悬浮窗口的位置和大小将无法手动修改。");
            });
    }

    private void DrawCombatSection()
    {
        if (!ImGui.CollapsingHeader("战斗结束设置"))
            return;

        DrawSettingCard(
            "##combat_end_rule_card",
            "战斗结束判定",
            "控制本地统计何时把当前战斗视为结束，并决定何时生成战斗快照。",
            8.8f,
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

                    ImGui.TextDisabled("全队脱战后，延迟 X 秒再视为战斗结束。");
                    return;
                }

                ImGui.TextDisabled("默认使用 PartyList，全队脱战后立即视为战斗结束。");
            });
    }

    private void DrawVisibleTabsSection()
    {
        if (!ImGui.CollapsingHeader("悬浮面板显示项目"))
            return;

        DrawSettingCard(
            "##visible_tabs_card",
            "标签页显示",
            "控制悬浮面板中哪些标签页可见，关闭后对应页签不会在悬浮面板中显示。",
            8.4f,
            () =>
            {
                DrawVisibleTabToggleGrid();

                if (!config.HasAnyVisibleStatsTab())
                    ImGui.TextDisabled("当前所有页面都已隐藏。");
            });

        ImGui.Spacing();
        DrawSettingCard(
            "##floating_layout_card",
            "表格布局参数",
            "控制玩家列最小宽度、固定列宽基准值和行高；设为 0 时会使用自动值。",
            8.8f,
            () =>
            {
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

                ImGui.TextDisabled("玩家列最小宽度和表格行高设置为 0 时会使用自动值。");
            });

        ImGui.Spacing();
        DrawSettingCard(
            "##floating_participant_card",
            "悬浮对象显示",
            "控制悬浮窗统计列表中显示玩家、友方 NPC 与敌方 NPC 的组合。",
            11.6f,
            DrawFloatingParticipantModeSection);
    }

    private void DrawColumnsSection()
    {
        if (!ImGui.CollapsingHeader("页面列显示"))
            return;

        ImGui.TextDisabled("以下设置会同时作用于 DPS / HPS / 承伤 三个页面。");
        ImGui.Spacing();

        DrawSettingCard(
            "##columns_toggle_card",
            "共享列开关",
            "控制 DPS / HPS / 承伤 三个页面的共享列显示与显示人数。",
            8.8f,
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

        ImGui.Spacing();
        DrawSettingCard(
            "##columns_semantic_card",
            "列语义映射",
            "下表说明同一组共享设置在 DPS / HPS / 承伤 中分别对应的实际列含义。",
            9.6f,
            DrawColumnSemanticTable);

        ImGui.Spacing();
        DrawSettingCard(
            "##columns_width_memory_card",
            "列宽记忆",
            "统计页与历史记录页的可拖拽列宽会自动写入配置，并在下次打开插件时恢复。",
            10.8f,
            () =>
            {
                DrawColumnWidthResetButtons();
                ImGui.TextDisabled("占比列与历史记录页的区域列保持自动拉伸，不参与固定宽度记忆。");
                DrawStoredWidthTable();
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
            8.8f,
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

                    ImGui.TextDisabled("单色模式会忽略职业主题色，切回主题色后会恢复下面的职业配色。");
                    return;
                }

                ImGui.TextDisabled("主题色模式会按职业使用各自颜色，可在下方调色板里自定义。");
            });
    }

    private void DrawThemePaletteSection()
    {
        if (!ImGui.CollapsingHeader("主题色调色板"))
            return;

        DrawSettingCard(
            "##theme_palette_card",
            "职业主题色调色板",
            "可分别调整每个职业的主题色；主题色模式下，占比条会使用这里的颜色。",
            17.0f,
            () =>
            {
                if (ImGui.Button("恢复默认主题色"))
                {
                    config.ResetThemeBarColors();
                    config.Save();
                }

                ImGui.SameLine();
                ImGui.TextDisabled("每个职业的主题色都可以单独调整。");

                if (!ImGui.BeginChild("##theme_palette", new Vector2(0f, 260f), true))
                    return;

                foreach (var group in JobThemePalette.GroupedEntries)
                {
                    if (!ImGui.CollapsingHeader(group.Key, ImGuiTreeNodeFlags.DefaultOpen))
                        continue;

                    foreach (var entry in group)
                    {
                        var color = config.GetThemeBarColor(entry.JobName);
                        if (ImGui.ColorEdit4($"{entry.JobName}##{entry.JobName}", ref color, ImGuiColorEditFlags.AlphaBar))
                        {
                            config.SetThemeBarColor(entry.JobName, color);
                            config.Save();
                        }
                    }
                }

                ImGui.EndChild();
            });
    }

    private void DrawMaintenanceSection()
    {
        if (!ImGui.CollapsingHeader("数据与状态", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        DrawSettingCard(
            "##maintenance_actions_card",
            "数据操作",
            "用于导入测试数据、历史记录导入导出、清空历史以及恢复插件默认设置。",
            8.4f,
            DrawMaintenanceActionGrid);

        ImGui.Spacing();
        DrawSettingCard(
            "##maintenance_logging_card",
            "日志与调试",
            "控制是否输出调试（Debug）/详细（Verbose）级别日志，便于排查问题；普通信息 / 警告 / 错误日志不受影响。",
            16.8f,
            DrawLoggingSection);

        ImGui.Spacing();
        DrawSettingCard(
            "##maintenance_status_card",
            "历史预览与状态",
            "控制历史记录预览时长，并查看当前历史文件路径、数据源和插件状态信息。",
            9.8f,
            () =>
            {
                ImGui.TextDisabled($"历史文件: {statsService.HistoryTransferFilePath}");
                var historyPreviewSeconds = config.HistoryPreviewSeconds;
                if (ImGui.SliderInt("历史记录预览时长（秒）", ref historyPreviewSeconds, 1, 30))
                {
                    config.HistoryPreviewSeconds = historyPreviewSeconds;
                    config.Save();
                }

                ImGui.TextDisabled("未进入战斗时，点击历史记录会无限预览该快照；进入战斗后，才按这里设置的秒数开始倒计时并自动回到当前统计。");
                if (!string.IsNullOrWhiteSpace(statsService.HistoryTransferStatusText))
                    ImGui.TextDisabled(statsService.HistoryTransferStatusText);

                ImGui.Spacing();
                ImGui.TextDisabled(statsService.DataSourceText);
                ImGui.TextDisabled(statsService.StatusText);
            });
    }

    private void DrawLoggingSection()
    {
        var enableDebugLog = config.EnableDebugLog;
        if (ImGui.Checkbox("启用调试日志", ref enableDebugLog))
        {
            config.EnableDebugLog = enableDebugLog;
            LogHelper.EnableDebugLog = enableDebugLog;
            config.Save();
            LogHelper.Info("设置", enableDebugLog ? "已从设置中启用调试日志。" : "已从设置中关闭调试日志。");
        }

        ImGui.TextDisabled("开启后，会把调试（Debug）与详细（Verbose）日志写入 Dalamud 插件日志。");
        ImGui.TextDisabled($"当前状态：{(config.EnableDebugLog ? "已开启" : "已关闭")}");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextUnformatted("最近日志摘要");

        ImGui.SameLine();
        LogUiHelper.DrawRecentLogToolbar();
        LogUiHelper.DrawRecentLogList();
    }

    private string GetFloatingStatsButtonLabel()
        => config.ShowStatsPanel ? "隐藏悬浮DPS统计面板" : "打开悬浮DPS统计面板";

    private void DrawSettingCard(string id, string title, string description, float heightInLines, Action drawContent)
    {
        var style = ImGui.GetStyle();
        var lineHeight = ImGui.GetTextLineHeightWithSpacing();
        var height = Math.Max(lineHeight * heightInLines, lineHeight * 4f) + style.WindowPadding.Y * 2f;

        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(1f, 1f, 1f, 0.035f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 1f, 0.12f));

        ImGui.BeginChild(id, new Vector2(0f, height), true);
        ImGui.TextUnformatted(title);
        ImGui.TextDisabled(description);
        ImGui.Spacing();
        drawContent();
        ImGui.EndChild();

        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(2);
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

    private void DrawMaintenanceActionGrid()
    {
        const ImGuiTableFlags flags =
            ImGuiTableFlags.SizingStretchSame
            | ImGuiTableFlags.NoSavedSettings;

        if (!ImGui.BeginTable("##maintenance_action_grid", 2, flags))
            return;

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        if (ImGui.Button("导入测试数据", new Vector2(-1f, 0f)))
            statsService.LoadTestData();
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Button("导出历史记录", new Vector2(-1f, 0f)))
            statsService.ExportHistoricalRecords();

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        if (ImGui.Button("导入历史记录", new Vector2(-1f, 0f)))
            statsService.ImportHistoricalRecords();
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Button("清空历史", new Vector2(-1f, 0f)))
            statsService.ClearHistory();

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        if (ImGui.Button("恢复默认", new Vector2(-1f, 0f)))
        {
            config.Reset();
            StatsPanel.RequestMetricColumnWidthReset();
            StatsPanel.RequestHistoryColumnWidthReset();
            config.Save();
            LogHelper.PrintWithModule("设置", "恢复默认", "已恢复插件默认配置，并重置统计页与历史页列宽记忆。");
        }
        ImGui.TableSetColumnIndex(1);
        ImGui.TextDisabled("恢复配置默认值");

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

        ImGui.Spacing();
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

        ImGui.TextDisabled("说明：友方 NPC 包括信赖/NPC 队友、Buddy、幻体等可识别的友方对象。");
        ImGui.TextDisabled("“玩家 + 敌方 NPC” 模式下会隐藏友方 NPC，只保留玩家与敌方对象。");
        ImGui.TextDisabled("敌方 NPC 只有在最大生命值达到本地玩家最大生命值指定倍率后，才会进入悬浮统计。");
        ImGui.TextDisabled("关闭“高亮 NPC 行”后，NPC 会回退到普通条形配色与默认文本颜色。");
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

        ImGui.EndTable();
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

    private void DrawToggle(string label, ref bool value)
    {
        var current = value;
        if (ImGui.Checkbox(label, ref current))
        {
            value = current;
            config.Save();
        }
    }
}
