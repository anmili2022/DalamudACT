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

public enum StatsBarColorMode
{
    Theme = 0,
    Single = 1,
}

public enum CombatEndRule
{
    PartyList = 0,
    PartyListWithDelay = 1,
}

public enum FloatingStatsParticipantDisplayMode
{
    Auto = 0,
    PlayersOnly = 1,
    PlayersAndFriendlyNpc = 2,
    PlayersAndHostileNpc = 3,
}

public enum FloatingStatsDisplayStyle
{
    Classic = 0,
    Ikegami = 1,
}

public enum IkegamiBoxAlignment
{
    Left = 0,
    Center = 1,
    Right = 2,
}

[Serializable]
public sealed class ThemeBarColorSetting
{
    public float R = 1f;
    public float G = 1f;
    public float B = 1f;
    public float A = 0.92f;

    public ThemeBarColorSetting()
    {
    }

    public ThemeBarColorSetting(Vector4 color)
        => Set(color);

    public Vector4 ToVector4()
        => new(R, G, B, A);

    public void Set(Vector4 color)
    {
        R = Math.Clamp(color.X, 0f, 1f);
        G = Math.Clamp(color.Y, 0f, 1f);
        B = Math.Clamp(color.Z, 0f, 1f);
        A = Math.Clamp(color.W, 0.2f, 1f);
    }
}

/// <summary>
/// 插件持久化配置对象，基于 Dalamud 的 IPluginConfiguration 保存窗口状态、统计显示项、配色与兼容迁移逻辑。
/// 相关参考：
/// - https://dalamud.dev/
/// - https://dalamud.dev/api/
/// 调整配置字段、版本迁移或 Save/Initialize 流程前，先对照 Dalamud 文档。
/// </summary>
[Serializable]
public sealed class PluginConfiguration : IPluginConfiguration
{
    public const float DefaultThemeBarOpacity = 0.75f;
    private const string FloatingClassicSettingsFileName = "floating-stats-classic.json";
    private const string FloatingIkegamiSettingsFileName = "floating-stats-ikegami.json";
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

    public int Version { get; set; } = 43;

    public float WindowOpacity = 0.92f;
    public float FloatingStatsOpacity = 0.72f;
    public bool ShowStatsPanel = true;
    public bool LockFloatingStatsWindow = false;
    public FloatingStatsDisplayStyle FloatingStatsDisplayStyle = FloatingStatsDisplayStyle.Classic;
    public FloatingStatsParticipantDisplayMode FloatingStatsParticipantDisplayMode = FloatingStatsParticipantDisplayMode.Auto;
    public int HostileNpcMinHpMultiplier = 10;
    public bool HighlightNpcRows = true;
    public CombatEndRule CombatEndRule = CombatEndRule.PartyList;
    public int EncounterTimeoutSeconds = 30;
    public int HistoryPreviewSeconds = 8;
    public int CombatTimelineMaxEntries = 500;
    public bool EnableDebugLog = LogHelper.DefaultEnableDebugLog;

