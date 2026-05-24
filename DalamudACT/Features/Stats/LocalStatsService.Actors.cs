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
    private static readonly TimeSpan OwnerCacheWarmupInterval = TimeSpan.FromMilliseconds(500);
    private const int PartyPlaceholderCount = 8;
    private static readonly string[] BuiltInFriendlyNpcNameArray =
    {
        "阿尔菲诺",
        "阿莉塞",
        "雅修特拉",
        "桑克瑞德",
        "于里昂热",
        "古拉哈提亚",
        "埃斯蒂尼安",
        "乌克拉玛特",
        "可露儿",
        "克鲁鲁",
        "敏菲利亚",
        "琳",
        "莉瑟",
        "水晶公",
        "零",
        "瓦尔桑",
        "卡尔瓦兰",
        "爱梅特赛尔克",
        "希斯拉德",
        "维涅斯",
    };

    internal static IReadOnlyList<string> BuiltInFriendlyNpcNames => BuiltInFriendlyNpcNameArray;

    private readonly Dictionary<uint, OwnerCacheEntry> ownerCache = new();
    private readonly Dictionary<uint, TrackedActor> observedFriendlyActorCache = new();
    private readonly Dictionary<uint, uint> partyMemberHpCache = new();
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

    private static uint ResolvePartyMemberActorId(Dalamud.Game.ClientState.Party.IPartyMember member)
        => ResolvePartyMemberActorId(member, member.GameObject);

    private static uint ResolvePartyMemberActorId(Dalamud.Game.ClientState.Party.IPartyMember member, IGameObject? gameObject)
        => GetPartyMemberIdentity(member, gameObject).ResolveActorId();

    private bool TryGetTrackedActor(uint actorId, out TrackedActor actor)
    {
        actor = default;
        if (actorId is 0 or InvalidActorId)
            return false;

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
        if (config.DebugRecordSmallHostileNpcAsBoss)
            return true;

        var localPlayerMaxHp = DalamudApi.GetLocalPlayerMaxHp();
        if (localPlayerMaxHp == 0)
            return false;

        var multiplier = Math.Clamp(config.HostileNpcMinHpMultiplier <= 0 ? 10 : config.HostileNpcMinHpMultiplier, 1, 100);
        return (ulong)battleNpc.MaxHp >= (ulong)localPlayerMaxHp * (ulong)multiplier;
    }

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

    private IEnumerable<IBattleChara> EnumerateTrackedPartyBattleCharas()
    {
        var helper = BuildLocalPartyHelperSnapshot();
        foreach (var ally in helper.CastableParty)
            yield return ally;
    }

    public LocalPartyHelperSnapshot? LastSnapshot { get; private set; }

    public LocalPartyHelperSnapshot BuildLocalPartyHelperSnapshot()
    {
        var helper = new LocalPartyHelperSnapshot();
        var seen = new HashSet<ulong>();

        var localPlayerBattleChara = TryResolveBattleCharaFromIdentity(GetLocalPlayerIdentity());
        AddAllyToLocalPartyHelper(helper, localPlayerBattleChara, seen);

        AddPronounPartyMembersToLocalPartyHelper(helper, seen);
        AddAgentHudPartyMembersToLocalPartyHelper(helper, seen);

        foreach (var member in DalamudApi.PartyList)
        {
            var battleChara = ResolvePartyMemberBattleChara(member);
            AddAllyToLocalPartyHelper(helper, battleChara, seen);
        }

        foreach (var buddy in DalamudApi.BuddyList)
        {
            var battleChara = ResolveBuddyBattleChara(buddy);
            AddAllyToLocalPartyHelper(helper, battleChara, seen);
        }

        foreach (var obj in DalamudApi.ObjectTable)
        {
            if (obj is not IBattleChara battleChara)
                continue;

            if (!IsFriendlyTrackedBattleNpc(battleChara))
                continue;

            AddAllyToLocalPartyHelper(helper, battleChara, seen);
        }

        LastSnapshot = helper;
        return helper;
    }

    private void AddPronounPartyMembersToLocalPartyHelper(LocalPartyHelperSnapshot helper, ISet<ulong> seen)
    {
        try
        {
            unsafe
            {
                var pronounModule = PronounModule.Instance();
                if (pronounModule == null)
                    return;

                for (var index = 1; index <= PartyPlaceholderCount; index++)
                {
                    var nativeObject = ResolvePartyPlaceholder(pronounModule, index);
                    if (nativeObject == null || nativeObject->EntityId is 0 or InvalidActorId)
                        continue;

                    AddNativePartyMemberToLocalPartyHelper(helper, seen, nativeObject);
                }
            }
        }
        catch (Exception ex)
        {
            if (!pronounPartyLookupUnavailableLogged)
            {
                pronounPartyLookupUnavailableLogged = true;
                LogHelper.Debug("统计", ex, "通过游戏占位符 <1>~<8> 读取队伍成员失败，已回退到 Dalamud PartyList/ObjectTable。");
            }
        }
    }

    private void AddAgentHudPartyMembersToLocalPartyHelper(LocalPartyHelperSnapshot helper, ISet<ulong> seen)
    {
        try
        {
            unsafe
            {
                var agentHud = NativeAgentHud.Instance();
                if (agentHud == null)
                    return;

                var partyMemberCount = Math.Clamp(agentHud->PartyMemberCount, (short)0, (short)10);
                var partyMembers = agentHud->PartyMembers;
                for (var index = 0; index < partyMemberCount && index < partyMembers.Length; index++)
                {
                    var partyMember = partyMembers[index];
                    if (partyMember.Object != null)
                    {
                        AddNativePartyMemberToLocalPartyHelper(helper, seen, (NativeGameObject*)partyMember.Object);
                        continue;
                    }

                    if (partyMember.EntityId is 0 or InvalidActorId)
                        continue;

                    var name = partyMember.Name.ToString();
                    AddUnresolvedNativePartyMemberToLocalPartyHelper(
                        helper,
                        seen,
                        nint.Zero,
                        partyMember.EntityId,
                        0,
                        string.IsNullOrWhiteSpace(name) ? $"队伍成员 0x{partyMember.EntityId:X8}" : name,
                        0,
                        0,
                        0);
                }
            }
        }
        catch (Exception ex)
        {
            if (!pronounPartyLookupUnavailableLogged)
            {
                pronounPartyLookupUnavailableLogged = true;
                LogHelper.Debug("统计", ex, "通过 HUD 队伍成员缓存读取队伍成员失败，已回退到 Dalamud PartyList/ObjectTable。");
            }
        }
    }

    private static unsafe NativeGameObject* ResolvePartyPlaceholder(
        PronounModule* pronounModule,
        int index)
    {
        _ = pronounModule;
        _ = index;

        // FFXIVClientStructs 生成的 ResolvePlaceholder 函数指针签名在本地 SDK 与
        // GitHub Actions 使用的 Dalamud CN SDK 间不一致（4 参数 / 5 参数）。
        // 为避免发布构建失败，这里不再直接调用该函数指针，队伍读取继续依赖
        // AgentHUD、Dalamud PartyList、BuddyList 和 ObjectTable 兜底。
        return null;

#if false
        byte* placeholder = stackalloc byte[4];
        placeholder[0] = (byte)'<';
        placeholder[1] = (byte)('0' + index);
        placeholder[2] = (byte)'>';
        placeholder[3] = 0;

        // 不调用 ResolvePlaceholder(string, byte, byte) 托管重载。
        // 不同 Dalamud / FFXIVClientStructs 运行时里这个便捷重载可能不存在，会触发 MissingMethodException。
        // 这里直接走生成器暴露的原生成员函数指针，与 AE 的底层读取路径等价，但避开字符串重载版本差异。
        return PronounModule.MemberFunctionPointers.ResolvePlaceholder(pronounModule, placeholder, 0, 0);
#endif
    }

    private unsafe void AddNativePartyMemberToLocalPartyHelper(
        LocalPartyHelperSnapshot helper,
        ISet<ulong> seen,
        NativeGameObject* nativeObject)
    {
        if (nativeObject == null || nativeObject->EntityId is 0 or InvalidActorId)
            return;

        var nativeAddress = (nint)nativeObject;
        var entityId = nativeObject->EntityId;
        var gameObjectId = TryGetNativeGameObjectId(nativeObject);
        var battleChara = TryResolveNativeBattleChara(nativeAddress, entityId, gameObjectId);
        if (battleChara != null)
        {
            AddAllyToLocalPartyHelper(helper, battleChara, seen);
            return;
        }

        var name = TryGetNativeGameObjectName(nativeObject);
        if (string.IsNullOrWhiteSpace(name))
            name = $"队伍成员 0x{entityId:X8}";

        var jobId = 0u;
        var currentHp = 0u;
        var maxHp = 0u;
        if (nativeObject->ObjectKind is NativeObjectKind.Pc or NativeObjectKind.BattleNpc)
        {
            var nativeBattleChara = (NativeBattleChara*)nativeObject;
            jobId = nativeBattleChara->ClassJob;
            currentHp = nativeBattleChara->Health;
            maxHp = nativeBattleChara->MaxHealth;
        }

        AddUnresolvedNativePartyMemberToLocalPartyHelper(
            helper,
            seen,
            nativeAddress,
            entityId,
            gameObjectId,
            name,
            jobId,
            currentHp,
            maxHp);
    }

    private void AddUnresolvedNativePartyMemberToLocalPartyHelper(
        LocalPartyHelperSnapshot helper,
        ISet<ulong> seen,
        nint nativeAddress,
        uint entityId,
        ulong gameObjectId,
        string name,
        uint jobId,
        uint currentHp,
        uint maxHp)
    {
        if (entityId is 0 or InvalidActorId || string.IsNullOrWhiteSpace(name))
            return;

        if (!TryMarkUniqueNativePartyMember(nativeAddress, entityId, gameObjectId, seen))
            return;

        var jobName = ResolveJobName(jobId);
        var actor = new TrackedActor(
            entityId,
            name.Trim(),
            jobId,
            jobName,
            TrackedActorKind.FriendlyNpc);

        observedFriendlyActorCache[entityId] = actor;
        helper.UnresolvedPartyMemberDisplayInfos.Add(new CurrentPartyMemberDisplayInfo(
            actor.Name,
            string.IsNullOrWhiteSpace(actor.JobName) ? "--" : actor.JobName,
            FormatTrackedActorKind(actor.Kind),
            actor.ActorId,
            currentHp,
            maxHp));
    }

    private static bool TryMarkUniqueNativePartyMember(nint nativeAddress, uint entityId, ulong gameObjectId, ISet<ulong> seen)
    {
        ulong uniqueId;
        if (nativeAddress != nint.Zero)
            uniqueId = unchecked((ulong)nativeAddress);
        else if (gameObjectId != 0)
            uniqueId = gameObjectId;
        else if (entityId is not 0 and not InvalidActorId)
            uniqueId = 0x8000000000000000UL | entityId;
        else
            return false;

        return seen.Add(uniqueId);
    }

    private static unsafe ulong TryGetNativeGameObjectId(NativeGameObject* nativeObject)
    {
        if (nativeObject == null)
            return 0;

        try
        {
            return nativeObject->GetGameObjectId().Id;
        }
        catch
        {
            return 0;
        }
    }

    private static unsafe string TryGetNativeGameObjectName(NativeGameObject* nativeObject)
    {
        if (nativeObject == null)
            return string.Empty;

        try
        {
            return nativeObject->NameString.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static IBattleChara? TryResolveNativeBattleChara(nint nativeAddress, uint entityId, ulong gameObjectId)
    {
        if (entityId is not 0 and not InvalidActorId)
        {
            var entityMatch = DalamudApi.ObjectTable.SearchByEntityId(entityId) as IBattleChara;
            if (entityMatch != null)
                return entityMatch;
        }

        if (gameObjectId != 0)
        {
            var gameObjectMatch = DalamudApi.ObjectTable.SearchById(gameObjectId) as IBattleChara;
            if (gameObjectMatch != null)
                return gameObjectMatch;
        }

        if (nativeAddress != nint.Zero)
        {
            foreach (var obj in DalamudApi.ObjectTable)
            {
                if (obj is IBattleChara battleChara && obj.Address == nativeAddress)
                    return battleChara;
            }
        }

        return entityId is 0 or InvalidActorId ? null : FindObjectByActorId(entityId) as IBattleChara;
    }

    private static void AddAllyToLocalPartyHelper(
        LocalPartyHelperSnapshot helper,
        IBattleChara? ally,
        ISet<ulong> seen)
    {
        if (ally == null || !TryMarkUniqueBattleChara(ally, seen))
            return;

        helper.Party.Add(ally);

        // DPS 统计只需要“当前可作为我方统计对象的队友集合”。
        // 不做距离过滤，避免远处剧情 NPC / 信赖 NPC 因超出施法距离而漏掉伤害与承伤。
        helper.CastableParty.Add(ally);
    }

    private static IBattleChara? ResolvePartyMemberBattleChara(Dalamud.Game.ClientState.Party.IPartyMember member)
    {
        var gameObject = member.GameObject;
        if (gameObject is IBattleChara battleChara)
            return battleChara;

        return TryResolveBattleCharaFromIdentity(GetPartyMemberIdentity(member, gameObject));
    }

    private static IBattleChara? ResolveBuddyBattleChara(IBuddyMember buddy)
    {
        var gameObject = buddy.GameObject;
        if (gameObject is IBattleChara battleChara)
            return battleChara;

        return TryResolveBattleCharaFromIdentity(GetBuddyIdentity(buddy, gameObject));
    }

    private static bool TryMarkUniqueBattleChara(IBattleChara battleChara, ISet<ulong> seen)
    {
        var uniqueId = ResolveBattleCharaUniqueId(battleChara);
        return uniqueId != 0 && seen.Add(uniqueId);
    }

    private static ulong ResolveBattleCharaUniqueId(IBattleChara battleChara)
    {
        var address = battleChara.Address;
        if (address != nint.Zero)
            return unchecked((ulong)address);

        try
        {
            var gameObjectId = TryGetGameObjectId(battleChara);
            return gameObjectId != 0 ? gameObjectId : battleChara.EntityId;
        }
        catch
        {
            return battleChara.EntityId;
        }
    }

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

    private static bool IsLocalPlayerActor(uint actorId)
        => GetLocalPlayerIdentity().MatchesActorId(actorId);

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
