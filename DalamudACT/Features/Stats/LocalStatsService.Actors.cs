using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game.ClientState.Buddy;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using NativeAgentHud = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentHUD;
using NativeBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using NativeObjectKind = FFXIVClientStructs.FFXIV.Client.Game.Object.ObjectKind;
using GameObjectId = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObjectId;

namespace DalamudACT;

// Actor 解析模块：负责 ObjectTable、PartyList、BuddyList、owner cache 和本地统计对象身份归一。
internal sealed partial class LocalStatsService
{
    private static readonly TimeSpan OwnerCacheTtl = TimeSpan.FromMilliseconds(2500);
    private static readonly TimeSpan OwnerCacheWarmupInterval = TimeSpan.FromMilliseconds(1000);
    private const int PartyPlaceholderCount = 8;

    private readonly Dictionary<uint, OwnerCacheEntry> ownerCache = new();
    private readonly Dictionary<uint, TrackedActor> observedFriendlyActorCache = new();
    private readonly Dictionary<uint, TrackedActor> trackedActorLookupCache = new();
    private readonly Dictionary<uint, uint> partyMemberHpCache = new();
    private DateTime lastTrackedActorLookupCacheUtc;
    private DateTime lastOwnerWarmupUtc;
    private bool pronounPartyLookupUnavailableLogged;

    public void WarmOwnerCacheFromObjectTable()
    {
        var nowUtc = DateTime.UtcNow;
        if (nowUtc - lastOwnerWarmupUtc < OwnerCacheWarmupInterval)
            return;

        lastOwnerWarmupUtc = nowUtc;

        var entries = new List<(uint EntityId, uint OwnerId)>();
        foreach (var obj in DalamudApi.ObjectTable)
        {
            if (obj == null || obj.ObjectKind != ObjectKind.BattleNpc)
                continue;

            if (obj.EntityId is 0 or <= 0x40000000)
                continue;

            if (!TryGetResolvableOwnerId(obj, out var ownerId))
                continue;

            entries.Add((obj.EntityId, ownerId));
        }

        if (entries.Count == 0)
            return;

        lock (gate)
        {
            foreach (var (entityId, ownerId) in entries)
                ownerCache[entityId] = new OwnerCacheEntry(ownerId, nowUtc);
        }
    }

    public bool IsTrackedActor(uint actorId)
    {
        lock (gate)
            return TryGetTrackedActor(actorId, out _);
    }

    public bool CanResolveTrackedSource(uint actorId, DateTime nowUtc)
    {
        lock (gate)
            return TryResolveTrackedSource(actorId, nowUtc, out _);
    }

    public bool IsTrackedPlayerSource(uint actorId, DateTime nowUtc, bool includeLocalPlayer = true)
    {
        lock (gate)
        {
            if (!TryResolveTrackedSource(actorId, nowUtc, out var actor) || actor.Kind != TrackedActorKind.Player)
                return false;

            return includeLocalPlayer || !GetLocalPlayerIdentity().MatchesActorId(actor.ActorId);
        }
    }

    public bool TryResolveTrackedFriendlyActorId(uint actorId, DateTime nowUtc, out uint resolvedActorId)
    {
        lock (gate)
        {
            resolvedActorId = 0;
            if (!TryResolveTrackedSource(actorId, nowUtc, out var actor) || actor.Kind == TrackedActorKind.HostileNpc)
                return false;

            resolvedActorId = actor.ActorId;
            return true;
        }
    }

    private bool TryResolveCombatantSource(uint actorId, DateTime nowUtc, out TrackedActor actor, out bool isFriendly)
    {
        if (TryResolveTrackedSource(actorId, nowUtc, out actor))
        {
            isFriendly = actor.Kind != TrackedActorKind.HostileNpc;
            return true;
        }

        if (TryGetHostileBattleNpcTrackedActor(actorId, out actor))
        {
            isFriendly = false;
            return true;
        }

        isFriendly = false;
        return false;
    }

