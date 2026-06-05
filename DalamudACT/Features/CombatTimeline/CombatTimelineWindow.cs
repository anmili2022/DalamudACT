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
internal sealed partial class CombatTimelineWindow : Window
{
    [Flags]
    private enum TimelineContentFilter
    {
        None = 0,
        Output = 1 << 0,
        TakenDamage = 1 << 1,
        Heal = 1 << 2,
        Death = 1 << 3,
        Mitigation = 1 << 4,
        Cast = 1 << 5,
        Status = 1 << 6,
        MapEffect = 1 << 7,
        CombatBoundary = 1 << 8,
        TargetIcon = 1 << 9,
        Tether = 1 << 10,
    }

    private const TimelineContentFilter DefaultContentFilters = TimelineContentFilter.Output
                                                                 | TimelineContentFilter.TakenDamage
                                                                 | TimelineContentFilter.Heal
                                                                 | TimelineContentFilter.Death
                                                                 | TimelineContentFilter.CombatBoundary;

    private static readonly TimeSpan InlineFeedbackDuration = TimeSpan.FromSeconds(2.4);

    private readonly PluginConfiguration config;
    private readonly LocalStatsService statsService;
    private string characterFilter = string.Empty;
    private string textSearchFilter = string.Empty;
    private TimelineContentFilter contentFilters = DefaultContentFilters;
    private bool autoScroll = true;
    private bool drawFaulted;
    private int lastRenderedEntryCount = -1;
    private DateTime? lastInlineFeedbackAtUtc;
    private string inlineFeedbackText = "已复制";
    private readonly HashSet<int> selectedTimelineIndices = new();
    private int lastSelectedTimelineIndex = -1;

    public CombatTimelineWindow(PluginConfiguration config, LocalStatsService statsService)
        : base("战斗流水###CombatTimelineWindow")
    {
        this.config = config;
        this.statsService = statsService;
        characterFilter = config.CombatTimelineCharacterFilter ?? string.Empty;
        textSearchFilter = config.CombatTimelineTextSearchFilter ?? string.Empty;
        contentFilters = config.CombatTimelineContentFilterMask == 0
            ? DefaultContentFilters
            : (TimelineContentFilter)config.CombatTimelineContentFilterMask;
        autoScroll = config.CombatTimelineAutoScroll;
        Size = new Vector2(860f, 560f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        try
        {
            BgAlpha = Math.Clamp(config.WindowOpacity, 0.2f, 1f);

            var entries = statsService.CombatTimelineEntries;
            var filteredEntries = FilterEntries(entries, characterFilter, contentFilters, textSearchFilter);
            var characterOptions = BuildCharacterOptions(entries, characterFilter);

            ImGui.TextUnformatted("战斗流水");
            ImGui.SameLine();
            ImGui.TextDisabled(BuildCountSummary(entries.Count, filteredEntries.Count));
            ImGui.Separator();

            DrawToolbar(filteredEntries, characterOptions);
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

}
