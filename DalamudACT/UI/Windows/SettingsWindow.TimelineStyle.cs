using System.Linq;
using System.Numerics;
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
                        timelineDraftStatusText = "未检测到 DailyRoutines：请先安装并启用 DailyRoutines 插件，再开启 TTS。";
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
                ImGui.TextUnformatted("ACT日志生成草稿");

                var actLogDirectory = config.ActLogDirectory ?? string.Empty;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.InputText("##act_log_directory", ref actLogDirectory, 512))
                {
                    config.ActLogDirectory = actLogDirectory;
                    config.Save();
                }

                var actLogFilePath = config.ActLogFilePath ?? string.Empty;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.InputText("##act_log_file_path", ref actLogFilePath, 512))
                {
                    config.ActLogFilePath = actLogFilePath;
                    config.Save();
                }

                if (ImGui.Button("选择日志文件"))
                {
                    if (WindowsFileDialog.TryPickLogFile(config.ActLogDirectory ?? string.Empty, out var selectedPath, out var errorMessage))
                    {
                        config.ActLogFilePath = selectedPath;
                        config.ActLogDirectory = System.IO.Path.GetDirectoryName(selectedPath) ?? string.Empty;
                        config.Save();
                        timelineDraftStatusText = $"已选择日志文件：{selectedPath}";
                    }
                    else if (!string.IsNullOrWhiteSpace(errorMessage))
                    {
                        timelineDraftStatusText = $"选择日志文件失败：{errorMessage}";
                    }
                }

                ImGui.SameLine();
                if (ImGui.Button("使用最新日志"))
                {
                    config.ActLogFilePath = string.Empty;
                    config.Save();
                    timelineDraftStatusText = "已切换为使用目录中的最新 Network*.log。";
                }

                var selectedLog = string.IsNullOrWhiteSpace(config.ActLogFilePath)
                    ? "当前：使用目录中的最新 Network*.log"
                    : $"当前：{config.ActLogFilePath}";
                ImGui.TextWrapped(selectedLog);

                if (ImGui.Button("刷新战斗列表"))
                {
                    timelineLogEncounterOptions = TimelineLogImporter.GetEncounterOptions(config, out var refreshMessage).ToList();
                    timelineDraftStatusText = refreshMessage;
                    if (timelineLogEncounterOptions.Count > 0 && !timelineLogEncounterOptions.Any(option => option.Key == config.ActLogEncounterKey))
                    {
                        config.ActLogEncounterKey = timelineLogEncounterOptions[0].Key;
                        config.Save();
                    }
                }

                var selectedEncounterLabel = timelineLogEncounterOptions.FirstOrDefault(option => option.Key == config.ActLogEncounterKey)?.Label
                                             ?? (string.IsNullOrWhiteSpace(config.ActLogEncounterKey) ? "未选择：默认使用最新战斗" : config.ActLogEncounterKey);
                if (BeginLabeledCombo("选择战斗", "##act_log_encounter", selectedEncounterLabel))
                {
                    try
                    {
                        if (ImGui.Selectable("未选择：默认使用最新战斗", string.IsNullOrWhiteSpace(config.ActLogEncounterKey)))
                        {
                            config.ActLogEncounterKey = string.Empty;
                            config.Save();
                        }

                        foreach (var option in timelineLogEncounterOptions)
                        {
                            var selected = option.Key == config.ActLogEncounterKey;
                            if (ImGui.Selectable(option.Label, selected))
                            {
                                config.ActLogEncounterKey = option.Key;
                                config.Save();
                            }
                        }
                    }
                    finally
                    {
                        ImGui.EndCombo();
                    }
                }

                if (ImGui.Button("生成时间轴草稿"))
                    timelineDraftStatusText = TimelineLogImporter.GenerateLatestDraft(config);

                ImGui.SameLine();
                if (ImGui.Button("打开草稿目录"))
                    timelineDraftStatusText = TimelineLogImporter.OpenGeneratedDirectory();

                ImGui.SameLine();
                if (ImGui.Button("刷新额外资源"))
                    timelineDraftStatusText = AeAssistResourceDownloader.RefreshResourcesForSettings();

                DrawCompactHelp("日志目录", "第一行填写 ACT 的 FFXIVLogs 目录，例如 D:\\ff14act\\FFXIVLogs。第二行可直接填写具体日志文件路径；选择日志文件后会优先读取选中文件；点击“使用最新日志”后回退为读取目录中的最新 Network*.log。刷新战斗列表后可按开始时间选择具体一场战斗生成草稿；未选择时默认使用最新战斗。额外资源会保存到 Timeline/Resource，用于生成草稿时标注 AOE/死刑。 ");

                if (!string.IsNullOrWhiteSpace(timelineDraftStatusText))
                    ImGui.TextWrapped(timelineDraftStatusText);
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
