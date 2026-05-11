using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DalamudACT;

internal sealed class MainWindow : Window
{
    private static readonly string PluginVersion = typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "未知版本";
    private const int MaxRecentSummaryItems = 4;
    private const ImGuiTableFlags SummaryTableFlags =
        ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.NoSavedSettings;

    private readonly PluginConfiguration config;
    private readonly LocalStatsService statsService;
    private readonly Action openSettings;
    private readonly Action toggleFloatingStatsPanel;
    private readonly Action openCombatTimelineWindow;

    public MainWindow(
        PluginConfiguration config,
        LocalStatsService statsService,
        Action openSettings,
        Action toggleFloatingStatsPanel,
        Action openCombatTimelineWindow)
        : base("DPS统计", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        this.config = config;
        this.statsService = statsService;
        this.openSettings = openSettings;
        this.toggleFloatingStatsPanel = toggleFloatingStatsPanel;
        this.openCombatTimelineWindow = openCombatTimelineWindow;
        Size = new Vector2(640f, 600f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        BgAlpha = Math.Clamp(config.WindowOpacity, 0.2f, 1f);

        ImGui.TextUnformatted("ACTX 风格本地统计");
        ImGui.SameLine();
        ImGui.TextDisabled($"v{PluginVersion}");
        ImGui.Separator();
        ImGui.TextWrapped("当前版本直接在 Dalamud 内采集战斗事件，并使用 ACTX / NotACT 的统计口径生成本地战斗快照，不再依赖外部 MiniParse。");

        ImGui.Spacing();
        DrawCard(
            "##main_quick_actions_card",
            "快速操作",
            "常用入口集中在这里，可快速打开设置页、战斗流水窗口，或显示/隐藏悬浮统计面板。",
            6.6f,
            DrawQuickActions);

        ImGui.Spacing();
        DrawCard(
            "##main_runtime_status_card",
            "运行状态",
            "查看当前本地统计的数据来源、插件状态和战斗结束判定。",
            8.8f,
            DrawRuntimeSummary);

        ImGui.Spacing();
        DrawCard(
            "##main_recent_status_card",
            "最近状态摘要",
            "快速查看最近几条信息 / 警告 / 错误日志，无需切到设置页即可了解刚发生的操作与异常。",
            8.2f,
            DrawRecentStatusSummary);

        ImGui.Spacing();
        DrawCard(
            "##main_ui_summary_card",
            "界面与列配置摘要",
            "查看当前面板显示、页签显示、共享列显示以及列宽记忆状态。",
            10.6f,
            DrawUiSummary);
    }

    private void DrawQuickActions()
    {
        const ImGuiTableFlags flags =
            ImGuiTableFlags.SizingStretchSame
            | ImGuiTableFlags.NoSavedSettings;

        if (ImGui.BeginTable("##main_action_grid", 3, flags))
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Button("打开设置", new Vector2(-1f, 0f)))
                openSettings();

            ImGui.TableSetColumnIndex(1);
            if (ImGui.Button("打开战斗流水", new Vector2(-1f, 0f)))
                openCombatTimelineWindow();

            ImGui.TableSetColumnIndex(2);
            if (ImGui.Button(GetFloatingStatsButtonLabel(), new Vector2(-1f, 0f)))
                toggleFloatingStatsPanel();

            ImGui.EndTable();
        }

