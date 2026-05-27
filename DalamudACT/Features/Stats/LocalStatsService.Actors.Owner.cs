using System;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
    private uint ResolveOwner(uint actorId, DateTime nowUtc)
    {
        if (actorId == 0 || actorId == InvalidActorId)
            return InvalidActorId;

        var obj = FindObjectByActorId(actorId);
        if (TryGetResolvableOwnerId(obj, out var ownerActorId))
        {
            ownerCache[actorId] = new OwnerCacheEntry(ownerActorId, nowUtc);
            return ownerActorId;
        }

        if (ownerCache.TryGetValue(actorId, out var cached) && nowUtc - cached.UpdatedAtUtc <= OwnerCacheTtl)
            return cached.OwnerId;

        return InvalidActorId;
    }

    // 除了 Pet / Buddy / 陆行鸟，还要兼容带 OwnerId 的玩家额外来源，
    // 例如：英雄的掠影、礼仪之铃、后式自走人偶。
    //
    // 注意：信赖 / 剧情 NPC 队友在部分副本中也会带 OwnerId=本地玩家。
    // 这类对象不是宠物，也不是玩家额外来源；如果把它们归属到 owner，
    // TryCreateObservedFriendlyActor 会直接拒绝，导致 NPC 队友自己的输出行丢失。
    private bool TryGetResolvableOwnerId(IGameObject? gameObject, out uint ownerId)
    {
        ownerId = InvalidActorId;
        if (gameObject == null)
            return false;

        if (gameObject.OwnerId is 0 or InvalidActorId)
            return false;

        if (ShouldResolveOwnerForObject(gameObject))
        {
            ownerId = gameObject.OwnerId;
            return true;
        }

        if (gameObject is not IBattleNpc battleNpc)
            return false;

        if ((battleNpc.StatusFlags & StatusFlags.Hostile) != 0)
            return false;

        if (LooksLikeDutySupportBattleNpc(battleNpc))
            return false;

        if (!TryGetTrackedActor(gameObject.OwnerId, out _))
            return false;

        ownerId = gameObject.OwnerId;
        return true;
    }
}