    public bool TryResolveTrackedSourceFromGameObject(IGameObject? gameObject, DateTime nowUtc, out uint actorId)
    {
        lock (gate)
        {
            actorId = 0;
            if (gameObject == null)
                return false;

            var identity = GetGameObjectIdentity(gameObject);
            var directActorId = identity.ResolveActorId();
            if (TryResolveTrackedSource(directActorId, nowUtc, out var resolvedActor))
            {
                actorId = resolvedActor.ActorId;
                return true;
            }

            if (TryGetTrackedActor(gameObject, out resolvedActor))
            {
                actorId = resolvedActor.ActorId;
                return true;
            }

            var ownerActorId = ResolveOwner(directActorId, nowUtc);
            if (TryResolveTrackedSource(ownerActorId, nowUtc, out resolvedActor))
            {
                actorId = resolvedActor.ActorId;
                return true;
            }

            return false;
        }
    }

    public bool ObserveFriendlyCombatantFromGameObject(IGameObject? gameObject, out uint actorId)
    {
        lock (gate)
        {
            actorId = 0;
            if (ShouldIgnoreFriendlyNpcStatistics())
                return false;

            if (!TryCreateObservedFriendlyActor(gameObject, allowUnmarkedBattleNpc: false, out var actor))
                return false;

            var shouldLog = !observedFriendlyActorCache.ContainsKey(actor.ActorId);
            observedFriendlyActorCache[actor.ActorId] = actor;
            actorId = actor.ActorId;
            if (shouldLog)
                LogHelper.DebugRecent("统计", $"已纳入可跟踪友方对象：name={actor.Name}，actorId=0x{actor.ActorId:X8}。");
            return true;
        }
    }

    public bool ObserveFriendlyCombatantSourceFromGameObject(IGameObject? gameObject, out uint actorId)
    {
        lock (gate)
        {
            actorId = 0;
            if (ShouldIgnoreFriendlyNpcStatistics())
                return false;

            if (!TryCreateObservedFriendlyActor(gameObject, allowUnmarkedBattleNpc: true, out var actor))
                return false;

            var shouldLog = !observedFriendlyActorCache.ContainsKey(actor.ActorId);
            observedFriendlyActorCache[actor.ActorId] = actor;
            actorId = actor.ActorId;
            if (shouldLog)
                LogHelper.Info("统计", $"已按战斗事件来源纳入可跟踪友方对象：name={actor.Name}，actorId=0x{actor.ActorId:X8}。");
            return true;
        }
    }

    public bool ObserveFriendlyCombatantIdentity(uint actorId, string? name)
    {
        lock (gate)
        {
            if (ShouldIgnoreFriendlyNpcStatistics())
                return false;

            if (!TryCreateObservedFriendlyActor(actorId, name, out var actor))
                return false;

            var shouldLog = !observedFriendlyActorCache.ContainsKey(actor.ActorId);
            observedFriendlyActorCache[actor.ActorId] = actor;
            if (shouldLog)
                LogHelper.DebugRecent("统计", $"已按事件身份纳入可跟踪友方对象：name={actor.Name}，actorId=0x{actor.ActorId:X8}。");
            return true;
        }
    }

