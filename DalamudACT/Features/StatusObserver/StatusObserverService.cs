using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

internal sealed class StatusObserverService
{
    private readonly PluginConfiguration config;

    public StatusObserverService(PluginConfiguration config)
    {
        this.config = config;
    }

    public IReadOnlyList<StatusObserverEntry> GetSelfStatuses()
    {
        var actor = DalamudApi.GetLocalPlayerBattleChara();
        return GetStatuses(actor, Math.Clamp(config.StatusObserver.SelfMaxStatuses, 1, 200));
    }

    public IReadOnlyList<StatusObserverEntry> GetTargetStatuses()
    {
        var target = DalamudApi.GetLocalPlayerBattleChara()?.TargetObject as IBattleChara;
        return GetStatuses(target, Math.Clamp(config.StatusObserver.TargetMaxStatuses, 1, 200));
    }

    private IReadOnlyList<StatusObserverEntry> GetStatuses(IBattleChara? actor, int maxCount)
    {
        if (actor == null)
            return [];

        var selfId = DalamudApi.GetLocalPlayerActorId();
        var favorites = config.StatusObserver.FavoriteStatusIds ?? [];
        List<StatusObserverEntry> result = [];

        foreach (var status in actor.StatusList)
        {
            var statusId = status.StatusId;
            if (statusId == 0)
                continue;

            var remaining = status.RemainingTime;
            if (config.StatusObserver.HidePermanentStatuses && remaining <= 0f)
                continue;

            var sourceId = status.SourceId;
            var param = status.Param;
            var favorite = favorites.Contains(statusId);
            result.Add(new StatusObserverEntry(
                statusId,
                ResolveStatusName(statusId),
                remaining,
                param,
                param,
                sourceId,
                sourceId != 0 && selfId != 0 && sourceId == selfId,
                favorite));
        }

        return result
            .OrderByDescending(static entry => entry.IsFavorite)
            .ThenBy(static entry => entry.RemainingSeconds <= 0f)
            .ThenBy(static entry => entry.RemainingSeconds)
            .Take(maxCount)
            .ToList();
    }

    private static string ResolveStatusName(uint statusId)
    {
        try
        {
            var sheet = DalamudApi.GameData.GetExcelSheet<Lumina.Excel.Sheets.Status>();
            if (sheet != null && sheet.TryGetRow(statusId, out var row))
            {
                var name = row.Name.ToString();
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
            }
        }
        catch (Exception ex)
        {
            LogHelper.Debug("状态观察", ex, $"读取状态名称失败：{statusId}");
        }

        return $"Status {statusId}";
    }
}
