using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace DalamudACT;

internal sealed record JobThemePaletteEntry(string JobName, string Category, Vector4 DefaultColor);

internal static class JobThemePalette
{
    private const float PreviousIkegamiBarAlpha = 0.80f;
    private const float IkegamiBarAlpha = 0.75f;

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

    private static readonly IReadOnlyDictionary<string, Vector4> IkegamiOpaqueDefaults = new Dictionary<string, Vector4>
    {
        ["剑术师"] = Rgb(21, 28, 100, 1f),
        ["骑士"] = Rgb(21, 28, 100, 1f),
        ["斧术师"] = Rgb(153, 23, 23, 1f),
        ["战士"] = Rgb(153, 23, 23, 1f),
        ["暗黑骑士"] = Rgb(136, 14, 79, 1f),
        ["绝枪战士"] = Rgb(78, 52, 46, 1f),

        ["幻术师"] = Rgb(216, 221, 230, 1f),
        ["白魔法师"] = Rgb(216, 221, 230, 1f),
        ["占星术士"] = Rgb(183, 110, 121, 1f),
        ["学者"] = Rgb(102, 170, 150, 1f),
        ["贤者"] = Rgb(127, 168, 232, 1f),

        ["格斗家"] = Rgb(245, 124, 0, 1f),
        ["武僧"] = Rgb(245, 124, 0, 1f),
        ["枪术师"] = Rgb(63, 81, 181, 1f),
        ["龙骑士"] = Rgb(63, 81, 181, 1f),
        ["双剑师"] = Rgb(211, 47, 47, 1f),
        ["忍者"] = Rgb(211, 47, 47, 1f),
        ["武士"] = Rgb(255, 202, 40, 1f),
        ["钐镰客"] = Rgb(255, 160, 0, 1f),
        ["蝰蛇剑士"] = Rgb(216, 116, 21, 1f),

        ["弓箭手"] = Rgb(158, 157, 36, 1f),
        ["吟游诗人"] = Rgb(158, 157, 36, 1f),
        ["机工士"] = Rgb(0, 151, 167, 1f),
        ["舞者"] = Rgb(244, 143, 177, 1f),

        ["秘术师"] = Rgb(46, 125, 50, 1f),
        ["咒术师"] = Rgb(126, 87, 194, 1f),
        ["黑魔法师"] = Rgb(126, 87, 194, 1f),
        ["召唤师"] = Rgb(46, 125, 50, 1f),
        ["赤魔法师"] = Rgb(233, 30, 99, 1f),
        ["绘灵法师"] = Rgb(95, 153, 51, 1f),
        ["青魔法师"] = Rgb(0, 185, 247, 1f),
    };

    private static readonly IReadOnlyDictionary<string, Vector4> SkylineDefaults = new Dictionary<string, Vector4>
    {
        ["剑术师"] = Rgb(168, 210, 230, 0.4f),
        ["骑士"] = Rgb(168, 210, 230, 0.4f),
        ["斧术师"] = Rgb(207, 38, 33, 0.4f),
        ["战士"] = Rgb(207, 38, 33, 0.4f),
        ["暗黑骑士"] = Rgb(209, 38, 204, 0.4f),
        ["绝枪战士"] = Rgb(121, 109, 48, 0.4f),

        ["幻术师"] = Rgb(255, 240, 220, 0.4f),
        ["白魔法师"] = Rgb(255, 240, 220, 0.4f),
        ["占星术士"] = Rgb(255, 231, 74, 0.4f),
        ["学者"] = Rgb(134, 87, 255, 0.4f),
        ["贤者"] = Rgb(128, 160, 240, 0.4f),

        ["格斗家"] = Rgb(214, 156, 0, 0.4f),
        ["武僧"] = Rgb(214, 156, 0, 0.4f),
        ["枪术师"] = Rgb(65, 100, 205, 0.4f),
        ["龙骑士"] = Rgb(65, 100, 205, 0.4f),
        ["双剑师"] = Rgb(175, 25, 100, 0.4f),
        ["忍者"] = Rgb(175, 25, 100, 0.4f),
        ["武士"] = Rgb(228, 109, 4, 0.4f),
        ["钐镰客"] = Rgb(150, 90, 144, 0.4f),
        ["蝰蛇剑士"] = Rgb(216, 67, 21, 0.4f),

        ["弓箭手"] = Rgb(145, 186, 94, 0.4f),
        ["吟游诗人"] = Rgb(145, 186, 94, 0.4f),
        ["机工士"] = Rgb(110, 225, 214, 0.4f),
        ["舞者"] = Rgb(226, 176, 175, 0.4f),

        ["秘术师"] = Rgb(45, 155, 120, 0.4f),
        ["咒术师"] = Rgb(165, 121, 214, 0.4f),
        ["黑魔法师"] = Rgb(165, 121, 214, 0.4f),
        ["召唤师"] = Rgb(45, 155, 120, 0.4f),
        ["赤魔法师"] = Rgb(232, 123, 123, 0.4f),
        ["绘灵法师"] = Rgb(139, 202, 23, 0.4f),
        ["青魔法师"] = Rgb(0, 185, 247, 0.4f),
    };

