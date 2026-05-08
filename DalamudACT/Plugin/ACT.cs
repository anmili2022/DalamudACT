using System;
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
public sealed class ACT : IDalamudPlugin
{
    private const uint InvalidActorId = 0xE0000000;

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly LocalStatsService statsService;
    private readonly PluginUI ui;
    private readonly ExcelSheet<TerritoryType> territorySheet;
    private readonly ExcelSheet<Action> actionSheet;
    private bool frameworkUpdateFaulted;

    private Hook<ReceiveAbilityDelegate>? receiveAbilityHook;

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
        InstallHooks();

        ui = new PluginUI(Configuration, statsService);
        pluginInterface.UiBuilder.Draw += ui.Draw;
        pluginInterface.UiBuilder.OpenMainUi += ui.OpenMainWindow;
        pluginInterface.UiBuilder.OpenConfigUi += ui.ToggleSettingsWindow;
        DalamudApi.Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        DalamudApi.Framework.Update -= OnFrameworkUpdate;
        pluginInterface.UiBuilder.Draw -= ui.Draw;
        pluginInterface.UiBuilder.OpenMainUi -= ui.OpenMainWindow;
        pluginInterface.UiBuilder.OpenConfigUi -= ui.ToggleSettingsWindow;
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
            statsService.Update(GetPlaceName(), DalamudApi.Conditions.Any(ConditionFlag.InCombat));
            frameworkUpdateFaulted = false;
        }
        catch (Exception ex)
        {
            if (!frameworkUpdateFaulted)
            {
                frameworkUpdateFaulted = true;
                DalamudApi.Log.Error(ex, "Failed to refresh local DPS statistics during framework update.");
            }
        }
    }

    private void InstallHooks()
    {
        unsafe
        {
            try
            {
                receiveAbilityHook = DalamudApi.Interop.HookFromSignature<ReceiveAbilityDelegate>(
                    ActionEffectHandler.Addresses.Receive.String,
                    ReceiveAbilityEffect);
                receiveAbilityHook.Enable();
            }
            catch (Exception ex)
            {
                DalamudApi.Log.Error(ex, "Failed to install the ActionEffect hook. The plugin will stay loaded, but live DPS data will be unavailable.");
            }
        }

        DalamudApi.Log.Warning("ActorControlSelf and cast hooks are disabled in compatibility mode on this Dalamud runtime.");
    }

    private void DisposeHooks()
    {
        try
        {
            receiveAbilityHook?.Disable();
        }
        catch
        {
            // Ignore hook shutdown failures while disposing.
        }

        receiveAbilityHook?.Dispose();
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

    private static long DecodeAmount(ActionEffectHandler.Effect effect)
        => (uint)effect.Value | ((uint)effect.Param3 << 16);

    private static bool IsCritical(ActionEffectHandler.Effect effect)
        => (effect.Param0 & 0x20) != 0;

    private static bool IsUsableActorId(uint actorId)
        => actorId is not 0 and not InvalidActorId;

    private static uint NormalizeEventActorId(uint actorId)
        => IsUsableActorId(actorId) ? actorId : 0;

    private static uint NormalizeEventActorId(GameObjectId actorId)
    {
        var low32 = unchecked((uint)(actorId & uint.MaxValue));
        return NormalizeEventActorId(low32);
    }

    private static uint TryGetActorIdFromGameObject(IGameObject? gameObject)
    {
        if (gameObject == null)
            return 0;

        var actorId = unchecked((uint)(gameObject.GameObjectId & uint.MaxValue));
        if (IsUsableActorId(actorId))
            return actorId;

        if (IsUsableActorId(gameObject.EntityId))
            return gameObject.EntityId;

        return 0;
    }

    private uint ResolveTrackedSourceActorId(uint sourceId, nint sourceCharacterAddress, DateTime nowUtc, out bool canResolveTrackedSource)
    {
        var normalizedSourceId = NormalizeEventActorId(sourceId);
        if (normalizedSourceId != 0 && statsService.CanResolveTrackedSource(normalizedSourceId, nowUtc))
        {
            canResolveTrackedSource = true;
            return normalizedSourceId;
        }

        if (sourceCharacterAddress != nint.Zero)
        {
            var sourceObject = DalamudApi.ObjectTable.CreateObjectReference(sourceCharacterAddress);
            var sourceObjectActorId = TryGetActorIdFromGameObject(sourceObject);
            if (sourceObjectActorId != 0 && statsService.CanResolveTrackedSource(sourceObjectActorId, nowUtc))
            {
                canResolveTrackedSource = true;
                return sourceObjectActorId;
            }
        }

        canResolveTrackedSource = false;
        return normalizedSourceId;
    }

    private unsafe void ReceiveAbilityEffect(
        uint sourceId,
        nint sourceCharacter,
        nint pos,
        ActionEffectHandler.Header* effectHeader,
        ActionEffectHandler.TargetEffects* effectArray,
        GameObjectId* effectTrail)
    {
        receiveAbilityHook!.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTrail);

        if (effectHeader->NumTargets > 0)
            HandleAbility(effectHeader, effectArray, effectTrail, sourceId, sourceCharacter);
    }

    private unsafe void HandleAbility(
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targets,
        uint sourceId,
        nint sourceCharacterAddress)
    {
        var nowUtc = DateTime.UtcNow;
        var zoneName = GetPlaceName();
        var actionId = header->SpellId != 0 ? (uint)header->SpellId : header->ActionId;
        var actionName = GetActionName(actionId);
        var inCombatNow = DalamudApi.Conditions.Any(ConditionFlag.InCombat);
        var sourceActorId = ResolveTrackedSourceActorId(sourceId, sourceCharacterAddress, nowUtc, out var sourceCanResolveToTrackedActor);

        var hasTrackedParticipant = sourceCanResolveToTrackedActor;
        var hasRelevantTrackedEffect = false;

        for (var targetIndex = 0; targetIndex < header->NumTargets; targetIndex++)
        {
            var targetId = NormalizeEventActorId(targets[targetIndex]);
            if (targetId == 0)
                continue;

            var targetIsTrackedActor = statsService.IsTrackedActor(targetId);
            hasTrackedParticipant |= targetIsTrackedActor;

            if (!sourceCanResolveToTrackedActor && !targetIsTrackedActor)
                continue;

            for (var effectIndex = 0; effectIndex < 8; effectIndex++)
            {
                ref var effect = ref effects[targetIndex].Effects[effectIndex];
                var effectType = (LocalActionEffectType)effect.Type;

                switch (effectType)
                {
                    case LocalActionEffectType.Damage:
                    case LocalActionEffectType.BlockedDamage:
                    case LocalActionEffectType.ParriedDamage:
                    {
                        hasRelevantTrackedEffect = true;
                        var amount = DecodeAmount(effect);
                        if (amount > 0)
                            statsService.RecordDamage(sourceActorId, targetId, actionName, amount, IsCritical(effect), nowUtc, zoneName);
                        break;
                    }
                    case LocalActionEffectType.Heal:
                    {
                        hasRelevantTrackedEffect = true;
                        var amount = DecodeAmount(effect);
                        if (amount > 0)
                            statsService.RecordHeal(sourceActorId, targetId, amount, IsCritical(effect), nowUtc, zoneName);
                        break;
                    }
                    case LocalActionEffectType.Miss:
                        hasRelevantTrackedEffect = true;
                        statsService.RecordFailure(sourceActorId, isMiss: true, nowUtc, zoneName);
                        break;
                    case LocalActionEffectType.FullResist:
                    case LocalActionEffectType.Invulnerable:
                    case LocalActionEffectType.PartialInvulnerable:
                        hasRelevantTrackedEffect = true;
                        statsService.RecordFailure(sourceActorId, isMiss: false, nowUtc, zoneName);
                        break;
                }
            }
        }

        if (hasTrackedParticipant && (hasRelevantTrackedEffect || inCombatNow))
            statsService.RecordEncounterActivity(zoneName, nowUtc);
    }

    private unsafe delegate void ReceiveAbilityDelegate(
        uint sourceId,
        nint sourceCharacter,
        nint pos,
        ActionEffectHandler.Header* effectHeader,
        ActionEffectHandler.TargetEffects* effectArray,
        GameObjectId* effectTrail);

    private enum LocalActionEffectType : byte
    {
        Miss = 1,
        FullResist = 2,
        Damage = 3,
        Heal = 4,
        BlockedDamage = 5,
        ParriedDamage = 6,
        Invulnerable = 7,
        PartialInvulnerable = 74,
    }
}
