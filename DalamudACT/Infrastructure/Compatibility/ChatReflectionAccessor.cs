using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using Dalamud.Game.Text.SeStringHandling;
using ECommons;

namespace DalamudACT;

internal static class ChatReflectionAccessor
{
    private static readonly ConcurrentDictionary<(Type Type, string EventName), EventInfo?> EventCache = new();

    public static EventInfo? GetEvent(object? instance, string eventName)
    {
        if (instance == null)
            return null;

        return EventCache.GetOrAdd((instance.GetType(), eventName), static key => key.Type.GetEvent(key.EventName));
    }

    public static void AddEventHandler(object instance, string eventName, Delegate handler)
        => GetEvent(instance, eventName)?.AddEventHandler(instance, handler);

    public static void RemoveEventHandler(object instance, string eventName, Delegate handler)
        => GetEvent(instance, eventName)?.RemoveEventHandler(instance, handler);

    public static string DescribeDelegate(Type? handlerType)
    {
        var invoke = handlerType?.GetMethod("Invoke");
        var parameters = invoke?.GetParameters();
        if (handlerType == null || invoke == null || parameters == null)
            return "未找到";

        return $"{handlerType.FullName}({string.Join(", ", parameters.Select(parameter => parameter.ParameterType.FullName ?? parameter.ParameterType.Name))})";
    }

    public static string GetLogKind(object message)
        => ReflectionPropertyCache.GetProperty(message.GetType(), "LogKind")?.GetValue(message)?.ToString() ?? "unknown";

    public static string ExtractLogMessageText(object message)
    {
        foreach (var propertyName in new[] { "Message", "OriginalMessage" })
        {
            try
            {
                var value = ReflectionPropertyCache.GetProperty(message.GetType(), propertyName)?.GetValue(message);
                if (value is SeString seString && !string.IsNullOrWhiteSpace(seString.TextValue))
                    return seString.TextValue;
                if (value != null)
                {
                    var textValue = ReflectionPropertyCache.GetProperty(value.GetType(), "TextValue")?.GetValue(value)?.ToString();
                    if (!string.IsNullOrWhiteSpace(textValue))
                        return textValue;

                    var text = value.ToString();
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
            }
            catch
            {
            }
        }

        return message.ToString() ?? string.Empty;
    }

    public static string ExtractChatMessageText(object message)
    {
        try
        {
            var value = ReflectionPropertyCache.GetProperty(message.GetType(), "Message")?.GetValue(message);
            return value == null ? string.Empty : ExtractSeStringText(value);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ExtractSeStringText(object value)
    {
        if (value is SeString seString)
        {
            var text = seString.GetText();
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        var textValue = ReflectionPropertyCache.GetProperty(value.GetType(), "TextValue")?.GetValue(value)?.ToString();
        if (!string.IsNullOrWhiteSpace(textValue))
            return textValue;

        return value.ToString() ?? string.Empty;
    }
}
