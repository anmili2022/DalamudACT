using System;
using System.Collections.Generic;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace DalamudACT;

public sealed partial class ACT
{
    private unsafe void ReceiveAbilityEffect(
        uint sourceId,
        nint sourceCharacter,
        nint pos,
        ActionEffectHandler.Header* effectHeader,
        ActionEffectHandler.TargetEffects* effectArray,
        GameObjectId* effectTrail)
    {
        receiveAbilityHook!.Original(sourceId, sourceCharacter, pos, effectHeader, effectArray, effectTrail);

        var numTargets = effectHeader->NumTargets;
        if (numTargets == 0)
            return;

        var actionId = effectHeader->SpellId != 0 ? (uint)effectHeader->SpellId : effectHeader->ActionId;
        try
        {
            HandleAbility(effectHeader, effectArray, effectTrail, sourceId, sourceCharacter);
            abilityEffectFaulted = false;
        }
        catch (Exception ex)
        {
            if (!abilityEffectFaulted)
            {
                abilityEffectFaulted = true;
                LogHelper.Error(
                    "插件",
                    ex,
                    $"处理 ActionEffect 事件失败：sourceId=0x{sourceId:X8}，actionId=0x{actionId:X8}，targets={numTargets}。");
            }
        }
    }

    private unsafe void HandleAbility(
        ActionEffectHandler.Header* header,
        ActionEffectHandler.TargetEffects* effects,
        GameObjectId* targets,
        uint sourceId,
        nint sourceCharacterAddress)
    {
        var nowUtc = DateTime.UtcNow;
        var actionId = header->SpellId != 0 ? (uint)header->SpellId : header->ActionId;
        var inCombatNow = DalamudApi.Conditions.Any(ConditionFlag.InCombat);
        var inDutyRecorderPlayback = DalamudApi.Conditions.Any(ConditionFlag.DutyRecorderPlayback);
        var statsEventActive = inCombatNow || Configuration.ReplayStatsMode && inDutyRecorderPlayback;
        if (!statsEventActive)
        {
            timelineService.ObserveAbility(actionId, nowUtc, sourceId);
            return;
        }

        var zoneName = GetPlaceName(DalamudApi.GetTerritoryTypeId());
        var actionName = GetActionName(actionId);
        var isLimitBreakAction = IsLimitBreakAction(actionId);
        var sourceActorId = ResolveTrackedSourceActorId(sourceId, sourceCharacterAddress, nowUtc, out var sourceCanResolveToTrackedActor);
        var sourceObject = sourceCanResolveToTrackedActor
            ? null
            : ResolveEventActorObject(sourceId, sourceCharacterAddress);
        var isKnownPlayerDotAction = PlayerDotCatalog.IsKnownPlayerDotAction(actionId);

        var hasTrackedParticipant = sourceCanResolveToTrackedActor;
        var hasCombatStartingTrackedEffect = false;
        var anyTargetTracked = false;
        uint firstTargetId = 0;
        var debugTargetIds = new List<uint>(header->NumTargets);
        long debugTotalDamageToTrackedTargets = 0;

        for (var targetIndex = 0; targetIndex < header->NumTargets; targetIndex++)
        {
            var targetId = NormalizeEventActorId(targets[targetIndex]);
            if (targetId == 0)
                continue;

            debugTargetIds.Add(targetId);

            var resolvedTargetActorId = targetId;
            if (firstTargetId == 0)
                firstTargetId = targetId;

            var targetIsTrackedActor = statsService.IsTrackedActor(resolvedTargetActorId);
            if (!targetIsTrackedActor)
            {
                var targetObject = DalamudApi.ObjectTable.SearchByEntityId(targetId);
                if (TryObserveFriendlyCombatant(targetId, targetObject, out var observedTargetActorId))
                {
                    resolvedTargetActorId = observedTargetActorId != 0 ? observedTargetActorId : targetId;
                    targetIsTrackedActor = statsService.IsTrackedActor(resolvedTargetActorId);
                }
            }

            anyTargetTracked |= targetIsTrackedActor;
            hasTrackedParticipant |= targetIsTrackedActor;

            if (isLimitBreakAction)
                continue;

            if (isKnownPlayerDotAction && sourceCanResolveToTrackedActor && !targetIsTrackedActor)
                statsService.ObservePotentialPlayerDotApplication(sourceActorId, resolvedTargetActorId, actionId, actionName, nowUtc);

            for (var effectIndex = 0; effectIndex < 8; effectIndex++)
            {
                ref var effect = ref effects[targetIndex].Effects[effectIndex];
                var effectType = (LocalActionEffectType)effect.Type;

                switch (effectType)
                {
                    case LocalActionEffectType.Damage:
                    case LocalActionEffectType.BlockedDamage:
                    case LocalActionEffectType.ParriedDamage:
                    {
                        var amount = DecodeAmount(effect);
                        if (amount <= 0)
                            break;

                        if (!sourceCanResolveToTrackedActor
                            && TryObserveFriendlyCombatantSource(
                                sourceActorId != 0 ? sourceActorId : NormalizeEventActorId(sourceId),
                                sourceObject,
                                out var observedSourceActorId))
                        {
                            sourceActorId = observedSourceActorId;
                            sourceCanResolveToTrackedActor = true;
                            hasTrackedParticipant = true;
                        }

                        if (sourceCanResolveToTrackedActor
                            && !targetIsTrackedActor)
                        {
                            statsService.ObservePotentialPlayerHostileActionSample(sourceActorId, resolvedTargetActorId, actionId, actionName, amount, IsCritical(effect), IsDirectHit(effect), nowUtc);
                        }

                        if (sourceCanResolveToTrackedActor || targetIsTrackedActor)
                        {
                            hasCombatStartingTrackedEffect = true;
                            if (targetIsTrackedActor)
                                debugTotalDamageToTrackedTargets += amount;
                            statsService.RecordDamage(sourceActorId, resolvedTargetActorId, actionId, actionName, amount, IsCritical(effect), IsDirectHit(effect), nowUtc, zoneName);
                            break;
                        }

                        break;
                    }
                    case LocalActionEffectType.Heal:
                    {
                        var amount = DecodeAmount(effect);
                        if (amount <= 0)
                            break;

                        // 有些攻击技能会在 hostile 目标上带出 Heal 类型的附加效果。
                        // 这类效果不是我方治疗，不能写进 HPS/治疗流水，否则会出现
                        // “玩家 使用攻击技能 治疗 Boss” 这类错误记录。
                        // 当前 HPS 口径只统计目标是已追踪我方对象的治疗。
                        if (!targetIsTrackedActor)
                            break;

                        if (!sourceCanResolveToTrackedActor
                            && TryObserveFriendlyCombatantSource(
                                sourceActorId != 0 ? sourceActorId : NormalizeEventActorId(sourceId),
                                sourceObject,
                                out var observedSourceActorId))
                        {
                            sourceActorId = observedSourceActorId;
                            sourceCanResolveToTrackedActor = true;
                            hasTrackedParticipant = true;
                        }

                        if (sourceCanResolveToTrackedActor || targetIsTrackedActor)
                        {
                            statsService.RecordHeal(sourceActorId, resolvedTargetActorId, actionId, actionName, amount, IsCritical(effect), nowUtc, zoneName);
                        }
                        break;
                    }
                    case LocalActionEffectType.Miss:
                        if (sourceCanResolveToTrackedActor || targetIsTrackedActor)
                        {
                            statsService.RecordFailure(sourceActorId, resolvedTargetActorId, actionId, actionName, isMiss: true, nowUtc, zoneName);
                        }
                        break;
                    case LocalActionEffectType.FullResist:
                    case LocalActionEffectType.Invulnerable:
                    case LocalActionEffectType.PartialInvulnerable:
                        if (sourceCanResolveToTrackedActor || targetIsTrackedActor)
                        {
                            statsService.RecordFailure(sourceActorId, resolvedTargetActorId, actionId, actionName, isMiss: false, nowUtc, zoneName);
                        }
                        break;
                }
            }
        }

        if (hasTrackedParticipant && (hasCombatStartingTrackedEffect || inCombatNow))
            statsService.RecordEncounterActivity(zoneName, nowUtc);
        else if (inCombatNow)
            DebugLogUntrackedCombatEvent(sourceId, sourceCharacterAddress, firstTargetId, sourceCanResolveToTrackedActor, anyTargetTracked, actionName);

        if (sourceCanResolveToTrackedActor)
        {
            monitorService.RecordSkillUse(sourceActorId, actionId, nowUtc);
            tankInvulnerabilityTtsService.ObserveAction(
                actionId,
                nowUtc,
                statsService.IsTrackedPlayerSource(sourceActorId, nowUtc, includeLocalPlayer: false));
        }

        timelineService.ObserveAbility(actionId, nowUtc, sourceId);

    }

    private unsafe delegate void ReceiveAbilityDelegate(
        uint sourceId,
        nint sourceCharacter,
        nint pos,
        ActionEffectHandler.Header* effectHeader,
        ActionEffectHandler.TargetEffects* effectArray,
        GameObjectId* effectTrail);

    private enum LocalActionEffectType : byte
    {
        Miss = 1,
        FullResist = 2,
        Damage = 3,
        Heal = 4,
        BlockedDamage = 5,
        ParriedDamage = 6,
        Invulnerable = 7,
        PartialInvulnerable = 74,
    }
}
