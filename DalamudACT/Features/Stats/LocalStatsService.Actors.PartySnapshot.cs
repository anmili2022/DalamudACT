using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Buddy;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using NativeAgentHud = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentHUD;
using NativeBattleChara = FFXIVClientStructs.FFXIV.Client.Game.Character.BattleChara;
using NativeGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using NativeObjectKind = FFXIVClientStructs.FFXIV.Client.Game.Object.ObjectKind;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
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
            if (ShouldIgnoreFriendlyNpcStatistics())
                break;

            var battleChara = ResolveBuddyBattleChara(buddy);
            AddAllyToLocalPartyHelper(helper, battleChara, seen);
        }

        if (ShouldScanFriendlyNpcObjectTable())
        {
            foreach (var obj in DalamudApi.ObjectTable)
            {
                if (obj is not IBattleChara battleChara)
                    continue;

                if (!IsFriendlyTrackedBattleNpc(battleChara))
                    continue;

                AddAllyToLocalPartyHelper(helper, battleChara, seen);
            }
        }

        LastSnapshot = helper;
        return helper;
    }

    private bool ShouldScanFriendlyNpcObjectTable()
    {
        if (ShouldIgnoreFriendlyNpcStatistics())
            return false;

        return latestInCombatHint
               || currentEncounter.Started
               || DalamudApi.Conditions.Any(ConditionFlag.InCombat)
               || DalamudApi.Conditions.Any(ConditionFlag.DutyRecorderPlayback);
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

        if (ShouldIgnoreFriendlyNpcStatistics())
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
}
