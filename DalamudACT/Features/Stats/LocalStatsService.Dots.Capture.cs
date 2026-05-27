using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
    private bool CapturePlayerDotStatusesForHostileTargetLocked(
        IBattleNpc hostileTarget,
        DateTime nowUtc,
        uint? preferredSourceActorId = null,
        uint? preferredActionId = null,
        string? preferredActionName = null)
    {
        var targetActorId = ResolveBattleCharaActorId(hostileTarget);
        if (targetActorId is 0 or InvalidActorId)
            return false;

        var observedNewOrRefreshedState = false;
        var normalizedPreferredActionName = string.IsNullOrWhiteSpace(preferredActionName)
            ? string.Empty
            : NormalizeActionName(preferredActionName);

        try
        {
            foreach (var status in EnumerateStatusEntries(hostileTarget))
            {
                try
                {
                    var statusId = GetStatusId(status);
                    if (PlayerDotCatalog.GetSkillByStatusId(statusId)?.StatusOwnerKind == PlayerDotStatusOwnerKind.SourceActor)
                        continue;

                    if (!TryCreateActivePlayerDotStateLocked(
                            status,
                            targetActorId,
                            nowUtc,
                            preferredSourceActorId,
                            preferredActionId,
                            preferredActionName,
                            out var key,
                            out var state))
                    {
                        continue;
                    }

                    if (activePlayerDots.TryGetValue(key, out var existing))
                    {
                        var shouldRefreshTickSchedule = state.RemainingTimeSeconds > existing.RemainingTimeSeconds + 0.5f;
                        var previousEstimatedTickDamage = existing.EstimatedTickDamage;
                        var previousEstimatedFromSeed = existing.EstimatedTickDamageFromObservedSeed;
                        var matchesPreferredApplication = (!preferredSourceActorId.HasValue || AreEquivalentActorIds(key.SourceActorId, preferredSourceActorId.Value))
                            && (string.IsNullOrWhiteSpace(normalizedPreferredActionName)
                                || string.Equals(state.ActionName, normalizedPreferredActionName, StringComparison.Ordinal)
                                || string.Equals(state.StatusName, normalizedPreferredActionName, StringComparison.Ordinal));
                        if (state.ActionId != 0)
                            existing.ActionId = state.ActionId;
                        existing.ActionName = ResolvePreferredDotActionName(existing.ActionName, state.ActionName, state.StatusName);
                        existing.StatusName = string.IsNullOrWhiteSpace(state.StatusName) ? existing.StatusName : state.StatusName;
                        existing.LastSeenUtc = nowUtc;
                        existing.RemainingTimeSeconds = state.RemainingTimeSeconds;
                        if (state.SkillEntry != null)
                            existing.SkillEntry = state.SkillEntry;
                        if (state.EstimatedTickDamage > 0
                            && (state.EstimatedTickDamageFromObservedSeed
                                || !existing.EstimatedTickDamageFromObservedSeed
                                || existing.EstimatedTickDamage <= 0))
                        {
                            existing.EstimatedTickDamage = state.EstimatedTickDamage;
                            existing.EstimatedTickDamageFromObservedSeed = state.EstimatedTickDamageFromObservedSeed;
                        }

                        if (shouldRefreshTickSchedule)
                        {
                            existing.LastAttributedTickUtc = state.LastAttributedTickUtc;
                            existing.TickCount = 0;
                            existing.NextTickRemainingTimeSeconds = state.NextTickRemainingTimeSeconds;
                            if (matchesPreferredApplication)
                                observedNewOrRefreshedState = true;
                        }

                        if (IsFocusedPlayerDotDiagnosticState(existing)
                            && (shouldRefreshTickSchedule
                                || previousEstimatedTickDamage != existing.EstimatedTickDamage
                                || previousEstimatedFromSeed != existing.EstimatedTickDamageFromObservedSeed))
                        {
                            LogFocusedPlayerDotDiagnosticLocked(
                                nowUtc,
                                $"active-refresh:0x{key.SourceActorId:X8}:0x{key.TargetActorId:X8}:0x{key.StatusId:X8}:{shouldRefreshTickSchedule}",
                                $"刷新活跃状态：{BuildFocusedPlayerDotDiagnosticStateText(existing, nowUtc)}，刷新Tick={shouldRefreshTickSchedule}，估算 {previousEstimatedTickDamage}->{existing.EstimatedTickDamage}，seed={existing.EstimatedTickDamageFromObservedSeed}，remaining={existing.RemainingTimeSeconds:0.00}s，next={existing.NextTickRemainingTimeSeconds:0.00}s。");
                        }
                        continue;
                    }

                    activePlayerDots[key] = state;
                    if (IsFocusedPlayerDotDiagnosticState(state))
                    {
                        LogFocusedPlayerDotDiagnosticLocked(
                            nowUtc,
                            $"active-new:0x{key.SourceActorId:X8}:0x{key.TargetActorId:X8}:0x{key.StatusId:X8}",
                            $"激活状态：{BuildFocusedPlayerDotDiagnosticStateText(state, nowUtc)}，remaining={state.RemainingTimeSeconds:0.00}s，next={state.NextTickRemainingTimeSeconds:0.00}s，estimated={state.EstimatedTickDamage}，seed={state.EstimatedTickDamageFromObservedSeed}。");
                    }

                    if ((!preferredSourceActorId.HasValue || AreEquivalentActorIds(key.SourceActorId, preferredSourceActorId.Value))
                        && (string.IsNullOrWhiteSpace(normalizedPreferredActionName)
                            || string.Equals(state.ActionName, normalizedPreferredActionName, StringComparison.Ordinal)
                            || string.Equals(state.StatusName, normalizedPreferredActionName, StringComparison.Ordinal)))
                    {
                        observedNewOrRefreshedState = true;
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Debug(
                        "统计",
                        ex,
                        $"读取 DOT 状态条目失败：targetId=0x{targetActorId:X8}。");
                }
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(
                "统计",
                ex,
                $"读取敌方目标状态列表失败：targetId=0x{targetActorId:X8}。");
        }

        var hasMatchingActiveState = false;
        try
        {
                hasMatchingActiveState = preferredActionId.HasValue
                && activePlayerDots.Values.Any(state =>
                    AreEquivalentActorIds(state.Key.TargetActorId, targetActorId)
                    && (!preferredSourceActorId.HasValue || AreEquivalentActorIds(state.Key.SourceActorId, preferredSourceActorId.Value))
                    && (state.ActionId == preferredActionId.Value
                        || state.Key.StatusId == preferredActionId.Value
                        || (!string.IsNullOrWhiteSpace(normalizedPreferredActionName)
                            && (string.Equals(state.ActionName, normalizedPreferredActionName, StringComparison.Ordinal)
                                || string.Equals(state.StatusName, normalizedPreferredActionName, StringComparison.Ordinal)))));
        }
        catch (Exception ex)
        {
            LogHelper.Debug(
                "统计",
                ex,
                $"检查 DOT 活跃状态匹配失败：targetId=0x{targetActorId:X8}。");
        }

        if (!observedNewOrRefreshedState
            && !hasMatchingActiveState
            && preferredActionId.HasValue
            && PlayerDotCatalog.IsKnownPlayerDotAction(preferredActionId.Value)
            && LogHelper.EnableDebugLog
            && nowUtc - lastPlayerDotDebugLogUtc >= PlayerDotDebugLogThrottle)
        {
            try
            {
                lastPlayerDotDebugLogUtc = nowUtc;
                var preferredSourceText = preferredSourceActorId.HasValue
                    ? ResolveCombatTimelineSourceName(preferredSourceActorId.Value, nowUtc)
                    : "未知来源";
                var preferredActionText = FormatActionNameWithId(preferredActionName, preferredActionId.Value);
                var targetName = ResolveCombatTimelineTargetName(targetActorId, nowUtc);
                var statusSummary = BuildPlayerDotStatusSummary(hostileTarget);
                LogHelper.DebugRecent(
                    "统计",
                    $"DOT 状态未确认：source={preferredSourceText}，target={targetName}，action={preferredActionText}，status={statusSummary}。");
            }
            catch (Exception ex)
            {
                LogHelper.Debug(
                    "统计",
                    ex,
                    $"输出 DOT 状态调试摘要失败：targetId=0x{targetActorId:X8}，actionId=0x{preferredActionId.Value:X8}。");
            }
        }

        return observedNewOrRefreshedState;
    }

    private void CaptureSourceOwnedPlayerDotStatusesForFriendlyActorLocked(IBattleChara friendlyActor, DateTime nowUtc)
    {
        if (!TryGetTrackedBattleCharaActor(friendlyActor, out var source) || source.Kind != TrackedActorKind.Player)
            return;

        foreach (var status in EnumerateStatusEntries(friendlyActor))
        {
            try
            {
                var statusId = GetStatusId(status);
                var skillEntry = PlayerDotCatalog.GetSkillByStatusId(statusId);
                if (skillEntry?.StatusOwnerKind != PlayerDotStatusOwnerKind.SourceActor)
                    continue;

                if (!TryResolveSourceOwnedPlayerDotTargetActorIdLocked(source, statusId, skillEntry, nowUtc, out var targetActorId))
                    continue;

                if (!TryCreateActivePlayerDotStateLocked(
                        status,
                        targetActorId,
                        nowUtc,
                        preferredSourceActorId: source.ActorId,
                        preferredActionId: skillEntry.GetPreferredActionId(0),
                        preferredActionName: skillEntry.SkillName,
                        out var key,
                        out var state))
                {
                    continue;
                }

                if (activePlayerDots.TryGetValue(key, out var existing))
                {
                    var shouldRefreshTickSchedule = state.RemainingTimeSeconds > existing.RemainingTimeSeconds + 0.5f;
                    if (state.ActionId != 0)
                        existing.ActionId = state.ActionId;
                    existing.ActionName = ResolvePreferredDotActionName(existing.ActionName, state.ActionName, state.StatusName);
                    existing.StatusName = string.IsNullOrWhiteSpace(state.StatusName) ? existing.StatusName : state.StatusName;
                    existing.LastSeenUtc = nowUtc;
                    existing.RemainingTimeSeconds = state.RemainingTimeSeconds;
                    if (state.SkillEntry != null)
                        existing.SkillEntry = state.SkillEntry;
                    if (state.EstimatedTickDamage > 0
                        && (state.EstimatedTickDamageFromObservedSeed
                            || !existing.EstimatedTickDamageFromObservedSeed
                            || existing.EstimatedTickDamage <= 0))
                    {
                        existing.EstimatedTickDamage = state.EstimatedTickDamage;
                        existing.EstimatedTickDamageFromObservedSeed = state.EstimatedTickDamageFromObservedSeed;
                    }

                    if (shouldRefreshTickSchedule)
                    {
                        existing.LastAttributedTickUtc = state.LastAttributedTickUtc;
                        existing.TickCount = 0;
                        existing.NextTickRemainingTimeSeconds = state.NextTickRemainingTimeSeconds;
                    }

                    continue;
                }

                activePlayerDots[key] = state;
            }
            catch (Exception ex)
            {
                LogHelper.Debug(
                    "统计",
                    ex,
                    $"读取友方自挂 DOT 状态条目失败：sourceId=0x{source.ActorId:X8}。");
            }
        }
    }

    private bool TryResolveSourceOwnedPlayerDotTargetActorIdLocked(
        TrackedActor source,
        uint statusId,
        PlayerDotSkillEntry skillEntry,
        DateTime nowUtc,
        out uint targetActorId)
    {
        targetActorId = 0;

        var activeTargetIds = activePlayerDots.Values
            .Where(state =>
                state.SkillEntry?.StatusOwnerKind == PlayerDotStatusOwnerKind.SourceActor
                && AreEquivalentActorIds(state.Key.SourceActorId, source.ActorId)
                && state.Key.StatusId == statusId
                && state.RemainingTimeSeconds > 0f)
            .Select(state => state.Key.TargetActorId);
        if (TryResolveUniqueHostileTargetActorIdLocked(activeTargetIds, out targetActorId))
            return true;

        var skillSpecificTargetIds = recentHostilePlayerActions
            .Where(action =>
                AreEquivalentActorIds(action.Source.ActorId, source.ActorId)
                && nowUtc - action.ObservedAtUtc <= PlayerDotSourceOwnedTargetResolutionWindow
                && (skillEntry.ActionIds.Contains(action.ActionId)
                    || skillEntry.StatusIds.Contains(action.ActionId)
                    || skillEntry.Anchors.Any(anchor => anchor.ActionIds.Contains(action.ActionId))))
            .OrderByDescending(action => action.ObservedAtUtc)
            .Select(action => action.TargetActorId);
        if (TryResolveUniqueHostileTargetActorIdLocked(skillSpecificTargetIds, out targetActorId))
            return true;

        var recentTargetIds = recentHostilePlayerActions
            .Where(action =>
                AreEquivalentActorIds(action.Source.ActorId, source.ActorId)
                && nowUtc - action.ObservedAtUtc <= PlayerDotSourceOwnedTargetResolutionWindow)
            .OrderByDescending(action => action.ObservedAtUtc)
            .Select(action => action.TargetActorId);
        return TryResolveUniqueHostileTargetActorIdLocked(recentTargetIds, out targetActorId);
    }

    private static bool TryResolveUniqueHostileTargetActorIdLocked(IEnumerable<uint> candidateActorIds, out uint targetActorId)
    {
        targetActorId = 0;
        var uniqueTargetActorId = 0u;

        foreach (var candidateActorId in candidateActorIds)
        {
            if (!TryGetHostileBattleTarget(candidateActorId, out var hostileTarget) || !hostileTarget.IsTargetable)
                continue;

            var canonicalTargetActorId = ResolveBattleCharaActorId(hostileTarget);
            if (canonicalTargetActorId is 0 or InvalidActorId)
                continue;

            if (uniqueTargetActorId == 0)
            {
                uniqueTargetActorId = canonicalTargetActorId;
                continue;
            }

            if (!AreEquivalentActorIds(uniqueTargetActorId, canonicalTargetActorId))
                return false;
        }

        if (uniqueTargetActorId == 0)
            return false;

        targetActorId = uniqueTargetActorId;
        return true;
    }

    private bool TryCreateActivePlayerDotStateLocked(
        object status,
        uint targetActorId,
        DateTime nowUtc,
        uint? preferredSourceActorId,
        uint? preferredActionId,
        string? preferredActionName,
        out PlayerDotKey key,
        out ActivePlayerDotState state)
    {
        key = default;
        state = default!;

        var statusId = GetStatusId(status);
        if (statusId == 0 || !IsPlayerDamageOverTimeStatus(status))
            return false;

        var rawStatusName = TryGetStatusGameDataText(status, "Name");
        var statusName = string.IsNullOrWhiteSpace(rawStatusName)
            ? string.Empty
            : NormalizeActionName(rawStatusName);

        var rawSourceActorId = ResolveStatusSourceActorId(status);
        var hasRawSourceActorId = rawSourceActorId is > 0 and not InvalidActorId;
        if (!TryResolveTrackedSource(rawSourceActorId, nowUtc, out var source) || source.Kind != TrackedActorKind.Player)
        {
            // 目标身上可能同时存在 NPC 队友的同系 DoT，例如阿尔菲诺的“均衡注药III”。
            // 这类状态有明确 raw source，且 raw source 与当前玩家候选来源不同；
            // 它不属于玩家 DoT 归因路径，不应刷“未能解析玩家来源”的聚焦诊断。
            if (hasRawSourceActorId
                && preferredSourceActorId.HasValue
                && !AreEquivalentActorIds(rawSourceActorId, preferredSourceActorId.Value))
            {
                return false;
            }

            // 如果 raw source 已能解析成友方 NPC / 敌方 NPC，也说明它不是玩家 DoT。
            // 直接交给普通 ActionEffect 统计路径处理，不进入玩家 DoT 诊断。
            if (hasRawSourceActorId
                && TryResolveTrackedSource(rawSourceActorId, nowUtc, out var resolvedNonPlayerSource)
                && resolvedNonPlayerSource.Kind != TrackedActorKind.Player)
            {
                return false;
            }

            // Only fall back to the event-derived source when the status itself has no usable source.
            // If the status already points to someone else, do not reassign that DoT to self or party.
            if (hasRawSourceActorId
                || !preferredSourceActorId.HasValue
                || !PreferredPlayerDotFallbackMatchesStatus(statusId, statusName, preferredActionId, preferredActionName)
                || !TryResolveTrackedSource(preferredSourceActorId.Value, nowUtc, out source)
                || source.Kind != TrackedActorKind.Player)
            {
                if (IsFocusedPlayerDotDiagnosticStatus(statusId))
                {
                    LogFocusedPlayerDotDiagnosticLocked(
                        nowUtc,
                        $"create-source-fail:0x{targetActorId:X8}:0x{statusId:X8}:0x{rawSourceActorId:X8}",
                        $"状态存在但未能解析玩家来源：target={ResolveCombatTimelineTargetName(targetActorId, nowUtc)}/0x{targetActorId:X8}，status={statusName}/0x{statusId:X}，rawSource=0x{rawSourceActorId:X8}，preferredSource={(preferredSourceActorId.HasValue ? $"0x{preferredSourceActorId.Value:X8}" : "无")}，preferredAction={(preferredActionId.HasValue ? FormatActionNameWithId(preferredActionName, preferredActionId.Value) : "无")}。");
                }

                return false;
            }
        }

        if (source.Kind != TrackedActorKind.Player)
            return false;

        var preferredSourceMatchesResolvedSource = preferredSourceActorId.HasValue
            && AreEquivalentActorIds(preferredSourceActorId.Value, source.ActorId);

        var actionId = preferredSourceMatchesResolvedSource && preferredActionId.HasValue
            ? preferredActionId.Value
            : ResolveRecentPlayerDotActionIdLocked(source.ActorId, targetActorId, nowUtc);

        var actionName = preferredSourceMatchesResolvedSource
                         && !string.IsNullOrWhiteSpace(preferredActionName)
            ? NormalizeActionName(preferredActionName)
            : ResolveRecentPlayerDotActionNameLocked(source.ActorId, targetActorId, nowUtc);

        if (string.IsNullOrWhiteSpace(actionName))
            actionName = !string.IsNullOrWhiteSpace(statusName)
                ? statusName
                : "\u672A\u77E5\u6301\u7EED\u4F24\u5BB3";

        var statusPotency = TryGetStatusGameDataInt(status, "ParamModifier");
        var catalogSkill = PlayerDotCatalog.GetSkillByStatusId(statusId)
                           ?? PlayerDotCatalog.GetSkillByActionId(actionId);
        actionId = ResolvePreferredPlayerDotActionId(actionId, catalogSkill);
        actionName = ResolvePreferredPlayerDotActionName(actionName, statusName, catalogSkill);
        var recentAction = ResolveRecentPlayerDotObservedActionLocked(source.ActorId, targetActorId, actionName, nowUtc, catalogSkill);
        var estimatedTickDamage = ResolvePlayerDotEstimatedTickDamageLocked(source, targetActorId, actionId, actionName, statusPotency, nowUtc, catalogSkill);
        var estimatedTickDamageFromObservedSeed = recentAction?.ObservedDamageAmount > 0;

        key = new PlayerDotKey(targetActorId, source.ActorId, statusId);
        state = new ActivePlayerDotState(
            key,
            source,
            actionId,
            actionName,
            statusName,
            statusPotency,
            catalogSkill,
            estimatedTickDamage,
            estimatedTickDamageFromObservedSeed,
            nowUtc,
            nowUtc,
            Math.Max(0f, GetStatusRemainingTime(status)));
        return true;
    }

    private bool TryResolvePlayerDotAttributionLocked(
        uint sourceId,
        uint targetId,
        uint actionId,
        string actionName,
        DateTime nowUtc,
        out ActivePlayerDotState dotState)
    {
        dotState = default!;

        if (!TryGetHostileBattleTarget(targetId, out var hostileTarget))
            return false;

        var canonicalTargetActorId = ResolveBattleCharaActorId(hostileTarget);
        if (canonicalTargetActorId is 0 or InvalidActorId)
            return false;

        if (!hostileTarget.IsTargetable)
        {
            RemoveActivePlayerDotsForTargetLocked(canonicalTargetActorId);
            RemoveActiveWildfiresForTargetLocked(canonicalTargetActorId);
            return false;
        }

        var resolvedSourceActorId = 0u;
        if (TryResolveTrackedSource(sourceId, nowUtc, out var resolvedSource) && resolvedSource.Kind == TrackedActorKind.Player)
            resolvedSourceActorId = resolvedSource.ActorId;

        CapturePlayerDotStatusesForHostileTargetLocked(
            hostileTarget,
            nowUtc,
            preferredSourceActorId: resolvedSourceActorId == 0 ? null : resolvedSourceActorId,
            preferredActionId: actionId,
            preferredActionName: actionName);
        TrimInactivePlayerDotsLocked(nowUtc);

        var normalizedActionName = NormalizeActionName(actionName);
        var candidates = activePlayerDots
            .Where(pair =>
                AreEquivalentActorIds(pair.Key.TargetActorId, canonicalTargetActorId)
                && (resolvedSourceActorId == 0 || AreEquivalentActorIds(pair.Key.SourceActorId, resolvedSourceActorId)))
            .Select(pair => pair.Value)
            .ToList();
        if (candidates.Count == 0 && resolvedSourceActorId != 0)
        {
            candidates = activePlayerDots
                .Where(pair => AreEquivalentActorIds(pair.Key.TargetActorId, canonicalTargetActorId))
                .Select(pair => pair.Value)
                .ToList();
        }

        if (candidates.Count == 0)
            return false;

        var matureCandidates = candidates
            .Where(candidate => nowUtc - candidate.FirstSeenUtc >= PlayerDotStatusGracePeriod)
            .ToList();
        if (matureCandidates.Count > 0)
            candidates = matureCandidates;
        else
            return false;

        candidates = candidates
            .Where(candidate => IsPlayerDotTickReady(candidate, nowUtc))
            .ToList();
        if (candidates.Count == 0)
            return false;

        if (actionId != 0)
        {
            var statusIdMatch = candidates.Where(candidate => candidate.Key.StatusId == actionId).ToList();
            if (statusIdMatch.Count == 1)
            {
                dotState = statusIdMatch[0];
                return true;
            }
        }

        if (!IsUnknownActionName(normalizedActionName))
        {
            var actionNameMatch = candidates
                .Where(candidate =>
                    string.Equals(candidate.ActionName, normalizedActionName, StringComparison.Ordinal)
                    || (!string.IsNullOrWhiteSpace(candidate.StatusName)
                        && string.Equals(candidate.StatusName, normalizedActionName, StringComparison.Ordinal)))
                .ToList();
            if (actionNameMatch.Count == 1)
            {
                dotState = actionNameMatch[0];
                return true;
            }
        }

        if (candidates.Count == 1)
        {
            dotState = candidates[0];
            return true;
        }

        return false;
    }
}
