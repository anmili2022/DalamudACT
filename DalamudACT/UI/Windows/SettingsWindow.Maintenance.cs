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

        DrawCompactHelp("日志写入规则", "开启后，会把调试（Debug）与详细（Verbose）日志写入 Dalamud 插件日志。");
        ImGui.TextDisabled($"当前状态：{(config.EnableDebugLog ? "已开启" : "已关闭")}");

        if (!ImGui.CollapsingHeader("最近日志摘要"))
        {
            ImGui.TextDisabled(LogUiHelper.HasRecentLogs
                ? "默认先收起最近日志摘要；需要查看最近输出时再展开。"
                : "当前没有最近日志摘要。");
            return;
        }

        LogUiHelper.DrawRecentLogToolbar();
        LogUiHelper.DrawRecentLogList(10);
    }

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

        var rawStatusList = localPlayer.GetType().GetProperty("StatusList")?.GetValue(localPlayer)
                            ?? localPlayer.GetType().GetProperty("Statuses")?.GetValue(localPlayer);
        if (rawStatusList == null)
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
        var length = ReadIntProperty(rawStatusList, "Length");
        if (length <= 0)
            length = ReadIntProperty(rawStatusList, "Count");

        for (var i = 0; i < length; i++)
        {
            var status = ReadIndexedValue(rawStatusList, i);
            if (status == null)
                continue;

            var statusId = ReadUIntProperty(status, "StatusId");
            if (statusId == 0)
                statusId = ReadUIntProperty(status, "Id");
            if (statusId == 0)
                continue;

            printed++;
            var statusName = "未知";
            var category = 0u;
            try
            {
                var gameDataRef = status.GetType().GetProperty("GameData")?.GetValue(status);
                var gameData = gameDataRef?.GetType().GetProperty("Value")?.GetValue(gameDataRef);
                statusName = gameData?.GetType().GetProperty("Name")?.GetValue(gameData)?.ToString() ?? statusName;
                category = ReadUIntProperty(gameData!, "StatusCategory");
            }
            catch
            {
            }

            LogHelper.PrintWithModule(
                "调试",
                "BUFF",
                $"#{i:00} id={statusId} name={statusName} category={category} remaining={ReadFloatProperty(status, "RemainingTime"):0.0}s param={ReadProperty(status, "Param")} stacks={ReadProperty(status, "StackCount")} source=0x{ReadUIntProperty(status, "SourceId"):X8} actor=0x{ReadUIntProperty(status, "ActorId"):X8}.");
        }

        LogHelper.PrintWithModule("调试", "BUFF", $"当前BUFF打印完成，共 {printed} 个非空状态。食物通常应关注 id/category/remaining 字段。");
    }

    private static string ReadProperty(object instance, string propertyName)
    {
        try
        {
            return instance.GetType().GetProperty(propertyName)?.GetValue(instance)?.ToString() ?? "-";
        }
        catch
        {
            return "-";
        }
    }

    private static object? ReadIndexedValue(object instance, int index)
    {
        try
        {
            return instance.GetType().GetProperty("Item")?.GetValue(instance, [index]);
        }
        catch
        {
            return null;
        }
    }

    private static int ReadIntProperty(object instance, string propertyName)
    {
        try
        {
            var value = instance.GetType().GetProperty(propertyName)?.GetValue(instance);
            return value == null ? 0 : Convert.ToInt32(value);
        }
        catch
        {
            return 0;
        }
    }

    private static float ReadFloatProperty(object instance, string propertyName)
    {
        try
        {
            var value = instance.GetType().GetProperty(propertyName)?.GetValue(instance);
            return value == null ? 0f : Convert.ToSingle(value);
        }
        catch
        {
            return 0f;
        }
    }

    private static uint ReadUIntProperty(object instance, string propertyName)
    {
        try
        {
            var value = instance.GetType().GetProperty(propertyName)?.GetValue(instance);
            return value == null ? 0 : Convert.ToUInt32(value);
        }
        catch
        {
            return 0;
        }
    }
}
