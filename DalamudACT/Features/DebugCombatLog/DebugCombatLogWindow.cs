using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DalamudACT;

/// <summary>
/// Debug 战斗记录悬浮窗，用于观察 Boss 技能 / Buff / 读条、我方 debuff 与机制标记等原始线索。
/// </summary>
internal sealed class DebugCombatLogWindow : Window
{
    private enum DebugKindFilter
    {
        All,
        BossAutoAttack,
        BossBuff,
        BossAction,
        BossCast,
        FriendlyAction,
        FriendlyBuff,
        FriendlyMarker,
        FriendlyDebuff,
        Recorder,
    }

    private const ImGuiTableFlags TableFlags =
        ImGuiTableFlags.RowBg
        | ImGuiTableFlags.BordersInnerH
        | ImGuiTableFlags.Resizable
        | ImGuiTableFlags.SizingFixedFit
        | ImGuiTableFlags.ScrollY
        | ImGuiTableFlags.NoSavedSettings;

    private static readonly TimeSpan InlineFeedbackDuration = TimeSpan.FromSeconds(2.4);
    private const int DebugRecordToggleCount = 9;
    private const int DebugCombatLogColumnCount = 6;
    private const string DebugCombatLogExportDirectoryName = "debug-combat-log-exports";
    private static readonly JsonSerializerOptions DebugCombatLogExportJsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly PluginConfiguration config;
    private readonly LocalStatsService statsService;
    private string actorFilter = string.Empty;
    private string targetFilter = string.Empty;
    private string idOrTextFilter = string.Empty;
    private string primarySearchText = string.Empty;
    private DebugKindFilter kindFilter;
    private bool autoScroll = true;
    private bool drawFaulted;
    private bool isResizingColumns;
    private int lastRenderedEntryCount = -1;
    private DateTime? lastInlineFeedbackAtUtc;
    private string inlineFeedbackText = "已复制";

