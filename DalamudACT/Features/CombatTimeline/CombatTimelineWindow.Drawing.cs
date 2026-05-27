using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal sealed partial class CombatTimelineWindow
{
    private void DrawToolbar(
        IReadOnlyList<LocalStatsService.CombatTimelineEntry> filteredEntries,
        IReadOnlyList<string> actorOptions,
        IReadOnlyList<string> targetOptions,
        IReadOnlyList<string> actionOptions)
    {
        var recordingEnabled = config.CombatTimelineRecordingEnabled;
        if (ImGui.Checkbox("开始记录", ref recordingEnabled))
            statsService.SetCombatTimelineRecordingEnabled(recordingEnabled);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("开启后才会写入新的战斗流水；关闭后保留已有流水，方便复制或排查。插件每次加载时默认关闭。");

        ImGui.SameLine();
        ImGui.TextDisabled(recordingEnabled ? "记录中" : "已停止");

        ImGui.SameLine();
        ImGui.Checkbox("自动滚动到最新事件", ref autoScroll);

        DrawInlineFeedback();

        ImGui.Spacing();

        ImGui.BeginDisabled(filteredEntries.Count == 0);
        if (ImGui.Button("复制当前显示"))
        {
            ImGui.SetClipboardText(BuildTimelineText(filteredEntries));
            ShowInlineFeedback("已复制");
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("复制当前筛选结果。");

        if (selectedTimelineIndices.Count > 0)
        {
            ImGui.SameLine();
            if (ImGui.Button($"复制选中({selectedTimelineIndices.Count})"))
            {
                var text = BuildSelectedTimelineText(filteredEntries);
                ImGui.SetClipboardText(text);
                ShowInlineFeedback($"已复制 {selectedTimelineIndices.Count} 行");
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("复制已选中的行。点击行可切换选中，Shift+点击可连续多选。");

            ImGui.SameLine();
            if (ImGui.Button("取消选中"))
            {
                selectedTimelineIndices.Clear();
                lastSelectedTimelineIndex = -1;
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("清空流水"))
        {
            statsService.ClearCombatTimeline();
            ShowInlineFeedback("已清空");
        }

        ImGui.EndDisabled();

        ImGui.Spacing();
        if (!ImGui.CollapsingHeader("保留与筛选###combat_timeline_tools"))
            return;

        DrawRetentionControls();

        ImGui.Spacing();
        DrawFilterControls(actorOptions, targetOptions, actionOptions);

        ImGui.Spacing();
        DrawQuickFilterControls();
    }

    private void DrawTimelineTable(IReadOnlyList<LocalStatsService.CombatTimelineEntry> entries)
    {
        ImGui.BeginChild("##combat_timeline_container", new Vector2(0f, 0f), true);
        try
        {
            if (entries.Count == 0)
            {
                lastRenderedEntryCount = 0;
                ImGui.TextDisabled(!config.CombatTimelineRecordingEnabled && !HasAnyActiveFilter()
                    ? "战斗流水记录已停止。勾选\"开始记录\"后才会写入新事件；关闭不会清空已有流水。"
                    : !HasAnyActiveFilter()
                    ? "暂无战斗流水。进入战斗后，这里会开始记录关键事件。"
                    : "当前筛选条件下暂无战斗流水。");
                return;
            }

            if (entries.Count != lastRenderedEntryCount)
            {
                selectedTimelineIndices.Clear();
                lastSelectedTimelineIndex = -1;
            }

            var shouldAutoScroll = autoScroll && entries.Count != lastRenderedEntryCount;
            var shiftHeld = ImGui.IsKeyDown(ImGuiKey.ModShift);

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                var isSelected = selectedTimelineIndices.Contains(index);

                ImGui.PushStyleColor(ImGuiCol.Text, GetEntryColor(entry.Kind));
                if (isSelected)
                    ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.30f, 0.48f, 0.78f, 0.55f));
                ImGui.PushID(index);
                try
                {
                    var line = $"[{entry.TimestampLocal:HH:mm:ss}] {entry.Message}";
                    if (ImGui.Selectable(line, isSelected))
                    {
                        if (shiftHeld && lastSelectedTimelineIndex >= 0)
                        {
                            var start = Math.Min(lastSelectedTimelineIndex, index);
                            var end = Math.Max(lastSelectedTimelineIndex, index);
                            for (var i = start; i <= end; i++)
                                selectedTimelineIndices.Add(i);
                        }
                        else
                        {
                            if (isSelected)
                                selectedTimelineIndices.Remove(index);
                            else
                                selectedTimelineIndices.Add(index);
                            lastSelectedTimelineIndex = index;
                        }
                    }
                }
                finally
                {
                    ImGui.PopID();
                    if (isSelected)
                        ImGui.PopStyleColor();
                    ImGui.PopStyleColor();
                }

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(entry.TimestampLocal.ToString("yyyy-MM-dd HH:mm:ss.fff"));

                if (shouldAutoScroll && index == entries.Count - 1)
                    ImGui.SetScrollHereY(1f);
            }

            lastRenderedEntryCount = entries.Count;
        }
        finally
        {
            ImGui.EndChild();
        }
    }

    private string BuildSelectedTimelineText(IReadOnlyList<LocalStatsService.CombatTimelineEntry> entries)
    {
        var builder = new StringBuilder(selectedTimelineIndices.Count * 48);
        var sortedIndices = selectedTimelineIndices.OrderBy(static i => i).ToList();
        foreach (var index in sortedIndices)
        {
            if (index < 0 || index >= entries.Count)
                continue;
            var entry = entries[index];
            builder.AppendLine($"{entry.TimestampLocal:yyyy-MM-dd HH:mm:ss.fff} {entry.Message}");
        }
        return builder.ToString().TrimEnd();
    }

    private void ShowInlineFeedback(string text)
    {
        inlineFeedbackText = string.IsNullOrWhiteSpace(text) ? "已完成" : text;
        lastInlineFeedbackAtUtc = DateTime.UtcNow;
    }

    private void DrawInlineFeedback()
    {
        if (!lastInlineFeedbackAtUtc.HasValue || DateTime.UtcNow - lastInlineFeedbackAtUtc.Value > InlineFeedbackDuration)
            return;

        ImGui.SameLine();
        ImGui.TextDisabled(inlineFeedbackText);
    }

    private static Vector4 GetEntryColor(LocalStatsService.CombatTimelineEntryKind kind)
    {
        return kind switch
        {
            LocalStatsService.CombatTimelineEntryKind.CombatStart => new Vector4(0.48f, 0.92f, 0.60f, 1f),
            LocalStatsService.CombatTimelineEntryKind.Heal => new Vector4(0.40f, 0.92f, 0.72f, 1f),
            LocalStatsService.CombatTimelineEntryKind.Status => new Vector4(0.68f, 0.82f, 1f, 1f),
            LocalStatsService.CombatTimelineEntryKind.Failure => new Vector4(1f, 0.84f, 0.42f, 1f),
            LocalStatsService.CombatTimelineEntryKind.Death => new Vector4(1f, 0.52f, 0.52f, 1f),
            LocalStatsService.CombatTimelineEntryKind.CombatEnd => new Vector4(0.98f, 0.76f, 0.45f, 1f),
            _ => new Vector4(0.88f, 0.92f, 1f, 1f),
        };
    }

    private static string BuildTimelineText(IReadOnlyList<LocalStatsService.CombatTimelineEntry> entries)
    {
        if (entries.Count == 0)
            return "暂无战斗流水。";

        var builder = new StringBuilder(entries.Count * 48);
        foreach (var entry in entries)
            builder.AppendLine($"{entry.TimestampLocal:yyyy-MM-dd HH:mm:ss.fff} {entry.Message}");

        return builder.ToString().TrimEnd();
    }

    private void DrawRetentionControls()
    {
        ImGui.TextDisabled("保留条数：");
        ImGui.SameLine();

        var presets = new[] { 500, 2000, 10000, 50000, 0 };
        for (var index = 0; index < presets.Length; index++)
        {
            if (index > 0)
            {
                ImGui.SameLine();
                ImGui.TextDisabled("/");
                ImGui.SameLine();
            }

            var preset = presets[index];
            var label = preset == 0 ? "全部" : FormatEntryCountPreset(preset);
            var isSelected = config.CombatTimelineMaxEntries == preset;
            if (isSelected)
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.28f, 0.46f, 0.82f, 0.95f));
            try
            {
                if (ImGui.SmallButton($"{label}##timeline_preset_{preset}"))
                    ApplyCombatTimelineMaxEntries(preset);
            }
            finally
            {
                if (isSelected)
                    ImGui.PopStyleColor();
            }
        }

        ImGui.SameLine();
        ImGui.TextDisabled($"当前：{GetRetentionDisplayText()}");
    }

    private void DrawFilterControls(
        IReadOnlyList<string> actorOptions,
        IReadOnlyList<string> targetOptions,
        IReadOnlyList<string> actionOptions)
    {
        DrawCampFilterCombo("角色阵营：", "timeline_actor_camp_filter", ref actorCampFilter);
        ImGui.SameLine();
        DrawFilterCombo("只显示角色：", "timeline_actor_filter", ref actorFilter, actorOptions, "全部");

        DrawCampFilterCombo("被攻击人阵营：", "timeline_target_camp_filter", ref targetCampFilter);
        ImGui.SameLine();
        DrawFilterCombo("只显示被攻击人：", "timeline_target_filter", ref targetFilter, targetOptions, "全部");

        DrawFilterCombo("只显示技能：", "timeline_action_filter", ref actionFilter, actionOptions, "全部");
        ImGui.SameLine();
        DrawSearchInput("技能搜索：", "timeline_action_search", ref actionSearchText);
    }

    private void DrawQuickFilterControls()
    {
        DrawKindFilterCombo();
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("快捷查看：");

        ImGui.SameLine();
        if (ImGui.SmallButton("玩家输出"))
            ApplyQuickFilterPlayerOutput();

        ImGui.SameLine();
        if (ImGui.SmallButton("敌打我方"))
            ApplyQuickFilterEnemyHitFriendly();

        ImGui.SameLine();
        if (ImGui.SmallButton("治疗"))
            ApplyQuickFilterKind(TimelineKindFilter.Heal);

        ImGui.SameLine();
        if (ImGui.SmallButton("死亡"))
            ApplyQuickFilterKind(TimelineKindFilter.Death);

        ImGui.SameLine();
        ImGui.BeginDisabled(!HasAnyActiveFilter());
        if (ImGui.SmallButton("清空筛选"))
            ClearAllFilters();
        ImGui.EndDisabled();
    }

    private static void DrawCampFilterCombo(string label, string id, ref TimelineCampFilter currentValue)
    {
        const string emptyLabel = "全部";
        var previewValue = GetCampFilterLabel(currentValue);

        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(label);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(110f);
        if (!ImGui.BeginCombo($"##{id}", previewValue))
            return;

        try
        {
            foreach (var value in Enum.GetValues<TimelineCampFilter>())
            {
                var isSelected = value == currentValue;
                var optionLabel = value == TimelineCampFilter.All ? emptyLabel : GetCampFilterLabel(value);
                if (ImGui.Selectable(optionLabel, isSelected))
                    currentValue = value;

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }
        }
        finally
        {
            ImGui.EndCombo();
        }
    }

    private void DrawKindFilterCombo()
    {
        var previewValue = GetKindFilterLabel(kindFilter);

        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("事件类型：");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(120f);
        if (!ImGui.BeginCombo("##timeline_kind_filter", previewValue))
            return;

        try
        {
            foreach (var value in Enum.GetValues<TimelineKindFilter>())
            {
                var isSelected = value == kindFilter;
                if (ImGui.Selectable(GetKindFilterLabel(value), isSelected))
                    kindFilter = value;

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }
        }
        finally
        {
            ImGui.EndCombo();
        }
    }

    private static void DrawFilterCombo(
        string label,
        string id,
        ref string currentValue,
        IReadOnlyList<string> options,
        string emptyLabel)
    {
        var previewValue = string.IsNullOrWhiteSpace(currentValue) ? emptyLabel : currentValue;
        var values = BuildFilterComboOptions(options, currentValue);

        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(label);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(160f);
        if (!ImGui.BeginCombo($"##{id}", previewValue))
            return;

        try
        {
            foreach (var value in values)
            {
                var isEmptyOption = string.IsNullOrWhiteSpace(value);
                var optionLabel = isEmptyOption ? emptyLabel : value;
                var isSelected = string.Equals(currentValue, value, StringComparison.Ordinal)
                                 || (isEmptyOption && string.IsNullOrWhiteSpace(currentValue));

                if (ImGui.Selectable(optionLabel, isSelected))
                    currentValue = isEmptyOption ? string.Empty : value;

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }
        }
        finally
        {
            ImGui.EndCombo();
        }
    }

    private static void DrawSearchInput(string label, string id, ref string currentValue)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(label);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180f);
        ImGui.InputText($"##{id}", ref currentValue, 128);

        if (string.IsNullOrWhiteSpace(currentValue))
            return;

        ImGui.SameLine();
        if (ImGui.SmallButton($"清空##{id}"))
            currentValue = string.Empty;
    }

    private static IReadOnlyList<string> BuildFilterComboOptions(IReadOnlyList<string> options, string currentValue)
    {
        var values = new List<string>(options.Count + 1)
        {
            string.Empty,
        };

        foreach (var option in options)
        {
            if (!values.Contains(option, StringComparer.Ordinal))
                values.Add(option);
        }

        if (!string.IsNullOrWhiteSpace(currentValue) && !values.Contains(currentValue, StringComparer.Ordinal))
            values.Add(currentValue);

        return values;
    }

    private void ApplyCombatTimelineMaxEntries(int value)
    {
        var normalized = value <= 0 ? 0 : Math.Clamp(value, 100, 50000);
        if (config.CombatTimelineMaxEntries == normalized)
            return;

        config.CombatTimelineMaxEntries = normalized;
        statsService.ApplyCombatTimelineRetentionLimit();
        config.Save();
    }

    private string GetRetentionDisplayText()
        => config.CombatTimelineMaxEntries <= 0 ? "全部" : $"{config.CombatTimelineMaxEntries} 条";

    private static string FormatEntryCountPreset(int value)
        => value >= 10000 ? $"{value / 10000d:0.#}万" : value.ToString();

    private static string BuildCountSummary(int totalCount, int filteredCount)
        => totalCount == filteredCount ? $"共 {totalCount} 条" : $"共 {totalCount} 条，当前显示 {filteredCount} 条";

}