        ImGui.TextDisabled($"当前状态：{statsService.StatusText}");
    }

    private void DrawRuntimeSummary()
    {
        if (!ImGui.BeginTable("##main_runtime_summary", 2, SummaryTableFlags))
            return;

        DrawRow("模式", "独立运行 / 本地采集");
        DrawRow("版本", PluginVersion);
        DrawRow("统计来源", statsService.DataSourceText);
        DrawRow("战斗结束判定", BuildCombatEndSummary(config));
        DrawRow("悬浮面板", BuildFloatingPanelSummary(config));
        DrawRow("悬浮对象", BuildFloatingParticipantSummary(config));
        DrawRow("当前状态", statsService.StatusText);
        ImGui.EndTable();
    }

    private void DrawUiSummary()
    {
        if (!ImGui.BeginTable("##main_ui_summary", 2, SummaryTableFlags))
            return;

        DrawRow("主界面透明度", $"{config.WindowOpacity:P0}");
        DrawRow("悬浮面板透明度", $"{config.FloatingStatsOpacity:P0}");
        DrawRow("页签显示", BuildTabSummary(config));
        DrawRow("共享列显示", BuildColumnSummary(config));
        DrawRow("统计页列宽记忆", BuildMetricWidthSummary(config));
        DrawRow("历史页列宽记忆", BuildHistoryWidthSummary(config));
        ImGui.EndTable();
    }

    private void DrawRecentStatusSummary()
    {
        LogUiHelper.DrawRecentLogToolbar();
        ImGui.Spacing();
        LogUiHelper.DrawRecentLogList(MaxRecentSummaryItems);
    }

    private void DrawCard(string id, string title, string description, float heightInLines, Action drawContent)
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

    private static string BuildFloatingPanelSummary(PluginConfiguration config)
    {
        var visibleText = config.ShowStatsPanel ? "已开启" : "已关闭";
        var lockText = config.LockFloatingStatsWindow ? "已锁定" : "可拖动";
        return $"{visibleText} / {lockText}";
    }

    private static string BuildFloatingParticipantSummary(PluginConfiguration config)
    {
        var modeText = config.FloatingStatsParticipantDisplayMode switch
        {
            FloatingStatsParticipantDisplayMode.PlayersOnly => "仅玩家",
            FloatingStatsParticipantDisplayMode.PlayersAndFriendlyNpc => "玩家 + 友方 NPC",
            FloatingStatsParticipantDisplayMode.PlayersAndHostileNpc => "玩家 + 敌方 NPC",
            _ => "智能（多人仅玩家 / 单人可含友方 NPC）",
        };

        var highlightText = config.HighlightNpcRows ? "NPC高亮开" : "NPC高亮关";
        return $"{modeText} / 门槛 {config.HostileNpcMinHpMultiplier}x / {highlightText}";
    }

    private static string BuildColumnSummary(PluginConfiguration config)
    {
        var columnNames = new string[5];
        var index = 0;

        if (config.ShowDpsPlayerColumn)
            columnNames[index++] = "玩家";
        if (config.ShowDpsJobColumn)
            columnNames[index++] = "职业";
        if (config.ShowDpsDamageColumn)
            columnNames[index++] = "伤害";
        if (config.ShowDpsValueColumn)
            columnNames[index++] = "秒伤";
        if (config.ShowDpsDeathsColumn)
            columnNames[index++] = "死亡";

        var columns = index == 0 ? "全部隐藏" : string.Join(" / ", columnNames.Take(index));
        return $"{columns}，人数 {config.DpsVisibleCount}";
    }

    private static string BuildMetricWidthSummary(PluginConfiguration config)
    {
        var rememberedCount = CountRememberedWidths(
            config.FloatingStatsPlayerColumnWidth,
            config.FloatingStatsJobColumnWidth,
            config.FloatingStatsDamageColumnWidth,
            config.FloatingStatsValueColumnWidth,
            config.FloatingStatsDeathsColumnWidth);

        return rememberedCount == 0
            ? "全部自动"
            : $"已记忆 {rememberedCount}/5 列";
    }

    private static string BuildHistoryWidthSummary(PluginConfiguration config)
    {
        var rememberedCount = CountRememberedWidths(
            config.HistoryStartTimeColumnWidth,
            config.HistoryEndTimeColumnWidth,
            config.HistoryDurationColumnWidth);

        return rememberedCount == 0
            ? "全部自动"
            : $"已记忆 {rememberedCount}/3 列";
    }

    private static int CountRememberedWidths(params float[] widths)
        => widths.Count(static width => width > 0f);
}
