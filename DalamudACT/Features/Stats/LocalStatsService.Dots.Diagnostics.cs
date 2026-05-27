using System;
using System.Linq;

namespace DalamudACT;

internal sealed partial class LocalStatsService
{
    private static bool IsFocusedPlayerDotDiagnosticAction(uint actionId)
        => actionId != 0 && FocusedPlayerDotDiagnosticActionIds.Contains(actionId);

    private static bool IsFocusedPlayerDotDiagnosticStatus(uint statusId)
        => statusId != 0 && FocusedPlayerDotDiagnosticStatusIds.Contains(statusId);

    private static bool IsFocusedPlayerDotDiagnosticSkill(PlayerDotSkillEntry? skillEntry)
        => skillEntry != null
           && (skillEntry.ActionIds.Any(IsFocusedPlayerDotDiagnosticAction)
               || skillEntry.StatusIds.Any(IsFocusedPlayerDotDiagnosticStatus));

    private static bool IsFocusedPlayerDotDiagnosticState(ActivePlayerDotState state)
        => IsFocusedPlayerDotDiagnosticStatus(state.Key.StatusId)
           || IsFocusedPlayerDotDiagnosticAction(state.ActionId)
           || IsFocusedPlayerDotDiagnosticSkill(state.SkillEntry);

    private string BuildFocusedPlayerDotDiagnosticStateText(ActivePlayerDotState state, DateTime nowUtc)
    {
        var targetName = ResolveCombatTimelineTargetName(state.Key.TargetActorId, nowUtc);
        var actionText = FormatActionNameWithId(state.ActionName, state.ActionId);
        var statusText = string.IsNullOrWhiteSpace(state.StatusName)
            ? $"0x{state.Key.StatusId:X}"
            : $"{state.StatusName}/0x{state.Key.StatusId:X}";
        return $"source={state.Source.Name}/0x{state.Key.SourceActorId:X8}，target={targetName}/0x{state.Key.TargetActorId:X8}，action={actionText}，status={statusText}";
    }

    private void LogFocusedPlayerDotDiagnosticLocked(
        DateTime nowUtc,
        string diagnosticKey,
        string message,
        bool includeRecentSummary = true)
    {
        if (!LogHelper.EnableDebugLog)
            return;

        var key = $"player-dot:{diagnosticKey}";
        if (playerDotDiagnosticLogTimestamps.TryGetValue(key, out var lastLogUtc)
            && nowUtc - lastLogUtc < PlayerDotFocusedDiagnosticLogThrottle)
        {
            return;
        }

        playerDotDiagnosticLogTimestamps[key] = nowUtc;
        if (playerDotDiagnosticLogTimestamps.Count > 256)
        {
            var staleKeys = playerDotDiagnosticLogTimestamps
                .OrderBy(static pair => pair.Value)
                .Take(64)
                .Select(static pair => pair.Key)
                .ToList();
            foreach (var staleKey in staleKeys)
                playerDotDiagnosticLogTimestamps.Remove(staleKey);
        }

        // 这组日志是短期现场对账用的聚焦诊断。仍然受 EnableDebugLog 控制，
        // 但使用 Info 级别写出，避免 Dalamud 当前日志级别不落 Debug 时无法在 dalamud.log 中检索到。
        LogHelper.Info("统计", $"DOT诊断：{message}");
    }
}
