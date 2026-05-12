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
    private bool abilityEffectFaulted;
    private DateTime lastUntrackedCombatDebugAtUtc;
    private int suppressedUntrackedCombatDebugCount;

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
                LogHelper.Error("插件", ex, "在 Framework 更新期间刷新本地 DPS 统计失败。");
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
                LogHelper.Info("插件", "已安装 ActionEffect Hook，用于实时战斗统计。");
            }
            catch (Exception ex)
            {
                LogHelper.Error("插件", ex, "安装 ActionEffect Hook 失败。插件会继续加载，但实时 DPS 数据将不可用。");
            }
        }

        LogHelper.Warning("插件", "当前 Dalamud 运行时处于兼容模式，ActorControlSelf 与 Cast Hook 已禁用。");
    }

    private void DisposeHooks()
    {
        try
        {
            receiveAbilityHook?.Disable();
            if (receiveAbilityHook != null)
                LogHelper.Debug("插件", "已关闭 ActionEffect Hook。");
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

    private static bool IsDirectHit(ActionEffectHandler.Effect effect)
        => (effect.Param0 & 0x40) != 0;

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

        var reflectedObjectId = TryGetReflectedActorId(gameObject, "ObjectId");
        if (IsUsableActorId(reflectedObjectId))
            return reflectedObjectId;

        if (IsUsableActorId(gameObject.EntityId))
            return gameObject.EntityId;

        return 0;
    }

    private static uint TryGetReflectedActorId(object? instance, string propertyName)
    {
        if (instance == null)
            return 0;

        try
        {
            var property = instance.GetType().GetProperty(propertyName);
            var rawValue = property?.GetValue(instance);
            return rawValue == null ? 0 : unchecked((uint)(Convert.ToUInt64(rawValue) & uint.MaxValue));
        }
        catch
        {
            return 0;
        }
    }

    private bool TryObserveFriendlyCombatant(uint preferredActorId, IGameObject? gameObject, out uint actorId)
    {
        if (statsService.ObserveFriendlyCombatantFromGameObject(gameObject, out actorId))
            return true;

        actorId = preferredActorId;
        if (actorId == 0)
            actorId = TryGetActorIdFromGameObject(gameObject);

        if (actorId == 0)
            return false;

        var name = gameObject?.Name.TextValue?.Trim();
        if (!statsService.ObserveFriendlyCombatantIdentity(actorId, name))
            return false;

        return true;
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

            if (statsService.TryResolveTrackedSourceFromGameObject(sourceObject, nowUtc, out var resolvedActorId))
            {
                canResolveTrackedSource = true;
                return resolvedActorId;
            }

            if (TryObserveFriendlyCombatant(sourceObjectActorId != 0 ? sourceObjectActorId : normalizedSourceId, sourceObject, out resolvedActorId))
            {
                canResolveTrackedSource = true;
                return resolvedActorId;
            }
        }

        if (normalizedSourceId != 0)
        {
            var sourceTableObject = DalamudApi.ObjectTable.SearchByEntityId(normalizedSourceId);
            if (sourceTableObject != null)
            {
                var sourceTableActorId = TryGetActorIdFromGameObject(sourceTableObject);
                if (sourceTableActorId != 0 && statsService.CanResolveTrackedSource(sourceTableActorId, nowUtc))
                {
                    canResolveTrackedSource = true;
                    return sourceTableActorId;
                }

                if (statsService.TryResolveTrackedSourceFromGameObject(sourceTableObject, nowUtc, out var resolvedActorId))
                {
                    canResolveTrackedSource = true;
                    return resolvedActorId;
                }

                if (TryObserveFriendlyCombatant(normalizedSourceId, sourceTableObject, out resolvedActorId))
                {
                    canResolveTrackedSource = true;
                    return resolvedActorId;
                }
            }
        }

        canResolveTrackedSource = false;
        return normalizedSourceId;
    }

    private void DebugLogUntrackedCombatEvent(
        uint sourceId,
        nint sourceCharacterAddress,
        uint firstTargetId,
        bool sourceCanResolveToTrackedActor,
        bool anyTargetTracked,
        string actionName)
    {
        if (!LogHelper.EnableDebugLog)
            return;

        var nowUtc = DateTime.UtcNow;
        if (nowUtc - lastUntrackedCombatDebugAtUtc < TimeSpan.FromSeconds(2))
        {
            suppressedUntrackedCombatDebugCount++;
            return;
        }

        lastUntrackedCombatDebugAtUtc = nowUtc;

        var sourceObject = sourceCharacterAddress == nint.Zero
            ? null
            : DalamudApi.ObjectTable.CreateObjectReference(sourceCharacterAddress);
        var sourceObjectGameObjectId = sourceObject?.GameObjectId.ToString() ?? "0";
        var sourceObjectId = TryGetReflectedActorId(sourceObject, "ObjectId");
        var sourceEntityId = sourceObject?.EntityId ?? 0;
        var sourceObjectName = sourceObject?.Name.TextValue?.Trim() ?? string.Empty;
        var targetObject = firstTargetId == 0 ? null : DalamudApi.ObjectTable.SearchByEntityId(firstTargetId);
        var targetObjectName = targetObject?.Name.TextValue?.Trim() ?? string.Empty;
        var localPlayerObjectId = DalamudApi.GetLocalPlayerObjectId();
        var localPlayerEntityId = DalamudApi.GetLocalPlayerEntityId();
        var localPlayerGameObjectId = DalamudApi.GetLocalPlayerGameObjectId();
        var suppressedCount = suppressedUntrackedCombatDebugCount;
        suppressedUntrackedCombatDebugCount = 0;

        LogHelper.DebugRecent(
            "插件",
            $"战斗事件未命中可跟踪对象：技能={actionName}，sourceId=0x{sourceId:X8}，firstTargetId=0x{firstTargetId:X8}，sourceTracked={sourceCanResolveToTrackedActor}，targetTracked={anyTargetTracked}，sourceCharacter=0x{sourceCharacterAddress.ToInt64():X}，sourceObjectName={sourceObjectName}，targetObjectName={targetObjectName}，sourceObjectGameObjectId={sourceObjectGameObjectId}，sourceObjectId=0x{sourceObjectId:X8}，sourceEntityId=0x{sourceEntityId:X8}，localPlayerGameObjectId=0x{localPlayerGameObjectId:X16}，localPlayerObjectId=0x{localPlayerObjectId:X8}，localPlayerEntityId=0x{localPlayerEntityId:X8}，本次合并调试日志={suppressedCount}。");
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

        var numTargets = effectHeader->NumTargets;
        if (numTargets == 0)
            return;

        var actionId = effectHeader->SpellId != 0 ? (uint)effectHeader->SpellId : effectHeader->ActionId;
        try
        {
            HandleAbility(effectHeader, effectArray, effectTrail, sourceId, sourceCharacter);
            abilityEffectFaulted = false;
        }
        catch (Exception ex)
        {
            if (!abilityEffectFaulted)
            {
                abilityEffectFaulted = true;
                LogHelper.Error(
                    "插件",
                    ex,
                    $"处理 ActionEffect 事件失败：sourceId=0x{sourceId:X8}，actionId=0x{actionId:X8}，targets={numTargets}。");
            }
        }
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
        var isKnownPlayerDotAction = PlayerDotCatalog.IsKnownPlayerDotAction(actionId);

        var hasTrackedParticipant = sourceCanResolveToTrackedActor;
        var hasRelevantTrackedEffect = false;
        var anyTargetTracked = false;
        uint firstTargetId = 0;

        for (var targetIndex = 0; targetIndex < header->NumTargets; targetIndex++)
        {
            var targetId = NormalizeEventActorId(targets[targetIndex]);
            if (targetId == 0)
                continue;

            var resolvedTargetActorId = targetId;
            if (firstTargetId == 0)
                firstTargetId = targetId;

            var targetIsTrackedActor = statsService.IsTrackedActor(resolvedTargetActorId);
            if (!targetIsTrackedActor)
            {
                var targetObject = DalamudApi.ObjectTable.SearchByEntityId(targetId);
                if (TryObserveFriendlyCombatant(targetId, targetObject, out var observedTargetActorId))
                {
                    resolvedTargetActorId = observedTargetActorId != 0 ? observedTargetActorId : targetId;
                    targetIsTrackedActor = statsService.IsTrackedActor(resolvedTargetActorId);
                }
            }

            anyTargetTracked |= targetIsTrackedActor;
            hasTrackedParticipant |= targetIsTrackedActor;

            if (isKnownPlayerDotAction && sourceCanResolveToTrackedActor && !targetIsTrackedActor)
                statsService.ObservePotentialPlayerDotApplication(sourceActorId, resolvedTargetActorId, actionId, actionName, nowUtc);

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
                        var amount = DecodeAmount(effect);
                        if (amount <= 0)
                            break;

                        if (sourceCanResolveToTrackedActor
                            && !targetIsTrackedActor)
                        {
                            statsService.ObservePotentialPlayerHostileActionSample(sourceActorId, resolvedTargetActorId, actionId, actionName, amount, IsCritical(effect), IsDirectHit(effect), nowUtc);
                        }

                        if (sourceCanResolveToTrackedActor || targetIsTrackedActor)
                        {
                            hasRelevantTrackedEffect = true;
                            statsService.RecordDamage(sourceActorId, resolvedTargetActorId, actionId, actionName, amount, IsCritical(effect), IsDirectHit(effect), nowUtc, zoneName);
                            break;
                        }

                        break;
                    }
                    case LocalActionEffectType.Heal:
                    {
                        var amount = DecodeAmount(effect);
                        if (amount > 0 && (sourceCanResolveToTrackedActor || targetIsTrackedActor))
                        {
                            hasRelevantTrackedEffect = true;
                            statsService.RecordHeal(sourceActorId, resolvedTargetActorId, actionId, actionName, amount, IsCritical(effect), nowUtc, zoneName);
                        }
                        break;
                    }
                    case LocalActionEffectType.Miss:
                        if (sourceCanResolveToTrackedActor || targetIsTrackedActor)
                        {
                            hasRelevantTrackedEffect = true;
                            statsService.RecordFailure(sourceActorId, resolvedTargetActorId, actionId, actionName, isMiss: true, nowUtc, zoneName);
                        }
                        break;
                    case LocalActionEffectType.FullResist:
                    case LocalActionEffectType.Invulnerable:
                    case LocalActionEffectType.PartialInvulnerable:
                        if (sourceCanResolveToTrackedActor || targetIsTrackedActor)
                        {
                            hasRelevantTrackedEffect = true;
                            statsService.RecordFailure(sourceActorId, resolvedTargetActorId, actionId, actionName, isMiss: false, nowUtc, zoneName);
                        }
                        break;
                }
            }
        }

        if (hasTrackedParticipant && (hasRelevantTrackedEffect || inCombatNow))
            statsService.RecordEncounterActivity(zoneName, nowUtc);
        else if (inCombatNow)
            DebugLogUntrackedCombatEvent(sourceId, sourceCharacterAddress, firstTargetId, sourceCanResolveToTrackedActor, anyTargetTracked, actionName);
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
