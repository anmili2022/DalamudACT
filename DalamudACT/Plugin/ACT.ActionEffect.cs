using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
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
        if (isDisposing)
            return;

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
        var perfStart = Configuration.EnableEnhancedLog ? Stopwatch.GetTimestamp() : 0;
        var nowUtc = DateTime.UtcNow;
        if (ShouldSuppressCombatModuleWork)
            return;

        var actionId = header->SpellId != 0 ? (uint)header->SpellId : header->ActionId;
        var inCombatNow = DalamudApi.Conditions.Any(ConditionFlag.InCombat);
        var inDutyRecorderPlayback = DalamudApi.Conditions.Any(ConditionFlag.DutyRecorderPlayback);
        var statsModuleEnabled = IsStatsModuleEnabled;
        var partyMonitorModuleEnabled = IsPartyMonitorModuleEnabled;
        var timelineModuleEnabled = IsTimelineModuleEnabled;
        var statsEventActive = statsModuleEnabled && (inCombatNow || Configuration.ReplayStatsMode && inDutyRecorderPlayback);
        var partyMonitorEventActive = partyMonitorModuleEnabled && inCombatNow;
        var highPerformanceMode = Configuration.HighPerformanceMode;
        var shouldObserveTimelineAbility = timelineModuleEnabled
                                           && !highPerformanceMode
                                           && Configuration.CurrentAreaKind != RuntimeAreaKind.Duty;
        var shouldInspectAbility = statsEventActive || partyMonitorEventActive;
        if (!shouldInspectAbility)
        {
            if (shouldObserveTimelineAbility)
                timelineService.ObserveAbility(actionId, nowUtc, sourceId);
            return;
        }

        if (!statsEventActive)
        {
            HandlePartyMonitorAbilityOnly(header, actionId, sourceId, sourceCharacterAddress, nowUtc);
            if (shouldObserveTimelineAbility)
                timelineService.ObserveAbility(actionId, nowUtc, sourceId);
            return;
        }

        var zoneName = GetPlaceName(DalamudApi.GetTerritoryTypeId());
        string? actionName = null;
        var isLimitBreakAction = IsLimitBreakAction(actionId);
        var sourceActorId = ResolveTrackedSourceActorId(sourceId, sourceCharacterAddress, nowUtc, out var sourceCanResolveToTrackedActor);
        var sourceObject = sourceCanResolveToTrackedActor
            ? null
            : ResolveEventActorObject(sourceId, sourceCharacterAddress);
        var triedResolveFriendlySource = false;
        var shouldRunDotAndWildfireAttribution = Configuration.EnableDotAndWildfireAttribution && !highPerformanceMode;
        var isKnownPlayerDotAction = shouldRunDotAndWildfireAttribution && PlayerDotCatalog.IsKnownPlayerDotAction(actionId);

        var hasTrackedParticipant = sourceCanResolveToTrackedActor;
        var hasCombatStartingTrackedEffect = false;
        var anyTargetTracked = false;
        uint firstTargetId = 0;
        var debugEnabled = LogHelper.IsDebugEnabled(DebugLogModule.DamageStats);
        List<uint>? debugTargetIds = debugEnabled ? new List<uint>(header->NumTargets) : null;
        long debugTotalDamageToTrackedTargets = 0;

        for (var targetIndex = 0; targetIndex < header->NumTargets; targetIndex++)
        {
            var targetId = NormalizeEventActorId(targets[targetIndex]);
            if (targetId == 0)
                continue;

            debugTargetIds?.Add(targetId);

            var resolvedTargetActorId = targetId;
            if (firstTargetId == 0)
                firstTargetId = targetId;

            var targetIsTrackedActor = statsService.IsTrackedActor(resolvedTargetActorId);
            var triedResolveTargetObject = false;
            IGameObject? targetObject = null;

            anyTargetTracked |= targetIsTrackedActor;
            hasTrackedParticipant |= targetIsTrackedActor;

            if (highPerformanceMode && !sourceCanResolveToTrackedActor && !targetIsTrackedActor)
                continue;

            if (isLimitBreakAction)
                continue;

            if (isKnownPlayerDotAction && sourceCanResolveToTrackedActor && !targetIsTrackedActor)
                statsService.ObservePotentialPlayerDotApplication(sourceActorId, resolvedTargetActorId, actionId, GetCurrentActionName(), nowUtc);

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

                        if (!sourceCanResolveToTrackedActor && targetIsTrackedActor && TryResolveFriendlySourceOnce(out var observedSourceActorId))
                        {
                            sourceActorId = observedSourceActorId;
                            sourceCanResolveToTrackedActor = true;
                            hasTrackedParticipant = true;
                        }

                        if (shouldRunDotAndWildfireAttribution
                            && sourceCanResolveToTrackedActor
                            && !targetIsTrackedActor)
                        {
                            statsService.ObservePotentialPlayerHostileActionSample(sourceActorId, resolvedTargetActorId, actionId, GetCurrentActionName(), amount, IsCritical(effect), IsDirectHit(effect), nowUtc);
                        }

                        if (sourceCanResolveToTrackedActor || targetIsTrackedActor)
                        {
                            hasCombatStartingTrackedEffect = true;
                            if (targetIsTrackedActor)
                                debugTotalDamageToTrackedTargets += amount;
                            statsService.RecordDamage(sourceActorId, resolvedTargetActorId, actionId, GetCurrentActionName(), amount, IsCritical(effect), IsDirectHit(effect), nowUtc, zoneName);
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
                        if (!targetIsTrackedActor && !TryResolveFriendlyTargetOnce(out targetIsTrackedActor))
                            break;

                        if (!sourceCanResolveToTrackedActor && targetIsTrackedActor && TryResolveFriendlySourceOnce(out var observedSourceActorId))
                        {
                            sourceActorId = observedSourceActorId;
                            sourceCanResolveToTrackedActor = true;
                            hasTrackedParticipant = true;
                        }

                        if (sourceCanResolveToTrackedActor || targetIsTrackedActor)
                        {
                            statsService.RecordHeal(sourceActorId, resolvedTargetActorId, actionId, GetCurrentActionName(), amount, IsCritical(effect), nowUtc, zoneName);
                        }
                        break;
                    }
                    case LocalActionEffectType.Miss:
                        if (sourceCanResolveToTrackedActor || targetIsTrackedActor)
                        {
                            statsService.RecordFailure(sourceActorId, resolvedTargetActorId, actionId, GetCurrentActionName(), isMiss: true, nowUtc, zoneName);
                        }
                        break;
                    case LocalActionEffectType.FullResist:
                    case LocalActionEffectType.Invulnerable:
                    case LocalActionEffectType.PartialInvulnerable:
                        if (sourceCanResolveToTrackedActor || targetIsTrackedActor)
                        {
                            statsService.RecordFailure(sourceActorId, resolvedTargetActorId, actionId, GetCurrentActionName(), isMiss: false, nowUtc, zoneName);
                        }
                        break;
                }
            }

            bool TryResolveFriendlyTargetOnce(out bool isTracked)
            {
                isTracked = targetIsTrackedActor;
                if (isTracked)
                    return true;

                if (triedResolveTargetObject)
                    return false;

                triedResolveTargetObject = true;
                targetObject = DalamudApi.ObjectTable.SearchByEntityId(targetId);
                if (!TryObserveFriendlyCombatant(targetId, targetObject, out var observedTargetActorId))
                    return false;

                resolvedTargetActorId = observedTargetActorId != 0 ? observedTargetActorId : targetId;
                isTracked = statsService.IsTrackedActor(resolvedTargetActorId);
                targetIsTrackedActor = isTracked;
                return isTracked;
            }
        }

        if (hasTrackedParticipant && (hasCombatStartingTrackedEffect || inCombatNow))
            statsService.RecordEncounterActivity(zoneName, nowUtc);
        else if (inCombatNow && debugEnabled)
            DebugLogUntrackedCombatEvent(sourceId, sourceCharacterAddress, firstTargetId, sourceCanResolveToTrackedActor, anyTargetTracked, GetCurrentActionName());

        if (sourceCanResolveToTrackedActor)
        {
            if (partyMonitorModuleEnabled)
            {
                monitorService.RecordSkillUse(sourceActorId, actionId, nowUtc);
                tankInvulnerabilityTtsService.ObserveAction(
                    actionId,
                    nowUtc,
                    statsService.IsTrackedPlayerSource(sourceActorId, nowUtc, includeLocalPlayer: false));
            }
        }

        if (shouldObserveTimelineAbility)
            timelineService.ObserveAbility(actionId, nowUtc, sourceId);

        bool TryResolveFriendlySourceOnce(out uint observedSourceActorId)
        {
            observedSourceActorId = 0;
            if (triedResolveFriendlySource)
                return false;

            triedResolveFriendlySource = true;
            return TryObserveFriendlyCombatantSource(
                sourceActorId != 0 ? sourceActorId : NormalizeEventActorId(sourceId),
                sourceObject,
                out observedSourceActorId);
        }

        string GetCurrentActionName()
            => actionName ??= GetActionName(actionId);

        if (perfStart != 0)
        {
            LogActionEffectPerfIfSlow(
                perfStart,
                actionId,
                header->NumTargets,
                sourceCanResolveToTrackedActor,
                anyTargetTracked,
                statsEventActive,
                partyMonitorEventActive,
                timelineModuleEnabled,
                highPerformanceMode);
        }

    }

    private void LogActionEffectPerfIfSlow(
        long startTimestamp,
        uint actionId,
        uint targetCount,
        bool sourceTracked,
        bool anyTargetTracked,
        bool statsEventActive,
        bool partyMonitorEventActive,
        bool timelineModuleEnabled,
        bool highPerformanceMode)
    {
        if (!Configuration.EnableEnhancedLog)
            return;

        var elapsedMs = Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
        if (elapsedMs < 1d)
            return;

        var nowUtc = DateTime.UtcNow;
        if (nowUtc - lastActionEffectPerfLogUtc < TimeSpan.FromSeconds(2))
            return;

        lastActionEffectPerfLogUtc = nowUtc;
        LogHelper.Info(
            "性能",
            $"ActionEffect 慢包 {elapsedMs:0.0}ms：action=0x{actionId:X}，targets={targetCount}，sourceTracked={sourceTracked}，anyTargetTracked={anyTargetTracked}，stats={statsEventActive}，monitor={partyMonitorEventActive}，timeline={timelineModuleEnabled}，highPerf={highPerformanceMode}。");
    }

    private unsafe void HandlePartyMonitorAbilityOnly(
        ActionEffectHandler.Header* header,
        uint actionId,
        uint sourceId,
        nint sourceCharacterAddress,
        DateTime nowUtc)
    {
        if (!IsPartyMonitorModuleEnabled)
            return;

        var sourceActorId = ResolveTrackedSourceActorId(sourceId, sourceCharacterAddress, nowUtc, out var sourceCanResolveToTrackedActor);
        if (!sourceCanResolveToTrackedActor)
            return;

        monitorService.RecordSkillUse(sourceActorId, actionId, nowUtc);
        tankInvulnerabilityTtsService.ObserveAction(
            actionId,
            nowUtc,
            statsService.IsTrackedPlayerSource(sourceActorId, nowUtc, includeLocalPlayer: false));
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
