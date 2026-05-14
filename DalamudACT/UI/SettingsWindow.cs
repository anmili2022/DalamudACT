using System;
using System.Collections.Generic;
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
    private readonly Dictionary<string, float> adaptiveChildHeights = new();
    private string floatingStyleShareCode = string.Empty;
    private string floatingStyleTransferStatusText = string.Empty;

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

        ImGui.Dummy(new Vector2(0f, 2f));

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
            6.4f,
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

                DrawCompactHelp("锁定后不可拖动或缩放。", "启用后，悬浮窗口的位置和大小将无法手动修改。");
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
        if (!ImGui.CollapsingHeader("悬浮面板显示项目"))
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

                if (!ImGui.CollapsingHeader("职业颜色列表"))
                {
                    ImGui.TextDisabled("默认先收起职业颜色列表；需要微调单职业 RGB 时再展开。");
                    return;
                }

                var themePaletteLineHeight = ImGui.GetTextLineHeightWithSpacing();
                var themePaletteMinHeight = themePaletteLineHeight * 5.8f;
                var themePaletteMaxHeight = themePaletteLineHeight * 10.0f;
                var themePaletteHeight = GetAdaptiveChildHeight("##theme_palette", themePaletteMinHeight, themePaletteMaxHeight);
                if (!ImGui.BeginChild("##theme_palette", new Vector2(0f, themePaletteHeight), true))
                    return;

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

                RememberAdaptiveChildHeight(
                    "##theme_palette",
                    ImGui.GetCursorPosY() + ImGui.GetStyle().WindowPadding.Y,
                    themePaletteMinHeight,
                    themePaletteMaxHeight);
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
            6.4f,
            DrawMaintenanceActionGrid);

        ImGui.Dummy(new Vector2(0f, 2f));
        DrawSettingCard(
            "##maintenance_logging_card",
            "日志与调试",
            "控制是否输出调试（Debug）/详细（Verbose）级别日志，便于排查问题；普通信息 / 警告 / 错误日志不受影响。",
            9.8f,
            DrawLoggingSection);

        ImGui.Dummy(new Vector2(0f, 2f));
        DrawSettingCard(
            "##maintenance_status_card",
            "历史预览与状态",
            "控制历史记录预览时长，并查看当前历史文件路径、数据源和插件状态信息。",
            6.6f,
            () =>
            {
                var historyPreviewSeconds = config.HistoryPreviewSeconds;
                if (ImGui.SliderInt("历史记录预览时长（秒）", ref historyPreviewSeconds, 1, 30))
                {
                    config.HistoryPreviewSeconds = historyPreviewSeconds;
                    config.Save();
                }

                DrawCompactHelp("预览规则说明", "未进入战斗时，点击历史记录会无限预览该快照；进入战斗后，才按这里设置的秒数开始倒计时并自动回到当前统计。");
                if (!string.IsNullOrWhiteSpace(statsService.HistoryTransferStatusText))
                    ImGui.TextDisabled(statsService.HistoryTransferStatusText);

                if (ImGui.CollapsingHeader("路径与状态详情"))
                {
                    ImGui.TextDisabled($"历史文件: {statsService.HistoryTransferFilePath}");
                    ImGui.Dummy(new Vector2(0f, 2f));
                    ImGui.TextDisabled(statsService.DataSourceText);
                    ImGui.TextDisabled(statsService.StatusText);
                }
                else
                {
                    ImGui.TextDisabled("默认先收起路径与状态详情；需要排查时再展开查看。");
                }
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

        DrawCompactHelp("日志写入规则", "开启后，会把调试（Debug）与详细（Verbose）日志写入 Dalamud 插件日志。");
        ImGui.TextDisabled($"当前状态：{(config.EnableDebugLog ? "已开启" : "已关闭")}");

        if (!ImGui.CollapsingHeader("最近日志摘要"))
        {
            ImGui.TextDisabled(LogUiHelper.HasRecentLogs
                ? "默认先收起最近日志摘要；需要查看最近输出时再展开。"
                : "当前没有最近日志摘要。");
            return;
        }

        LogUiHelper.DrawRecentLogToolbar();
        LogUiHelper.DrawRecentLogList(10);
    }

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

        ImGui.BeginChild(id, new Vector2(0f, height), true);
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
        ImGui.EndChild();

        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(4);
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
            ImGui.PushTextWrapPos(Math.Min(ImGui.GetFontSize() * 26f, 560f));
            ImGui.TextUnformatted(tooltip);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
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

    private void DrawMinimalFloatingDisplayStyleSection()
    {
        const ImGuiTableFlags compactTableFlags =
            ImGuiTableFlags.SizingStretchSame
            | ImGuiTableFlags.NoSavedSettings;
        var style = ImGui.GetStyle();

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(style.ItemSpacing.X, 2f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(style.FramePadding.X, 2f));
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(style.CellPadding.X, 2f));

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

        DrawCompactHelp("以上都是极简样式专属属性。", "我把开关和滑块压成了多列表格；现在表格行高会同时影响单元格高度、占比条高度，并自动限制极简字号上限。");
        ImGui.PopStyleVar(3);
    }

    private void DrawIkegamiFloatingDisplayStyleSection()
    {
        const ImGuiTableFlags compactTableFlags =
            ImGuiTableFlags.SizingStretchSame
            | ImGuiTableFlags.NoSavedSettings;
        var style = ImGui.GetStyle();

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(style.ItemSpacing.X, 2f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(style.FramePadding.X, 2f));
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(style.CellPadding.X, 2f));

        ImGui.Dummy(new Vector2(0f, 2f));
        ImGui.Separator();
        ImGui.TextDisabled("Ikegami 专属布局微调");
        DrawCompactHelp("这些参数只影响 Ikegami 样式。", "用于微调名字行、色块、正文、footer、滚动条与字号。");

        if (ImGui.CollapsingHeader("结构与显示", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var ikegamiPanelRaise = config.FloatingStatsIkegamiPanelRaise;
            var ikegamiDetailRaise = config.FloatingStatsIkegamiDetailRaise;
            var ikegamiFooterRaise = config.FloatingStatsIkegamiFooterRaise;
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
                ImGui.Dummy(Vector2.Zero);
                ImGui.EndTable();
            }

            DrawCompactHelp("控制条带布局与显示开关。", "这里集中调整色块、最高伤害文本、footer 的纵向位置，以及 Ikegami 模式的显示开关。");
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
        ImGui.PopStyleVar(3);
    }

    private void DrawFloatingStyleFileManagementSection()
    {
        ImGui.Dummy(new Vector2(0f, 2f));
        ImGui.Separator();
        if (!ImGui.CollapsingHeader("样式管理"))
            return;

        ImGui.TextDisabled("样式分享码");
        foreach (var style in new[]
                 {
                     FloatingStatsDisplayStyle.Classic,
                     FloatingStatsDisplayStyle.Ikegami,
                     FloatingStatsDisplayStyle.Minimal,
                 })
        {
            if (style != FloatingStatsDisplayStyle.Classic)
                ImGui.SameLine();

            if (!ImGui.Button($"生成并复制 {GetFloatingStyleShareCodeStyleLabel(style)} 分享码"))
                continue;

            if (config.TryGenerateFloatingStyleShareCode(
                    style,
                    out var shareCode,
                    out var message))
            {
                floatingStyleShareCode = shareCode;
                ImGui.SetClipboardText(shareCode);
            }

            floatingStyleTransferStatusText = message;
        }

        DrawCompactHelp("生成后会自动复制到剪贴板。", "对外分享时直接发送整段文本即可。");

        var shareCodeBoxHeight = ImGui.GetTextLineHeightWithSpacing() * 3.0f;

        DrawCompactHelp("同一个输入框可粘贴或暂存分享码。", "复制按钮适合转发现成内容；导入时会自动识别 Classic / Ikegami / Minimal。");
        if (ImGui.Button("复制当前分享码"))
        {
            ImGui.SetClipboardText(floatingStyleShareCode ?? string.Empty);
            floatingStyleTransferStatusText = "已复制当前分享码。";
        }

        ImGui.SameLine();
        if (ImGui.Button("清空分享码"))
        {
            floatingStyleShareCode = string.Empty;
            floatingStyleTransferStatusText = "已清空分享码输入框。";
        }

        floatingStyleShareCode ??= string.Empty;
        ImGui.InputTextMultiline(
            "##floating_style_share_code",
            ref floatingStyleShareCode,
            65535,
            new Vector2(-1f, shareCodeBoxHeight));

        if (config.TryPeekFloatingStyleShareCodeStyle(floatingStyleShareCode, out var detectedStyle))
        {
            ImGui.TextDisabled($"已识别分享码样式：{GetFloatingStyleShareCodeStyleLabel(detectedStyle)}");
        }
        else if (!string.IsNullOrWhiteSpace(floatingStyleShareCode))
        {
            ImGui.TextDisabled("当前输入内容还不是可识别的分享码。");
        }

        if (ImGui.Button("按分享码标识导入"))
        {
            config.ImportFloatingStyleShareCode(
                floatingStyleShareCode,
                out floatingStyleTransferStatusText);
        }

        ImGui.Dummy(new Vector2(0f, 4f));
        ImGui.Separator();
        ImGui.TextDisabled("按样式恢复默认");

        foreach (var style in new[]
                 {
                     FloatingStatsDisplayStyle.Classic,
                     FloatingStatsDisplayStyle.Ikegami,
                     FloatingStatsDisplayStyle.Minimal,
                 })
        {
            if (style != FloatingStatsDisplayStyle.Classic)
                ImGui.SameLine();

            if (!ImGui.Button($"恢复 {GetFloatingStyleShareCodeStyleLabel(style)} 默认"))
                continue;

            config.ResetFloatingStyleToDefaults(style, out floatingStyleTransferStatusText);
            if (style == config.FloatingStatsDisplayStyle)
            {
                StatsPanel.RequestMetricColumnWidthReset();
                StatsPanel.RequestHistoryColumnWidthReset();
            }
        }

        DrawCompactHelp("只恢复指定样式的默认设置。", "恢复当前正在使用的样式时，会立即刷新当前界面；其它样式会写回各自样式文件，等切过去时生效。");

        if (!string.IsNullOrWhiteSpace(floatingStyleTransferStatusText))
            ImGui.TextWrapped(floatingStyleTransferStatusText);
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

    private static string GetFloatingDisplayStyleLabel(FloatingStatsDisplayStyle style)
        => style switch
        {
            FloatingStatsDisplayStyle.Classic => "Classic（经典表格）",
            FloatingStatsDisplayStyle.Ikegami => "Ikegami",
            FloatingStatsDisplayStyle.Minimal => "Minimal（极简样式）",
            _ => style.ToString(),
        };

    private static string GetFloatingDisplayStyleDescription(FloatingStatsDisplayStyle style)
        => style switch
        {
            FloatingStatsDisplayStyle.Classic => "经典表格布局，保留列宽、固定列宽和表格行高等旧参数。",
            FloatingStatsDisplayStyle.Ikegami => "横向条带卡片布局，使用专属的尺寸、透明度、滚动条与 footer 参数。",
            FloatingStatsDisplayStyle.Minimal => "极简表格布局：固定只显示 DPS，无页签；职业列与秒伤列会合并到占比条文字。",
            _ => "未识别的展示样式。",
        };

    private static string GetIkegamiBoxAlignmentLabel(IkegamiBoxAlignment alignment)
        => alignment switch
        {
            IkegamiBoxAlignment.Left => "左对齐",
            IkegamiBoxAlignment.Center => "居中",
            IkegamiBoxAlignment.Right => "右对齐",
            _ => alignment.ToString(),
        };

    private static string GetFloatingStyleShareCodeStyleLabel(FloatingStatsDisplayStyle style)
        => style switch
        {
            FloatingStatsDisplayStyle.Ikegami => "Ikegami",
            FloatingStatsDisplayStyle.Minimal => "Minimal",
            _ => "Classic",
        };

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
