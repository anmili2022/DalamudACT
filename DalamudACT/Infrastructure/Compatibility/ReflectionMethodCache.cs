using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace DalamudACT;

internal static class ReflectionMethodCache
{
    private static readonly ConcurrentDictionary<(Type Type, string MethodNames, Type[] ParameterTypes), MethodInfo?> Cache = new(new MethodKeyComparer());

    public static MethodInfo? GetMethod(Type type, string methodName, params Type[] parameterTypes)
        => GetMethod(type, [methodName], parameterTypes);

    public static MethodInfo? GetMethod(Type type, string[] methodNames, params Type[] parameterTypes)
        => Cache.GetOrAdd((type, string.Join("|", methodNames), parameterTypes), static key =>
        {
            var names = key.MethodNames.Split('|');
            return key.Type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(method => names.Contains(method.Name, StringComparer.Ordinal)
                                          && HasParameters(method, key.ParameterTypes));
        });

    private static bool HasParameters(MethodInfo method, Type[] parameterTypes)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != parameterTypes.Length)
            return false;

        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType != parameterTypes[i])
                return false;
        }

        return true;
    }

    private sealed class MethodKeyComparer : IEqualityComparer<(Type Type, string MethodNames, Type[] ParameterTypes)>
    {
        public bool Equals((Type Type, string MethodNames, Type[] ParameterTypes) x, (Type Type, string MethodNames, Type[] ParameterTypes) y)
            => x.Type == y.Type
               && string.Equals(x.MethodNames, y.MethodNames, StringComparison.Ordinal)
               && x.ParameterTypes.SequenceEqual(y.ParameterTypes);

        public int GetHashCode((Type Type, string MethodNames, Type[] ParameterTypes) obj)
        {
            var hash = HashCode.Combine(obj.Type, obj.MethodNames);
            foreach (var parameterType in obj.ParameterTypes)
                hash = HashCode.Combine(hash, parameterType);
            return hash;
        }
    }
}
