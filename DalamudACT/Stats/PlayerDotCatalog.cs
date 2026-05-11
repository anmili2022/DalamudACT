using System.Collections.Generic;
using System.Linq;

namespace DalamudACT;

/// <summary>
/// 玩家 DoT 静态表。
/// 规则：
/// - 这里只收录“会在目标身上形成持续伤害状态”的玩家技能；
/// - 只用 actionId / statusId 做白名单判断，不再靠技能名或描述文本猜测；
/// - 每个技能组都保留中文技能名，方便后续维护和补漏。
/// - 如果技能存在稳定可观测的“直伤 + DoT”组合，会补充 seedPotency / dotTickPotency，
///   用于把首段真实伤害反推出后续 DoT 单跳；没有填的数据继续走旧的兜底估算。
/// 
/// 数据整理参考：
/// - FFXIV 官方 Job Guide（确认当前 PvE DoT 技能）
/// - XIVAPI Action / Status（整理 actionId / statusId）
/// </summary>
internal static class PlayerDotCatalog
{
    public static readonly IReadOnlyList<PlayerDotJobEntry> Entries =
    [
        Job("骑士",
            Skill("厄运流转", [23], [248])),
        Job("战士"),
        Job("暗黑骑士"),
        Job("绝枪战士",
            Skill("音速破", [16153], [1837], seedPotency: 340, dotTickPotency: 120),
            Skill("弓形冲波", [16159], [1838], seedPotency: 150, dotTickPotency: 60)),

        Job("白魔法师",
            Skill("疾风", [121], [143]),
            Skill("烈风", [132], [144]),
            Skill("天辉", [16532], [1871, 2035], seedPotency: 85, dotTickPotency: 85)),
        Job("学者",
            Skill("毒菌", [17864], [179]),
            Skill("猛毒菌", [17865], [189]),
            Skill("蛊毒法", [16540, 29233], [1895, 2039, 3089], dotTickPotency: 85,
                anchors:
                [
                    Anchor("极炎法", [25865, 29231], potency: 320),
                ]),
            Skill("埋伏之毒", [37012], [3883], dotTickPotency: 3000)),
        Job("占星术士",
            Skill("烧灼", [3599], [838]),
            Skill("炽灼", [3608], [843]),
            Skill("焚灼", [16554, 17806], [1881, 2041], dotTickPotency: 70,
                anchors:
                [
                    Anchor("落陷凶星", [25871, 29242], potency: 270),
                ])),
        Job("贤者",
            Skill("均衡注药", [24293], [2616]),
            Skill("均衡注药II", [24308], [2864]),
            Skill("均衡注药III", [24314, 29257], [3108, 3897, 3976], dotTickPotency: 90,
                anchors:
                [
                    Anchor("注药III", [24312, 27822, 29256], potency: 380),
                ]),
            Skill("均衡失衡", [37032], [3897, 3976])),

        Job("武僧"),
        Job("龙骑士",
            Skill("樱花怒放", [88], [118, 1312]),
            // Chaotic Spring 存在前/背位与连击差异；这里优先采用官方连击威力 300 做近似。
            Skill("樱花缭乱", [25772, 29490], [2719], seedPotency: 300, dotTickPotency: 45)),
        Job("忍者"),
        Job("武士",
            Skill("彼岸花", [7489], [1228, 1319], seedPotency: 200, dotTickPotency: 50)),
        Job("钐镰客"),
        Job("蝰蛇剑士"),

        Job("吟游诗人",
            Skill("毒咬箭", [100], [124]),
            Skill("风蚀箭", [113], [129]),
            Skill("烈毒咬箭", [7406, 8836], [1200, 1321], seedPotency: 150, dotTickPotency: 20),
            Skill("狂风蚀箭", [7407, 8837], [1201, 1322], seedPotency: 100, dotTickPotency: 25)),
        Job("机工士",
            Skill("毒菌冲击", [16499, 29406], [1866, 2019])),
        Job("舞者"),

        Job("黑魔法师",
            Skill("闪雷", [144, 7447], [161, 162]),
            Skill("暴雷", [153], [163]),
            Skill("霹雷", [7420], [1210], seedPotency: 120, dotTickPotency: 50),
            Skill("高闪雷", [36986], [3871], seedPotency: 150, dotTickPotency: 60),
            Skill("高震雷", [36987], [3872], seedPotency: 100, dotTickPotency: 40)),
        Job("召唤师",
            Skill("星极超流（Slipstream）", [16523, 25837, 29669], [2706, 3225], seedPotency: 520, dotTickPotency: 30),
            Skill("赤焰（Scarlet Flame）", [16519, 29681], [3231])),
        Job("赤魔法师"),
        Job("绘灵法师"),
    ];

