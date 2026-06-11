using Dalamud.Game.Command;

namespace DalamudACT;

public sealed partial class ACT
{
    private void RegisterCommands()
    {
        DalamudApi.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "DPS统计：settings 设置；dps DPS悬浮窗；skills 技能监控；status 状态监控；timeline/time 时间轴；help 帮助。",
        });
    }

    private static void UnregisterCommands()
    {
        DalamudApi.Commands.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        if (isDisposing)
            return;

        _ = command;
        var normalized = (args ?? string.Empty).Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "":
            case "help":
            case "帮助":
                PrintCommandHelp();
                return;
            case "settings":
            case "setting":
            case "config":
            case "cfg":
            case "设置":
                ui.ToggleSettingsWindow();
                return;
            case "dps":
            case "stats":
            case "panel":
            case "统计":
            case "悬浮窗":
                ui.ToggleFloatingStatsWindow();
                return;
            case "skills":
            case "skill":
            case "party":
            case "monitor":
            case "技能":
            case "技能监控":
                ui.TogglePartyMonitorWindow();
                return;
            case "timeline":
            case "time":
            case "时间轴":
                ui.ToggleTimelineWindow();
                return;
            case "status":
            case "buff":
            case "状态":
            case "状态观察":
            case "状态监控":
                ui.ToggleStatusObserverWindow();
                return;
            default:
                LogHelper.PrintWithModule("命令", "宏", $"未知子命令：{args}。输入 /dps help 查看可用命令。");
                return;
        }
    }

    private static void PrintCommandHelp()
    {
        LogHelper.PrintWithModule("命令", "宏", "可用宏命令：/dps help 显示帮助；/dps settings 切换设置面板；/dps dps 切换DPS统计悬浮窗；/dps skills 切换技能监控悬浮窗；/dps status 切换状态监控窗口；/dps timeline 或 /dps time 切换时间轴悬浮窗。 ");
    }
}
