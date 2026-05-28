using System;
using System.Collections.Generic;
using System.Linq;
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
internal sealed partial class SettingsWindow : Window
{
    private static readonly string PluginVersion = typeof(SettingsWindow).Assembly.GetName().Version?.ToString() ?? "未知版本";
    private readonly PluginConfiguration config;
    private readonly LocalStatsService statsService;
    private readonly PartyMonitorService? monitorService;
    private readonly Action openMainWindow;
    private readonly Action toggleFloatingStatsPanel;
    private readonly Action openCombatTimelineWindow;
    private readonly TimelineService? timelineService;
    private readonly Dictionary<string, float> adaptiveChildHeights = new();
    private string floatingStyleShareCode = string.Empty;
    private string floatingStyleTransferStatusText = string.Empty;
    private string timelineDraftStatusText = string.Empty;
    private List<TimelineLogEncounterOption> timelineLogEncounterOptions = new();
    private string customFriendlyNpcNameInput = string.Empty;
    private string customFriendlyNpcStatusText = string.Empty;
    private readonly Dictionary<uint, string> customSkillActionIdInputs = new();
    private readonly Dictionary<uint, string> customSkillNameInputs = new();
    private readonly Dictionary<uint, string> customSkillCdInputs = new();
    private readonly Dictionary<uint, bool> customSkillIsMit = new();
    private string customSkillStatusText = string.Empty;
    private uint customSkillSelectedJobId;
    private string? customSkillSelectedJobName;

    public SettingsWindow(
        PluginConfiguration config,
        LocalStatsService statsService,
        PartyMonitorService? monitorService,
        TimelineService? timelineService,
        Action openMainWindow,
        Action toggleFloatingStatsPanel,
        Action openCombatTimelineWindow)
        : base($"DPS统计 设置 v{PluginVersion}###SettingsWindow")
    {
        this.config = config;
        this.statsService = statsService;
        this.monitorService = monitorService;
        this.timelineService = timelineService;
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
        DrawFloatingPanelSection();
        DrawPartyMonitorSection();
        DrawTimelineStyleSection();
        DrawMaintenanceSection();
        DrawCommandHelpSection();
    }

}
