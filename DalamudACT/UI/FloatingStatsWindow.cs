using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DalamudACT;

/// <summary>
/// 悬浮统计窗口封装，基于 Dalamud.Interface.Windowing.Window 管理悬浮面板大小、折叠态与活动页签。
/// 相关参考：
/// - https://dalamud.dev/
/// - https://dalamud.dev/api/
/// 调整 Window 生命周期、窗口标志、折叠行为或绘制入口前，先对照 Dalamud 文档。
/// </summary>
internal sealed class FloatingStatsWindow : Window
{
    private const ImGuiWindowFlags BaseWindowFlags =
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoTitleBar;
    private const float DefaultExpandedWindowWidth = 300f;
    private const float DefaultExpandedWindowHeight = 300f;
    private const float CollapsedWindowWidth = 270f;
    private const float CollapsedWindowHeight = 42f;

    private readonly PluginConfiguration config;
    private readonly LocalStatsService statsService;
    private readonly Action toggleSettingsWindow;
    private Vector2 expandedWindowSize;
    private bool collapseToTabBar;
    private bool applyStartupCollapsedSize;
    private StatsPanelTabId activeTab = StatsPanelTabId.None;
    private int observedEncounterFinalizedVersion;

    public FloatingStatsWindow(
        PluginConfiguration config,
        LocalStatsService statsService,
        Action toggleSettingsWindow)
        : base("###DpsStatsPanel", BaseWindowFlags)
    {
        this.config = config;
        this.statsService = statsService;
        this.toggleSettingsWindow = toggleSettingsWindow;
        Size = new Vector2(DefaultExpandedWindowWidth, DefaultExpandedWindowHeight);
        SizeCondition = ImGuiCond.FirstUseEver;
        expandedWindowSize = Size.Value;
        InitializeStartupLayout();
    }

    public override void Draw()
    {
        Flags = config.LockFloatingStatsWindow
            ? BaseWindowFlags | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize
            : BaseWindowFlags;

        BgAlpha = Math.Clamp(config.FloatingStatsOpacity, 0f, 1f);

        if (applyStartupCollapsedSize && collapseToTabBar)
        {
            ImGui.SetWindowSize(new Vector2(CollapsedWindowWidth, CollapsedWindowHeight), ImGuiCond.Always);
            applyStartupCollapsedSize = false;
        }

        if (!collapseToTabBar)
            expandedWindowSize = ImGui.GetWindowSize();

        var finalizedVersion = statsService.EncounterFinalizedVersion;
        if (finalizedVersion != observedEncounterFinalizedVersion)
        {
            observedEncounterFinalizedVersion = finalizedVersion;
            if (activeTab == StatsPanelTabId.History)
                activeTab = ResolvePreferredLiveTab();
        }

        var drawResult = StatsPanel.Draw(statsService, config, activeTab, collapseToTabBar);
        if (drawResult.ActiveTab != StatsPanelTabId.None)
            activeTab = drawResult.ActiveTab;

        if (drawResult.OpenSettingsRequested)
            toggleSettingsWindow();

        if (collapseToTabBar && activeTab != StatsPanelTabId.Dps)
        {
            collapseToTabBar = false;
            ImGui.SetWindowSize(expandedWindowSize, ImGuiCond.Always);
            return;
        }

        if (!drawResult.ToggleDpsCollapseRequested)
            return;

        if (collapseToTabBar)
        {
            collapseToTabBar = false;
            ImGui.SetWindowSize(expandedWindowSize, ImGuiCond.Always);
            return;
        }

        expandedWindowSize = ImGui.GetWindowSize();
        collapseToTabBar = true;
        activeTab = StatsPanelTabId.Dps;
        ImGui.SetWindowSize(new Vector2(CollapsedWindowWidth, CollapsedWindowHeight), ImGuiCond.Always);
    }

    private void InitializeStartupLayout()
    {
        if (!config.ShowDpsTab)
        {
            activeTab = ResolvePreferredLiveTab();
            return;
        }

        collapseToTabBar = true;
        applyStartupCollapsedSize = true;
        activeTab = StatsPanelTabId.Dps;
    }

    private StatsPanelTabId ResolvePreferredLiveTab()
    {
        if (config.ShowDpsTab)
            return StatsPanelTabId.Dps;

        if (config.ShowHpsTab)
            return StatsPanelTabId.Hps;

        if (config.ShowTakenTab)
            return StatsPanelTabId.Taken;

        if (config.ShowOverviewTab)
            return StatsPanelTabId.Overview;

        return config.ShowHistoryTab ? StatsPanelTabId.History : StatsPanelTabId.None;
    }
}
