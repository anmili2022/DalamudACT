using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DalamudACT;

internal sealed class SettingsWindow : Window
{
    private readonly PluginConfiguration config;
    private readonly LocalStatsService statsService;
    private readonly Action openMainWindow;
    private readonly Action toggleFloatingStatsPanel;

    public SettingsWindow(
        PluginConfiguration config,
        LocalStatsService statsService,
        Action openMainWindow,
        Action toggleFloatingStatsPanel)
        : base("DPS统计 设置", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.config = config;
        this.statsService = statsService;
        this.openMainWindow = openMainWindow;
        this.toggleFloatingStatsPanel = toggleFloatingStatsPanel;
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

        ImGui.Spacing();

        DrawWindowSection();
        DrawCombatSection();
        DrawVisibleTabsSection();
        DrawDpsColumnsSection();
        DrawBarColorsSection();
        DrawThemePaletteSection();
        DrawMaintenanceSection();
    }

    private void DrawWindowSection()
    {
        if (!ImGui.CollapsingHeader("窗口设置", ImGuiTreeNodeFlags.DefaultOpen))
            return;

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
    }

    private void DrawCombatSection()
    {
        if (!ImGui.CollapsingHeader("战斗结束设置", ImGuiTreeNodeFlags.DefaultOpen))
            return;

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
    }

    private void DrawVisibleTabsSection()
    {
        if (!ImGui.CollapsingHeader("悬浮面板显示项目", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        DrawToggle("显示 DPS", ref config.ShowDpsTab);
        DrawToggle("显示 HPS", ref config.ShowHpsTab);
        DrawToggle("显示 承伤", ref config.ShowTakenTab);
        DrawToggle("显示 概览", ref config.ShowOverviewTab);
        DrawToggle("显示 历史记录", ref config.ShowHistoryTab);

        ImGui.Spacing();

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

        ImGui.TextDisabled("玩家列最小宽度和表格行高设为 0 时会使用自动值。");

        if (!config.HasAnyVisibleStatsTab())
            ImGui.TextDisabled("当前所有页面都已隐藏。");
    }

    private void DrawDpsColumnsSection()
    {
        if (!ImGui.CollapsingHeader("DPS 页面列显示", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        DrawToggle("显示职业列", ref config.ShowDpsJobColumn);
        DrawToggle("显示伤害量列", ref config.ShowDpsDamageColumn);
        DrawToggle("显示秒伤列", ref config.ShowDpsValueColumn);
        DrawToggle("显示死亡列", ref config.ShowDpsDeathsColumn);

        var visibleCount = config.DpsVisibleCount;
        if (ImGui.SliderInt("显示人数", ref visibleCount, 1, 24))
        {
            config.DpsVisibleCount = visibleCount;
            config.Save();
        }
    }

    private void DrawBarColorsSection()
    {
        if (!ImGui.CollapsingHeader("占比条配色", ImGuiTreeNodeFlags.DefaultOpen))
            return;

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
    }

    private void DrawThemePaletteSection()
    {
        if (!ImGui.CollapsingHeader("主题色调色板", ImGuiTreeNodeFlags.DefaultOpen))
            return;

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
    }

    private void DrawMaintenanceSection()
    {
        if (!ImGui.CollapsingHeader("数据与状态", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        if (ImGui.Button("导入测试数据"))
            statsService.LoadTestData();

        ImGui.SameLine();
        if (ImGui.Button("导出历史记录"))
            statsService.ExportHistoricalRecords();

        ImGui.SameLine();
        if (ImGui.Button("导入历史记录"))
            statsService.ImportHistoricalRecords();

        ImGui.SameLine();
        if (ImGui.Button("清空历史"))
            statsService.ClearHistory();

        ImGui.SameLine();
        if (ImGui.Button("恢复默认"))
        {
            config.Reset();
            config.Save();
        }

        ImGui.Spacing();
        ImGui.TextDisabled($"历史文件: {statsService.HistoryTransferFilePath}");
        if (!string.IsNullOrWhiteSpace(statsService.HistoryTransferStatusText))
            ImGui.TextDisabled(statsService.HistoryTransferStatusText);

        ImGui.Spacing();
        ImGui.TextDisabled(statsService.DataSourceText);
        ImGui.TextDisabled(statsService.StatusText);
    }

    private string GetFloatingStatsButtonLabel()
        => config.ShowStatsPanel ? "隐藏悬浮DPS统计面板" : "打开悬浮DPS统计面板";

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
