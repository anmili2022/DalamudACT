using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
    private static readonly TimeSpan PlayerDotStatusPollInterval = TimeSpan.FromMilliseconds(500);
    private const int PlayerDotMaxHostileTargetsPerPoll = 1;
    private const int PlayerDotMaxFriendlyActorsPerPoll = 1;
    private const int PlayerDotMaxSimulatedStatesPerPoll = 4;
    private const int PlayerDotMaxSimulatedTicksPerPoll = 4;
    private const int PlayerDotMaxTrimStatesPerPoll = 12;
    private const int PlayerDotMaxDecayStatesPerPoll = 16;
    // DoT 只在对应状态存续且上一次结算满 3 秒后才进入下一次归因。
    private static readonly TimeSpan PlayerDotTickInterval = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PlayerDotTickJitterAllowance = TimeSpan.FromMilliseconds(250);
    // 2.5 秒对 DoT 来说太短：状态确认和首跳补算容易在种子过期后才命中，
    // 结果就会退回到粗糙兜底。这里放宽到 35 秒，尽量覆盖完整的 30 秒 DoT 观察窗口，
    // 避免白魔 / 贤者这类长 DoT 在后续状态刷新时拿不到同技能样本。
    private static readonly TimeSpan PlayerDotRecentActionTtl = TimeSpan.FromSeconds(35);
    private static readonly TimeSpan PlayerDotSourceOwnedTargetResolutionWindow = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PlayerDotTargetStatusRefreshWindow = TimeSpan.FromSeconds(10);
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
}
