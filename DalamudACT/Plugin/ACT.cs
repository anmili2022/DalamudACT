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
    private readonly TankInvulnerabilityTtsService tankInvulnerabilityTtsService;
    private readonly PluginUI ui;
    private readonly ExcelSheet<TerritoryType> territorySheet;
    private readonly ExcelSheet<Action> actionSheet;
    private bool frameworkUpdateFaulted;
    private bool abilityEffectFaulted;
    private bool isDisposing;
    private DateTime lastUntrackedCombatDebugAtUtc;
    private DateTime lastBattleCharaPollUtc;
    private DateTime lastStatsUpdateUtc;
    private DateTime lastTimelineUpdateUtc;
    private DateTime lastRawPacketHookStateUpdateUtc;
    private DateTime lastFrameworkPerfLogUtc;
    private DateTime lastActionEffectPerfLogUtc;
    private int observedPartyWipeFinalizedVersion;
    private uint cachedTerritoryId;
    private string cachedZoneName = "未知区域";
    private int suppressedUntrackedCombatDebugCount;
    private string lastRawPacketCorrelationText = string.Empty;
    private DateTime lastRawPacketCorrelationAtUtc = DateTime.MinValue;
    private DateTime lastBattleCountdownFiveSecondsUtc = DateTime.MinValue;
    private RuntimeAreaKind lastRuntimeAreaKind = RuntimeAreaKind.Unknown;
    private DateTime pendingPartyMonitorDutyEnterRefreshUntilUtc = DateTime.MinValue;
    private DateTime pendingPartyMonitorDutyEnterPartyCountChangedUtc = DateTime.MinValue;
    private int pendingPartyMonitorDutyEnterPartyCount = -1;
    private bool hasRuntimeAreaKindSnapshot;

    private Hook<ReceiveAbilityDelegate>? receiveAbilityHook;
    private Hook<ActorControlDelegate>? actorControlEventHook;
    private RawGamePacketHook? rawGamePacketHook;
    private DateTime lastActorControlWipeResetUtc;
    private bool rawGamePacketHookInstallFailed;

    public string Name => "DPS统计";

    public PluginConfiguration Configuration { get; }

    private bool IsStatsModuleEnabled => Configuration.ShowStatsPanel;

    private bool IsPartyMonitorModuleEnabled => Configuration.PartyMonitor.EnablePartyMonitor && Configuration.PartyMonitor.ShowPartyMonitorWindow;

    private bool IsTimelineModuleEnabled => Configuration.ShowTimelineWindow;

    private bool IsTimelineModuleEnabledInCurrentArea
        => IsTimelineModuleEnabled && IsTimelineRuntimeAreaEnabled(Configuration.CurrentAreaKind);

    private bool IsStatusObserverModuleEnabled => Configuration.StatusObserver.ShowWindow;

    private bool ShouldSuppressCombatModuleWork => DalamudApi.ClientState.IsPvP;

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
        tankInvulnerabilityTtsService = new TankInvulnerabilityTtsService(Configuration);
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
        isDisposing = true;
        LogHelper.Debug("插件", "开始卸载 DPS统计。 ");
        SafeShutdownStep("取消 Framework 更新", () => DalamudApi.Framework.Update -= OnFrameworkUpdate);
        SafeShutdownStep("取消 UI 绘制", () => pluginInterface.UiBuilder.Draw -= ui.Draw);
        SafeShutdownStep("取消插件列表入口", () =>
        {
            pluginInterface.UiBuilder.OpenMainUi -= ui.OpenSettingsWindow;
            pluginInterface.UiBuilder.OpenConfigUi -= ui.OpenSettingsWindow;
        });
        SafeShutdownStep("取消聊天事件", UnregisterChatHandlers);
        SafeShutdownStep("取消命令", UnregisterCommands);
        SafeShutdownStep("关闭 UI", ui.Dispose);
        SafeShutdownStep("释放 Hook", DisposeHooks);
        SafeShutdownStep("保存配置", Configuration.Save);
        LogHelper.Debug("插件", "DPS统计 卸载完成。 ");
    }


    private void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            if (isDisposing)
                return;

            var perfEnabled = Configuration.EnableEnhancedLog;
            var perfStart = perfEnabled ? Stopwatch.GetTimestamp() : 0;
            var perfLast = perfStart;
            List<string>? perfParts = perfEnabled ? new List<string>(8) : null;
            _ = framework;
            var territoryId = DalamudApi.GetTerritoryTypeId();
            var zoneName = GetPlaceName(territoryId);
            var runtimeAreaKind = ResolveRuntimeAreaKind(territoryId);
            Configuration.CurrentAreaKind = runtimeAreaKind;
            MarkFrameworkPerfSegment("zone", ref perfLast, perfParts);
            var inCombat = DalamudApi.Conditions.Any(ConditionFlag.InCombat);
            var inDutyRecorderPlayback = DalamudApi.Conditions.Any(ConditionFlag.DutyRecorderPlayback);
            var suppressCombatModuleWork = ShouldSuppressCombatModuleWork;
            var replayStatsActive = Configuration.ReplayStatsMode && inDutyRecorderPlayback;
            var statsActive = !suppressCombatModuleWork && IsStatsModuleEnabled && (inCombat || replayStatsActive);
            var timelineAreaEnabled = IsTimelineRuntimeAreaEnabled(runtimeAreaKind);
            var timelineModuleEnabled = IsTimelineModuleEnabled;
            var timelineActive = !suppressCombatModuleWork && timelineAreaEnabled && timelineModuleEnabled && (inCombat || inDutyRecorderPlayback && statsService.HasActiveEncounter);
            var nowUtc = DateTime.UtcNow;
            var forceReplayOutOfCombat = replayStatsActive && !inCombat && statsService.IsEncounterIdle(nowUtc, TimeSpan.FromSeconds(60));
            var statsUpdateIntervalMs = Configuration.GetEffectiveStatsUpdateIntervalMs();
            var shouldUpdateStats = nowUtc - lastStatsUpdateUtc >= TimeSpan.FromMilliseconds(statsUpdateIntervalMs);
            var timelineUpdateIntervalMs = Configuration.GetEffectiveTimelineUpdateIntervalMs(timelineActive);
            var timelineUpdateInterval = TimeSpan.FromMilliseconds(timelineUpdateIntervalMs);
            var shouldUpdateTimeline = timelineModuleEnabled && nowUtc - lastTimelineUpdateUtc >= timelineUpdateInterval;
            var ranHeavyWork = false;
            RefreshPartyMonitorAfterDutyTransition(runtimeAreaKind, nowUtc);

            if (shouldUpdateStats)
            {
                lastStatsUpdateUtc = nowUtc;
                if (statsActive || !suppressCombatModuleWork && IsStatsModuleEnabled && !inDutyRecorderPlayback && statsService.HasActiveEncounter)
                {
                    statsService.WarmOwnerCacheFromObjectTable();
                    statsService.Update(zoneName, statsActive, forceReplayOutOfCombat);
                    ResetPartyMonitorCooldownsAfterPartyWipe(nowUtc);
                    MarkFrameworkPerfSegment("stats", ref perfLast, perfParts);
                    ranHeavyWork = true;
                }
            }

            var shouldPollBattleCharas = ShouldRunHeavyTimelineSync(runtimeAreaKind)
                && !ranHeavyWork
                && timelineActive
                && nowUtc - lastBattleCharaPollUtc >= GetBattleCharaPollInterval(runtimeAreaKind);
            if (shouldPollBattleCharas)
            {
                var battleCharas = DalamudApi.ObjectTable.OfType<IBattleChara>().ToArray();
                lastBattleCharaPollUtc = nowUtc;
                statsService.PollCombatTimelineHostileCasts(nowUtc, statsActive, battleCharas);
                timelineService.PollStartsUsingCasts(nowUtc, timelineActive, battleCharas);
                MarkFrameworkPerfSegment("casts", ref perfLast, perfParts);
                ranHeavyWork = true;
            }
            else if (!timelineActive || !ShouldRunHeavyTimelineSync(runtimeAreaKind))
            {
                timelineService.PollStartsUsingCasts(nowUtc, false, Array.Empty<IBattleChara>());
            }

            if (!suppressCombatModuleWork && IsPartyMonitorModuleEnabled)
            {
                monitorService.Update();
                MarkFrameworkPerfSegment("monitor", ref perfLast, perfParts);
            }

            if (!ranHeavyWork && shouldUpdateTimeline)
            {
                lastTimelineUpdateUtc = nowUtc;
                if (timelineAreaEnabled)
                    timelineService.Update(timelineActive, territoryId, zoneName);
                else
                    timelineService.DisableOutsideDuty(territoryId, zoneName);
                MarkFrameworkPerfSegment("timeline", ref perfLast, perfParts);
                ranHeavyWork = true;
            }

            if (!ranHeavyWork && nowUtc - lastRawPacketHookStateUpdateUtc >= TimeSpan.FromMilliseconds(500))
            {
                lastRawPacketHookStateUpdateUtc = nowUtc;
                UpdateRawGamePacketHookState();
                MarkFrameworkPerfSegment("rawHook", ref perfLast, perfParts);
            }

            if (perfEnabled)
                LogFrameworkPerfIfSlow(perfStart, perfParts!, statsActive, timelineActive);
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

    private static void SafeShutdownStep(string label, System.Action action)
    {
        try
        {
            LogHelper.Debug("插件", $"卸载步骤开始：{label}。");
            action();
            LogHelper.Debug("插件", $"卸载步骤完成：{label}。");
        }
        catch (Exception ex)
        {
            LogHelper.Warning("插件", ex, $"卸载步骤失败：{label}。已继续卸载。 ");
        }
    }

    private void ResetPartyMonitorCooldownsAfterPartyWipe(DateTime nowUtc)
    {
        var partyWipeVersion = statsService.PartyWipeFinalizedVersion;
        if (partyWipeVersion == observedPartyWipeFinalizedVersion)
            return;

        observedPartyWipeFinalizedVersion = partyWipeVersion;
        if (DalamudApi.ClientState.IsPvP)
            return;

        monitorService.ResetSkillCooldowns(nowUtc);
        LogHelper.Info("队友监控", "检测到团灭战斗结算，已重置队友技能冷却。");
    }

    private static TimeSpan GetBattleCharaPollInterval(RuntimeAreaKind runtimeAreaKind)
        => runtimeAreaKind == RuntimeAreaKind.Duty
            ? TimeSpan.FromMilliseconds(1000)
            : TimeSpan.FromMilliseconds(100);

    private static bool IsTimelineRuntimeAreaEnabled(RuntimeAreaKind runtimeAreaKind)
        => runtimeAreaKind == RuntimeAreaKind.Duty;

    private bool ShouldRunHeavyTimelineSync(RuntimeAreaKind runtimeAreaKind)
        => !Configuration.HighPerformanceMode
           && runtimeAreaKind != RuntimeAreaKind.Duty;

    private void RefreshPartyMonitorAfterDutyTransition(RuntimeAreaKind runtimeAreaKind, DateTime nowUtc)
    {
        var previousAreaKind = lastRuntimeAreaKind;
        var hadSnapshot = hasRuntimeAreaKindSnapshot;
        lastRuntimeAreaKind = runtimeAreaKind;
        hasRuntimeAreaKindSnapshot = true;

        if (!hadSnapshot)
            return;

        if (!IsPartyMonitorModuleEnabled || ShouldSuppressCombatModuleWork)
            return;

        if (previousAreaKind != RuntimeAreaKind.Duty && runtimeAreaKind == RuntimeAreaKind.Duty)
        {
            pendingPartyMonitorDutyEnterRefreshUntilUtc = nowUtc.AddSeconds(10);
            pendingPartyMonitorDutyEnterPartyCountChangedUtc = DateTime.MinValue;
            pendingPartyMonitorDutyEnterPartyCount = -1;
            LogHelper.Debug("队友监控", "检测到进入副本，等待队伍列表加载完成后刷新技能监控缓存。");
        }
        else if (previousAreaKind == RuntimeAreaKind.Duty && runtimeAreaKind != RuntimeAreaKind.Duty)
        {
            pendingPartyMonitorDutyEnterRefreshUntilUtc = DateTime.MinValue;
            pendingPartyMonitorDutyEnterPartyCountChangedUtc = DateTime.MinValue;
            pendingPartyMonitorDutyEnterPartyCount = -1;
            monitorService.RefreshOnce(nowUtc);
            LogHelper.Debug("队友监控", "检测到从副本返回非副本区域，已刷新一次技能监控缓存。");
            return;
        }

        if (runtimeAreaKind != RuntimeAreaKind.Duty || pendingPartyMonitorDutyEnterRefreshUntilUtc == DateTime.MinValue)
            return;

        if (nowUtc > pendingPartyMonitorDutyEnterRefreshUntilUtc)
        {
            pendingPartyMonitorDutyEnterRefreshUntilUtc = DateTime.MinValue;
            pendingPartyMonitorDutyEnterPartyCountChangedUtc = DateTime.MinValue;
            pendingPartyMonitorDutyEnterPartyCount = -1;
            LogHelper.Debug("队友监控", "进入副本后等待队伍列表加载超时，已跳过本次自动刷新。");
            return;
        }

        if (!IsPartyMonitorDutyPartyReady(nowUtc))
            return;

        pendingPartyMonitorDutyEnterRefreshUntilUtc = DateTime.MinValue;
        pendingPartyMonitorDutyEnterPartyCountChangedUtc = DateTime.MinValue;
        pendingPartyMonitorDutyEnterPartyCount = -1;
        monitorService.RefreshOnce(nowUtc);
        LogHelper.Debug("队友监控", "进入副本后队伍列表已加载完成，已刷新一次技能监控缓存。");
    }

    private bool IsPartyMonitorDutyPartyReady(DateTime nowUtc)
    {
        if (!DalamudApi.TryGetLocalPlayerInfo(out _, out _, out _, out _))
            return false;

        var partyCount = 0;
        try
        {
            partyCount = DalamudApi.PartyList.Count();
        }
        catch
        {
            return false;
        }

        if (partyCount <= 0)
            return false;

        if (partyCount != pendingPartyMonitorDutyEnterPartyCount)
        {
            pendingPartyMonitorDutyEnterPartyCount = partyCount;
            pendingPartyMonitorDutyEnterPartyCountChangedUtc = nowUtc;
            return false;
        }

        if (pendingPartyMonitorDutyEnterPartyCountChangedUtc == DateTime.MinValue)
        {
            pendingPartyMonitorDutyEnterPartyCountChangedUtc = nowUtc;
            return false;
        }

        return nowUtc - pendingPartyMonitorDutyEnterPartyCountChangedUtc >= TimeSpan.FromMilliseconds(750);
    }

    private void MarkFrameworkPerfSegment(string name, ref long lastTimestamp, List<string>? parts)
    {
        if (parts == null)
            return;

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