    private static readonly Dictionary<uint, PlayerDotSkillEntry> SkillsByActionId = Entries
        .SelectMany(static entry => entry.Skills)
        .SelectMany(static skill => skill.ActionIds.Select(actionId => new KeyValuePair<uint, PlayerDotSkillEntry>(actionId, skill)))
        .GroupBy(static pair => pair.Key)
        .ToDictionary(static group => group.Key, static group => group.First().Value);

    private static readonly Dictionary<uint, PlayerDotSkillEntry> SkillsByStatusId = Entries
        .SelectMany(static entry => entry.Skills)
        .SelectMany(static skill => skill.StatusIds.Select(statusId => new KeyValuePair<uint, PlayerDotSkillEntry>(statusId, skill)))
        .GroupBy(static pair => pair.Key)
        .ToDictionary(static group => group.Key, static group => group.First().Value);

    private static readonly HashSet<uint> KnownActionIds = Entries
        .SelectMany(static entry => entry.Skills)
        .SelectMany(static skill => skill.ActionIds)
        .ToHashSet();

    private static readonly HashSet<uint> KnownStatusIds = Entries
        .SelectMany(static entry => entry.Skills)
        .SelectMany(static skill => skill.StatusIds)
        .ToHashSet();

    public static bool IsKnownPlayerDotAction(uint actionId)
        => actionId != 0 && KnownActionIds.Contains(actionId);

    public static bool IsKnownPlayerDotStatus(uint statusId)
        => statusId != 0 && KnownStatusIds.Contains(statusId);

    public static PlayerDotSkillEntry? GetSkillByActionId(uint actionId)
        => actionId != 0 && SkillsByActionId.TryGetValue(actionId, out var skill) ? skill : null;

    public static PlayerDotSkillEntry? GetSkillByStatusId(uint statusId)
        => statusId != 0 && SkillsByStatusId.TryGetValue(statusId, out var skill) ? skill : null;

    public static PlayerDotSkillEntry? ResolveSkill(uint actionId, uint statusId)
        => GetSkillByActionId(actionId) ?? GetSkillByStatusId(statusId);

    private static PlayerDotJobEntry Job(string jobName, params PlayerDotSkillEntry[] skills)
        => new(jobName, skills);

    private static PlayerDotSkillEntry Skill(
        string skillName,
        IReadOnlyCollection<uint> actionIds,
        IReadOnlyCollection<uint> statusIds,
        int? seedPotency = null,
        int? dotTickPotency = null,
        IReadOnlyList<PlayerDotAnchorEntry>? anchors = null)
        => new(skillName, actionIds, statusIds, seedPotency, dotTickPotency, anchors ?? []);

    private static PlayerDotAnchorEntry Anchor(string anchorName, IReadOnlyCollection<uint> actionIds, int potency)
        => new(anchorName, actionIds, potency);
}

internal sealed record PlayerDotJobEntry(string JobName, IReadOnlyList<PlayerDotSkillEntry> Skills);

internal sealed record PlayerDotSkillEntry(
    string SkillName,
    IReadOnlyCollection<uint> ActionIds,
    IReadOnlyCollection<uint> StatusIds,
    int? SeedPotency,
    int? DotTickPotency,
    IReadOnlyList<PlayerDotAnchorEntry> Anchors)
{
    public bool TryGetPotencyRatio(out double ratio)
    {
        if (!SeedPotency.HasValue || !DotTickPotency.HasValue || SeedPotency.Value <= 0 || DotTickPotency.Value <= 0)
        {
            ratio = 0d;
            return false;
        }

        ratio = DotTickPotency.Value / (double)SeedPotency.Value;
        return ratio > 0d;
    }

    public uint GetPreferredActionId(uint observedActionId)
    {
        if (observedActionId != 0 && ActionIds.Contains(observedActionId))
            return observedActionId;

        return ActionIds.FirstOrDefault();
    }
}

internal sealed record PlayerDotAnchorEntry(
    string AnchorName,
    IReadOnlyCollection<uint> ActionIds,
    int Potency);
