using System;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace DalamudACT;

public sealed partial class ACT
{
    private static long DecodeAmount(ActionEffectHandler.Effect effect)
        => (uint)effect.Value | ((uint)effect.Param3 << 16);

    private static bool IsCritical(ActionEffectHandler.Effect effect)
        => (effect.Param0 & 0x20) != 0;

    private static bool IsDirectHit(ActionEffectHandler.Effect effect)
        => (effect.Param0 & 0x40) != 0;

    private static bool IsUsableActorId(uint actorId)
        => ActorIdentityAccessor.IsUsableActorId(actorId);

    private static uint NormalizeEventActorId(uint actorId)
        => ActorIdentityAccessor.NormalizeActorId(actorId);

    private static uint NormalizeEventActorId(GameObjectId actorId)
    {
        var low32 = unchecked((uint)(actorId & uint.MaxValue));
        return NormalizeEventActorId(low32);
    }

    private static uint TryGetActorIdFromGameObject(IGameObject? gameObject)
    {
        if (gameObject == null)
            return 0;

        return ActorIdentityAccessor.GetBestActorId(gameObject);
    }

    private static bool MatchesEventActorId(IGameObject? gameObject, uint actorId)
    {
        if (gameObject == null || !IsUsableActorId(actorId))
            return false;

        return ActorIdentityAccessor.MatchesActorId(gameObject, actorId);
    }

    private static IGameObject? ResolveEventActorObject(uint actorId, nint characterAddress)
    {
        var normalizedActorId = NormalizeEventActorId(actorId);
        if (characterAddress != nint.Zero)
        {
            var objectFromAddress = DalamudApi.ObjectTable.CreateObjectReference(characterAddress);
            // ActionEffect 里的 character 指针在部分 NPC/副本事件中不一定可靠。
            // 只有它和事件 sourceId 口径能对上时才使用；否则回退到 sourceId 查表。
            // 否则会把 Boss/目标对象误当成 NPC 队友来源，历史里出现“佐拉加=friendlyNpc”。
            if (objectFromAddress != null
                && (normalizedActorId == 0 || MatchesEventActorId(objectFromAddress, normalizedActorId)))
            {
                return objectFromAddress;
            }
        }

        if (normalizedActorId == 0)
            return null;

        var entityMatch = DalamudApi.ObjectTable.SearchByEntityId(normalizedActorId);
        if (entityMatch != null)
            return entityMatch;

        foreach (var obj in DalamudApi.ObjectTable)
        {
            if (MatchesEventActorId(obj, normalizedActorId))
                return obj;
        }

        return null;
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

    private bool TryObserveFriendlyCombatantSource(uint preferredActorId, IGameObject? gameObject, out uint actorId)
    {
        if (statsService.ObserveFriendlyCombatantSourceFromGameObject(gameObject, out actorId))
            return true;

        return TryObserveFriendlyCombatant(preferredActorId, gameObject, out actorId);
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
            var sourceObject = ResolveEventActorObject(sourceId, sourceCharacterAddress);
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
        if (!LogHelper.IsDebugEnabled(DebugLogModule.DamageStats))
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
        var sourceObjectId = ActorIdentityAccessor.GetReflectedActorId(sourceObject, "ObjectId");
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

}
