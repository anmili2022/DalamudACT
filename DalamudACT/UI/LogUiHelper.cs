using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

/// <summary>
/// 日志相关的 UI 展示辅助。
/// 目前用于主窗口和设置页中“最近日志摘要”的级别着色，避免重复维护相同的颜色映射。
/// </summary>
internal static class LogUiHelper
{
    private static readonly TimeSpan InlineFeedbackDuration = TimeSpan.FromSeconds(2.4);
    private static DateTime? lastInlineFeedbackAtUtc;
    private static string inlineFeedbackText = "已复制";

    public static bool HasRecentLogs
        => LogHelper.RecentLogs.Count > 0;

    public static Vector4 GetRecentLogLevelColor(string level)
    {
        return level switch
        {
            "错误" or "ERROR" => new Vector4(1f, 0.42f, 0.42f, 1f),
            "警告" or "WARN" => new Vector4(1f, 0.82f, 0.32f, 1f),
            "调试" or "DEBUG" => new Vector4(0.68f, 0.84f, 1f, 1f),
            _ => new Vector4(0.82f, 0.90f, 1f, 1f),
        };
    }

    public static bool CopyRecentLogsToClipboard(int maxItems = int.MaxValue)
    {
        if (!HasRecentLogs)
            return false;

        ImGui.SetClipboardText(BuildRecentLogSummaryText(maxItems));
        ShowInlineFeedback("已复制");
        return true;
    }

    public static void ShowInlineFeedback(string text)
    {
        inlineFeedbackText = string.IsNullOrWhiteSpace(text) ? "已完成" : text;
        lastInlineFeedbackAtUtc = DateTime.UtcNow;
    }

    public static void DrawInlineFeedback()
    {
        if (!lastInlineFeedbackAtUtc.HasValue || DateTime.UtcNow - lastInlineFeedbackAtUtc.Value > InlineFeedbackDuration)
            return;

        ImGui.SameLine();
        ImGui.TextDisabled(inlineFeedbackText);
    }

    public static void DrawRecentLogToolbar(int copyMaxItems = int.MaxValue)
    {
        ImGui.BeginDisabled(!HasRecentLogs);
        if (ImGui.SmallButton("复制全部"))
            CopyRecentLogsToClipboard(copyMaxItems);
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("复制当前最近日志摘要到剪贴板。");

        ImGui.SameLine();
        if (ImGui.SmallButton("清空摘要"))
        {
            LogHelper.ClearRecentLogs();
            ShowInlineFeedback("已清空");
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("只清空插件内的最近日志摘要缓存，不影响 Dalamud 日志文件。");

        ImGui.EndDisabled();
        DrawInlineFeedback();
    }

    public static void DrawRecentLogList(int maxItems = int.MaxValue)
    {
        var recentLogs = LogHelper.RecentLogs;
        if (recentLogs.Count == 0)
        {
            ImGui.TextDisabled("暂无最近日志。");
            return;
        }

        var shownCount = 0;
        for (var index = recentLogs.Count - 1; index >= 0 && shownCount < maxItems; index--, shownCount++)
        {
            var entry = recentLogs[index];
            ImGui.Bullet();
            ImGui.SameLine();
            ImGui.PushStyleColor(ImGuiCol.Text, GetRecentLogLevelColor(entry.Level));
            ImGui.TextWrapped($"[{entry.Level}] {entry.TimestampLocal:HH:mm:ss} {entry.Message}");
            ImGui.PopStyleColor();

            if (ImGui.IsItemHovered())
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted($"级别：{entry.Level}");
                ImGui.TextUnformatted($"时间：{entry.TimestampLocal:yyyy-MM-dd HH:mm:ss.fff}");
                ImGui.Separator();
                if (ImGui.SmallButton($"复制全文##recent_log_copy_{index}"))
                {
                    ImGui.SetClipboardText(BuildRecentLogFullText(entry));
                    ShowInlineFeedback("已复制");
                }

                ImGui.Separator();
                ImGui.PushTextWrapPos(Math.Min(ImGui.GetFontSize() * 28f, 720f));
                ImGui.TextUnformatted(entry.Message);
                ImGui.PopTextWrapPos();
                ImGui.EndTooltip();
            }
        }
    }

    private static string BuildRecentLogSummaryText(int maxItems)
    {
        var recentLogs = LogHelper.RecentLogs;
        if (recentLogs.Count == 0)
            return "暂无最近日志。";

        var lines = new System.Collections.Generic.List<string>(Math.Min(recentLogs.Count, maxItems == int.MaxValue ? recentLogs.Count : maxItems));
        var shownCount = 0;
        for (var index = recentLogs.Count - 1; index >= 0 && shownCount < maxItems; index--, shownCount++)
            lines.Add(BuildRecentLogFullText(recentLogs[index]));

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildRecentLogFullText(LogHelper.RecentLogEntry entry)
        => $"[{entry.Level}] {entry.TimestampLocal:yyyy-MM-dd HH:mm:ss.fff} {entry.Message}";
}
