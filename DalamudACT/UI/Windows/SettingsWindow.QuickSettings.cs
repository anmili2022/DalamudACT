using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal sealed partial class SettingsWindow
{
    private void DrawQuickSettings()
    {
        ImGui.TextUnformatted("简易设置");
        ImGui.SameLine();
        if (ImGui.Button("战斗流水"))
            openCombatTimelineWindow();

        ImGui.SameLine();
        if (ImGui.Button("完整设置 →"))
        {
            showAdvancedSettings = true;
            Size = new Vector2(620f, 760f);
        }

        ImGui.Separator();

        DrawQuickWindowSection();
        DrawQuickOpacitySection();
        DrawQuickStyleSection();
        DrawQuickTtsSection();
        DrawQuickTimelineSection();
    }

    private void DrawQuickWindowSection()
    {
        if (!DrawFirstLevelHeader("窗口显隐与锁定", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var showStats = config.ShowStatsPanel;
        if (ImGui.Checkbox("显示统计面板", ref showStats))
        {
            config.ShowStatsPanel = showStats;
            config.ShowDemoPanel = showStats;
            config.Save();
        }

        ImGui.SameLine(0f, 12f);
        var lockStats = config.LockFloatingStatsWindow;
        if (ImGui.Checkbox("锁定统计面板", ref lockStats))
        {
            config.LockFloatingStatsWindow = lockStats;
            config.Save();
        }

        var enableParty = config.PartyMonitor.EnablePartyMonitor && config.PartyMonitor.ShowPartyMonitorWindow;
        if (ImGui.Checkbox("显示监控窗口", ref enableParty))
        {
            config.PartyMonitor.EnablePartyMonitor = enableParty;
            config.PartyMonitor.ShowPartyMonitorWindow = enableParty;
            config.Save();
        }

        ImGui.SameLine(0f, 12f);
        var lockParty = config.PartyMonitor.LockPartyMonitorWindow;
        if (ImGui.Checkbox("锁定监控窗口", ref lockParty))
        {
            config.PartyMonitor.LockPartyMonitorWindow = lockParty;
            config.Save();
        }

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

        var showStatus = config.StatusObserver.ShowWindow;
        if (ImGui.Checkbox("显示状态监控窗口", ref showStatus))
        {
            config.StatusObserver.ShowWindow = showStatus;
            config.Save();
        }

        ImGui.SameLine(0f, 12f);
        var lockStatus = config.StatusObserver.LockWindow;
        if (ImGui.Checkbox("锁定状态监控窗口", ref lockStatus))
        {
            config.StatusObserver.LockWindow = lockStatus;
            config.Save();
        }
    }

    private void DrawQuickOpacitySection()
    {
        if (!DrawFirstLevelHeader("透明度", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var mainOpacity = config.WindowOpacity;
        if (ImGui.SliderFloat("主界面透明度", ref mainOpacity, 0.2f, 1f))
        {
            config.WindowOpacity = mainOpacity;
            config.Save();
        }

        var statsOpacity = config.FloatingStatsOpacity;
        if (ImGui.SliderFloat("DPS统计面板透明度", ref statsOpacity, 0f, 1f))
        {
            config.FloatingStatsOpacity = statsOpacity;
            config.Save();
        }

        var partyOpacity = config.PartyMonitor.PartyMonitorOpacity;
        if (ImGui.SliderFloat("技能监控窗口透明度", ref partyOpacity, 0f, 1f))
        {
            config.PartyMonitor.PartyMonitorOpacity = partyOpacity;
            config.Save();
        }

        var timelineOpacity = config.TimelineWindowOpacity;
        if (ImGui.SliderFloat("时间轴窗口透明度", ref timelineOpacity, 0.2f, 1f))
        {
            config.TimelineWindowOpacity = timelineOpacity;
            config.Save();
        }

        var statusOpacity = config.StatusObserver.WindowOpacity;
        if (ImGui.SliderFloat("状态监控透明度", ref statusOpacity, 0f, 1f))
        {
            config.StatusObserver.WindowOpacity = statusOpacity;
            config.Save();
        }
    }

    private void DrawQuickStyleSection()
    {
        if (!DrawFirstLevelHeader("悬浮 DPS 样式", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var currentStyle = config.FloatingStatsDisplayStyle;
        var styleLabel = currentStyle switch
        {
            FloatingStatsDisplayStyle.Classic => "Classic",
            FloatingStatsDisplayStyle.Ikegami => "Ikegami",
            FloatingStatsDisplayStyle.Minimal => "Minimal",
            _ => "Classic",
        };

        if (ImGui.BeginCombo("展示模式", styleLabel))
        {
            foreach (FloatingStatsDisplayStyle style in Enum.GetValues(typeof(FloatingStatsDisplayStyle)))
            {
                var label = style switch
                {
                    FloatingStatsDisplayStyle.Classic => "Classic",
                    FloatingStatsDisplayStyle.Ikegami => "Ikegami",
                    FloatingStatsDisplayStyle.Minimal => "Minimal",
                    _ => style.ToString(),
                };

                if (ImGui.Selectable(label, currentStyle == style))
                {
                    config.SwitchFloatingStatsDisplayStyle(style);
                    currentStyle = config.FloatingStatsDisplayStyle;
                }
            }

            ImGui.EndCombo();
        }

        DrawQuickStyleParams(currentStyle);
    }

    private void DrawQuickStyleParams(FloatingStatsDisplayStyle style)
    {
        switch (style)
        {
            case FloatingStatsDisplayStyle.Classic:
            {
                var rowHeight = config.FloatingStatsRowHeight;
                if (ImGui.SliderFloat("表格行高", ref rowHeight, 20f, 40f))
                {
                    config.FloatingStatsRowHeight = rowHeight;
                    config.Save();
                }

                break;
            }

            case FloatingStatsDisplayStyle.Ikegami:
            {
                var boxWidth = config.FloatingStatsIkegamiBoxWidth;
                if (ImGui.SliderFloat("小框宽度", ref boxWidth, 120f, 400f))
                {
                    config.FloatingStatsIkegamiBoxWidth = boxWidth;
                    config.Save();
                }

                var boxHeight = config.FloatingStatsIkegamiBoxHeight;
                if (ImGui.SliderFloat("小框高度", ref boxHeight, 30f, 200f))
                {
                    config.FloatingStatsIkegamiBoxHeight = boxHeight;
                    config.Save();
                }

                var minimal = config.FloatingStatsIkegamiMinimalMode;
                if (ImGui.Checkbox("极简模式", ref minimal))
                {
                    config.FloatingStatsIkegamiMinimalMode = minimal;
                    config.Save();
                }

                break;
            }

            case FloatingStatsDisplayStyle.Minimal:
            {
                var autoHeight = config.FloatingStatsMinimalAutoWindowHeight;
                if (ImGui.Checkbox("高度自动适配条目数", ref autoHeight))
                {
                    config.FloatingStatsMinimalAutoWindowHeight = autoHeight;
                    config.Save();
                }

                var rowHeight = config.FloatingStatsMinimalRowHeight;
                if (ImGui.SliderFloat("表格行高", ref rowHeight, 16f, 40f))
                {
                    config.FloatingStatsMinimalRowHeight = rowHeight;
                    config.Save();
                }

                var fontScale = config.FloatingStatsMinimalFontScale;
                if (ImGui.SliderFloat("字号缩放", ref fontScale, 0.5f, 2f))
                {
                    config.FloatingStatsMinimalFontScale = fontScale;
                    config.Save();
                }

                break;
            }
        }
    }

    private void DrawQuickTtsSection()
    {
        if (!DrawFirstLevelHeader("TTS 播报", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var ttsEnabled = config.EnableTimelineDailyRoutinesTts;
        if (ImGui.Checkbox("启用时间轴 TTS（需开启DR的文本转语音）", ref ttsEnabled))
        {
            config.EnableTimelineDailyRoutinesTts = ttsEnabled;
            config.Save();
        }

        var ttsMechanic = config.TimelineTtsMechanic;
        if (ImGui.Checkbox("播报机制（AOE/死刑分类）", ref ttsMechanic))
        {
            config.TimelineTtsMechanic = ttsMechanic;
            config.Save();
        }

        var ttsResponse = config.TimelineTtsResponse;
        if (ImGui.Checkbox("播报应对方案（分摊/分散/去背后..等）", ref ttsResponse))
        {
            config.TimelineTtsResponse = ttsResponse;
            config.Save();
        }

        var leadSeconds = config.TimelineTtsLeadSeconds;
        if (ImGui.SliderInt("TTS 提前秒数", ref leadSeconds, 0, 10))
        {
            config.TimelineTtsLeadSeconds = leadSeconds;
            config.Save();
        }
    }

    private void DrawQuickTimelineSection()
    {
        if (!DrawFirstLevelHeader("时间轴", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var autoDl = config.TimelineAutoDownloadOnEnter;
        if (ImGui.Checkbox("进入副本时自动下载时间轴", ref autoDl))
        {
            config.TimelineAutoDownloadOnEnter = autoDl;
            config.Save();
        }

        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.7f, 0.2f, 1f));
        ImGui.TextUnformatted("注：目前时间轴仅涵盖 7.x 版本内容。6.x 及更早版本暂不支持。");
        ImGui.PopStyleColor();

        var visibleSeconds = config.TimelineVisibleSeconds;
        if (ImGui.SliderInt("显示未来秒数", ref visibleSeconds, 10, 120))
        {
            config.TimelineVisibleSeconds = visibleSeconds;
            config.Save();
        }

        var maxEntries = config.TimelineMaxVisibleEntries;
        if (ImGui.SliderInt("最大显示条数", ref maxEntries, 5, 50))
        {
            config.TimelineMaxVisibleEntries = maxEntries;
            config.Save();
        }

        var rowGap = config.TimelineRowGap;
        if (ImGui.SliderFloat("时间条间隔", ref rowGap, 0f, 20f))
        {
            config.TimelineRowGap = rowGap;
            config.Save();
        }

        ImGui.Dummy(new Vector2(0f, 4f));
        ImGui.Separator();
        ImGui.TextDisabled("DEBUG");

        if (timelineService is null)
        {
            ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "TimelineService 未初始化");
            return;
        }

        var dbg = timelineService.DebugText;
        ImGui.TextUnformatted($"区域: {dbg}");
        ImGui.TextUnformatted($"定义: {timelineService.DefinitionName}");
        ImGui.TextUnformatted($"状态: {(timelineService.HasTimeline ? "已加载" : "未加载")}");
        ImGui.TextUnformatted($"运行: {(timelineService.IsRunning ? $"是 ({timelineService.CurrentTimeSeconds:F1}s)" : "否")}");
        ImGui.TextUnformatted($"自动下载: {timelineService.AutoDownloadStatusText}");
    }
}
