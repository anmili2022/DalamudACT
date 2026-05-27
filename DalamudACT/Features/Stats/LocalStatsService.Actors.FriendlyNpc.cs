using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
    private bool IsFriendlyTrackedBattleNpc(IBattleChara battleChara)
    {
        if (battleChara is not IBattleNpc battleNpc)
            return false;

        var name = battleNpc.Name.TextValue?.Trim();
        var looksLikeDutyCompanion = LooksLikeDutyCompanionName(name);
        var statusFlags = battleNpc.StatusFlags;
        if ((statusFlags & StatusFlags.Hostile) != 0 && !looksLikeDutyCompanion)
            return false;

        if (TryGetResolvableOwnerId(battleNpc, out _))
            return false;

        return looksLikeDutyCompanion
               || HasFriendlyBattleNpcIndicators(battleNpc)
               || LooksLikeDutySupportBattleNpc(battleNpc);
    }

    private bool TryCreateObservedFriendlyActor(IGameObject? gameObject, bool allowUnmarkedBattleNpc, out TrackedActor actor)
    {
        actor = default;
        IBattleChara? battleChara = gameObject as IBattleChara;
        if (battleChara == null && gameObject != null)
            battleChara = TryResolveBattleCharaFromIdentity(GetGameObjectIdentity(gameObject));

        if (battleChara == null)
            return TryCreateNamedFriendlyActorFromGameObject(gameObject, out actor);

        if (battleChara is IPlayerCharacter)
            return TryCreateNamedFriendlyActorFromGameObject(gameObject, out actor);

        if (battleChara.ObjectKind != ObjectKind.BattleNpc && !allowUnmarkedBattleNpc)
            return TryCreateNamedFriendlyActorFromGameObject(gameObject, out actor);

        var battleCharaName = battleChara.Name.TextValue?.Trim();
        var looksLikeDutyCompanion = LooksLikeDutyCompanionName(battleCharaName);
        if ((battleChara.StatusFlags & StatusFlags.Hostile) != 0 && !looksLikeDutyCompanion)
            return false;

        if (TryGetResolvableOwnerId(battleChara, out _))
            return false;

        if (battleChara is IBattleNpc battleNpc)
        {
            var hasFriendlyIndicators = HasFriendlyBattleNpcIndicators(battleNpc);
            if (!hasFriendlyIndicators && !allowUnmarkedBattleNpc)
                return false;

            // 战斗事件 source 指针/ID 在部分副本 NPC 事件中可能错位。
            // 如果一个“未带友方标记”的候选对象和 hostile 目标同名，优先判定为
            // Boss/敌方对象口径错位，不把它动态收编为友方 NPC。
            if (!hasFriendlyIndicators && HasHostileBattleNpcWithSameName(battleNpc))
                return false;
        }

        var trackedActor = CreateTrackedActor(battleChara, ResolveBattleCharaActorId(battleChara), TrackedActorKind.FriendlyNpc);
        if (trackedActor == null)
            return TryCreateNamedFriendlyActorFromGameObject(gameObject, out actor);

        actor = trackedActor.Value;
        return true;
    }

    private bool TryCreateObservedFriendlyActor(uint actorId, string? name, out TrackedActor actor)
    {
        actor = default;
        if (actorId is 0 or InvalidActorId)
            return false;

        var normalizedName = name?.Trim();
        if (!LooksLikeDutyCompanionName(normalizedName))
            return false;

        actor = new TrackedActor(actorId, normalizedName!, 0, string.Empty, TrackedActorKind.FriendlyNpc);
        return true;
    }

    private bool TryCreateNamedFriendlyActorFromGameObject(IGameObject? gameObject, out TrackedActor actor)
    {
        actor = default;
        if (gameObject == null)
            return false;

        var name = gameObject.Name.TextValue?.Trim();
        if (!LooksLikeDutyCompanionName(name))
            return false;

        var actorId = GetGameObjectIdentity(gameObject).ResolveActorId();
        if (actorId is 0 or InvalidActorId)
            return false;

        actor = new TrackedActor(actorId, name!, 0, string.Empty, TrackedActorKind.FriendlyNpc);
        return true;
    }

    private bool LooksLikeDutyCompanionName(string? name)
    {
        var normalizedName = NormalizeActorNameForCatalog(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
            return false;

        return normalizedName.EndsWith("的幻体", StringComparison.Ordinal)
               || IsKnownDutySupportCompanionName(normalizedName);
    }

    private bool ShouldIgnoreFriendlyNpcStatistics()
        => CountCurrentPartyPlayers() >= PartyPlaceholderCount;

    private int CountCurrentPartyPlayers()
    {
        var seen = new HashSet<uint>();
        var localPlayerId = DalamudApi.GetLocalPlayerEntityId();
        if (localPlayerId is not 0 and not InvalidActorId)
            seen.Add(localPlayerId);

        foreach (var member in DalamudApi.PartyList)
        {
            if (ResolvePartyMemberTrackedActorKind(member, member.GameObject) != TrackedActorKind.Player)
                continue;

            var actorId = ResolvePartyMemberActorId(member);
            if (actorId is 0 or InvalidActorId)
                continue;

            seen.Add(actorId);
        }

        return seen.Count;
    }

    internal static bool IsBuiltInFriendlyNpcName(string normalizedName)
    {
        foreach (var builtInName in BuiltInFriendlyNpcNameArray)
        {
            if (string.Equals(builtInName, normalizedName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private bool IsKnownDutySupportCompanionName(string normalizedName)
    {
        if (IsBuiltInFriendlyNpcName(normalizedName))
            return true;

        if (config.CustomFriendlyNpcNames == null)
            return false;

        foreach (var customName in config.CustomFriendlyNpcNames)
        {
            if (string.Equals(customName, normalizedName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static string NormalizeActorNameForCatalog(string? name)
        => PluginConfiguration.NormalizeFriendlyNpcNameForCatalog(name);

    private bool HasFriendlyBattleNpcIndicators(IBattleNpc battleNpc)
    {
        var statusFlags = battleNpc.StatusFlags;
        if ((statusFlags & (StatusFlags.PartyMember | StatusFlags.Friend)) != 0)
            return true;

        if (IsDutyNpcPartyMemberKind(battleNpc))
            return true;

        var name = battleNpc.Name.TextValue?.Trim();
        return LooksLikeDutyCompanionName(name);
    }

    private bool LooksLikeDutySupportBattleNpc(IBattleNpc battleNpc)
    {
        var name = battleNpc.Name.TextValue?.Trim();
        var looksLikeDutyCompanion = LooksLikeDutyCompanionName(name);
        if ((battleNpc.StatusFlags & StatusFlags.Hostile) != 0 && !looksLikeDutyCompanion)
            return false;

        if (IsDutyNpcPartyMemberKind(battleNpc))
            return true;

        var statusFlags = battleNpc.StatusFlags;
        if ((statusFlags & (StatusFlags.PartyMember | StatusFlags.Friend)) != 0)
            return true;

        if (looksLikeDutyCompanion)
            return true;

        // 7.x 主线 / 单人任务中的 NPC 队友经常表现为：
        // - ObjectKind = BattleNpc
        // - 非 Hostile
        // - 有职业 RowId
        // - OwnerId 指向本地玩家
        // 这和召唤物/宠物不同，应独立统计为 friendlyNpc。
        // 真正的 Pet / Buddy / RaceChocobo 已在 TryGetResolvableOwnerId 前段
        // 通过 ShouldResolveOwnerForObject 优先归属 owner，不会走到这里。
        return battleNpc.ClassJob.RowId != 0
               && battleNpc.OwnerId is not 0 and not InvalidActorId;
    }

    private static bool HasHostileBattleNpcWithSameName(IBattleNpc candidate)
    {
        var candidateName = candidate.Name.TextValue?.Trim();
        if (string.IsNullOrWhiteSpace(candidateName))
            return false;

        foreach (var obj in DalamudApi.ObjectTable)
        {
            if (obj is not IBattleNpc battleNpc)
                continue;

            if (battleNpc.Address == candidate.Address)
                continue;

            if ((battleNpc.StatusFlags & StatusFlags.Hostile) == 0)
                continue;

            var name = battleNpc.Name.TextValue?.Trim();
            if (string.Equals(name, candidateName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static bool ShouldResolveOwnerForObject(IGameObject? gameObject)
    {
        if (gameObject is not IBattleNpc battleNpc)
            return gameObject != null;

        var kindName = battleNpc.BattleNpcKind.ToString();
        return string.Equals(kindName, "Pet", StringComparison.Ordinal)
               || string.Equals(kindName, "Buddy", StringComparison.Ordinal)
               || string.Equals(kindName, "RaceChocobo", StringComparison.Ordinal);
    }

    private static bool IsDutyNpcPartyMemberKind(IBattleNpc battleNpc)
        => string.Equals(battleNpc.BattleNpcKind.ToString(), "NpcPartyMember", StringComparison.Ordinal);
}
