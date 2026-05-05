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

    public PluginUI(PluginConfiguration config, LocalStatsService statsService)
    {
        this.config = config;

        mainWindow = new MainWindow(config, statsService, OpenSettingsWindow, ToggleFloatingStatsWindow);
        settingsWindow = new SettingsWindow(config, statsService, OpenMainWindow, ToggleFloatingStatsWindow);
        floatingStatsWindow = new FloatingStatsWindow(config, statsService, ToggleSettingsWindow);

        AddWindow(windowSystem, mainWindow);
        AddWindow(windowSystem, settingsWindow);
        AddWindow(windowSystem, floatingStatsWindow);

        mainWindow.IsOpen = false;
        settingsWindow.IsOpen = false;
        floatingStatsWindow.IsOpen = config.ShowStatsPanel;
    }

    public void Draw()
    {
        if (floatingStatsWindow.IsOpen != config.ShowStatsPanel)
            floatingStatsWindow.IsOpen = config.ShowStatsPanel;

        windowSystem.Draw();

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