    public bool ShowDpsTab = true;
    public bool ShowHpsTab = true;
    public bool ShowTakenTab = true;
    public bool ShowOverviewTab = true;
    public bool ShowHistoryTab = true;
    public bool ShowDpsPlayerColumn = true;
    public bool ShowDpsJobColumn = true;
    public bool ShowDpsDamageColumn = true;
    public bool ShowDpsValueColumn = true;
    public bool ShowDpsDeathsColumn = true;
    public bool ShowHpsPlayerColumn = true;
    public bool ShowHpsJobColumn = true;
    public bool ShowHpsHealColumn = true;
    public bool ShowHpsValueColumn = true;
    public bool ShowTakenPlayerColumn = true;
    public bool ShowTakenJobColumn = true;
    public bool ShowTakenDamageColumn = true;
    public bool ShowTakenValueColumn = true;
    public int DpsVisibleCount = 8;
    public float FloatingStatsPlayerColumnMinWidth = 0f;
    public float FloatingStatsMetricColumnWidth = 88f;
    public float FloatingStatsPlayerColumnWidth = 0f;
    public float FloatingStatsJobColumnWidth = 0f;
    public float FloatingStatsDamageColumnWidth = 0f;
    public float FloatingStatsValueColumnWidth = 0f;
    public float FloatingStatsDeathsColumnWidth = 0f;
    public float HistoryStartTimeColumnWidth = 0f;
    public float HistoryEndTimeColumnWidth = 0f;
    public float HistoryDurationColumnWidth = 0f;
    public float FloatingStatsRowHeight = 0f;
    public float FloatingStatsIkegamiPanelRaise = 10f;
    public float FloatingStatsIkegamiDetailRaise = 10f;
    public float FloatingStatsIkegamiFooterRaise = 30f;
    public bool FloatingStatsIkegamiShowScrollbar = true;
    public float FloatingStatsIkegamiBoxWidth = 154f;
    public float FloatingStatsIkegamiBoxHeight = 74f;
    public float FloatingStatsIkegamiNameHeight = 20f;
    public float FloatingStatsIkegamiHeaderHeight = 32f;
    public float FloatingStatsIkegamiHeaderLeftPadding = 8f;
    public float FloatingStatsIkegamiDetailLeftPadding = 8f;
    public bool FloatingStatsIkegamiShowMaxHitDetail = true;
    public bool FloatingStatsIkegamiShowVerticalScrollbar = true;
    public bool FloatingStatsIkegamiShowNameLine = true;
    public float FloatingStatsIkegamiNameAlpha = 1f;
    public float FloatingStatsIkegamiHeaderAlpha = 1f;
    public float FloatingStatsIkegamiPanelBackgroundAlpha = 1f;
    public float FloatingStatsIkegamiBodyAlpha = 1f;
    public float FloatingStatsIkegamiFooterAlpha = 1f;
    public float FloatingStatsIkegamiNameLeftPadding = 0f;
    public float FloatingStatsIkegamiNameRightPadding = 0f;
    public float FloatingStatsIkegamiJobBadgeSize = 18f;
    public float FloatingStatsIkegamiFooterHeight = 24f;
    public float FloatingStatsIkegamiFooterTimeZoneSpacing = 10f;
    public float FloatingStatsIkegamiFooterRightPadding = 4f;
    public float FloatingStatsIkegamiNameBackgroundAlpha = 0f;
    public float FloatingStatsIkegamiBodyBackgroundAlpha = 0f;
    public float FloatingStatsIkegamiContentBackgroundAlpha = 0f;
    public float FloatingStatsIkegamiTabFontScale = 1f;
    public float FloatingStatsIkegamiNameFontScale = 1f;
    public float FloatingStatsIkegamiHeaderFontScale = 1f;
    public float FloatingStatsIkegamiBodyFontScale = 1f;
    public float FloatingStatsIkegamiFooterFontScale = 1f;
    public float FloatingStatsIkegamiTooltipFontScale = 1f;
    public IkegamiBoxAlignment FloatingStatsIkegamiBoxAlignment = IkegamiBoxAlignment.Left;
    public float FloatingStatsClassicWindowWidth = 0f;
    public float FloatingStatsClassicWindowHeight = 0f;
    public float FloatingStatsIkegamiWindowWidth = 0f;
    public float FloatingStatsIkegamiWindowHeight = 0f;

    public StatsBarColorMode BarColorMode = StatsBarColorMode.Theme;
    public float SingleBarColorR = 0.25f;
    public float SingleBarColorG = 0.65f;
    public float SingleBarColorB = 1f;
    public float SingleBarColorA = 0.9f;
    public float ThemeBarOpacity = DefaultThemeBarOpacity;
    public Dictionary<string, ThemeBarColorSetting> ThemeBarColors = new();

    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool? ShowDemoPanel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public string? MiniParseUrl;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    [NonSerialized]
    private bool suppressFloatingStyleSettingsSync;

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
        FloatingStatsClassicWindowWidth = Math.Clamp(FloatingStatsClassicWindowWidth, 0f, 4000f);
        FloatingStatsClassicWindowHeight = Math.Clamp(FloatingStatsClassicWindowHeight, 0f, 4000f);
        FloatingStatsIkegamiWindowWidth = Math.Clamp(FloatingStatsIkegamiWindowWidth, 0f, 4000f);
        FloatingStatsIkegamiWindowHeight = Math.Clamp(FloatingStatsIkegamiWindowHeight, 0f, 4000f);

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

        SyncSharedColumnSettings();
        EnsureThemeBarColors();
        LogHelper.EnableDebugLog = EnableDebugLog;

        ShowDemoPanel = ShowStatsPanel;
        Version = Math.Max(Version, 43);

