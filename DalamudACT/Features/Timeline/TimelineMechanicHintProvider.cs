using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace DalamudACT;

internal sealed partial class TimelineMechanicHintProvider
{
    private readonly Dictionary<uint, string> hintsByActionId = new();
    private bool loaded;

    public string? GetHint(TimelineEntry entry)
    {
        EnsureLoaded();
        foreach (var actionId in entry.ActionIds)
        {
            if (hintsByActionId.TryGetValue(actionId, out var hint))
                return hint;
        }

        return null;
    }

    private void EnsureLoaded()
    {
        if (loaded)
            return;

        loaded = true;
        foreach (var path in GetBundleCandidates())
        {
            try
            {
                if (!File.Exists(path))
                    continue;

                ParseBundle(File.ReadAllText(path));
                LogHelper.Info("时间轴", $"已从 cactbot 静态提取 {hintsByActionId.Count} 个机制类型映射。 ");
                return;
            }
            catch (Exception ex)
            {
                LogHelper.Debug("时间轴", ex, $"解析 cactbot 机制类型映射失败：{path}");
            }
        }
    }

    private void ParseBundle(string bundle)
    {
        foreach (Match trigger in TriggerBlockRegex().Matches(bundle))
        {
            var block = trigger.Value;
            var responseMatch = ResponseRegex().Match(block);
            if (!responseMatch.Success)
                continue;

            var hint = MapResponseToHint(responseMatch.Groups["response"].Value);
            if (hint == null)
                continue;

            foreach (Match idMatch in IdRegex().Matches(block))
            {
                if (uint.TryParse(idMatch.Groups["id"].Value, System.Globalization.NumberStyles.HexNumber, null, out var actionId))
                    hintsByActionId.TryAdd(actionId, hint);
            }
        }
    }

    private static string? MapResponseToHint(string response)
        => response switch
        {
            "aoe" or "bigAoe" or "bleedAoe" or "hpTo1Aoe" or "miniBuster" => "AOE",
            "tankBuster" or "tankBusterSwap" or "tankCleave" or "tankBusterCleaves" => "死刑",
            "spread" or "protean" or "spreadThenStack" => "分散",
            "stackMarker" or "stackMarkerOn" or "getTogether" or "stackThenSpread" or "stackMiddle" or "doritoStack" or "healerGroups" or "stackPartner" => "分摊",
            "getOut" or "outOfMelee" or "moveAway" => "远离",
            "getIn" or "getUnder" => "靠近",
            "lookAway" => "背对",
            "knockback" => "击退",
            "getTowers" or "stackInTower" => "踩塔",
            "stopMoving" or "stopEverything" => "停止",
            "moveAround" => "移动",
            _ => null,
        };

    private static IEnumerable<string> GetBundleCandidates()
    {
        yield return @"D:\ff14act\Plugins\ACT.OverlayPlugin\cactbot\ui\common\raidboss_data.bundle.js";
        yield return @"D:\ff14act\Plugins\ACT.OverlayPlugin\cactbot\ui\raidboss\raidboss.bundle.js";
    }

    [GeneratedRegex(@"\{\s*id:\s*['""].*?\n\s*type:\s*['""](?:StartsUsing|Ability)['""],[\s\S]*?response:\s*responses/\*\s*Responses\.(?<response>[A-Za-z0-9_]+)\s*\*/[\s\S]*?\n\s*\}", RegexOptions.Compiled)]
    private static partial Regex TriggerBlockRegex();

    [GeneratedRegex(@"Responses\.(?<response>[A-Za-z0-9_]+)", RegexOptions.Compiled)]
    private static partial Regex ResponseRegex();

    [GeneratedRegex(@"['""](?<id>[0-9A-Fa-f]{3,6})['""]", RegexOptions.Compiled)]
    private static partial Regex IdRegex();
}
