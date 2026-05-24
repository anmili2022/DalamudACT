using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

// 状态反射模块：负责兼容读取 StatusList、状态 ID、来源、参数、剩余时间和状态表文本。
internal sealed partial class LocalStatsService
{
    private static uint ResolveStatusSourceActorId(object status)
    {
        var sourceId = TryConvertActorId(GetReflectedStatusValue(status, "SourceId"));
        if (sourceId is > 0 and not InvalidActorId)
            return sourceId;

        var sourceObject = GetReflectedStatusValue(status, "SourceObject") as IGameObject;
        return sourceObject == null
            ? 0
            : GetGameObjectIdentity(sourceObject).ResolveActorId();
    }

    private static string? TryGetStatusGameDataText(object status, string propertyName)
    {
        try
        {
            var gameData = GetReflectedStatusValue(status, "GameData");
            if (gameData == null)
                return null;

            var row = gameData.GetType().GetProperty("Value")?.GetValue(gameData);
            if (row == null)
                return null;

            var value = row.GetType().GetProperty(propertyName)?.GetValue(row);
            return TryExtractGameDataText(value);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryExtractGameDataText(object? value)
    {
        if (value == null)
            return null;

        if (value is string text)
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();

        try
        {
            var extractTextMethod = value.GetType().GetMethod("ExtractText", Type.EmptyTypes);
            if (extractTextMethod?.Invoke(value, null) is string extracted && !string.IsNullOrWhiteSpace(extracted))
                return extracted.Trim();
        }
        catch
        {
        }

        try
        {
            if (value.GetType().GetProperty("TextValue")?.GetValue(value) is string textValue && !string.IsNullOrWhiteSpace(textValue))
                return textValue.Trim();
        }
        catch
        {
        }

        var fallback = value.ToString();
        return string.IsNullOrWhiteSpace(fallback) ? null : fallback.Trim();
    }

    private static int TryGetStatusGameDataInt(object status, string propertyName)
    {
        try
        {
            var gameData = GetReflectedStatusValue(status, "GameData");
            if (gameData == null)
                return 0;

            var row = gameData.GetType().GetProperty("Value")?.GetValue(gameData);
            if (row == null)
                return 0;

            var value = row.GetType().GetProperty(propertyName)?.GetValue(row);
            if (value == null)
                return 0;

            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static uint GetStatusId(object status)
        => TryConvertActorId(GetReflectedStatusValue(status, "StatusId"));

    private static int TryGetStatusParam(object status)
    {
        try
        {
            var rawValue = GetReflectedStatusValue(status, "Param");
            return rawValue == null ? 0 : Convert.ToInt32(rawValue);
        }
        catch
        {
            return 0;
        }
    }

    private static float GetStatusRemainingTime(object status)
    {
        try
        {
            var remainingTime = GetReflectedStatusValue(status, "RemainingTime");
            return remainingTime == null
                ? 0f
                : Convert.ToSingle(remainingTime, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0f;
        }
    }

    private static object? GetReflectedStatusValue(object status, string propertyName)
    {
        try
        {
            return status.GetType().GetProperty(propertyName)?.GetValue(status);
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<object> EnumerateStatusEntries(object statusOwner)
    {
        var entries = new List<object>();
        if (statusOwner == null)
            return entries;

        object? statusList = null;
        try
        {
            statusList = statusOwner.GetType().GetProperty("StatusList")?.GetValue(statusOwner);
        }
        catch
        {
            return entries;
        }

        if (statusList == null)
            return entries;

        if (statusList is System.Collections.IEnumerable enumerable)
        {
            try
            {
                foreach (var entry in enumerable)
                {
                    if (entry != null)
                        entries.Add(entry);
                }
            }
            catch
            {
                // Fall through to reflection-based enumerator below.
            }

            if (entries.Count > 0)
                return entries;
        }

        try
        {
            var enumerator = statusList.GetType().GetMethod("GetEnumerator", Type.EmptyTypes)?.Invoke(statusList, null);
            if (enumerator == null)
                return entries;

            var moveNextMethod = enumerator.GetType().GetMethod("MoveNext", Type.EmptyTypes);
            var currentProperty = enumerator.GetType().GetProperty("Current");
            if (moveNextMethod == null || currentProperty == null)
                return entries;

            while (true)
            {
                var moved = moveNextMethod.Invoke(enumerator, null);
                if (moved is not bool hasNext || !hasNext)
                    break;

                var current = currentProperty.GetValue(enumerator);
                if (current != null)
                    entries.Add(current);
            }
        }
        catch
        {
            // Ignore incompatible runtime status enumerators and return what we already collected.
        }

        return entries;
    }
}