    public static readonly IReadOnlyList<JobThemePaletteEntry> Entries =
    [
        new("剑术师", "坦克", Rgb(21, 28, 100, IkegamiBarAlpha)),
        new("骑士", "坦克", Rgb(21, 28, 100, IkegamiBarAlpha)),
        new("斧术师", "坦克", Rgb(153, 23, 23, IkegamiBarAlpha)),
        new("战士", "坦克", Rgb(153, 23, 23, IkegamiBarAlpha)),
        new("暗黑骑士", "坦克", Rgb(136, 14, 79, IkegamiBarAlpha)),
        new("绝枪战士", "坦克", Rgb(78, 52, 46, IkegamiBarAlpha)),

        new("幻术师", "治疗", Rgb(216, 221, 230, IkegamiBarAlpha)),
        new("白魔法师", "治疗", Rgb(216, 221, 230, IkegamiBarAlpha)),
        new("占星术士", "治疗", Rgb(183, 110, 121, IkegamiBarAlpha)),
        new("学者", "治疗", Rgb(102, 170, 150, IkegamiBarAlpha)),
        new("贤者", "治疗", Rgb(127, 168, 232, IkegamiBarAlpha)),

        new("格斗家", "近战", Rgb(245, 124, 0, IkegamiBarAlpha)),
        new("武僧", "近战", Rgb(245, 124, 0, IkegamiBarAlpha)),
        new("枪术师", "近战", Rgb(63, 81, 181, IkegamiBarAlpha)),
        new("龙骑士", "近战", Rgb(63, 81, 181, IkegamiBarAlpha)),
        new("双剑师", "近战", Rgb(211, 47, 47, IkegamiBarAlpha)),
        new("忍者", "近战", Rgb(211, 47, 47, IkegamiBarAlpha)),
        new("武士", "近战", Rgb(255, 202, 40, IkegamiBarAlpha)),
        new("钐镰客", "近战", Rgb(255, 160, 0, IkegamiBarAlpha)),
        new("蝰蛇剑士", "近战", Rgb(216, 116, 21, IkegamiBarAlpha)),

        new("弓箭手", "远敏", Rgb(158, 157, 36, IkegamiBarAlpha)),
        new("吟游诗人", "远敏", Rgb(158, 157, 36, IkegamiBarAlpha)),
        new("机工士", "远敏", Rgb(0, 151, 167, IkegamiBarAlpha)),
        new("舞者", "远敏", Rgb(244, 143, 177, IkegamiBarAlpha)),

        new("秘术师", "法系", Rgb(46, 125, 50, IkegamiBarAlpha)),
        new("咒术师", "法系", Rgb(126, 87, 194, IkegamiBarAlpha)),
        new("黑魔法师", "法系", Rgb(126, 87, 194, IkegamiBarAlpha)),
        new("召唤师", "法系", Rgb(46, 125, 50, IkegamiBarAlpha)),
        new("赤魔法师", "法系", Rgb(233, 30, 99, IkegamiBarAlpha)),
        new("绘灵法师", "法系", Rgb(95, 153, 51, IkegamiBarAlpha)),
        new("青魔法师", "法系", Rgb(0, 185, 247, IkegamiBarAlpha)),
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

    public static bool TryGetSkylineDefaultColor(string? jobName, out Vector4 color)
    {
        if (!string.IsNullOrWhiteSpace(jobName) && SkylineDefaults.TryGetValue(jobName, out color))
            return true;

        color = default;
        return false;
    }

    public static bool TryGetIkegamiOpaqueDefaultColor(string? jobName, out Vector4 color)
    {
        if (!string.IsNullOrWhiteSpace(jobName) && IkegamiOpaqueDefaults.TryGetValue(jobName, out color))
            return true;

        color = default;
        return false;
    }

    public static bool TryGetPreviousIkegamiSoftDefaultColor(string? jobName, out Vector4 color)
    {
        if (TryGetIkegamiOpaqueDefaultColor(jobName, out var opaque))
        {
            color = WithAlpha(opaque, PreviousIkegamiBarAlpha);
            return true;
        }

        color = default;
        return false;
    }

    public static bool TryGetPreviousTunedDefaultColor(string? jobName, out Vector4 color)
    {
        if (jobName == "占星术士")
        {
            color = Rgb(121, 85, 72, IkegamiBarAlpha);
            return true;
        }

        color = default;
        return false;
    }

    public static bool TryGetPreviousAstDefaultColor(string? jobName, out Vector4 color)
    {
        if (jobName == "占星术士")
        {
            color = Rgb(112, 89, 168, IkegamiBarAlpha);
            return true;
        }

        color = default;
        return false;
    }

    public static bool TryGetPreviousHealerDefaultColor(string? jobName, out Vector4 color)
    {
        switch (jobName)
        {
            case "幻术师":
            case "白魔法师":
                color = Rgb(117, 117, 117, IkegamiBarAlpha);
                return true;
            case "占星术士":
                color = Rgb(214, 166, 58, IkegamiBarAlpha);
                return true;
            case "学者":
                color = Rgb(121, 134, 203, IkegamiBarAlpha);
                return true;
            case "贤者":
                color = Rgb(79, 195, 247, IkegamiBarAlpha);
                return true;
            default:
                color = default;
                return false;
        }
    }

    private static Vector4 WithAlpha(Vector4 color, float alpha)
        => new(color.X, color.Y, color.Z, alpha);

    private static Vector4 Rgb(byte r, byte g, byte b, float alpha = 0.92f)
        => new(r / 255f, g / 255f, b / 255f, alpha);
}
