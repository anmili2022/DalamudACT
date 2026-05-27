using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
    private readonly record struct ActorIdentity(ulong GameObjectId, uint ActorId, uint ObjectId, uint EntityId)
    {
        public uint ResolveActorId()
        {
            if (ActorId > 0 && ActorId != InvalidActorId)
                return ActorId;

            if (ObjectId > 0 && ObjectId != InvalidActorId)
                return ObjectId;

            if (EntityId > 0 && EntityId != InvalidActorId)
                return EntityId;

            return 0;
        }

        public bool MatchesActorId(uint actorId)
        {
            if (actorId is 0 or InvalidActorId)
                return false;

            return (ActorId > 0 && ActorId != InvalidActorId && ActorId == actorId)
                   || (ObjectId > 0 && ObjectId != InvalidActorId && ObjectId == actorId)
                   || (EntityId > 0 && EntityId != InvalidActorId && EntityId == actorId);
        }
    }

    private readonly record struct OwnerCacheEntry(uint OwnerId, DateTime UpdatedAtUtc);

    public readonly record struct CurrentPartyMemberDisplayInfo(
        string Name,
        string JobName,
        string KindName,
        uint ActorId,
        uint CurrentHp,
        uint MaxHp);

    public sealed class LocalPartyHelperSnapshot
    {
        public List<IBattleChara> Party { get; } = new();
        public List<IBattleChara> CastableParty { get; } = new();
        public List<CurrentPartyMemberDisplayInfo> UnresolvedPartyMemberDisplayInfos { get; } = new();
    }

    private enum TrackedActorKind
    {
        Unknown,
        Player,
        FriendlyNpc,
        HostileNpc,
    }

    private readonly record struct TrackedActor(uint ActorId, string Name, uint JobId, string JobName, TrackedActorKind Kind);
}
