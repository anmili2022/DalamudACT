using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

// 玩家 DoT / Wildfire 模块：负责 DoT 挂载识别、tick 归因、模拟补算、野火结算和 DOT 诊断日志。
internal sealed partial class LocalStatsService
{

    private readonly List<RecentHostilePlayerAction> recentHostilePlayerActions = new();
    private readonly Dictionary<PlayerDotKey, ActivePlayerDotState> activePlayerDots = new();
    private readonly Dictionary<PlayerWildfireKey, ActiveWildfireState> activeWildfires = new();
    private readonly Dictionary<uint, bool> dotStatusClassificationCache = new();
    private readonly Dictionary<uint, ActionDescriptionDotPotencyEntry> actionDescriptionDotPotencyCache = new();
    private readonly HashSet<uint> actionDescriptionDotPotencyCacheMisses = new();
    private readonly Dictionary<uint, int> actionDescriptionPotencyCache = new();
    private readonly HashSet<uint> actionDescriptionPotencyCacheMisses = new();
    private readonly Dictionary<string, DateTime> playerDotDiagnosticLogTimestamps = new(StringComparer.Ordinal);
    private DateTime lastPlayerDotStatusPollUtc;
    private DateTime lastPlayerDotDebugLogUtc;
    private int nextPlayerDotTargetPollIndex;
    private int nextPlayerDotFriendlyPollIndex;
    private int nextPlayerDotSimulationIndex;
    private int nextPlayerDotTrimIndex;
    private int nextPlayerDotDecayIndex;

    public bool ObservePotentialPlayerDotApplication(
        uint sourceId,
        uint targetId,
        uint actionId,
        string actionName,
        DateTime timeUtc)
    {
        if (!PlayerDotCatalog.IsKnownPlayerDotAction(actionId))
            return false;

        lock (gate)
        {
            try
            {
                if (!TryResolveTrackedSource(sourceId, timeUtc, out var source) || source.Kind != TrackedActorKind.Player)
                    return false;

                TrimRecentHostilePlayerActionsLocked(timeUtc);
                recentHostilePlayerActions.Add(new RecentHostilePlayerAction(
                    source,
                    targetId,
                    actionId,
                    NormalizeActionName(actionName),
                    timeUtc));
                if (IsFocusedPlayerDotDiagnosticAction(actionId))
                {
                    LogFocusedPlayerDotDiagnosticLocked(
                        timeUtc,
                        $"candidate:0x{source.ActorId:X8}:0x{targetId:X8}:0x{actionId:X8}",
                        $"记录挂载候选：source={source.Name}/0x{source.ActorId:X8}，target=0x{targetId:X8}，action={FormatActionNameWithId(actionName, actionId)}。");
                }

                if (!TryGetHostileBattleTarget(targetId, out var hostileTarget))
                {
                    if (IsFocusedPlayerDotDiagnosticAction(actionId))
                    {
                        LogFocusedPlayerDotDiagnosticLocked(
                            timeUtc,
                            $"candidate-target-miss:0x{source.ActorId:X8}:0x{targetId:X8}:0x{actionId:X8}",
                            $"挂载候选暂未找到敌方目标对象：source={source.Name}/0x{source.ActorId:X8}，target=0x{targetId:X8}，action={FormatActionNameWithId(actionName, actionId)}。");
                    }

                    return false;
                }

                var captured = CapturePlayerDotStatusesForHostileTargetLocked(
                    hostileTarget,
                    timeUtc,
                    preferredSourceActorId: source.ActorId,
                    preferredActionId: actionId,
                    preferredActionName: NormalizeActionName(actionName));
                if (IsFocusedPlayerDotDiagnosticAction(actionId))
                {
                    LogFocusedPlayerDotDiagnosticLocked(
                        timeUtc,
                        $"candidate-capture:0x{source.ActorId:X8}:0x{targetId:X8}:0x{actionId:X8}:{captured}",
                        $"挂载候选即时状态确认：source={source.Name}/0x{source.ActorId:X8}，target={ResolveCombatTimelineTargetName(targetId, timeUtc)}/0x{targetId:X8}，action={FormatActionNameWithId(actionName, actionId)}，captured={captured}。");
                }

                return captured;
            }
            catch (Exception ex)
            {
                LogHelper.Error(
                    "统计",
                    ex,
                    $"记录玩家 DOT 挂载候选失败：sourceId=0x{sourceId:X8}，targetId=0x{targetId:X8}，actionId=0x{actionId:X8}。");
                return false;
            }
        }
    }