    public IReadOnlyList<CurrentPartyMemberDisplayInfo> GetCurrentPartyMemberDisplayInfos()
    {
        lock (gate)
        {
            var helper = BuildLocalPartyHelperSnapshot();
            var result = new List<CurrentPartyMemberDisplayInfo>(helper.Party.Count + helper.UnresolvedPartyMemberDisplayInfos.Count);
            var seenDisplayActors = new HashSet<uint>();
            var seenDisplayNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ally in helper.Party)
            {
                var name = ally.Name.TextValue?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var actorId = ResolveBattleCharaActorId(ally);
                if (!TryMarkCurrentPartyMemberDisplay(seenDisplayActors, seenDisplayNames, actorId, name))
                    continue;

                var jobName = ResolveJobName(ally.ClassJob.RowId);
                result.Add(new CurrentPartyMemberDisplayInfo(
                    name,
                    string.IsNullOrWhiteSpace(jobName) ? "--" : jobName,
                    FormatTrackedActorKind(ResolveLocalPartyActorKind(ally)),
                    actorId,
                    ally.CurrentHp,
                    ally.MaxHp));
            }

            foreach (var member in helper.UnresolvedPartyMemberDisplayInfos)
            {
                if (!TryMarkCurrentPartyMemberDisplay(seenDisplayActors, seenDisplayNames, member.ActorId, member.Name))
                    continue;

                result.Add(member);
            }

            return result;
        }
    }

    public bool IsHostileBattleActor(uint actorId)
    {
        if (actorId is 0 or InvalidActorId)
            return false;

        var obj = FindObjectByActorId(actorId);
        return obj is IBattleNpc battleNpc
               && (battleNpc.StatusFlags & StatusFlags.Hostile) != 0;
    }

    private bool TryResolveTrackedSource(uint actorId, DateTime nowUtc, out TrackedActor actor)
    {
        actor = default;
        if (TryGetTrackedActor(actorId, out actor))
            return true;

        var resolvedActorId = ResolveOwner(actorId, nowUtc);
        if (resolvedActorId is 0 or InvalidActorId || resolvedActorId == actorId)
            return false;

        return TryGetTrackedActor(resolvedActorId, out actor);
    }


    private static uint ResolvePartyMemberActorId(Dalamud.Game.ClientState.Party.IPartyMember member)
        => ResolvePartyMemberActorId(member, member.GameObject);

    private static uint ResolvePartyMemberActorId(Dalamud.Game.ClientState.Party.IPartyMember member, IGameObject? gameObject)
        => GetPartyMemberIdentity(member, gameObject).ResolveActorId();

    private bool TryGetTrackedActor(uint actorId, out TrackedActor actor)
    {
        actor = default;
        if (actorId is 0 or InvalidActorId)
            return false;

        if (TryGetCachedTrackedActor(actorId, out actor))
            return true;

        if (TryGetTrackedPartyBattleCharaActor(actorId, out actor))
            return true;

        if (TryGetPartyMemberTrackedActor(actorId, out actor))
            return true;

        if (TryGetBuddyTrackedActor(actorId, out actor))
            return true;

        if (observedFriendlyActorCache.TryGetValue(actorId, out actor))
            return true;

        if (TryGetFriendlyBattleNpcTrackedActor(actorId, out actor))
            return true;


        return TryGetLocalPlayerTrackedActor(actorId, out actor);
    }

    private bool TryGetCachedTrackedActor(uint actorId, out TrackedActor actor)
    {
        RefreshTrackedActorLookupCache(DateTime.UtcNow);
        return trackedActorLookupCache.TryGetValue(actorId, out actor);
    }

    private void RefreshTrackedActorLookupCache(DateTime nowUtc)
    {
        if (nowUtc - lastTrackedActorLookupCacheUtc < TimeSpan.FromMilliseconds(250))
            return;

        lastTrackedActorLookupCacheUtc = nowUtc;
        trackedActorLookupCache.Clear();

        foreach (var member in DalamudApi.PartyList)
        {
            var name = member.Name.TextValue ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var gameObject = member.GameObject;
            var identity = GetPartyMemberIdentity(member, gameObject);
            var canonicalActorId = identity.ResolveActorId();
            if (canonicalActorId is 0 or InvalidActorId)
                continue;

            var jobId = member.ClassJob.RowId;
            var actor = new TrackedActor(
                canonicalActorId,
                name.Trim(),
                jobId,
                ResolveJobName(jobId),
                ResolvePartyMemberTrackedActorKind(member, gameObject));
            AddTrackedActorLookup(identity, actor);
        }

        var localIdentity = GetLocalPlayerIdentity();
        var localActorId = localIdentity.ResolveActorId();
        if (localActorId is not 0 and not InvalidActorId && TryGetLocalPlayerTrackedActor(localActorId, out var localActor))
        {
            AddTrackedActorLookup(localIdentity, localActor);
        }
    }

    private void AddTrackedActorLookup(ActorIdentity identity, TrackedActor actor)
    {
        AddTrackedActorLookup(identity.ActorId, actor);
        AddTrackedActorLookup(identity.ObjectId, actor);
        AddTrackedActorLookup(identity.EntityId, actor);
    }

    private void AddTrackedActorLookup(uint actorId, TrackedActor actor)
    {
        if (actorId is 0 or InvalidActorId)
            return;

        trackedActorLookupCache[actorId] = actor;
    }

    private bool TryGetTrackedActor(IGameObject? gameObject, out TrackedActor actor)
    {
        actor = default;
        if (gameObject == null)
            return false;

        if (gameObject is IBattleChara battleChara && TryGetTrackedBattleCharaActor(battleChara, out actor))
            return true;


        var identity = GetGameObjectIdentity(gameObject);
        if (identity.ResolveActorId() is var actorId && actorId != 0 && TryGetLocalPlayerTrackedActor(actorId, out actor))
            return true;

        return false;
    }

    private bool TryGetTrackedBattleCharaActor(IBattleChara battleChara, out TrackedActor actor)
    {
        foreach (var trackedBattleChara in EnumerateTrackedPartyBattleCharas())
        {
            if (!AreSameGameObject(trackedBattleChara, battleChara))
                continue;

            var trackedActor = CreateTrackedActor(
                trackedBattleChara,
                ResolveBattleCharaActorId(battleChara),
                ResolveLocalPartyActorKind(trackedBattleChara));
            if (trackedActor == null)
                continue;

            actor = trackedActor.Value;
            return true;
        }

        var battleCharaActorId = ResolveBattleCharaActorId(battleChara);
        if (battleCharaActorId != 0 && TryGetLocalPlayerTrackedActor(battleCharaActorId, out actor))
            return true;


        actor = default;
        return false;
    }

    private bool TryGetPartyMemberTrackedActor(uint actorId, out TrackedActor actor)
    {
        foreach (var member in DalamudApi.PartyList)
        {
            if (!MatchesPartyMemberActor(member, actorId))
                continue;

            var name = member.Name.TextValue ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var jobId = member.ClassJob.RowId;
            var canonicalActorId = ResolvePartyMemberActorId(member);
            actor = new TrackedActor(
                canonicalActorId is 0 or InvalidActorId ? actorId : canonicalActorId,
                name.Trim(),
                jobId,
                ResolveJobName(jobId),
                ResolvePartyMemberTrackedActorKind(member, member.GameObject));
            return true;
        }

        actor = default;
        return false;
    }

    private bool TryGetTrackedPartyBattleCharaActor(uint actorId, out TrackedActor actor)
    {
        foreach (var battleChara in EnumerateTrackedPartyBattleCharas())
        {
            if (!MatchesBattleCharaActor(battleChara, actorId))
                continue;

            var trackedActor = CreateTrackedActor(
                battleChara,
                actorId,
                ResolveLocalPartyActorKind(battleChara));
            if (trackedActor == null)
                continue;

            actor = trackedActor.Value;
            return true;
        }

        actor = default;
        return false;
    }

    private static bool TryGetBuddyTrackedActor(uint actorId, out TrackedActor actor)
    {
        foreach (var buddy in DalamudApi.BuddyList)
        {
            if (!MatchesBuddyActor(buddy, actorId))
                continue;

            var gameObject = buddy.GameObject;
            var canonicalActorId = ResolveBuddyActorId(buddy, gameObject);
            var name = gameObject?.Name.TextValue?.Trim();
            if (string.IsNullOrWhiteSpace(name))
                name = FindObjectByActorId(actorId)?.Name.TextValue?.Trim();

            if (string.IsNullOrWhiteSpace(name))
                continue;

            actor = new TrackedActor(
                canonicalActorId is 0 or InvalidActorId ? actorId : canonicalActorId,
                name,
                0,
                string.Empty,
                TrackedActorKind.FriendlyNpc);
            return true;
        }

        actor = default;
        return false;
    }

    private bool TryGetFriendlyBattleNpcTrackedActor(uint actorId, out TrackedActor actor)
    {
        if (ShouldIgnoreFriendlyNpcStatistics())
        {
            actor = default;
            return false;
        }

        foreach (var obj in DalamudApi.ObjectTable)
        {
            if (obj is not IBattleNpc battleNpc)
                continue;

            if (!IsFriendlyTrackedBattleNpc(battleNpc))
                continue;

            if (!MatchesBattleCharaActor(battleNpc, actorId))
                continue;

            var trackedActor = CreateTrackedActor(battleNpc, actorId, TrackedActorKind.FriendlyNpc);
            if (trackedActor == null)
                continue;

            actor = trackedActor.Value;
            return true;
        }

        actor = default;
        return false;
    }

    private bool TryGetHostileBattleNpcTrackedActor(uint actorId, out TrackedActor actor)
    {
        foreach (var obj in DalamudApi.ObjectTable)
        {
            if (obj is not IBattleNpc battleNpc)
                continue;

            if ((battleNpc.StatusFlags & StatusFlags.Hostile) == 0)
                continue;

            if (!ShouldTrackHostileBattleNpc(battleNpc))
                continue;

            if (!MatchesBattleCharaActor(battleNpc, actorId))
                continue;

            var trackedActor = CreateTrackedActor(battleNpc, actorId);
            if (trackedActor == null)
                continue;

            actor = trackedActor.Value;
            return true;
        }

        actor = default;
        return false;
    }

    private bool ShouldTrackHostileBattleNpc(IBattleNpc battleNpc)
    {
        var localPlayerMaxHp = DalamudApi.GetLocalPlayerMaxHp();
        if (localPlayerMaxHp == 0)
            return false;

        var multiplier = Math.Clamp(config.HostileNpcMinHpMultiplier <= 0 ? 10 : config.HostileNpcMinHpMultiplier, 1, 100);
        return (ulong)battleNpc.MaxHp >= (ulong)localPlayerMaxHp * (ulong)multiplier;
    }

    private static bool LooksLikeCombatActorId(uint value)
        => (value & 0xF0000000u) is 0x10000000u or 0x40000000u;

    private TrackedActor? CreateTrackedActor(
        IBattleChara battleChara,
        uint fallbackActorId,
        TrackedActorKind? forcedKind = null)
    {
        var name = battleChara.Name.TextValue?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var actorId = ResolveBattleCharaActorId(battleChara);
        var jobId = battleChara.ClassJob.RowId;
        return new TrackedActor(
            actorId is 0 or InvalidActorId ? fallbackActorId : actorId,
            name,
            jobId,
            ResolveJobName(jobId),
            forcedKind ?? ResolveTrackedActorKind(battleChara));
    }

    private static TrackedActorKind ResolveLocalPartyActorKind(IBattleChara battleChara)
        => battleChara is IPlayerCharacter ? TrackedActorKind.Player : TrackedActorKind.FriendlyNpc;

    private TrackedActorKind ResolvePartyMemberTrackedActorKind(Dalamud.Game.ClientState.Party.IPartyMember member, IGameObject? gameObject)
    {
        if (gameObject is IPlayerCharacter)
            return TrackedActorKind.Player;

        var name = member.Name.TextValue?.Trim();
        return LooksLikeDutyCompanionName(name)
            ? TrackedActorKind.FriendlyNpc
            : gameObject is IBattleNpc or IBattleChara ? TrackedActorKind.FriendlyNpc : TrackedActorKind.Player;
    }

    private TrackedActorKind ResolveTrackedActorKind(IGameObject? gameObject)
    {
        return gameObject switch
        {
            null => TrackedActorKind.Unknown,
            IPlayerCharacter => TrackedActorKind.Player,
            IBattleNpc battleNpc => LooksLikeDutyCompanionName(battleNpc.Name.TextValue?.Trim())
                ? TrackedActorKind.FriendlyNpc
                : (battleNpc.StatusFlags & StatusFlags.Hostile) != 0
                    ? TrackedActorKind.HostileNpc
                    : TrackedActorKind.FriendlyNpc,
            IBattleChara => TrackedActorKind.FriendlyNpc,
            _ => TrackedActorKind.Unknown,
        };
    }

    private static string FormatTrackedActorKind(TrackedActorKind kind)
        => kind switch
        {
            TrackedActorKind.Player => "玩家",
            TrackedActorKind.FriendlyNpc => "友方NPC",
            TrackedActorKind.HostileNpc => "敌方NPC",
            _ => "未知",
        };

    private static bool TryMarkCurrentPartyMemberDisplay(
        ISet<uint> seenActors,
        ISet<string> seenNames,
        uint actorId,
        string name)
    {
        if (actorId is not 0 and not InvalidActorId && !seenActors.Add(actorId))
            return false;

        if (!string.IsNullOrWhiteSpace(name) && !seenNames.Add(name))
            return false;

        return true;
    }




    private static bool IsLocalPlayerActor(uint actorId)
        => GetLocalPlayerIdentity().MatchesActorId(actorId);

}
