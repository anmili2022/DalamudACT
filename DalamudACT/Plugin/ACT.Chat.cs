using System;
using System.Reflection;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace DalamudACT;

public sealed partial class ACT
{
    private Delegate? chatMessageDelegate;
    private Delegate? logMessageDelegate;

    private void RegisterChatHandlers()
    {
        RegisterLogMessageHandler();

        try
        {
            var eventInfo = ChatReflectionAccessor.GetEvent(DalamudApi.ChatGui, "ChatMessage");
            var handlerType = eventInfo?.EventHandlerType;
            var methodInfo = ResolveChatMessageHandler(handlerType);
            LogHelper.Debug("时间轴", $"ChatMessage 事件签名：{ChatReflectionAccessor.DescribeDelegate(handlerType)}，处理器：{methodInfo?.Name ?? "无"}。 ");
            if (eventInfo == null || handlerType == null || methodInfo == null)
            {
                LogHelper.Warning("时间轴", "当前 Dalamud 运行时未暴露可用的聊天消息事件，SystemLogMessage 时间轴同步已跳过。");
            }
            else
            {
                chatMessageDelegate = Delegate.CreateDelegate(handlerType, this, methodInfo, false);
                if (chatMessageDelegate == null)
                {
                    LogHelper.Warning("时间轴", "聊天消息事件签名与当前插件不兼容，SystemLogMessage 时间轴同步已跳过。 ");
                    return;
                }

                ChatReflectionAccessor.AddEventHandler(DalamudApi.ChatGui, "ChatMessage", chatMessageDelegate);
                LogHelper.Debug("时间轴", "已接入聊天系统消息同步。 ");
            }
        }
        catch (Exception ex)
        {
            chatMessageDelegate = null;
            LogHelper.Warning("时间轴", ex, "接入聊天系统消息失败，SystemLogMessage 时间轴同步已跳过。插件会继续加载。");
        }
    }

    private void UnregisterChatHandlers()
    {
        if (chatMessageDelegate == null)
        {
            UnregisterLogMessageHandler();
            return;
        }

        try
        {
            ChatReflectionAccessor.RemoveEventHandler(DalamudApi.ChatGui, "ChatMessage", chatMessageDelegate);
        }
        catch
        {
            // Ignore shutdown failures.
        }

        chatMessageDelegate = null;
        UnregisterLogMessageHandler();
    }

    private void RegisterLogMessageHandler()
    {
        try
        {
            var eventInfo = ChatReflectionAccessor.GetEvent(DalamudApi.ChatGui, "LogMessage");
            var handlerType = eventInfo?.EventHandlerType;
            var methodInfo = ResolveLogMessageHandler(handlerType);
            LogHelper.Debug("时间轴", $"LogMessage 事件签名：{ChatReflectionAccessor.DescribeDelegate(handlerType)}，处理器：{methodInfo?.Name ?? "无"}。 ");
            if (eventInfo == null || handlerType == null || methodInfo == null)
                return;

            logMessageDelegate = Delegate.CreateDelegate(handlerType, this, methodInfo, false);
            if (logMessageDelegate == null)
                return;

            ChatReflectionAccessor.AddEventHandler(DalamudApi.ChatGui, "LogMessage", logMessageDelegate);
            LogHelper.Debug("时间轴", "已接入 LogMessage 系统消息同步。 ");
        }
        catch (Exception ex)
        {
            logMessageDelegate = null;
            LogHelper.Debug("时间轴", ex, "接入 LogMessage 系统消息失败。 ");
        }
    }

    private void UnregisterLogMessageHandler()
    {
        if (logMessageDelegate == null)
            return;

        try
        {
            ChatReflectionAccessor.RemoveEventHandler(DalamudApi.ChatGui, "LogMessage", logMessageDelegate);
        }
        catch
        {
        }

        logMessageDelegate = null;
    }

    private MethodInfo? ResolveChatMessageHandler(Type? handlerType)
    {
        var invoke = handlerType?.GetMethod("Invoke");
        var parameters = invoke?.GetParameters();
        if (parameters == null)
            return null;

        if (parameters.Length == 1)
            return GetType().GetMethod(nameof(OnHandleableChatMessage), BindingFlags.Instance | BindingFlags.NonPublic);

        if (parameters.Length < 5)
            return null;

        var secondParameterType = parameters[1].ParameterType;
        var methodName = secondParameterType == typeof(uint)
            ? nameof(OnChatMessageWithSenderId)
            : nameof(OnChatMessageWithTimestamp);
        return GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
    }

    private MethodInfo? ResolveLogMessageHandler(Type? handlerType)
    {
        var invoke = handlerType?.GetMethod("Invoke");
        var parameters = invoke?.GetParameters();
        if (parameters == null || parameters.Length != 1)
            return null;

        return GetType().GetMethod(nameof(OnLogMessage), BindingFlags.Instance | BindingFlags.NonPublic);
    }