    public void ObservePotentialPlayerHostileActionSample(
        uint sourceId,
        uint targetId,
        uint actionId,
        string actionName,
        long amount,
        bool critical,
        bool directHit,
        DateTime timeUtc)
    {
        if (amount <= 0)
            return;

        lock (gate)
        {
            try
            {
                if (!TryResolveTrackedSource(sourceId, timeUtc, out var source) || source.Kind != TrackedActorKind.Player)
                    return;

                TrimRecentHostilePlayerActionsLocked(timeUtc);
                var normalizedActionName = NormalizeActionName(actionName);

                var matchedAction = recentHostilePlayerActions
                    .Where(action =>
                        AreEquivalentActorIds(action.Source.ActorId, source.ActorId)
                        && AreEquivalentActorIds(action.TargetActorId, targetId)
                        && action.ActionId == actionId
                        && string.Equals(action.ActionName, normalizedActionName, StringComparison.Ordinal)
                        && timeUtc - action.ObservedAtUtc <= PlayerDotRecentActionTtl)
                    .OrderByDescending(action => action.ObservedAtUtc)
                    .FirstOrDefault();

                if (matchedAction != null)
                {
                    matchedAction.ObservedDamageAmount = amount;
                    matchedAction.ObservedCritical = critical;
                    matchedAction.ObservedDirectHit = directHit;
                    if (IsFocusedPlayerDotDiagnosticAction(actionId))
                    {
                        LogFocusedPlayerDotDiagnosticLocked(
                            timeUtc,
                            $"seed-update:0x{source.ActorId:X8}:0x{targetId:X8}:0x{actionId:X8}",
                            $"更新伤害种子：source={source.Name}/0x{source.ActorId:X8}，target={ResolveCombatTimelineTargetName(targetId, timeUtc)}/0x{targetId:X8}，action={FormatActionNameWithId(actionName, actionId)}，amount={amount}，crit={critical}，dh={directHit}。");
                    }

                    NoteWildfireWeaponskillContributionLocked(source.ActorId, targetId, actionId, normalizedActionName, amount, critical, directHit, timeUtc);
                    RefreshActivePlayerDotEstimatedDamageLocked(source.ActorId, targetId, actionId, normalizedActionName, amount, critical, directHit, timeUtc);
                    return;
                }

                recentHostilePlayerActions.Add(new RecentHostilePlayerAction(
                    source,
                    targetId,
                    actionId,
                    normalizedActionName,
                    timeUtc)
                {
                    ObservedDamageAmount = amount,
                    ObservedCritical = critical,
                    ObservedDirectHit = directHit,
                });
                if (IsFocusedPlayerDotDiagnosticAction(actionId))
                {
                    LogFocusedPlayerDotDiagnosticLocked(
                        timeUtc,
                        $"seed-new:0x{source.ActorId:X8}:0x{targetId:X8}:0x{actionId:X8}",
                        $"记录伤害种子：source={source.Name}/0x{source.ActorId:X8}，target={ResolveCombatTimelineTargetName(targetId, timeUtc)}/0x{targetId:X8}，action={FormatActionNameWithId(actionName, actionId)}，amount={amount}，crit={critical}，dh={directHit}。");
                }

                NoteWildfireWeaponskillContributionLocked(source.ActorId, targetId, actionId, normalizedActionName, amount, critical, directHit, timeUtc);
                RefreshActivePlayerDotEstimatedDamageLocked(source.ActorId, targetId, actionId, normalizedActionName, amount, critical, directHit, timeUtc);
            }
            catch (Exception ex)
            {
                LogHelper.Debug(
                    "统计",
                    ex,
                    $"记录玩家 DOT 伤害种子失败：sourceId=0x{sourceId:X8}，targetId=0x{targetId:X8}，actionId=0x{actionId:X8}，amount={amount}。");
            }
        }
    }

