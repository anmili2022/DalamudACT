using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Network;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Action = Lumina.Excel.Sheets.Action;

namespace DalamudACT;

/// <summary>
/// 插件主入口，负责 Dalamud 生命周期、Hook 安装和 Lumina 表数据读取。
/// 相关参考：
/// - https://dalamud.dev/
/// - https://dalamud.dev/api/
/// - https://github.com/NotAdam/Lumina.Excel
/// 调整 Hook、IDataManager、GetExcelSheet&lt;T&gt;() 或 ExcelSheet&lt;T&gt; 相关逻辑前，先对照这些文档。
/// </summary>
public sealed partial class ACT : IDalamudPlugin
{
    private const uint InvalidActorId = 0xE0000000;
    private const string CommandName = "/dps";
    private static readonly string PluginVersion = typeof(ACT).Assembly.GetName().Version?.ToString() ?? "未知版本";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly LocalStatsService statsService;
    private readonly PartyMonitorService monitorService;
    private readonly TimelineService timelineService;
    private readonly PluginUI ui;
    private readonly ExcelSheet<TerritoryType> territorySheet;
    private readonly ExcelSheet<Action> actionSheet;
    private bool frameworkUpdateFaulted;
    private bool abilityEffectFaulted;
    private DateTime lastUntrackedCombatDebugAtUtc;
    private DateTime lastBattleCharaPollUtc;
    private DateTime lastStatsUpdateUtc;
    private DateTime lastTimelineUpdateUtc;
    private DateTime lastRawPacketHookStateUpdateUtc;
    private DateTime lastFrameworkPerfLogUtc;
    private uint cachedTerritoryId;
    private string cachedZoneName = "未知区域";
    private int suppressedUntrackedCombatDebugCount;
    private string lastRawPacketCorrelationText = string.Empty;
    private DateTime lastRawPacketCorrelationAtUtc = DateTime.MinValue;

    private Hook<ReceiveAbilityDelegate>? receiveAbilityHook;
    private Hook<ActorControlDelegate>? mapEffectHook;
    private Hook<ActorControlDelegate>? actorControlHook;
    private RawGamePacketHook? rawGamePacketHook;
    private bool rawGamePacketHookInstallFailed;

    // 2026-05-23：ActorControl Hook 在部分 Dalamud / 客户端组合下会在 HookFromAddress 的
    // FollowJmp 阶段触发原生 AccessViolation，并直接导致游戏进程崩溃。
    // 在补上目标地址范围校验、页保护校验和显式配置开关前，默认不再安装该 Hook。
    private static bool ShouldInstallActorControlHook => false;

    public string Name => "DPS统计";

    public PluginConfiguration Configuration { get; }

    public ACT(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;

        DalamudApi.Initialize(pluginInterface);
        // TerritoryType / Action 表通过 Dalamud 的 IDataManager 读取，底层 sheet API 由 Lumina.Excel 提供。
        territorySheet = DalamudApi.GameData.GetExcelSheet<TerritoryType>()!;
        actionSheet = DalamudApi.GameData.GetExcelSheet<Action>()!;

        Configuration = pluginInterface.GetPluginConfig() as PluginConfiguration ?? new PluginConfiguration();
        Configuration.Initialize(pluginInterface);

        statsService = new LocalStatsService(Configuration);
        monitorService = new PartyMonitorService(Configuration, statsService);
        timelineService = new TimelineService(Configuration);
        InstallHooks();

        ui = new PluginUI(Configuration, statsService, monitorService, timelineService);
        RegisterCommands();
        RegisterChatHandlers();
        pluginInterface.UiBuilder.Draw += ui.Draw;
        pluginInterface.UiBuilder.OpenMainUi += ui.OpenSettingsWindow;
        pluginInterface.UiBuilder.OpenConfigUi += ui.OpenSettingsWindow;
        DalamudApi.Framework.Update += OnFrameworkUpdate;
        LogHelper.PrintWithModule("插件", "加载", $"已加载 DPS统计 v{PluginVersion}。");
    }

    public void Dispose()
    {
        DalamudApi.Framework.Update -= OnFrameworkUpdate;
        pluginInterface.UiBuilder.Draw -= ui.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= ui.OpenSettingsWindow;
        pluginInterface.UiBuilder.OpenConfigUi -= ui.OpenSettingsWindow;
        UnregisterChatHandlers();
        UnregisterCommands();
        ui.Dispose();
        DisposeHooks();
        Configuration.Save();
    }


