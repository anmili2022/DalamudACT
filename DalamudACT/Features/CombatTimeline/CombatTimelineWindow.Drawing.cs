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
        IReadOnlyList<string> characterOptions)
    {
        var recordingEnabled = config.CombatTimelineRecordingEnabled;
        if (ImGui.Checkbox("开始记录", ref recordingEnabled))
            statsService.SetCombatTimelineRecordingEnabled(recordingEnabled);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("开启后会写入新的战斗流水；关闭后保留已有流水，方便复制或排查。开关状态会保存到配置。");

        ImGui.SameLine();
        ImGui.TextDisabled(recordingEnabled ? "记录中" : "已停止");

        ImGui.SameLine();
        if (ImGui.Checkbox("自动滚动到最新事件", ref autoScroll))
        {
            config.CombatTimelineAutoScroll = autoScroll;
            config.Save();
        }

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
        DrawTimeDisplayControls();

        ImGui.Spacing();
        DrawFilterControls(characterOptions);
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

                ImGui.PushStyleColor(ImGuiCol.Text, GetEntryColor(entry));
                if (isSelected)
                    ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.30f, 0.48f, 0.78f, 0.55f));
                ImGui.PushID(index);
                try
                {
                    var line = $"{FormatTimelineLine(entry, includeMilliseconds: false)}";
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
            builder.AppendLine(FormatTimelineLine(entry, includeMilliseconds: true));
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

    private static Vector4 GetEntryColor(LocalStatsService.CombatTimelineEntry entry)
    {
        if (IsUnmitigatedTakenDamageEntry(entry))
            return new Vector4(1f, 0.36f, 0.30f, 1f);

        return entry.Kind switch
        {
            LocalStatsService.CombatTimelineEntryKind.CombatStart => new Vector4(0.48f, 0.92f, 0.60f, 1f),
            LocalStatsService.CombatTimelineEntryKind.Heal => new Vector4(0.40f, 0.92f, 0.72f, 1f),
            LocalStatsService.CombatTimelineEntryKind.Cast => new Vector4(0.86f, 0.72f, 1f, 1f),
            LocalStatsService.CombatTimelineEntryKind.Status => new Vector4(0.68f, 0.82f, 1f, 1f),
            LocalStatsService.CombatTimelineEntryKind.Failure => new Vector4(1f, 0.84f, 0.42f, 1f),
            LocalStatsService.CombatTimelineEntryKind.Death => new Vector4(1f, 0.52f, 0.52f, 1f),
            LocalStatsService.CombatTimelineEntryKind.MapEffect => new Vector4(0.96f, 0.56f, 0.96f, 1f),
            LocalStatsService.CombatTimelineEntryKind.TargetIcon => new Vector4(1f, 0.72f, 0.48f, 1f),
            LocalStatsService.CombatTimelineEntryKind.Tether => new Vector4(0.72f, 0.64f, 1f, 1f),
            LocalStatsService.CombatTimelineEntryKind.CombatEnd => new Vector4(0.98f, 0.76f, 0.45f, 1f),
            _ => new Vector4(0.88f, 0.92f, 1f, 1f),
        };
    }

    private string BuildTimelineText(IReadOnlyList<LocalStatsService.CombatTimelineEntry> entries)
    {
        if (entries.Count == 0)
            return "暂无战斗流水。";

        var builder = new StringBuilder(entries.Count * 48);
        foreach (var entry in entries)
            builder.AppendLine(FormatTimelineLine(entry, includeMilliseconds: true));

        return builder.ToString().TrimEnd();
    }

    private string FormatTimelineLine(LocalStatsService.CombatTimelineEntry entry, bool includeMilliseconds)
    {
        var prefix = BuildTimePrefix(entry, includeMilliseconds);
        return string.IsNullOrWhiteSpace(prefix)
            ? entry.Message
            : $"{prefix}{entry.Message}";
    }

    private string BuildTimePrefix(LocalStatsService.CombatTimelineEntry entry, bool includeMilliseconds)
    {
        var parts = new List<string>(2);
        if (config.CombatTimelineShowRawTime)
        {
            var format = includeMilliseconds ? "yyyy-MM-dd HH:mm:ss.fff" : "HH:mm:ss";
            parts.Add($"[{entry.TimestampLocal.ToString(format)}]");
        }

        if (config.CombatTimelineShowEncounterTime && entry.EncounterSeconds.HasValue)
            parts.Add($"[{entry.EncounterSeconds.Value}]");

        return parts.Count == 0 ? string.Empty : string.Concat(parts);
    }

    private void DrawTimeDisplayControls()
    {
        ImGui.TextDisabled("时间显示：");
        ImGui.SameLine();

        var showEncounterTime = config.CombatTimelineShowEncounterTime;
        if (ImGui.Checkbox("战斗时间", ref showEncounterTime))
        {
            config.CombatTimelineShowEncounterTime = showEncounterTime;
            config.Save();
        }

        ImGui.SameLine(0f, 12f);
        var showRawTime = config.CombatTimelineShowRawTime;
        if (ImGui.Checkbox("原始时间", ref showRawTime))
        {
            config.CombatTimelineShowRawTime = showRawTime;
            config.Save();
        }
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

        var mapEffectEnabled = config.CombatTimelineMapEffectEnabled;
        if (ImGui.Checkbox("显示场地特效", ref mapEffectEnabled))
        {
            config.CombatTimelineMapEffectEnabled = mapEffectEnabled;
            config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("开启后会把 MapEffect 场地特效写入新的战斗流水；关闭不会移除已有记录。 ");

        ImGui.SameLine();
        var targetIconEnabled = config.CombatTimelineTargetIconEnabled;
        if (ImGui.Checkbox("显示头顶标记", ref targetIconEnabled))
        {
            config.CombatTimelineTargetIconEnabled = targetIconEnabled;
            config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("开启后会把 ActorControl 头顶标记写入新的战斗流水；关闭不会移除已有记录。 ");

        ImGui.SameLine();
        var tetherEnabled = config.CombatTimelineTetherEnabled;
        if (ImGui.Checkbox("显示连线", ref tetherEnabled))
        {
            config.CombatTimelineTetherEnabled = tetherEnabled;
            config.Save();
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("开启后会把 ActorControl 连线和连线取消写入新的战斗流水；关闭不会移除已有记录。 ");
    }

    private void DrawFilterControls(IReadOnlyList<string> characterOptions)
    {
        if (DrawFilterCombo("角色：", "timeline_character_filter", ref characterFilter, characterOptions, "全部"))
        {
            config.CombatTimelineCharacterFilter = characterFilter;
            config.Save();
        }
        ImGui.SameLine();
        if (DrawSearchInput("技能/全文搜索：", "timeline_text_search", ref textSearchFilter))
        {
            config.CombatTimelineTextSearchFilter = textSearchFilter;
            config.Save();
        }

        ImGui.Spacing();
        DrawContentFilterCheckbox("输出", TimelineContentFilter.Output);
        ImGui.SameLine();
        DrawContentFilterCheckbox("承伤", TimelineContentFilter.TakenDamage);
        ImGui.SameLine();
        DrawContentFilterCheckbox("治疗", TimelineContentFilter.Heal);
        ImGui.SameLine();
        DrawContentFilterCheckbox("死亡", TimelineContentFilter.Death);
        ImGui.SameLine();
        DrawContentFilterCheckbox("减伤分析", TimelineContentFilter.Mitigation);
        ImGui.SameLine();
        DrawContentFilterCheckbox("读条", TimelineContentFilter.Cast);
        ImGui.SameLine();
        DrawContentFilterCheckbox("状态", TimelineContentFilter.Status);
        ImGui.SameLine();
        DrawContentFilterCheckbox("场地特效", TimelineContentFilter.MapEffect);
        ImGui.SameLine();
        DrawContentFilterCheckbox("头顶标记", TimelineContentFilter.TargetIcon);
        ImGui.SameLine();
        DrawContentFilterCheckbox("连线", TimelineContentFilter.Tether);
        ImGui.SameLine();
        DrawContentFilterCheckbox("进出战", TimelineContentFilter.CombatBoundary);

        ImGui.SameLine();
        ImGui.BeginDisabled(!HasAnyActiveFilter());
        if (ImGui.SmallButton("清空筛选"))
            ClearAllFilters();
        ImGui.EndDisabled();
    }

    private void DrawContentFilterCheckbox(string label, TimelineContentFilter flag)
    {
        var enabled = contentFilters.HasFlag(flag);
        if (!ImGui.Checkbox(label, ref enabled))
            return;

        contentFilters = enabled
            ? contentFilters | flag
            : contentFilters & ~flag;
        config.CombatTimelineContentFilterMask = (int)contentFilters;
        config.Save();
    }

    private static bool DrawFilterCombo(
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
            return false;

        var changed = false;
        try
        {
            foreach (var value in values)
            {
                var isEmptyOption = string.IsNullOrWhiteSpace(value);
                var optionLabel = isEmptyOption ? emptyLabel : value;
                var isSelected = string.Equals(currentValue, value, StringComparison.Ordinal)
                                 || (isEmptyOption && string.IsNullOrWhiteSpace(currentValue));

                if (ImGui.Selectable(optionLabel, isSelected))
                {
                    currentValue = isEmptyOption ? string.Empty : value;
                    changed = true;
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }
        }
        finally
        {
            ImGui.EndCombo();
        }

        return changed;
    }

    private static bool DrawSearchInput(string label, string id, ref string currentValue)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(label);
        ImGui.SameLine();
        ImGui.SetNextItemWidth(180f);
        var changed = ImGui.InputText($"##{id}", ref currentValue, 128);

        if (string.IsNullOrWhiteSpace(currentValue))
            return changed;

        ImGui.SameLine();
        if (ImGui.SmallButton($"清空##{id}"))
        {
            currentValue = string.Empty;
            changed = true;
        }

        return changed;
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
