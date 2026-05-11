using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DalamudACT;

internal sealed class CombatDataWrapper
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("msgtype")]
    public string? MsgType { get; set; }

    [JsonPropertyName("msg")]
    public CombatData? Msg { get; set; }
}

internal sealed class CombatData
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("Encounter")]
    public Encounter? Encounter { get; set; }

    [JsonPropertyName("Combatant")]
    public Dictionary<string, Combatant> Combatant { get; set; } = new();

    [JsonPropertyName("isActive")]
    public string? IsActive { get; set; }
}

internal sealed class Encounter
{
    [JsonPropertyName("CurrentZoneName")]
    public string? CurrentZoneName { get; set; }

    [JsonPropertyName("duration")]
    public string? DurationText { get; set; }

    [JsonPropertyName("damage-*")]
    public string? DamageText { get; set; }

    [JsonPropertyName("ENCDPS")]
    public string? EncDpsText { get; set; }

    [JsonPropertyName("hits")]
    public string? HitsText { get; set; }

    [JsonPropertyName("hitfailed")]
    public string? HitFailedText { get; set; }

    [JsonPropertyName("crithits")]
    public string? CritHitsText { get; set; }

    [JsonPropertyName("crithit%")]
    public string? CritHitPercentText { get; set; }

    [JsonPropertyName("maxhit-*")]
    public string? MaxHitText { get; set; }

    [JsonPropertyName("MAXHIT-*")]
    public string? MaxHitValueText { get; set; }

    [JsonPropertyName("damagetaken-*")]
    public string? DamageTakenText { get; set; }
}

internal sealed class Combatant
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("participantKind")]
    public string? ParticipantKind { get; set; }

    [JsonPropertyName("Job")]
    public string? Job { get; set; }

    [JsonPropertyName("damage%")]
    public string? DamagePercentText { get; set; }

    [JsonPropertyName("damage-*")]
    public string? DamageText { get; set; }

    [JsonPropertyName("ENCDPS")]
    public string? EncDpsText { get; set; }

    [JsonPropertyName("ENCHPS")]
    public string? EncHpsText { get; set; }

    [JsonPropertyName("healed-*")]
    public string? HealedText { get; set; }

    [JsonPropertyName("DTPS")]
    public string? DtpsText { get; set; }

    [JsonPropertyName("maxhit-*")]
    public string? MaxHitText { get; set; }

    [JsonPropertyName("hits")]
    public string? HitsText { get; set; }

    [JsonPropertyName("crithits")]
    public string? CritHitsText { get; set; }

    [JsonPropertyName("tohit")]
    public string? ToHitText { get; set; }

    [JsonPropertyName("damagetaken-*")]
    public string? DamageTakenText { get; set; }

    [JsonPropertyName("BlockPct")]
    public string? BlockPctText { get; set; }

    [JsonPropertyName("ParryPct")]
    public string? ParryPctText { get; set; }

    [JsonPropertyName("deaths")]
    public string? DeathsText { get; set; }

    [JsonPropertyName("dotDamage-*")]
    public string? DotDamageText { get; set; } = "0";
}

internal sealed record HistoricalCombatData(
    string ZoneName,
    string Duration,
    CombatDataWrapper Snapshot,
    System.DateTime? StartTimeUtc = null,
    System.DateTime? EndTimeUtc = null);
