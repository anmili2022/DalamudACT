using System;
using System.Globalization;
using Dalamud.Game.ClientState.Buddy;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
    private static uint ResolveBuddyActorId(IBuddyMember buddy)
        => ResolveBuddyActorId(buddy, buddy.GameObject);

    private static uint ResolveBuddyActorId(IBuddyMember buddy, IGameObject? gameObject)
    {
        return GetBuddyIdentity(buddy, gameObject).ResolveActorId();
    }

    private static ulong TryGetGameObjectId(IGameObject? gameObject)
    {
        if (gameObject == null)
            return 0UL;

        try
        {
            return Convert.ToUInt64(gameObject.GameObjectId, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0UL;
        }
    }

    private static ActorIdentity GetGameObjectIdentity(IGameObject? gameObject)
    {
        var gameObjectId = TryGetGameObjectId(gameObject);
        // ActionEffectHandler 这条统计链路里拿到的是低 32 位 ID。
        // 因此内部 actorId 口径继续保留 uint，但对象回查优先使用完整的 ulong GameObjectId。
        var actorId = gameObjectId == 0 ? 0 : unchecked((uint)(gameObjectId & uint.MaxValue));
        // 某些运行时/对象实现会额外暴露 ObjectId，但接口层不一定直接声明。
        // 单人解限、NPC 队友、信赖等场景里，ActionEffect 的 sourceId / targetId
        // 可能更接近这个 ObjectId，而不是低 32 位 GameObjectId。
        var objectId = TryGetPropertyActorId(gameObject, "ObjectId");
        var entityId = gameObject?.EntityId ?? 0;
        return new ActorIdentity(gameObjectId, actorId, objectId != 0 ? objectId : entityId, entityId);
    }

    private static ActorIdentity GetPartyMemberIdentity(Dalamud.Game.ClientState.Party.IPartyMember member, IGameObject? gameObject)
    {
        var gameObjectIdentity = GetGameObjectIdentity(gameObject);
        var objectId = member.ObjectId;
        var entityId = TryGetPropertyActorId(member, "EntityId");
        return new ActorIdentity(
            gameObjectIdentity.GameObjectId,
            gameObjectIdentity.ActorId,
            objectId,
            entityId != 0 ? entityId : gameObjectIdentity.EntityId);
    }

    private static ActorIdentity GetBuddyIdentity(IBuddyMember buddy, IGameObject? gameObject)
    {
        var gameObjectIdentity = GetGameObjectIdentity(gameObject);
        var objectId = buddy.ObjectId;
        var entityId = TryGetPropertyActorId(buddy, "EntityId");
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

    private static uint TryGetPropertyActorId(object? instance, string propertyName)
    {
        if (instance == null)
            return 0;

        try
        {
            var property = instance.GetType().GetProperty(propertyName);
            return TryConvertActorId(property?.GetValue(instance));
        }
        catch
        {
            return 0;
        }
    }

    private static uint TryConvertActorId(object? rawValue)
    {
        if (rawValue == null)
            return 0;

        try
        {
            return unchecked((uint)(Convert.ToUInt64(rawValue, CultureInfo.InvariantCulture) & uint.MaxValue));
        }
        catch
        {
            return 0;
        }
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
