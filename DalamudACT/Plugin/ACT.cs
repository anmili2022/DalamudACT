using System;
using System.Collections.Generic;
using Dalamud.Game.Command;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
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
    private int suppressedUntrackedCombatDebugCount;

    private Hook<ReceiveAbilityDelegate>? receiveAbilityHook;
    private Hook<ActorControlDelegate>? actorControlHook;

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
        pluginInterface.UiBuilder.OpenMainUi += ui.OpenMainWindow;
        pluginInterface.UiBuilder.OpenConfigUi += ui.ToggleSettingsWindow;
        DalamudApi.Framework.Update += OnFrameworkUpdate;
        LogHelper.PrintWithModule("插件", "加载", $"已加载 DPS统计 v{PluginVersion}。");
    }

    public void Dispose()
    {
        DalamudApi.Framework.Update -= OnFrameworkUpdate;
        pluginInterface.UiBuilder.Draw -= ui.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= ui.OpenMainWindow;
        pluginInterface.UiBuilder.OpenConfigUi -= ui.ToggleSettingsWindow;
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
            _ = framework;
            statsService.WarmOwnerCacheFromObjectTable();
            var zoneName = GetPlaceName();
            var inCombat = DalamudApi.Conditions.Any(ConditionFlag.InCombat);
            statsService.Update(zoneName, inCombat);
            statsService.PollCombatTimelineHostileCasts(DateTime.UtcNow, inCombat);
            monitorService.Update();
            timelineService.Update(inCombat, DalamudApi.GetTerritoryTypeId(), zoneName);
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


    private string GetPlaceName()
    {
        var territoryId = DalamudApi.GetTerritoryTypeId();
        if (territoryId == 0 || !territorySheet.TryGetRow(territoryId, out var territory))
            return "未知区域";

        try
        {
            if (!territory.ContentFinderCondition.Value.Name.IsEmpty)
                return territory.ContentFinderCondition.Value.Name.ExtractText();

            if (!territory.PlaceName.Value.Name.IsEmpty)
                return territory.PlaceName.Value.Name.ExtractText();

            if (!territory.PlaceNameRegion.Value.Name.IsEmpty)
                return territory.PlaceNameRegion.Value.Name.ExtractText();

            if (!territory.PlaceNameZone.Value.Name.IsEmpty)
                return territory.PlaceNameZone.Value.Name.ExtractText();
        }
        catch
        {
            // Fall through to the generic zone label if runtime data shape changes.
        }

        return "未知区域";
    }

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
