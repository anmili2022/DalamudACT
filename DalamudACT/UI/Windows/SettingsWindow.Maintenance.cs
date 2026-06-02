using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal sealed partial class SettingsWindow
{
    private void DrawMaintenanceSection()
    {
        if (!DrawFirstLevelHeader("数据与状态", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        DrawSettingCard(
            "##maintenance_actions_card",
            "数据操作",
            "用于导入测试数据、历史记录导入导出、清空历史以及恢复插件默认设置。",
            6.4f,
            DrawMaintenanceActionGrid);

        ImGui.Dummy(new Vector2(0f, 2f));
        DrawSettingCard(
            "##friendly_npc_name_list_card",
            "NPC 队友识别名单",
            "只显示当前可识别到的队伍成员，用于核对玩家与 NPC 队友是否已纳入统计。",
            10.0f,
            DrawFriendlyNpcNameListSection);

        ImGui.Dummy(new Vector2(0f, 2f));
        DrawSettingCard(
            "##maintenance_logging_card",
            "日志与调试",
            "控制是否输出调试（Debug）/详细（Verbose）级别日志，便于排查问题；普通信息 / 警告 / 错误日志不受影响。",
            9.8f,
            DrawLoggingSection);

        ImGui.Dummy(new Vector2(0f, 2f));
        DrawSettingCard(
            "##maintenance_status_card",
            "历史预览与状态",
            "控制历史记录预览时长，并查看当前历史文件路径、数据源和插件状态信息。",
            6.6f,
            () =>
            {
                var historyPreviewSeconds = config.HistoryPreviewSeconds;
                if (ImGui.SliderInt("历史记录预览时长（秒）", ref historyPreviewSeconds, 1, 30))
                {
                    config.HistoryPreviewSeconds = historyPreviewSeconds;
                    config.Save();
                }

                DrawCompactHelp("预览规则说明", "未进入战斗时，点击历史记录会无限预览该快照；进入战斗后，才按这里设置的秒数开始倒计时并自动回到当前统计。");
                if (!string.IsNullOrWhiteSpace(statsService.HistoryTransferStatusText))
                    ImGui.TextDisabled(statsService.HistoryTransferStatusText);

                if (ImGui.CollapsingHeader("路径与状态详情"))
                {
                    ImGui.TextDisabled($"历史文件: {statsService.HistoryTransferFilePath}");
                    ImGui.Dummy(new Vector2(0f, 2f));
                    ImGui.TextDisabled(statsService.DataSourceText);
                    ImGui.TextDisabled(statsService.StatusText);
                }
                else
                {
                    ImGui.TextDisabled("默认先收起路径与状态详情；需要排查时再展开查看。");
                }
            });
    }

    private void DrawLoggingSection()
    {
        var enableDebugLog = config.EnableDebugLog;
        if (ImGui.Checkbox("启用调试日志", ref enableDebugLog))
        {
            config.EnableDebugLog = enableDebugLog;
            LogHelper.EnableDebugLog = enableDebugLog;
            config.Save();
            LogHelper.Info("设置", enableDebugLog ? "已从设置中启用调试日志。" : "已从设置中关闭调试日志。");
        }

        ImGui.SameLine(0f, 16f);
        ImGui.TextDisabled("日志频道");
        ImGui.SameLine(0f, 6f);
        var logChannel = (int)config.LogChannel;
        ImGui.SetNextItemWidth(130f);
        if (ImGui.Combo("##plugin_log_channel", ref logChannel, "关闭\0Info(/xllog)\0Debug\0Echo\0ErrorMessage\0SystemMessage\0"))
        {
            config.LogChannel = (PluginLogChannel)logChannel;
            LogHelper.Channel = config.LogChannel;
            config.Save();
            LogHelper.Info("设置", $"日志频道已切换为 {GetLogChannelLabel(config.LogChannel)}。");
        }

        ImGui.Dummy(new Vector2(0f, 4f));
        ImGui.TextDisabled("调试日志分组");
        DrawDebugLogModuleCheckbox(DebugLogModule.PluginHook);
        ImGui.SameLine(0f, 12f);
        DrawDebugLogModuleCheckbox(DebugLogModule.Timeline);
        ImGui.SameLine(0f, 12f);
        DrawDebugLogModuleCheckbox(DebugLogModule.StatusObserver);
        ImGui.SameLine(0f, 12f);
        DrawDebugLogModuleCheckbox(DebugLogModule.CommandChat);

        DrawDebugLogModuleCheckbox(DebugLogModule.Configuration);
        ImGui.SameLine(0f, 12f);
        DrawDebugLogModuleCheckbox(DebugLogModule.DamageStats);
        ImGui.SameLine(0f, 12f);
        DrawDebugLogModuleCheckbox(DebugLogModule.Dot);

        ImGui.Dummy(new Vector2(0f, 4f));
        var replayStatsMode = config.ReplayStatsMode;
        if (ImGui.Checkbox("回顾模式", ref replayStatsMode))
        {
            config.ReplayStatsMode = replayStatsMode;
            config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("开启后，战斗回顾播放会按真实战斗进入统计、战斗流水和时间轴；关闭后回顾只用于时间轴辅助同步，不计入真实统计。 ");

        DrawCompactHelp("日志写入规则", "启用调试日志只控制 Debug/Verbose 是否写入 /xllog。分组开关只在启用调试日志后生效；伤害统计和 DoT 属于战斗高频日志，默认关闭。日志频道控制插件聊天通知输出位置；Info(/xllog) 表示只写入 Dalamud 日志，不往游戏聊天框输出。 ");
        ImGui.TextDisabled($"当前状态：{(config.EnableDebugLog ? "已开启" : "已关闭")}");
    }

    private void DrawDebugLogModuleCheckbox(DebugLogModule module)
    {
        var enabled = config.EnabledDebugLogModules.HasFlag(module);
        if (!config.EnableDebugLog)
            ImGui.BeginDisabled();

        if (ImGui.Checkbox(LogHelper.GetDebugLogModuleLabel(module), ref enabled))
        {
            if (enabled)
                config.EnabledDebugLogModules |= module;
            else
                config.EnabledDebugLogModules &= ~module;

            config.EnabledDebugLogModules &= DebugLogModule.All;
            LogHelper.EnabledDebugLogModules = config.EnabledDebugLogModules;
            config.Save();
            LogHelper.Info("设置", $"调试日志分组已更新：{BuildDebugLogModuleSummary(config.EnabledDebugLogModules)}。");
        }

        if (!config.EnableDebugLog)
            ImGui.EndDisabled();
    }

    private static string BuildDebugLogModuleSummary(DebugLogModule modules)
    {
        if (modules == DebugLogModule.None)
            return "未选择";

        var labels = new System.Collections.Generic.List<string>();
        foreach (var module in DebugLogModules)
        {
            if (modules.HasFlag(module))
                labels.Add(LogHelper.GetDebugLogModuleLabel(module));
        }

        return string.Join("、", labels);
    }

    private static readonly DebugLogModule[] DebugLogModules =
    {
        DebugLogModule.PluginHook,
        DebugLogModule.Timeline,
        DebugLogModule.DamageStats,
        DebugLogModule.Dot,
        DebugLogModule.StatusObserver,
        DebugLogModule.CommandChat,
        DebugLogModule.Configuration,
    };

    private static string GetLogChannelLabel(PluginLogChannel channel)
        => channel switch
        {
            PluginLogChannel.None => "关闭",
            PluginLogChannel.Info => "Info(/xllog)",
            PluginLogChannel.Debug => "Debug",
            PluginLogChannel.Echo => "Echo",
            PluginLogChannel.ErrorMessage => "ErrorMessage",
            PluginLogChannel.SystemMessage => "SystemMessage",
            _ => channel.ToString(),
        };

    private void DrawMaintenanceActionGrid()
    {
        const ImGuiTableFlags flags =
            ImGuiTableFlags.SizingStretchSame
            | ImGuiTableFlags.NoSavedSettings;

        if (!ImGui.BeginTable("##maintenance_action_grid", 2, flags))
            return;

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        if (ImGui.Button("导入测试数据", new Vector2(-1f, 0f)))
            statsService.LoadTestData();
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Button("导出历史记录", new Vector2(-1f, 0f)))
            statsService.ExportHistoricalRecords();

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        if (ImGui.Button("导入历史记录", new Vector2(-1f, 0f)))
            statsService.ImportHistoricalRecords();
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Button("清空历史", new Vector2(-1f, 0f)))
            statsService.ClearHistory();

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        if (ImGui.Button("恢复默认", new Vector2(-1f, 0f)))
        {
            config.Reset();
            StatsPanel.RequestMetricColumnWidthReset();
            StatsPanel.RequestHistoryColumnWidthReset();
            config.Save();
            LogHelper.PrintWithModule("设置", "恢复默认", "已恢复插件默认配置，并重置统计页与历史页列宽记忆。");
        }
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Button("打印当前BUFF", new Vector2(-1f, 0f)))
            DumpLocalPlayerBuffs();

        ImGui.EndTable();
    }

    private static void DumpLocalPlayerBuffs()
    {
        var localPlayer = DalamudApi.GetLocalPlayerBattleChara();
        if (localPlayer == null)
        {
            LogHelper.PrintErrorWithModule("调试", "BUFF", "未读取到本地玩家对象，无法打印当前BUFF。请确认已登录且角色已加载。");
            return;
        }

        var statuses = StatusReflectionAccessor.GetStatuses(localPlayer);
        if (statuses.Count == 0)
        {
            LogHelper.PrintErrorWithModule("调试", "BUFF", "未读取到本地玩家 StatusList/Statuses。请检查当前 Dalamud API 版本。 ");
            return;
        }

        var name = localPlayer.Name.TextValue?.Trim();
        var actorId = unchecked((uint)(localPlayer.GameObjectId & uint.MaxValue));
        if (actorId == 0)
            actorId = localPlayer.EntityId;

        LogHelper.PrintWithModule("调试", "BUFF", $"开始打印当前BUFF：name={name}，actorId=0x{actorId:X8}，job={localPlayer.ClassJob.RowId}。");

        var printed = 0;
        for (var i = 0; i < statuses.Count; i++)
        {
            var status = statuses[i];

            var statusId = StatusReflectionAccessor.GetStatusId(status);
            if (statusId == 0)
                continue;

            printed++;
            var statusName = StatusReflectionAccessor.GetName(status);
            var category = StatusReflectionAccessor.GetCategory(status);

            LogHelper.PrintWithModule(
                "调试",
                "BUFF",
                $"#{i:00} id={statusId} name={statusName} category={category} remaining={StatusReflectionAccessor.GetRemainingTime(status):0.0}s param={StatusReflectionAccessor.ReadPropertyText(status, "Param")} stacks={StatusReflectionAccessor.ReadPropertyText(status, "StackCount")} source=0x{StatusReflectionAccessor.GetSourceId(status):X8} actor=0x{StatusReflectionAccessor.GetActorId(status):X8}.");
        }

        LogHelper.PrintWithModule("调试", "BUFF", $"当前BUFF打印完成，共 {printed} 个非空状态。食物通常应关注 id/category/remaining 字段。");
    }
}