    public void ObservePotentialPlayerDotDamageSeed(
        uint sourceId,
        uint targetId,
        uint actionId,
        string actionName,
        long amount,
        bool critical,
        bool directHit,
        DateTime timeUtc)
        => ObservePotentialPlayerHostileActionSample(sourceId, targetId, actionId, actionName, amount, critical, directHit, timeUtc);

    public bool TryRecordPlayerDotDamage(
        uint sourceId,
        uint targetId,
        uint actionId,
        string actionName,
        long amount,
        bool critical,
        DateTime timeUtc,
        string zoneName)
    {
        if (amount <= 0)
            return false;

        lock (gate)
        {
            try
            {
                currentEncounter.ZoneName = NormalizeZoneName(zoneName);
                TrimRecentHostilePlayerActionsLocked(timeUtc);
                DecayActivePlayerDotStatesLocked(timeUtc);
                TrimInactivePlayerDotsLocked(timeUtc);

                if (!TryResolvePlayerDotAttributionLocked(sourceId, targetId, actionId, actionName, timeUtc, out var dotState))
                    return false;

                var source = dotState.Source;
                var loggedTargetName = ResolveCombatTimelineTargetName(targetId, timeUtc);
                var encounterActionName = NormalizeActionName(dotState.ActionName);
                var dotActionName = FormatActionNameWithId(encounterActionName, dotState.ActionId);
                var wasStarted = currentEncounter.Started;
                var resolvedCritical = ResolvePlayerDotCritical(source.ActorId, dotState, critical, timeUtc);

                currentEncounter.RecordOutgoingDamage(source, encounterActionName, amount, resolvedCritical, false, timeUtc, isDotDamage: true);
                AppendEncounterStartIfNeededLocked(wasStarted, timeUtc);
                AppendCombatTimelineEntryLocked(
                    timeUtc,
                    CombatTimelineEntryKind.Damage,
                    $"{source.Name} 使用{dotActionName} 攻击 {loggedTargetName}，造成 {CreateDamageString(amount, useSuffix: true, useDecimals: true)} 伤害{FormatCriticalSuffix(resolvedCritical)}。",
                    source.Name,
                    loggedTargetName,
                    actorIsFriendly: true,
                    targetIsFriendly: false,
                    actionText: dotActionName);

                dotState.LastAttributedTickUtc = timeUtc;
                dotState.TickCount++;
                AdvancePlayerDotTickSchedule(dotState);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Error(
                    "统计",
                    ex,
                    $"回补玩家 DOT 伤害失败：sourceId=0x{sourceId:X8}，targetId=0x{targetId:X8}，actionId=0x{actionId:X8}，amount={amount}。");
                return false;
            }
        }
    }

