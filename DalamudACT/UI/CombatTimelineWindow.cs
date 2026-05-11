using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DalamudACT;

/// <summary>
/// 战斗流水窗口，用于按时间顺序展示进入战斗、技能事件、战斗结束等关键过程。
/// </summary>
internal sealed class CombatTimelineWindow : Window
{
    private enum TimelineCampFilter
    {
        All,
        Friendly,
        Hostile,
    }

    private enum TimelineKindFilter
    {
        All,
        Damage,
        Heal,
        Failure,
        Death,
        CombatBoundary,
    }

    private const ImGuiTableFlags TimelineTableFlags =
        ImGuiTableFlags.RowBg
        | ImGuiTableFlags.BordersInnerH
        | ImGuiTableFlags.SizingStretchProp
        | ImGuiTableFlags.ScrollY
        | ImGuiTableFlags.NoSavedSettings;

    private static readonly TimeSpan InlineFeedbackDuration = TimeSpan.FromSeconds(2.4);

    private readonly PluginConfiguration config;
    private readonly LocalStatsService statsService;
    private string actorFilter = string.Empty;
    private string actionFilter = string.Empty;
    private string actionSearchText = string.Empty;
    private string targetFilter = string.Empty;
    private TimelineCampFilter actorCampFilter;
    private TimelineCampFilter targetCampFilter;
    private TimelineKindFilter kindFilter;
    private bool autoScroll = true;
    private bool drawFaulted;
    private int lastRenderedEntryCount = -1;
    private DateTime? lastInlineFeedbackAtUtc;
    private string inlineFeedbackText = "已复制";