        if (!suppressFloatingStyleSettingsSync)
            EnsureFloatingStyleSettingFilesInitialized();
    }

    public bool HasAnyVisibleStatsTab()
        => ShowDpsTab || ShowHpsTab || ShowTakenTab || ShowOverviewTab || ShowHistoryTab;

    public static bool UsesLegacyFloatingTableLayout(FloatingStatsDisplayStyle style)
        => style == FloatingStatsDisplayStyle.Classic;

    public Vector4 GetSingleBarColor()
        => new(SingleBarColorR, SingleBarColorG, SingleBarColorB, SingleBarColorA);

    public void SetSingleBarColor(Vector4 color)
    {
        SingleBarColorR = Math.Clamp(color.X, 0f, 1f);
        SingleBarColorG = Math.Clamp(color.Y, 0f, 1f);
        SingleBarColorB = Math.Clamp(color.Z, 0f, 1f);
        SingleBarColorA = Math.Clamp(color.W, 0.2f, 1f);
    }

    public Vector4 GetThemeBarColor(string? jobName)
    {
        if (!string.IsNullOrWhiteSpace(jobName) && ThemeBarColors.TryGetValue(jobName, out var configured))
            return ApplyThemeBarOpacity(configured.ToVector4());

        return JobThemePalette.TryGetDefaultColor(jobName, out var fallback)
            ? ApplyThemeBarOpacity(fallback)
            : new Vector4(0.25f, 0.65f, 1f, ThemeBarOpacity);
    }

    public void SetThemeBarColor(string jobName, Vector4 color)
    {
        if (string.IsNullOrWhiteSpace(jobName))
            return;

        ThemeBarColors[jobName] = new ThemeBarColorSetting(color);
    }

    public void ResetThemeBarColors()
    {
        ThemeBarColors = new Dictionary<string, ThemeBarColorSetting>();
        foreach (var entry in JobThemePalette.Entries)
            ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
    }

    public void Reset()
    {
        WindowOpacity = 0.92f;
        FloatingStatsOpacity = 0.72f;
        ShowStatsPanel = true;
        LockFloatingStatsWindow = false;
        FloatingStatsDisplayStyle = FloatingStatsDisplayStyle.Classic;
        FloatingStatsParticipantDisplayMode = FloatingStatsParticipantDisplayMode.Auto;
        HostileNpcMinHpMultiplier = 10;
        HighlightNpcRows = true;
        ShowDemoPanel = true;
        CombatEndRule = CombatEndRule.PartyList;
        EncounterTimeoutSeconds = 30;
        HistoryPreviewSeconds = 8;
        CombatTimelineMaxEntries = 500;
        EnableDebugLog = LogHelper.DefaultEnableDebugLog;
        ShowDpsTab = true;
        ShowHpsTab = true;
        ShowTakenTab = true;
        ShowOverviewTab = true;
        ShowHistoryTab = true;
        ShowDpsPlayerColumn = true;
        ShowDpsJobColumn = true;
        ShowDpsDamageColumn = true;
        ShowDpsValueColumn = true;
        ShowDpsDeathsColumn = true;
        ShowHpsPlayerColumn = true;
        ShowHpsJobColumn = true;
        ShowHpsHealColumn = true;
        ShowHpsValueColumn = true;
        ShowTakenPlayerColumn = true;
        ShowTakenJobColumn = true;
        ShowTakenDamageColumn = true;
        ShowTakenValueColumn = true;
        DpsVisibleCount = 8;
        FloatingStatsPlayerColumnMinWidth = 0f;
        FloatingStatsMetricColumnWidth = 88f;
        FloatingStatsPlayerColumnWidth = 0f;
        FloatingStatsJobColumnWidth = 0f;
        FloatingStatsDamageColumnWidth = 0f;
        FloatingStatsValueColumnWidth = 0f;
        FloatingStatsDeathsColumnWidth = 0f;
        HistoryStartTimeColumnWidth = 0f;
        HistoryEndTimeColumnWidth = 0f;
        HistoryDurationColumnWidth = 0f;
        FloatingStatsRowHeight = 0f;
        FloatingStatsIkegamiPanelRaise = 10f;
        FloatingStatsIkegamiDetailRaise = 10f;
        FloatingStatsIkegamiFooterRaise = 30f;
        FloatingStatsIkegamiShowScrollbar = true;
        FloatingStatsIkegamiBoxWidth = 154f;
        FloatingStatsIkegamiBoxHeight = 74f;
        FloatingStatsIkegamiNameHeight = 20f;
        FloatingStatsIkegamiHeaderHeight = 32f;
        FloatingStatsIkegamiHeaderLeftPadding = 8f;
        FloatingStatsIkegamiDetailLeftPadding = 8f;
        FloatingStatsIkegamiShowMaxHitDetail = true;
        FloatingStatsIkegamiShowVerticalScrollbar = true;
        FloatingStatsIkegamiShowNameLine = true;
        FloatingStatsIkegamiNameAlpha = 1f;
        FloatingStatsIkegamiHeaderAlpha = 1f;
        FloatingStatsIkegamiPanelBackgroundAlpha = 1f;
        FloatingStatsIkegamiBodyAlpha = 1f;
        FloatingStatsIkegamiFooterAlpha = 1f;
        FloatingStatsIkegamiNameLeftPadding = 0f;
        FloatingStatsIkegamiNameRightPadding = 0f;
        FloatingStatsIkegamiJobBadgeSize = 18f;
        FloatingStatsIkegamiFooterHeight = 24f;
        FloatingStatsIkegamiFooterTimeZoneSpacing = 10f;
        FloatingStatsIkegamiFooterRightPadding = 4f;
        FloatingStatsIkegamiNameBackgroundAlpha = 0f;
        FloatingStatsIkegamiBodyBackgroundAlpha = 0f;
        FloatingStatsIkegamiContentBackgroundAlpha = 0f;
        FloatingStatsIkegamiTabFontScale = 1f;
        FloatingStatsIkegamiNameFontScale = 1f;
        FloatingStatsIkegamiHeaderFontScale = 1f;
        FloatingStatsIkegamiBodyFontScale = 1f;
        FloatingStatsIkegamiFooterFontScale = 1f;
        FloatingStatsIkegamiTooltipFontScale = 1f;
        FloatingStatsIkegamiBoxAlignment = IkegamiBoxAlignment.Left;
        FloatingStatsClassicWindowWidth = 0f;
        FloatingStatsClassicWindowHeight = 0f;
        FloatingStatsIkegamiWindowWidth = 0f;
        FloatingStatsIkegamiWindowHeight = 0f;
        BarColorMode = StatsBarColorMode.Theme;
        SingleBarColorR = 0.25f;
        SingleBarColorG = 0.65f;
        SingleBarColorB = 1f;
        SingleBarColorA = 0.9f;
        ThemeBarOpacity = DefaultThemeBarOpacity;
        ResetThemeBarColors();
        LogHelper.EnableDebugLog = EnableDebugLog;
    }

    public void Save()
    {
        pluginInterface?.SavePluginConfig(this);
        SaveFloatingStyleSettingsFile(FloatingStatsDisplayStyle);
    }

    public void SwitchFloatingStatsDisplayStyle(FloatingStatsDisplayStyle style)
    {
        if (FloatingStatsDisplayStyle == style)
            return;

        SaveFloatingStyleSettingsFile(FloatingStatsDisplayStyle);

        if (!TryLoadFloatingStyleSettingsFromFile(style))
        {
            FloatingStatsDisplayStyle = style;
            ReinitializeAfterExternalStyleChange();
        }

        Save();
    }

    public string? GetFloatingStyleSettingsFilePath(FloatingStatsDisplayStyle style)
        => GetFloatingStyleSettingsPath(style);

    public string? GetFloatingStyleSettingsDirectoryPath()
        => pluginInterface?.GetPluginConfigDirectory();

    public string? GetFloatingStyleExportDirectoryPath()
    {
        var configDirectory = GetFloatingStyleSettingsDirectoryPath();
        return string.IsNullOrWhiteSpace(configDirectory)
            ? null
            : Path.Combine(configDirectory, FloatingStyleExportsDirectoryName);
    }

    public bool OpenFloatingStyleSettingsDirectory(out string message)
        => TryOpenDirectory(
            GetFloatingStyleSettingsDirectoryPath(),
            "已打开样式配置目录。",
            out message);

    public bool OpenFloatingStyleExportDirectory(out string message)
    {
        var exportDirectory = GetFloatingStyleExportDirectoryPath();
        if (!string.IsNullOrWhiteSpace(exportDirectory))
            Directory.CreateDirectory(exportDirectory);

        return TryOpenDirectory(exportDirectory, "已打开样式导出目录。", out message);
    }

    public bool ExportFloatingStyleSettingsTo(
        FloatingStatsDisplayStyle style,
        string? exportPath,
        out string message)
    {
        var sourcePath = GetFloatingStyleSettingsPath(style);
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            message = "导出失败：未能定位样式配置文件。";
            return false;
        }

        if (style == FloatingStatsDisplayStyle)
            SaveFloatingStyleSettingsFile(style);
        else
            EnsureFloatingStyleSettingsFileExists(style);

        if (!File.Exists(sourcePath))
        {
            message = $"导出失败：未找到源文件 {sourcePath}";
            return false;
        }

        var resolvedExportPath = ResolveExportPath(style, exportPath);
        if (string.IsNullOrWhiteSpace(resolvedExportPath))
        {
            message = "导出失败：无法确定导出目标路径。";
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(resolvedExportPath)!);
            File.Copy(sourcePath, resolvedExportPath, true);
            message = $"已导出 {GetFloatingStyleDisplayName(style)} 样式到 {resolvedExportPath}";
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Warning("配置", ex, $"导出样式设置失败：{resolvedExportPath}");
            message = $"导出失败：{ex.Message}";
            return false;
        }
    }

    public bool TryGenerateFloatingStyleShareCode(
        FloatingStatsDisplayStyle style,
        out string shareCode,
        out string message)
    {
        shareCode = string.Empty;

        try
        {
            var snapshot = CreateFloatingStyleSnapshot(style);
            var json = JsonSerializer.Serialize(snapshot, FloatingStyleShareCodeJsonOptions);
            var payloadBytes = Encoding.UTF8.GetBytes(json);

            using var compressedStream = new MemoryStream();
            using (var gzip = new GZipStream(compressedStream, CompressionLevel.SmallestSize, true))
                gzip.Write(payloadBytes, 0, payloadBytes.Length);

            shareCode = $"{FloatingStyleShareCodePrefix}|{GetFloatingStyleShareCodeStyleToken(style)}|{Convert.ToBase64String(compressedStream.ToArray())}";
            message = $"已生成 {GetFloatingStyleDisplayName(style)} 分享码。";
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Warning("配置", ex, $"生成 {GetFloatingStyleDisplayName(style)} 分享码失败。");
            message = $"生成分享码失败：{ex.Message}";
            return false;
        }
    }

    public bool ImportFloatingStyleShareCode(
        FloatingStatsDisplayStyle style,
        string shareCode,
        out string message)
    {
        if (string.IsNullOrWhiteSpace(shareCode))
        {
            message = "导入失败：请先粘贴分享码。";
            return false;
        }

        if (!TryDecodeFloatingStyleShareCode(shareCode, style, out var snapshot, out message))
            return false;

        var targetPath = GetFloatingStyleSettingsPath(style);
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            message = "导入失败：未能定位目标样式配置文件。";
            return false;
        }

        try
        {
            WriteFloatingStyleSnapshotToPath(snapshot!, targetPath);

            if (FloatingStatsDisplayStyle == style)
            {
                CopyPersistentFieldsFrom(snapshot!);
                FloatingStatsDisplayStyle = style;
                ReinitializeAfterExternalStyleChange();
                Save();
            }

            message = $"已导入 {GetFloatingStyleDisplayName(style)} 分享码。";
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Warning("配置", ex, $"导入 {GetFloatingStyleDisplayName(style)} 分享码失败。");
            message = $"导入失败：{ex.Message}";
            return false;
        }
    }

    public bool ImportFloatingStyleShareCode(
        string shareCode,
        out string message)
    {
        if (string.IsNullOrWhiteSpace(shareCode))
        {
            message = "导入失败：请先粘贴分享码。";
            return false;
        }

        if (!TryResolveFloatingStyleFromShareCode(shareCode, out var style, out message))
            return false;

        return ImportFloatingStyleShareCode(style, shareCode, out message);
    }

    public bool TryPeekFloatingStyleShareCodeStyle(
        string shareCode,
        out FloatingStatsDisplayStyle style)
    {
        style = FloatingStatsDisplayStyle.Classic;
        return !string.IsNullOrWhiteSpace(shareCode)
               && TryResolveFloatingStyleFromShareCode(shareCode, out style, out _);
    }

    public bool ImportFloatingStyleSettingsFrom(
        FloatingStatsDisplayStyle style,
        string importPath,
        out string message)
    {
        if (string.IsNullOrWhiteSpace(importPath))
        {
            message = "导入失败：请先填写要导入的 JSON 路径。";
            return false;
        }

        if (!File.Exists(importPath))
        {
            message = $"导入失败：未找到文件 {importPath}";
            return false;
        }

        var targetPath = GetFloatingStyleSettingsPath(style);
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            message = "导入失败：未能定位目标样式配置文件。";
            return false;
        }

        if (!TryLoadFloatingStyleSnapshotFromFile(importPath, style, out var snapshot, out message))
            return false;

        try
        {
            WriteFloatingStyleSnapshotToPath(snapshot!, targetPath);

            if (FloatingStatsDisplayStyle == style)
            {
                CopyPersistentFieldsFrom(snapshot!);
                FloatingStatsDisplayStyle = style;
                ReinitializeAfterExternalStyleChange();
                Save();
            }

            message = $"已导入 {GetFloatingStyleDisplayName(style)} 样式：{importPath}";
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Warning("配置", ex, $"导入样式设置失败：{importPath}");
            message = $"导入失败：{ex.Message}";
            return false;
        }
    }

    public void SyncSharedColumnSettings()
    {
        ShowHpsPlayerColumn = ShowDpsPlayerColumn;
        ShowTakenPlayerColumn = ShowDpsPlayerColumn;

        ShowHpsJobColumn = ShowDpsJobColumn;
        ShowTakenJobColumn = ShowDpsJobColumn;

        ShowHpsHealColumn = ShowDpsDamageColumn;
        ShowTakenDamageColumn = ShowDpsDamageColumn;

        ShowHpsValueColumn = ShowDpsValueColumn;
        ShowTakenValueColumn = ShowDpsValueColumn;
    }

    public void ResetSharedMetricColumnWidths()
    {
        FloatingStatsPlayerColumnWidth = 0f;
        FloatingStatsJobColumnWidth = 0f;
        FloatingStatsDamageColumnWidth = 0f;
        FloatingStatsValueColumnWidth = 0f;
        FloatingStatsDeathsColumnWidth = 0f;
    }

    public void ResetHistoryColumnWidths()
    {
        HistoryStartTimeColumnWidth = 0f;
        HistoryEndTimeColumnWidth = 0f;
        HistoryDurationColumnWidth = 0f;
    }

    private void EnsureFloatingStyleSettingFilesInitialized()
    {
        if (pluginInterface == null)
            return;

        EnsureFloatingStyleSettingsFileExists(FloatingStatsDisplayStyle.Classic);
        EnsureFloatingStyleSettingsFileExists(FloatingStatsDisplayStyle.Ikegami);
        _ = TryLoadFloatingStyleSettingsFromFile(FloatingStatsDisplayStyle);
    }

    private void EnsureFloatingStyleSettingsFileExists(FloatingStatsDisplayStyle style)
    {
        var path = GetFloatingStyleSettingsPath(style);
        if (string.IsNullOrWhiteSpace(path) || File.Exists(path))
            return;

        SaveFloatingStyleSettingsFile(style);
    }

    private bool TryLoadFloatingStyleSettingsFromFile(FloatingStatsDisplayStyle style)
    {
        var path = GetFloatingStyleSettingsPath(style);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            var json = File.ReadAllText(path);
            var snapshot = JsonSerializer.Deserialize<PluginConfiguration>(json, FloatingStyleJsonOptions);
            if (snapshot == null)
                return false;

            CopyPersistentFieldsFrom(snapshot);
            FloatingStatsDisplayStyle = style;
            ReinitializeAfterExternalStyleChange();
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Warning("配置", ex, $"读取样式设置文件失败：{path}");
            return false;
        }
    }

    private void SaveFloatingStyleSettingsFile(FloatingStatsDisplayStyle style)
    {
        var path = GetFloatingStyleSettingsPath(style);
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var snapshot = CreateFloatingStyleSnapshot(style);
            WriteFloatingStyleSnapshotToPath(snapshot, path);
        }
        catch (Exception ex)
        {
            LogHelper.Warning("配置", ex, $"写入样式设置文件失败：{path}");
        }
    }

    private PluginConfiguration CreateFloatingStyleSnapshot(FloatingStatsDisplayStyle style)
    {
        var snapshot = new PluginConfiguration();
        snapshot.CopyPersistentFieldsFrom(this);
        snapshot.FloatingStatsDisplayStyle = style;
        return snapshot;
    }

    private void CopyPersistentFieldsFrom(PluginConfiguration source)
    {
        Version = source.Version;
        foreach (var field in PersistentFieldInfos)
            field.SetValue(this, field.GetValue(source));
    }

    private void ReinitializeAfterExternalStyleChange()
    {
        if (pluginInterface == null)
            return;

        suppressFloatingStyleSettingsSync = true;
        try
        {
            Initialize(pluginInterface);
        }
        finally
        {
            suppressFloatingStyleSettingsSync = false;
        }
    }

    private string? GetFloatingStyleSettingsPath(FloatingStatsDisplayStyle style)
    {
        if (pluginInterface == null)
            return null;

        var configDirectory = pluginInterface.GetPluginConfigDirectory();
        var fileName = style switch
        {
            FloatingStatsDisplayStyle.Ikegami => FloatingIkegamiSettingsFileName,
            _ => FloatingClassicSettingsFileName,
        };

        return Path.Combine(configDirectory, fileName);
    }

    private string ResolveExportPath(FloatingStatsDisplayStyle style, string? exportPath)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

        if (string.IsNullOrWhiteSpace(exportPath))
        {
            var exportDirectory = GetFloatingStyleExportDirectoryPath();
            if (string.IsNullOrWhiteSpace(exportDirectory))
                return string.Empty;

            return Path.Combine(exportDirectory, $"{GetFloatingStyleFileStem(style)}-{timestamp}.json");
        }

        if (Directory.Exists(exportPath))
            return Path.Combine(exportPath, $"{GetFloatingStyleFileStem(style)}-{timestamp}.json");

        if (EndsWithDirectorySeparator(exportPath))
        {
            return Path.Combine(exportPath, $"{GetFloatingStyleFileStem(style)}-{timestamp}.json");
        }

        return Path.GetExtension(exportPath).Length == 0
            ? $"{exportPath}.json"
            : exportPath;
    }

    private bool TryLoadFloatingStyleSnapshotFromFile(
        string importPath,
        FloatingStatsDisplayStyle style,
        out PluginConfiguration? snapshot,
        out string message)
    {
        snapshot = null;

        if (pluginInterface == null)
        {
            message = "导入失败：插件接口尚未初始化。";
            return false;
        }

        try
        {
            var json = File.ReadAllText(importPath);
            snapshot = JsonSerializer.Deserialize<PluginConfiguration>(json, FloatingStyleJsonOptions);
            if (snapshot == null)
            {
                message = "导入失败：JSON 内容为空或无法识别。";
                return false;
            }

            snapshot.FloatingStatsDisplayStyle = style;
            snapshot.suppressFloatingStyleSettingsSync = true;
            snapshot.Initialize(pluginInterface);
            snapshot.suppressFloatingStyleSettingsSync = false;
            message = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Warning("配置", ex, $"解析样式设置文件失败：{importPath}");
            message = $"导入失败：{ex.Message}";
            return false;
        }
    }

    private bool TryDecodeFloatingStyleShareCode(
        string shareCode,
        FloatingStatsDisplayStyle expectedStyle,
        out PluginConfiguration? snapshot,
        out string message)
    {
        snapshot = null;

        if (pluginInterface == null)
        {
            message = "导入失败：插件接口尚未初始化。";
            return false;
        }

        try
        {
            var trimmed = shareCode.Trim();
            var parts = trimmed.Split('|', 3, StringSplitOptions.TrimEntries);
            if (parts.Length != 3 || !string.Equals(parts[0], FloatingStyleShareCodePrefix, StringComparison.Ordinal))
            {
                message = "导入失败：分享码格式不正确。";
                return false;
            }

            var styleFromCode = TryParseFloatingStyleShareCodeStyleToken(parts[1], out var parsedStyle)
                ? parsedStyle
                : (FloatingStatsDisplayStyle?)null;
            if (styleFromCode == null)
            {
                message = "导入失败：分享码里的样式标记无法识别。";
                return false;
            }

            if (styleFromCode.Value != expectedStyle)
            {
                message = $"导入失败：这是一份{GetFloatingStyleDisplayName(styleFromCode.Value)}分享码，不是{GetFloatingStyleDisplayName(expectedStyle)}。";
                return false;
            }

            var compressedBytes = Convert.FromBase64String(parts[2]);
            using var compressedStream = new MemoryStream(compressedBytes);
            using var gzip = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);
            var json = reader.ReadToEnd();

            snapshot = JsonSerializer.Deserialize<PluginConfiguration>(json, FloatingStyleShareCodeJsonOptions);
            if (snapshot == null)
            {
                message = "导入失败：分享码内容为空或无法识别。";
                return false;
            }

            snapshot.FloatingStatsDisplayStyle = expectedStyle;
            snapshot.suppressFloatingStyleSettingsSync = true;
            snapshot.Initialize(pluginInterface);
            snapshot.suppressFloatingStyleSettingsSync = false;
            message = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Warning("配置", ex, "解析分享码失败。");
            message = $"导入失败：{ex.Message}";
            return false;
        }
    }

    private bool TryResolveFloatingStyleFromShareCode(
        string shareCode,
        out FloatingStatsDisplayStyle style,
        out string message)
    {
        style = FloatingStatsDisplayStyle.Classic;
        var trimmed = shareCode.Trim();
        var parts = trimmed.Split('|', 3, StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], FloatingStyleShareCodePrefix, StringComparison.Ordinal))
        {
            message = "导入失败：分享码格式不正确。";
            return false;
        }

        if (!TryParseFloatingStyleShareCodeStyleToken(parts[1], out style))
        {
            message = "导入失败：分享码里的样式标记无法识别。";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private void WriteFloatingStyleSnapshotToPath(PluginConfiguration snapshot, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(snapshot, FloatingStyleJsonOptions);
        File.WriteAllText(path, json);
    }

    private static string GetFloatingStyleDisplayName(FloatingStatsDisplayStyle style)
        => style switch
        {
            FloatingStatsDisplayStyle.Ikegami => "ikegami",
            _ => "经典表格",
        };

    private static string GetFloatingStyleFileStem(FloatingStatsDisplayStyle style)
        => style switch
        {
            FloatingStatsDisplayStyle.Ikegami => "floating-stats-ikegami",
            _ => "floating-stats-classic",
        };

    private static string GetFloatingStyleShareCodeStyleToken(FloatingStatsDisplayStyle style)
        => style switch
        {
            FloatingStatsDisplayStyle.Ikegami => "Ikegami",
            _ => "Classic",
        };

    private static bool TryParseFloatingStyleShareCodeStyleToken(string code, out FloatingStatsDisplayStyle style)
    {
        style = FloatingStatsDisplayStyle.Classic;
        if (string.Equals(code, "Ikegami", StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, "I", StringComparison.OrdinalIgnoreCase))
        {
            style = FloatingStatsDisplayStyle.Ikegami;
            return true;
        }

        if (string.Equals(code, "Classic", StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, "C", StringComparison.OrdinalIgnoreCase))
        {
            style = FloatingStatsDisplayStyle.Classic;
            return true;
        }

        return false;
    }

    private static bool EndsWithDirectorySeparator(string path)
        => !string.IsNullOrWhiteSpace(path)
           && (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
               || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal));

    private static bool TryOpenDirectory(string? directoryPath, string successMessage, out string message)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            message = "操作失败：未能定位目标目录。";
            return false;
        }

        try
        {
            Directory.CreateDirectory(directoryPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = directoryPath,
                UseShellExecute = true,
                Verb = "open",
            });
            message = successMessage;
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Warning("配置", ex, $"打开目录失败：{directoryPath}");
            message = $"打开目录失败：{ex.Message}";
            return false;
        }
    }

    private void EnsureThemeBarColors()
    {
        ThemeBarColors ??= new Dictionary<string, ThemeBarColorSetting>();

        foreach (var entry in JobThemePalette.Entries)
        {
            if (!ThemeBarColors.TryGetValue(entry.JobName, out var configured) || configured == null)
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            configured.Set(configured.ToVector4());
        }
    }

    private void MigrateThemeBarColorsToSkylineDefaults()
    {
        ThemeBarColors ??= new Dictionary<string, ThemeBarColorSetting>();

        foreach (var entry in JobThemePalette.Entries)
        {
            if (!ThemeBarColors.TryGetValue(entry.JobName, out var configured) || configured == null)
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            var current = configured.ToVector4();
            if (JobThemePalette.TryGetLegacyDefaultColor(entry.JobName, out var legacy)
                && ColorsApproximatelyEqual(current, legacy))
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            configured.Set(current);
        }
    }

    private void MigrateThemeBarColorsToIkegamiDefaults()
    {
        ThemeBarColors ??= new Dictionary<string, ThemeBarColorSetting>();

        foreach (var entry in JobThemePalette.Entries)
        {
            if (!ThemeBarColors.TryGetValue(entry.JobName, out var configured) || configured == null)
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            var current = configured.ToVector4();
            if (JobThemePalette.TryGetSkylineDefaultColor(entry.JobName, out var skyline)
                && ColorsApproximatelyEqual(current, skyline))
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            configured.Set(current);
        }
    }

    private void MigrateThemeBarColorsToIkegamiSoftDefaults()
    {
        ThemeBarColors ??= new Dictionary<string, ThemeBarColorSetting>();

        foreach (var entry in JobThemePalette.Entries)
        {
            if (!ThemeBarColors.TryGetValue(entry.JobName, out var configured) || configured == null)
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            var current = configured.ToVector4();
            if (JobThemePalette.TryGetIkegamiOpaqueDefaultColor(entry.JobName, out var opaque)
                && ColorsApproximatelyEqual(current, opaque))
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            configured.Set(current);
        }
    }

    private void MigrateThemeBarColorsToIkegamiSofterDefaults()
    {
        ThemeBarColors ??= new Dictionary<string, ThemeBarColorSetting>();

        foreach (var entry in JobThemePalette.Entries)
        {
            if (!ThemeBarColors.TryGetValue(entry.JobName, out var configured) || configured == null)
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            var current = configured.ToVector4();
            if (JobThemePalette.TryGetPreviousIkegamiSoftDefaultColor(entry.JobName, out var soft)
                && ColorsApproximatelyEqual(current, soft))
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            configured.Set(current);
        }
    }

    private void MigrateThemeBarColorsToFineTunedDefaults()
    {
        ThemeBarColors ??= new Dictionary<string, ThemeBarColorSetting>();

        foreach (var entry in JobThemePalette.Entries)
        {
            if (!ThemeBarColors.TryGetValue(entry.JobName, out var configured) || configured == null)
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            var current = configured.ToVector4();
            if (JobThemePalette.TryGetPreviousTunedDefaultColor(entry.JobName, out var previous)
                && ColorsApproximatelyEqual(current, previous))
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            configured.Set(current);
        }
    }

    private void MigrateThemeBarColorsToAstDistinctDefaults()
    {
        ThemeBarColors ??= new Dictionary<string, ThemeBarColorSetting>();

        foreach (var entry in JobThemePalette.Entries)
        {
            if (!ThemeBarColors.TryGetValue(entry.JobName, out var configured) || configured == null)
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            var current = configured.ToVector4();
            if (JobThemePalette.TryGetPreviousAstDefaultColor(entry.JobName, out var previousAst)
                && ColorsApproximatelyEqual(current, previousAst))
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            configured.Set(current);
        }
    }

    private void MigrateThemeBarColorsToSelectedHealerDefaults()
    {
        ThemeBarColors ??= new Dictionary<string, ThemeBarColorSetting>();

        foreach (var entry in JobThemePalette.Entries)
        {
            if (!ThemeBarColors.TryGetValue(entry.JobName, out var configured) || configured == null)
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            var current = configured.ToVector4();
            if (JobThemePalette.TryGetPreviousHealerDefaultColor(entry.JobName, out var previousHealer)
                && ColorsApproximatelyEqual(current, previousHealer))
            {
                ThemeBarColors[entry.JobName] = new ThemeBarColorSetting(entry.DefaultColor);
                continue;
            }

            configured.Set(current);
        }
    }

    private static bool ColorsApproximatelyEqual(Vector4 left, Vector4 right, float epsilon = 0.001f)
        => Math.Abs(left.X - right.X) <= epsilon
           && Math.Abs(left.Y - right.Y) <= epsilon
           && Math.Abs(left.Z - right.Z) <= epsilon
           && Math.Abs(left.W - right.W) <= epsilon;

    private Vector4 ApplyThemeBarOpacity(Vector4 color)
        => new(color.X, color.Y, color.Z, ThemeBarOpacity);
}