    private void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            var perfStart = Stopwatch.GetTimestamp();
            var perfLast = perfStart;
            var perfParts = new List<string>(8);
            _ = framework;
            var territoryId = DalamudApi.GetTerritoryTypeId();
            var zoneName = GetPlaceName(territoryId);
            Configuration.CurrentAreaKind = ResolveRuntimeAreaKind(territoryId);
            MarkFrameworkPerfSegment("zone", ref perfLast, perfParts);
            var inCombat = DalamudApi.Conditions.Any(ConditionFlag.InCombat);
            var inDutyRecorderPlayback = DalamudApi.Conditions.Any(ConditionFlag.DutyRecorderPlayback);
            var replayStatsActive = Configuration.ReplayStatsMode && inDutyRecorderPlayback;
            var statsActive = inCombat || replayStatsActive;
            var timelineActive = inCombat || inDutyRecorderPlayback && statsService.HasActiveEncounter;
            var nowUtc = DateTime.UtcNow;
            var forceReplayOutOfCombat = replayStatsActive && !inCombat && statsService.IsEncounterIdle(nowUtc, TimeSpan.FromSeconds(60));
            var statsUpdateIntervalMs = Configuration.GetEffectiveStatsUpdateIntervalMs();
            var shouldUpdateStats = nowUtc - lastStatsUpdateUtc >= TimeSpan.FromMilliseconds(statsUpdateIntervalMs);
            var timelineUpdateIntervalMs = Configuration.GetEffectiveTimelineUpdateIntervalMs(timelineActive);
            var timelineUpdateInterval = TimeSpan.FromMilliseconds(timelineUpdateIntervalMs);
            var shouldUpdateTimeline = nowUtc - lastTimelineUpdateUtc >= timelineUpdateInterval;
            var ranHeavyWork = false;

            if (shouldUpdateStats)
            {
                lastStatsUpdateUtc = nowUtc;
                if (statsActive || !inDutyRecorderPlayback && statsService.HasActiveEncounter)
                {
                    statsService.WarmOwnerCacheFromObjectTable();
                    statsService.Update(zoneName, statsActive, forceReplayOutOfCombat);
                    MarkFrameworkPerfSegment("stats", ref perfLast, perfParts);
                    ranHeavyWork = true;
                }
            }

            var shouldPollBattleCharas = !ranHeavyWork && timelineActive && nowUtc - lastBattleCharaPollUtc >= TimeSpan.FromMilliseconds(100);
            if (shouldPollBattleCharas)
            {
                var battleCharas = DalamudApi.ObjectTable.OfType<IBattleChara>().ToArray();
                lastBattleCharaPollUtc = nowUtc;
                statsService.PollCombatTimelineHostileCasts(nowUtc, statsActive, battleCharas);
                timelineService.PollStartsUsingCasts(nowUtc, timelineActive, battleCharas);
                MarkFrameworkPerfSegment("casts", ref perfLast, perfParts);
                ranHeavyWork = true;
            }
            else if (!timelineActive)
            {
                timelineService.PollStartsUsingCasts(nowUtc, false, Array.Empty<IBattleChara>());
            }

            monitorService.Update();
            MarkFrameworkPerfSegment("monitor", ref perfLast, perfParts);

            if (!ranHeavyWork && shouldUpdateTimeline)
            {
                lastTimelineUpdateUtc = nowUtc;
                timelineService.Update(timelineActive, territoryId, zoneName);
                MarkFrameworkPerfSegment("timeline", ref perfLast, perfParts);
                ranHeavyWork = true;
            }

            if (!ranHeavyWork && nowUtc - lastRawPacketHookStateUpdateUtc >= TimeSpan.FromMilliseconds(500))
            {
                lastRawPacketHookStateUpdateUtc = nowUtc;
                UpdateRawGamePacketHookState();
                MarkFrameworkPerfSegment("rawHook", ref perfLast, perfParts);
            }

