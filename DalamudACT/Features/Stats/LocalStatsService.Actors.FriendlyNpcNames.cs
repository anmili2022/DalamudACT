using System.Collections.Generic;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
    private static readonly string[] BuiltInFriendlyNpcNameArray =
    {
        "阿尔菲诺",
        "阿莉塞",
        "雅修特拉",
        "桑克瑞德",
        "于里昂热",
        "古拉哈提亚",
        "埃斯蒂尼安",
        "乌克拉玛特",
        "可露儿",
        "克鲁鲁",
        "敏菲利亚",
        "琳",
        "莉瑟",
        "水晶公",
        "零",
        "瓦尔桑",
        "卡尔瓦兰",
        "爱梅特赛尔克",
        "希斯拉德",
        "维涅斯",
    };

    internal static IReadOnlyList<string> BuiltInFriendlyNpcNames => BuiltInFriendlyNpcNameArray;
}
