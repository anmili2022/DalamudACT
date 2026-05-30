using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
    public sealed record CombatTimelineEntry(
        DateTime TimestampLocal,
        CombatTimelineEntryKind Kind,
        string Message,
        string? ActorName,
        string? TargetName,
        bool ActorIsFriendly,
        bool TargetIsFriendly,
        string? ActionText = null);

    public enum CombatTimelineEntryKind
    {
        CombatStart,
        Damage,
        Heal,
        Cast,
        Failure,
        Death,
        Status,
        CombatEnd,
        MapEffect,
    }

    private readonly record struct CombatTimelineStatusKey(
        uint TargetActorId,
        uint StatusId,
        uint SourceActorId,
        bool IsDebuff);

    private sealed class EncounterSession
    {
        private readonly Dictionary<uint, CombatantSession> combatants = new();

        public DateTime StartUtc { get; private set; }

        public DateTime LastEventUtc { get; private set; }

        public DateTime EndUtc { get; private set; }

        public string ZoneName { get; set; } = "未知区域";

        public bool Started => StartUtc != default;

        public bool HasMeaningfulData => combatants.Values.Any(static combatant =>
            combatant.Damage > 0
            || combatant.Healed > 0
            || combatant.DamageTaken > 0
            || combatant.HealsTaken > 0
            || combatant.Deaths > 0
            || combatant.Swings > 0
            || combatant.Heals > 0);

        public IReadOnlyCollection<CombatantSession> Combatants => combatants.Values;

        public double DurationSeconds
        {
            get
            {
                if (!Started)
                    return 1d;

                var endUtc = EndUtc == default ? LastEventUtc : EndUtc;
                var seconds = (endUtc - StartUtc).TotalSeconds;
                return seconds < 1d ? 1d : seconds;
            }
        }

        public void MarkActivity(DateTime timeUtc)
        {
            if (!Started)
                StartUtc = timeUtc;

            if (LastEventUtc < timeUtc)
                LastEventUtc = timeUtc;

            if (EndUtc < timeUtc)
                EndUtc = timeUtc;
        }

        public void RecordOutgoingDamage(
            TrackedActor source,
            string actionName,
            long amount,
            bool critical,
            bool directHit,
            DateTime timeUtc,
            bool isDotDamage = false)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(source).NoteOutgoingDamage(actionName, amount, critical, directHit, timeUtc, isDotDamage);
        }

        public void RecordIncomingDamage(TrackedActor target, long amount, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(target).NoteIncomingDamage(amount, timeUtc);
        }

        public void RecordOutgoingHeal(TrackedActor source, long amount, bool critical, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(source).NoteOutgoingHeal(amount, critical, timeUtc);
        }

        public void RecordIncomingHeal(TrackedActor target, long amount, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(target).NoteIncomingHeal(amount, timeUtc);
        }

        public void RecordFailedSwing(TrackedActor source, bool isMiss, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(source).NoteFailedSwing(isMiss, timeUtc);
        }

        public void RecordDeath(TrackedActor target, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(target).NoteDeath(timeUtc);
        }

        private CombatantSession EnsureCombatant(TrackedActor actor)
        {
            if (combatants.TryGetValue(actor.ActorId, out var existing))
            {
                existing.RefreshIdentity(actor);
                return existing;
            }

            var created = new CombatantSession(actor);
            combatants[actor.ActorId] = created;
            return created;
        }
    }

    private sealed class CombatantSession
    {
        public CombatantSession(TrackedActor actor)
        {
            ActorId = actor.ActorId;
            Name = actor.Name;
            JobId = actor.JobId;
            JobName = actor.JobName;
            Kind = actor.Kind;
        }

        public uint ActorId { get; }

        public string Name { get; private set; }

        public uint JobId { get; private set; }

        public string JobName { get; private set; }

        public TrackedActorKind Kind { get; private set; }

        public long Damage { get; private set; }

        public long Healed { get; private set; }

        public long DamageTaken { get; private set; }

        public long DotDamage { get; private set; }

        public long HealsTaken { get; private set; }

        public int Swings { get; private set; }

        public int Hits { get; private set; }

        public int CritHits { get; private set; }

        public int CritDirectHits { get; private set; }

        public int DirectDamageHits { get; private set; }

        public int DirectDamageCritHits { get; private set; }

        public int Misses { get; private set; }

        public int HitFailed { get; private set; }

        public int Heals { get; private set; }

        public int CritHeals { get; private set; }

        public int Deaths { get; private set; }

        public DateTime FirstEventUtc { get; private set; }

        public DateTime LastEventUtc { get; private set; }

        public long MaxHitValue { get; private set; }

        public string MaxHitActionName { get; private set; } = string.Empty;

        public double PersonalDurationSeconds
        {
            get
            {
                if (FirstEventUtc == default || LastEventUtc <= FirstEventUtc)
                    return 1d;

                var seconds = (LastEventUtc - FirstEventUtc).TotalSeconds;
                return seconds < 1d ? 1d : seconds;
            }
        }

        public void RefreshIdentity(TrackedActor actor)
        {
            if (!string.IsNullOrWhiteSpace(actor.Name))
                Name = actor.Name;

            if (actor.JobId != 0)
                JobId = actor.JobId;

            if (!string.IsNullOrWhiteSpace(actor.JobName))
                JobName = actor.JobName;

            if (actor.Kind != TrackedActorKind.Unknown)
                Kind = actor.Kind;
        }

        public void NoteOutgoingDamage(string actionName, long amount, bool critical, bool directHit, DateTime timeUtc, bool isDotDamage)
        {
            Touch(timeUtc);
            Damage += amount;
            if (isDotDamage)
                DotDamage += amount;
            Swings++;
            Hits++;
            if (critical)
                CritHits++;
            if (critical && directHit)
                CritDirectHits++;
            if (!isDotDamage)
            {
                DirectDamageHits++;
                if (critical)
                    DirectDamageCritHits++;
            }

            if (amount > MaxHitValue)
            {
                MaxHitValue = amount;
                MaxHitActionName = actionName;
            }
        }

        public void NoteIncomingDamage(long amount, DateTime timeUtc)
        {
            Touch(timeUtc);
            DamageTaken += amount;
        }

        public void NoteOutgoingHeal(long amount, bool critical, DateTime timeUtc)
        {
            Touch(timeUtc);
            Healed += amount;
            Heals++;
            if (critical)
                CritHeals++;
        }

        public void NoteIncomingHeal(long amount, DateTime timeUtc)
        {
            Touch(timeUtc);
            HealsTaken += amount;
        }

        public void NoteFailedSwing(bool isMiss, DateTime timeUtc)
        {
            Touch(timeUtc);
            Swings++;
            if (isMiss)
                Misses++;
            else
                HitFailed++;
        }

        public void NoteDeath(DateTime timeUtc)
        {
            Touch(timeUtc);
            Deaths++;
        }

        private void Touch(DateTime timeUtc)
        {
            if (FirstEventUtc == default || timeUtc < FirstEventUtc)
                FirstEventUtc = timeUtc;

            if (LastEventUtc < timeUtc)
                LastEventUtc = timeUtc;
        }
    }

    private static class ActxSnapshotFormatter
    {
        public static CombatDataWrapper Build(EncounterSession encounter, bool isActive)
        {
            var durationSeconds = encounter.DurationSeconds;
            var combatants = encounter.Combatants
                .OrderByDescending(combatant => combatant.Damage / durationSeconds)
                .ThenBy(combatant => combatant.Name, StringComparer.Ordinal)
                .ToList();

            var summaryCombatants = combatants
                .Where(static combatant => combatant.Kind != TrackedActorKind.HostileNpc)
                .ToList();
            if (summaryCombatants.Count == 0)
                summaryCombatants = combatants;

            var totalDamage = summaryCombatants.Sum(static combatant => combatant.Damage);
            var totalDamageTaken = summaryCombatants.Sum(static combatant => combatant.DamageTaken);
            var totalHits = summaryCombatants.Sum(static combatant => combatant.Hits);
            var totalHitFailed = summaryCombatants.Sum(static combatant => combatant.HitFailed);
            var totalCritHits = summaryCombatants.Sum(static combatant => combatant.CritHits);

            var maxHitCombatant = summaryCombatants
                .Where(static combatant => combatant.MaxHitValue > 0)
                .OrderByDescending(static combatant => combatant.MaxHitValue)
                .ThenBy(combatant => combatant.Name, StringComparer.Ordinal)
                .FirstOrDefault();

            var encounterMaxHit = "--";
            var encounterShortMaxHit = "--";
            if (maxHitCombatant != null)
            {
                var actionName = SafeActionName(maxHitCombatant.MaxHitActionName);
                encounterMaxHit =
                    $"{maxHitCombatant.Name}-{actionName}-{CreateDamageString(maxHitCombatant.MaxHitValue, useSuffix: true, useDecimals: true)}";
                encounterShortMaxHit =
                    $"{maxHitCombatant.Name}-{CreateDamageString(maxHitCombatant.MaxHitValue, useSuffix: true, useDecimals: false)}";
            }

            var combatantPayload = new Dictionary<string, Combatant>(combatants.Count, StringComparer.Ordinal);
            foreach (var combatant in combatants)
            {
                var damagePercent = totalDamage > 0
                    ? $"{(int)(combatant.Damage / (float)totalDamage * 100f)}%"
                    : "--";

                var encDps = combatant.Damage / durationSeconds;
                var encHps = combatant.Healed / durationSeconds;
                var dtps = combatant.DamageTaken / durationSeconds;
                var toHit = combatant.Swings > 0
                    ? combatant.Hits / (float)combatant.Swings * 100f
                    : 0f;

                combatantPayload[$"{combatant.Name}#{combatant.ActorId:X8}"] = new Combatant
                {
                    Name = combatant.Name,
                    ParticipantKind = FormatTrackedActorKind(combatant.Kind),
                    Job = FormatCombatantJobName(combatant),
                    DamagePercentText = damagePercent,
                    DamageText = CreateDamageString(combatant.Damage, useSuffix: true, useDecimals: true),
                    EncDpsText = encDps.ToString("0", CultureInfo.InvariantCulture),
                    EncHpsText = encHps.ToString("0", CultureInfo.InvariantCulture),
                    HealedText = CreateDamageString(combatant.Healed, useSuffix: true, useDecimals: true),
                    DtpsText = dtps.ToString("0", CultureInfo.InvariantCulture),
                    MaxHitText = combatant.MaxHitValue > 0
                        ? $"{SafeActionName(combatant.MaxHitActionName)}-{CreateDamageString(combatant.MaxHitValue, useSuffix: true, useDecimals: true)}"
                        : "--",
                    HitsText = combatant.Hits.ToString(CultureInfo.InvariantCulture),
                    CritHitsText = combatant.CritHits.ToString(CultureInfo.InvariantCulture),
                    CritDirectHitsText = combatant.CritDirectHits.ToString(CultureInfo.InvariantCulture),
                    ToHitText = toHit.ToString("F", CultureInfo.InvariantCulture),
                    DamageTakenText = CreateDamageString(combatant.DamageTaken, useSuffix: true, useDecimals: true),
                    BlockPctText = "--",
                    ParryPctText = "--",
                    DeathsText = combatant.Deaths.ToString(CultureInfo.InvariantCulture),
                    DotDamageText = CreateDamageString(combatant.DotDamage, useSuffix: true, useDecimals: true),
                };
            }

            return new CombatDataWrapper
            {
                Type = "broadcast",
                MsgType = "CombatData",
                Msg = new CombatData
                {
                    Type = "CombatData",
                    IsActive = isActive ? "true" : "false",
                    Encounter = new Encounter
                    {
                        CurrentZoneName = encounter.ZoneName,
                        DurationText = FormatDuration(durationSeconds),
                        DamageText = CreateDamageString(totalDamage, useSuffix: true, useDecimals: true),
                        EncDpsText = (totalDamage / durationSeconds).ToString("0", CultureInfo.InvariantCulture),
                        HitsText = totalHits.ToString(CultureInfo.InvariantCulture),
                        HitFailedText = totalHitFailed.ToString(CultureInfo.InvariantCulture),
                        CritHitsText = totalCritHits.ToString(CultureInfo.InvariantCulture),
                        CritHitPercentText = totalHits > 0
                            ? $"{(int)(totalCritHits / (float)totalHits * 100f)}%"
                            : "0%",
                        MaxHitText = encounterMaxHit,
                        MaxHitValueText = encounterShortMaxHit,
                        DamageTakenText = CreateDamageString(totalDamageTaken, useSuffix: true, useDecimals: true),
                    },
                    Combatant = combatantPayload,
                },
            };
        }

        public static string FormatDuration(double durationSeconds)
        {
            var wholeSeconds = durationSeconds < 1d
                ? 1
                : (int)Math.Round(durationSeconds, MidpointRounding.AwayFromZero);
            var span = TimeSpan.FromSeconds(wholeSeconds);
            return span.TotalHours >= 1d
                ? span.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
                : span.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        }

        private static string SafeActionName(string? actionName)
            => string.IsNullOrWhiteSpace(actionName) ? "未知技能" : actionName;

        private static string FormatCombatantJobName(CombatantSession combatant)
        {
            if (!string.IsNullOrWhiteSpace(combatant.JobName))
                return combatant.JobName;

            return combatant.Kind switch
            {
                TrackedActorKind.FriendlyNpc => "友方NPC",
                TrackedActorKind.HostileNpc => "敌方NPC",
                _ => "-",
            };
        }

        private static string? FormatTrackedActorKind(TrackedActorKind kind)
            => kind switch
            {
                TrackedActorKind.Player => "player",
                TrackedActorKind.FriendlyNpc => "friendlyNpc",
                TrackedActorKind.HostileNpc => "hostileNpc",
                _ => null,
            };

    }

}
