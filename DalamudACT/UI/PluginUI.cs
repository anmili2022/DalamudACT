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
    private readonly DebugCombatLogWindow debugCombatLogWindow;
    private readonly PartyMonitorWindow partyMonitorWindow;
    private bool windowDrawFaulted;

    public PluginUI(PluginConfiguration config, LocalStatsService statsService, PartyMonitorService monitorService)
    {
        this.config = config;

        mainWindow = new MainWindow(config, statsService, OpenSettingsWindow, ToggleFloatingStatsWindow, OpenCombatTimelineWindow, OpenDebugCombatLogWindow);
        settingsWindow = new SettingsWindow(config, statsService, monitorService, OpenMainWindow, ToggleFloatingStatsWindow, OpenCombatTimelineWindow, OpenDebugCombatLogWindow);
        floatingStatsWindow = new FloatingStatsWindow(config, statsService, ToggleSettingsWindow);
        combatTimelineWindow = new CombatTimelineWindow(config, statsService);
        debugCombatLogWindow = new DebugCombatLogWindow(config, statsService);
        partyMonitorWindow = new PartyMonitorWindow(config, monitorService);

        AddWindow(windowSystem, mainWindow);
        AddWindow(windowSystem, settingsWindow);
        AddWindow(windowSystem, floatingStatsWindow);
        AddWindow(windowSystem, combatTimelineWindow);
        AddWindow(windowSystem, debugCombatLogWindow);
        AddWindow(windowSystem, partyMonitorWindow);

        mainWindow.IsOpen = false;
        settingsWindow.IsOpen = false;
        floatingStatsWindow.IsOpen = config.ShowStatsPanel;
        combatTimelineWindow.IsOpen = false;
        debugCombatLogWindow.IsOpen = false;
        partyMonitorWindow.IsOpen = config.PartyMonitor.ShowPartyMonitorWindow;
    }

    public void Draw()
    {
        if (floatingStatsWindow.IsOpen != config.ShowStatsPanel)
            floatingStatsWindow.IsOpen = config.ShowStatsPanel;

        SyncPartyMonitorVisibility();

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

        if (config.ShowStatsPanel != floatingStatsWindow.IsOpen)
        {
            config.ShowStatsPanel = floatingStatsWindow.IsOpen;
            config.ShowDemoPanel = floatingStatsWindow.IsOpen;
            config.Save();
        }
    }

    private void SyncPartyMonitorVisibility()
    {
        var shouldShow = config.PartyMonitor.EnablePartyMonitor && config.PartyMonitor.ShowPartyMonitorWindow;
        if (partyMonitorWindow.IsOpen != shouldShow)
        {
            partyMonitorWindow.IsOpen = shouldShow;
        }
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

    public void Dispose() => windowSystem.RemoveAllWindows();

    private void OpenSettingsWindow() => settingsWindow.IsOpen = true;

    private void OpenCombatTimelineWindow() => combatTimelineWindow.IsOpen = true;

    private void OpenDebugCombatLogWindow() => debugCombatLogWindow.IsOpen = true;

    private void ToggleFloatingStatsWindow()
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
