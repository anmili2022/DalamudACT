using System;
using Dalamud.Game.ClientState.Buddy;
using Dalamud.Game.ClientState.Objects.Types;
using GameObjectId = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObjectId;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
    private static bool MatchesPartyMemberActor(Dalamud.Game.ClientState.Party.IPartyMember member, uint actorId)
    {
        var gameObject = member.GameObject;
        var identity = GetPartyMemberIdentity(member, gameObject);
        // 这里只按 ID 口径匹配，不再按名字兜底，避免误把队外同名对象算进来。
        return identity.MatchesActorId(actorId);
    }

    private static bool MatchesBuddyActor(IBuddyMember buddy, uint actorId)
    {
        var gameObject = buddy.GameObject;
        var identity = GetBuddyIdentity(buddy, gameObject);
        return identity.MatchesActorId(actorId);
    }

    private static bool MatchesBattleCharaActor(IBattleChara battleChara, uint actorId)
    {
        var identity = GetGameObjectIdentity(battleChara);
        return identity.MatchesActorId(actorId);
    }

    private static bool AreSameGameObject(IGameObject? left, IGameObject? right)
    {
        if (left == null || right == null)
            return false;

        if (left.Address != nint.Zero && right.Address != nint.Zero && left.Address == right.Address)
            return true;

        var leftIdentity = GetGameObjectIdentity(left);
        var rightIdentity = GetGameObjectIdentity(right);

        return (leftIdentity.GameObjectId != 0 && leftIdentity.GameObjectId == rightIdentity.GameObjectId)
               || (leftIdentity.ActorId != 0 && leftIdentity.ActorId == rightIdentity.ActorId)
               || (leftIdentity.ObjectId != 0 && leftIdentity.ObjectId == rightIdentity.ObjectId)
               || (leftIdentity.EntityId != 0 && leftIdentity.EntityId == rightIdentity.EntityId);
    }

    private static bool AreEquivalentActorIds(uint leftActorId, uint rightActorId)
    {
        if (leftActorId is 0 or InvalidActorId || rightActorId is 0 or InvalidActorId)
            return false;

        if (leftActorId == rightActorId)
            return true;

        var leftObject = FindObjectByActorId(leftActorId);
        var rightObject = FindObjectByActorId(rightActorId);
        if (leftObject != null && rightObject != null && AreSameGameObject(leftObject, rightObject))
            return true;

        if (leftObject != null && GetGameObjectIdentity(leftObject).MatchesActorId(rightActorId))
            return true;

        if (rightObject != null && GetGameObjectIdentity(rightObject).MatchesActorId(leftActorId))
            return true;

        return false;
    }

    private static IGameObject? FindObjectByActorId(uint actorId)
    {
        if (actorId is 0 or InvalidActorId)
            return null;

        var entityMatch = DalamudApi.ObjectTable.SearchByEntityId(actorId);
        if (entityMatch != null)
            return entityMatch;

        foreach (var obj in DalamudApi.ObjectTable)
        {
            if (obj == null)
                continue;

            var identity = GetGameObjectIdentity(obj);
            if (identity.MatchesActorId(actorId))
                return obj;
        }

        return null;
    }

    private static uint ResolveBattleCharaActorId(IBattleChara battleChara)
    {
        return GetGameObjectIdentity(battleChara).ResolveActorId();
    }

    private static uint NormalizeEventActorId(uint actorId)
        => actorId is 0 or InvalidActorId ? 0u : actorId;

    private static uint NormalizeEventActorId(ulong actorId)
    {
        var low32 = unchecked((uint)(actorId & uint.MaxValue));
        return NormalizeEventActorId(low32);
    }

    private static uint NormalizeEventActorId(GameObjectId actorId)
    {
        var low32 = unchecked((uint)(actorId & uint.MaxValue));
        return NormalizeEventActorId(low32);
    }

}
