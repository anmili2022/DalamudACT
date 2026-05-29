using System;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal sealed partial class SettingsWindow
{
    private void DrawTimelineStyleSection()
    {
        if (!DrawFirstLevelHeader("时间轴设置"))
            return;

        DrawSettingCard(
            "##timeline_style_card",
            "显示范围、间距与语音",
            "控制时间轴窗口显示范围、时间条间隔，以及是否通过 DailyRoutines 的 /pdr tts 命令播报机制名。",
            16.8f,
            () =>
            {
                var visibleSeconds = config.TimelineVisibleSeconds;
                if (ImGui.SliderInt("时间轴显示未来秒数", ref visibleSeconds, 10, 600))
                {
                    config.TimelineVisibleSeconds = visibleSeconds;
                    config.Save();
                }

                var maxEntries = config.TimelineMaxVisibleEntries;
                if (ImGui.SliderInt("时间轴最大显示条数", ref maxEntries, 1, 30))
                {
                    config.TimelineMaxVisibleEntries = maxEntries;
                    config.Save();
                }

                var rowGap = config.TimelineRowGap;
                if (ImGui.SliderFloat("时间条间隔", ref rowGap, 0f, 8f, "%.0f px"))
                {
                    config.TimelineRowGap = rowGap;
                    config.Save();
                }

                ImGui.Separator();

                var enableTts = config.EnableTimelineDailyRoutinesTts;
                if (ImGui.Checkbox("DailyRoutines TTS", ref enableTts))
                {
                    if (enableTts && !DalamudApi.IsCommandRegistered("/pdr"))
                    {
                        config.EnableTimelineDailyRoutinesTts = false;
                        DalamudApi.PrintChatMessage("[DPS统计] 未检测到 DailyRoutines：请先安装并启用 DailyRoutines 插件，再开启时间轴 TTS。");
                    }
                    else
                    {
                        config.EnableTimelineDailyRoutinesTts = enableTts;
                    }

                    config.Save();
                }

                var ttsLeadSeconds = config.TimelineTtsLeadSeconds;
                if (ImGui.SliderInt("TTS提前秒数", ref ttsLeadSeconds, 1, 30))
                {
                    config.TimelineTtsLeadSeconds = ttsLeadSeconds;
                    config.Save();
                }

                var ttsContentMode = (int)config.TimelineTtsContentMode;
                const string ttsContentLabels = "机制类型+技能名\0仅机制类型\0仅技能名\0";
                if (ImGui.Combo("TTS播报内容", ref ttsContentMode, ttsContentLabels))
                {
                    config.TimelineTtsContentMode = (TimelineTtsContentMode)ttsContentMode;
                    config.Save();
                }

                if (ImGui.Button("测试TTS"))
                    DalamudApi.TrySendChatCommand("/pdr tts 欢迎使用DPS统计");

                DrawCompactHelp("样式说明", "时间条间隔只控制每条机制之间的距离；时间轴窗口透明度仍在“窗口设置”里控制，并且只影响黑色窗体背景。DailyRoutines TTS 需要已安装并启用 Daily Routines，实际发送命令格式为 /pdr tts 文本。 ");

                ImGui.Separator();
                ImGui.TextUnformatted("在线时间轴");

                if (timelineRemoteOperationRunning)
                {
                    ImGui.BeginDisabled();
                }

                if (ImGui.Button("刷新当前副本时间轴"))
                    RunTimelineRemoteOperation(async () =>
                    {
                        if (timelineService == null)
                            return "时间轴服务未初始化。";

                        var message = await timelineService.RefreshCurrentZoneTimelineAsync().ConfigureAwait(false);
                        timelineService.ReloadCurrentTimeline();
                        return message;
                    });

                ImGui.SameLine();
                if (ImGui.Button("下载全部时间轴"))
                    RunTimelineRemoteOperation(async () =>
                    {
                        if (timelineService == null)
                            return "时间轴服务未初始化。";

                        var message = await timelineService.DownloadAllTimelinesAsync().ConfigureAwait(false);
                        timelineService.ReloadCurrentTimeline();
                        return message;
                    });

                if (timelineRemoteOperationRunning)
                {
                    ImGui.EndDisabled();
                    ImGui.SameLine();
                    ImGui.TextUnformatted("下载中...");
                }

                DrawCompactHelp("在线时间轴说明", "刷新当前副本时间轴只下载当前区域匹配的时间轴；下载全部时间轴会下载索引中的所有时间轴，用于离线使用或批量更新。在线缓存位置为 pluginConfigs/DalamudACT/Timeline/RemoteCache/Data，用户手动时间轴仍然优先。 ");

                if (!string.IsNullOrWhiteSpace(timelineRemoteStatusText))
                    ImGui.TextWrapped(timelineRemoteStatusText);
            });
    }

    private void RunTimelineRemoteOperation(Func<Task<string>> operation)
    {
        if (timelineRemoteOperationRunning)
            return;

        timelineRemoteOperationRunning = true;
        timelineRemoteStatusText = "下载中...";
        _ = Task.Run(async () =>
        {
            try
            {
                timelineRemoteStatusText = await operation().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                timelineRemoteStatusText = $"在线时间轴操作失败：{ex.Message}";
                LogHelper.Warning("时间轴", ex, "在线时间轴操作失败。 ");
            }
            finally
            {
                timelineRemoteOperationRunning = false;
            }
        });
    }

    private void DrawCommandHelpSection()
    {
        if (!DrawFirstLevelHeader("帮助"))
            return;

        DrawSettingCard(
            "##command_help_card",
            "宏命令",
            "当前只注册 /dps，一个主命令下通过子命令打开或切换不同窗口。",
            7.8f,
            () =>
            {
                if (!ImGui.BeginTable("##command_help_table", 2, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
                    return;

                ImGui.TableSetupColumn("命令", ImGuiTableColumnFlags.WidthFixed, 150f);
                ImGui.TableSetupColumn("作用");
                ImGui.TableHeadersRow();

                DrawCommandHelpRow("/dps", "显示宏帮助。");
                DrawCommandHelpRow("/dps help", "显示宏帮助。");
                DrawCommandHelpRow("/dps settings", "切换设置面板。");
                DrawCommandHelpRow("/dps dps", "切换 DPS 统计悬浮窗。");
                DrawCommandHelpRow("/dps skills", "切换队友技能监控悬浮窗。");
                DrawCommandHelpRow("/dps timeline", "切换时间轴悬浮窗。");
                DrawCommandHelpRow("/dps time", "切换时间轴悬浮窗。");

                ImGui.EndTable();
            });
    }

    private static void DrawCommandHelpRow(string command, string description)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(command);
        ImGui.TableSetColumnIndex(1);
        ImGui.TextWrapped(description);
    }
}
