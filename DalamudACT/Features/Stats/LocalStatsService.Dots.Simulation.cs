using System;
using System.Collections.Generic;
using System.Linq;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
    private void SimulateActivePlayerDotTicksLocked(DateTime nowUtc)
    {
        if (activePlayerDots.Count == 0)
            return;

        var activeDots = activePlayerDots.Values.ToList();
        foreach (var dotState in activeDots)
        {
            if (dotState.RemainingTimeSeconds <= 0f)
                continue;

            if (!TryResolveTrackedSource(dotState.Key.SourceActorId, nowUtc, out var source) || source.Kind != TrackedActorKind.Player)
                continue;

            var ticksDue = ResolvePlayerDotTicksDue(dotState);
            if (ticksDue <= 0)
                continue;

            var tickTimeUtc = dotState.LastAttributedTickUtc;
            for (var index = 0; index < ticksDue; index++)
            {
                tickTimeUtc = tickTimeUtc == default
                    ? nowUtc
                    : tickTimeUtc + PlayerDotTickInterval;

                if (!TryRecordSimulatedPlayerDotTickLocked(dotState, source, tickTimeUtc))
                    break;
            }
        }
    }

    private static int ResolvePlayerDotTicksDue(ActivePlayerDotState dotState)
    {
        var currentRemaining = dotState.RemainingTimeSeconds;
        if (currentRemaining <= 0f)
            return 0;

        var tickThreshold = dotState.NextTickRemainingTimeSeconds;
        var allowance = (float)PlayerDotTickJitterAllowance.TotalSeconds;
        var tickInterval = (float)PlayerDotTickInterval.TotalSeconds;
        var ticksDue = 0;

        while (currentRemaining <= tickThreshold + allowance)
        {
            ticksDue++;
            tickThreshold -= tickInterval;

            if (ticksDue >= 16)
                break;
        }

        return ticksDue;
    }

    private bool TryRecordSimulatedPlayerDotTickLocked(ActivePlayerDotState dotState, TrackedActor source, DateTime tickTimeUtc)
    {
        try
        {
            var amount = dotState.EstimatedTickDamage;
            if (amount <= 0)
            {
                amount = ResolvePlayerDotEstimatedTickDamageLocked(source, dotState.Key.TargetActorId, dotState.ActionId, dotState.ActionName, dotState.StatusPotency, tickTimeUtc, dotState.SkillEntry);
                if (amount > 0)
                    dotState.EstimatedTickDamage = amount;
            }

            if (amount <= 0)
                return false;

            var loggedTargetName = ResolveCombatTimelineTargetName(dotState.Key.TargetActorId, tickTimeUtc);
            var encounterActionName = NormalizeActionName(dotState.ActionName);
            var dotActionName = FormatActionNameWithId(encounterActionName, dotState.ActionId);
            var wasStarted = currentEncounter.Started;
            var resolvedCritical = ResolvePlayerDotCritical(source.ActorId, dotState, reportedCritical: false, tickTimeUtc);
            if (resolvedCritical)
                amount = Math.Max(amount + 1L, (long)Math.Round(amount * SimulatedDotCriticalMultiplier));

            currentEncounter.RecordOutgoingDamage(source, encounterActionName, amount, resolvedCritical, false, tickTimeUtc, isDotDamage: true);
            AppendEncounterStartIfNeededLocked(wasStarted, tickTimeUtc);
            AppendCombatTimelineEntryLocked(
                tickTimeUtc,
                CombatTimelineEntryKind.Damage,
                $"{source.Name} 使用{dotActionName} 攻击 {loggedTargetName}，造成 {CreateDamageString(amount, useSuffix: true, useDecimals: true)} 伤害{FormatSimulatedCriticalSuffix(resolvedCritical)}。",
                source.Name,
                loggedTargetName,
                actorIsFriendly: true,
                targetIsFriendly: false,
                actionText: dotActionName);

            dotState.LastAttributedTickUtc = tickTimeUtc;
            dotState.TickCount++;
            if (IsFocusedPlayerDotDiagnosticState(dotState))
            {
                var tickMessage =
                    $"补算Tick：{BuildFocusedPlayerDotDiagnosticStateText(dotState, tickTimeUtc)}，amount={amount}，crit={resolvedCritical}，tick={dotState.TickCount}，remaining={dotState.RemainingTimeSeconds:0.00}s，next={dotState.NextTickRemainingTimeSeconds:0.00}s。";
                if (!dotState.FocusedDiagnosticFirstTickLogged)
                {
                    dotState.FocusedDiagnosticFirstTickLogged = true;
                    LogFocusedPlayerDotDiagnosticLocked(
                        tickTimeUtc,
                        $"tick-first:0x{dotState.Key.SourceActorId:X8}:0x{dotState.Key.TargetActorId:X8}:0x{dotState.Key.StatusId:X8}",
                        tickMessage);
                }
                else
                {
                    LogFocusedPlayerDotDiagnosticLocked(
                        tickTimeUtc,
                        $"tick:0x{dotState.Key.SourceActorId:X8}:0x{dotState.Key.TargetActorId:X8}:0x{dotState.Key.StatusId:X8}:{dotState.TickCount}",
                        tickMessage,
                        includeRecentSummary: false);
                }
            }

            AdvancePlayerDotTickSchedule(dotState);
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Error(
                "统计",
                ex,
                $"补算玩家 DOT 伤害失败：sourceId=0x{source.ActorId:X8}，targetId=0x{dotState.Key.TargetActorId:X8}，statusId=0x{dotState.Key.StatusId:X8}。");
            return false;
        }
    }

    private void RefreshActivePlayerDotEstimatedDamageLocked(
        uint sourceId,
        uint targetId,
        uint actionId,
        string actionName,
        long observedDamage,
        bool observedCritical,
        bool observedDirectHit,
        DateTime nowUtc)
    {
        if (activePlayerDots.Count == 0)
            return;

        var normalizedActionName = NormalizeActionName(actionName);
        var matchingStates = activePlayerDots.Values
            .Where(state =>
                AreEquivalentActorIds(state.Key.SourceActorId, sourceId)
                && AreEquivalentActorIds(state.Key.TargetActorId, targetId)
                && (state.ActionId == actionId
                    || state.Key.StatusId == actionId
                    || IsUnknownActionName(normalizedActionName)
                    || IsUnknownActionName(state.ActionName)
                    || string.Equals(state.ActionName, normalizedActionName, StringComparison.Ordinal)
                    || string.Equals(state.StatusName, normalizedActionName, StringComparison.Ordinal)))
            .ToList();

        if (matchingStates.Count == 0)
        {
            matchingStates = activePlayerDots.Values
                .Where(state =>
                    AreEquivalentActorIds(state.Key.TargetActorId, targetId)
                    && (state.ActionId == actionId
                        || state.Key.StatusId == actionId
                        || IsUnknownActionName(normalizedActionName)
                        || IsUnknownActionName(state.ActionName)
                        || string.Equals(state.ActionName, normalizedActionName, StringComparison.Ordinal)
                        || string.Equals(state.StatusName, normalizedActionName, StringComparison.Ordinal)))
                .ToList();
        }

        if (matchingStates.Count == 0)
        {
            matchingStates = activePlayerDots.Values
                .Where(state =>
                    AreEquivalentActorIds(state.Key.SourceActorId, sourceId)
                    && AreEquivalentActorIds(state.Key.TargetActorId, targetId)
                    && state.SkillEntry?.Anchors.Any(anchor => anchor.ActionIds.Contains(actionId)) == true)
                .ToList();
        }

        foreach (var state in matchingStates)
        {
            var sourceAverageDamage = ResolveObservedAverageDamage(state.Source.ActorId);
            var estimatedTickDamage = observedDamage > 0
                ? EstimatePlayerDotTickDamageFromObservedDamage(observedDamage, actionId, observedCritical, observedDirectHit, sourceAverageDamage, state.SkillEntry)
                : ResolvePlayerDotEstimatedTickDamageLocked(state.Source, targetId, state.ActionId, state.ActionName, state.StatusPotency, nowUtc, state.SkillEntry);

            if (estimatedTickDamage > 0)
            {
                var previousEstimatedTickDamage = state.EstimatedTickDamage;
                var previousEstimatedFromSeed = state.EstimatedTickDamageFromObservedSeed;
                state.EstimatedTickDamage = estimatedTickDamage;
                state.EstimatedTickDamageFromObservedSeed = observedDamage > 0;
                if (IsFocusedPlayerDotDiagnosticState(state)
                    && (previousEstimatedTickDamage != state.EstimatedTickDamage
                        || previousEstimatedFromSeed != state.EstimatedTickDamageFromObservedSeed))
                {
                    LogFocusedPlayerDotDiagnosticLocked(
                        nowUtc,
                        $"estimate-refresh:0x{state.Key.SourceActorId:X8}:0x{state.Key.TargetActorId:X8}:0x{state.Key.StatusId:X8}:0x{actionId:X8}",
                        $"刷新估算伤害：{BuildFocusedPlayerDotDiagnosticStateText(state, nowUtc)}，observedAction={FormatActionNameWithId(actionName, actionId)}，observedDamage={observedDamage}，crit={observedCritical}，dh={observedDirectHit}，估算 {previousEstimatedTickDamage}->{state.EstimatedTickDamage}，seed={state.EstimatedTickDamageFromObservedSeed}。");
                }
            }
        }
    }


    private void TrimInactivePlayerDotsLocked(DateTime nowUtc)
    {
        if (activePlayerDots.Count == 0)
            return;

        var staleKeys = new List<PlayerDotKey>();
        foreach (var pair in activePlayerDots)
        {
            try
            {
                var state = pair.Value;
                string? staleReason = null;
                if (state.RemainingTimeSeconds <= 0f)
                {
                    staleReason = "剩余时间归零";
                }
                else
                {
                    var targetObject = FindObjectByActorId(pair.Key.TargetActorId);
                    if (targetObject == null)
                        staleReason = "目标对象消失";
                    else if (!targetObject.IsTargetable)
                        staleReason = "目标不可选中";
                }

                if (staleReason != null)
                {
                    if (IsFocusedPlayerDotDiagnosticState(state))
                    {
                        LogFocusedPlayerDotDiagnosticLocked(
                            nowUtc,
                            $"trim:0x{pair.Key.SourceActorId:X8}:0x{pair.Key.TargetActorId:X8}:0x{pair.Key.StatusId:X8}:{staleReason}",
                            $"清理活跃状态：{BuildFocusedPlayerDotDiagnosticStateText(state, nowUtc)}，原因={staleReason}，remaining={state.RemainingTimeSeconds:0.00}s，tick={state.TickCount}。");
                    }

                    staleKeys.Add(pair.Key);
                }
            }
            catch
            {
                if (activePlayerDots.TryGetValue(pair.Key, out var state)
                    && IsFocusedPlayerDotDiagnosticState(state))
                {
                    LogFocusedPlayerDotDiagnosticLocked(
                        nowUtc,
                        $"trim-ex:0x{pair.Key.SourceActorId:X8}:0x{pair.Key.TargetActorId:X8}:0x{pair.Key.StatusId:X8}",
                        $"清理活跃状态：{BuildFocusedPlayerDotDiagnosticStateText(state, nowUtc)}，原因=检查异常，tick={state.TickCount}。");
                }

                staleKeys.Add(pair.Key);
            }
        }

        foreach (var key in staleKeys)
            activePlayerDots.Remove(key);
    }

    private string? ResolveRecentPlayerDotActionNameLocked(uint sourceActorId, uint targetActorId, DateTime nowUtc)
    {
        return recentHostilePlayerActions
            .Where(action =>
                AreEquivalentActorIds(action.Source.ActorId, sourceActorId)
                && AreEquivalentActorIds(action.TargetActorId, targetActorId)
                && nowUtc - action.ObservedAtUtc <= PlayerDotRecentActionTtl)
            .OrderByDescending(action => action.ObservedAtUtc)
            .Select(action => action.ActionName)
            .FirstOrDefault(actionName => !string.IsNullOrWhiteSpace(actionName));
    }

    private static bool IsPlayerDotTickReady(ActivePlayerDotState dotState, DateTime nowUtc)
        => nowUtc - dotState.LastAttributedTickUtc >= PlayerDotTickInterval - PlayerDotTickJitterAllowance;

    private static void AdvancePlayerDotTickSchedule(ActivePlayerDotState dotState)
        => dotState.NextTickRemainingTimeSeconds -= (float)PlayerDotTickInterval.TotalSeconds;

    private bool ResolvePlayerDotCritical(uint sourceActorId, ActivePlayerDotState dotState, bool reportedCritical, DateTime tickTimeUtc)
    {
        if (reportedCritical)
            return true;

        var critRate = ResolveObservedCritRate(sourceActorId);
        return IsSimulatedCritical(sourceActorId, dotState.Key.TargetActorId, dotState.Key.StatusId, dotState.TickCount, tickTimeUtc, critRate);
    }

    private double ResolveObservedCritRate(uint sourceActorId)
    {
        var combatant = currentEncounter.Combatants
            .FirstOrDefault(combatant => combatant.ActorId == sourceActorId);

        if (combatant == null || combatant.DirectDamageHits < 20)
            return 0.25d;

        var critRate = combatant.DirectDamageCritHits / (double)Math.Max(1, combatant.DirectDamageHits);
        return Math.Clamp(critRate, 0.05d, 0.95d);
    }

    private static bool IsSimulatedCritical(uint sourceActorId, uint targetActorId, uint statusId, int tickIndex, DateTime tickTimeUtc, double critRate)
    {
        if (critRate <= 0d)
            return false;

        if (critRate >= 1d)
            return true;

        unchecked
        {
            uint hash = 2166136261;
            var tickSeed = (ulong)(tickTimeUtc.ToUniversalTime().Ticks / PlayerDotTickInterval.Ticks);
            hash = (hash ^ sourceActorId) * 16777619;
            hash = (hash ^ targetActorId) * 16777619;
            hash = (hash ^ statusId) * 16777619;
            hash = (hash ^ (uint)tickSeed) * 16777619;
            hash = (hash ^ (uint)(tickSeed >> 32)) * 16777619;
            hash = (hash ^ (uint)tickIndex) * 16777619;

            var sample = hash / (double)uint.MaxValue;
            return sample < critRate;
        }
    }
}
