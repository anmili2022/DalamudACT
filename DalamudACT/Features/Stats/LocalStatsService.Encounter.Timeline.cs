using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
    public void ClearCombatTimeline()
    {
        lock (gate)
            combatTimelineEntries.Clear();
    }

    public void SetCombatTimelineRecordingEnabled(bool enabled)
    {
        lock (gate)
        {
            if (config.CombatTimelineRecordingEnabled == enabled)
                return;

            config.CombatTimelineRecordingEnabled = enabled;
            config.Save();
        }
    }

    public void ApplyCombatTimelineRetentionLimit()
    {
        lock (gate)
            TrimCombatTimelineEntriesLocked();
    }


    private void PollCombatTimelineFriendlyStatusesLocked(DateTime nowUtc, bool inCombat)
    {
        if (!config.CombatTimelineRecordingEnabled || !currentEncounter.Started)
        {
            observedCombatTimelineStatusKeys.Clear();
            combatTimelineStatusRecorderPrimed = false;
            lastCombatTimelineStatusPollUtc = default;
            return;
        }

        if (nowUtc - lastCombatTimelineStatusPollUtc < TimeSpan.FromMilliseconds(100))
            return;

        lastCombatTimelineStatusPollUtc = nowUtc;

        var seenStatusKeys = new HashSet<CombatTimelineStatusKey>();
        foreach (var friendlyActor in EnumerateTrackedPartyBattleCharas())
            CaptureCombatTimelineFriendlyStatusesLocked(friendlyActor, nowUtc, seenStatusKeys);

        observedCombatTimelineStatusKeys.RemoveWhere(key => !seenStatusKeys.Contains(key));
        if (!combatTimelineStatusRecorderPrimed)
            combatTimelineStatusRecorderPrimed = true;
    }

    public void RecordCombatTimelineMapEffect(uint flags, uint location, DateTime nowUtc)
    {
        lock (gate)
        {
            if (!config.CombatTimelineRecordingEnabled || !config.CombatTimelineMapEffectEnabled || !currentEncounter.Started)
                return;

            AppendCombatTimelineEntryLocked(
                nowUtc,
                CombatTimelineEntryKind.MapEffect,
                $"场地特效：flags={flags:X8}, location={location:X2}。",
                "场地",
                null,
                false,
                false,
                $"MapEffect {flags:X8}/{location:X2}");
        }
    }

    public void PollCombatTimelineHostileCasts(DateTime nowUtc, bool inCombat, IEnumerable<IBattleChara>? battleCharas = null)
    {
        lock (gate)
        {
            PollCombatTimelineHostileCastsLocked(nowUtc, inCombat, battleCharas);
        }
    }

    private void PollCombatTimelineHostileCastsLocked(DateTime nowUtc, bool inCombat, IEnumerable<IBattleChara>? battleCharas)
    {
        if (!config.CombatTimelineRecordingEnabled || !inCombat || !currentEncounter.Started)
        {
            observedCombatTimelineCastKeys.Clear();
            lastCombatTimelineCastPollUtc = default;
            return;
        }

        if (nowUtc - lastCombatTimelineCastPollUtc < TimeSpan.FromMilliseconds(100))
            return;

        lastCombatTimelineCastPollUtc = nowUtc;
        foreach (var battleChara in battleCharas ?? DalamudApi.ObjectTable.OfType<IBattleChara>())
            CaptureCombatTimelineHostileCastLocked(battleChara, nowUtc);
    }

    private void CaptureCombatTimelineHostileCastLocked(IBattleChara battleChara, DateTime nowUtc)
    {
        if (!BattleCharaReflectionAccessor.IsLikelyHostileBattleNpc(battleChara))
            return;

        var actorId = ResolveBattleCharaActorId(battleChara);
        if (actorId is 0 or InvalidActorId)
            return;

        var actionId = BattleCharaReflectionAccessor.GetCastingActionId(battleChara);
        if (actionId == 0)
            return;

        var key = $"{actorId:X8}:{actionId:X8}";
        if (observedCombatTimelineCastKeys.TryGetValue(key, out var lastSeen)
            && (nowUtc - lastSeen).TotalSeconds < 3)
            return;

        observedCombatTimelineCastKeys[key] = nowUtc;
        var actorName = ResolveCombatTimelineSourceName(actorId, nowUtc);
        var actionName = ResolveActionNameForCombatTimeline(actionId);
        var actionText = FormatActionNameWithId(actionName, actionId);
        AppendCombatTimelineEntryLocked(
            nowUtc,
            CombatTimelineEntryKind.Cast,
            $"{actorName} 开始读条 {actionText}。",
            actorName,
            null,
            false,
            false,
            actionText);
    }

    private void CaptureCombatTimelineFriendlyStatusesLocked(IBattleChara friendlyActor, DateTime nowUtc, ISet<CombatTimelineStatusKey> seenStatusKeys)
    {
        var actorId = ResolveBattleCharaActorId(friendlyActor);
        if (actorId is 0 or InvalidActorId)
            return;

        if (!TryGetTrackedActor(actorId, out var trackedActor) || trackedActor.Kind == TrackedActorKind.HostileNpc)
            return;

        foreach (var status in EnumerateStatusEntries(friendlyActor))
        {
            var statusId = GetStatusId(status);
            if (statusId == 0)
                continue;

            var isBuff = IsBuffStatus(status);
            var isDebuff = IsDebuffStatus(status);
            if (!isBuff && !isDebuff)
                continue;

            var sourceActorId = ResolveStatusSourceActorId(status);
            var key = new CombatTimelineStatusKey(actorId, statusId, sourceActorId, isDebuff);
            seenStatusKeys.Add(key);
            if (!combatTimelineStatusRecorderPrimed)
            {
                observedCombatTimelineStatusKeys.Add(key);
                continue;
            }

            if (!observedCombatTimelineStatusKeys.Add(key))
                continue;

            var statusName = GetStatusName(status, statusId);
            var statusText = FormatStatusNameWithId(statusName, statusId);
            var sourceName = sourceActorId == 0 ? "未知来源" : ResolveCombatTimelineSourceName(sourceActorId, nowUtc);
            var statusKindText = isDebuff ? "debuff" : "BUFF";
            var remainingText = FormatStatusRemaining(status);
            AppendCombatTimelineEntryLocked(
                nowUtc,
                CombatTimelineEntryKind.Status,
                $"{trackedActor.Name} 获得{statusKindText} {statusText}，来源 {sourceName}{remainingText}。",
                trackedActor.Name,
                trackedActor.Name,
                true,
                true,
                statusText);
        }
    }


    private void AppendEncounterStartIfNeededLocked(bool wasStarted, DateTime timeUtc)
    {
        if (wasStarted || !currentEncounter.Started)
            return;

        AppendCombatTimelineEntryLocked(timeUtc, CombatTimelineEntryKind.CombatStart, $"进入战斗：{currentEncounter.ZoneName}");
    }

    private void RemoveLastCombatStartTimelineEntryLocked()
    {
        for (var i = combatTimelineEntries.Count - 1; i >= 0; i--)
        {
            if (combatTimelineEntries[i].Kind == CombatTimelineEntryKind.CombatStart)
            {
                combatTimelineEntries.RemoveAt(i);
                return;
            }
        }
    }

    private void AppendCombatTimelineEntryLocked(
        DateTime timeUtc,
        CombatTimelineEntryKind kind,
        string message,
        string? actorName = null,
        string? targetName = null,
        bool actorIsFriendly = false,
        bool targetIsFriendly = false,
        string? actionText = null)
    {
        if (!config.CombatTimelineRecordingEnabled)
            return;

        combatTimelineEntries.Add(new CombatTimelineEntry(
            timeUtc.ToLocalTime(),
            currentEncounter.Started ? Math.Max(0, (int)Math.Floor((timeUtc - currentEncounter.StartUtc).TotalSeconds)) : null,
            kind,
            message,
            actorName,
            targetName,
            actorIsFriendly,
            targetIsFriendly,
            actionText));
        TrimCombatTimelineEntriesLocked();
    }

    private void TrimCombatTimelineEntriesLocked()
    {
        var maxEntryCount = config.CombatTimelineMaxEntries <= 0
            ? 0
            : Math.Clamp(config.CombatTimelineMaxEntries, 100, 50000);
        if (maxEntryCount == 0)
            return;

        if (combatTimelineEntries.Count > maxEntryCount)
            combatTimelineEntries.RemoveRange(0, combatTimelineEntries.Count - maxEntryCount);
    }

    private string ResolveCombatTimelineSourceName(uint actorId, DateTime nowUtc)
    {
        if (TryGetTrackedActor(actorId, out var trackedActor))
            return trackedActor.Name;

        var obj = FindObjectByActorId(actorId);
        var objectName = obj?.Name.TextValue?.Trim();
        if (!string.IsNullOrWhiteSpace(objectName))
            return objectName;

        return TryResolveTrackedSource(actorId, nowUtc, out trackedActor)
            ? trackedActor.Name
            : BuildUnknownActorName(actorId, "未知来源");
    }

    private string ResolveCombatTimelineTargetName(uint actorId, DateTime nowUtc)
    {
        _ = nowUtc;

        if (TryGetTrackedActor(actorId, out var trackedActor))
            return trackedActor.Name;

        var obj = FindObjectByActorId(actorId);
        var objectName = obj?.Name.TextValue?.Trim();
        if (!string.IsNullOrWhiteSpace(objectName))
            return objectName;

        return BuildUnknownActorName(actorId, "未知目标");
    }

    private string ResolveActionNameForCombatTimeline(uint actionId)
    {
        if (actionId == 0)
            return "未知技能";

        try
        {
            var sheet = DalamudApi.GameData.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            if (sheet != null && sheet.TryGetRow(actionId, out var row) && !row.Name.IsEmpty)
                return row.Name.ExtractText();
        }
        catch
        {
            // Fall through to the id label if sheet access fails.
        }

        return $"技能 {actionId:X}";
    }

    private static string BuildCombatTimelineDamageSnapshotText(uint sourceId, uint targetId)
    {
        var targetObject = FindObjectByActorId(targetId) as IBattleChara;
        if (targetObject == null)
            return string.Empty;

        var hpText = BuildCombatTimelineHpText(targetObject);
        var mitigationText = BuildCombatTimelineMitigationText(sourceId, targetObject);
        if (string.IsNullOrWhiteSpace(hpText) && string.IsNullOrWhiteSpace(mitigationText))
            return string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(hpText))
            parts.Add(hpText);
        if (!string.IsNullOrWhiteSpace(mitigationText))
            parts.Add(mitigationText);

        return $"（{string.Join("；", parts)}）";
    }

    private static string BuildCombatTimelineHpText(IBattleChara targetObject)
    {
        var maxHp = targetObject.MaxHp;
        if (maxHp == 0)
            return string.Empty;

        var currentHp = Math.Min(targetObject.CurrentHp, maxHp);
        var hpPercent = currentHp / (double)maxHp * 100d;
        return $"HP {CreateDamageString(currentHp, useSuffix: true, useDecimals: true)}/{CreateDamageString(maxHp, useSuffix: true, useDecimals: true)} ({hpPercent:0.#}%)";
    }

    private static string BuildCombatTimelineMitigationText(uint sourceId, IBattleChara targetObject)
    {
        const int maxStatusCount = 8;
        var statuses = new List<string>();
        foreach (var status in EnumerateStatusEntries(targetObject))
        {
            var statusId = GetStatusId(status);
            if (statusId == 0 || !IsBuffStatus(status))
                continue;

            var statusName = GetStatusName(status, statusId);
            if (!IsLikelyDefensiveStatus(statusId, statusName, false))
                continue;

            statuses.Add($"目标:{FormatStatusNameWithId(statusName, statusId)}{FormatStatusRemaining(status)}");
            if (statuses.Count >= maxStatusCount)
                break;
        }

        if (statuses.Count < maxStatusCount && FindObjectByActorId(sourceId) is IBattleChara sourceObject)
        {
            foreach (var status in EnumerateStatusEntries(sourceObject))
            {
                var statusId = GetStatusId(status);
                if (statusId == 0 || !IsDebuffStatus(status))
                    continue;

                var statusName = GetStatusName(status, statusId);
                if (!IsLikelyDefensiveStatus(statusId, statusName, true))
                    continue;

                statuses.Add($"来源:{FormatStatusNameWithId(statusName, statusId)}{FormatStatusRemaining(status)}");
                if (statuses.Count >= maxStatusCount)
                    break;
            }
        }

        if (statuses.Count == 0)
            return "减伤 无";

        return $"减伤 {string.Join("、", statuses)}";
    }

    private static bool IsLikelyDefensiveStatus(uint statusId, string statusName, bool sourceDebuff)
    {
        if (KnownMitigationStatusIds.Contains(statusId))
            return true;

        if (string.IsNullOrWhiteSpace(statusName))
            return false;

        var normalized = statusName.Trim();
        foreach (var name in KnownMitigationStatusNames)
        {
            if (normalized.Contains(name, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (sourceDebuff)
            return normalized.Contains("雪仇", StringComparison.OrdinalIgnoreCase)
                   || normalized.Contains("牵制", StringComparison.OrdinalIgnoreCase)
                   || normalized.Contains("昏乱", StringComparison.OrdinalIgnoreCase)
                   || normalized.Contains("Feint", StringComparison.OrdinalIgnoreCase)
                   || normalized.Contains("Addle", StringComparison.OrdinalIgnoreCase)
                   || normalized.Contains("Reprisal", StringComparison.OrdinalIgnoreCase);

        return normalized.Contains("盾", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("障壁", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("防御", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("守护", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("减伤", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("Barrier", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("Guard", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("Shield", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly string[] KnownMitigationStatusNames =
    [
        "铁壁", "雪仇", "牵制", "昏乱", "盾阵", "干预", "极致防御", "圣光幕帘", "武装戍卫", "神圣领域",
        "死斗", "屠戮", "原初的血气", "摆脱", "战栗", "行尸走肉", "至黑之夜", "献奉", "弃明投暗", "暗黑布道", "暗影卫",
        "超火流星", "大星云", "刚玉之心", "光之心", "水流幕", "神祝祷", "庇护所", "全大赦", "礼仪之铃", "节制",
        "鼓舞", "激励", "炽天的幕帘", "鼓舞激励之策", "士气高扬之策", "慰藉", "Galvanize", "Catalyze", "Seraphic Veil", "野战治疗阵", "炽天召唤", "炽天附体", "疾风怒涛之计", "生命回生法", "展开战术", "炽天的幻光",
        "星位合图", "命运之轮", "中间学派", "大宇宙", "地星", "均衡诊断", "均衡预后", "整体论", "泛输血", "输血", "坚角清汁", "自生", "魂灵风息", "消化", "活化", "白牛清汁",
        "牵制", "残影", "内丹", "浴血", "亲疏自行", "武装解除", "行吟", "大地神的抒情恋歌", "策动", "即兴表演", "防守之桑巴",
        "昏乱", "抗死", "守护之光", "魔罩", "Addle", "Feint", "Reprisal", "Rampart", "Shield", "Barrier", "Guard"
    ];

    private static readonly HashSet<uint> KnownMitigationStatusIds =
    [
        74, 82, 83, 87, 89, 157, 297, 299, 409, 735, 740, 746, 747, 807,
        810, 811, 837, 849, 942, 956, 1175, 1178, 1179, 1191, 1193,
        1195, 1202, 1203, 1218, 1219, 1220, 1224, 1362, 1404, 1826,
        1832, 1834, 1836, 1839, 1840, 1856, 1858, 1872, 1888, 1892,
        1894, 1911, 1912, 1917, 1918, 1921, 1934, 1951, 1993, 2659,
        2607, 2608, 2609, 2612, 2613, 2618, 2619, 2622, 2642, 2643,
        2674, 2675, 2678, 2679, 2680, 2682, 2683, 2684, 2685, 2702,
        2708, 2709, 2710, 2711, 2712, 2717, 2718, 2938, 3003, 3829,
        3830, 3832, 3838,
    ];
}