    private void PollActivePlayerDots(DateTime nowUtc, bool inCombat)
    {
        try
        {
            var perfStart = System.Diagnostics.Stopwatch.GetTimestamp();
            var perfLast = perfStart;
            var perfParts = new List<string>(8);
            TrimRecentHostilePlayerActionsLocked(nowUtc);

            if (!inCombat && !currentEncounter.Started)
            {
                activePlayerDots.Clear();
                activeWildfires.Clear();
                return;
            }

            if (nowUtc - lastPlayerDotStatusPollUtc < PlayerDotStatusPollInterval)
                return;

            lastPlayerDotStatusPollUtc = nowUtc;
            DecayActivePlayerDotStatesLocked(nowUtc);
            var targetActorIds = recentHostilePlayerActions
                .Where(action => nowUtc - action.ObservedAtUtc <= PlayerDotTargetStatusRefreshWindow)
                .Where(static action => PlayerDotCatalog.IsKnownPlayerDotAction(action.ActionId) || action.ActionId == WildfireActionId)
                .Select(static action => action.TargetActorId)
                .Where(static actorId => actorId is not 0 and not InvalidActorId)
                .Distinct()
                .ToList();

            var targetBatch = BuildRoundRobinBatch(targetActorIds, ref nextPlayerDotTargetPollIndex, PlayerDotMaxHostileTargetsPerPoll);
            foreach (var targetActorId in targetBatch)
            {
                try
                {
                    if (!TryGetHostileBattleTarget(targetActorId, out var hostileBattleNpc))
                    {
                        RemoveActivePlayerDotsForTargetLocked(targetActorId);
                        RemoveActiveWildfiresForTargetLocked(targetActorId);
                        continue;
                    }

                    if (!hostileBattleNpc.IsTargetable)
                    {
                        RemoveActivePlayerDotsForTargetLocked(targetActorId);
                        RemoveActiveWildfiresForTargetLocked(targetActorId);
                        continue;
                    }

                    var preferredRecentActions = BuildPreferredRecentDotActionsForTarget(targetActorId);
                    if (preferredRecentActions.Count == 0)
                    {
                        CapturePlayerDotStatusesForHostileTargetLocked(hostileBattleNpc, nowUtc);
                    }
                    else
                    {
                        foreach (var recentAction in preferredRecentActions)
                        {
                            CapturePlayerDotStatusesForHostileTargetLocked(
                                hostileBattleNpc,
                                nowUtc,
                                preferredSourceActorId: recentAction.Source.ActorId,
                                preferredActionId: recentAction.ActionId,
                                preferredActionName: recentAction.ActionName);
                        }
                    }

                    CaptureActiveWildfiresForHostileTargetLocked(hostileBattleNpc, nowUtc);
                }
                catch (Exception ex)
                {
                    RemoveActivePlayerDotsForTargetLocked(targetActorId);
                    RemoveActiveWildfiresForTargetLocked(targetActorId);
                    LogHelper.Error(
                        "统计",
                        ex,
                        $"轮询玩家 DOT 目标失败：targetId=0x{targetActorId:X8}，异常={ex.GetType().Name}: {ex.Message}");
                }
            }
            MarkStatsPerfSegment("dotTargets", ref perfLast, perfParts);

            if (ShouldPollSourceOwnedPlayerDotStatuses(nowUtc))
            {
                var friendlyActors = EnumerateTrackedPartyBattleCharas().ToList();
                var friendlyBatch = BuildRoundRobinBatch(friendlyActors, ref nextPlayerDotFriendlyPollIndex, PlayerDotMaxFriendlyActorsPerPoll);
                foreach (var friendlyActor in friendlyBatch)
                {
                    try
                    {
                        CaptureSourceOwnedPlayerDotStatusesForFriendlyActorLocked(friendlyActor, nowUtc);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Debug(
                            "统计",
                            ex,
                            $"轮询友方自挂 DOT 状态失败：actorId=0x{ResolveBattleCharaActorId(friendlyActor):X8}。");
                    }
                }
            }
            MarkStatsPerfSegment("dotFriendly", ref perfLast, perfParts);

            try
            {
                SimulateActivePlayerDotTicksLocked(nowUtc);
                TryRecordPendingWildfireDetonationsLocked(nowUtc);
                MarkStatsPerfSegment("dotSim", ref perfLast, perfParts);
            }
            catch (Exception ex)
            {
                LogHelper.Error(
                    "统计",
                    ex,
                    $"模拟玩家 DOT tick 失败：异常={ex.GetType().Name}: {ex.Message}");
            }

            try
            {
                TrimInactivePlayerDotsLocked(nowUtc);
                TrimInactiveWildfiresLocked(nowUtc);
                MarkStatsPerfSegment("dotTrim", ref perfLast, perfParts);
            }
            catch (Exception ex)
            {
                LogHelper.Error(
                    "统计",
                    ex,
                    $"清理玩家 DOT 活跃状态失败：异常={ex.GetType().Name}: {ex.Message}");
            }

            LogStatsPerfIfSlow(perfStart, perfParts, inCombat);
        }
        catch (Exception ex)
        {
            LogHelper.Error(
                "统计",
                ex,
                $"轮询玩家 DOT 状态失败，已自动跳过本轮刷新。异常={ex.GetType().Name}: {ex.Message}");
        }
    }

