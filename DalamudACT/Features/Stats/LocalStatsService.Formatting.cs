using System;
using System.Globalization;

namespace DalamudACT;

// 通用格式化模块：负责区域名、技能名、职业名、伤害数字和战斗日志文本片段的统一格式化。
internal sealed partial class LocalStatsService
{
    private static string FormatActionNameWithId(string? actionName, uint actionId)
    {
        var normalizedActionName = NormalizeActionName(actionName);
        return actionId == 0
            ? normalizedActionName
            : $"{normalizedActionName}[{actionId}]";
    }

    private static string NormalizeZoneName(string? zoneName)
        => string.IsNullOrWhiteSpace(zoneName) ? "未知区域" : zoneName.Trim();

    private static string NormalizeActionName(string? actionName)
        => string.IsNullOrWhiteSpace(actionName) ? "未知技能" : actionName.Trim();

    private static string FormatCriticalSuffix(bool critical)
        => critical ? "（暴击）" : string.Empty;

    private static string FormatSimulatedCriticalSuffix(bool critical)
        => critical ? "（模拟，暴击）" : "（模拟）";

    private static string BuildUnknownActorName(uint actorId, string fallbackLabel)
        => actorId is 0 or InvalidActorId ? fallbackLabel : $"{fallbackLabel}(0x{actorId:X8})";

    private static string ResolveJobName(uint jobId)
    {
        return jobId switch
        {
            1 => "剑术师",
            2 => "格斗家",
            3 => "斧术师",
            4 => "枪术师",
            5 => "弓箭手",
            6 => "幻术师",
            7 => "咒术师",
            19 => "骑士",
            20 => "武僧",
            21 => "战士",
            22 => "龙骑士",
            23 => "吟游诗人",
            24 => "白魔法师",
            25 => "黑魔法师",
            26 => "秘术师",
            27 => "召唤师",
            28 => "学者",
            29 => "双剑师",
            30 => "忍者",
            31 => "机工士",
            32 => "暗黑骑士",
            33 => "占星术士",
            34 => "武士",
            35 => "赤魔法师",
            37 => "绝枪战士",
            38 => "舞者",
            39 => "钐镰客",
            40 => "贤者",
            41 => "蝰蛇剑士",
            42 => "绘灵法师",
            43 => "青魔法师",
            _ => string.Empty,
        };
    }

    private static string CreateDamageString(long damage, bool useSuffix, bool useDecimals)
    {
        const long trillion = 1_000_000_000_000L;
        const long hundredMillion = 100_000_000L;
        const long tenThousand = 10_000L;

        if (!useSuffix)
            return damage.ToString(CultureInfo.InvariantCulture);

        var abs = Math.Abs(damage);
        if (abs >= trillion)
            return FormatChineseDamageUnit(damage, trillion, "兆", useDecimals ? "0.00" : "0.#");

        if (abs >= hundredMillion)
            return FormatChineseDamageUnit(damage, hundredMillion, "亿", useDecimals ? "0.00" : "0.#");

        if (abs >= tenThousand)
            return FormatChineseDamageUnit(damage, tenThousand, "万", useDecimals ? "0.00" : "0.#");

        return damage.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatChineseDamageUnit(long value, long unitBase, string unit, string numericFormat)
        => (value / (double)unitBase).ToString(numericFormat, CultureInfo.InvariantCulture) + unit;
}
