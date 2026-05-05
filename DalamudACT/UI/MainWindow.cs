using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DalamudACT;

internal sealed class MainWindow : Window
{
    private readonly PluginConfiguration config;
    private readonly LocalStatsService statsService;
    private readonly Action openSettings;
    private readonly Action toggleFloatingStatsPanel;

    public MainWindow(
        PluginConfiguration config,
        LocalStatsService statsService,
        Action openSettings,
        Action toggleFloatingStatsPanel)
        : base("DPS统计", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.config = config;
        this.statsService = statsService;
        this.openSettings = openSettings;
        this.toggleFloatingStatsPanel = toggleFloatingStatsPanel;
        Size = new Vector2(560f, 320f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        BgAlpha = Math.Clamp(config.WindowOpacity, 0.2f, 1f);

        ImGui.TextUnformatted("ACTX 风格本地统计");
        ImGui.Separator();
        ImGui.TextWrapped("当前版本直接在 Dalamud 内采集战斗事件，并使用 ACTX / NotACT 的统计口径生成本地战斗快照，不再依赖外部 MiniParse。");
        ImGui.Spacing();

        if (ImGui.Button("打开设置"))
            openSettings();

        ImGui.SameLine();
        if (ImGui.Button(GetFloatingStatsButtonLabel()))
            toggleFloatingStatsPanel();

        ImGui.Spacing();
        ImGui.TextDisabled(statsService.StatusText);
        ImGui.Separator();

        if (ImGui.BeginTable("##ui_summary", 2, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH))
        {
            DrawRow("模式", "独立运行 / 本地采集");
            DrawRow("统计来源", statsService.DataSourceText);
            DrawRow("战斗结束判定", BuildCombatEndSummary(config));
            DrawRow("主界面透明度", $"{config.WindowOpacity:P0}");
            DrawRow("DPS统计面板透明度", $"{config.FloatingStatsOpacity:P0}");
            DrawRow("悬浮面板", config.ShowStatsPanel ? "已开启" : "已关闭");
            DrawRow("页面显示", BuildTabSummary(config));
            ImGui.EndTable();
        }
    }

    private string GetFloatingStatsButtonLabel()
        => config.ShowStatsPanel ? "隐藏悬浮DPS统计面板" : "打开悬浮DPS统计面板";

    private static void DrawRow(string label, string value)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(label);
        ImGui.TableSetColumnIndex(1);
        ImGui.TextUnformatted(string.IsNullOrWhiteSpace(value) ? "--" : value);
    }

    private static string BuildTabSummary(PluginConfiguration config)
    {
        var tabNames = new string[5];
        var index = 0;

        if (config.ShowDpsTab)
            tabNames[index++] = "DPS";
        if (config.ShowHpsTab)
            tabNames[index++] = "HPS";
        if (config.ShowTakenTab)
            tabNames[index++] = "承伤";
        if (config.ShowOverviewTab)
            tabNames[index++] = "概览";
        if (config.ShowHistoryTab)
            tabNames[index++] = "历史记录";

        return index == 0 ? "全部隐藏" : string.Join(" / ", tabNames.Take(index));
    }

    private static string BuildCombatEndSummary(PluginConfiguration config)
        => config.CombatEndRule == CombatEndRule.PartyListWithDelay
            ? $"全队脱战 + {config.EncounterTimeoutSeconds} 秒延迟"
            : "全队脱战（PartyList）";
}
