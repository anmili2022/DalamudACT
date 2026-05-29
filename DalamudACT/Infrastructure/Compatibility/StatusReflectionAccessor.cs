using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace DalamudACT;

internal static class StatusReflectionAccessor
{
    public static IReadOnlyList<object> GetStatuses(object? statusOwner)
    {
        var statusList = GetValue(statusOwner, "StatusList") ?? GetValue(statusOwner, "Statuses");
        return Enumerate(statusList);
    }

    public static uint GetStatusId(object status)
        => GetUInt32(status, "StatusId", "StatusID", "Id", "RowId");

    public static float GetRemainingTime(object status)
        => GetSingle(status, "RemainingTime");

    public static uint GetSourceId(object status)
        => GetUInt32(status, "SourceId", "SourceID", "SourceObjectId", "SourceObjectID");

    public static uint GetActorId(object status)
        => GetUInt32(status, "ActorId", "ActorID");

    public static ushort GetParam(object status)
        => GetUInt16(status, "Param", "StackCount");

    public static ushort GetStackCount(object status)
        => GetUInt16(status, "StackCount", "Param");

    public static uint GetCategory(object status)
    {
        var gameData = GetGameDataValue(status);
        return gameData == null ? 0 : GetUInt32(gameData, "StatusCategory", "Category");
    }

    public static string GetName(object status, string fallback = "未知")
    {
        var gameData = GetGameDataValue(status);
        var name = gameData == null ? null : GetValue(gameData, "Name")?.ToString();
        return string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
    }

    public static string ReadPropertyText(object status, string propertyName)
        => GetValue(status, propertyName)?.ToString() ?? "-";

    public static uint GetUInt32(object? instance, params string[] propertyNames)
    {
        var value = GetValue(instance, propertyNames);
        return value switch
        {
            uint uintValue => uintValue,
            int intValue when intValue >= 0 => (uint)intValue,
            ushort ushortValue => ushortValue,
            byte byteValue => byteValue,
            ulong ulongValue => unchecked((uint)(ulongValue & uint.MaxValue)),
            long longValue when longValue >= 0 => unchecked((uint)((ulong)longValue & uint.MaxValue)),
            _ => TryConvertUInt32(value),
        };
    }

    public static ushort GetUInt16(object? instance, params string[] propertyNames)
    {
        var value = GetValue(instance, propertyNames);
        return value switch
        {
            ushort ushortValue => ushortValue,
            byte byteValue => byteValue,
            uint uintValue when uintValue <= ushort.MaxValue => (ushort)uintValue,
            int intValue when intValue is >= 0 and <= ushort.MaxValue => (ushort)intValue,
            _ => TryConvertUInt16(value),
        };
    }

    public static float GetSingle(object? instance, params string[] propertyNames)
    {
        var value = GetValue(instance, propertyNames);
        return value switch
        {
            float floatValue => floatValue,
            double doubleValue => (float)doubleValue,
            int intValue => intValue,
            uint uintValue => uintValue,
            _ => TryConvertSingle(value),
        };
    }

    private static object? GetGameDataValue(object status)
    {
        var gameDataRef = GetValue(status, "GameData");
        return GetValue(gameDataRef, "Value");
    }

    private static object? GetValue(object? instance, params string[] propertyNames)
    {
        if (instance == null)
            return null;

        var type = instance.GetType();
        foreach (var propertyName in propertyNames)
        {
            try
            {
                var property = ReflectionPropertyCache.GetProperty(type, propertyName);
                if (property == null)
                    continue;

                return property.GetValue(instance);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static IReadOnlyList<object> Enumerate(object? statusList)
    {
        var entries = new List<object>();
        if (statusList == null)
            return entries;

        if (statusList is IEnumerable enumerable)
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
                // Fall through to indexer-based enumeration below.
            }

            if (entries.Count > 0)
                return entries;
        }

        var length = GetUInt32(statusList, "Length", "Count");
        for (var i = 0; i < length; i++)
        {
            try
            {
                var entry = ReflectionPropertyCache.GetProperty(statusList.GetType(), "Item")?.GetValue(statusList, [i]);
                if (entry != null)
                    entries.Add(entry);
            }
            catch
            {
            }
        }

        return entries;
    }

    private static uint TryConvertUInt32(object? value)
    {
        try
        {
            return value == null ? 0 : Convert.ToUInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static ushort TryConvertUInt16(object? value)
    {
        try
        {
            return value == null ? (ushort)0 : Convert.ToUInt16(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static float TryConvertSingle(object? value)
    {
        try
        {
            return value == null ? 0f : Convert.ToSingle(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0f;
        }
    }
}
