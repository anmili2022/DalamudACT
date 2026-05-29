using System;
using System.Collections.Concurrent;
using System.Reflection;

namespace DalamudACT;

internal static class ReflectionPropertyCache
{
    private static readonly ConcurrentDictionary<(Type Type, string PropertyName), PropertyInfo?> Cache = new();

    public static PropertyInfo? GetProperty(Type type, string propertyName)
        => Cache.GetOrAdd((type, propertyName), static key => key.Type.GetProperty(key.PropertyName));
}
