using System;
using System.Collections;
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

        var statusList = GetPropertyValue(actor, "StatusList") as IEnumerable;
        if (statusList == null)
            return [];

        foreach (var status in statusList)
        {
            var statusId = TryGetUInt32(status, "StatusId", "StatusID", "RowId");
            if (statusId == 0)
                continue;

            var remaining = TryGetSingle(status, "RemainingTime");
            if (config.StatusObserver.HidePermanentStatuses && remaining <= 0f)
                continue;

            var sourceId = TryGetUInt32(status, "SourceId", "SourceID", "SourceObjectId", "SourceObjectID");
            var param = TryGetUInt16(status, "Param", "StackCount");
            var favorite = favorites.Contains(statusId);
            var statusInfo = ResolveStatusInfo(statusId);
            result.Add(new StatusObserverEntry(
                statusId,
                statusInfo.Name,
                statusInfo.IconId,
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

    private static object? GetPropertyValue(object target, params string[] names)
    {
        var type = target.GetType();
        foreach (var name in names)
        {
            var property = type.GetProperty(name);
            if (property == null)
                continue;

            try
            {
                return property.GetValue(target);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static uint TryGetUInt32(object target, params string[] names)
    {
        var value = GetPropertyValue(target, names);
        return value switch
        {
            uint uintValue => uintValue,
            int intValue when intValue >= 0 => (uint)intValue,
            ushort ushortValue => ushortValue,
            byte byteValue => byteValue,
            _ => 0,
        };
    }

    private static ushort TryGetUInt16(object target, params string[] names)
    {
        var value = GetPropertyValue(target, names);
        return value switch
        {
            ushort ushortValue => ushortValue,
            byte byteValue => byteValue,
            uint uintValue when uintValue <= ushort.MaxValue => (ushort)uintValue,
            int intValue when intValue is >= 0 and <= ushort.MaxValue => (ushort)intValue,
            _ => 0,
        };
    }

    private static float TryGetSingle(object target, params string[] names)
    {
        var value = GetPropertyValue(target, names);
        return value switch
        {
            float floatValue => floatValue,
            double doubleValue => (float)doubleValue,
            int intValue => intValue,
            uint uintValue => uintValue,
            _ => 0f,
        };
    }
}
