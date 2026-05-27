using System;
using System.Collections.Generic;
using System.Globalization;

namespace DalamudACT;

// 测试数据模块：负责生成内置演示战斗记录和一键导入测试数据。
internal sealed partial class LocalStatsService
{
    public void LoadTestData()
    {
        lock (gate)
        {
            var syntheticAnchorUtc = DateTime.UtcNow.Date.AddHours(12);
            var firstSnapshot = BuildRaidEightPlayerTestCombatData();
            var secondSnapshot = BuildTrialTestCombatData();
            var thirdSnapshot = BuildTrainingTestCombatData();
            UpsertHistoricalRecord(CreateSyntheticHistoricalRecord(firstSnapshot, syntheticAnchorUtc.AddMinutes(-36)));
            UpsertHistoricalRecord(CreateSyntheticHistoricalRecord(secondSnapshot, syntheticAnchorUtc.AddMinutes(-22)));
            UpsertHistoricalRecord(CreateSyntheticHistoricalRecord(thirdSnapshot, syntheticAnchorUtc.AddMinutes(-8)));
            SortHistoricalRecords();

            ownerCache.Clear();
            observedFriendlyActorCache.Clear();
            partyMemberHpCache.Clear();
            recentHostilePlayerActions.Clear();
            activePlayerDots.Clear();
            activeWildfires.Clear();
            dotStatusClassificationCache.Clear();
            actionDescriptionDotPotencyCache.Clear();
            actionDescriptionDotPotencyCacheMisses.Clear();
            actionDescriptionPotencyCache.Clear();
            actionDescriptionPotencyCacheMisses.Clear();
            combatTimelineEntries.Clear();
            CurrentCombatData = firstSnapshot;
            DisplayCombatData = firstSnapshot;
            ClearHistoricalPreviewLocked();
            currentEncounter = new EncounterSession
            {
                ZoneName = CurrentCombatData.Msg?.Encounter?.CurrentZoneName ?? "零式测试场",
            };
            partyOutOfCombatSinceUtc = default;
            enteredCombatWithoutDataSinceUtc = default;
            lastNoDataCombatDiagnosticUtc = default;
            HistoryTransferStatusText = $"已导入测试数据，共 {historicalRecords.Count} 条历史记录。";
            StatusText = "已导入测试数据，可用于预览 DPS 统计面板。";
            LogHelper.PrintWithModule("统计", "测试数据", $"已导入测试数据，共 {historicalRecords.Count} 条历史记录。");
        }
    }

}
