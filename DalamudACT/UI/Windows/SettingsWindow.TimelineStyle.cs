using System;
using System.Numerics;
using System.Threading;
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
                    DalamudApi.TrySendChatCommand($"/pdr tts {config.ApplyTimelineTtsCorrections("欢迎使用DPS统计")}");

                DrawTimelineTtsCorrectionsEditor();

                DrawCompactHelp("样式说明", "时间条间隔只控制每条机制之间的距离；时间轴窗口透明度仍在“窗口设置”里控制，并且只影响黑色窗体背景。DailyRoutines TTS 需要已安装并启用 Daily Routines，实际发送命令格式为 /pdr tts 文本。 ");

                ImGui.Separator();
                ImGui.TextUnformatted("在线时间轴");

                var remoteOperationRunning = timelineRemoteOperationRunning;
                if (remoteOperationRunning)
                {
                    ImGui.BeginDisabled();
                }

                if (ImGui.Button("刷新当前副本时间轴"))
                    RunTimelineRemoteOperation(async cancellationToken =>
                    {
                        if (timelineService == null)
                            return "时间轴服务未初始化。";

                        var message = await timelineService.RefreshCurrentZoneTimelineAsync(CreateTimelineRemoteProgress(), cancellationToken).ConfigureAwait(false);
                        timelineService.ReloadCurrentTimeline();
                        return message;
                    });

                ImGui.SameLine();
                if (ImGui.Button("下载全部时间轴"))
                    RunTimelineRemoteOperation(async cancellationToken =>
                    {
                        if (timelineService == null)
                            return "时间轴服务未初始化。";

                        var message = await timelineService.DownloadAllTimelinesAsync(CreateTimelineRemoteProgress(), cancellationToken).ConfigureAwait(false);
                        timelineService.ReloadCurrentTimeline();
                        return message;
                    });

                if (remoteOperationRunning)
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

    private void RunTimelineRemoteOperation(Func<CancellationToken, Task<string>> operation)
    {
        if (timelineRemoteOperationRunning)
            return;

        timelineRemoteOperationRunning = true;
        timelineRemoteStatusText = "下载中...";
        _ = Task.Run(async () =>
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            try
            {
                timelineRemoteStatusText = await operation(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                timelineRemoteStatusText = "在线时间轴操作超时，请稍后重试。";
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

    private Progress<string> CreateTimelineRemoteProgress()
        => new(message => timelineRemoteStatusText = message);

    private void DrawTimelineTtsCorrectionsEditor()
    {
        config.EnsureTimelineTtsCorrections();

        ImGui.Separator();
        ImGui.TextUnformatted("TTS纠偏");
        DrawCompactHelp("TTS纠偏说明", "发送 TTS 前会按列表把原词替换成纠偏词。纠偏会作用在完整文本中，例如“地动山摇”命中“地动 -> 帝动”后会播报为“帝动山摇”。");

        var removeIndex = -1;
        if (ImGui.BeginTable("##timeline_tts_corrections", 4, ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings))
        {
            ImGui.TableSetupColumn("启用", ImGuiTableColumnFlags.WidthFixed, 46f);
            ImGui.TableSetupColumn("原词");
            ImGui.TableSetupColumn("纠偏词");
            ImGui.TableSetupColumn("操作", ImGuiTableColumnFlags.WidthFixed, 54f);
            ImGui.TableHeadersRow();

            for (var i = 0; i < config.TimelineTtsCorrections.Count; i++)
            {
                var rule = config.TimelineTtsCorrections[i];
                ImGui.PushID(i);
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                var enabled = rule.Enabled;
                if (ImGui.Checkbox("##enabled", ref enabled))
                {
                    rule.Enabled = enabled;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                var from = rule.From ?? string.Empty;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.InputText("##from", ref from, 64))
                {
                    rule.From = from;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(2);
                var to = rule.To ?? string.Empty;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.InputText("##to", ref to, 64))
                {
                    rule.To = to;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(3);
                if (ImGui.SmallButton("删除"))
                    removeIndex = i;

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        if (removeIndex >= 0 && removeIndex < config.TimelineTtsCorrections.Count)
        {
            config.TimelineTtsCorrections.RemoveAt(removeIndex);
            config.Save();
        }

        if (ImGui.Button("新增纠偏"))
        {
            config.TimelineTtsCorrections.Add(new TtsCorrectionRule { From = string.Empty, To = string.Empty, Enabled = true });
            config.Save();
        }
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
                DrawCommandHelpRow("/dps status", "切换状态观察窗口。");
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
