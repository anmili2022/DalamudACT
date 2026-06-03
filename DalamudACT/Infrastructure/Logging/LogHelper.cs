using System;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace DalamudACT;

/// <summary>
/// 项目统一日志与聊天框输出入口。
/// 轻量封装 Dalamud 的 <c>IPluginLog</c> / <c>IChatGui</c>，方便后续统一调整日志级别、调试开关和用户提示。
/// </summary>
internal static class LogHelper
{
    private const string ChatTag = "DPS统计";

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
    public static DebugLogModule EnabledDebugLogModules { get; set; } = DefaultDebugLogModules;
    public static PluginLogChannel Channel { get; set; } = PluginLogChannel.Debug;

    public const DebugLogModule DefaultDebugLogModules =
        DebugLogModule.PluginHook | DebugLogModule.Timeline | DebugLogModule.StatusObserver | DebugLogModule.CommandChat | DebugLogModule.Configuration;

    public static bool IsDebugEnabled(DebugLogModule module)
        => EnableDebugLog && module != DebugLogModule.None && EnabledDebugLogModules.HasFlag(module);

    public static bool IsDebugEnabled(string module)
        => IsDebugEnabled(ResolveDebugLogModule(module));

    public static void Verbose(string message)
    {
        if (!EnableDebugLog)
            return;

        DalamudApi.Log.Verbose(message);
    }

    public static void Verbose(string module, string message)
    {
        if (!IsDebugEnabled(module))
            return;

        DalamudApi.Log.Verbose(FormatMessage(GetDebugLogModuleLabel(module), message));
    }

    public static void Verbose(Exception exception, string message)
    {
        if (!EnableDebugLog)
            return;

        DalamudApi.Log.Verbose(exception, message);
    }

    public static void Verbose(string module, Exception exception, string message)
    {
        if (!IsDebugEnabled(module))
            return;

        DalamudApi.Log.Verbose(exception, FormatMessage(GetDebugLogModuleLabel(module), message));
    }

    public static void Debug(string message)
    {
        if (!EnableDebugLog)
            return;

        DalamudApi.Log.Debug(message);
    }

    public static void Debug(string module, string message)
    {
        if (!IsDebugEnabled(module))
            return;

        DalamudApi.Log.Debug(FormatMessage(GetDebugLogModuleLabel(module), message));
    }

    public static void DebugRecent(string message)
    {
        if (!EnableDebugLog)
            return;

        DalamudApi.Log.Debug(message);
    }

    public static void DebugRecent(string module, string message)
    {
        if (!IsDebugEnabled(module))
            return;

        DalamudApi.Log.Debug(FormatMessage(GetDebugLogModuleLabel(module), message));
    }

    public static void Debug(Exception exception, string message)
    {
        if (!EnableDebugLog)
            return;

        DalamudApi.Log.Debug(exception, message);
    }

    public static void Debug(string module, Exception exception, string message)
    {
        if (!IsDebugEnabled(module))
            return;

        DalamudApi.Log.Debug(exception, FormatMessage(GetDebugLogModuleLabel(module), message));
    }

    public static string GetDebugLogModuleLabel(DebugLogModule module)
        => module switch
        {
            DebugLogModule.PluginHook => "插件/Hook",
            DebugLogModule.Timeline => "时间轴",
            DebugLogModule.DamageStats => "伤害统计",
            DebugLogModule.Dot => "DoT",
            DebugLogModule.StatusObserver => "状态监控",
            DebugLogModule.CommandChat => "命令/聊天",
            DebugLogModule.Configuration => "配置/设置",
            _ => module.ToString(),
        };

    private static string GetDebugLogModuleLabel(string module)
        => GetDebugLogModuleLabel(ResolveDebugLogModule(module));

    private static DebugLogModule ResolveDebugLogModule(string module)
    {
        var normalized = module.Trim();
        return normalized switch
        {
            "插件" or "Hook" or "插件/Hook" => DebugLogModule.PluginHook,
            "时间轴" => DebugLogModule.Timeline,
            "统计" or "伤害统计" => DebugLogModule.DamageStats,
            "DoT" or "DOT" or "dot" => DebugLogModule.Dot,
            "状态监控" or "状态观察" => DebugLogModule.StatusObserver,
            "命令" or "聊天" or "命令/聊天" => DebugLogModule.CommandChat,
            "设置" or "配置" or "配置/设置" => DebugLogModule.Configuration,
            _ => DebugLogModule.Configuration,
        };
    }

    public static void Info(string message)
    {
        DalamudApi.Log.Info(message);
    }

    public static void Info(string module, string message)
        => Info(FormatMessage(module, message));

    public static void Info(Exception exception, string message)
    {
        DalamudApi.Log.Info(exception, message);
    }

    public static void Info(string module, Exception exception, string message)
        => Info(exception, FormatMessage(module, message));

    public static void Warning(string message)
    {
        DalamudApi.Log.Warning(message);
    }

    public static void Warning(string module, string message)
        => Warning(FormatMessage(module, message));

    public static void Warning(Exception exception, string message)
    {
        DalamudApi.Log.Warning(exception, message);
    }

    public static void Warning(string module, Exception exception, string message)
        => Warning(exception, FormatMessage(module, message));

    public static void Error(string message)
    {
        DalamudApi.Log.Error(message);
    }

    public static void Error(string module, string message)
        => Error(FormatMessage(module, message));

    public static void Error(Exception exception, string message)
    {
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

        TryPrintChannel(message, isError, ComposeChatTitle(module, normalizedTitle));
    }

    private static void TryPrintChannel(string message, bool isError, string? title)
    {
        try
        {
            if (Channel == PluginLogChannel.None)
                return;

            var tag = string.IsNullOrWhiteSpace(title)
                ? ChatTag
                : $"{ChatTag} · {title!.Trim()}";

            if (Channel == PluginLogChannel.Info)
            {
                if (isError)
                    DalamudApi.Log.Error(FormatMessage(tag, message));
                else
                    DalamudApi.Log.Info(FormatMessage(tag, message));
                return;
            }

            DalamudApi.ChatGui.Print(new XivChatEntry
            {
                Type = ToXivChatType(Channel, isError),
                Message = new SeStringBuilder().AddText($"[{tag}] {message}").Build(),
            });
        }
        catch (Exception ex)
        {
            DalamudApi.Log.Warning(ex, FormatMessage("日志", $"输出插件消息失败。消息内容：{message}"));
        }
    }

    private static XivChatType ToXivChatType(PluginLogChannel channel, bool isError)
        => channel switch
        {
            PluginLogChannel.Debug => XivChatType.Debug,
            PluginLogChannel.Echo => XivChatType.Echo,
            PluginLogChannel.ErrorMessage => XivChatType.ErrorMessage,
            PluginLogChannel.SystemMessage => XivChatType.SystemMessage,
            _ => isError ? XivChatType.ErrorMessage : XivChatType.Debug,
        };

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

    public static class LogLevelLabel
    {
        public const string Info = "信息";
        public const string Warning = "警告";
        public const string Error = "错误";
        public const string Debug = "调试";
    }
}
