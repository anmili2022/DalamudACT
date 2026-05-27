using System;
using System.Collections.Generic;
using System.Linq;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
    private readonly record struct ActionDescriptionDotPotencyEntry(uint ActionId, int SeedPotency, int DotTickPotency);

    private sealed class WildfireContributionSample
    {
        public WildfireContributionSample(
            uint actionId,
            string actionName,
            int potency,
            long observedDamageAmount,
            bool observedCritical,
            bool observedDirectHit,
            DateTime observedAtUtc)
        {
            ActionId = actionId;
            ActionName = string.IsNullOrWhiteSpace(actionName)
                ? $"技能 {actionId}"
                : actionName;
            Potency = potency;
            ObservedDamageAmount = observedDamageAmount;
            ObservedCritical = observedCritical;
            ObservedDirectHit = observedDirectHit;
            ObservedAtUtc = observedAtUtc;
        }

        public uint ActionId { get; set; }

        public string ActionName { get; }

        public int Potency { get; }

        public long ObservedDamageAmount { get; }

        public bool ObservedCritical { get; }

        public bool ObservedDirectHit { get; }

        public DateTime ObservedAtUtc { get; }

        public double GetNormalizedDamagePerPotency()
        {
            if (ObservedDamageAmount <= 0 || Potency <= 0)
                return 0d;

            var normalizedDamage = ObservedDamageAmount / (ObservedCritical ? ObservedPlayerDotCriticalHitMultiplier : 1d);
            if (ObservedDirectHit)
                normalizedDamage /= ObservedPlayerDotDirectHitMultiplier;

            return normalizedDamage / Potency;
        }
    }

    private sealed class RecentHostilePlayerAction
    {
        public RecentHostilePlayerAction(
            TrackedActor source,
            uint targetActorId,
            uint actionId,
            string actionName,
            DateTime observedAtUtc)
        {
            Source = source;
            TargetActorId = targetActorId;
            ActionId = actionId;
            ActionName = actionName;
            ObservedAtUtc = observedAtUtc;
        }

        public TrackedActor Source { get; }

        public uint TargetActorId { get; }

        public uint ActionId { get; set; }

        public string ActionName { get; }

        public DateTime ObservedAtUtc { get; }

        public long ObservedDamageAmount { get; set; }

        public bool? ObservedCritical { get; set; }

        public bool? ObservedDirectHit { get; set; }
    }

    private readonly record struct PlayerDotKey(uint TargetActorId, uint SourceActorId, uint StatusId);

    private readonly record struct PlayerWildfireKey(uint TargetActorId, uint SourceActorId, uint StatusId);

    private sealed class ActivePlayerDotState
    {
        public ActivePlayerDotState(
            PlayerDotKey key,
            TrackedActor source,
            uint actionId,
            string actionName,
            string statusName,
            int statusPotency,
            PlayerDotSkillEntry? skillEntry,
            long estimatedTickDamage,
            bool estimatedTickDamageFromObservedSeed,
            DateTime firstSeenUtc,
            DateTime lastSeenUtc,
            float remainingTimeSeconds)
        {
            Key = key;
            Source = source;
            ActionId = actionId;
            ActionName = actionName;
            StatusName = statusName;
            StatusPotency = statusPotency;
            SkillEntry = skillEntry;
            EstimatedTickDamage = estimatedTickDamage;
            EstimatedTickDamageFromObservedSeed = estimatedTickDamageFromObservedSeed;
            FirstSeenUtc = firstSeenUtc;
            LastSeenUtc = lastSeenUtc;
            RemainingTimeSeconds = remainingTimeSeconds;
            var tickIntervalSeconds = (float)PlayerDotTickInterval.TotalSeconds;
            var startsWithImmediateTick = skillEntry?.StatusOwnerKind == PlayerDotStatusOwnerKind.SourceActor;
            LastAttributedTickUtc = startsWithImmediateTick
                ? firstSeenUtc - PlayerDotTickInterval
                : firstSeenUtc;
            NextTickRemainingTimeSeconds = startsWithImmediateTick
                ? Math.Max(0f, remainingTimeSeconds)
                : Math.Max(0f, remainingTimeSeconds - tickIntervalSeconds);
        }

        public PlayerDotKey Key { get; }

        public TrackedActor Source { get; }

        public uint ActionId { get; set; }

        public string ActionName { get; set; }

        public string StatusName { get; set; }

        public int StatusPotency { get; }

        public PlayerDotSkillEntry? SkillEntry { get; set; }

        public long EstimatedTickDamage { get; set; }

        public bool EstimatedTickDamageFromObservedSeed { get; set; }

        public DateTime FirstSeenUtc { get; }

        public DateTime LastSeenUtc { get; set; }

        public float RemainingTimeSeconds { get; set; }

        public DateTime LastAttributedTickUtc { get; set; }

        public int TickCount { get; set; }

        public float NextTickRemainingTimeSeconds { get; set; }

        public bool FocusedDiagnosticFirstTickLogged { get; set; }
    }

    private sealed class ActiveWildfireState
    {
        private readonly List<WildfireContributionSample> contributionSamples = new();

        public ActiveWildfireState(
            PlayerWildfireKey key,
            TrackedActor source,
            string actionName,
            DateTime firstSeenUtc,
            float remainingTimeSeconds,
            int stackCount)
        {
            Key = key;
            Source = source;
            ActionName = actionName;
            FirstSeenUtc = firstSeenUtc;
            Refresh(source, actionName, firstSeenUtc, remainingTimeSeconds, stackCount);
        }

        public PlayerWildfireKey Key { get; }

        public TrackedActor Source { get; private set; }

        public string ActionName { get; private set; }

        public DateTime FirstSeenUtc { get; private set; }

        public DateTime LastSeenUtc { get; private set; }

        public float RemainingTimeSeconds { get; private set; }

        public DateTime ExpectedDetonationUtc { get; private set; }

        public int LastKnownStackCount { get; private set; }

        public int ObservedWeaponskillCount { get; private set; }

        public bool DetonationRecorded { get; set; }

        public IReadOnlyList<WildfireContributionSample> ContributionSamples => contributionSamples;

        public int EffectiveStackCount
            => Math.Clamp(Math.Max(LastKnownStackCount, ObservedWeaponskillCount), 0, WildfireMaxWeaponskillCount);

        public void Reset(TrackedActor source, string actionName, DateTime nowUtc, float remainingTimeSeconds, int stackCount)
        {
            FirstSeenUtc = nowUtc;
            LastKnownStackCount = 0;
            ObservedWeaponskillCount = 0;
            DetonationRecorded = false;
            contributionSamples.Clear();
            Refresh(source, actionName, nowUtc, remainingTimeSeconds, stackCount);
        }

        public void Refresh(TrackedActor source, string actionName, DateTime nowUtc, float remainingTimeSeconds, int stackCount)
        {
            Source = source;
            ActionName = actionName;
            LastSeenUtc = nowUtc;
            RemainingTimeSeconds = Math.Max(0f, remainingTimeSeconds);
            ExpectedDetonationUtc = nowUtc + TimeSpan.FromSeconds(RemainingTimeSeconds);
            LastKnownStackCount = Math.Clamp(Math.Max(LastKnownStackCount, stackCount), 0, WildfireMaxWeaponskillCount);
        }

        public void NoteWeaponskillContribution(
            uint actionId,
            string actionName,
            long observedDamageAmount,
            int potency,
            bool critical,
            bool directHit,
            DateTime observedAtUtc)
        {
            if (actionId == 0 || observedDamageAmount <= 0 || potency <= 0)
                return;

            var duplicateSample = contributionSamples.Any(sample =>
                sample.ActionId == actionId
                && sample.ObservedAtUtc == observedAtUtc);
            if (duplicateSample)
                return;

            if (contributionSamples.Count < WildfireMaxWeaponskillCount)
            {
                contributionSamples.Add(new WildfireContributionSample(
                    actionId,
                    actionName,
                    potency,
                    observedDamageAmount,
                    critical,
                    directHit,
                    observedAtUtc));
            }

            ObservedWeaponskillCount = Math.Clamp(ObservedWeaponskillCount + 1, 0, WildfireMaxWeaponskillCount);
        }
    }

}
