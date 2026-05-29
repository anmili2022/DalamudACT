using System;
using System.Linq;
using Dalamud.Interface.Windowing;

namespace DalamudACT;

internal sealed class PluginUI : IDisposable
{
    private readonly PluginConfiguration config;
    private readonly WindowSystem windowSystem = new("DalamudACT");
    private readonly MainWindow mainWindow;
    private readonly SettingsWindow settingsWindow;
    private readonly FloatingStatsWindow floatingStatsWindow;
    private readonly CombatTimelineWindow combatTimelineWindow;
    private readonly PartyMonitorWindow partyMonitorWindow;
    private readonly StatusObserverService statusObserverService;
    private readonly StatusObserverWindow statusObserverWindow;
    private readonly TimelineService timelineService;
    private readonly TimelineWindow timelineWindow;
    private bool windowDrawFaulted;

    public PluginUI(PluginConfiguration config, LocalStatsService statsService, PartyMonitorService monitorService, TimelineService timelineService)
    {
        this.config = config;
        this.timelineService = timelineService;

        mainWindow = new MainWindow(config, statsService, OpenSettingsWindow, ToggleFloatingStatsWindow, OpenCombatTimelineWindow);
        settingsWindow = new SettingsWindow(config, statsService, monitorService, timelineService, OpenMainWindow, ToggleFloatingStatsWindow, OpenCombatTimelineWindow);
        floatingStatsWindow = new FloatingStatsWindow(config, statsService, ToggleSettingsWindow);
        combatTimelineWindow = new CombatTimelineWindow(config, statsService);
        partyMonitorWindow = new PartyMonitorWindow(config, monitorService, ToggleSettingsWindow);
        statusObserverService = new StatusObserverService(config);
        statusObserverWindow = new StatusObserverWindow(config, statusObserverService, ToggleSettingsWindow);
        timelineWindow = new TimelineWindow(config, timelineService, ToggleSettingsWindow);

        AddWindow(windowSystem, mainWindow);
        AddWindow(windowSystem, settingsWindow);
        AddWindow(windowSystem, floatingStatsWindow);
        AddWindow(windowSystem, combatTimelineWindow);
        AddWindow(windowSystem, partyMonitorWindow);
        AddWindow(windowSystem, statusObserverWindow);
        AddWindow(windowSystem, timelineWindow);

        mainWindow.IsOpen = false;
        settingsWindow.IsOpen = false;
        floatingStatsWindow.IsOpen = config.ShowStatsPanel;
        combatTimelineWindow.IsOpen = false;
        partyMonitorWindow.IsOpen = config.PartyMonitor.ShowPartyMonitorWindow;
        statusObserverWindow.IsOpen = config.StatusObserver.ShowWindow;
        timelineWindow.IsOpen = config.ShowTimelineWindow;
    }

    public void Draw()
    {
        SyncPartyMonitorVisibility();
        SyncStatusObserverVisibility();
        SyncTimelineVisibility();

        try
        {
            windowSystem.Draw();
            windowDrawFaulted = false;
        }
        catch (Exception ex)
        {
            if (!windowDrawFaulted)
            {
                windowDrawFaulted = true;
                LogHelper.Error("界面", ex, "插件窗口绘制失败，已拦截异常以避免影响游戏。");
            }
        }

        SyncFloatingStatsVisibility();
    }

    private void SyncFloatingStatsVisibility()
    {
        if (floatingStatsWindow.IsOpen != config.ShowStatsPanel)
            floatingStatsWindow.IsOpen = config.ShowStatsPanel;
    }

    private void SyncPartyMonitorVisibility()
    {
        var shouldShow = config.PartyMonitor.EnablePartyMonitor && config.PartyMonitor.ShowPartyMonitorWindow;
        if (partyMonitorWindow.IsOpen != shouldShow)
        {
            partyMonitorWindow.IsOpen = shouldShow;
        }
    }

    private void SyncTimelineVisibility()
    {
        var shouldShow = config.ShowTimelineWindow && (config.TimelineDebugMode || timelineService.HasTimeline);
        if (timelineWindow.IsOpen != shouldShow)
            timelineWindow.IsOpen = shouldShow;
    }

    private void SyncStatusObserverVisibility()
    {
        if (statusObserverWindow.IsOpen != config.StatusObserver.ShowWindow)
            statusObserverWindow.IsOpen = config.StatusObserver.ShowWindow;
    }

    public void ToggleSettingsWindow()
        => settingsWindow.IsOpen = !settingsWindow.IsOpen;

    public void OpenMainWindow() => mainWindow.IsOpen = true;

    public void TogglePartyMonitorWindow()
    {
        var nextState = !partyMonitorWindow.IsOpen;
        partyMonitorWindow.IsOpen = nextState;
        config.PartyMonitor.EnablePartyMonitor = nextState;
        config.PartyMonitor.ShowPartyMonitorWindow = nextState;
        config.Save();
    }

    public void ToggleTimelineWindow()
    {
        config.ShowTimelineWindow = !config.ShowTimelineWindow;
        timelineWindow.IsOpen = config.ShowTimelineWindow && (config.TimelineDebugMode || timelineService.HasTimeline);
        config.Save();
    }

    public void ToggleStatusObserverWindow()
    {
        config.StatusObserver.ShowWindow = !config.StatusObserver.ShowWindow;
        statusObserverWindow.IsOpen = config.StatusObserver.ShowWindow;
        config.Save();
    }

    public void Dispose() => windowSystem.RemoveAllWindows();

    private void OpenSettingsWindow() => settingsWindow.IsOpen = true;

    private void OpenCombatTimelineWindow() => combatTimelineWindow.IsOpen = true;

    public void ToggleFloatingStatsWindow()
    {
        var nextState = !floatingStatsWindow.IsOpen;
        floatingStatsWindow.IsOpen = nextState;
        config.ShowStatsPanel = nextState;
        config.ShowDemoPanel = nextState;
        config.Save();
    }

    private static void AddWindow(WindowSystem system, Window window)
    {
        var method = system.GetType()
            .GetMethods()
            .FirstOrDefault(m => m.Name == nameof(WindowSystem.AddWindow) && m.GetParameters().Length == 1)
            ?? throw new MissingMethodException($"{system.GetType().FullName}.AddWindow");

        method.Invoke(system, [window]);
    }
}
