using System;
using System.Reflection;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace DalamudACT;

public sealed partial class ACT
{
    private Delegate? chatMessageDelegate;

    private void RegisterChatHandlers()
    {
        try
        {
            var eventInfo = DalamudApi.ChatGui.GetType().GetEvent("ChatMessage");
            var handlerType = eventInfo?.EventHandlerType;
            var methodInfo = ResolveChatMessageHandler(handlerType);
            if (eventInfo == null || handlerType == null || methodInfo == null)
            {
                LogHelper.Warning("时间轴", "当前 Dalamud 运行时未暴露可用的聊天消息事件，SystemLogMessage 时间轴同步已跳过。");
                return;
            }

            chatMessageDelegate = Delegate.CreateDelegate(handlerType, this, methodInfo, false);
            if (chatMessageDelegate == null)
            {
                LogHelper.Warning("时间轴", "聊天消息事件签名与当前插件不兼容，SystemLogMessage 时间轴同步已跳过。");
                return;
            }

            eventInfo.AddEventHandler(DalamudApi.ChatGui, chatMessageDelegate);
            LogHelper.Info("时间轴", "已接入聊天系统消息同步。 ");
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
            return;

        try
        {
            DalamudApi.ChatGui.GetType().GetEvent("ChatMessage")?.RemoveEventHandler(DalamudApi.ChatGui, chatMessageDelegate);
        }
        catch
        {
            // Ignore shutdown failures.
        }

        chatMessageDelegate = null;
    }

    private MethodInfo? ResolveChatMessageHandler(Type? handlerType)
    {
        var invoke = handlerType?.GetMethod("Invoke");
        var parameters = invoke?.GetParameters();
        if (parameters == null || parameters.Length < 5)
            return null;

        var secondParameterType = parameters[1].ParameterType;
        var methodName = secondParameterType == typeof(uint)
            ? nameof(OnChatMessageWithSenderId)
            : nameof(OnChatMessageWithTimestamp);
        return GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
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

    private void HandleTimelineChatMessage(XivChatType type, ref SeString sender, ref SeString message, ref bool isHandled)
    {
        _ = sender;
        _ = isHandled;

        if (type != XivChatType.SystemMessage)
            return;

        try
        {
            timelineService.ObserveSystemLogMessage(message.TextValue ?? message.ToString(), DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            LogHelper.Debug("时间轴", ex, "处理系统日志时间轴同步失败。");
        }
    }
}
