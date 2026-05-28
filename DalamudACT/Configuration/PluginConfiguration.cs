using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace DalamudACT;


/// <summary>
/// 插件持久化配置对象，基于 Dalamud 的 IPluginConfiguration 保存窗口状态、统计显示项、配色与兼容迁移逻辑。
/// 相关参考：
/// - https://dalamud.dev/
/// - https://dalamud.dev/api/
/// 调整配置字段、版本迁移或 Save/Initialize 流程前，先对照 Dalamud 文档。
/// </summary>
[Serializable]
public sealed partial class PluginConfiguration : IPluginConfiguration
{
    public const float DefaultThemeBarOpacity = 0.75f;
    private const string FloatingClassicSettingsFileName = "floating-stats-classic.json";
    private const string FloatingIkegamiSettingsFileName = "floating-stats-ikegami.json";
    private const string FloatingMinimalSettingsFileName = "floating-stats-minimal.json";
    private const string FloatingStyleExportsDirectoryName = "floating-style-exports";
    private const string FloatingStyleShareCodePrefix = "DACTSTYLE1";
    private static readonly JsonSerializerOptions FloatingStyleJsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true,
    };
    private static readonly JsonSerializerOptions FloatingStyleShareCodeJsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = false,
    };
    private static readonly FieldInfo[] PersistentFieldInfos = typeof(PluginConfiguration)
        .GetFields(BindingFlags.Public | BindingFlags.Instance);

    public int Version { get; set; } = 57;

    public float WindowOpacity = 1f;
    public float FloatingStatsOpacity = 0.72f;
    public bool ShowStatsPanel = true;
    public bool LockFloatingStatsWindow = false;
    public FloatingStatsDisplayStyle FloatingStatsDisplayStyle = FloatingStatsDisplayStyle.Minimal;
    public FloatingStatsParticipantDisplayMode FloatingStatsParticipantDisplayMode = FloatingStatsParticipantDisplayMode.Auto;
    public int HostileNpcMinHpMultiplier = 10;
    public bool HighlightNpcRows = true;
    public CombatEndRule CombatEndRule = CombatEndRule.PartyList;
    public int EncounterTimeoutSeconds = 30;
    public int HistoryPreviewSeconds = 8;
    public bool CombatTimelineRecordingEnabled = false;
    public int CombatTimelineMaxEntries = 500;
    public bool ShowTimelineWindow = false;
    public bool LockTimelineWindow = false;
    public bool TimelineDebugMode = false;
    public float TimelineWindowOpacity = 0.9f;
    public int TimelineVisibleSeconds = 90;
    public int TimelineMaxVisibleEntries = 8;
    public float TimelineRowGap = 1f;
    public bool EnableTimelineDailyRoutinesTts = false;
    public int TimelineTtsLeadSeconds = 5;
    public TimelineTtsContentMode TimelineTtsContentMode = TimelineTtsContentMode.MechanicAndSkill;
    public string ActLogDirectory = @"D:\ff14act\FFXIVLogs";
    public string ActLogFilePath = string.Empty;
    public string ActLogEncounterKey = string.Empty;
    public bool EnableDebugLog = LogHelper.DefaultEnableDebugLog;

    public PartyMonitorConfig PartyMonitor = new();

    public bool ShowDpsTab = true;
    public bool ShowHpsTab = true;
    public bool ShowTakenTab = true;
    public bool ShowOverviewTab = true;
    public bool ShowHistoryTab = true;
    public bool ShowDpsPlayerColumn = true;
    public bool ShowDpsJobColumn = false;
    public bool ShowDpsDamageColumn = false;
    public bool ShowDpsValueColumn = true;
    public bool ShowDpsDeathsColumn = true;
    public bool ShowHpsPlayerColumn = true;
    public bool ShowHpsJobColumn = false;
    public bool ShowHpsHealColumn = false;
    public bool ShowHpsValueColumn = true;
    public bool ShowTakenPlayerColumn = true;
    public bool ShowTakenJobColumn = false;
    public bool ShowTakenDamageColumn = false;
    public bool ShowTakenValueColumn = true;
    public int DpsVisibleCount = 9;
    public float FloatingStatsPlayerColumnMinWidth = 0f;
    public float FloatingStatsMetricColumnWidth = 48f;
    public float FloatingStatsPlayerColumnWidth = 62f;
    public float FloatingStatsJobColumnWidth = 44f;
    public float FloatingStatsDamageColumnWidth = 73f;
    public float FloatingStatsValueColumnWidth = 48f;
    public float FloatingStatsDeathsColumnWidth = 24f;
    public float HistoryStartTimeColumnWidth = 100f;
    public float HistoryEndTimeColumnWidth = 100f;
    public float HistoryDurationColumnWidth = 100f;
    public float FloatingStatsRowHeight = 0f;
    public bool FloatingStatsIkegamiMinimalMode = false;
    public float FloatingStatsIkegamiPanelRaise = 7f;
    public float FloatingStatsIkegamiDetailRaise = 5f;
    public float FloatingStatsIkegamiFooterRaise = 24f;
    public bool FloatingStatsIkegamiShowScrollbar = false;
    public float FloatingStatsIkegamiBoxWidth = 132f;
    public float FloatingStatsIkegamiBoxHeight = 40f;
    public float FloatingStatsIkegamiNameHeight = 20f;
    public float FloatingStatsIkegamiHeaderHeight = 24f;
    public float FloatingStatsIkegamiHeaderLeftPadding = 8f;
    public float FloatingStatsIkegamiDetailLeftPadding = 8f;
    public bool FloatingStatsIkegamiShowMaxHitDetail = false;
    public bool FloatingStatsIkegamiShowVerticalScrollbar = false;
    public bool FloatingStatsIkegamiShowNameLine = true;
    public float FloatingStatsIkegamiNameAlpha = 1f;
    public float FloatingStatsIkegamiHeaderAlpha = 1f;
    public float FloatingStatsIkegamiPanelBackgroundAlpha = 1f;
    public float FloatingStatsIkegamiBodyAlpha = 1f;
    public float FloatingStatsIkegamiFooterAlpha = 1f;
    public float FloatingStatsIkegamiNameLeftPadding = 40f;
    public float FloatingStatsIkegamiNameRightPadding = 0f;
    public float FloatingStatsIkegamiJobBadgeSize = 20f;
    public float FloatingStatsIkegamiFooterHeight = 24f;
    public float FloatingStatsIkegamiFooterTimeZoneSpacing = 15f;
    public float FloatingStatsIkegamiFooterRightPadding = 20f;
    public float FloatingStatsIkegamiNameBackgroundAlpha = 0f;
    public float FloatingStatsIkegamiBodyBackgroundAlpha = 0f;
    public float FloatingStatsIkegamiContentBackgroundAlpha = 0.3f;
    public float FloatingStatsIkegamiTabFontScale = 1f;
    public float FloatingStatsIkegamiNameFontScale = 1f;
    public float FloatingStatsIkegamiHeaderFontScale = 1f;
    public float FloatingStatsIkegamiBodyFontScale = 1f;
    public float FloatingStatsIkegamiFooterFontScale = 1f;
    public float FloatingStatsIkegamiTooltipFontScale = 1f;
    public IkegamiBoxAlignment FloatingStatsIkegamiBoxAlignment = IkegamiBoxAlignment.Center;
    public bool FloatingStatsMinimalShowHeader = false;
    public bool FloatingStatsMinimalShowSummaryRow = true;
    public bool FloatingStatsMinimalShowPlayerColumn = false;
    public bool FloatingStatsMinimalShowPlayerNameInShareBar = false;
    public bool FloatingStatsMinimalShowJobInShareBar = true;
    public bool FloatingStatsMinimalShowDamageInShareBar = false;
    public bool FloatingStatsMinimalShowDeathsInShareBar = false;
    public bool FloatingStatsMinimalShowRatioInShareBar = false;
    public bool FloatingStatsMinimalShowDamageColumn = false;
    public bool FloatingStatsMinimalShowDeathsColumn = false;
    public bool FloatingStatsMinimalShowDurationInSummaryBar = true;
    public bool FloatingStatsMinimalShowTitleInSummaryBar = true;
    public bool FloatingStatsMinimalShowDpsInSummaryBar = true;
    public bool FloatingStatsMinimalShowDamageInSummaryBar = false;
    public bool FloatingStatsMinimalShowDeathsInSummaryBar = false;
    public bool FloatingStatsMinimalAutoWindowHeight = false;
    public float FloatingStatsMinimalRowHeight = 20f;
    public float FloatingStatsMinimalFontScale = 1f;
    public float FloatingStatsMinimalPlayerColumnWidth = 51f;
    public float FloatingStatsMinimalDamageColumnWidth = 88f;
    public float FloatingStatsMinimalDeathsColumnWidth = 32f;
    public float FloatingStatsClassicWindowWidth = 300f;
    public float FloatingStatsClassicWindowHeight = 300f;
    public float FloatingStatsIkegamiWindowWidth = 1139f;
    public float FloatingStatsIkegamiWindowHeight = 110f;
    public float FloatingStatsMinimalWindowWidth = 186f;
    public float FloatingStatsMinimalWindowHeight = 207f;

    public StatsBarColorMode BarColorMode = StatsBarColorMode.Theme;
    public float SingleBarColorR = 0.25f;
    public float SingleBarColorG = 0.65f;
    public float SingleBarColorB = 1f;
    public float SingleBarColorA = 0.9f;
    public float ThemeBarOpacity = DefaultThemeBarOpacity;
    public Dictionary<string, ThemeBarColorSetting> ThemeBarColors = new();
    public bool HighlightSelfBar = false;
    public SelfHighlightColorMode SelfHighlightColor = SelfHighlightColorMode.SunlightYellow;
    public List<string> CustomFriendlyNpcNames = new();

    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool? ShowDemoPanel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public string? MiniParseUrl;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    [NonSerialized]
    private bool suppressFloatingStyleSettingsSync;

    [NonSerialized]
    private DateTime lastSaveFailureLogUtc;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;

        WindowOpacity = Math.Clamp(WindowOpacity, 0.2f, 1f);
        FloatingStatsOpacity = Math.Clamp(FloatingStatsOpacity, 0f, 1f);
        EncounterTimeoutSeconds = Math.Clamp(EncounterTimeoutSeconds, 5, 180);
        HistoryPreviewSeconds = Math.Clamp(HistoryPreviewSeconds <= 0 ? 8 : HistoryPreviewSeconds, 1, 30);
        CombatTimelineMaxEntries = CombatTimelineMaxEntries < 0
            ? 500
            : Math.Clamp(CombatTimelineMaxEntries, 0, 50000);
        TimelineWindowOpacity = Math.Clamp(TimelineWindowOpacity, 0f, 1f);
        TimelineVisibleSeconds = Math.Clamp(TimelineVisibleSeconds <= 0 ? 90 : TimelineVisibleSeconds, 10, 600);
        TimelineMaxVisibleEntries = Math.Clamp(TimelineMaxVisibleEntries <= 0 ? 8 : TimelineMaxVisibleEntries, 1, 30);
        DpsVisibleCount = Math.Clamp(DpsVisibleCount, 1, 24);
        FloatingStatsPlayerColumnMinWidth = Math.Clamp(FloatingStatsPlayerColumnMinWidth, 0f, 360f);
        FloatingStatsMetricColumnWidth = Math.Clamp(FloatingStatsMetricColumnWidth, 48f, 220f);
        FloatingStatsPlayerColumnWidth = Math.Clamp(FloatingStatsPlayerColumnWidth, 0f, 2000f);
        FloatingStatsJobColumnWidth = Math.Clamp(FloatingStatsJobColumnWidth, 0f, 2000f);
        FloatingStatsDamageColumnWidth = Math.Clamp(FloatingStatsDamageColumnWidth, 0f, 2000f);
        FloatingStatsValueColumnWidth = Math.Clamp(FloatingStatsValueColumnWidth, 0f, 2000f);
        FloatingStatsDeathsColumnWidth = Math.Clamp(FloatingStatsDeathsColumnWidth, 0f, 2000f);
        if (FloatingStatsDeathsColumnWidth > 0f && FloatingStatsDeathsColumnWidth < 20f)
            FloatingStatsDeathsColumnWidth = 20f;
        HistoryStartTimeColumnWidth = Math.Clamp(HistoryStartTimeColumnWidth, 0f, 2000f);
        HistoryEndTimeColumnWidth = Math.Clamp(HistoryEndTimeColumnWidth, 0f, 2000f);
        HistoryDurationColumnWidth = Math.Clamp(HistoryDurationColumnWidth, 0f, 2000f);
        FloatingStatsRowHeight = Math.Clamp(FloatingStatsRowHeight, 0f, 60f);
        FloatingStatsIkegamiPanelRaise = Math.Clamp(FloatingStatsIkegamiPanelRaise, 0f, 60f);
        FloatingStatsIkegamiDetailRaise = Math.Clamp(FloatingStatsIkegamiDetailRaise, 0f, 60f);
        FloatingStatsIkegamiFooterRaise = Math.Clamp(FloatingStatsIkegamiFooterRaise, 0f, 80f);
        FloatingStatsIkegamiBoxWidth = Math.Clamp(FloatingStatsIkegamiBoxWidth, 1f, 260f);
        FloatingStatsIkegamiBoxHeight = Math.Clamp(FloatingStatsIkegamiBoxHeight, 1f, 140f);
        FloatingStatsIkegamiNameHeight = Math.Clamp(FloatingStatsIkegamiNameHeight, 16f, 40f);
        FloatingStatsIkegamiHeaderHeight = Math.Clamp(FloatingStatsIkegamiHeaderHeight, 20f, 80f);
        FloatingStatsIkegamiHeaderLeftPadding = Math.Clamp(FloatingStatsIkegamiHeaderLeftPadding, 0f, 32f);
        FloatingStatsIkegamiDetailLeftPadding = Math.Clamp(FloatingStatsIkegamiDetailLeftPadding, 0f, 32f);
        FloatingStatsIkegamiNameAlpha = Math.Clamp(FloatingStatsIkegamiNameAlpha, 0f, 1f);
        FloatingStatsIkegamiHeaderAlpha = Math.Clamp(FloatingStatsIkegamiHeaderAlpha, 0f, 1f);
        FloatingStatsIkegamiPanelBackgroundAlpha = Math.Clamp(FloatingStatsIkegamiPanelBackgroundAlpha, 0f, 1f);
        FloatingStatsIkegamiBodyAlpha = Math.Clamp(FloatingStatsIkegamiBodyAlpha, 0f, 1f);
        FloatingStatsIkegamiFooterAlpha = Math.Clamp(FloatingStatsIkegamiFooterAlpha, 0f, 1f);
        FloatingStatsIkegamiNameLeftPadding = Math.Clamp(FloatingStatsIkegamiNameLeftPadding, 0f, 40f);
        FloatingStatsIkegamiNameRightPadding = Math.Clamp(FloatingStatsIkegamiNameRightPadding, 0f, 40f);
        FloatingStatsIkegamiJobBadgeSize = Math.Clamp(FloatingStatsIkegamiJobBadgeSize, 12f, 36f);
        FloatingStatsIkegamiFooterHeight = Math.Clamp(FloatingStatsIkegamiFooterHeight, 18f, 48f);
        FloatingStatsIkegamiFooterTimeZoneSpacing = Math.Clamp(FloatingStatsIkegamiFooterTimeZoneSpacing, 0f, 32f);
        FloatingStatsIkegamiFooterRightPadding = Math.Clamp(FloatingStatsIkegamiFooterRightPadding, 0f, 40f);
        FloatingStatsIkegamiNameBackgroundAlpha = Math.Clamp(FloatingStatsIkegamiNameBackgroundAlpha, 0f, 1f);
        FloatingStatsIkegamiBodyBackgroundAlpha = Math.Clamp(FloatingStatsIkegamiBodyBackgroundAlpha, 0f, 1f);
        FloatingStatsIkegamiContentBackgroundAlpha = Math.Clamp(FloatingStatsIkegamiContentBackgroundAlpha, 0f, 1f);
        FloatingStatsIkegamiTabFontScale = Math.Clamp(FloatingStatsIkegamiTabFontScale, 0.6f, 2.0f);
        FloatingStatsIkegamiNameFontScale = Math.Clamp(FloatingStatsIkegamiNameFontScale, 0.6f, 2.0f);
        FloatingStatsIkegamiHeaderFontScale = Math.Clamp(FloatingStatsIkegamiHeaderFontScale, 0.6f, 2.0f);
        FloatingStatsIkegamiBodyFontScale = Math.Clamp(FloatingStatsIkegamiBodyFontScale, 0.6f, 2.0f);
        FloatingStatsIkegamiFooterFontScale = Math.Clamp(FloatingStatsIkegamiFooterFontScale, 0.6f, 2.0f);
        FloatingStatsIkegamiTooltipFontScale = Math.Clamp(FloatingStatsIkegamiTooltipFontScale, 0.6f, 2.0f);
        FloatingStatsMinimalRowHeight = Math.Clamp(FloatingStatsMinimalRowHeight <= 0f ? 20f : FloatingStatsMinimalRowHeight, 1f, 60f);
        FloatingStatsMinimalFontScale = Math.Clamp(FloatingStatsMinimalFontScale <= 0f ? 0.88f : FloatingStatsMinimalFontScale, 0.6f, 2.0f);
        FloatingStatsMinimalPlayerColumnWidth = Math.Clamp(FloatingStatsMinimalPlayerColumnWidth <= 0f ? 140f : FloatingStatsMinimalPlayerColumnWidth, 1f, 2000f);
        FloatingStatsMinimalDamageColumnWidth = Math.Clamp(FloatingStatsMinimalDamageColumnWidth <= 0f ? 88f : FloatingStatsMinimalDamageColumnWidth, 1f, 2000f);
        FloatingStatsMinimalDeathsColumnWidth = Math.Clamp(FloatingStatsMinimalDeathsColumnWidth <= 0f ? 32f : FloatingStatsMinimalDeathsColumnWidth, 1f, 2000f);
        FloatingStatsClassicWindowWidth = Math.Clamp(FloatingStatsClassicWindowWidth, 0f, 4000f);
        FloatingStatsClassicWindowHeight = Math.Clamp(FloatingStatsClassicWindowHeight, 0f, 4000f);
        FloatingStatsIkegamiWindowWidth = Math.Clamp(FloatingStatsIkegamiWindowWidth, 0f, 4000f);
        FloatingStatsIkegamiWindowHeight = Math.Clamp(FloatingStatsIkegamiWindowHeight, 0f, 4000f);
        FloatingStatsMinimalWindowWidth = Math.Clamp(FloatingStatsMinimalWindowWidth, 0f, 4000f);
        FloatingStatsMinimalWindowHeight = Math.Clamp(FloatingStatsMinimalWindowHeight, 0f, 4000f);
        TimelineWindowOpacity = Math.Clamp(TimelineWindowOpacity, 0f, 1f);
        TimelineVisibleSeconds = Math.Clamp(TimelineVisibleSeconds <= 0 ? 90 : TimelineVisibleSeconds, 10, 600);
        TimelineMaxVisibleEntries = Math.Clamp(TimelineMaxVisibleEntries <= 0 ? 8 : TimelineMaxVisibleEntries, 1, 30);
        TimelineRowGap = Math.Clamp(TimelineRowGap, 0f, 8f);
        TimelineTtsLeadSeconds = Math.Clamp(TimelineTtsLeadSeconds <= 0 ? 5 : TimelineTtsLeadSeconds, 1, 30);
        if (!Enum.IsDefined(typeof(TimelineTtsContentMode), TimelineTtsContentMode))
            TimelineTtsContentMode = TimelineTtsContentMode.MechanicAndSkill;
        if (string.IsNullOrWhiteSpace(ActLogDirectory))
            ActLogDirectory = @"D:\ff14act\FFXIVLogs";
        ActLogFilePath ??= string.Empty;
        ActLogEncounterKey ??= string.Empty;

        if (!Enum.IsDefined(typeof(CombatEndRule), CombatEndRule))
            CombatEndRule = CombatEndRule.PartyList;

        if (!Enum.IsDefined(typeof(FloatingStatsDisplayStyle), FloatingStatsDisplayStyle))
            FloatingStatsDisplayStyle = FloatingStatsDisplayStyle.Classic;

        if (!Enum.IsDefined(typeof(IkegamiBoxAlignment), FloatingStatsIkegamiBoxAlignment))
            FloatingStatsIkegamiBoxAlignment = IkegamiBoxAlignment.Left;

        if (!Enum.IsDefined(typeof(FloatingStatsParticipantDisplayMode), FloatingStatsParticipantDisplayMode))
            FloatingStatsParticipantDisplayMode = FloatingStatsParticipantDisplayMode.Auto;

        HostileNpcMinHpMultiplier = Math.Clamp(HostileNpcMinHpMultiplier <= 0 ? 10 : HostileNpcMinHpMultiplier, 1, 100);

        SingleBarColorR = Math.Clamp(SingleBarColorR, 0f, 1f);
        SingleBarColorG = Math.Clamp(SingleBarColorG, 0f, 1f);
        SingleBarColorB = Math.Clamp(SingleBarColorB, 0f, 1f);
        SingleBarColorA = Math.Clamp(SingleBarColorA, 0.2f, 1f);
        ThemeBarOpacity = ThemeBarOpacity <= 0f
            ? DefaultThemeBarOpacity
            : Math.Clamp(ThemeBarOpacity, 0.2f, 1f);

        if (!Enum.IsDefined(typeof(StatsBarColorMode), BarColorMode))
            BarColorMode = StatsBarColorMode.Theme;

        NormalizeCustomFriendlyNpcNames();

        if (ShowDemoPanel.HasValue)
            ShowStatsPanel = ShowDemoPanel.Value;

        if (Version < 4)
        {
            ShowDpsTab = true;
            ShowHpsTab = true;
            ShowTakenTab = true;
            ShowOverviewTab = true;
            ShowHistoryTab = true;
        }

        if (Version < 5)
            FloatingStatsOpacity = 0.72f;

        if (Version < 6)
        {
            ShowDpsPlayerColumn = true;
            ShowDpsJobColumn = true;
            ShowDpsDamageColumn = true;
            ShowDpsValueColumn = true;
            ShowDpsDeathsColumn = true;
        }

        if (Version < 13)
            ShowDpsDamageColumn = true;

        if (Version < 14)
            CombatEndRule = CombatEndRule.PartyList;

        if (Version < 15)
            LockFloatingStatsWindow = false;

        if (Version < 16)
            HistoryPreviewSeconds = 8;

        if (Version < 17)
            ShowDpsPlayerColumn = true;

        if (Version < 18)
        {
            ShowHpsPlayerColumn = true;
            ShowHpsJobColumn = true;
            ShowHpsValueColumn = true;
            ShowTakenPlayerColumn = true;
            ShowTakenJobColumn = true;
            ShowTakenValueColumn = true;
        }

        if (Version < 19)
            ShowTakenDamageColumn = true;

        if (Version < 20)
            ShowHpsHealColumn = true;

        if (Version < 21)
        {
            ShowDpsPlayerColumn = ShowDpsPlayerColumn || ShowHpsPlayerColumn || ShowTakenPlayerColumn;
            ShowDpsJobColumn = ShowDpsJobColumn || ShowHpsJobColumn || ShowTakenJobColumn;
            ShowDpsDamageColumn = ShowDpsDamageColumn || ShowHpsHealColumn || ShowTakenDamageColumn;
            ShowDpsValueColumn = ShowDpsValueColumn || ShowHpsValueColumn || ShowTakenValueColumn;
        }

        if (Version < 22)
        {
            FloatingStatsPlayerColumnWidth = 0f;
            FloatingStatsJobColumnWidth = 0f;
            FloatingStatsDamageColumnWidth = 0f;
            FloatingStatsValueColumnWidth = 0f;
            FloatingStatsDeathsColumnWidth = 0f;
        }

        if (Version < 23)
        {
            HistoryStartTimeColumnWidth = 0f;
            HistoryEndTimeColumnWidth = 0f;
            HistoryDurationColumnWidth = 0f;
        }

        if (Version < 24)
            EnableDebugLog = LogHelper.DefaultEnableDebugLog;

        if (Version < 25)
            CombatTimelineMaxEntries = 500;

        if (Version < 57)
            CombatTimelineRecordingEnabled = false;

        if (Version < 26)
            FloatingStatsParticipantDisplayMode = FloatingStatsParticipantDisplayMode.Auto;

        if (Version < 27 && !Enum.IsDefined(typeof(FloatingStatsParticipantDisplayMode), FloatingStatsParticipantDisplayMode))
            FloatingStatsParticipantDisplayMode = FloatingStatsParticipantDisplayMode.Auto;

        if (Version < 28)
        {
            HostileNpcMinHpMultiplier = 10;
            HighlightNpcRows = true;
        }

        if (Version < 29)
            MigrateThemeBarColorsToIkegamiDefaults();

        if (Version < 30)
            MigrateThemeBarColorsToIkegamiSoftDefaults();

        if (Version < 31)
            MigrateThemeBarColorsToIkegamiSofterDefaults();

        if (Version < 32)
            ThemeBarOpacity = DefaultThemeBarOpacity;

        if (Version < 33)
            MigrateThemeBarColorsToFineTunedDefaults();

        if (Version < 34)
            MigrateThemeBarColorsToAstDistinctDefaults();

        if (Version < 35)
            MigrateThemeBarColorsToSelectedHealerDefaults();

        if (Version < 36)
            FloatingStatsDisplayStyle = FloatingStatsDisplayStyle.Classic;

        if (Version < 37)
        {
            FloatingStatsIkegamiPanelRaise = 10f;
            FloatingStatsIkegamiDetailRaise = 10f;
            FloatingStatsIkegamiFooterRaise = 30f;
        }

        if (Version < 38)
        {
            FloatingStatsIkegamiShowScrollbar = true;
            FloatingStatsIkegamiBoxWidth = 154f;
            FloatingStatsIkegamiBoxHeight = 74f;
        }

        if (Version < 39)
        {
            FloatingStatsIkegamiNameHeight = 20f;
            FloatingStatsIkegamiHeaderHeight = 32f;
            FloatingStatsIkegamiHeaderLeftPadding = 8f;
            FloatingStatsIkegamiDetailLeftPadding = 8f;
        }

        if (Version < 40)
        {
            FloatingStatsIkegamiShowMaxHitDetail = true;
            FloatingStatsIkegamiShowVerticalScrollbar = true;
            FloatingStatsIkegamiShowNameLine = true;
            FloatingStatsIkegamiNameAlpha = 1f;
            FloatingStatsIkegamiHeaderAlpha = 1f;
            FloatingStatsIkegamiPanelBackgroundAlpha = 1f;
            FloatingStatsIkegamiBodyAlpha = 1f;
            FloatingStatsIkegamiFooterAlpha = 1f;
        }

        if (Version < 41)
        {
            FloatingStatsIkegamiNameLeftPadding = 0f;
            FloatingStatsIkegamiNameRightPadding = 0f;
            FloatingStatsIkegamiJobBadgeSize = 18f;
            FloatingStatsIkegamiFooterHeight = 24f;
            FloatingStatsIkegamiFooterTimeZoneSpacing = 10f;
            FloatingStatsIkegamiFooterRightPadding = 4f;
        }

        if (Version < 42)
        {
            FloatingStatsIkegamiNameBackgroundAlpha = 0f;
            FloatingStatsIkegamiBodyBackgroundAlpha = 0f;
            FloatingStatsIkegamiContentBackgroundAlpha = 0f;
            FloatingStatsIkegamiTabFontScale = 1f;
            FloatingStatsIkegamiNameFontScale = 1f;
            FloatingStatsIkegamiHeaderFontScale = 1f;
            FloatingStatsIkegamiBodyFontScale = 1f;
            FloatingStatsIkegamiFooterFontScale = 1f;
            FloatingStatsIkegamiTooltipFontScale = 1f;
        }

        if (Version < 43)
        {
            FloatingStatsIkegamiBoxAlignment = IkegamiBoxAlignment.Left;
            FloatingStatsClassicWindowWidth = 0f;
            FloatingStatsClassicWindowHeight = 0f;
            FloatingStatsIkegamiWindowWidth = 0f;
            FloatingStatsIkegamiWindowHeight = 0f;
        }

        if (Version < 44)
        {
            FloatingStatsMinimalShowPlayerColumn = true;
            FloatingStatsMinimalShowDamageColumn = true;
            FloatingStatsMinimalShowDeathsColumn = true;
            FloatingStatsMinimalRowHeight = 20f;
            FloatingStatsMinimalPlayerColumnWidth = 140f;
            FloatingStatsMinimalDamageColumnWidth = 88f;
            FloatingStatsMinimalDeathsColumnWidth = 32f;
            FloatingStatsMinimalWindowWidth = 0f;
            FloatingStatsMinimalWindowHeight = 0f;
        }

        if (Version < 45)
        {
            FloatingStatsMinimalShowDamageColumn = true;
            FloatingStatsMinimalPlayerColumnWidth = 140f;
            FloatingStatsMinimalDamageColumnWidth = 88f;
            FloatingStatsMinimalDeathsColumnWidth = 32f;
        }

        if (Version < 46)
        {
            FloatingStatsMinimalShowHeader = true;
        }

        if (Version < 51)
        {
            FloatingStatsMinimalAutoWindowHeight = false;
        }

        if (Version < 47)
        {
            FloatingStatsMinimalShowSummaryRow = true;
        }

        if (Version < 48)
        {
            FloatingStatsMinimalFontScale = 0.88f;
        }

        if (Version < 49)
        {
            FloatingStatsMinimalShowPlayerNameInShareBar = false;
        }

        if (Version < 50)
        {
            FloatingStatsMinimalShowJobInShareBar = true;
            FloatingStatsMinimalShowDamageInShareBar = false;
            FloatingStatsMinimalShowDeathsInShareBar = false;
            FloatingStatsMinimalShowRatioInShareBar = false;
            FloatingStatsMinimalShowDurationInSummaryBar = true;
            FloatingStatsMinimalShowTitleInSummaryBar = true;
            FloatingStatsMinimalShowDpsInSummaryBar = true;
            FloatingStatsMinimalShowDamageInSummaryBar = false;
            FloatingStatsMinimalShowDeathsInSummaryBar = false;
        }

        if (Version < 11)
            DpsVisibleCount = 8;

        if (Version < 12)
        {
            FloatingStatsPlayerColumnMinWidth = 0f;
            FloatingStatsMetricColumnWidth = 88f;
            FloatingStatsRowHeight = 0f;
        }

        if (Version < 7)
        {
            BarColorMode = StatsBarColorMode.Theme;
            SingleBarColorR = 0.25f;
            SingleBarColorG = 0.65f;
            SingleBarColorB = 1f;
            SingleBarColorA = 0.9f;
        }

        if (Version < 9)
            ResetThemeBarColors();

        if (Version < 10)
            MigrateThemeBarColorsToSkylineDefaults();

        if (Version < 59)
            PartyMonitor.RemoveDefaultDisabledBuiltInSkills();

        if (Version < 60)
            TimelineRowGap = 1f;

        if (Version < 61)
        {
            EnableTimelineDailyRoutinesTts = false;
            TimelineTtsLeadSeconds = 5;
        }

        if (Version < 62)
            TimelineTtsContentMode = TimelineTtsContentMode.MechanicAndSkill;

        if (Version < 63 && string.IsNullOrWhiteSpace(ActLogDirectory))
            ActLogDirectory = @"D:\ff14act\FFXIVLogs";

        if (Version < 64)
            ActLogFilePath = string.Empty;

        if (Version < 65)
            ActLogEncounterKey = string.Empty;

        SyncSharedColumnSettings();
        EnsureThemeBarColors();
        LogHelper.EnableDebugLog = EnableDebugLog;

        ShowDemoPanel = ShowStatsPanel;
        Version = Math.Max(Version, 65);

        if (!suppressFloatingStyleSettingsSync)
            EnsureFloatingStyleSettingFilesInitialized();
    }

    public bool HasAnyVisibleStatsTab()
        => ShowDpsTab || ShowHpsTab || ShowTakenTab || ShowOverviewTab || ShowHistoryTab;

    public static bool UsesLegacyFloatingTableLayout(FloatingStatsDisplayStyle style)
        => style == FloatingStatsDisplayStyle.Classic;



    public void Save()
    {
        try
        {
            pluginInterface?.SavePluginConfig(this);
            SaveFloatingStyleSettingsFile(FloatingStatsDisplayStyle);
        }
        catch (Exception ex)
        {
            var now = DateTime.UtcNow;
            if ((now - lastSaveFailureLogUtc).TotalSeconds >= 10)
            {
                lastSaveFailureLogUtc = now;
                LogHelper.Error("配置", ex, "保存插件配置失败，可能是配置文件被占用或权限不足。稍后再次修改设置会重试保存。");
            }
        }
    }


}
