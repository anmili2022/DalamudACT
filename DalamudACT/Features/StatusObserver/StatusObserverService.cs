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

        var statuses = StatusReflectionAccessor.GetStatuses(actor);
        if (statuses.Count == 0)
            return [];

        foreach (var status in statuses)
        {
            var statusId = StatusReflectionAccessor.GetStatusId(status);
            if (statusId == 0)
                continue;

            var remaining = StatusReflectionAccessor.GetRemainingTime(status);
            if (config.StatusObserver.HidePermanentStatuses && remaining <= 0f)
                continue;

            var sourceId = StatusReflectionAccessor.GetSourceId(status);
            var param = StatusReflectionAccessor.GetParam(status);
            var favorite = favorites.Contains(statusId);
            var statusInfo = ResolveStatusInfo(statusId);
            result.Add(new StatusObserverEntry(
                statusId,
                statusInfo.Name,
                statusInfo.IconId,
                remaining,
                param,
                StatusReflectionAccessor.GetStackCount(status),
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

    private static (string Name, uint IconId) ResolveStatusInfo(uint statusId)
    {
        try
        {
            var sheet = DalamudApi.GameData.GetExcelSheet<Lumina.Excel.Sheets.Status>();
            if (sheet != null && sheet.TryGetRow(statusId, out var row))
            {
                var name = row.Name.ToString();
                var iconId = row.Icon;
                if (!string.IsNullOrWhiteSpace(name))
                    return (name, iconId);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Debug("状态监控", ex, $"读取状态名称失败：{statusId}");
        }

        return ($"Status {statusId}", 0);
    }
}