            LogFrameworkPerfIfSlow(perfStart, perfParts, statsActive, timelineActive);
            frameworkUpdateFaulted = false;
        }
        catch (Exception ex)
        {
            if (!frameworkUpdateFaulted)
            {
                frameworkUpdateFaulted = true;
                LogHelper.Error("插件", ex, "在 Framework 更新期间刷新本地 DPS 统计失败。");
            }
        }
    }

    private void MarkFrameworkPerfSegment(string name, ref long lastTimestamp, List<string> parts)
    {
        var now = Stopwatch.GetTimestamp();
        var elapsedMs = Stopwatch.GetElapsedTime(lastTimestamp, now).TotalMilliseconds;
        lastTimestamp = now;
        if (elapsedMs >= 1d)
            parts.Add($"{name}={elapsedMs:0.0}ms");
    }

    private void LogFrameworkPerfIfSlow(long startTimestamp, List<string> parts, bool statsActive, bool timelineActive)
    {
        if (!Configuration.EnableEnhancedLog)
            return;

        var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        if (elapsedMs < 5d)
            return;

        var nowUtc = DateTime.UtcNow;
        if (nowUtc - lastFrameworkPerfLogUtc < TimeSpan.FromSeconds(2))
            return;

        lastFrameworkPerfLogUtc = nowUtc;
        var detail = parts.Count == 0 ? "无单段超过 1ms" : string.Join(", ", parts);
        LogHelper.Info("性能", $"FrameworkUpdate 慢帧 {elapsedMs:0.0}ms：{detail}；statsActive={statsActive}，timelineActive={timelineActive}。");
    }


    private string GetPlaceName(uint territoryId)
    {
        if (territoryId == cachedTerritoryId && !string.IsNullOrWhiteSpace(cachedZoneName))
            return cachedZoneName;

        cachedTerritoryId = territoryId;
        if (territoryId == 0 || !territorySheet.TryGetRow(territoryId, out var territory))
            return cachedZoneName = "未知区域";

        try
        {
            if (!territory.ContentFinderCondition.Value.Name.IsEmpty)
                return cachedZoneName = territory.ContentFinderCondition.Value.Name.ExtractText();

            if (!territory.PlaceName.Value.Name.IsEmpty)
                return cachedZoneName = territory.PlaceName.Value.Name.ExtractText();

            if (!territory.PlaceNameRegion.Value.Name.IsEmpty)
                return cachedZoneName = territory.PlaceNameRegion.Value.Name.ExtractText();

            if (!territory.PlaceNameZone.Value.Name.IsEmpty)
                return cachedZoneName = territory.PlaceNameZone.Value.Name.ExtractText();
        }
        catch
        {
            // Fall through to the generic zone label if runtime data shape changes.
        }

        return cachedZoneName = "未知区域";
    }

    private RuntimeAreaKind ResolveRuntimeAreaKind(uint territoryId)
    {
        if (DalamudApi.Conditions.Any(ConditionFlag.BoundByDuty)
            || DalamudApi.Conditions.Any(ConditionFlag.BoundByDuty56)
            || DalamudApi.Conditions.Any(ConditionFlag.BoundByDuty95)
            || DalamudApi.Conditions.Any(ConditionFlag.DutyRecorderPlayback))
        {
            return RuntimeAreaKind.Duty;
        }

        if (IsKnownCityTerritory(territoryId))
            return RuntimeAreaKind.City;

        if (IsKnownHousingTerritory(territoryId))
            return RuntimeAreaKind.Housing;

        if (territoryId == 0)
            return RuntimeAreaKind.Unknown;

        try
        {
            if (territorySheet.TryGetRow(territoryId, out var territory)
                && !territory.ContentFinderCondition.Value.Name.IsEmpty)
            {
                return RuntimeAreaKind.Duty;
            }
        }
        catch
        {
        }

        return RuntimeAreaKind.Field;
    }

    private static bool IsKnownCityTerritory(uint territoryId)
        => territoryId is 128 or 129 or 130 or 131 or 132 or 133 or 418 or 419 or 478 or 628 or 819 or 962 or 1185 or 1186;

    private static bool IsKnownHousingTerritory(uint territoryId)
        => territoryId is 339 or 340 or 341 or 342 or 343 or 344 or 345 or 346 or 347 or 384 or 385 or 386 or 423 or 424 or 425 or 573 or 574 or 575 or 608 or 609 or 610 or 641 or 649 or 650 or 651 or 979 or 980 or 981;

    private string GetActionName(uint actionId)
    {
        if (actionId == 0)
            return "未知技能";

        if (actionSheet.TryGetRow(actionId, out var actionRow) && !actionRow.Name.IsEmpty)
            return actionRow.Name.ExtractText();

        return $"技能 {actionId}";
    }

    private bool IsLimitBreakAction(uint actionId)
    {
        if (actionId == 0)
            return false;

        if (!actionSheet.TryGetRow(actionId, out var actionRow))
            return false;

        var actionCategoryId = actionRow.ActionCategory.RowId;
        return actionCategoryId is 9 or 15;
    }

    private bool IsAutoAttackAction(uint actionId, string actionName)
    {
        if (actionId is 7 or 8)
            return true;

        if (actionId == 0)
            return false;

        if (actionSheet.TryGetRow(actionId, out var actionRow)
            && actionRow.ActionCategory.RowId == 1)
        {
            return true;
        }

        return string.Equals(actionName, "攻击", StringComparison.Ordinal)
               || string.Equals(actionName, "射击", StringComparison.Ordinal)
               || string.Equals(actionName, "Attack", StringComparison.OrdinalIgnoreCase);
    }

}
