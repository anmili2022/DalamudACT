using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

internal sealed class StatusObserverService
{
    private readonly PluginConfiguration config;
    private IReadOnlyList<StatusObserverEntry> cachedSelfStatuses = [];
    private IReadOnlyList<StatusObserverEntry> cachedTargetStatuses = [];
    private DateTime lastSelfStatusRefreshUtc;
    private DateTime lastTargetStatusRefreshUtc;
    private readonly Dictionary<uint, (string Name, uint IconId)> statusInfoCache = new();
    public bool IsPausedOutOfCombat { get; private set; }

    public StatusObserverService(PluginConfiguration config)
    {
        this.config = config;
    }

    public IReadOnlyList<StatusObserverEntry> GetSelfStatuses()
    {
        if (!ShouldRefreshStatuses())
        {
            IsPausedOutOfCombat = config.StatusObserver.ShowWindow;
            return cachedSelfStatuses;
        }

        IsPausedOutOfCombat = false;

        var nowUtc = DateTime.UtcNow;
        if (nowUtc - lastSelfStatusRefreshUtc < RefreshInterval)
            return cachedSelfStatuses;

        var actor = DalamudApi.GetLocalPlayerBattleChara();
        cachedSelfStatuses = GetStatuses(actor, Math.Clamp(config.StatusObserver.SelfMaxStatuses, 1, 200));
        lastSelfStatusRefreshUtc = nowUtc;
        return cachedSelfStatuses;
    }

    public IReadOnlyList<StatusObserverEntry> GetTargetStatuses()
    {
        if (!ShouldRefreshStatuses())
        {
            IsPausedOutOfCombat = config.StatusObserver.ShowWindow;
            return cachedTargetStatuses;
        }

        IsPausedOutOfCombat = false;

        var nowUtc = DateTime.UtcNow;
        if (nowUtc - lastTargetStatusRefreshUtc < RefreshInterval)
            return cachedTargetStatuses;

        var target = DalamudApi.GetLocalPlayerBattleChara()?.TargetObject as IBattleChara;
        cachedTargetStatuses = GetStatuses(target, Math.Clamp(config.StatusObserver.TargetMaxStatuses, 1, 200));
        lastTargetStatusRefreshUtc = nowUtc;
        return cachedTargetStatuses;
    }

    public void RefreshOnce(bool refreshSelf, bool refreshTarget)
    {
        if (!config.StatusObserver.ShowWindow)
            return;

        if (refreshSelf)
        {
            var actor = DalamudApi.GetLocalPlayerBattleChara();
            cachedSelfStatuses = GetStatuses(actor, Math.Clamp(config.StatusObserver.SelfMaxStatuses, 1, 200));
            lastSelfStatusRefreshUtc = DateTime.UtcNow;
        }

        if (refreshTarget)
        {
            var target = DalamudApi.GetLocalPlayerBattleChara()?.TargetObject as IBattleChara;
            cachedTargetStatuses = GetStatuses(target, Math.Clamp(config.StatusObserver.TargetMaxStatuses, 1, 200));
            lastTargetStatusRefreshUtc = DateTime.UtcNow;
        }
    }

    private TimeSpan RefreshInterval => TimeSpan.FromMilliseconds(config.GetEffectiveStatusObserverUpdateIntervalMs());

    private bool ShouldRefreshStatuses()
        => config.StatusObserver.ShowWindow
           && DalamudApi.Conditions.Any(ConditionFlag.InCombat);

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

    private (string Name, uint IconId) ResolveStatusInfo(uint statusId)
    {
        if (statusInfoCache.TryGetValue(statusId, out var cached))
            return cached;

        try
        {
            var sheet = DalamudApi.GameData.GetExcelSheet<Lumina.Excel.Sheets.Status>();
            if (sheet != null && sheet.TryGetRow(statusId, out var row))
            {
                var name = row.Name.ToString();
                var iconId = row.Icon;
                if (!string.IsNullOrWhiteSpace(name))
                    return statusInfoCache[statusId] = (name, iconId);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Debug("状态监控", ex, $"读取状态名称失败：{statusId}");
        }

        return statusInfoCache[statusId] = ($"Status {statusId}", 0);
    }
}
