using System.Collections.Generic;

namespace DalamudACT;

public sealed partial class PluginConfiguration
{
    public void Reset()
    {
        WindowOpacity = 1f;
        FloatingStatsOpacity = 0.72f;
        ShowStatsPanel = true;
        LockFloatingStatsWindow = false;
        FloatingStatsDisplayStyle = FloatingStatsDisplayStyle.Minimal;
        FloatingStatsParticipantDisplayMode = FloatingStatsParticipantDisplayMode.Auto;
        HostileNpcMinHpMultiplier = 10;
        HighlightNpcRows = true;
        ShowDemoPanel = true;
        CombatEndRule = CombatEndRule.PartyList;
        EncounterTimeoutSeconds = 30;
        HistoryPreviewSeconds = 8;
        CombatTimelineRecordingEnabled = false;
        CombatTimelineMaxEntries = 500;
        ShowTimelineWindow = false;
        LockTimelineWindow = false;
        TimelineDebugMode = false;
        TimelineRawPacketDebug = false;
        TimelineRawPacketOpcodeFilter = "0095,025F,0251,015A";
        TimelineRawPacketPreviewBytes = 256;
        TimelineWindowOpacity = 0.9f;
        TimelineVisibleSeconds = 90;
        TimelineMaxVisibleEntries = 8;
        TimelineRowGap = 1f;
        EnableTimelineDailyRoutinesTts = false;
        TimelineTtsMechanic = true;
        TimelineTtsSkillName = true;
        TimelineTtsResponse = true;
        TimelineTtsLeadSeconds = 5;
        TimelineTtsCorrections = new List<TtsCorrectionRule>
        {
            new TtsCorrectionRule { From = "AOE", To = "诶欧意", Enabled = true },
            new TtsCorrectionRule { From = "地火", To = "帝火", Enabled = true },
            new TtsCorrectionRule { From = "地动", To = "帝动", Enabled = true },
            new TtsCorrectionRule { From = "--middle--", To = "回到中间", Enabled = true },
            new TtsCorrectionRule { From = "--north--", To = "去北侧", Enabled = true },
            new TtsCorrectionRule { From = "--south--", To = "去南侧", Enabled = true },
            new TtsCorrectionRule { From = "--east--", To = "去东侧", Enabled = true },
            new TtsCorrectionRule { From = "--west--", To = "去西侧", Enabled = true },
            new TtsCorrectionRule { From = "--untargetable--", To = "无法选中", Enabled = true },
            new TtsCorrectionRule { From = "--targetable--", To = "可选中", Enabled = true },
            new TtsCorrectionRule { From = "--adds targetable--", To = "小怪可选中", Enabled = true },
        };
        EnableDebugLog = LogHelper.DefaultEnableDebugLog;
        EnabledDebugLogModules = LogHelper.DefaultDebugLogModules;
        LogChannel = PluginLogChannel.Info;
        StatusObserver = new StatusObserverConfig();
        ShowDpsTab = true;
        ShowHpsTab = true;
        ShowTakenTab = true;
        ShowOverviewTab = true;
        ShowHistoryTab = true;
        ShowDpsPlayerColumn = true;
        ShowDpsJobColumn = false;
        ShowDpsDamageColumn = false;
        ShowDpsValueColumn = true;
        ShowDpsDeathsColumn = true;
        ShowHpsPlayerColumn = true;
        ShowHpsJobColumn = false;
        ShowHpsHealColumn = false;
        ShowHpsValueColumn = true;
        ShowTakenPlayerColumn = true;
        ShowTakenJobColumn = false;
        ShowTakenDamageColumn = false;
        ShowTakenValueColumn = true;
        DpsVisibleCount = 9;
        FloatingStatsPlayerColumnMinWidth = 0f;
        FloatingStatsMetricColumnWidth = 48f;
        FloatingStatsPlayerColumnWidth = 62f;
        FloatingStatsJobColumnWidth = 44f;
        FloatingStatsDamageColumnWidth = 73f;
        FloatingStatsValueColumnWidth = 48f;
        FloatingStatsDeathsColumnWidth = 24f;
        HistoryStartTimeColumnWidth = 100f;
        HistoryEndTimeColumnWidth = 100f;
        HistoryDurationColumnWidth = 100f;
        FloatingStatsRowHeight = 0f;
        FloatingStatsIkegamiMinimalMode = false;
        FloatingStatsIkegamiPanelRaise = 7f;
        FloatingStatsIkegamiDetailRaise = 5f;
        FloatingStatsIkegamiFooterRaise = 24f;
        FloatingStatsIkegamiShowScrollbar = false;
        FloatingStatsIkegamiBoxWidth = 132f;
        FloatingStatsIkegamiBoxHeight = 40f;
        FloatingStatsIkegamiNameHeight = 20f;
        FloatingStatsIkegamiHeaderHeight = 24f;
        FloatingStatsIkegamiHeaderLeftPadding = 8f;
        FloatingStatsIkegamiDetailLeftPadding = 8f;
        FloatingStatsIkegamiShowMaxHitDetail = false;
        FloatingStatsIkegamiShowVerticalScrollbar = false;
        FloatingStatsIkegamiShowNameLine = true;
        FloatingStatsIkegamiNameAlpha = 1f;
        FloatingStatsIkegamiHeaderAlpha = 1f;
        FloatingStatsIkegamiPanelBackgroundAlpha = 1f;
        FloatingStatsIkegamiBodyAlpha = 1f;
        FloatingStatsIkegamiFooterAlpha = 1f;
        FloatingStatsIkegamiNameLeftPadding = 40f;
        FloatingStatsIkegamiNameRightPadding = 0f;
        FloatingStatsIkegamiJobBadgeSize = 20f;
        FloatingStatsIkegamiFooterHeight = 24f;
        FloatingStatsIkegamiFooterTimeZoneSpacing = 15f;
        FloatingStatsIkegamiFooterRightPadding = 20f;
        FloatingStatsIkegamiNameBackgroundAlpha = 0f;
        FloatingStatsIkegamiBodyBackgroundAlpha = 0f;
        FloatingStatsIkegamiContentBackgroundAlpha = 0.3f;
        FloatingStatsIkegamiTabFontScale = 1f;
        FloatingStatsIkegamiNameFontScale = 1f;
        FloatingStatsIkegamiHeaderFontScale = 1f;
        FloatingStatsIkegamiBodyFontScale = 1f;
        FloatingStatsIkegamiFooterFontScale = 1f;
        FloatingStatsIkegamiTooltipFontScale = 1f;
        FloatingStatsIkegamiBoxAlignment = IkegamiBoxAlignment.Center;
        FloatingStatsMinimalShowHeader = false;
        FloatingStatsMinimalShowSummaryRow = true;
        FloatingStatsMinimalShowPlayerColumn = false;
        FloatingStatsMinimalShowPlayerNameInShareBar = false;
        FloatingStatsMinimalShowJobInShareBar = true;
        FloatingStatsMinimalShowDamageInShareBar = false;
        FloatingStatsMinimalShowDeathsInShareBar = false;
        FloatingStatsMinimalShowRatioInShareBar = false;
        FloatingStatsMinimalShowDamageColumn = false;
        FloatingStatsMinimalShowDeathsColumn = false;
        FloatingStatsMinimalShowDurationInSummaryBar = true;
        FloatingStatsMinimalShowTitleInSummaryBar = true;
        FloatingStatsMinimalShowDpsInSummaryBar = true;
        FloatingStatsMinimalShowDamageInSummaryBar = false;
        FloatingStatsMinimalShowDeathsInSummaryBar = false;
        FloatingStatsMinimalAutoWindowHeight = false;
        FloatingStatsMinimalRowHeight = 20f;
        FloatingStatsMinimalFontScale = 1f;
        FloatingStatsMinimalPlayerColumnWidth = 51f;
        FloatingStatsMinimalDamageColumnWidth = 88f;
        FloatingStatsMinimalDeathsColumnWidth = 32f;
        FloatingStatsClassicWindowWidth = 300f;
        FloatingStatsClassicWindowHeight = 300f;
        FloatingStatsIkegamiWindowWidth = 1139f;
        FloatingStatsIkegamiWindowHeight = 110f;
        FloatingStatsMinimalWindowWidth = 186f;
        FloatingStatsMinimalWindowHeight = 207f;
        BarColorMode = StatsBarColorMode.Theme;
        SingleBarColorR = 0.25f;
        SingleBarColorG = 0.65f;
        SingleBarColorB = 1f;
        SingleBarColorA = 0.9f;
        ThemeBarOpacity = DefaultThemeBarOpacity;
        ResetThemeBarColors();
        HighlightSelfBar = false;
        SelfHighlightColor = SelfHighlightColorMode.SunlightYellow;
        CustomFriendlyNpcNames = new List<string>();
        LogHelper.EnableDebugLog = EnableDebugLog;
        LogHelper.EnabledDebugLogModules = EnabledDebugLogModules;
        LogHelper.Channel = LogChannel;
    }
}
