using System;
using System.Collections.Generic;
using System.Globalization;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
    private static CombatDataWrapper BuildTestCombatData(
        string zoneName,
        string durationText,
        string damageText,
        string encDpsText,
        string hitsText,
        string hitFailedText,
        string critHitsText,
        string critHitPercentText,
        string maxHitText,
        string maxHitValueText,
        string damageTakenText,
        Dictionary<string, Combatant> combatants)
    {
        PopulateDerivedTestCombatantMetrics(combatants, durationText);

        return new CombatDataWrapper
        {
            Type = "broadcast",
            MsgType = "CombatData",
            Msg = new CombatData
            {
                Type = "CombatData",
                IsActive = "false",
                Encounter = new Encounter
                {
                    CurrentZoneName = zoneName,
                    DurationText = durationText,
                    DamageText = damageText,
                    EncDpsText = encDpsText,
                    HitsText = hitsText,
                    HitFailedText = hitFailedText,
                    CritHitsText = critHitsText,
                    CritHitPercentText = critHitPercentText,
                    MaxHitText = maxHitText,
                    MaxHitValueText = maxHitValueText,
                    DamageTakenText = damageTakenText,
                },
                Combatant = combatants,
            },
        };
    }

    private static void PopulateDerivedTestCombatantMetrics(
        Dictionary<string, Combatant> combatants,
        string durationText)
    {
        var durationSeconds = ParseDurationTextToSeconds(durationText);
        if (durationSeconds <= 0d)
            return;

        foreach (var combatant in combatants.Values)
        {
            if (!string.IsNullOrWhiteSpace(combatant.HealedText))
                continue;

            if (!double.TryParse(combatant.EncHpsText, NumberStyles.Float, CultureInfo.InvariantCulture, out var hps)
                || hps <= 0d)
            {
                continue;
            }

            var totalHealed = (long)Math.Round(hps * durationSeconds, MidpointRounding.AwayFromZero);
            combatant.HealedText = CreateDamageString(totalHealed, useSuffix: true, useDecimals: true);
        }
    }

    private static double ParseDurationTextToSeconds(string? durationText)
    {
        if (string.IsNullOrWhiteSpace(durationText))
            return 0d;

        return TimeSpan.TryParseExact(durationText, @"mm\:ss", CultureInfo.InvariantCulture, out var mmss)
            ? mmss.TotalSeconds
            : TimeSpan.TryParseExact(durationText, @"hh\:mm\:ss", CultureInfo.InvariantCulture, out var hhmmss)
                ? hhmmss.TotalSeconds
                : 0d;
    }

    private static Combatant CreateTestCombatant(
        string name,
        string job,
        string damagePercentText,
        string damageText,
        string encDpsText,
        string encHpsText,
        string dtpsText,
        string maxHitText,
        string hitsText,
        string critHitsText,
        string toHitText,
        string damageTakenText,
        string deathsText,
        string? healedText = null)
    {
        return new Combatant
        {
            Name = name,
            Job = job,
            DamagePercentText = damagePercentText,
            DamageText = damageText,
            EncDpsText = encDpsText,
            EncHpsText = encHpsText,
            HealedText = healedText,
            DtpsText = dtpsText,
            MaxHitText = maxHitText,
            HitsText = hitsText,
            CritHitsText = critHitsText,
            CritDirectHitsText = "0",
            ToHitText = toHitText,
            DamageTakenText = damageTakenText,
            BlockPctText = "--",
            ParryPctText = "--",
            DeathsText = deathsText,
        };
    }

}
