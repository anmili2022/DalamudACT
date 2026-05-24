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
    private static readonly TimeSpan PlayerDotStatusPollInterval = TimeSpan.FromMilliseconds(100);
    // DoT 只在对应状态存续且上一次结算满 3 秒后才进入下一次归因。
    private static readonly TimeSpan PlayerDotTickInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PlayerDotTickJitterAllowance = TimeSpan.FromMilliseconds(250);
    // 2.5 秒对 DoT 来说太短：状态确认和首跳补算容易在种子过期后才命中，
    // 结果就会退回到粗糙兜底。这里放宽到 35 秒，尽量覆盖完整的 30 秒 DoT 观察窗口，
    // 避免白魔 / 贤者这类长 DoT 在后续状态刷新时拿不到同技能样本。
    private static readonly TimeSpan PlayerDotRecentActionTtl = TimeSpan.FromSeconds(35);
    private static readonly TimeSpan PlayerDotSourceOwnedTargetResolutionWindow = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PlayerDotStatusGracePeriod = TimeSpan.FromSeconds(1.0);
    private static readonly TimeSpan PlayerDotDebugLogThrottle = TimeSpan.FromSeconds(1.0);
    private static readonly TimeSpan PlayerDotFocusedDiagnosticLogThrottle = TimeSpan.FromSeconds(2.0);
    private static readonly TimeSpan WildfireStatusGracePeriod = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan WildfireDetonationTimingAllowance = TimeSpan.FromMilliseconds(600);
    private const double ObservedPlayerDotCriticalHitMultiplier = 1.6d;
    private const double ObservedPlayerDotDirectHitMultiplier = 1.25d;
    private const double SimulatedDotCriticalMultiplier = ObservedPlayerDotCriticalHitMultiplier;
    private static readonly Regex ActionDescriptionPotencyRegex = new(
        @"(?:威力|Potency)\s*[：:]\s*(?<potency>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ActionDescriptionDotPotencyRegex = new(
        @"(?:持续伤害|damage over time|継続ダメージ)[\s\S]{0,160}?(?:威力|Potency)\s*[：:]\s*(?<potency>\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private const uint WildfireActionId = 2878;
    private const uint WildfireStatusId = 0x35D;
    private const int WildfirePotencyPerWeaponskill = 240;
    private const int WildfireMaxWeaponskillCount = 6;
    // ACT 把野火最终结算记成 24|DoT|35D，实测口径接近同等威力直伤估算值的 1/3。
    // 如果直接拿武器技直伤样本按威力比例换算，会稳定高估约 1.5k~2k。
    private const double WildfireDotLikeDamageScale = 1d / 3d;
    private static readonly HashSet<uint> FocusedPlayerDotDiagnosticActionIds =
    [
        23,    // 骑士：厄运流转
        3639,  // 暗黑骑士：腐秽大地
        16153, // 绝枪战士：音速破
        16159, // 绝枪战士：弓形冲波
        121,   // 白魔法师：疾风
        132,   // 白魔法师：烈风
        16532, // 白魔法师：天辉
        16540, // 学者：蛊毒法
        29233, // 学者：蛊毒法（等级同步/旧日志兼容）
        37012, // 学者：埋伏之毒
        3599,  // 占星术士：烧灼
        3608,  // 占星术士：炽灼
        16554, // 占星术士：焚灼
        17806, // 占星术士：焚灼（旧日志兼容）
        24293, // 贤者：均衡注药
        24308, // 贤者：均衡注药II
        24314, // 贤者：均衡注药III
        29257, // 贤者：均衡注药III（等级同步/旧日志兼容）
        35881, // 贤者：均衡注药III（等级同步/旧日志兼容）
        24297, // 贤者：均衡失衡
        37032, // 贤者：均衡失衡
        36986, // 黑魔法师：高闪雷
        36987, // 黑魔法师：高震雷
        100,   // 吟游诗人：毒咬箭
        113,   // 吟游诗人：风蚀箭
        7406,  // 吟游诗人：烈毒咬箭
        8836,  // 吟游诗人：烈毒咬箭（等级同步/旧日志兼容）
        7407,  // 吟游诗人：狂风蚀箭
        8837,  // 吟游诗人：狂风蚀箭（等级同步/旧日志兼容）
        16523, // 召唤师：星极超流（Slipstream）
        25837, // 召唤师：星极超流（Slipstream）
        29669, // 召唤师：星极超流（Slipstream，等级同步/旧日志兼容）
    ];
    private static readonly HashSet<uint> FocusedPlayerDotDiagnosticStatusIds =
    [
        0xF8,  // 骑士：厄运流转
        0x2ED, // 暗黑骑士：腐秽大地
        0x72D, // 绝枪战士：音速破
        0x72E, // 绝枪战士：弓形冲波
        0x8F,  // 白魔法师：疾风
        0x90,  // 白魔法师：烈风
        0x74F, // 白魔法师：天辉
        0x7F3, // 白魔法师：天辉（等级同步/旧日志兼容）
        0x767, // 学者：蛊毒法
        0x7F7, // 学者：蛊毒法（等级同步/旧日志兼容）
        0xC11, // 学者：蛊毒法（等级同步/旧日志兼容）
        0xF2B, // 学者：埋伏之毒
        0x346, // 占星术士：烧灼
        0x34B, // 占星术士：炽灼
        0x759, // 占星术士：焚灼
        0x7F9, // 占星术士：焚灼（旧日志兼容）
        0xA36, // 贤者：均衡注药
        0xA37, // 贤者：均衡注药II
        0xA38, // 贤者：均衡注药III
        0xB30, // 贤者：均衡注药III（等级同步/旧日志兼容）
        0xC24, // 贤者：均衡注药III（等级同步/旧日志兼容）
        0xF28, // 贤者：均衡注药III（等级同步/旧日志兼容）
        0xF39, // 贤者：均衡失衡
        0xF88, // 贤者：均衡注药III（等级同步/旧日志兼容）
        0xF1F, // 黑魔法师：高闪雷
        0xF20, // 黑魔法师：高震雷
        0x7C,  // 吟游诗人：毒咬箭
        0x81,  // 吟游诗人：风蚀箭
        0x4B0, // 吟游诗人：烈毒咬箭
        0x4B1, // 吟游诗人：狂风蚀箭
        0x529, // 吟游诗人：烈毒咬箭（等级同步/旧日志兼容）
        0x52A, // 吟游诗人：狂风蚀箭（等级同步/旧日志兼容）
        0xA92, // 召唤师：星极超流（Slipstream）
        0xC99, // 召唤师：星极超流（Slipstream，等级同步/旧日志兼容）
    ];
    private static readonly IReadOnlyDictionary<uint, int> WildfireAnchorPotencies = new Dictionary<uint, int>
    {
        { 2866, 140 },  // 分裂弹
        { 2868, 210 },  // 独头弹（连击）
        { 2872, 240 },  // 热弹
        { 2873, 270 },  // 狙击弹（连击）
        { 7410, 200 },  // 热冲击
        { 7411, 220 },  // 热分裂弹
        { 7412, 320 },  // 热独头弹（连击）
        { 7413, 420 },  // 热狙击弹（连击）
        { 16498, 660 }, // 钻头
        { 16500, 660 }, // 空气锚
        { 25788, 660 }, // 回转飞锯
        { 36978, 240 }, // 烈焰弹 / Blazing Shot
        { 36981, 660 }, // 掘地飞轮 / Excavator
        { 36982, 900 }, // 全金属爆发 / Full Metal Field
    };

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
            var targetActorIds = activePlayerDots.Keys
                .Select(static key => key.TargetActorId)
                .Concat(activeWildfires.Keys.Select(static key => key.TargetActorId))
                .Concat(recentHostilePlayerActions.Select(static action => action.TargetActorId))
                .Where(static actorId => actorId is not 0 and not InvalidActorId)
                .Distinct()
                .ToList();

            foreach (var targetActorId in targetActorIds)
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

                    var preferredRecentActions = recentHostilePlayerActions
                        .Where(action => AreEquivalentActorIds(action.TargetActorId, targetActorId))
                        .Where(action => PlayerDotCatalog.IsKnownPlayerDotAction(action.ActionId))
                        .OrderByDescending(action => action.ObservedAtUtc)
                        .GroupBy(action => action.Source.ActorId)
                        .Select(static group => group.First())
                        .ToList();
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

            foreach (var friendlyActor in EnumerateTrackedPartyBattleCharas())
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

            try
            {
                SimulateActivePlayerDotTicksLocked(nowUtc);
                TryRecordPendingWildfireDetonationsLocked(nowUtc);
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
            }
            catch (Exception ex)
            {
                LogHelper.Error(
                    "统计",
                    ex,
                    $"清理玩家 DOT 活跃状态失败：异常={ex.GetType().Name}: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(
                "统计",
                ex,
                $"轮询玩家 DOT 状态失败，已自动跳过本轮刷新。异常={ex.GetType().Name}: {ex.Message}");
        }
    }

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

    private void RemoveActiveWildfiresForTargetLocked(uint targetActorId)
    {
        if (targetActorId is 0 or InvalidActorId || activeWildfires.Count == 0)
            return;

        var staleKeys = activeWildfires.Keys
            .Where(key => AreEquivalentActorIds(key.TargetActorId, targetActorId))
            .ToList();
        foreach (var key in staleKeys)
            activeWildfires.Remove(key);
    }

    private void CaptureActiveWildfiresForHostileTargetLocked(IBattleNpc hostileTarget, DateTime nowUtc)
    {
        var targetActorId = ResolveBattleCharaActorId(hostileTarget);
        if (targetActorId is 0 or InvalidActorId)
            return;

        var seenKeys = new HashSet<PlayerWildfireKey>();
        foreach (var status in EnumerateStatusEntries(hostileTarget))
        {
            try
            {
                if (!TryCreateOrRefreshActiveWildfireStateLocked(status, targetActorId, nowUtc, out var key))
                    continue;

                seenKeys.Add(key);
            }
            catch (Exception ex)
            {
                LogHelper.Debug(
                    "统计",
                    ex,
                    $"读取野火状态条目失败：targetId=0x{targetActorId:X8}。");
            }
        }

        var disappearedStates = activeWildfires.Values
            .Where(state =>
                AreEquivalentActorIds(state.Key.TargetActorId, targetActorId)
                && !seenKeys.Contains(state.Key))
            .ToList();
        foreach (var state in disappearedStates)
        {
            if (state.DetonationRecorded
                || !hostileTarget.IsTargetable
                || nowUtc - state.LastSeenUtc < WildfireStatusGracePeriod)
                continue;

            _ = TryRecordWildfireDetonationLocked(state, nowUtc);
        }
    }

    private bool TryCreateOrRefreshActiveWildfireStateLocked(object status, uint targetActorId, DateTime nowUtc, out PlayerWildfireKey key)
    {
        key = default;

        var statusId = GetStatusId(status);
        if (statusId != WildfireStatusId)
            return false;

        var rawSourceActorId = ResolveStatusSourceActorId(status);
        if (!TryResolveTrackedSource(rawSourceActorId, nowUtc, out var source) || source.Kind != TrackedActorKind.Player)
            return false;

        var statusName = TryGetStatusGameDataText(status, "Name");
        var actionName = string.IsNullOrWhiteSpace(statusName)
            ? "野火"
            : NormalizeActionName(statusName);
        var remainingTimeSeconds = Math.Max(0f, GetStatusRemainingTime(status));
        var stackCount = ResolveWildfireStackCount(status);

        key = new PlayerWildfireKey(targetActorId, source.ActorId, statusId);
        if (activeWildfires.TryGetValue(key, out var existing))
        {
            var isRefresh = remainingTimeSeconds > existing.RemainingTimeSeconds + 0.5f
                            || nowUtc - existing.ExpectedDetonationUtc > WildfireStatusGracePeriod;
            if (isRefresh)
                existing.Reset(source, actionName, nowUtc, remainingTimeSeconds, stackCount);
            else
                existing.Refresh(source, actionName, nowUtc, remainingTimeSeconds, stackCount);

            return true;
        }

        activeWildfires[key] = new ActiveWildfireState(
            key,
            source,
            actionName,
            nowUtc,
            remainingTimeSeconds,
            stackCount);
        return true;
    }

    private int ResolveWildfireStackCount(object status)
    {
        var rawStackCount = TryGetStatusParam(status);
        if (rawStackCount <= 0)
            return 0;

        return Math.Clamp(rawStackCount, 0, WildfireMaxWeaponskillCount);
    }

    private void NoteWildfireWeaponskillContributionLocked(
        uint sourceActorId,
        uint targetActorId,
        uint actionId,
        string actionName,
        long observedDamageAmount,
        bool critical,
        bool directHit,
        DateTime timeUtc)
    {
        if (activeWildfires.Count == 0
            || observedDamageAmount <= 0
            || !WildfireAnchorPotencies.TryGetValue(actionId, out var potency))
            return;

        var matchingStates = activeWildfires.Values
            .Where(state =>
                !state.DetonationRecorded
                && AreEquivalentActorIds(state.Key.SourceActorId, sourceActorId)
                && AreEquivalentActorIds(state.Key.TargetActorId, targetActorId)
                && timeUtc <= state.ExpectedDetonationUtc + WildfireDetonationTimingAllowance)
            .ToList();
        foreach (var state in matchingStates)
            state.NoteWeaponskillContribution(actionId, actionName, observedDamageAmount, potency, critical, directHit, timeUtc);
    }

    private void TryRecordPendingWildfireDetonationsLocked(DateTime nowUtc)
    {
        if (activeWildfires.Count == 0)
            return;

        var dueStates = activeWildfires.Values
            .Where(state =>
                !state.DetonationRecorded
                && state.EffectiveStackCount > 0
                && nowUtc + WildfireDetonationTimingAllowance >= state.ExpectedDetonationUtc)
            .ToList();
        foreach (var state in dueStates)
        {
            var detonationTimeUtc = state.ExpectedDetonationUtc <= nowUtc
                ? state.ExpectedDetonationUtc
                : nowUtc;
            _ = TryRecordWildfireDetonationLocked(state, detonationTimeUtc);
        }
    }

    private bool TryRecordWildfireDetonationLocked(ActiveWildfireState state, DateTime timeUtc)
    {
        if (state.DetonationRecorded)
            return false;

        var stackCount = state.EffectiveStackCount;
        if (stackCount <= 0)
            return false;

        var amount = EstimateWildfireDamageLocked(state, stackCount, timeUtc);
        if (amount <= 0)
            return false;

        var loggedTargetName = ResolveCombatTimelineTargetName(state.Key.TargetActorId, timeUtc);
        var encounterActionName = NormalizeActionName(state.ActionName);
        var wildfireActionText = FormatActionNameWithId(encounterActionName, WildfireActionId);
        var wasStarted = currentEncounter.Started;
        var contributionSummary = BuildWildfireContributionSummary(state);

        currentEncounter.RecordOutgoingDamage(state.Source, encounterActionName, amount, false, false, timeUtc);
        AppendEncounterStartIfNeededLocked(wasStarted, timeUtc);
        AppendCombatTimelineEntryLocked(
            timeUtc,
            CombatTimelineEntryKind.Damage,
            $"{state.Source.Name} 使用{wildfireActionText} 攻击 {loggedTargetName}，造成 {CreateDamageString(amount, useSuffix: true, useDecimals: true)} 伤害（模拟：{contributionSummary}）。",
            state.Source.Name,
            loggedTargetName,
            actorIsFriendly: true,
            targetIsFriendly: false,
            actionText: wildfireActionText);

        state.DetonationRecorded = true;
        return true;
    }

    private long EstimateWildfireDamageLocked(ActiveWildfireState state, int stackCount, DateTime nowUtc)
    {
        if (stackCount <= 0)
            return 0L;

        if (TryEstimateWildfireDamageFromContributionSamplesLocked(state, stackCount, out var contributionEstimatedDamage))
            return contributionEstimatedDamage;

        if (!TryResolveWildfireAnchorActionLocked(state.Key.SourceActorId, state.Key.TargetActorId, nowUtc, out var observedDamageAmount, out var anchorPotency))
            return 0L;

        var wildfirePotency = stackCount * WildfirePotencyPerWeaponskill;
        if (wildfirePotency <= 0 || anchorPotency <= 0)
            return 0L;

        return Math.Max(1L, (long)Math.Round(
            observedDamageAmount
            * (wildfirePotency / (double)anchorPotency)
            * WildfireDotLikeDamageScale));
    }

    private bool TryEstimateWildfireDamageFromContributionSamplesLocked(ActiveWildfireState state, int stackCount, out long estimatedDamage)
    {
        estimatedDamage = 0L;
        if (stackCount <= 0)
            return false;

        var normalizedDamagePerPotencySamples = state.ContributionSamples
            .Where(sample => sample.Potency > 0 && sample.ObservedDamageAmount > 0)
            .Select(static sample => sample.GetNormalizedDamagePerPotency())
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

        var wildfirePotency = stackCount * WildfirePotencyPerWeaponskill;
        if (wildfirePotency <= 0)
            return false;

        var averageDamagePerPotency = effectiveSamples.Average();
        if (averageDamagePerPotency <= 0d)
            return false;

        estimatedDamage = Math.Max(1L, (long)Math.Round(
            averageDamagePerPotency
            * wildfirePotency
            * WildfireDotLikeDamageScale));
        return estimatedDamage > 0L;
    }

    private static string BuildWildfireContributionSummary(ActiveWildfireState state)
    {
        var stackCount = state.EffectiveStackCount;
        if (state.ContributionSamples.Count == 0)
            return $"层数 {stackCount}，无有效样本";

        var groupedSamples = state.ContributionSamples
            .GroupBy(static sample => sample.ActionName)
            .OrderByDescending(static group => group.Count())
            .ThenBy(static group => group.Key, StringComparer.Ordinal)
            .Take(3)
            .Select(static group => $"{group.Key}×{group.Count()}");
        return $"层数 {stackCount}，样本 {string.Join("、", groupedSamples)}";
    }

    private bool TryResolveWildfireAnchorActionLocked(uint sourceActorId, uint targetActorId, DateTime nowUtc, out long observedDamageAmount, out int anchorPotency)
    {
        observedDamageAmount = 0L;
        anchorPotency = 0;

        static bool TryResolveAnchorPotency(RecentHostilePlayerAction action, out int potency)
            => WildfireAnchorPotencies.TryGetValue(action.ActionId, out potency);

        var targetMatch = recentHostilePlayerActions
            .Where(action =>
                AreEquivalentActorIds(action.Source.ActorId, sourceActorId)
                && AreEquivalentActorIds(action.TargetActorId, targetActorId)
                && action.ObservedDamageAmount > 0
                && nowUtc - action.ObservedAtUtc <= PlayerDotRecentActionTtl
                && TryResolveAnchorPotency(action, out _))
            .OrderByDescending(action => action.ObservedAtUtc)
            .FirstOrDefault();
        if (targetMatch != null && TryResolveAnchorPotency(targetMatch, out anchorPotency))
        {
            observedDamageAmount = targetMatch.ObservedDamageAmount;
            return true;
        }

        var sourceOnlyMatch = recentHostilePlayerActions
            .Where(action =>
                AreEquivalentActorIds(action.Source.ActorId, sourceActorId)
                && action.ObservedDamageAmount > 0
                && nowUtc - action.ObservedAtUtc <= PlayerDotRecentActionTtl
                && TryResolveAnchorPotency(action, out _))
            .OrderByDescending(action => action.ObservedAtUtc)
            .FirstOrDefault();
        if (sourceOnlyMatch != null && TryResolveAnchorPotency(sourceOnlyMatch, out anchorPotency))
        {
            observedDamageAmount = sourceOnlyMatch.ObservedDamageAmount;
            return true;
        }

        return false;
    }

    private void TrimInactiveWildfiresLocked(DateTime nowUtc)
    {
        if (activeWildfires.Count == 0)
            return;

        var staleKeys = activeWildfires
            .Where(pair =>
            {
                var state = pair.Value;
                if (state.DetonationRecorded)
                    return nowUtc - state.LastSeenUtc > PlayerDotStatusGracePeriod;

                return nowUtc - state.LastSeenUtc > PlayerDotStatusGracePeriod
                       && nowUtc - state.ExpectedDetonationUtc > PlayerDotStatusGracePeriod;
            })
            .Select(pair => pair.Key)
            .ToList();
        foreach (var key in staleKeys)
            activeWildfires.Remove(key);
    }

    private void TrimRecentHostilePlayerActionsLocked(DateTime nowUtc)
        => recentHostilePlayerActions.RemoveAll(action => nowUtc - action.ObservedAtUtc > PlayerDotRecentActionTtl);

    private void DecayActivePlayerDotStatesLocked(DateTime nowUtc)
    {
        foreach (var state in activePlayerDots.Values)
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

    private static bool IsFocusedPlayerDotDiagnosticAction(uint actionId)
        => actionId != 0 && FocusedPlayerDotDiagnosticActionIds.Contains(actionId);

    private static bool IsFocusedPlayerDotDiagnosticStatus(uint statusId)
        => statusId != 0 && FocusedPlayerDotDiagnosticStatusIds.Contains(statusId);

    private static bool IsFocusedPlayerDotDiagnosticSkill(PlayerDotSkillEntry? skillEntry)
        => skillEntry != null
           && (skillEntry.ActionIds.Any(IsFocusedPlayerDotDiagnosticAction)
               || skillEntry.StatusIds.Any(IsFocusedPlayerDotDiagnosticStatus));

    private static bool IsFocusedPlayerDotDiagnosticState(ActivePlayerDotState state)
        => IsFocusedPlayerDotDiagnosticStatus(state.Key.StatusId)
           || IsFocusedPlayerDotDiagnosticAction(state.ActionId)
           || IsFocusedPlayerDotDiagnosticSkill(state.SkillEntry);

    private string BuildFocusedPlayerDotDiagnosticStateText(ActivePlayerDotState state, DateTime nowUtc)
    {
        var targetName = ResolveCombatTimelineTargetName(state.Key.TargetActorId, nowUtc);
        var actionText = FormatActionNameWithId(state.ActionName, state.ActionId);
        var statusText = string.IsNullOrWhiteSpace(state.StatusName)
            ? $"0x{state.Key.StatusId:X}"
            : $"{state.StatusName}/0x{state.Key.StatusId:X}";
        return $"source={state.Source.Name}/0x{state.Key.SourceActorId:X8}，target={targetName}/0x{state.Key.TargetActorId:X8}，action={actionText}，status={statusText}";
    }

    private void LogFocusedPlayerDotDiagnosticLocked(
        DateTime nowUtc,
        string diagnosticKey,
        string message,
        bool includeRecentSummary = true)
    {
        if (!LogHelper.EnableDebugLog)
            return;

        var key = $"player-dot:{diagnosticKey}";
        if (playerDotDiagnosticLogTimestamps.TryGetValue(key, out var lastLogUtc)
            && nowUtc - lastLogUtc < PlayerDotFocusedDiagnosticLogThrottle)
        {
            return;
        }

        playerDotDiagnosticLogTimestamps[key] = nowUtc;
        if (playerDotDiagnosticLogTimestamps.Count > 256)
        {
            var staleKeys = playerDotDiagnosticLogTimestamps
                .OrderBy(static pair => pair.Value)
                .Take(64)
                .Select(static pair => pair.Key)
                .ToList();
            foreach (var staleKey in staleKeys)
                playerDotDiagnosticLogTimestamps.Remove(staleKey);
        }

        // 这组日志是短期现场对账用的聚焦诊断。仍然受 EnableDebugLog 控制，
        // 但使用 Info 级别写出，避免 Dalamud 当前日志级别不落 Debug 时无法在 dalamud.log 中检索到。
        LogHelper.Info("统计", $"DOT诊断：{message}");
    }

    private void SimulateActivePlayerDotTicksLocked(DateTime nowUtc)
    {
        if (activePlayerDots.Count == 0)
            return;

        var activeDots = activePlayerDots.Values.ToList();
        foreach (var dotState in activeDots)
        {
            if (dotState.RemainingTimeSeconds <= 0f)
                continue;

            if (!TryResolveTrackedSource(dotState.Key.SourceActorId, nowUtc, out var source) || source.Kind != TrackedActorKind.Player)
                continue;

            var ticksDue = ResolvePlayerDotTicksDue(dotState);
            if (ticksDue <= 0)
                continue;

            var tickTimeUtc = dotState.LastAttributedTickUtc;
            for (var index = 0; index < ticksDue; index++)
            {
                tickTimeUtc = tickTimeUtc == default
                    ? nowUtc
                    : tickTimeUtc + PlayerDotTickInterval;

                if (!TryRecordSimulatedPlayerDotTickLocked(dotState, source, tickTimeUtc))
                    break;
            }
        }
    }

    private static int ResolvePlayerDotTicksDue(ActivePlayerDotState dotState)
    {
        var currentRemaining = dotState.RemainingTimeSeconds;
        if (currentRemaining <= 0f)
            return 0;

        var tickThreshold = dotState.NextTickRemainingTimeSeconds;
        var allowance = (float)PlayerDotTickJitterAllowance.TotalSeconds;
        var tickInterval = (float)PlayerDotTickInterval.TotalSeconds;
        var ticksDue = 0;

        while (currentRemaining <= tickThreshold + allowance)
        {
            ticksDue++;
            tickThreshold -= tickInterval;

            if (ticksDue >= 16)
                break;
        }

        return ticksDue;
    }

    private bool TryRecordSimulatedPlayerDotTickLocked(ActivePlayerDotState dotState, TrackedActor source, DateTime tickTimeUtc)
    {
        try
        {
            var amount = dotState.EstimatedTickDamage;
            if (amount <= 0)
            {
                amount = ResolvePlayerDotEstimatedTickDamageLocked(source, dotState.Key.TargetActorId, dotState.ActionId, dotState.ActionName, dotState.StatusPotency, tickTimeUtc, dotState.SkillEntry);
                if (amount > 0)
                    dotState.EstimatedTickDamage = amount;
            }

            if (amount <= 0)
                return false;

            var loggedTargetName = ResolveCombatTimelineTargetName(dotState.Key.TargetActorId, tickTimeUtc);
            var encounterActionName = NormalizeActionName(dotState.ActionName);
            var dotActionName = FormatActionNameWithId(encounterActionName, dotState.ActionId);
            var wasStarted = currentEncounter.Started;
            var resolvedCritical = ResolvePlayerDotCritical(source.ActorId, dotState, reportedCritical: false, tickTimeUtc);
            if (resolvedCritical)
                amount = Math.Max(amount + 1L, (long)Math.Round(amount * SimulatedDotCriticalMultiplier));

            currentEncounter.RecordOutgoingDamage(source, encounterActionName, amount, resolvedCritical, false, tickTimeUtc, isDotDamage: true);
            AppendEncounterStartIfNeededLocked(wasStarted, tickTimeUtc);
            AppendCombatTimelineEntryLocked(
                tickTimeUtc,
                CombatTimelineEntryKind.Damage,
                $"{source.Name} 使用{dotActionName} 攻击 {loggedTargetName}，造成 {CreateDamageString(amount, useSuffix: true, useDecimals: true)} 伤害{FormatSimulatedCriticalSuffix(resolvedCritical)}。",
                source.Name,
                loggedTargetName,
                actorIsFriendly: true,
                targetIsFriendly: false,
                actionText: dotActionName);

            dotState.LastAttributedTickUtc = tickTimeUtc;
            dotState.TickCount++;
            if (IsFocusedPlayerDotDiagnosticState(dotState))
            {
                var tickMessage =
                    $"补算Tick：{BuildFocusedPlayerDotDiagnosticStateText(dotState, tickTimeUtc)}，amount={amount}，crit={resolvedCritical}，tick={dotState.TickCount}，remaining={dotState.RemainingTimeSeconds:0.00}s，next={dotState.NextTickRemainingTimeSeconds:0.00}s。";
                if (!dotState.FocusedDiagnosticFirstTickLogged)
                {
                    dotState.FocusedDiagnosticFirstTickLogged = true;
                    LogFocusedPlayerDotDiagnosticLocked(
                        tickTimeUtc,
                        $"tick-first:0x{dotState.Key.SourceActorId:X8}:0x{dotState.Key.TargetActorId:X8}:0x{dotState.Key.StatusId:X8}",
                        tickMessage);
                }
                else
                {
                    LogFocusedPlayerDotDiagnosticLocked(
                        tickTimeUtc,
                        $"tick:0x{dotState.Key.SourceActorId:X8}:0x{dotState.Key.TargetActorId:X8}:0x{dotState.Key.StatusId:X8}:{dotState.TickCount}",
                        tickMessage,
                        includeRecentSummary: false);
                }
            }

            AdvancePlayerDotTickSchedule(dotState);
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Error(
                "统计",
                ex,
                $"补算玩家 DOT 伤害失败：sourceId=0x{source.ActorId:X8}，targetId=0x{dotState.Key.TargetActorId:X8}，statusId=0x{dotState.Key.StatusId:X8}。");
            return false;
        }
    }

    private void RefreshActivePlayerDotEstimatedDamageLocked(
        uint sourceId,
        uint targetId,
        uint actionId,
        string actionName,
        long observedDamage,
        bool observedCritical,
        bool observedDirectHit,
        DateTime nowUtc)
    {
        if (activePlayerDots.Count == 0)
            return;

        var normalizedActionName = NormalizeActionName(actionName);
        var matchingStates = activePlayerDots.Values
            .Where(state =>
                AreEquivalentActorIds(state.Key.SourceActorId, sourceId)
                && AreEquivalentActorIds(state.Key.TargetActorId, targetId)
                && (state.ActionId == actionId
                    || state.Key.StatusId == actionId
                    || IsUnknownActionName(normalizedActionName)
                    || IsUnknownActionName(state.ActionName)
                    || string.Equals(state.ActionName, normalizedActionName, StringComparison.Ordinal)
                    || string.Equals(state.StatusName, normalizedActionName, StringComparison.Ordinal)))
            .ToList();

        if (matchingStates.Count == 0)
        {
            matchingStates = activePlayerDots.Values
                .Where(state =>
                    AreEquivalentActorIds(state.Key.TargetActorId, targetId)
                    && (state.ActionId == actionId
                        || state.Key.StatusId == actionId
                        || IsUnknownActionName(normalizedActionName)
                        || IsUnknownActionName(state.ActionName)
                        || string.Equals(state.ActionName, normalizedActionName, StringComparison.Ordinal)
                        || string.Equals(state.StatusName, normalizedActionName, StringComparison.Ordinal)))
                .ToList();
        }

        if (matchingStates.Count == 0)
        {
            matchingStates = activePlayerDots.Values
                .Where(state =>
                    AreEquivalentActorIds(state.Key.SourceActorId, sourceId)
                    && AreEquivalentActorIds(state.Key.TargetActorId, targetId)
                    && state.SkillEntry?.Anchors.Any(anchor => anchor.ActionIds.Contains(actionId)) == true)
                .ToList();
        }

        foreach (var state in matchingStates)
        {
            var sourceAverageDamage = ResolveObservedAverageDamage(state.Source.ActorId);
            var estimatedTickDamage = observedDamage > 0
                ? EstimatePlayerDotTickDamageFromObservedDamage(observedDamage, actionId, observedCritical, observedDirectHit, sourceAverageDamage, state.SkillEntry)
                : ResolvePlayerDotEstimatedTickDamageLocked(state.Source, targetId, state.ActionId, state.ActionName, state.StatusPotency, nowUtc, state.SkillEntry);

            if (estimatedTickDamage > 0)
            {
                var previousEstimatedTickDamage = state.EstimatedTickDamage;
                var previousEstimatedFromSeed = state.EstimatedTickDamageFromObservedSeed;
                state.EstimatedTickDamage = estimatedTickDamage;
                state.EstimatedTickDamageFromObservedSeed = observedDamage > 0;
                if (IsFocusedPlayerDotDiagnosticState(state)
                    && (previousEstimatedTickDamage != state.EstimatedTickDamage
                        || previousEstimatedFromSeed != state.EstimatedTickDamageFromObservedSeed))
                {
                    LogFocusedPlayerDotDiagnosticLocked(
                        nowUtc,
                        $"estimate-refresh:0x{state.Key.SourceActorId:X8}:0x{state.Key.TargetActorId:X8}:0x{state.Key.StatusId:X8}:0x{actionId:X8}",
                        $"刷新估算伤害：{BuildFocusedPlayerDotDiagnosticStateText(state, nowUtc)}，observedAction={FormatActionNameWithId(actionName, actionId)}，observedDamage={observedDamage}，crit={observedCritical}，dh={observedDirectHit}，估算 {previousEstimatedTickDamage}->{state.EstimatedTickDamage}，seed={state.EstimatedTickDamageFromObservedSeed}。");
                }
            }
        }
    }

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

    private void TrimInactivePlayerDotsLocked(DateTime nowUtc)
    {
        if (activePlayerDots.Count == 0)
            return;

        var staleKeys = new List<PlayerDotKey>();
        foreach (var pair in activePlayerDots)
        {
            try
            {
                var state = pair.Value;
                string? staleReason = null;
                if (state.RemainingTimeSeconds <= 0f)
                {
                    staleReason = "剩余时间归零";
                }
                else
                {
                    var targetObject = FindObjectByActorId(pair.Key.TargetActorId);
                    if (targetObject == null)
                        staleReason = "目标对象消失";
                    else if (!targetObject.IsTargetable)
                        staleReason = "目标不可选中";
                }

                if (staleReason != null)
                {
                    if (IsFocusedPlayerDotDiagnosticState(state))
                    {
                        LogFocusedPlayerDotDiagnosticLocked(
                            nowUtc,
                            $"trim:0x{pair.Key.SourceActorId:X8}:0x{pair.Key.TargetActorId:X8}:0x{pair.Key.StatusId:X8}:{staleReason}",
                            $"清理活跃状态：{BuildFocusedPlayerDotDiagnosticStateText(state, nowUtc)}，原因={staleReason}，remaining={state.RemainingTimeSeconds:0.00}s，tick={state.TickCount}。");
                    }

                    staleKeys.Add(pair.Key);
                }
            }
            catch
            {
                if (activePlayerDots.TryGetValue(pair.Key, out var state)
                    && IsFocusedPlayerDotDiagnosticState(state))
                {
                    LogFocusedPlayerDotDiagnosticLocked(
                        nowUtc,
                        $"trim-ex:0x{pair.Key.SourceActorId:X8}:0x{pair.Key.TargetActorId:X8}:0x{pair.Key.StatusId:X8}",
                        $"清理活跃状态：{BuildFocusedPlayerDotDiagnosticStateText(state, nowUtc)}，原因=检查异常，tick={state.TickCount}。");
                }

                staleKeys.Add(pair.Key);
            }
        }

        foreach (var key in staleKeys)
            activePlayerDots.Remove(key);
    }

    private string? ResolveRecentPlayerDotActionNameLocked(uint sourceActorId, uint targetActorId, DateTime nowUtc)
    {
        return recentHostilePlayerActions
            .Where(action =>
                AreEquivalentActorIds(action.Source.ActorId, sourceActorId)
                && AreEquivalentActorIds(action.TargetActorId, targetActorId)
                && nowUtc - action.ObservedAtUtc <= PlayerDotRecentActionTtl)
            .OrderByDescending(action => action.ObservedAtUtc)
            .Select(action => action.ActionName)
            .FirstOrDefault(actionName => !string.IsNullOrWhiteSpace(actionName));
    }

    private static bool IsPlayerDotTickReady(ActivePlayerDotState dotState, DateTime nowUtc)
        => nowUtc - dotState.LastAttributedTickUtc >= PlayerDotTickInterval - PlayerDotTickJitterAllowance;

    private static void AdvancePlayerDotTickSchedule(ActivePlayerDotState dotState)
        => dotState.NextTickRemainingTimeSeconds -= (float)PlayerDotTickInterval.TotalSeconds;

    private bool ResolvePlayerDotCritical(uint sourceActorId, ActivePlayerDotState dotState, bool reportedCritical, DateTime tickTimeUtc)
    {
        if (reportedCritical)
            return true;

        var critRate = ResolveObservedCritRate(sourceActorId);
        return IsSimulatedCritical(sourceActorId, dotState.Key.TargetActorId, dotState.Key.StatusId, dotState.TickCount, tickTimeUtc, critRate);
    }

    private double ResolveObservedCritRate(uint sourceActorId)
    {
        var combatant = currentEncounter.Combatants
            .FirstOrDefault(combatant => combatant.ActorId == sourceActorId);

        if (combatant == null || combatant.DirectDamageHits < 20)
            return 0.25d;

        var critRate = combatant.DirectDamageCritHits / (double)Math.Max(1, combatant.DirectDamageHits);
        return Math.Clamp(critRate, 0.05d, 0.95d);
    }

    private static bool IsSimulatedCritical(uint sourceActorId, uint targetActorId, uint statusId, int tickIndex, DateTime tickTimeUtc, double critRate)
    {
        if (critRate <= 0d)
            return false;

        if (critRate >= 1d)
            return true;

        unchecked
        {
            uint hash = 2166136261;
            var tickSeed = (ulong)(tickTimeUtc.ToUniversalTime().Ticks / PlayerDotTickInterval.Ticks);
            hash = (hash ^ sourceActorId) * 16777619;
            hash = (hash ^ targetActorId) * 16777619;
            hash = (hash ^ statusId) * 16777619;
            hash = (hash ^ (uint)tickSeed) * 16777619;
            hash = (hash ^ (uint)(tickSeed >> 32)) * 16777619;
            hash = (hash ^ (uint)tickIndex) * 16777619;

            var sample = hash / (double)uint.MaxValue;
            return sample < critRate;
        }
    }

    private static string ResolvePreferredDotActionName(string existingActionName, string newActionName, string statusName)
    {
        if (!IsUnknownActionName(existingActionName))
            return existingActionName;

        if (!IsUnknownActionName(newActionName))
            return newActionName;

        return !string.IsNullOrWhiteSpace(statusName)
            ? statusName
            : "\u672A\u77E5\u6301\u7EED\u4F24\u5BB3";
    }

    private static uint ResolvePreferredPlayerDotActionId(uint observedActionId, PlayerDotSkillEntry? skillEntry)
    {
        if (skillEntry == null)
            return observedActionId;

        var preferredActionId = skillEntry.GetPreferredActionId(observedActionId);
        return preferredActionId != 0 ? preferredActionId : observedActionId;
    }

    private static string ResolvePreferredPlayerDotActionName(string observedActionName, string statusName, PlayerDotSkillEntry? skillEntry)
    {
        if (!string.IsNullOrWhiteSpace(skillEntry?.SkillName))
            return NormalizeActionName(skillEntry.SkillName);

        if (!string.IsNullOrWhiteSpace(observedActionName))
            return NormalizeActionName(observedActionName);

        if (!string.IsNullOrWhiteSpace(statusName))
            return NormalizeActionName(statusName);

        return "\u672A\u77E5\u6301\u7EED\u4F24\u5BB3";
    }

    private static bool PreferredPlayerDotFallbackMatchesStatus(
        uint statusId,
        string statusName,
        uint? preferredActionId,
        string? preferredActionName)
    {
        if (!preferredActionId.HasValue || !PlayerDotCatalog.IsKnownPlayerDotAction(preferredActionId.Value))
            return false;

        var preferredSkill = PlayerDotCatalog.GetSkillByActionId(preferredActionId.Value);
        if (preferredSkill == null)
            return false;

        if (preferredSkill.StatusIds.Contains(statusId))
            return true;

        if (string.IsNullOrWhiteSpace(statusName))
            return false;

        var normalizedPreferredActionName = string.IsNullOrWhiteSpace(preferredActionName)
            ? string.Empty
            : NormalizeActionName(preferredActionName);
        var normalizedPreferredSkillName = NormalizeActionName(preferredSkill.SkillName);

        return string.Equals(statusName, normalizedPreferredActionName, StringComparison.Ordinal)
               || string.Equals(statusName, normalizedPreferredSkillName, StringComparison.Ordinal);
    }

    private bool IsPlayerDamageOverTimeStatus(object status)
    {
        var statusId = GetStatusId(status);
        if (statusId == 0)
            return false;

        if (dotStatusClassificationCache.TryGetValue(statusId, out var cached))
            return cached;

        var result = PlayerDotCatalog.IsKnownPlayerDotStatus(statusId);
        dotStatusClassificationCache[statusId] = result;
        return result;
    }

    private static string FormatPlayerDotActionName(string actionName)
        => $"{NormalizeActionName(actionName)}\uFF08\u6301\u7EED\u4F24\u5BB3\uFF09";

    private static bool IsUnknownActionName(string? actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName))
            return true;

        var normalized = actionName.Trim();
        return string.Equals(normalized, "鏈煡鎶€鑳?", StringComparison.Ordinal)
               || string.Equals(normalized, "\u672A\u77E5\u6280\u80FD", StringComparison.Ordinal)
               || normalized.StartsWith("\u6280\u80FD", StringComparison.Ordinal);
    }

    private static string BuildPlayerDotStatusSummary(IBattleNpc hostileTarget)
    {
        var statusSummaries = new List<string>();
        foreach (var status in EnumerateStatusEntries(hostileTarget))
        {
            try
            {
                var statusId = GetStatusId(status);
                if (statusId == 0)
                    continue;

                var statusName = TryGetStatusGameDataText(status, "Name") ?? "未知状态";
                var remainingTime = GetStatusRemainingTime(status);
                var sourceActorId = ResolveStatusSourceActorId(status);
                var sourceText = sourceActorId is 0 or InvalidActorId
                    ? "source=?"
                    : $"source=0x{sourceActorId:X8}";
                statusSummaries.Add($"{statusName}[{statusId}] {remainingTime:0.0}s {sourceText}");
                if (statusSummaries.Count >= 8)
                    break;
            }
            catch
            {
                // Ignore reflection issues while building debug summaries.
            }
        }

        return statusSummaries.Count == 0
            ? "无有效状态"
            : string.Join("；", statusSummaries);
    }

    private static bool TryGetHostileBattleTarget(uint targetId, out IBattleNpc hostileTarget)
    {
        hostileTarget = default!;
        try
        {
            var targetObject = FindObjectByActorId(targetId);
            if (targetObject is not IBattleNpc battleNpc)
                return false;

            if ((battleNpc.StatusFlags & StatusFlags.Hostile) == 0)
                return false;

            hostileTarget = battleNpc;
            return true;
        }
        catch
        {
            return false;
        }
    }

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
