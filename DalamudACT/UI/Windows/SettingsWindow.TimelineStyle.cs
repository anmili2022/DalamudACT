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
                DrawForceLoadTimelineControls();
                ImGui.Separator();

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

                var mech = config.TimelineTtsMechanic;
                if (ImGui.Checkbox("播报机制（AOE/死刑分类）", ref mech))
                {
                    config.TimelineTtsMechanic = mech;
                    config.Save();
                }

                var skill = config.TimelineTtsSkillName;
                if (ImGui.Checkbox("播报技能名", ref skill))
                {
                    config.TimelineTtsSkillName = skill;
                    config.Save();
                }

                var resp = config.TimelineTtsResponse;
                if (ImGui.Checkbox("播报应对方案", ref resp))
                {
                    config.TimelineTtsResponse = resp;
                    config.Save();
                }

                var ttsLeadSeconds = config.TimelineTtsLeadSeconds;
                if (ImGui.SliderInt("TTS提前秒数", ref ttsLeadSeconds, 1, 30))
                {
                    config.TimelineTtsLeadSeconds = ttsLeadSeconds;
                    config.Save();
                }

                if (ImGui.Button("测试TTS"))
                    DalamudApi.TrySendChatCommand($"/pdr tts {config.ApplyTimelineTtsCorrections("欢迎使用DPS统计")}");

                DrawTimelineTtsCorrectionsEditor();

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.TextUnformatted("Timer 时间标记");
                ImGui.TextDisabled("不依赖任何游戏事件，纯按战斗时间显示的条目，播报归\u201c播报应对方案\u201d控制。");
                if (ImGui.Selectable("115 \"准备集合放黄圈\" Timer { }"))
                    ImGui.SetClipboardText("115 \"准备集合放黄圈\" Timer { }");

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
                if (ImGui.Button("更新时间轴"))
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

                DrawCompactHelp("在线时间轴说明", "刷新当前副本时间轴只下载当前区域匹配的时间轴；更新时间轴会下载索引中的所有时间轴，用于离线使用或批量更新。在线缓存位置为 pluginConfigs/DalamudACT/Timeline/RemoteCache/Data，用户手动时间轴仍然优先。 ");

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

    private void DrawForceLoadTimelineControls()
    {
        ImGui.TextUnformatted("强制加载时间轴");
        if (timelineService == null)
        {
            ImGui.TextDisabled("时间轴服务未初始化。");
            return;
        }

        ImGui.SetNextItemWidth(-1f);
        ImGui.InputText("##force_load_timeline_path", ref timelineForceLoadPath, 512);

        if (ImGui.Button("强制加载时间轴"))
            timelineRemoteStatusText = timelineService.ForceLoadTimelineFile(timelineForceLoadPath);

        ImGui.SameLine();
        if (ImGui.Button("取消强制加载"))
            timelineRemoteStatusText = timelineService.ClearForcedTimeline();

        if (timelineService.HasForcedTimeline)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("当前：强制加载中");
        }

        DrawCompactHelp("强制加载说明", "输入或粘贴 .txt / .cn.txt 时间轴文件完整路径后加载，忽略当前副本 ZoneId。用于测试时间轴文件。取消后恢复按当前区域自动匹配。 ");
    }

    private void DrawTimelineTtsCorrectionsEditor()
    {
        config.EnsureTimelineTtsCorrections();

        ImGui.Separator();
        if (!ImGui.CollapsingHeader($"TTS纠偏规则（{config.TimelineTtsCorrections.Count}）###timeline_tts_corrections"))
            return;

        DrawCompactHelp("TTS纠偏说明", "发送 TTS 前会按列表把原词替换成纠偏词。纠偏会作用在完整文本中，例如“地动山摇”命中“地动 -> 帝动”后会播报为“帝动山摇”。");

        var removeIndex = -1;
        for (var i = 0; i < config.TimelineTtsCorrections.Count; i++)
        {
            var rule = config.TimelineTtsCorrections[i];
            ImGui.PushID(i);
            DrawTtsCorrectionRuleRow(rule, i, ref removeIndex);
            ImGui.PopID();
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

    private void DrawTtsCorrectionRuleRow(TtsCorrectionRule rule, int index, ref int removeIndex)
    {
        var style = ImGui.GetStyle();
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var inputWidth = Math.Clamp((availableWidth - 112f - style.ItemSpacing.X * 4f) * 0.5f, 90f, 180f);

        if (index > 0)
            ImGui.Separator();

        var enabled = rule.Enabled;
        if (ImGui.Checkbox("启用", ref enabled))
        {
            rule.Enabled = enabled;
            config.Save();
        }

        ImGui.SameLine(0f, 10f);
        ImGui.TextDisabled($"规则 {index + 1}");
        ImGui.SameLine(0f, 12f);
        if (ImGui.SmallButton("删除"))
            removeIndex = index;

        ImGui.TextDisabled("原词");
        ImGui.SameLine(inputWidth + style.ItemSpacing.X + 20f);
        ImGui.TextDisabled("纠偏词");

        var from = rule.From ?? string.Empty;
        ImGui.SetNextItemWidth(inputWidth);
        if (ImGui.InputText("##from", ref from, 64, ImGuiInputTextFlags.AutoSelectAll))
        {
            rule.From = from;
            config.Save();
        }

        ImGui.SameLine(0f, 6f);
        ImGui.TextUnformatted("→");
        ImGui.SameLine(0f, 6f);

        var to = rule.To ?? string.Empty;
        ImGui.SetNextItemWidth(inputWidth);
        if (ImGui.InputText("##to", ref to, 64, ImGuiInputTextFlags.AutoSelectAll))
        {
            rule.To = to;
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
                DrawCommandHelpRow("/dps status", "切换状态监控窗口。");
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