    public DebugCombatLogWindow(PluginConfiguration config, LocalStatsService statsService)
        : base("debug战斗记录###DebugCombatLogWindow")
    {
        this.config = config;
        this.statsService = statsService;
        Size = new Vector2(980f, 620f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        try
        {
            BgAlpha = Math.Clamp(config.WindowOpacity, 0.2f, 1f);

            var entries = statsService.DebugCombatLogEntries;
            var filteredEntries = FilterEntries(entries, actorFilter, targetFilter, kindFilter, idOrTextFilter, primarySearchText);
            var actorOptions = BuildNameOptions(entries, static entry => NormalizeDebugCellText(entry.ActorName), actorFilter);
            var targetOptions = BuildNameOptions(entries, static entry => NormalizeDebugCellText(entry.TargetName), targetFilter);
            var primaryOptions = BuildPrimaryOptions(entries, primarySearchText, idOrTextFilter);

            ImGui.TextUnformatted("debug战斗记录");
            ImGui.SameLine();
            ImGui.TextDisabled(BuildCountSummary(entries.Count, filteredEntries.Count));
            ImGui.Separator();
            ImGui.TextWrapped("用于排查机制与统计问题：可记录 Boss / 小怪的平A、BUFF/debuff、技能、读条，以及友方的标记、技能、BUFF 和 debuff。");

            DrawRecordingControls();
            ImGui.Spacing();
            DrawCollapsibleControlPanel(entries, filteredEntries, actorOptions, targetOptions, primaryOptions);
            ImGui.Spacing();
            DrawTable(filteredEntries);
            drawFaulted = false;
        }
        catch (Exception ex)
        {
            if (!drawFaulted)
            {
                drawFaulted = true;
                LogHelper.Error("debug战斗记录", ex, "绘制 debug 战斗记录窗口失败，已自动关闭窗口以避免影响游戏。");
            }

            IsOpen = false;
        }
    }

    private void DrawCollapsibleControlPanel(
        IReadOnlyList<LocalStatsService.DebugCombatLogEntry> allEntries,
        IReadOnlyList<LocalStatsService.DebugCombatLogEntry> filteredEntries,
        IReadOnlyList<string> actorOptions,
        IReadOnlyList<string> targetOptions,
        IReadOnlyList<string> primaryOptions)
    {
        if (!ImGui.CollapsingHeader("记录项 / 操作 / 筛选 / 列显示###debug_combat_log_controls", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.TextDisabled("控制区已收起；点击标题可展开记录项、复制/导出、保留条数、列显示和筛选。表格记录仍会继续刷新。");
            return;
        }

        DrawCategoryControls();
        ImGui.Spacing();
        DrawToolbar(allEntries, filteredEntries);
        ImGui.Spacing();
        DrawRetentionControls();
        ImGui.Spacing();
        DrawColumnVisibilityControls();
        ImGui.Spacing();
        DrawFilterControls(actorOptions, targetOptions, primaryOptions);
    }

    private void DrawRecordingControls()
    {
        var recordingEnabled = config.DebugCombatRecordingEnabled;
        if (ImGui.Checkbox("开始记录", ref recordingEnabled))
            statsService.SetDebugCombatRecordingEnabled(recordingEnabled);

        ImGui.SameLine();
        ImGui.TextDisabled(recordingEnabled ? "记录中" : "已停止");

        ImGui.SameLine();
        ImGui.Checkbox("自动滚动", ref autoScroll);

        ImGui.SameLine();
        DrawHelpMarker("开启“开始记录”后才会写入新事件；关闭后会保留当前窗口里的已有记录，方便复制。插件每次加载时默认关闭。");
    }

    private void DrawCategoryControls()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled($"记录项：{BuildDebugRecordToggleSummary()}");
        ImGui.SameLine();
        DrawDebugRecordPresetButtons("debug_window");

        if (!ImGui.CollapsingHeader("展开记录项开关###debug_combat_record_options"))
        {
            ImGui.TextDisabled("详细开关已收起；常用操作可直接用右侧“全开 / 全关 / 默认”。");
            return;
        }

        DrawDebugRecordToggleGrid("debug_window");
    }

    private void DrawDebugRecordToggleGrid(string idPrefix)
    {
        const ImGuiTableFlags flags =
            ImGuiTableFlags.SizingStretchSame
            | ImGuiTableFlags.NoSavedSettings;

        if (!ImGui.BeginTable($"##{idPrefix}_debug_record_toggle_grid", 2, flags))
            return;

        ImGui.TableNextRow();

        ImGui.TableSetColumnIndex(0);
        DrawDebugRecordToggleGroup("Boss / 小怪", () =>
        {
            DrawConfigCheckbox($"平A##{idPrefix}_boss_auto", ref config.DebugRecordBossAutoAttack);
            DrawConfigCheckbox($"BUFF/debuff##{idPrefix}_boss_buff", ref config.DebugRecordBossBuff);
            DrawConfigCheckbox($"技能##{idPrefix}_boss_action", ref config.DebugRecordBossAction);
            DrawConfigCheckbox($"读条##{idPrefix}_boss_cast", ref config.DebugRecordBossCast);
            DrawConfigCheckbox($"小怪按 Boss##{idPrefix}_small_as_boss", ref config.DebugRecordSmallHostileNpcAsBoss);
        });

        ImGui.TableSetColumnIndex(1);
        DrawDebugRecordToggleGroup("友方", () =>
        {
            DrawFriendlyConfigCheckbox($"标记##{idPrefix}_friendly_marker", ref config.DebugRecordPartyMarker, ref config.DebugRecordSelfMarker);
            DrawFriendlyConfigCheckbox($"技能##{idPrefix}_friendly_action", ref config.DebugRecordPartyAction, ref config.DebugRecordSelfAction);
            DrawFriendlyConfigCheckbox($"BUFF##{idPrefix}_friendly_buff", ref config.DebugRecordPartyBuff, ref config.DebugRecordSelfBuff);
            DrawFriendlyConfigCheckbox($"debuff##{idPrefix}_friendly_debuff", ref config.DebugRecordPartyDebuff, ref config.DebugRecordSelfDebuff);
        });

        ImGui.EndTable();
    }

    private static void DrawDebugRecordToggleGroup(string title, Action drawContent)
    {
        ImGui.TextDisabled(title);
        drawContent();
    }

    private void DrawDebugRecordPresetButtons(string idPrefix)
    {
        if (ImGui.SmallButton($"全开##{idPrefix}_debug_record_all_on"))
            SetAllDebugRecordToggles(true);

        ImGui.SameLine();
        if (ImGui.SmallButton($"全关##{idPrefix}_debug_record_all_off"))
            SetAllDebugRecordToggles(false);

        ImGui.SameLine();
        if (ImGui.SmallButton($"默认##{idPrefix}_debug_record_default"))
            ResetDebugRecordTogglesToDefault();

        ImGui.SameLine();
        DrawHelpMarker("默认：除“小怪按 Boss”外，其他 debug 记录项全部开启。详细开关默认收起，避免窗口顶部过长。");
    }

    private void DrawToolbar(
        IReadOnlyList<LocalStatsService.DebugCombatLogEntry> allEntries,
        IReadOnlyList<LocalStatsService.DebugCombatLogEntry> filteredEntries)
    {
        var hasDisplayedEntries = filteredEntries.Count > 0;
        var hasAnyEntries = allEntries.Count > 0;

        ImGui.BeginDisabled(!hasDisplayedEntries);
        if (ImGui.Button("复制当前显示"))
        {
            ImGui.SetClipboardText(BuildDebugLogText(filteredEntries));
            ShowInlineFeedback("已复制");
        }

        ImGui.SameLine();
        if (ImGui.Button("导出当前显示"))
            ExportCurrentDebugCombatLog(allEntries.Count, filteredEntries);

        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.BeginDisabled(!hasAnyEntries);
        if (ImGui.Button("清空记录"))
        {
            statsService.ClearDebugCombatLog();
            ShowInlineFeedback("已清空");
        }
        ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.SmallButton("打开导出目录"))
            OpenDebugCombatLogExportDirectory();

        DrawInlineFeedback();
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
            var isSelected = config.DebugCombatLogMaxEntries == preset;
            if (isSelected)
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.28f, 0.46f, 0.82f, 0.95f));

            if (ImGui.SmallButton($"{label}##debug_combat_preset_{preset}"))
                ApplyDebugCombatLogMaxEntries(preset);

            if (isSelected)
                ImGui.PopStyleColor();
        }

        ImGui.SameLine();
        ImGui.TextDisabled($"当前：{(config.DebugCombatLogMaxEntries <= 0 ? "全部" : $"{config.DebugCombatLogMaxEntries} 条")}");
    }

    private void DrawColumnVisibilityControls()
    {
        config.EnsureAnyDebugCombatLogColumnVisible();

        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled($"表格列：{BuildColumnVisibilitySummary()}");
        ImGui.SameLine();
        if (ImGui.SmallButton("全部显示##debug_combat_columns_all_on"))
            SetAllDebugCombatLogColumnsVisible(true);

        ImGui.SameLine();
        if (ImGui.SmallButton("默认##debug_combat_columns_default"))
            SetAllDebugCombatLogColumnsVisible(true);

        ImGui.SameLine();
        DrawHelpMarker("隐藏列只影响表格显示，不影响筛选、复制和导出。鼠标悬停任意一条记录会显示完整内容，左键点击可复制该条完整记录。");

        if (!ImGui.CollapsingHeader("展开列显示开关##debug_combat_log_column_options"))
        {
            ImGui.TextDisabled("列显示开关已收起；需要缩窄窗口或专注查看某几列时再展开调整。");
            return;
        }

        DrawColumnVisibilityCheckbox("时间##debug_col_time", ref config.DebugCombatLogShowTimeColumn);
        ImGui.SameLine();
        DrawColumnVisibilityCheckbox("类型##debug_col_kind", ref config.DebugCombatLogShowKindColumn);
        ImGui.SameLine();
        DrawColumnVisibilityCheckbox("角色##debug_col_actor", ref config.DebugCombatLogShowActorColumn);
        ImGui.SameLine();
        DrawColumnVisibilityCheckbox("目标##debug_col_target", ref config.DebugCombatLogShowTargetColumn);
        ImGui.SameLine();
        DrawColumnVisibilityCheckbox("ID/技能##debug_col_primary", ref config.DebugCombatLogShowPrimaryColumn);
        ImGui.SameLine();
        DrawColumnVisibilityCheckbox("内容##debug_col_message", ref config.DebugCombatLogShowMessageColumn);
    }

    private string BuildColumnVisibilitySummary()
    {
        var visibleCount = CountVisibleDebugCombatLogColumns();
        return visibleCount == DebugCombatLogColumnCount ? "全部显示" : $"已显示 {visibleCount}/{DebugCombatLogColumnCount}";
    }

    private void DrawColumnVisibilityCheckbox(string label, ref bool value)
    {
        var current = value;
        if (!ImGui.Checkbox(label, ref current))
            return;

        value = current;
        config.EnsureAnyDebugCombatLogColumnVisible();
        config.Save();
    }

    private void SetAllDebugCombatLogColumnsVisible(bool visible)
    {
        config.DebugCombatLogShowTimeColumn = visible;
        config.DebugCombatLogShowKindColumn = visible;
        config.DebugCombatLogShowActorColumn = visible;
        config.DebugCombatLogShowTargetColumn = visible;
        config.DebugCombatLogShowPrimaryColumn = visible;
        config.DebugCombatLogShowMessageColumn = visible;
        config.EnsureAnyDebugCombatLogColumnVisible();
        config.Save();
    }

    private void DrawFilterControls(
        IReadOnlyList<string> actorOptions,
        IReadOnlyList<string> targetOptions,
        IReadOnlyList<string> primaryOptions)
    {
        DrawKindFilterCombo();
        ImGui.SameLine();
        DrawFilterCombo("角色：", "debug_actor_filter", ref actorFilter, actorOptions, "全部");

        DrawFilterCombo("目标：", "debug_target_filter", ref targetFilter, targetOptions, "全部");
        ImGui.SameLine();
        DrawFilterCombo("技能/状态/标记：", "debug_primary_filter", ref idOrTextFilter, primaryOptions, "全部");
        ImGui.SameLine();
        DrawSearchInput("搜索：", "debug_primary_search", ref primarySearchText);

        ImGui.BeginDisabled(!HasAnyActiveFilter());
        if (ImGui.SmallButton("清空筛选"))
            ClearAllFilters();
        ImGui.EndDisabled();
    }

    private void DrawTable(IReadOnlyList<LocalStatsService.DebugCombatLogEntry> entries)
    {
        ImGui.BeginChild("##debug_combat_log_container", new Vector2(0f, 0f), true);

        if (entries.Count == 0)
        {
            lastRenderedEntryCount = 0;
            ImGui.TextDisabled(!HasAnyActiveFilter()
                ? "暂无 debug 战斗记录。开启“开始记录”后进入战斗，或等待当前战斗中出现新事件。"
                : "当前筛选条件下暂无 debug 战斗记录。");
            ImGui.EndChild();
            return;
        }

        config.EnsureAnyDebugCombatLogColumnVisible();
        if (ImGui.BeginTable("##debug_combat_log_table", CountVisibleDebugCombatLogColumns(), TableFlags))
        {
            SetupDebugCombatLogColumns();
            ImGui.TableHeadersRow();

            var shouldAutoScroll = autoScroll && entries.Count != lastRenderedEntryCount;

            for (var index = 0; index < entries.Count; index++)
            {
                var entry = entries[index];
                ImGui.TableNextRow();

                var columnIndex = 0;
                var rowHovered = false;

                if (config.DebugCombatLogShowTimeColumn)
                {
                    ImGui.TableSetColumnIndex(columnIndex++);
                    ImGui.TextDisabled(entry.TimestampLocal.ToString("HH:mm:ss"));
                    rowHovered |= ImGui.IsItemHovered();
                }

                if (config.DebugCombatLogShowKindColumn)
                {
                    ImGui.TableSetColumnIndex(columnIndex++);
                    ImGui.PushStyleColor(ImGuiCol.Text, GetKindColor(entry.Kind));
                    ImGui.TextUnformatted(GetKindLabel(entry.Kind));
                    rowHovered |= ImGui.IsItemHovered();
                    ImGui.PopStyleColor();
                }

                if (config.DebugCombatLogShowActorColumn)
                {
                    ImGui.TableSetColumnIndex(columnIndex++);
                    ImGui.TextUnformatted(FormatDebugCellValue(entry.ActorName));
                    rowHovered |= ImGui.IsItemHovered();
                }

                if (config.DebugCombatLogShowTargetColumn)
                {
                    ImGui.TableSetColumnIndex(columnIndex++);
                    ImGui.TextUnformatted(FormatDebugCellValue(entry.TargetName));
                    rowHovered |= ImGui.IsItemHovered();
                }

                if (config.DebugCombatLogShowPrimaryColumn)
                {
                    ImGui.TableSetColumnIndex(columnIndex++);
                    ImGui.TextUnformatted(BuildPrimaryDisplay(entry));
                    rowHovered |= ImGui.IsItemHovered();
                }

                if (config.DebugCombatLogShowMessageColumn)
                {
                    ImGui.TableSetColumnIndex(columnIndex++);
                    ImGui.PushStyleColor(ImGuiCol.Text, GetKindColor(entry.Kind));
                    ImGui.TextWrapped(entry.Message);
                    rowHovered |= ImGui.IsItemHovered();
                    ImGui.PopStyleColor();
                }

                if (rowHovered)
                    DrawDebugLogEntryInteraction(entry);

                if (shouldAutoScroll && index == entries.Count - 1)
                    ImGui.SetScrollHereY(1f);
            }

            lastRenderedEntryCount = entries.Count;
            PersistDebugCombatLogColumnWidths();
            ImGui.EndTable();
        }

        ImGui.EndChild();
    }

    private void DrawConfigCheckbox(string label, ref bool value)
    {
        var current = value;
        if (ImGui.Checkbox(label, ref current))
        {
            value = current;
            config.Save();
        }
    }

    private void DrawFriendlyConfigCheckbox(string label, ref bool partyValue, ref bool selfValue)
    {
        var current = partyValue || selfValue;
        if (!ImGui.Checkbox(label, ref current))
            return;

        partyValue = current;
        selfValue = current;
        config.Save();
    }

    private void PersistDebugCombatLogColumnWidths()
    {
        var visibleColumnCount = CountVisibleDebugCombatLogColumns();
        var hoveringResizableColumn = false;
        for (var columnIndex = 0; columnIndex < visibleColumnCount; columnIndex++)
            hoveringResizableColumn |= IsTableColumnHovered(columnIndex);

        if (ImGui.IsMouseDown(ImGuiMouseButton.Left)
            && hoveringResizableColumn
            && ImGui.GetMouseCursor() == ImGuiMouseCursor.ResizeEw)
        {
            isResizingColumns = true;
        }

        if (!ImGui.IsMouseReleased(ImGuiMouseButton.Left) || !isResizingColumns)
            return;

        var changed = false;
        var storedColumnIndex = 0;
        if (config.DebugCombatLogShowTimeColumn)
            changed |= TryUpdateStoredColumnWidth(ref config.DebugCombatLogTimeColumnWidth, storedColumnIndex++, 48f);

        if (config.DebugCombatLogShowKindColumn)
            changed |= TryUpdateStoredColumnWidth(ref config.DebugCombatLogKindColumnWidth, storedColumnIndex++, 48f);

        if (config.DebugCombatLogShowActorColumn)
            changed |= TryUpdateStoredColumnWidth(ref config.DebugCombatLogActorColumnWidth, storedColumnIndex++, 48f);

        if (config.DebugCombatLogShowTargetColumn)
            changed |= TryUpdateStoredColumnWidth(ref config.DebugCombatLogTargetColumnWidth, storedColumnIndex++, 48f);

        if (config.DebugCombatLogShowPrimaryColumn)
            changed |= TryUpdateStoredColumnWidth(ref config.DebugCombatLogPrimaryColumnWidth, storedColumnIndex++, 48f);

        isResizingColumns = false;

        if (changed)
            config.Save();
    }

    private static bool IsTableColumnHovered(int columnIndex)
        => (ImGui.TableGetColumnFlags(columnIndex) & ImGuiTableColumnFlags.IsHovered) != 0;

    private static bool TryUpdateStoredColumnWidth(ref float storedWidth, int columnIndex, float minimumWidth)
    {
        var currentWidth = Math.Max(ImGui.GetColumnWidth(columnIndex), minimumWidth);
        if (currentWidth <= 0f)
            return false;

        if (Math.Abs(storedWidth - currentWidth) <= 0.5f)
            return false;

        storedWidth = currentWidth;
        return true;
    }

    private static float ResolveDebugColumnWidth(float savedWidth, float fallbackWidth)
        => Math.Clamp(savedWidth <= 0f ? fallbackWidth : savedWidth, 48f, 2000f);

    private int CountVisibleDebugCombatLogColumns()
    {
        var count = 0;
        if (config.DebugCombatLogShowTimeColumn) count++;
        if (config.DebugCombatLogShowKindColumn) count++;
        if (config.DebugCombatLogShowActorColumn) count++;
        if (config.DebugCombatLogShowTargetColumn) count++;
        if (config.DebugCombatLogShowPrimaryColumn) count++;
        if (config.DebugCombatLogShowMessageColumn) count++;
        return Math.Max(count, 1);
    }

    private void SetupDebugCombatLogColumns()
    {
        if (config.DebugCombatLogShowTimeColumn)
            ImGui.TableSetupColumn("时间", ImGuiTableColumnFlags.WidthFixed, ResolveDebugColumnWidth(config.DebugCombatLogTimeColumnWidth, 86f));

        if (config.DebugCombatLogShowKindColumn)
            ImGui.TableSetupColumn("类型", ImGuiTableColumnFlags.WidthFixed, ResolveDebugColumnWidth(config.DebugCombatLogKindColumnWidth, 100f));

        if (config.DebugCombatLogShowActorColumn)
            ImGui.TableSetupColumn("角色", ImGuiTableColumnFlags.WidthFixed, ResolveDebugColumnWidth(config.DebugCombatLogActorColumnWidth, 140f));

        if (config.DebugCombatLogShowTargetColumn)
            ImGui.TableSetupColumn("目标", ImGuiTableColumnFlags.WidthFixed, ResolveDebugColumnWidth(config.DebugCombatLogTargetColumnWidth, 140f));

        if (config.DebugCombatLogShowPrimaryColumn)
            ImGui.TableSetupColumn("ID/技能", ImGuiTableColumnFlags.WidthFixed, ResolveDebugColumnWidth(config.DebugCombatLogPrimaryColumnWidth, 160f));

        if (config.DebugCombatLogShowMessageColumn)
            ImGui.TableSetupColumn("内容", ImGuiTableColumnFlags.WidthStretch, 1f);
    }

    private string BuildDebugRecordToggleSummary()
    {
        var enabledCount = CountEnabledDebugRecordToggles();
        return enabledCount switch
        {
            0 => "全部关闭",
            DebugRecordToggleCount => "全部开启",
            _ => $"已开 {enabledCount}/{DebugRecordToggleCount}",
        };
    }

    private int CountEnabledDebugRecordToggles()
    {
        var count = 0;
        if (config.DebugRecordBossAutoAttack) count++;
        if (config.DebugRecordBossBuff) count++;
        if (config.DebugRecordBossAction) count++;
        if (config.DebugRecordBossCast) count++;
        if (config.DebugRecordSmallHostileNpcAsBoss) count++;
        if (config.DebugRecordPartyMarker || config.DebugRecordSelfMarker) count++;
        if (config.DebugRecordPartyAction || config.DebugRecordSelfAction) count++;
        if (config.DebugRecordPartyBuff || config.DebugRecordSelfBuff) count++;
        if (config.DebugRecordPartyDebuff || config.DebugRecordSelfDebuff) count++;
        return count;
    }

    private void SetAllDebugRecordToggles(bool enabled)
    {
        config.DebugRecordBossAutoAttack = enabled;
        config.DebugRecordBossBuff = enabled;
        config.DebugRecordBossAction = enabled;
        config.DebugRecordBossCast = enabled;
        config.DebugRecordSmallHostileNpcAsBoss = enabled;
        config.DebugRecordPartyMarker = enabled;
        config.DebugRecordPartyAction = enabled;
        config.DebugRecordPartyBuff = enabled;
        config.DebugRecordPartyDebuff = enabled;
        config.DebugRecordSelfMarker = enabled;
        config.DebugRecordSelfAction = enabled;
        config.DebugRecordSelfBuff = enabled;
        config.DebugRecordSelfDebuff = enabled;
        config.Save();
    }

    private void ResetDebugRecordTogglesToDefault()
    {
        config.DebugRecordBossAutoAttack = true;
        config.DebugRecordBossBuff = true;
        config.DebugRecordBossAction = true;
        config.DebugRecordBossCast = true;
        config.DebugRecordSmallHostileNpcAsBoss = false;
        config.DebugRecordPartyMarker = true;
        config.DebugRecordPartyAction = true;
        config.DebugRecordPartyBuff = true;
        config.DebugRecordPartyDebuff = true;
        config.DebugRecordSelfMarker = true;
        config.DebugRecordSelfAction = true;
        config.DebugRecordSelfBuff = true;
        config.DebugRecordSelfDebuff = true;
        config.Save();
    }

    private void DrawKindFilterCombo()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled("类型：");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(130f);
        if (!ImGui.BeginCombo("##debug_kind_filter", GetKindFilterLabel(kindFilter)))
            return;

        foreach (var value in Enum.GetValues<DebugKindFilter>())
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

    private static IReadOnlyList<LocalStatsService.DebugCombatLogEntry> FilterEntries(
        IReadOnlyList<LocalStatsService.DebugCombatLogEntry> entries,
        string actorName,
        string targetName,
        DebugKindFilter kindFilter,
        string idOrTextFilter,
        string searchText)
    {
        if (entries.Count == 0)
            return entries;

        var hasActorFilter = !string.IsNullOrWhiteSpace(actorName);
        var hasTargetFilter = !string.IsNullOrWhiteSpace(targetName);
        var hasKindFilter = kindFilter != DebugKindFilter.All;
        var hasPrimaryFilter = !string.IsNullOrWhiteSpace(idOrTextFilter);
        var hasSearchFilter = !string.IsNullOrWhiteSpace(searchText);
        if (!hasActorFilter && !hasTargetFilter && !hasKindFilter && !hasPrimaryFilter && !hasSearchFilter)
            return entries;

        var filtered = new List<LocalStatsService.DebugCombatLogEntry>(entries.Count);
        foreach (var entry in entries)
        {
            if (hasActorFilter && !string.Equals(NormalizeDebugCellText(entry.ActorName), actorName, StringComparison.Ordinal))
                continue;

            if (hasTargetFilter && !string.Equals(NormalizeDebugCellText(entry.TargetName), targetName, StringComparison.Ordinal))
                continue;

            if (hasKindFilter && !MatchesKindFilter(entry.Kind, kindFilter))
                continue;

            if (hasPrimaryFilter && !MatchesPrimaryFilter(entry, idOrTextFilter))
                continue;

            if (hasSearchFilter && !MatchesSearchFilter(entry, searchText))
                continue;

            filtered.Add(entry);
        }

        return filtered;
    }

    private static IReadOnlyList<string> BuildNameOptions(
        IReadOnlyList<LocalStatsService.DebugCombatLogEntry> entries,
        Func<LocalStatsService.DebugCombatLogEntry, string?> selector,
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

    private static IReadOnlyList<string> BuildPrimaryOptions(
        IReadOnlyList<LocalStatsService.DebugCombatLogEntry> entries,
        string searchText,
        string currentValue)
    {
        var values = entries
            .Select(BuildPrimaryDisplay)
            .Where(static text => !string.IsNullOrWhiteSpace(text) && text != "--")
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var trimmedSearchText = searchText.Trim();
            values = values
                .Where(text => text.IndexOf(trimmedSearchText, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
        }

        values = values
            .OrderBy(static text => text, StringComparer.Ordinal)
            .ToList();

        if (!string.IsNullOrWhiteSpace(currentValue) && !values.Contains(currentValue, StringComparer.Ordinal))
            values.Insert(0, currentValue);

        return values;
    }

    private static bool MatchesPrimaryFilter(LocalStatsService.DebugCombatLogEntry entry, string filterText)
    {
        var primary = BuildPrimaryDisplay(entry);
        return string.Equals(primary, filterText, StringComparison.Ordinal)
               || (!string.IsNullOrWhiteSpace(entry.PrimaryText)
                   && string.Equals(entry.PrimaryText, filterText, StringComparison.Ordinal));
    }

    private static bool MatchesSearchFilter(LocalStatsService.DebugCombatLogEntry entry, string searchText)
    {
        var terms = searchText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (terms.Length == 0)
            return true;

        return terms.All(term =>
            ContainsSearchTerm(GetKindLabel(entry.Kind), term)
            || ContainsSearchTerm(NormalizeDebugCellText(entry.ActorName), term)
            || ContainsSearchTerm(NormalizeDebugCellText(entry.TargetName), term)
            || ContainsSearchTerm(BuildPrimaryDisplay(entry), term)
            || ContainsSearchTerm(entry.PrimaryId == 0 ? string.Empty : entry.PrimaryId.ToString(), term)
            || ContainsSearchTerm(entry.Message, term)
            || ContainsSearchTerm(entry.TimestampLocal.ToString("HH:mm:ss"), term));
    }

    private static bool ContainsSearchTerm(string? text, string term)
        => !string.IsNullOrWhiteSpace(text)
           && text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool MatchesKindFilter(LocalStatsService.DebugCombatLogEntryKind kind, DebugKindFilter filter)
        => filter switch
        {
            DebugKindFilter.BossAutoAttack => kind == LocalStatsService.DebugCombatLogEntryKind.BossAutoAttack,
            DebugKindFilter.BossBuff => kind == LocalStatsService.DebugCombatLogEntryKind.BossBuff,
            DebugKindFilter.BossAction => kind == LocalStatsService.DebugCombatLogEntryKind.BossAction,
            DebugKindFilter.BossCast => kind == LocalStatsService.DebugCombatLogEntryKind.BossCast,
            DebugKindFilter.FriendlyAction => kind is LocalStatsService.DebugCombatLogEntryKind.PartyAction or LocalStatsService.DebugCombatLogEntryKind.SelfAction,
            DebugKindFilter.FriendlyBuff => kind is LocalStatsService.DebugCombatLogEntryKind.PartyBuff or LocalStatsService.DebugCombatLogEntryKind.SelfBuff,
            DebugKindFilter.FriendlyMarker => kind is LocalStatsService.DebugCombatLogEntryKind.PartyMarker or LocalStatsService.DebugCombatLogEntryKind.SelfMarker,
            DebugKindFilter.FriendlyDebuff => kind is LocalStatsService.DebugCombatLogEntryKind.PartyDebuff or LocalStatsService.DebugCombatLogEntryKind.SelfDebuff,
            DebugKindFilter.Recorder => kind == LocalStatsService.DebugCombatLogEntryKind.Recorder,
            _ => true,
        };

    private static string BuildDebugLogText(IReadOnlyList<LocalStatsService.DebugCombatLogEntry> entries)
    {
        if (entries.Count == 0)
            return "暂无 debug 战斗记录。";

        var builder = new StringBuilder(entries.Count * 72);
        foreach (var entry in entries)
            builder.AppendLine(BuildDebugLogEntryText(entry));

        return builder.ToString().TrimEnd();
    }

    private void ExportCurrentDebugCombatLog(
        int totalEntryCount,
        IReadOnlyList<LocalStatsService.DebugCombatLogEntry> entries)
    {
        try
        {
            if (entries.Count == 0)
            {
                ShowInlineFeedback("没有可导出的记录");
                return;
            }

            var exportDirectory = GetDebugCombatLogExportDirectory();
            Directory.CreateDirectory(exportDirectory);

            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            var exportPath = Path.Combine(exportDirectory, $"debug-combat-log-{timestamp}.json");
            var payload = BuildDebugCombatLogExportPayload(totalEntryCount, entries);
            var json = JsonSerializer.Serialize(payload, DebugCombatLogExportJsonOptions);
            File.WriteAllText(exportPath, json, Encoding.UTF8);

            ShowInlineFeedback($"已导出 {entries.Count} 条：{exportPath}");
            LogHelper.PrintWithModule("debug战斗记录", "导出", $"已导出 {entries.Count} 条记录到 {exportPath}");
        }
        catch (Exception ex)
        {
            LogHelper.Error("debug战斗记录", ex, "导出 debug 战斗记录失败。");
            ShowInlineFeedback($"导出失败：{ex.Message}");
        }
    }

    private DebugCombatLogExportPayload BuildDebugCombatLogExportPayload(
        int totalEntryCount,
        IReadOnlyList<LocalStatsService.DebugCombatLogEntry> entries)
        => new()
        {
            Version = 1,
            ExportMode = "当前显示",
            ExportedAtLocal = DateTime.Now,
            ExportedAtUtc = DateTime.UtcNow,
            TotalEntryCount = totalEntryCount,
            ExportedEntryCount = entries.Count,
            Filters = new DebugCombatLogExportFilters
            {
                Kind = GetKindFilterLabel(kindFilter),
                KindValue = kindFilter.ToString(),
                ActorName = actorFilter,
                TargetName = targetFilter,
                Primary = idOrTextFilter,
                PrimarySearchText = primarySearchText,
            },
            Records = entries.Select(CreateDebugCombatLogExportRecord).ToList(),
        };

    private static DebugCombatLogExportRecord CreateDebugCombatLogExportRecord(LocalStatsService.DebugCombatLogEntry entry)
        => new()
        {
            TimestampLocal = entry.TimestampLocal,
            TimestampText = entry.TimestampLocal.ToString("yyyy-MM-dd HH:mm:ss.fff"),
            Kind = GetKindLabel(entry.Kind),
            KindValue = entry.Kind.ToString(),
            ActorName = entry.ActorName ?? string.Empty,
            TargetName = entry.TargetName ?? string.Empty,
            PrimaryId = entry.PrimaryId,
            PrimaryIdHex = entry.PrimaryId == 0 ? string.Empty : $"0x{entry.PrimaryId:X}",
            PrimaryText = entry.PrimaryText ?? string.Empty,
            PrimaryDisplay = BuildPrimaryDisplay(entry),
            Message = entry.Message,
        };

    private void OpenDebugCombatLogExportDirectory()
    {
        try
        {
            var exportDirectory = GetDebugCombatLogExportDirectory();
            Directory.CreateDirectory(exportDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = exportDirectory,
                UseShellExecute = true,
                Verb = "open",
            });

            ShowInlineFeedback($"已打开导出目录：{exportDirectory}");
        }
        catch (Exception ex)
        {
            LogHelper.Warning("debug战斗记录", ex, "打开 debug 战斗记录导出目录失败。");
            ShowInlineFeedback($"打开目录失败：{ex.Message}");
        }
    }

    private static string GetDebugCombatLogExportDirectory()
    {
        var configDirectory = DalamudApi.PluginInterface.GetPluginConfigDirectory();
        return Path.Combine(configDirectory, DebugCombatLogExportDirectoryName);
    }

    private void ApplyDebugCombatLogMaxEntries(int value)
    {
        var normalized = value <= 0 ? 0 : Math.Clamp(value, 100, 50000);
        if (config.DebugCombatLogMaxEntries == normalized)
            return;

        config.DebugCombatLogMaxEntries = normalized;
        statsService.ApplyDebugCombatLogRetentionLimit();
        config.Save();
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

    private void DrawDebugLogEntryInteraction(LocalStatsService.DebugCombatLogEntry entry)
    {
        if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            ImGui.SetClipboardText(BuildDebugLogEntryText(entry));
            ShowInlineFeedback("已复制该条记录");
        }

        ImGui.BeginTooltip();
        ImGui.TextDisabled("左键点击该记录可复制完整内容。");
        ImGui.Separator();
        ImGui.TextUnformatted($"时间：{entry.TimestampLocal:yyyy-MM-dd HH:mm:ss.fff}");
        ImGui.TextUnformatted($"类型：{GetKindLabel(entry.Kind)}");
        ImGui.TextUnformatted($"角色：{FormatDebugCellValue(entry.ActorName)}");
        ImGui.TextUnformatted($"目标：{FormatDebugCellValue(entry.TargetName)}");
        ImGui.TextUnformatted($"ID/技能：{BuildPrimaryDisplay(entry)}");
        ImGui.Separator();
        ImGui.PushTextWrapPos(Math.Min(ImGui.GetFontSize() * 42f, 860f));
        ImGui.TextUnformatted(entry.Message);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    private static string BuildPrimaryDisplay(LocalStatsService.DebugCombatLogEntry entry)
    {
        var primaryText = entry.PrimaryText?.Trim();
        if (!string.IsNullOrWhiteSpace(primaryText))
        {
            if (entry.PrimaryId != 0)
            {
                var slashHexIndex = primaryText.LastIndexOf("/0x", StringComparison.OrdinalIgnoreCase);
                if (slashHexIndex >= 0)
                    return $"{primaryText[..slashHexIndex]}[{entry.PrimaryId}]";

                if (primaryText.StartsWith("标记 ", StringComparison.Ordinal)
                    || primaryText.StartsWith("标记0x", StringComparison.OrdinalIgnoreCase))
                {
                    return $"标记[{entry.PrimaryId}]";
                }
            }

            return primaryText;
        }

        return entry.PrimaryId == 0 ? "--" : entry.PrimaryId.ToString();
    }

    private static string BuildDebugLogEntryText(LocalStatsService.DebugCombatLogEntry entry)
        => $"{entry.TimestampLocal:yyyy-MM-dd HH:mm:ss.fff}\t{GetKindLabel(entry.Kind)}\t角色={FormatDebugCellValue(entry.ActorName)}\t目标={FormatDebugCellValue(entry.TargetName)}\tID/技能={BuildPrimaryDisplay(entry)}\t{entry.Message}";

    private static string FormatDebugCellValue(string? value)
    {
        var text = NormalizeDebugCellText(value);
        return string.IsNullOrWhiteSpace(text) ? "--" : text;
    }

    private static string NormalizeDebugCellText(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.StartsWith("，目标 ", StringComparison.Ordinal))
            text = text["，目标 ".Length..].Trim();

        if (text.StartsWith("目标 ", StringComparison.Ordinal))
            text = text["目标 ".Length..].Trim();

        return text;
    }

    private static string BuildCountSummary(int totalCount, int filteredCount)
        => totalCount == filteredCount ? $"共 {totalCount} 条" : $"共 {totalCount} 条，当前显示 {filteredCount} 条";

    private static string FormatEntryCountPreset(int value)
        => value >= 10000 ? $"{value / 10000d:0.#}万" : value.ToString();

    private bool HasAnyActiveFilter()
        => !string.IsNullOrWhiteSpace(actorFilter)
           || !string.IsNullOrWhiteSpace(targetFilter)
           || !string.IsNullOrWhiteSpace(idOrTextFilter)
           || !string.IsNullOrWhiteSpace(primarySearchText)
           || kindFilter != DebugKindFilter.All;

    private void ClearAllFilters()
    {
        actorFilter = string.Empty;
        targetFilter = string.Empty;
        idOrTextFilter = string.Empty;
        primarySearchText = string.Empty;
        kindFilter = DebugKindFilter.All;
    }

    private static string GetKindFilterLabel(DebugKindFilter filter)
        => filter switch
        {
            DebugKindFilter.BossAutoAttack => "Boss平A",
            DebugKindFilter.BossBuff => "Boss BUFF/debuff",
            DebugKindFilter.BossAction => "Boss技能",
            DebugKindFilter.BossCast => "Boss读条",
            DebugKindFilter.FriendlyAction => "友方技能",
            DebugKindFilter.FriendlyBuff => "友方BUFF",
            DebugKindFilter.FriendlyMarker => "友方标记",
            DebugKindFilter.FriendlyDebuff => "友方debuff",
            DebugKindFilter.Recorder => "记录器",
            _ => "全部",
        };

    private static string GetKindLabel(LocalStatsService.DebugCombatLogEntryKind kind)
        => kind switch
        {
            LocalStatsService.DebugCombatLogEntryKind.BossAutoAttack => "Boss平A",
            LocalStatsService.DebugCombatLogEntryKind.BossBuff => "Boss BUFF/debuff",
            LocalStatsService.DebugCombatLogEntryKind.BossAction => "Boss技能",
            LocalStatsService.DebugCombatLogEntryKind.BossCast => "Boss读条",
            LocalStatsService.DebugCombatLogEntryKind.PartyAction or LocalStatsService.DebugCombatLogEntryKind.SelfAction => "友方技能",
            LocalStatsService.DebugCombatLogEntryKind.PartyBuff or LocalStatsService.DebugCombatLogEntryKind.SelfBuff => "友方BUFF",
            LocalStatsService.DebugCombatLogEntryKind.PartyMarker or LocalStatsService.DebugCombatLogEntryKind.SelfMarker => "友方标记",
            LocalStatsService.DebugCombatLogEntryKind.PartyDebuff or LocalStatsService.DebugCombatLogEntryKind.SelfDebuff => "友方debuff",
            _ => "记录器",
        };

    private static Vector4 GetKindColor(LocalStatsService.DebugCombatLogEntryKind kind)
        => kind switch
        {
            LocalStatsService.DebugCombatLogEntryKind.BossAutoAttack => new Vector4(1.00f, 0.72f, 0.52f, 1f),
            LocalStatsService.DebugCombatLogEntryKind.BossBuff => new Vector4(0.72f, 0.90f, 1.00f, 1f),
            LocalStatsService.DebugCombatLogEntryKind.BossAction => new Vector4(1.00f, 0.58f, 0.58f, 1f),
            LocalStatsService.DebugCombatLogEntryKind.BossCast => new Vector4(1.00f, 0.84f, 0.42f, 1f),
            LocalStatsService.DebugCombatLogEntryKind.PartyAction => new Vector4(0.74f, 0.86f, 1.00f, 1f),
            LocalStatsService.DebugCombatLogEntryKind.SelfAction => new Vector4(0.64f, 0.82f, 1.00f, 1f),
            LocalStatsService.DebugCombatLogEntryKind.PartyBuff => new Vector4(0.62f, 0.96f, 0.76f, 1f),
            LocalStatsService.DebugCombatLogEntryKind.PartyMarker => new Vector4(0.78f, 0.70f, 1.00f, 1f),
            LocalStatsService.DebugCombatLogEntryKind.PartyDebuff => new Vector4(1.00f, 0.64f, 0.86f, 1f),
            LocalStatsService.DebugCombatLogEntryKind.SelfBuff => new Vector4(0.52f, 0.90f, 1.00f, 1f),
            LocalStatsService.DebugCombatLogEntryKind.SelfMarker => new Vector4(0.64f, 0.86f, 1.00f, 1f),
            LocalStatsService.DebugCombatLogEntryKind.SelfDebuff => new Vector4(1.00f, 0.48f, 0.72f, 1f),
            _ => new Vector4(0.76f, 0.80f, 0.88f, 1f),
        };

    private static void DrawHelpMarker(string tooltip)
    {
        ImGui.TextDisabled("(?)");
        if (!ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(Math.Min(ImGui.GetFontSize() * 28f, 580f));
        ImGui.TextUnformatted(tooltip);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    private sealed class DebugCombatLogExportPayload
    {
        public int Version { get; set; }

        public string ExportMode { get; set; } = string.Empty;

        public DateTime ExportedAtLocal { get; set; }

        public DateTime ExportedAtUtc { get; set; }

        public int TotalEntryCount { get; set; }

        public int ExportedEntryCount { get; set; }

        public DebugCombatLogExportFilters Filters { get; set; } = new();

        public List<DebugCombatLogExportRecord> Records { get; set; } = new();
    }

    private sealed class DebugCombatLogExportFilters
    {
        public string Kind { get; set; } = string.Empty;

        public string KindValue { get; set; } = string.Empty;

        public string ActorName { get; set; } = string.Empty;

        public string TargetName { get; set; } = string.Empty;

        public string Primary { get; set; } = string.Empty;

        public string PrimarySearchText { get; set; } = string.Empty;
    }

    private sealed class DebugCombatLogExportRecord
    {
        public DateTime TimestampLocal { get; set; }

        public string TimestampText { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string KindValue { get; set; } = string.Empty;

        public string ActorName { get; set; } = string.Empty;

        public string TargetName { get; set; } = string.Empty;

        public uint PrimaryId { get; set; }

        public string PrimaryIdHex { get; set; } = string.Empty;

        public string PrimaryText { get; set; } = string.Empty;

        public string PrimaryDisplay { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}
