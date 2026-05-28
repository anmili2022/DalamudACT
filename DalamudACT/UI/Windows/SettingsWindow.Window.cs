using System;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal sealed partial class SettingsWindow
{
    private void DrawWindowSection()
    {
        if (!DrawFirstLevelHeader("窗口设置", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        DrawSettingCard(
            "##window_settings_card",
            "窗口与悬浮面板",
            "统一控制主窗口透明度、悬浮统计面板透明度、队友监控窗口透明度与锁定状态。",
            14f,
            () =>
            {
                var opacity = config.WindowOpacity;
                if (ImGui.SliderFloat("主界面透明度", ref opacity, 0.2f, 1f))
                {
                    config.WindowOpacity = opacity;
                    config.Save();
                }

                var statsPanelOpacity = config.FloatingStatsOpacity;
                if (ImGui.SliderFloat("DPS统计面板透明度", ref statsPanelOpacity, 0f, 1f))
                {
                    config.FloatingStatsOpacity = statsPanelOpacity;
                    config.Save();
                }

                var showStats = config.ShowStatsPanel;
                if (ImGui.Checkbox("显示统计面板", ref showStats))
                {
                    config.ShowStatsPanel = showStats;
                    config.ShowDemoPanel = showStats;
                    config.Save();
                }

                ImGui.SameLine(0f, 12f);
                var highlightSelfBar = config.HighlightSelfBar;
                if (ImGui.Checkbox("高亮自身", ref highlightSelfBar))
                {
                    config.HighlightSelfBar = highlightSelfBar;
                    config.Save();
                }

                ImGui.SameLine(0f, 2f);
                DrawHelpMarker("开启后，统计面板里的本地玩家占比条会使用高亮色。高亮方式可在“主题色调色板”中修改。");

                ImGui.SameLine(0f, 12f);
                var lockFloatingStatsWindow = config.LockFloatingStatsWindow;
                if (ImGui.Checkbox("锁定统计窗口", ref lockFloatingStatsWindow))
                {
                    config.LockFloatingStatsWindow = lockFloatingStatsWindow;
                    config.Save();
                }

                DrawCompactHelp("锁定后不可拖动或缩放。", "启用后，悬浮窗口的位置和大小将无法手动修改。");

                var enableParty = config.PartyMonitor.EnablePartyMonitor && config.PartyMonitor.ShowPartyMonitorWindow;
                if (ImGui.Checkbox("显示监控窗口", ref enableParty))
                {
                    config.PartyMonitor.EnablePartyMonitor = enableParty;
                    config.PartyMonitor.ShowPartyMonitorWindow = enableParty;
                    config.Save();
                }

                ImGui.SameLine(0f, 12f);
                var lockPartyWindow = config.PartyMonitor.LockPartyMonitorWindow;
                if (ImGui.Checkbox("锁定监控窗口", ref lockPartyWindow))
                {
                    config.PartyMonitor.LockPartyMonitorWindow = lockPartyWindow;
                    config.Save();
                }

                ImGui.SameLine(0f, 12f);
                var autoResizePartyWindow = config.PartyMonitor.AutoResizePartyMonitorWindow;
                if (ImGui.Checkbox("自动适配", ref autoResizePartyWindow))
                {
                    config.PartyMonitor.AutoResizePartyMonitorWindow = autoResizePartyWindow;
                    config.Save();
                }

                var partyOpacity = config.PartyMonitor.PartyMonitorOpacity;
                if (ImGui.SliderFloat("队友监控窗口透明度", ref partyOpacity, 0f, 1f))
                {
                    config.PartyMonitor.PartyMonitorOpacity = partyOpacity;
                    config.Save();
                }

                ImGui.Separator();
                var showTimeline = config.ShowTimelineWindow;
                if (ImGui.Checkbox("显示时间轴窗口", ref showTimeline))
                {
                    config.ShowTimelineWindow = showTimeline;
                    config.Save();
                }

                ImGui.SameLine(0f, 12f);
                var lockTimeline = config.LockTimelineWindow;
                if (ImGui.Checkbox("锁定时间轴窗口", ref lockTimeline))
                {
                    config.LockTimelineWindow = lockTimeline;
                    config.Save();
                }

                ImGui.SameLine(0f, 12f);
                var timelineDebugMode = config.TimelineDebugMode;
                if (ImGui.Checkbox("DEBUG", ref timelineDebugMode))
                {
                    config.TimelineDebugMode = timelineDebugMode;
                    config.Save();
                }

                ImGui.SameLine(0f, 12f);
                if (ImGui.Button("重载时间轴"))
                    timelineService?.ReloadCurrentTimeline();

                ImGui.SameLine(0f, 12f);
                if (ImGui.Button("打开配置目录") && config.OpenFloatingStyleSettingsDirectory(out var openConfigDirectoryMessage))
                    floatingStyleTransferStatusText = openConfigDirectoryMessage;

                DrawCompactHelp("时间轴", "按当前副本 ZoneId 从 Timeline/Data/timeline-index.json 自动选择内置中文时间轴，只解析 timeline 文本，不执行 cactbot triggers。用户配置目录下的 Timeline/Data 可覆盖内置数据。右键时间轴窗口可打开设置。");

                var timelineOpacity = config.TimelineWindowOpacity;
                if (ImGui.SliderFloat("时间轴窗口透明度", ref timelineOpacity, 0f, 1f))
                {
                    config.TimelineWindowOpacity = timelineOpacity;
                    config.Save();
                }

                if (timelineService != null)
                {
                    ImGui.TextDisabled(timelineService.StatusText);
                    if (config.TimelineDebugMode)
                        ImGui.TextDisabled(timelineService.DebugText);
                }
            });
    }
}
