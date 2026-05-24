using System;
using System.Collections.Generic;
using System.Linq;

namespace DalamudACT;

/// <summary>
/// 项目统一日志与聊天框输出入口。
/// 轻量封装 Dalamud 的 <c>IPluginLog</c> / <c>IChatGui</c>，方便后续统一调整日志级别、调试开关和用户提示。
/// </summary>
internal static class LogHelper
{
    private const string ChatTag = "DPS统计";
    private const int MaxRecentLogCount = 10;
    private static readonly object Gate = new();
    private static readonly Queue<RecentLogEntry> RecentLogQueue = new();

    public static bool DefaultEnableDebugLog
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    public static bool EnableDebugLog { get; set; } = DefaultEnableDebugLog;

    public static IReadOnlyList<RecentLogEntry> RecentLogs
    {
        get
        {
            lock (Gate)
                return RecentLogQueue.ToArray();
        }
    }

    public static IReadOnlyList<string> RecentInfos
        => RecentLogs.Select(static entry => entry.Message).ToArray();

    public static void ClearRecentLogs()
    {
        lock (Gate)
            RecentLogQueue.Clear();
    }

    public static void Verbose(string message)
    {
        if (!EnableDebugLog)
            return;

        DalamudApi.Log.Verbose(message);
    }

    public static void Verbose(string module, string message)
        => Verbose(FormatMessage(module, message));

    public static void Verbose(Exception exception, string message)
    {
        if (!EnableDebugLog)
            return;

        DalamudApi.Log.Verbose(exception, message);
    }

    public static void Verbose(string module, Exception exception, string message)
        => Verbose(exception, FormatMessage(module, message));

    public static void Debug(string message)
    {
        if (!EnableDebugLog)
            return;

        DalamudApi.Log.Debug(message);
    }

    public static void Debug(string module, string message)
        => Debug(FormatMessage(module, message));

    public static void DebugRecent(string message)
    {
        if (!EnableDebugLog)
            return;

        EnqueueRecentLog(LogLevelLabel.Debug, message);
        DalamudApi.Log.Debug(message);
    }

    public static void DebugRecent(string module, string message)
        => DebugRecent(FormatMessage(module, message));

    public static void Debug(Exception exception, string message)
    {
        if (!EnableDebugLog)
            return;

        DalamudApi.Log.Debug(exception, message);
    }

    public static void Debug(string module, Exception exception, string message)
        => Debug(exception, FormatMessage(module, message));

    public static void Info(string message)
    {
        EnqueueRecentLog(LogLevelLabel.Info, message);
        DalamudApi.Log.Info(message);
    }

    public static void Info(string module, string message)
        => Info(FormatMessage(module, message));

    public static void Info(Exception exception, string message)
    {
        EnqueueRecentLog(LogLevelLabel.Info, message);
        DalamudApi.Log.Info(exception, message);
    }

    public static void Info(string module, Exception exception, string message)
        => Info(exception, FormatMessage(module, message));

    public static void Warning(string message)
    {
        EnqueueRecentLog(LogLevelLabel.Warning, message);
        DalamudApi.Log.Warning(message);
    }

    public static void Warning(string module, string message)
        => Warning(FormatMessage(module, message));

    public static void Warning(Exception exception, string message)
    {
        EnqueueRecentLog(LogLevelLabel.Warning, message);
        DalamudApi.Log.Warning(exception, message);
    }

    public static void Warning(string module, Exception exception, string message)
        => Warning(exception, FormatMessage(module, message));

    public static void Error(string message)
    {
        EnqueueRecentLog(LogLevelLabel.Error, message);
        DalamudApi.Log.Error(message);
    }

    public static void Error(string module, string message)
        => Error(FormatMessage(module, message));

    public static void Error(Exception exception, string message)
    {
        EnqueueRecentLog(LogLevelLabel.Error, message);
        DalamudApi.Log.Error(exception, message);
    }

    public static void Error(string module, Exception exception, string message)
        => Error(exception, FormatMessage(module, message));

    public static void Print(string message)
        => PrintCore(module: null, title: null, message, isError: false);

    public static void PrintWithModule(string module, string message)
        => PrintCore(module, title: null, message, isError: false);

    public static void Print(string title, string message)
        => PrintCore(module: null, title, message, isError: false);

    public static void PrintWithModule(string module, string title, string message)
        => PrintCore(module, title, message, isError: false);

    public static void PrintError(string message)
        => PrintCore(module: null, title: null, message, isError: true);

    public static void PrintErrorWithModule(string module, string message)
        => PrintCore(module, title: null, message, isError: true);

    public static void PrintError(string title, string message)
        => PrintCore(module: null, title, message, isError: true);

    public static void PrintErrorWithModule(string module, string title, string message)
        => PrintCore(module, title, message, isError: true);

    private static void EnqueueRecentLog(string level, string message)
    {
        lock (Gate)
        {
            RecentLogQueue.Enqueue(new RecentLogEntry(DateTime.Now, level, message));
            while (RecentLogQueue.Count > MaxRecentLogCount)
                RecentLogQueue.Dequeue();
        }
    }

    private static void PrintCore(string? module, string? title, string message, bool isError)
    {
        var normalizedTitle = NormalizeTitle(title);
        var mergedMessage = string.IsNullOrWhiteSpace(normalizedTitle)
            ? message
            : $"{normalizedTitle}{message}";

        if (isError)
        {
            if (string.IsNullOrWhiteSpace(module))
                Error(mergedMessage);
            else
                Error(module!, mergedMessage);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(module))
                Info(mergedMessage);
            else
                Info(module!, mergedMessage);
        }

        TryPrintChat(message, isError, ComposeChatTitle(module, normalizedTitle));
    }

    private static void TryPrintChat(string message, bool isError, string? title)
    {
        try
        {
            var tag = string.IsNullOrWhiteSpace(title)
                ? ChatTag
                : $"{ChatTag} · {title!.Trim()}";

            if (isError)
                DalamudApi.ChatGui.PrintError(message, tag);
            else
                DalamudApi.ChatGui.Print(message, tag);
        }
        catch (Exception ex)
        {
            DalamudApi.Log.Warning(ex, FormatMessage("日志", $"向聊天框输出插件消息失败。消息内容：{message}"));
        }
    }

    private static string FormatMessage(string module, string message)
    {
        if (string.IsNullOrWhiteSpace(module))
            return message;

        return $"[{module.Trim()}] {message}";
    }

    private static string? ComposeChatTitle(string? module, string? title)
    {
        var normalizedModule = string.IsNullOrWhiteSpace(module)
            ? null
            : $"[{module.Trim()}]";

        if (string.IsNullOrWhiteSpace(normalizedModule))
            return title;

        if (string.IsNullOrWhiteSpace(title))
            return normalizedModule;

        return $"{normalizedModule} {title}";
    }

    private static string? NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var trimmed = title.Trim();
        return trimmed.EndsWith("：", StringComparison.Ordinal)
               || trimmed.EndsWith(":", StringComparison.Ordinal)
            ? trimmed
            : $"{trimmed}：";
    }

    public sealed record RecentLogEntry(DateTime TimestampLocal, string Level, string Message);

    public static class LogLevelLabel
    {
        public const string Info = "信息";
        public const string Warning = "警告";
        public const string Error = "错误";
        public const string Debug = "调试";
    }
}
