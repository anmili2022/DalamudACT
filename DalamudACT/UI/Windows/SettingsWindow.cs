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
    private enum AdvancedSettingsPage
    {
        Window,
        Stats,
        PartyMonitor,
        StatusObserver,
        Timeline,
        Data,
        Help,
    }

    private static readonly string PluginVersion = typeof(SettingsWindow).Assembly.GetName().Version?.ToString() ?? "未知版本";
    private readonly PluginConfiguration config;
    private bool showAdvancedSettings;
    private readonly LocalStatsService statsService;
    private readonly PartyMonitorService? monitorService;
    private readonly Action openMainWindow;
    private readonly Action toggleFloatingStatsPanel;
    private readonly Action openCombatTimelineWindow;
    private readonly Action openTimelineListWindow;
    private readonly TimelineService? timelineService;
    private readonly Dictionary<string, float> adaptiveChildHeights = new();
    private AdvancedSettingsPage advancedSettingsPage = AdvancedSettingsPage.Window;
    private Vector2? pendingWindowSize;
    private string floatingStyleShareCode = string.Empty;
    private string floatingStyleTransferStatusText = string.Empty;
    private string timelineRemoteStatusText = string.Empty;
    private string timelineForceLoadPath = string.Empty;
    private bool timelineRemoteOperationRunning;
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
        Action openCombatTimelineWindow,
        Action openTimelineListWindow)
        : base($"DPS统计 设置 v{PluginVersion}###SettingsWindow", ImGuiWindowFlags.NoTitleBar)
    {
        this.config = config;
        this.statsService = statsService;
        this.monitorService = monitorService;
        this.timelineService = timelineService;
        this.openMainWindow = openMainWindow;
        this.toggleFloatingStatsPanel = toggleFloatingStatsPanel;
        this.openCombatTimelineWindow = openCombatTimelineWindow;
        this.openTimelineListWindow = openTimelineListWindow;
        timelineForceLoadPath = config.TimelineForceLoadPath ?? string.Empty;
        Size = new Vector2(480f, 520f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        BgAlpha = showAdvancedSettings ? 0.2f : 0.4f;

        if (pendingWindowSize is { } windowSize)
        {
            Size = windowSize;
            ImGui.SetWindowSize(windowSize, ImGuiCond.Always);
            pendingWindowSize = null;
        }

        if (showAdvancedSettings)
        {
            DrawAdvancedSettings();
        }
        else
        {
            DrawQuickSettings();
        }
    }

    public override void PreDraw()
    {
        ImGui.PushStyleColor(ImGuiCol.ResizeGrip, new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.ResizeGripHovered, new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.ResizeGripActive, new Vector4(0f, 0f, 0f, 0f));
    }

    public override void PostDraw()
    {
        ImGui.PopStyleColor(3);
    }

    private void DrawAdvancedSettings()
    {
        Size ??= new Vector2(1044f, 600f);
        var theme = UiThemeColors.Get(config.SelectedUiTheme);
        PushThemeStyle(theme);
        try
        {
            DrawAdvancedSettingsShell(theme);
        }
        finally
        {
            PopThemeStyle();
        }
    }

    private void DrawAdvancedSettingsShell(UiThemeColors theme)
    {
        DrawAdvancedConsoleHeader(theme);

        var fullWidth = ImGui.GetContentRegionAvail().X;
        var fullHeight = ImGui.GetContentRegionAvail().Y;
        var navWidth = 172f;
        var sideWidth = 214f;
        var contentWidth = Math.Max(360f, fullWidth - navWidth - sideWidth - 16f);

        ImGui.PushStyleColor(ImGuiCol.ChildBg, theme.PanelDark);
        ImGui.BeginChild("##advanced_nav", new Vector2(navWidth, fullHeight), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleColor();
        try
        {
            DrawAdvancedNavItem("窗口", AdvancedSettingsPage.Window);
            DrawAdvancedNavItem("统计", AdvancedSettingsPage.Stats);
            DrawAdvancedNavItem("队友监控", AdvancedSettingsPage.PartyMonitor);
            DrawAdvancedNavItem("状态监控", AdvancedSettingsPage.StatusObserver);
            DrawAdvancedNavItem("时间轴", AdvancedSettingsPage.Timeline);
            DrawAdvancedNavItem("数据", AdvancedSettingsPage.Data);
            DrawAdvancedNavItem("帮助", AdvancedSettingsPage.Help);
        }
        finally
        {
            ImGui.EndChild();
        }

        ImGui.SameLine(0f, 8f);
        ImGui.BeginChild("##advanced_content", new Vector2(contentWidth, fullHeight), true);
        try
        {
            DrawAdvancedPageContent();
        }
        finally
        {
            ImGui.EndChild();
        }

        ImGui.SameLine(0f, 8f);
        ImGui.BeginChild("##advanced_status", new Vector2(sideWidth, fullHeight), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        try
        {
            DrawAdvancedStatusPanel(theme);
        }
        finally
        {
            ImGui.EndChild();
        }
    }

    private void DrawAdvancedConsoleHeader(UiThemeColors theme)
    {
        const float headerHeight = 50f;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, theme.PanelDark);
        ImGui.BeginChild("##advanced_header", new Vector2(0f, headerHeight), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleColor();
        try
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(theme.Accent, "DalamudACT");
            ImGui.SameLine(0f, 6f);
            ImGui.TextUnformatted($"完整设置 / v{PluginVersion}");
            ImGui.SameLine();
            ImGui.TextColored(theme.Ok, UiThemeColors.Get(config.SelectedUiTheme).Label);

            const float simpleWidth = 92f;
            const float closeWidth = 76f;
            var right = ImGui.GetWindowContentRegionMax().X;
            var buttonY = MathF.Max(0f, (headerHeight - ImGui.GetFrameHeight()) * 0.5f);
            ImGui.SetCursorPos(new Vector2(MathF.Max(0f, right - simpleWidth - closeWidth - 16f), buttonY));
            if (ImGui.Button("简易设置", new Vector2(simpleWidth, 0f)))
            {
                showAdvancedSettings = false;
                pendingWindowSize = new Vector2(790f, 650f);
            }

            ImGui.SameLine(0f, 8f);
            if (ImGui.Button("关闭窗口", new Vector2(closeWidth, 0f)))
                IsOpen = false;
        }
        finally
        {
            ImGui.EndChild();
        }
    }

    private void DrawAdvancedNavItem(string label, AdvancedSettingsPage page)
    {
        var theme = UiThemeColors.Get(config.SelectedUiTheme);
        var selected = advancedSettingsPage == page;
        ImGui.PushStyleColor(ImGuiCol.Button, selected ? theme.WindowBg : new Vector4(0f, 0f, 0f, 0f));
        try
        {
            if (ImGui.Button($"      {label}##advanced_nav_{page}", new Vector2(-1f, 34f)))
                advancedSettingsPage = page;

            DrawAdvancedNavIcon(page, selected, theme);
        }
        finally
        {
            ImGui.PopStyleColor();
        }
    }

    private static void DrawAdvancedNavIcon(AdvancedSettingsPage page, bool selected, UiThemeColors theme)
    {
        var min = ImGui.GetItemRectMin();
        var center = min + new Vector2(18f, 17f);
        var drawList = ImGui.GetWindowDrawList();
        var color = ImGui.ColorConvertFloat4ToU32(selected ? theme.Accent : theme.TextDisabled);
        var fill = ImGui.ColorConvertFloat4ToU32(selected ? theme.AccentSoft : new Vector4(theme.WindowBg.X, theme.WindowBg.Y, theme.WindowBg.Z, 0.65f));

        switch (page)
        {
            case AdvancedSettingsPage.Window:
                drawList.AddRectFilled(center - new Vector2(8f, 7f), center + new Vector2(8f, 7f), fill, 4f);
                drawList.AddRect(center - new Vector2(8f, 7f), center + new Vector2(8f, 7f), color, 4f, ImDrawFlags.None, 1.5f);
                break;
            case AdvancedSettingsPage.Stats:
                drawList.AddLine(center + new Vector2(-8f, 6f), center + new Vector2(-8f, -2f), color, 1.8f);
                drawList.AddLine(center + new Vector2(-1f, 6f), center + new Vector2(-1f, -7f), color, 1.8f);
                drawList.AddLine(center + new Vector2(6f, 6f), center + new Vector2(6f, 1f), color, 1.8f);
                break;
            case AdvancedSettingsPage.PartyMonitor:
                drawList.AddCircleFilled(center + new Vector2(-5f, -2f), 4f, fill, 16);
                drawList.AddCircle(center + new Vector2(-5f, -2f), 4f, color, 16, 1.4f);
                drawList.AddCircleFilled(center + new Vector2(6f, 3f), 4f, fill, 16);
                drawList.AddCircle(center + new Vector2(6f, 3f), 4f, color, 16, 1.4f);
                break;
            case AdvancedSettingsPage.StatusObserver:
                drawList.AddCircleFilled(center, 8f, fill, 16);
                drawList.AddCircle(center, 8f, color, 16, 1.5f);
                drawList.AddCircleFilled(center, 3f, color, 12);
                break;
            case AdvancedSettingsPage.Timeline:
                drawList.AddLine(center + new Vector2(-8f, 5f), center + new Vector2(8f, 5f), color, 1.6f);
                drawList.AddLine(center + new Vector2(-6f, 1f), center + new Vector2(5f, -5f), color, 1.6f);
                drawList.AddCircleFilled(center + new Vector2(-7f, 5f), 2f, color, 10);
                drawList.AddCircleFilled(center + new Vector2(8f, 5f), 2f, color, 10);
                break;
            case AdvancedSettingsPage.Data:
                drawList.AddRectFilled(center - new Vector2(7f, 7f), center + new Vector2(7f, 7f), fill, 2f);
                drawList.AddLine(center + new Vector2(-5f, -2f), center + new Vector2(5f, -2f), color, 1.3f);
                drawList.AddLine(center + new Vector2(-5f, 3f), center + new Vector2(5f, 3f), color, 1.3f);
                break;
            case AdvancedSettingsPage.Help:
                drawList.AddCircle(center, 8f, color, 16, 1.5f);
                drawList.AddText(center - new Vector2(3.5f, 7f), color, "?");
                break;
        }
    }

    private void DrawAdvancedPageContent()
    {
        switch (advancedSettingsPage)
        {
            case AdvancedSettingsPage.Window:
                DrawWindowSection();
                break;
            case AdvancedSettingsPage.Stats:
                DrawFloatingPanelSection();
                break;
            case AdvancedSettingsPage.PartyMonitor:
                DrawPartyMonitorSection();
                break;
            case AdvancedSettingsPage.StatusObserver:
                DrawStatusObserverSection();
                break;
            case AdvancedSettingsPage.Timeline:
                DrawTimelineStyleSection();
                break;
            case AdvancedSettingsPage.Data:
                DrawMaintenanceSection();
                break;
            case AdvancedSettingsPage.Help:
                DrawCommandHelpSection();
                break;
        }
    }

    private void DrawAdvancedStatusPanel(UiThemeColors theme)
    {
        ImGui.TextUnformatted("当前状态");
        ImGui.Separator();
        ImGui.TextColored(theme.Accent, PluginVersion);
        ImGui.TextDisabled("release version");
        ImGui.Dummy(new Vector2(0f, 6f));
        ImGui.TextUnformatted("快捷入口");
        if (ImGui.Button(GetFloatingStatsButtonLabel(), new Vector2(-1f, 0f)))
            toggleFloatingStatsPanel();
        if (ImGui.Button("打开战斗流水", new Vector2(-1f, 0f)))
            openCombatTimelineWindow();
        if (ImGui.Button("已有时间轴", new Vector2(-1f, 0f)))
            openTimelineListWindow();

        ImGui.Dummy(new Vector2(0f, 8f));
        DrawThemeSwitcher();
    }

    private void DrawThemeSwitcher()
    {
        var current = config.SelectedUiTheme;
        var currentLabel = UiThemeColors.Get(current).Label;
        ImGui.TextUnformatted("主题配色");
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo("##ui_theme_selector", currentLabel))
        {
            foreach (UiThemeId id in Enum.GetValues(typeof(UiThemeId)))
            {
                var tc = UiThemeColors.Get(id);
                var selected = current == id;
                if (ImGui.Selectable(tc.Label, selected))
                {
                    config.SelectedUiTheme = id;
                    current = id;
                    config.Save();
                }

                if (selected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        var preview = UiThemeColors.Get(current);
        ImGui.Dummy(new System.Numerics.Vector2(0f, 4f));
        ImGui.TextDisabled("预览");
        var ps = ImGui.GetCursorScreenPos();
        var dl = ImGui.GetWindowDrawList();
        var sw = 20f; var gap = 3f;
        var swatches = new[] { preview.Panel, preview.WindowBg, preview.PanelDark, preview.Accent, preview.Ok, preview.CheckMark };
        for (var i = 0; i < swatches.Length; i++)
        {
            var rectMin = ps + new System.Numerics.Vector2(i * (sw + gap), 0f);
            var rectMax = rectMin + new System.Numerics.Vector2(sw, 20f);
            dl.AddRectFilled(rectMin, rectMax, ImGui.ColorConvertFloat4ToU32(swatches[i]), 4f);
            dl.AddRect(rectMin, rectMax, ImGui.ColorConvertFloat4ToU32(preview.Border), 4f);
        }
        ImGui.Dummy(new System.Numerics.Vector2(0f, 24f));
    }
}
