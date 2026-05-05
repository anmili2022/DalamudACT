using System;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using Action = Lumina.Excel.Sheets.Action;

namespace DalamudACT;

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

    private static bool LooksLikeBattleNpc(uint actorId)
        => actorId is > 0x40000000 and not InvalidActorId;

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
            HandleAbility(effectHeader, effectArray, effectTrail, sourceId);
    }

    private unsafe void HandleAbility(
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targets,
        uint sourceId)
    {
        var nowUtc = DateTime.UtcNow;
        var zoneName = GetPlaceName();
        var actionId = header->SpellId != 0 ? (uint)header->SpellId : header->ActionId;
        var actionName = GetActionName(actionId);
        var inCombatNow = DalamudApi.Conditions.Any(ConditionFlag.InCombat);

        var hasBattleNpc = LooksLikeBattleNpc(sourceId);
        var hasTrackedPlayer = statsService.IsTrackedActor(sourceId);
        var hasRelevantEffect = false;

        for (var targetIndex = 0; targetIndex < header->NumTargets; targetIndex++)
        {
            var targetId = (uint)(targets[targetIndex] & uint.MaxValue);
            if (targetId == 0)
                continue;

            hasBattleNpc |= LooksLikeBattleNpc(targetId);
            hasTrackedPlayer |= statsService.IsTrackedActor(targetId);

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
                        hasRelevantEffect = true;
                        var amount = DecodeAmount(effect);
                        if (amount > 0)
                            statsService.RecordDamage(sourceId, targetId, actionName, amount, IsCritical(effect), nowUtc, zoneName);
                        break;
                    }
                    case LocalActionEffectType.Heal:
                    {
                        hasRelevantEffect = true;
                        var amount = DecodeAmount(effect);
                        if (amount > 0)
                            statsService.RecordHeal(sourceId, targetId, amount, IsCritical(effect), nowUtc, zoneName);
                        break;
                    }
                    case LocalActionEffectType.Miss:
                        hasRelevantEffect = true;
                        statsService.RecordFailure(sourceId, isMiss: true, nowUtc, zoneName);
                        break;
                    case LocalActionEffectType.FullResist:
                    case LocalActionEffectType.Invulnerable:
                    case LocalActionEffectType.PartialInvulnerable:
                        hasRelevantEffect = true;
                        statsService.RecordFailure(sourceId, isMiss: false, nowUtc, zoneName);
                        break;
                }
            }
        }

        if (hasRelevantEffect || hasBattleNpc || (hasTrackedPlayer && inCombatNow))
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
