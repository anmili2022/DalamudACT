using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DalamudACT;

internal sealed record JobThemePaletteEntry(string JobName, string Category, Vector4 DefaultColor);

internal static class JobThemePalette
{
    private static readonly IReadOnlyDictionary<string, Vector4> LegacyDefaults = new Dictionary<string, Vector4>
    {
        ["剑术师"] = Rgb(54, 89, 151),
        ["骑士"] = Rgb(54, 89, 151),
        ["斧术师"] = Rgb(163, 70, 61),
        ["战士"] = Rgb(163, 70, 61),
        ["暗黑骑士"] = Rgb(151, 81, 123),
        ["绝枪战士"] = Rgb(156, 149, 142),

        ["幻术师"] = Rgb(196, 190, 208),
        ["白魔法师"] = Rgb(196, 190, 208),
        ["占星术士"] = Rgb(103, 123, 188),
        ["秘术师"] = Rgb(145, 111, 83),
        ["学者"] = Rgb(145, 111, 83),
        ["贤者"] = Rgb(110, 201, 233),

        ["格斗家"] = Rgb(224, 142, 28),
        ["武僧"] = Rgb(224, 142, 28),
        ["枪术师"] = Rgb(63, 107, 189),
        ["龙骑士"] = Rgb(63, 107, 189),
        ["双剑师"] = Rgb(231, 147, 43),
        ["忍者"] = Rgb(231, 147, 43),
        ["武士"] = Rgb(220, 174, 46),
        ["钐镰客"] = Rgb(168, 43, 84),
        ["蝰蛇剑士"] = Rgb(211, 140, 49),

        ["弓箭手"] = Rgb(124, 141, 74),
        ["吟游诗人"] = Rgb(124, 141, 74),
        ["机工士"] = Rgb(45, 148, 166),
        ["舞者"] = Rgb(224, 147, 193),

        ["咒术师"] = Rgb(129, 92, 170),
        ["黑魔法师"] = Rgb(129, 92, 170),
        ["召唤师"] = Rgb(62, 155, 85),
        ["赤魔法师"] = Rgb(221, 62, 124),
        ["绘灵法师"] = Rgb(122, 183, 92),
        ["青魔法师"] = Rgb(67, 175, 222),
    };

    public static readonly IReadOnlyList<JobThemePaletteEntry> Entries =
    [
        new("剑术师", "坦克", Rgb(168, 210, 230, 0.4f)),
        new("骑士", "坦克", Rgb(168, 210, 230, 0.4f)),
        new("斧术师", "坦克", Rgb(207, 38, 33, 0.4f)),
        new("战士", "坦克", Rgb(207, 38, 33, 0.4f)),
        new("暗黑骑士", "坦克", Rgb(209, 38, 204, 0.4f)),
        new("绝枪战士", "坦克", Rgb(121, 109, 48, 0.4f)),

        new("幻术师", "治疗", Rgb(255, 240, 220, 0.4f)),
        new("白魔法师", "治疗", Rgb(255, 240, 220, 0.4f)),
        new("占星术士", "治疗", Rgb(255, 231, 74, 0.4f)),
        new("学者", "治疗", Rgb(134, 87, 255, 0.4f)),
        new("贤者", "治疗", Rgb(128, 160, 240, 0.4f)),

        new("格斗家", "近战", Rgb(214, 156, 0, 0.4f)),
        new("武僧", "近战", Rgb(214, 156, 0, 0.4f)),
        new("枪术师", "近战", Rgb(65, 100, 205, 0.4f)),
        new("龙骑士", "近战", Rgb(65, 100, 205, 0.4f)),
        new("双剑师", "近战", Rgb(175, 25, 100, 0.4f)),
        new("忍者", "近战", Rgb(175, 25, 100, 0.4f)),
        new("武士", "近战", Rgb(228, 109, 4, 0.4f)),
        new("钐镰客", "近战", Rgb(150, 90, 144, 0.4f)),
        new("蝰蛇剑士", "近战", Rgb(216, 67, 21, 0.4f)),

        new("弓箭手", "远敏", Rgb(145, 186, 94, 0.4f)),
        new("吟游诗人", "远敏", Rgb(145, 186, 94, 0.4f)),
        new("机工士", "远敏", Rgb(110, 225, 214, 0.4f)),
        new("舞者", "远敏", Rgb(226, 176, 175, 0.4f)),

        new("秘术师", "法系", Rgb(45, 155, 120, 0.4f)),
        new("咒术师", "法系", Rgb(165, 121, 214, 0.4f)),
        new("黑魔法师", "法系", Rgb(165, 121, 214, 0.4f)),
        new("召唤师", "法系", Rgb(45, 155, 120, 0.4f)),
        new("赤魔法师", "法系", Rgb(232, 123, 123, 0.4f)),
        new("绘灵法师", "法系", Rgb(139, 202, 23, 0.4f)),
        new("青魔法师", "法系", Rgb(0, 185, 247, 0.4f)),
    ];

    public static IEnumerable<IGrouping<string, JobThemePaletteEntry>> GroupedEntries
        => Entries.GroupBy(static entry => entry.Category);

    public static bool TryGetDefaultColor(string? jobName, out Vector4 color)
    {
        var entry = Entries.FirstOrDefault(entry => entry.JobName == jobName);
        if (entry != null)
        {
            color = entry.DefaultColor;
            return true;
        }

        color = default;
        return false;
    }

    public static bool TryGetLegacyDefaultColor(string? jobName, out Vector4 color)
    {
        if (!string.IsNullOrWhiteSpace(jobName) && LegacyDefaults.TryGetValue(jobName, out color))
            return true;

        color = default;
        return false;
    }

    private static Vector4 Rgb(byte r, byte g, byte b, float alpha = 0.92f)
        => new(r / 255f, g / 255f, b / 255f, alpha);
}
