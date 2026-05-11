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
    private bool windowDrawFaulted;

    public PluginUI(PluginConfiguration config, LocalStatsService statsService)
    {
        this.config = config;

        mainWindow = new MainWindow(config, statsService, OpenSettingsWindow, ToggleFloatingStatsWindow, OpenCombatTimelineWindow);
        settingsWindow = new SettingsWindow(config, statsService, OpenMainWindow, ToggleFloatingStatsWindow, OpenCombatTimelineWindow);
        floatingStatsWindow = new FloatingStatsWindow(config, statsService, ToggleSettingsWindow);
        combatTimelineWindow = new CombatTimelineWindow(config, statsService);

        AddWindow(windowSystem, mainWindow);
        AddWindow(windowSystem, settingsWindow);
        AddWindow(windowSystem, floatingStatsWindow);
        AddWindow(windowSystem, combatTimelineWindow);

        mainWindow.IsOpen = false;
        settingsWindow.IsOpen = false;
        floatingStatsWindow.IsOpen = config.ShowStatsPanel;
        combatTimelineWindow.IsOpen = false;
    }

    public void Draw()
    {
        if (floatingStatsWindow.IsOpen != config.ShowStatsPanel)
            floatingStatsWindow.IsOpen = config.ShowStatsPanel;

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

    public void ToggleSettingsWindow()
        => settingsWindow.IsOpen = !settingsWindow.IsOpen;

    public void OpenMainWindow() => mainWindow.IsOpen = true;

    public void Dispose() => windowSystem.RemoveAllWindows();

    private void OpenSettingsWindow() => settingsWindow.IsOpen = true;

    private void OpenCombatTimelineWindow() => combatTimelineWindow.IsOpen = true;

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
