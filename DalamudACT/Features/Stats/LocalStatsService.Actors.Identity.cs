using System;
using Dalamud.Game.ClientState.Buddy;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
    private static ulong TryGetGameObjectId(IGameObject? gameObject)
        => ActorIdentityAccessor.GetGameObjectId(gameObject);

    private static uint ResolveBuddyActorId(IBuddyMember buddy)
        => ResolveBuddyActorId(buddy, buddy.GameObject);

    private static uint ResolveBuddyActorId(IBuddyMember buddy, IGameObject? gameObject)
    {
        return GetBuddyIdentity(buddy, gameObject).ResolveActorId();
    }

    private static ActorIdentity GetGameObjectIdentity(IGameObject? gameObject)
    {
        var gameObjectId = ActorIdentityAccessor.GetGameObjectId(gameObject);
        // ActionEffectHandler 这条统计链路里拿到的是低 32 位 ID。
        // 因此内部 actorId 口径继续保留 uint，但对象回查优先使用完整的 ulong GameObjectId。
        var actorId = ActorIdentityAccessor.NormalizeActorId(gameObjectId);
        // 某些运行时/对象实现会额外暴露 ObjectId，但接口层不一定直接声明。
        // 单人解限、NPC 队友、信赖等场景里，ActionEffect 的 sourceId / targetId
        // 可能更接近这个 ObjectId，而不是低 32 位 GameObjectId。
        var objectId = ActorIdentityAccessor.GetReflectedActorId(gameObject, "ObjectId");
        var entityId = ActorIdentityAccessor.NormalizeActorId(gameObject?.EntityId ?? 0);
        return new ActorIdentity(gameObjectId, actorId, objectId != 0 ? objectId : entityId, entityId);
    }

    private static uint TryConvertActorId(object? rawValue)
        => ActorIdentityAccessor.GetReflectedActorId(new RawActorIdBox(rawValue), nameof(RawActorIdBox.Value));

    private readonly record struct RawActorIdBox(object? Value);

    private static ActorIdentity GetPartyMemberIdentity(Dalamud.Game.ClientState.Party.IPartyMember member, IGameObject? gameObject)
    {
        var gameObjectIdentity = GetGameObjectIdentity(gameObject);
        var objectId = ActorIdentityAccessor.GetReflectedActorId(member, "EntityId");
        if (objectId == 0)
            objectId = ActorIdentityAccessor.GetReflectedActorId(member, "ObjectId");
        var entityId = ActorIdentityAccessor.GetReflectedActorId(member, "EntityId");
        return new ActorIdentity(
            gameObjectIdentity.GameObjectId,
            gameObjectIdentity.ActorId,
            objectId,
            entityId != 0 ? entityId : gameObjectIdentity.EntityId);
    }

    private static ActorIdentity GetBuddyIdentity(IBuddyMember buddy, IGameObject? gameObject)
    {
        var gameObjectIdentity = GetGameObjectIdentity(gameObject);
        var objectId = ActorIdentityAccessor.GetReflectedActorId(buddy, "EntityId");
        if (objectId == 0)
            objectId = ActorIdentityAccessor.GetReflectedActorId(buddy, "ObjectId");
        var entityId = ActorIdentityAccessor.GetReflectedActorId(buddy, "EntityId");
        return new ActorIdentity(
            gameObjectIdentity.GameObjectId,
            gameObjectIdentity.ActorId,
            objectId,
            entityId != 0 ? entityId : gameObjectIdentity.EntityId);
    }

    private static IBattleChara? TryResolveBattleCharaFromIdentity(ActorIdentity identity)
    {
        if (identity.GameObjectId != 0)
        {
            var objectTableMatch = DalamudApi.ObjectTable.SearchById(identity.GameObjectId) as IBattleChara;
            if (objectTableMatch != null)
                return objectTableMatch;
        }

        var actorId = identity.ResolveActorId();
        return actorId is 0 or InvalidActorId ? null : FindObjectByActorId(actorId) as IBattleChara;
    }

    private static ActorIdentity GetLocalPlayerIdentity()
    {
        var gameObjectId = DalamudApi.GetLocalPlayerGameObjectId();
        var actorId = gameObjectId == 0 ? 0 : unchecked((uint)(gameObjectId & uint.MaxValue));
        var objectId = DalamudApi.GetLocalPlayerObjectId();
        var entityId = DalamudApi.GetLocalPlayerEntityId();
        return new ActorIdentity(gameObjectId, actorId, objectId, entityId);
    }

    private static bool TryGetLocalPlayerTrackedActor(uint actorId, out TrackedActor actor)
    {
        var identity = GetLocalPlayerIdentity();
        if (!identity.MatchesActorId(actorId))
        {
            actor = default;
            return false;
        }

        var name = DalamudApi.GetLocalPlayerName();
        if (string.IsNullOrWhiteSpace(name))
        {
            actor = default;
            return false;
        }

        var jobId = DalamudApi.GetLocalPlayerClassJobId();
        var canonicalActorId = identity.ResolveActorId();
        actor = new TrackedActor(
            canonicalActorId is 0 or InvalidActorId ? actorId : canonicalActorId,
            name.Trim(),
            jobId,
            ResolveJobName(jobId),
            TrackedActorKind.Player);
        return true;
    }
}