    public CombatTimelineWindow(PluginConfiguration config, LocalStatsService statsService)
        : base("战斗流水###CombatTimelineWindow")
    {
        this.config = config;
        this.statsService = statsService;
        Size = new Vector2(860f, 560f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        try
        {
            BgAlpha = Math.Clamp(config.WindowOpacity, 0.2f, 1f);

            var entries = statsService.CombatTimelineEntries;
            var filteredEntries = FilterEntries(entries, actorFilter, actorCampFilter, targetFilter, targetCampFilter, kindFilter, actionFilter);
            var actorOptions = BuildActorOptions(entries, actorFilter, actorCampFilter);
            var targetOptions = BuildTargetOptions(entries, targetFilter, targetCampFilter);
            var actionOptions = BuildActionOptions(entries, actionFilter, actorFilter, actorCampFilter, targetFilter, targetCampFilter, kindFilter, actionSearchText);

            ImGui.TextUnformatted("战斗流水");
            ImGui.SameLine();
            ImGui.TextDisabled(BuildCountSummary(entries.Count, filteredEntries.Count));
            ImGui.Separator();
            ImGui.TextWrapped("这里会按时间顺序记录进入战斗、攻击、治疗、未命中、战斗不能和战斗结束等关键事件。");

            DrawToolbar(filteredEntries, actorOptions, targetOptions, actionOptions);
            ImGui.Spacing();
            DrawTimelineTable(filteredEntries);
            drawFaulted = false;
        }
        catch (Exception ex)
        {
            if (!drawFaulted)
            {
                drawFaulted = true;
                LogHelper.Error("战斗流水", ex, "绘制战斗流水窗口失败，已自动关闭窗口以避免影响游戏。");
            }

            IsOpen = false;
        }
    }

    private void DrawToolbar(
        IReadOnlyList<LocalStatsService.CombatTimelineEntry> filteredEntries,
        IReadOnlyList<string> actorOptions,
        IReadOnlyList<string> targetOptions,
        IReadOnlyList<string> actionOptions)
    {
        ImGui.Checkbox("自动滚动到最新事件", ref autoScroll);

        ImGui.SameLine();
        ImGui.BeginDisabled(filteredEntries.Count == 0);
        if (ImGui.Button("复制当前显示"))
        {
            ImGui.SetClipboardText(BuildTimelineText(filteredEntries));
            ShowInlineFeedback("已复制");
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("复制当前筛选结果。");

        ImGui.SameLine();
        if (ImGui.Button("清空流水"))
        {
            statsService.ClearCombatTimeline();
            ShowInlineFeedback("已清空");
        }

        ImGui.EndDisabled();
        DrawInlineFeedback();

        ImGui.Spacing();
        DrawRetentionControls();

        ImGui.Spacing();
        DrawFilterControls(actorOptions, targetOptions, actionOptions);

        ImGui.Spacing();
        DrawQuickFilterControls();
    }

    private void DrawTimelineTable(IReadOnlyList<LocalStatsService.CombatTimelineEntry> entries)
    {
        ImGui.BeginChild("##combat_timeline_container", new Vector2(0f, 0f), true);

        if (entries.Count == 0)
        {
            lastRenderedEntryCount = 0;
            ImGui.TextDisabled(!HasAnyActiveFilter()
                ? "暂无战斗流水。进入战斗后，这里会开始记录关键事件。"
                : "当前筛选条件下暂无战斗流水。");
            ImGui.EndChild();
            return;
        }

        if (ImGui.BeginTable("##combat_timeline_table", 2, TimelineTableFlags))
        {
            ImGui.TableSetupColumn("时间", ImGuiTableColumnFlags.WidthFixed, 86f);
            ImGui.TableSetupColumn("内容", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableHeadersRow();

            var shouldAutoScroll = autoScroll && entries.Count != lastRenderedEntryCount;

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.TextDisabled(entry.TimestampLocal.ToString("HH:mm:ss"));

                ImGui.TableSetColumnIndex(1);
                ImGui.PushStyleColor(ImGuiCol.Text, GetEntryColor(entry.Kind));
                ImGui.TextWrapped(entry.Message);
                ImGui.PopStyleColor();

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(entry.TimestampLocal.ToString("yyyy-MM-dd HH:mm:ss.fff"));

                if (shouldAutoScroll && index == entries.Count - 1)
                    ImGui.SetScrollHereY(1f);
            }

            lastRenderedEntryCount = entries.Count;
            ImGui.EndTable();
        }

        ImGui.EndChild();
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

            if (ImGui.SmallButton($"{label}##timeline_preset_{preset}"))
                ApplyCombatTimelineMaxEntries(preset);

            if (isSelected)
                ImGui.PopStyleColor();
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

        foreach (var value in Enum.GetValues<TimelineCampFilter>())
        {
            var isSelected = value == currentValue;
            var optionLabel = value == TimelineCampFilter.All ? emptyLabel : GetCampFilterLabel(value);
            if (ImGui.Selectable(optionLabel, isSelected))
                currentValue = value;

            if (isSelected)
                ImGui.SetItemDefaultFocus();
        }

        ImGui.EndCombo();
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

        foreach (var value in Enum.GetValues<TimelineKindFilter>())
        {
            var isSelected = value == kindFilter;
            if (ImGui.Selectable(GetKindFilterLabel(value), isSelected))
                kindFilter = value;

            if (isSelected)
                ImGui.SetItemDefaultFocus();
        }

        ImGui.EndCombo();
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

        ImGui.EndCombo();
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

    private static IReadOnlyList<LocalStatsService.CombatTimelineEntry> FilterEntries(
        IReadOnlyList<LocalStatsService.CombatTimelineEntry> entries,
        string actorName,
        TimelineCampFilter actorCampFilter,
        string targetName,
        TimelineCampFilter targetCampFilter,
        TimelineKindFilter kindFilter,
        string actionText)
    {
        if (entries.Count == 0)
            return entries;

        var hasActorFilter = !string.IsNullOrWhiteSpace(actorName);
        var hasActorCampFilter = actorCampFilter != TimelineCampFilter.All;
        var hasTargetFilter = !string.IsNullOrWhiteSpace(targetName);
        var hasTargetCampFilter = targetCampFilter != TimelineCampFilter.All;
        var hasKindFilter = kindFilter != TimelineKindFilter.All;
        var hasActionFilter = !string.IsNullOrWhiteSpace(actionText);
        if (!hasActorFilter && !hasActorCampFilter && !hasTargetFilter && !hasTargetCampFilter && !hasKindFilter && !hasActionFilter)
            return entries;

        var filtered = new List<LocalStatsService.CombatTimelineEntry>(entries.Count);
        foreach (var entry in entries)
        {
            if (hasKindFilter && !MatchesKindFilter(entry, kindFilter))
                continue;

            if (hasActorFilter && !string.Equals(entry.ActorName, actorName, StringComparison.Ordinal))
                continue;

            if (hasActorCampFilter && !MatchesActorCampFilter(entry, actorCampFilter))
                continue;

            if (hasTargetFilter && !string.Equals(entry.TargetName, targetName, StringComparison.Ordinal))
                continue;

            if (hasTargetCampFilter && !MatchesTargetCampFilter(entry, targetCampFilter))
                continue;

            if (hasActionFilter && !string.Equals(entry.ActionText, actionText, StringComparison.Ordinal))
                continue;

            filtered.Add(entry);
        }

        return filtered;
    }

    private static IReadOnlyList<string> BuildDistinctNameOptions(
        IReadOnlyList<LocalStatsService.CombatTimelineEntry> entries,
        Func<LocalStatsService.CombatTimelineEntry, string?> selector,
        string currentValue)
    {
        var names = entries
            .Select(selector)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();

        if (!string.IsNullOrWhiteSpace(currentValue) && !names.Contains(currentValue, StringComparer.Ordinal))
            names.Insert(0, currentValue);

        return names;
    }

    private static IReadOnlyList<string> BuildActorOptions(
        IReadOnlyList<LocalStatsService.CombatTimelineEntry> entries,
        string currentValue,
        TimelineCampFilter actorCampFilter)
    {
        var names = entries
            .Where(entry => MatchesActorCampFilter(entry, actorCampFilter))
            .Select(static entry => entry.ActorName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();

        if (!string.IsNullOrWhiteSpace(currentValue) && !names.Contains(currentValue, StringComparer.Ordinal))
            names.Insert(0, currentValue);

        return names;
    }

    private static IReadOnlyList<string> BuildTargetOptions(
        IReadOnlyList<LocalStatsService.CombatTimelineEntry> entries,
        string currentValue,
        TimelineCampFilter targetCampFilter)
    {
        var names = entries
            .Where(entry => MatchesTargetCampFilter(entry, targetCampFilter))
            .Select(static entry => entry.TargetName)
            .Where(static name => !string.IsNullOrWhiteSpace(name))
            .Select(static name => name!.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToList();

        if (!string.IsNullOrWhiteSpace(currentValue) && !names.Contains(currentValue, StringComparer.Ordinal))
            names.Insert(0, currentValue);

        return names;
    }

    private static IReadOnlyList<string> BuildActionOptions(
        IReadOnlyList<LocalStatsService.CombatTimelineEntry> entries,
        string currentValue,
        string actorName,
        TimelineCampFilter actorCampFilter,
        string targetName,
        TimelineCampFilter targetCampFilter,
        TimelineKindFilter kindFilter,
        string searchText)
    {
        var names = FilterEntries(entries, actorName, actorCampFilter, targetName, targetCampFilter, kindFilter, string.Empty)
            .Select(static entry => entry.ActionText)
            .Where(static actionText => !string.IsNullOrWhiteSpace(actionText))
            .Select(static actionText => actionText!.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var trimmedSearchText = searchText.Trim();
            names = names
                .Where(actionText => actionText.IndexOf(trimmedSearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        names = names
            .OrderBy(static actionText => actionText, StringComparer.Ordinal)
            .ToList();

        if (!string.IsNullOrWhiteSpace(currentValue) && !names.Contains(currentValue, StringComparer.Ordinal))
            names.Insert(0, currentValue);

        return names;
    }

    private static bool MatchesTargetCampFilter(
        LocalStatsService.CombatTimelineEntry entry,
        TimelineCampFilter targetCampFilter)
    {
        if (targetCampFilter != TimelineCampFilter.All && string.IsNullOrWhiteSpace(entry.TargetName))
            return false;

        return targetCampFilter switch
        {
            TimelineCampFilter.All => true,
            TimelineCampFilter.Friendly => entry.TargetIsFriendly,
            TimelineCampFilter.Hostile => !entry.TargetIsFriendly,
            _ => true,
        };
    }

    private static bool MatchesActorCampFilter(
        LocalStatsService.CombatTimelineEntry entry,
        TimelineCampFilter actorCampFilter)
    {
        if (actorCampFilter != TimelineCampFilter.All && string.IsNullOrWhiteSpace(entry.ActorName))
            return false;

        return actorCampFilter switch
        {
            TimelineCampFilter.All => true,
            TimelineCampFilter.Friendly => entry.ActorIsFriendly,
            TimelineCampFilter.Hostile => !entry.ActorIsFriendly,
            _ => true,
        };
    }

    private static bool MatchesKindFilter(
        LocalStatsService.CombatTimelineEntry entry,
        TimelineKindFilter kindFilter)
    {
        return kindFilter switch
        {
            TimelineKindFilter.All => true,
            TimelineKindFilter.Damage => entry.Kind == LocalStatsService.CombatTimelineEntryKind.Damage,
            TimelineKindFilter.Heal => entry.Kind == LocalStatsService.CombatTimelineEntryKind.Heal,
            TimelineKindFilter.Failure => entry.Kind == LocalStatsService.CombatTimelineEntryKind.Failure,
            TimelineKindFilter.Death => entry.Kind == LocalStatsService.CombatTimelineEntryKind.Death,
            TimelineKindFilter.CombatBoundary => entry.Kind is LocalStatsService.CombatTimelineEntryKind.CombatStart or LocalStatsService.CombatTimelineEntryKind.CombatEnd,
            _ => true,
        };
    }

    private static string GetCampFilterLabel(TimelineCampFilter filter)
        => filter switch
        {
            TimelineCampFilter.Friendly => "友方",
            TimelineCampFilter.Hostile => "敌方",
            _ => "全部",
        };

    private static string GetKindFilterLabel(TimelineKindFilter filter)
        => filter switch
        {
            TimelineKindFilter.Damage => "伤害",
            TimelineKindFilter.Heal => "治疗",
            TimelineKindFilter.Failure => "未命中/抵抗",
            TimelineKindFilter.Death => "死亡",
            TimelineKindFilter.CombatBoundary => "进战/结算",
            _ => "全部",
        };

    private void ApplyQuickFilterPlayerOutput()
    {
        ClearAllFilters();
        actorCampFilter = TimelineCampFilter.Friendly;
        targetCampFilter = TimelineCampFilter.Hostile;
        kindFilter = TimelineKindFilter.Damage;

        var localPlayerName = DalamudApi.GetLocalPlayerName()?.Trim();
        if (!string.IsNullOrWhiteSpace(localPlayerName))
            actorFilter = localPlayerName;
    }

    private void ApplyQuickFilterEnemyHitFriendly()
    {
        ClearAllFilters();
        actorCampFilter = TimelineCampFilter.Hostile;
        targetCampFilter = TimelineCampFilter.Friendly;
        kindFilter = TimelineKindFilter.Damage;
    }

    private void ApplyQuickFilterKind(TimelineKindFilter filter)
    {
        ClearAllFilters();
        kindFilter = filter;
    }

    private void ClearAllFilters()
    {
        actorFilter = string.Empty;
        actionFilter = string.Empty;
        actionSearchText = string.Empty;
        targetFilter = string.Empty;
        actorCampFilter = TimelineCampFilter.All;
        targetCampFilter = TimelineCampFilter.All;
        kindFilter = TimelineKindFilter.All;
    }

    private bool HasAnyActiveFilter()
        => !string.IsNullOrWhiteSpace(actorFilter)
           || !string.IsNullOrWhiteSpace(actionFilter)
           || !string.IsNullOrWhiteSpace(targetFilter)
           || actorCampFilter != TimelineCampFilter.All
           || targetCampFilter != TimelineCampFilter.All
           || kindFilter != TimelineKindFilter.All;
}
