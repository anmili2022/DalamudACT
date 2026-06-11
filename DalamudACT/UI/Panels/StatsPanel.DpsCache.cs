using System;
using System.Collections.Generic;

namespace DalamudACT;

internal static partial class StatsPanel
{
    private static readonly object DpsRowsCacheGate = new();
    private static CombatDataWrapper? cachedDpsRowsCombatData;
    private static FloatingStatsParticipantDisplayMode cachedDpsRowsParticipantDisplayMode;
    private static int cachedDpsRowsCombatantCount;
    private static IReadOnlyList<Combatant> cachedDpsOrderedCombatants = Array.Empty<Combatant>();
    private static int cachedDpsSummaryRowInsertIndex;
    private static double cachedDpsTotalDps;
    private static string cachedDpsTotalDamageText = "0";
    private static int cachedDpsTotalDeaths;

    private readonly record struct DpsMetricRow(
        Combatant Combatant,
        FloatingCombatantKind Kind,
        double Dps);

    private readonly record struct DpsRowsSnapshot(
        IReadOnlyList<Combatant> OrderedCombatants,
        int SummaryRowInsertIndex,
        double TotalDps,
        string TotalDamageText,
        int TotalDeaths);

    private static DpsRowsSnapshot GetDpsRowsSnapshot(CombatDataWrapper combatData, PluginConfiguration config)
    {
        var combatantCount = combatData.Msg?.Combatant?.Count ?? 0;
        var participantDisplayMode = config.FloatingStatsParticipantDisplayMode;
        lock (DpsRowsCacheGate)
        {
            if (ReferenceEquals(cachedDpsRowsCombatData, combatData)
                && cachedDpsRowsParticipantDisplayMode == participantDisplayMode
                && cachedDpsRowsCombatantCount == combatantCount)
            {
                return new DpsRowsSnapshot(
                    cachedDpsOrderedCombatants,
                    cachedDpsSummaryRowInsertIndex,
                    cachedDpsTotalDps,
                    cachedDpsTotalDamageText,
                    cachedDpsTotalDeaths);
            }

            var visibleRows = GetVisibleCombatantRows(combatData, config);
            var nonHostileRows = new List<DpsMetricRow>(visibleRows.Count);
            var hostileRows = new List<DpsMetricRow>(visibleRows.Count);
            double totalDps = 0d;
            long totalDamage = 0L;
            var totalDeaths = 0;

            foreach (var row in visibleRows)
            {
                var combatant = row.Combatant;
                var dps = ParseMetric(combatant.EncDpsText);
                var metricRow = new DpsMetricRow(combatant, row.Kind, dps);
                if (row.Kind == FloatingCombatantKind.HostileNpc)
                {
                    hostileRows.Add(metricRow);
                    continue;
                }

                nonHostileRows.Add(metricRow);
                totalDps += dps;
                totalDamage += ParseLocalizedAmount(combatant.DamageText);
                totalDeaths += ParseCount(combatant.DeathsText);
            }

            nonHostileRows.Sort(CompareDpsRows);
            hostileRows.Sort(CompareDpsRows);

            var orderedCombatants = new List<Combatant>(nonHostileRows.Count + hostileRows.Count);
            foreach (var row in nonHostileRows)
                orderedCombatants.Add(row.Combatant);
            foreach (var row in hostileRows)
                orderedCombatants.Add(row.Combatant);

            cachedDpsRowsCombatData = combatData;
            cachedDpsRowsParticipantDisplayMode = participantDisplayMode;
            cachedDpsRowsCombatantCount = combatantCount;
            cachedDpsOrderedCombatants = orderedCombatants;
            cachedDpsSummaryRowInsertIndex = nonHostileRows.Count;
            cachedDpsTotalDps = totalDps;
            cachedDpsTotalDamageText = FormatCompactAmount(totalDamage);
            cachedDpsTotalDeaths = totalDeaths;

            return new DpsRowsSnapshot(
                cachedDpsOrderedCombatants,
                cachedDpsSummaryRowInsertIndex,
                cachedDpsTotalDps,
                cachedDpsTotalDamageText,
                cachedDpsTotalDeaths);
        }
    }

    private static int CompareDpsRows(DpsMetricRow left, DpsMetricRow right)
    {
        var dpsCompare = right.Dps.CompareTo(left.Dps);
        if (dpsCompare != 0)
            return dpsCompare;

        return string.CompareOrdinal(left.Combatant.Name, right.Combatant.Name);
    }
}
