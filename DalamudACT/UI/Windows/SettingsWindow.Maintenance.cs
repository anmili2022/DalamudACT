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
        var enableEnhancedLog = config.EnableEnhancedLog;
        if (ImGui.Checkbox("强化日志", ref enableEnhancedLog))
        {
            config.EnableEnhancedLog = enableEnhancedLog;
            config.Save();
            LogHelper.Info("设置", enableEnhancedLog ? "已启用强化日志，慢帧性能日志会写入 Debug。" : "已关闭强化日志。");
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("开启后输出 FrameworkUpdate / StatsUpdate 慢帧性能日志，用于定位卡顿来源。日志频道固定为 Debug。 ");

        ImGui.Dummy(new Vector2(0f, 4f));
        DrawHighPerformanceModeControl();

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
        DrawRefreshIntervalControls();

        ImGui.Dummy(new Vector2(0f, 4f));
        var replayStatsMode = config.ReplayStatsMode;
        if (ImGui.Checkbox("回顾模式", ref replayStatsMode))
        {
            config.ReplayStatsMode = replayStatsMode;
            config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("开启后，战斗回顾播放会按真实战斗进入统计、战斗流水和时间轴；关闭后回顾只用于时间轴辅助同步，不计入真实统计。 ");

        DrawCompactHelp("日志写入规则", "启用调试日志只控制 Debug/Verbose 是否写入；强化日志用于输出慢帧性能日志。分组开关只在启用调试日志后生效；伤害统计和 DoT 属于战斗高频日志，默认关闭。插件聊天通知频道固定为 Debug。 ");
        ImGui.TextDisabled($"当前状态：调试日志{(config.EnableDebugLog ? "已开启" : "已关闭")}，强化日志{(config.EnableEnhancedLog ? "已开启" : "已关闭")}");
    }

    private void DrawHighPerformanceModeControl()
    {
        var highPerformanceMode = config.HighPerformanceMode;
        if (ImGui.Checkbox("高性能模式", ref highPerformanceMode))
        {
            config.HighPerformanceMode = highPerformanceMode;
            config.Save();
            LogHelper.Info("设置", highPerformanceMode ? "已启用高性能模式。" : "已关闭高性能模式。");
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("适合绝境、战场和高特效场景。保留主要 DPS/HPS 和技能监控，但会降低 DoT、友方 NPC、战斗流水和部分特殊归属精度。 ");

        DrawCompactHelp(
            "高性能模式说明",
            "开启后会跳过友方 NPC 自动观察、DoT 轮询模拟、战斗流水详细采集，以及非队伍来源/目标的 ActionEffect 深解析。直伤 DPS/HPS 基本保留，精确复盘时建议关闭。 ");

        var lightweightTimeline = config.CombatTimelineLightweightMode;
        if (ImGui.Checkbox("轻量战斗流水", ref lightweightTimeline))
        {
            config.CombatTimelineLightweightMode = lightweightTimeline;
            config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("只记录战斗开始/结束、死亡、头标、连线和场地特效，跳过伤害/治疗/状态等高频流水。 ");

        var enableDotAttribution = config.EnableDotAndWildfireAttribution;
        if (ImGui.Checkbox("统计 DoT/野火归属", ref enableDotAttribution))
        {
            config.EnableDotAndWildfireAttribution = enableDotAttribution;
            config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("关闭后降低高特效战斗压力，但 DoT、野火等持续/延迟伤害会更不准。高性能模式会临时停用。 ");
    }

    private void DrawRefreshIntervalControls()
    {
        var autoRefresh = config.AutoRefreshIntervalByArea;
        if (ImGui.Checkbox("按区域自动刷新频率", ref autoRefresh))
        {
            config.AutoRefreshIntervalByArea = autoRefresh;
            config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("开启后按当前区域自动套用预设：主城/住宅=低负载，副本/野外=标准。关闭后使用下方手动间隔。 ");
        ImGui.SameLine(0f, 12f);
        ImGui.TextDisabled($"当前区域：{FormatRuntimeAreaKind(config.CurrentAreaKind)}");

        ImGui.TextDisabled("刷新频率预设");
        if (ImGui.Button("低负载##refresh_preset_low"))
            ApplyRefreshIntervalPreset(PluginConfiguration.PresetLowLoadStatsMs, PluginConfiguration.PresetLowLoadPartyMs, PluginConfiguration.PresetLowLoadStatusMs, PluginConfiguration.PresetLowLoadTimelineMs);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("适合主城、人多区域或低配置机器。降低后台刷新压力，显示会稍慢一点。DPS统计 500ms，队友监控 1000ms，状态监控 1000ms，时间轴 1000ms。 ");
        ImGui.SameLine(0f, 8f);
        if (ImGui.Button("标准##refresh_preset_balanced"))
            ApplyRefreshIntervalPreset(PluginConfiguration.PresetStandardStatsMs, PluginConfiguration.PresetStandardPartyMs, PluginConfiguration.PresetStandardStatusMs, PluginConfiguration.PresetStandardTimelineMs);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("推荐默认值。兼顾实时性和性能，适合大多数副本和日常使用。DPS统计 250ms，队友监控 500ms，状态监控 500ms，时间轴 500ms。 ");
        ImGui.SameLine(0f, 8f);
        if (ImGui.Button("低延迟##refresh_preset_fast"))
            ApplyRefreshIntervalPreset(PluginConfiguration.PresetLowLatencyStatsMs, PluginConfiguration.PresetLowLatencyPartyMs, PluginConfiguration.PresetLowLatencyStatusMs, PluginConfiguration.PresetLowLatencyTimelineMs);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("适合需要更即时反馈的战斗排查。刷新更快，但 CPU/主线程压力更高。DPS统计 100ms，队友监控 250ms，状态监控 250ms，时间轴 100ms。 ");

        ImGui.Dummy(new Vector2(0f, 2f));
        var statsInterval = config.StatsUpdateIntervalMs;
        if (DrawLabeledSliderInt("DPS统计刷新间隔", "##stats_update_interval_ms", ref statsInterval, 100, 2000, "%d ms"))
        {
            config.StatsUpdateIntervalMs = statsInterval;
            config.Save();
        }

        var partyInterval = config.PartyMonitorUpdateIntervalMs;
        if (DrawLabeledSliderInt("队友监控刷新间隔", "##party_monitor_update_interval_ms", ref partyInterval, 100, 2000, "%d ms"))
        {
            config.PartyMonitorUpdateIntervalMs = partyInterval;
            config.Save();
        }

        var statusInterval = config.StatusObserverUpdateIntervalMs;
        if (DrawLabeledSliderInt("状态监控刷新间隔", "##status_observer_update_interval_ms", ref statusInterval, 100, 2000, "%d ms"))
        {
            config.StatusObserverUpdateIntervalMs = statusInterval;
            config.Save();
        }

        var timelineInterval = config.TimelineUpdateIntervalMs;
        if (DrawLabeledSliderInt("时间轴刷新间隔", "##timeline_update_interval_ms", ref timelineInterval, 100, 2000, "%d ms"))
        {
            config.TimelineUpdateIntervalMs = timelineInterval;
            config.Save();
        }

        DrawCompactHelp("刷新频率说明", "数值越小越实时，但 CPU/主线程压力越高。队友监控和状态监控仍然只在战斗中刷新；DPS统计与时间轴会按当前区域和运行状态使用有效间隔。自动模式不会改写下方手动数值，只在运行时选择有效间隔。 ");
    }

    private void ApplyRefreshIntervalPreset(int statsMs, int partyMs, int statusMs, int timelineMs)
    {
        config.StatsUpdateIntervalMs = statsMs;
        config.PartyMonitorUpdateIntervalMs = partyMs;
        config.StatusObserverUpdateIntervalMs = statusMs;
        config.TimelineUpdateIntervalMs = timelineMs;
        config.Save();
        LogHelper.Info("设置", $"刷新频率预设已应用：DPS={statsMs}ms，队友监控={partyMs}ms，状态监控={statusMs}ms，时间轴={timelineMs}ms。");
    }

    private static string FormatRuntimeAreaKind(RuntimeAreaKind kind)
        => kind switch
        {
            RuntimeAreaKind.Duty => "副本",
            RuntimeAreaKind.City => "主城",
            RuntimeAreaKind.Field => "野外",
            RuntimeAreaKind.Housing => "住宅区",
            RuntimeAreaKind.Special => "特殊区域",
            _ => "未知",
        };

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