    private bool ShouldPollSourceOwnedPlayerDotStatuses(DateTime nowUtc)
        => activePlayerDots.Values.Any(static state => state.SkillEntry?.StatusOwnerKind == PlayerDotStatusOwnerKind.SourceActor)
           || recentHostilePlayerActions.Any(action => nowUtc - action.ObservedAtUtc <= PlayerDotSourceOwnedTargetResolutionWindow);

    private IReadOnlyList<RecentHostilePlayerAction> BuildPreferredRecentDotActionsForTarget(uint targetActorId)
    {
        for (var index = recentHostilePlayerActions.Count - 1; index >= 0; index--)
        {
            var action = recentHostilePlayerActions[index];
            if (!PlayerDotCatalog.IsKnownPlayerDotAction(action.ActionId))
                continue;

            if (!AreEquivalentActorIds(action.TargetActorId, targetActorId))
                continue;

            return [action];
        }

        return [];
    }

    private static IReadOnlyList<T> BuildRoundRobinBatch<T>(IReadOnlyList<T> items, ref int nextIndex, int maxCount)
    {
        if (items.Count == 0 || maxCount <= 0)
        {
            nextIndex = 0;
            return [];
        }

        var count = Math.Min(maxCount, items.Count);
        var start = ((nextIndex % items.Count) + items.Count) % items.Count;
        var result = new List<T>(count);
        for (var offset = 0; offset < count; offset++)
            result.Add(items[(start + offset) % items.Count]);

        nextIndex = (start + count) % items.Count;
        return result;
    }


    private void RemoveActivePlayerDotsForTargetLocked(uint targetActorId)
    {
        if (targetActorId is 0 or InvalidActorId || activePlayerDots.Count == 0)
            return;

        var staleKeys = activePlayerDots.Keys
            .Where(key => AreEquivalentActorIds(key.TargetActorId, targetActorId))
            .ToList();
        foreach (var key in staleKeys)
        {
            if (activePlayerDots.TryGetValue(key, out var state)
                && IsFocusedPlayerDotDiagnosticState(state))
            {
                var nowUtc = DateTime.UtcNow;
                LogFocusedPlayerDotDiagnosticLocked(
                    nowUtc,
                    $"remove-target:0x{key.SourceActorId:X8}:0x{key.TargetActorId:X8}:0x{key.StatusId:X8}",
                    $"按目标清理活跃状态：{BuildFocusedPlayerDotDiagnosticStateText(state, nowUtc)}，targetId=0x{targetActorId:X8}。");
            }

            activePlayerDots.Remove(key);
        }
    }


    private void TrimRecentHostilePlayerActionsLocked(DateTime nowUtc)
        => recentHostilePlayerActions.RemoveAll(action => nowUtc - action.ObservedAtUtc > PlayerDotRecentActionTtl);

    private void DecayActivePlayerDotStatesLocked(DateTime nowUtc)
    {
        var states = BuildRoundRobinBatch(activePlayerDots.Values.ToList(), ref nextPlayerDotDecayIndex, PlayerDotMaxDecayStatesPerPoll);
        foreach (var state in states)
            DecayActivePlayerDotStateRemainingTime(state, nowUtc);
    }

    private static void DecayActivePlayerDotStateRemainingTime(ActivePlayerDotState state, DateTime nowUtc)
    {
        var elapsed = nowUtc - state.LastSeenUtc;
        if (elapsed <= TimeSpan.Zero)
            return;

        if (state.RemainingTimeSeconds > 0f)
            state.RemainingTimeSeconds = Math.Max(0f, state.RemainingTimeSeconds - (float)elapsed.TotalSeconds);

        state.LastSeenUtc = nowUtc;
    }



}
