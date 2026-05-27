using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
    private void RemoveActiveWildfiresForTargetLocked(uint targetActorId)
    {
        if (targetActorId is 0 or InvalidActorId || activeWildfires.Count == 0)
            return;

        var staleKeys = activeWildfires.Keys
            .Where(key => AreEquivalentActorIds(key.TargetActorId, targetActorId))
            .ToList();
        foreach (var key in staleKeys)
            activeWildfires.Remove(key);
    }

    private void CaptureActiveWildfiresForHostileTargetLocked(IBattleNpc hostileTarget, DateTime nowUtc)
    {
        var targetActorId = ResolveBattleCharaActorId(hostileTarget);
        if (targetActorId is 0 or InvalidActorId)
            return;

        var seenKeys = new HashSet<PlayerWildfireKey>();
        foreach (var status in EnumerateStatusEntries(hostileTarget))
        {
            try
            {
                if (!TryCreateOrRefreshActiveWildfireStateLocked(status, targetActorId, nowUtc, out var key))
                    continue;

                seenKeys.Add(key);
            }
            catch (Exception ex)
            {
                LogHelper.Debug(
                    "统计",
                    ex,
                    $"读取野火状态条目失败：targetId=0x{targetActorId:X8}。");
            }
        }

        var disappearedStates = activeWildfires.Values
            .Where(state =>
                AreEquivalentActorIds(state.Key.TargetActorId, targetActorId)
                && !seenKeys.Contains(state.Key))
            .ToList();
        foreach (var state in disappearedStates)
        {
            if (state.DetonationRecorded
                || !hostileTarget.IsTargetable
                || nowUtc - state.LastSeenUtc < WildfireStatusGracePeriod)
                continue;

            _ = TryRecordWildfireDetonationLocked(state, nowUtc);
        }
    }

    private bool TryCreateOrRefreshActiveWildfireStateLocked(object status, uint targetActorId, DateTime nowUtc, out PlayerWildfireKey key)
    {
        key = default;

        var statusId = GetStatusId(status);
        if (statusId != WildfireStatusId)
            return false;

        var rawSourceActorId = ResolveStatusSourceActorId(status);
        if (!TryResolveTrackedSource(rawSourceActorId, nowUtc, out var source) || source.Kind != TrackedActorKind.Player)
            return false;

        var statusName = TryGetStatusGameDataText(status, "Name");
        var actionName = string.IsNullOrWhiteSpace(statusName)
            ? "野火"
            : NormalizeActionName(statusName);
        var remainingTimeSeconds = Math.Max(0f, GetStatusRemainingTime(status));
        var stackCount = ResolveWildfireStackCount(status);

        key = new PlayerWildfireKey(targetActorId, source.ActorId, statusId);
        if (activeWildfires.TryGetValue(key, out var existing))
        {
            var isRefresh = remainingTimeSeconds > existing.RemainingTimeSeconds + 0.5f
                            || nowUtc - existing.ExpectedDetonationUtc > WildfireStatusGracePeriod;
            if (isRefresh)
                existing.Reset(source, actionName, nowUtc, remainingTimeSeconds, stackCount);
            else
                existing.Refresh(source, actionName, nowUtc, remainingTimeSeconds, stackCount);

            return true;
        }

        activeWildfires[key] = new ActiveWildfireState(
            key,
            source,
            actionName,
            nowUtc,
            remainingTimeSeconds,
            stackCount);
        return true;
    }

    private int ResolveWildfireStackCount(object status)
    {
        var rawStackCount = TryGetStatusParam(status);
        if (rawStackCount <= 0)
            return 0;

        return Math.Clamp(rawStackCount, 0, WildfireMaxWeaponskillCount);
    }

    private void NoteWildfireWeaponskillContributionLocked(
        uint sourceActorId,
        uint targetActorId,
        uint actionId,
        string actionName,
        long observedDamageAmount,
        bool critical,
        bool directHit,
        DateTime timeUtc)
    {
        if (activeWildfires.Count == 0
            || observedDamageAmount <= 0
            || !WildfireAnchorPotencies.TryGetValue(actionId, out var potency))
            return;

        var matchingStates = activeWildfires.Values
            .Where(state =>
                !state.DetonationRecorded
                && AreEquivalentActorIds(state.Key.SourceActorId, sourceActorId)
                && AreEquivalentActorIds(state.Key.TargetActorId, targetActorId)
                && timeUtc <= state.ExpectedDetonationUtc + WildfireDetonationTimingAllowance)
            .ToList();
        foreach (var state in matchingStates)
            state.NoteWeaponskillContribution(actionId, actionName, observedDamageAmount, potency, critical, directHit, timeUtc);
    }

    private void TryRecordPendingWildfireDetonationsLocked(DateTime nowUtc)
    {
        if (activeWildfires.Count == 0)
            return;

        var dueStates = activeWildfires.Values
            .Where(state =>
                !state.DetonationRecorded
                && state.EffectiveStackCount > 0
                && nowUtc + WildfireDetonationTimingAllowance >= state.ExpectedDetonationUtc)
            .ToList();
        foreach (var state in dueStates)
        {
            var detonationTimeUtc = state.ExpectedDetonationUtc <= nowUtc
                ? state.ExpectedDetonationUtc
                : nowUtc;
            _ = TryRecordWildfireDetonationLocked(state, detonationTimeUtc);
        }
    }

    private bool TryRecordWildfireDetonationLocked(ActiveWildfireState state, DateTime timeUtc)
    {
        if (state.DetonationRecorded)
            return false;

        var stackCount = state.EffectiveStackCount;
        if (stackCount <= 0)
            return false;

        var amount = EstimateWildfireDamageLocked(state, stackCount, timeUtc);
        if (amount <= 0)
            return false;

        var loggedTargetName = ResolveCombatTimelineTargetName(state.Key.TargetActorId, timeUtc);
        var encounterActionName = NormalizeActionName(state.ActionName);
        var wildfireActionText = FormatActionNameWithId(encounterActionName, WildfireActionId);
        var wasStarted = currentEncounter.Started;
        var contributionSummary = BuildWildfireContributionSummary(state);

        currentEncounter.RecordOutgoingDamage(state.Source, encounterActionName, amount, false, false, timeUtc);
        AppendEncounterStartIfNeededLocked(wasStarted, timeUtc);
        AppendCombatTimelineEntryLocked(
            timeUtc,
            CombatTimelineEntryKind.Damage,
            $"{state.Source.Name} 使用{wildfireActionText} 攻击 {loggedTargetName}，造成 {CreateDamageString(amount, useSuffix: true, useDecimals: true)} 伤害（模拟：{contributionSummary}）。",
            state.Source.Name,
            loggedTargetName,
            actorIsFriendly: true,
            targetIsFriendly: false,
            actionText: wildfireActionText);

        state.DetonationRecorded = true;
        return true;
    }

    private long EstimateWildfireDamageLocked(ActiveWildfireState state, int stackCount, DateTime nowUtc)
    {
        if (stackCount <= 0)
            return 0L;

        if (TryEstimateWildfireDamageFromContributionSamplesLocked(state, stackCount, out var contributionEstimatedDamage))
            return contributionEstimatedDamage;

        if (!TryResolveWildfireAnchorActionLocked(state.Key.SourceActorId, state.Key.TargetActorId, nowUtc, out var observedDamageAmount, out var anchorPotency))
            return 0L;

        var wildfirePotency = stackCount * WildfirePotencyPerWeaponskill;
        if (wildfirePotency <= 0 || anchorPotency <= 0)
            return 0L;

        return Math.Max(1L, (long)Math.Round(
            observedDamageAmount
            * (wildfirePotency / (double)anchorPotency)
            * WildfireDotLikeDamageScale));
    }

    private bool TryEstimateWildfireDamageFromContributionSamplesLocked(ActiveWildfireState state, int stackCount, out long estimatedDamage)
    {
        estimatedDamage = 0L;
        if (stackCount <= 0)
            return false;

        var normalizedDamagePerPotencySamples = state.ContributionSamples
            .Where(sample => sample.Potency > 0 && sample.ObservedDamageAmount > 0)
            .Select(static sample => sample.GetNormalizedDamagePerPotency())
            .Where(value => value > 0d)
            .OrderBy(value => value)
            .ToList();
        if (normalizedDamagePerPotencySamples.Count == 0)
            return false;

        var effectiveSamples = normalizedDamagePerPotencySamples.Count >= 3
            ? normalizedDamagePerPotencySamples.Skip(1).Take(normalizedDamagePerPotencySamples.Count - 2).ToList()
            : normalizedDamagePerPotencySamples;
        if (effectiveSamples.Count == 0)
            effectiveSamples = normalizedDamagePerPotencySamples;

        var wildfirePotency = stackCount * WildfirePotencyPerWeaponskill;
        if (wildfirePotency <= 0)
            return false;

        var averageDamagePerPotency = effectiveSamples.Average();
        if (averageDamagePerPotency <= 0d)
            return false;

        estimatedDamage = Math.Max(1L, (long)Math.Round(
            averageDamagePerPotency
            * wildfirePotency
            * WildfireDotLikeDamageScale));
        return estimatedDamage > 0L;
    }

    private static string BuildWildfireContributionSummary(ActiveWildfireState state)
    {
        var stackCount = state.EffectiveStackCount;
        if (state.ContributionSamples.Count == 0)
            return $"层数 {stackCount}，无有效样本";

        var groupedSamples = state.ContributionSamples
            .GroupBy(static sample => sample.ActionName)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(3)
            .Select(static group => $"{group.Key}×{group.Count()}");
        return $"层数 {stackCount}，样本 {string.Join("、", groupedSamples)}";
    }

    private bool TryResolveWildfireAnchorActionLocked(uint sourceActorId, uint targetActorId, DateTime nowUtc, out long observedDamageAmount, out int anchorPotency)
    {
        observedDamageAmount = 0L;
        anchorPotency = 0;

        static bool TryResolveAnchorPotency(RecentHostilePlayerAction action, out int potency)
            => WildfireAnchorPotencies.TryGetValue(action.ActionId, out potency);

        var targetMatch = recentHostilePlayerActions
            .Where(action =>
                AreEquivalentActorIds(action.Source.ActorId, sourceActorId)
                && AreEquivalentActorIds(action.TargetActorId, targetActorId)
                && action.ObservedDamageAmount > 0
                && nowUtc - action.ObservedAtUtc <= PlayerDotRecentActionTtl
                && TryResolveAnchorPotency(action, out _))
            .OrderByDescending(action => action.ObservedAtUtc)
            .FirstOrDefault();
        if (targetMatch != null && TryResolveAnchorPotency(targetMatch, out anchorPotency))
        {
            observedDamageAmount = targetMatch.ObservedDamageAmount;
            return true;
        }

        var sourceOnlyMatch = recentHostilePlayerActions
            .Where(action =>
                AreEquivalentActorIds(action.Source.ActorId, sourceActorId)
                && action.ObservedDamageAmount > 0
                && nowUtc - action.ObservedAtUtc <= PlayerDotRecentActionTtl
                && TryResolveAnchorPotency(action, out _))
            .OrderByDescending(action => action.ObservedAtUtc)
            .FirstOrDefault();
        if (sourceOnlyMatch != null && TryResolveAnchorPotency(sourceOnlyMatch, out anchorPotency))
        {
            observedDamageAmount = sourceOnlyMatch.ObservedDamageAmount;
            return true;
        }

        return false;
    }

    private void TrimInactiveWildfiresLocked(DateTime nowUtc)
    {
        if (activeWildfires.Count == 0)
            return;

        var staleKeys = activeWildfires
            .Where(pair =>
            {
                var state = pair.Value;
                if (state.DetonationRecorded)
                    return nowUtc - state.LastSeenUtc > PlayerDotStatusGracePeriod;

                return nowUtc - state.LastSeenUtc > PlayerDotStatusGracePeriod
                       && nowUtc - state.ExpectedDetonationUtc > PlayerDotStatusGracePeriod;
            })
            .Select(pair => pair.Key)
            .ToList();
        foreach (var key in staleKeys)
            activeWildfires.Remove(key);
    }
}
