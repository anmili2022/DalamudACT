using System;
using System.Globalization;
using System.Linq;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
    private long EstimatePlayerDotTickDamageFromObservedDamage(
        long observedDamage,
        uint observedActionId,
        bool observedCritical,
        bool observedDirectHit,
        long sourceAverageDamage,
        PlayerDotSkillEntry? skillEntry)
    {
        if (observedDamage <= 0)
            return 0L;

        var observedActionMatchesSkill = MatchesPlayerDotObservedAction(skillEntry, observedActionId);

        if (TryEstimatePlayerDotTickDamageFromPotencyRatio(observedDamage, observedActionId, observedCritical, observedDirectHit, skillEntry, out var potencyEstimatedTickDamage))
            return potencyEstimatedTickDamage;

        if ((skillEntry == null || observedActionMatchesSkill)
            && !ShouldDisableAverageFallback(skillEntry)
            && TryEstimatePlayerDotTickDamageFromAveragePotencyRatio(sourceAverageDamage, skillEntry, out var averagePotencyEstimatedTickDamage))
            return averagePotencyEstimatedTickDamage;

        if (skillEntry != null)
            return 0L;

        var divisor = observedCritical ? 4d : 3d;
        if (observedDirectHit)
            divisor *= ObservedPlayerDotDirectHitMultiplier;

        var estimatedFromObserved = (long)Math.Round(observedDamage / divisor);
        if (sourceAverageDamage > 0)
        {
            var estimatedFromAverage = (long)Math.Round(sourceAverageDamage / 3d);
            if (estimatedFromAverage > 0)
                estimatedFromObserved = (long)Math.Round((estimatedFromObserved + estimatedFromAverage) / 2d);
        }

        return Math.Max(1L, estimatedFromObserved);
    }

    private bool TryEstimatePlayerDotTickDamageFromPotencyRatio(
        long observedDamage,
        uint observedActionId,
        bool observedCritical,
        bool observedDirectHit,
        PlayerDotSkillEntry? skillEntry,
        out long estimatedTickDamage)
    {
        estimatedTickDamage = 0L;
        if (observedDamage <= 0 || skillEntry == null)
            return false;

        double potencyRatio;
        if (skillEntry.ActionIds.Contains(observedActionId))
        {
            if (!TryResolvePlayerDotPotencyRatio(observedActionId, skillEntry, out potencyRatio))
                return false;
        }
        else if (skillEntry.StatusIds.Contains(observedActionId))
        {
            if (!skillEntry.DotTickPotency.HasValue || skillEntry.DotTickPotency.Value <= 0)
                return false;

            potencyRatio = 1d;
        }
        else
        {
            var matchedAnchor = skillEntry.Anchors.FirstOrDefault(anchor => anchor.ActionIds.Contains(observedActionId));
            if (matchedAnchor == null || !skillEntry.DotTickPotency.HasValue || matchedAnchor.Potency <= 0 || skillEntry.DotTickPotency.Value <= 0)
                return false;

            potencyRatio = skillEntry.DotTickPotency.Value / (double)matchedAnchor.Potency;
        }

        var normalizedObservedDamage = observedDamage / (observedCritical ? ObservedPlayerDotCriticalHitMultiplier : 1d);
        if (observedDirectHit)
            normalizedObservedDamage /= ObservedPlayerDotDirectHitMultiplier;

        estimatedTickDamage = Math.Max(1L, (long)Math.Round(normalizedObservedDamage * potencyRatio));
        return estimatedTickDamage > 0;
    }

    private static bool TryEstimatePlayerDotTickDamageFromAveragePotencyRatio(
        long sourceAverageDamage,
        PlayerDotSkillEntry? skillEntry,
        out long estimatedTickDamage)
    {
        estimatedTickDamage = 0L;
        if (sourceAverageDamage <= 0 || skillEntry == null)
            return false;

        double potencyRatio;
        if (skillEntry.TryGetPotencyRatio(out potencyRatio))
        {
        }
        else
        {
            var matchedAnchor = skillEntry.Anchors.FirstOrDefault(anchor => anchor.Potency > 0);
            if (matchedAnchor == null || !skillEntry.DotTickPotency.HasValue || skillEntry.DotTickPotency.Value <= 0)
                return false;

            potencyRatio = skillEntry.DotTickPotency.Value / (double)matchedAnchor.Potency;
        }

        estimatedTickDamage = Math.Max(1L, (long)Math.Round(sourceAverageDamage * potencyRatio));
        return estimatedTickDamage > 0;
    }

    private RecentHostilePlayerAction? ResolveRecentPlayerDotActionLocked(uint sourceActorId, uint targetActorId, string actionName, DateTime nowUtc)
    {
        var normalizedActionName = NormalizeActionName(actionName);
        var recentActions = recentHostilePlayerActions
            .Where(action =>
                AreEquivalentActorIds(action.Source.ActorId, sourceActorId)
                && AreEquivalentActorIds(action.TargetActorId, targetActorId)
                && nowUtc - action.ObservedAtUtc <= PlayerDotRecentActionTtl);

        if (!IsUnknownActionName(normalizedActionName))
        {
            var namedMatch = recentActions
                .Where(action => string.Equals(action.ActionName, normalizedActionName, StringComparison.Ordinal))
                .OrderByDescending(action => action.ObservedAtUtc)
                .FirstOrDefault();
            if (namedMatch != null)
                return namedMatch;
        }

        return recentActions
            .OrderByDescending(action => action.ObservedAtUtc)
            .FirstOrDefault();
    }

    private RecentHostilePlayerAction? ResolveRecentPlayerDotObservedActionLocked(
        uint sourceActorId,
        uint targetActorId,
        string actionName,
        DateTime nowUtc,
        PlayerDotSkillEntry? skillEntry)
    {
        if (skillEntry != null)
        {
            var skillAction = ResolveRecentPlayerDotSkillActionLocked(sourceActorId, targetActorId, actionName, nowUtc, skillEntry);
            if (skillAction?.ObservedDamageAmount > 0)
                return skillAction;

            return ResolveRecentPlayerDotAnchorActionLocked(sourceActorId, targetActorId, nowUtc, skillEntry);
        }

        var recentAction = ResolveRecentPlayerDotActionLocked(sourceActorId, targetActorId, actionName, nowUtc);
        if (recentAction?.ObservedDamageAmount > 0)
            return recentAction;

        return ResolveRecentPlayerDotAnchorActionLocked(sourceActorId, targetActorId, nowUtc, skillEntry);
    }

    private RecentHostilePlayerAction? ResolveRecentPlayerDotSkillActionLocked(
        uint sourceActorId,
        uint targetActorId,
        string actionName,
        DateTime nowUtc,
        PlayerDotSkillEntry skillEntry)
    {
        var normalizedActionName = NormalizeActionName(actionName);
        var recentActions = recentHostilePlayerActions
            .Where(action =>
                AreEquivalentActorIds(action.Source.ActorId, sourceActorId)
                && AreEquivalentActorIds(action.TargetActorId, targetActorId)
                && nowUtc - action.ObservedAtUtc <= PlayerDotRecentActionTtl);

        var actionIdMatch = recentActions
            .Where(action => skillEntry.ActionIds.Contains(action.ActionId) || skillEntry.StatusIds.Contains(action.ActionId))
            .OrderByDescending(action => action.ObservedAtUtc)
            .FirstOrDefault();
        if (actionIdMatch != null)
            return actionIdMatch;

        if (IsUnknownActionName(normalizedActionName))
            return null;

        return recentActions
            .Where(action => string.Equals(action.ActionName, normalizedActionName, StringComparison.Ordinal))
            .OrderByDescending(action => action.ObservedAtUtc)
            .FirstOrDefault();
    }

    private RecentHostilePlayerAction? ResolveRecentPlayerDotAnchorActionLocked(
        uint sourceActorId,
        uint targetActorId,
        DateTime nowUtc,
        PlayerDotSkillEntry? skillEntry)
    {
        if (skillEntry?.Anchors == null || skillEntry.Anchors.Count == 0)
            return null;

        foreach (var anchor in skillEntry.Anchors)
        {
            var targetMatch = recentHostilePlayerActions
                .Where(action =>
                    AreEquivalentActorIds(action.Source.ActorId, sourceActorId)
                    && AreEquivalentActorIds(action.TargetActorId, targetActorId)
                    && action.ObservedDamageAmount > 0
                    && anchor.ActionIds.Contains(action.ActionId)
                    && nowUtc - action.ObservedAtUtc <= PlayerDotRecentActionTtl)
                .OrderByDescending(action => action.ObservedAtUtc)
                .FirstOrDefault();
            if (targetMatch != null)
                return targetMatch;

            var sourceOnlyMatch = recentHostilePlayerActions
                .Where(action =>
                    AreEquivalentActorIds(action.Source.ActorId, sourceActorId)
                    && action.ObservedDamageAmount > 0
                    && anchor.ActionIds.Contains(action.ActionId)
                    && nowUtc - action.ObservedAtUtc <= PlayerDotRecentActionTtl)
                .OrderByDescending(action => action.ObservedAtUtc)
                .FirstOrDefault();
            if (sourceOnlyMatch != null)
                return sourceOnlyMatch;
        }

        return null;
    }

    private uint ResolveRecentPlayerDotActionIdLocked(uint sourceActorId, uint targetActorId, DateTime nowUtc)
    {
        return recentHostilePlayerActions
            .Where(action =>
                AreEquivalentActorIds(action.Source.ActorId, sourceActorId)
                && AreEquivalentActorIds(action.TargetActorId, targetActorId)
                && nowUtc - action.ObservedAtUtc <= PlayerDotRecentActionTtl)
            .OrderByDescending(action => action.ObservedAtUtc)
            .Select(action => action.ActionId)
            .FirstOrDefault(actionId => actionId != 0);
    }

    private long ResolvePlayerDotEstimatedTickDamageLocked(
        TrackedActor source,
        uint targetActorId,
        uint actionId,
        string actionName,
        int statusPotency,
        DateTime nowUtc,
        PlayerDotSkillEntry? skillEntry = null)
    {
        var normalizedActionName = NormalizeActionName(actionName);
        var recentAction = ResolveRecentPlayerDotObservedActionLocked(source.ActorId, targetActorId, normalizedActionName, nowUtc, skillEntry);

        var sourceAverageDamage = ResolveObservedAverageDamage(source.ActorId);

        if (recentAction?.ObservedDamageAmount > 0)
        {
            var estimatedFromObservedDamage = EstimatePlayerDotTickDamageFromObservedDamage(
                recentAction.ObservedDamageAmount,
                recentAction.ActionId,
                recentAction.ObservedCritical == true,
                recentAction.ObservedDirectHit == true,
                sourceAverageDamage,
                skillEntry);
            if (estimatedFromObservedDamage > 0)
                return estimatedFromObservedDamage;
        }

        if (!ShouldDisableAverageFallback(skillEntry)
            && TryEstimatePlayerDotTickDamageFromAveragePotencyRatio(sourceAverageDamage, skillEntry, out var averagePotencyEstimatedTickDamage))
            return averagePotencyEstimatedTickDamage;

        if (TryEstimatePlayerDotTickDamageFromObservedPotencySamplesLocked(source.ActorId, nowUtc, skillEntry, out var observedPotencySampleEstimatedTickDamage))
            return observedPotencySampleEstimatedTickDamage;

        if (skillEntry != null)
            return 0L;

        if (sourceAverageDamage > 0)
            return Math.Max(1L, (long)Math.Round(sourceAverageDamage / 3d));

        if (statusPotency > 0)
            return Math.Max(1L, Math.Max(500L, statusPotency * 100L));

        return 500L;
    }

    private bool TryEstimatePlayerDotTickDamageFromObservedPotencySamplesLocked(
        uint sourceActorId,
        DateTime nowUtc,
        PlayerDotSkillEntry? skillEntry,
        out long estimatedTickDamage)
    {
        estimatedTickDamage = 0L;
        if (skillEntry?.AllowObservedPotencySampleFallback != true
            || !skillEntry.DotTickPotency.HasValue
            || skillEntry.DotTickPotency.Value <= 0)
        {
            return false;
        }

        var normalizedDamagePerPotencySamples = recentHostilePlayerActions
            .Where(action =>
                AreEquivalentActorIds(action.Source.ActorId, sourceActorId)
                && action.ObservedDamageAmount > 0
                && nowUtc - action.ObservedAtUtc <= PlayerDotRecentActionTtl)
            .Select(action =>
            {
                if (!TryGetActionDescriptionPotency(action.ActionId, out var potency) || potency <= 0)
                    return 0d;

                var normalizedDamage = action.ObservedDamageAmount / (action.ObservedCritical == true ? ObservedPlayerDotCriticalHitMultiplier : 1d);
                if (action.ObservedDirectHit == true)
                    normalizedDamage /= ObservedPlayerDotDirectHitMultiplier;

                return normalizedDamage / potency;
            })
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

        var averageDamagePerPotency = effectiveSamples.Average();
        if (averageDamagePerPotency <= 0d)
            return false;

        estimatedTickDamage = Math.Max(1L, (long)Math.Round(averageDamagePerPotency * skillEntry.DotTickPotency.Value));
        return estimatedTickDamage > 0L;
    }

    private static bool MatchesPlayerDotObservedAction(PlayerDotSkillEntry? skillEntry, uint observedActionId)
    {
        if (skillEntry == null || observedActionId == 0)
            return false;

        if (skillEntry.ActionIds.Contains(observedActionId) || skillEntry.StatusIds.Contains(observedActionId))
            return true;

        return skillEntry.Anchors.Any(anchor => anchor.ActionIds.Contains(observedActionId));
    }

    private static bool ShouldDisableAverageFallback(PlayerDotSkillEntry? skillEntry)
        => skillEntry?.DisableAverageFallback == true;

    private bool TryResolvePlayerDotPotencyRatio(uint observedActionId, PlayerDotSkillEntry? skillEntry, out double potencyRatio)
    {
        potencyRatio = 0d;
        if (skillEntry == null)
            return false;

        if (skillEntry.TryGetPotencyRatio(out potencyRatio))
            return potencyRatio > 0d;

        var preferredActionId = skillEntry.GetPreferredActionId(observedActionId);
        if (preferredActionId == 0)
            return false;

        if (!TryGetActionDescriptionDotPotencies(preferredActionId, out var actionDotPotency))
            return false;

        if (actionDotPotency.SeedPotency <= 0 || actionDotPotency.DotTickPotency <= 0)
            return false;

        potencyRatio = actionDotPotency.DotTickPotency / (double)actionDotPotency.SeedPotency;
        return potencyRatio > 0d;
    }

    private bool TryGetActionDescriptionDotPotencies(uint actionId, out ActionDescriptionDotPotencyEntry entry)
    {
        entry = default;
        if (actionId == 0)
            return false;

        if (actionDescriptionDotPotencyCache.TryGetValue(actionId, out entry))
            return true;

        if (actionDescriptionDotPotencyCacheMisses.Contains(actionId))
            return false;

        if (actionTransientSheet == null)
        {
            actionDescriptionDotPotencyCacheMisses.Add(actionId);
            return false;
        }

        try
        {
            var actionTransient = actionTransientSheet.GetRow(actionId);
            var description = actionTransient.Description.ToString();
            if (TryParseActionDescriptionDotPotencies(description, out var seedPotency, out var dotTickPotency))
            {
                entry = new ActionDescriptionDotPotencyEntry(actionId, seedPotency, dotTickPotency);
                actionDescriptionDotPotencyCache[actionId] = entry;
                return true;
            }
        }
        catch (Exception ex)
        {
            LogHelper.Debug("统计", ex, $"解析动作说明中的 DoT 威力失败：actionId=0x{actionId:X8}。");
        }

        actionDescriptionDotPotencyCacheMisses.Add(actionId);
        return false;
    }

    private bool TryGetActionDescriptionPotency(uint actionId, out int potency)
    {
        potency = 0;
        if (actionId == 0)
            return false;

        if (actionDescriptionPotencyCache.TryGetValue(actionId, out potency))
            return potency > 0;

        if (actionDescriptionPotencyCacheMisses.Contains(actionId))
            return false;

        if (actionTransientSheet == null)
        {
            actionDescriptionPotencyCacheMisses.Add(actionId);
            return false;
        }

        try
        {
            var actionTransient = actionTransientSheet.GetRow(actionId);
            var description = actionTransient.Description.ToString();
            var normalizedDescription = description.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            var directMatch = ActionDescriptionPotencyRegex.Match(normalizedDescription);
            if (directMatch.Success
                && int.TryParse(directMatch.Groups["potency"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out potency)
                && potency > 0)
            {
                actionDescriptionPotencyCache[actionId] = potency;
                return true;
            }
        }
        catch (Exception ex)
        {
            LogHelper.Debug("统计", ex, $"解析动作说明中的威力失败：actionId=0x{actionId:X8}。");
        }

        actionDescriptionPotencyCacheMisses.Add(actionId);
        return false;
    }

    private static bool TryParseActionDescriptionDotPotencies(string? description, out int seedPotency, out int dotTickPotency)
    {
        seedPotency = 0;
        dotTickPotency = 0;
        if (string.IsNullOrWhiteSpace(description))
            return false;

        var normalizedDescription = description.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        if (normalizedDescription.Length == 0)
            return false;

        var dotMatch = ActionDescriptionDotPotencyRegex.Match(normalizedDescription);
        if (!dotMatch.Success || !int.TryParse(dotMatch.Groups["potency"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out dotTickPotency) || dotTickPotency <= 0)
            return false;

        var directMatch = ActionDescriptionPotencyRegex.Match(normalizedDescription);
        if (!directMatch.Success || !int.TryParse(directMatch.Groups["potency"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out seedPotency) || seedPotency <= 0)
        {
            dotTickPotency = 0;
            return false;
        }

        return true;
    }

    private long ResolveObservedAverageDamage(uint sourceActorId)
    {
        var combatant = currentEncounter.Combatants
            .FirstOrDefault(combatant => combatant.ActorId == sourceActorId);

        if (combatant == null || combatant.Hits < 20)
            return 0L;

        return Math.Max(1L, (long)Math.Round(combatant.Damage / (double)Math.Max(1, combatant.Hits)));
    }
}
