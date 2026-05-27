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
        Status,
        Failure,
        Death,
        CombatBoundary,
    }

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
    private readonly HashSet<int> selectedTimelineIndices = new();
    private int lastSelectedTimelineIndex = -1;

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

}
