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
    private const float ClassicExpandedWindowWidth = 300f;
    private const float ClassicExpandedWindowHeight = 300f;
    private const float ClassicCollapsedWindowWidth = 270f;
    private const float ClassicCollapsedWindowHeight = 42f;
    private const float IkegamiExpandedWindowWidth = 920f;
    private const float IkegamiExpandedWindowHeight = 126f;
    private const float IkegamiCollapsedWindowWidth = 220f;
    private const float IkegamiCollapsedWindowHeight = 42f;
    private const float SavedWindowSizeMax = 4000f;
    private const float SavedWindowSizeEpsilon = 0.5f;

    private readonly PluginConfiguration config;
    private readonly LocalStatsService statsService;
    private readonly Action toggleSettingsWindow;
    private Vector2 expandedWindowSize;
    private bool collapseToTabBar;
    private bool applyStartupCollapsedSize;
    private bool applyStartupExpandedSize;
    private StatsPanelTabId activeTab = StatsPanelTabId.None;
    private int observedEncounterFinalizedVersion;
    private FloatingStatsDisplayStyle observedDisplayStyle;

    public FloatingStatsWindow(
        PluginConfiguration config,
        LocalStatsService statsService,
        Action toggleSettingsWindow)
        : base("###DpsStatsPanel", BaseWindowFlags)
    {
        this.config = config;
        this.statsService = statsService;
        this.toggleSettingsWindow = toggleSettingsWindow;
        expandedWindowSize = GetExpandedWindowSize(config.FloatingStatsDisplayStyle);
        Size = expandedWindowSize;
        SizeCondition = ImGuiCond.FirstUseEver;
        observedDisplayStyle = config.FloatingStatsDisplayStyle;
        InitializeStartupLayout();
        applyStartupExpandedSize = !collapseToTabBar;
    }

    public override void Draw()
    {
        Flags = config.LockFloatingStatsWindow
            ? BaseWindowFlags | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize
            : BaseWindowFlags;

        BgAlpha = Math.Clamp(config.FloatingStatsOpacity, 0f, 1f);

        if (observedDisplayStyle != config.FloatingStatsDisplayStyle)
        {
            PersistExpandedWindowSizeIfNeeded(
                observedDisplayStyle,
                collapseToTabBar ? expandedWindowSize : ImGui.GetWindowSize(),
                true);
            ApplyDisplayStyleLayoutChange(config.FloatingStatsDisplayStyle);
            observedDisplayStyle = config.FloatingStatsDisplayStyle;
        }

        if (applyStartupCollapsedSize && collapseToTabBar)
        {
            ImGui.SetWindowSize(GetCollapsedWindowSize(config.FloatingStatsDisplayStyle), ImGuiCond.Always);
            applyStartupCollapsedSize = false;
        }

        if (applyStartupExpandedSize && !collapseToTabBar)
        {
            ImGui.SetWindowSize(expandedWindowSize, ImGuiCond.Always);
            applyStartupExpandedSize = false;
        }

        if (!collapseToTabBar)
        {
            expandedWindowSize = ImGui.GetWindowSize();
            PersistExpandedWindowSizeIfNeeded(config.FloatingStatsDisplayStyle, expandedWindowSize);
        }

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
        PersistExpandedWindowSizeIfNeeded(config.FloatingStatsDisplayStyle, expandedWindowSize, true);
        collapseToTabBar = true;
        activeTab = StatsPanelTabId.Dps;
        ImGui.SetWindowSize(GetCollapsedWindowSize(config.FloatingStatsDisplayStyle), ImGuiCond.Always);
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

    private void ApplyDisplayStyleLayoutChange(FloatingStatsDisplayStyle currentStyle)
    {
        var targetExpandedSize = GetExpandedWindowSize(currentStyle);

        if (collapseToTabBar)
        {
            expandedWindowSize = targetExpandedSize;
            ImGui.SetWindowSize(GetCollapsedWindowSize(currentStyle), ImGuiCond.Always);
            return;
        }

        expandedWindowSize = targetExpandedSize;
        ImGui.SetWindowSize(expandedWindowSize, ImGuiCond.Always);
    }

    private Vector2 GetExpandedWindowSize(FloatingStatsDisplayStyle style)
    {
        var fallback = GetDefaultExpandedWindowSize(style);
        return style switch
        {
            FloatingStatsDisplayStyle.Ikegami => ResolveSavedExpandedWindowSize(
                config.FloatingStatsIkegamiWindowWidth,
                config.FloatingStatsIkegamiWindowHeight,
                fallback),
            _ => ResolveSavedExpandedWindowSize(
                config.FloatingStatsClassicWindowWidth,
                config.FloatingStatsClassicWindowHeight,
                fallback),
        };
    }

    private static Vector2 GetDefaultExpandedWindowSize(FloatingStatsDisplayStyle style)
        => style switch
        {
            FloatingStatsDisplayStyle.Ikegami => new Vector2(IkegamiExpandedWindowWidth, IkegamiExpandedWindowHeight),
            _ => new Vector2(ClassicExpandedWindowWidth, ClassicExpandedWindowHeight),
        };

    private static Vector2 GetCollapsedWindowSize(FloatingStatsDisplayStyle style)
        => style switch
        {
            FloatingStatsDisplayStyle.Ikegami => new Vector2(IkegamiCollapsedWindowWidth, IkegamiCollapsedWindowHeight),
            _ => new Vector2(ClassicCollapsedWindowWidth, ClassicCollapsedWindowHeight),
        };

    private void PersistExpandedWindowSizeIfNeeded(
        FloatingStatsDisplayStyle style,
        Vector2 windowSize,
        bool forceSave = false)
    {
        if (!forceSave && ImGui.IsMouseDown(ImGuiMouseButton.Left))
            return;

        if (SetSavedExpandedWindowSize(style, windowSize))
            config.Save();
    }

    private bool SetSavedExpandedWindowSize(FloatingStatsDisplayStyle style, Vector2 windowSize)
    {
        var width = Math.Clamp(windowSize.X, 1f, SavedWindowSizeMax);
        var height = Math.Clamp(windowSize.Y, 1f, SavedWindowSizeMax);

        switch (style)
        {
            case FloatingStatsDisplayStyle.Ikegami:
                return UpdateSavedExpandedWindowSize(
                    ref config.FloatingStatsIkegamiWindowWidth,
                    ref config.FloatingStatsIkegamiWindowHeight,
                    width,
                    height);
            default:
                return UpdateSavedExpandedWindowSize(
                    ref config.FloatingStatsClassicWindowWidth,
                    ref config.FloatingStatsClassicWindowHeight,
                    width,
                    height);
        }
    }

    private static Vector2 ResolveSavedExpandedWindowSize(float savedWidth, float savedHeight, Vector2 fallback)
        => savedWidth > 0f && savedHeight > 0f
            ? new Vector2(savedWidth, savedHeight)
            : fallback;

    private static bool UpdateSavedExpandedWindowSize(
        ref float savedWidth,
        ref float savedHeight,
        float width,
        float height)
    {
        if (NearlyEqual(savedWidth, width) && NearlyEqual(savedHeight, height))
            return false;

        savedWidth = width;
        savedHeight = height;
        return true;
    }

    private static bool NearlyEqual(float left, float right)
        => Math.Abs(left - right) <= SavedWindowSizeEpsilon;

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