    private void OnChatMessageWithSenderId(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        _ = senderId;
        HandleTimelineChatMessage(type, ref sender, ref message, ref isHandled);
    }

    private void OnChatMessageWithTimestamp(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        _ = timestamp;
        HandleTimelineChatMessage(type, ref sender, ref message, ref isHandled);
    }

    private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled)
        => OnChatMessageWithTimestamp(type, timestamp, ref sender, ref message, ref isHandled);

    private void OnHandleableChatMessage(object message)
    {
        try
        {
            var text = ChatReflectionAccessor.ExtractChatMessageText(message);
            if (string.IsNullOrWhiteSpace(text))
                return;

            var logKind = ChatReflectionAccessor.GetLogKind(message);
            if (IsNpcYellLikeChatKind(logKind))
            {
                timelineService.ObserveNpcYell(text, DateTime.UtcNow);
                return;
            }

            if (!IsSystemLikeChatKind(logKind))
                return;

            var nowUtc = DateTime.UtcNow;
            ObserveBattleCountdownMessage(text, nowUtc);
            LogRawPacketsNearSystemMessage("handleable-chat", text);
            timelineService.ObserveSystemLogMessage(text, nowUtc);
        }
        catch (Exception ex)
        {
            LogHelper.Debug("时间轴", ex, "处理新版聊天时间轴同步失败。 ");
        }
    }

    private void OnLogMessage(object message)
    {
        try
        {
            var text = ChatReflectionAccessor.ExtractLogMessageText(message);
            if (string.IsNullOrWhiteSpace(text))
                return;

            var nowUtc = DateTime.UtcNow;
            ObserveBattleCountdownMessage(text, nowUtc);
            LogRawPacketsNearSystemMessage("log-message", text);
            timelineService.ObserveSystemLogMessage(text, nowUtc);
        }
        catch (Exception ex)
        {
            LogHelper.Debug("时间轴", ex, "处理 LogMessage 时间轴同步失败。 ");
        }
    }

    private void HandleTimelineChatMessage(XivChatType type, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        _ = sender;
        _ = isHandled;

        try
        {
            var text = message.TextValue ?? message.ToString();
            if (string.IsNullOrWhiteSpace(text))
                return;

            if (IsNpcYellLikeChatKind(type.ToString()))
            {
                timelineService.ObserveNpcYell(text, DateTime.UtcNow);
                return;
            }

            if (type != XivChatType.SystemMessage)
                return;

            var nowUtc = DateTime.UtcNow;
            ObserveBattleCountdownMessage(text, nowUtc);
            LogRawPacketsNearSystemMessage("system-chat", text);
            timelineService.ObserveSystemLogMessage(text, nowUtc);
        }
        catch (Exception ex)
        {
            LogHelper.Debug("时间轴", ex, "处理系统日志时间轴同步失败。");
        }
    }

    private void ObserveBattleCountdownMessage(string text, DateTime nowUtc)
    {
        var normalized = text.Trim();
        if (normalized.Contains("距离战斗开始还有5秒", StringComparison.Ordinal))
        {
            lastBattleCountdownFiveSecondsUtc = nowUtc;
            LogHelper.Debug("队友监控", "检测到战斗倒计时 5 秒，等待战斗开始消息以重置技能冷却。");
            return;
        }

        if (!normalized.Contains("战斗开始", StringComparison.Ordinal))
            return;

        if (lastBattleCountdownFiveSecondsUtc == DateTime.MinValue
            || nowUtc - lastBattleCountdownFiveSecondsUtc > TimeSpan.FromSeconds(10))
        {
            return;
        }

        lastBattleCountdownFiveSecondsUtc = DateTime.MinValue;
        if (DalamudApi.ClientState.IsPvP)
            return;

        monitorService.ResetSkillCooldowns(nowUtc);
        LogHelper.Info("队友监控", "检测到战斗倒计时 5 秒后战斗开始，已重置队友技能冷却。");
    }

    private static bool IsSystemLikeChatKind(string logKind)
        => logKind.Contains("System", StringComparison.OrdinalIgnoreCase)
           || logKind.Contains("Notice", StringComparison.OrdinalIgnoreCase)
           || logKind.Contains("Progress", StringComparison.OrdinalIgnoreCase);

    private static bool IsNpcYellLikeChatKind(string logKind)
        => logKind.Contains("Npc", StringComparison.OrdinalIgnoreCase)
           || logKind.Contains("Yell", StringComparison.OrdinalIgnoreCase)
           || logKind.Contains("Dialogue", StringComparison.OrdinalIgnoreCase)
           || logKind.Contains("Talk", StringComparison.OrdinalIgnoreCase);

}
