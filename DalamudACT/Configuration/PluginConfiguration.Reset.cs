using System.Collections.Generic;
using System.Numerics;

namespace DalamudACT;

public sealed partial class PluginConfiguration
{
    public UiSettingsSnapshot? SavedUiSettings;

    public void SaveCurrentUiAsDefault()
    {
        SavedUiSettings = new UiSettingsSnapshot
        {
            WindowOpacity = WindowOpacity,
            FloatingStatsOpacity = FloatingStatsOpacity,
            ShowStatsPanel = ShowStatsPanel,
            LockFloatingStatsWindow = LockFloatingStatsWindow,
            FloatingStatsDisplayStyle = FloatingStatsDisplayStyle,
            FloatingStatsParticipantDisplayMode = FloatingStatsParticipantDisplayMode,
            ShowDemoPanel = ShowDemoPanel ?? true,
            ShowTimelineWindow = ShowTimelineWindow,
            LockTimelineWindow = LockTimelineWindow,
            TimelineWindowOpacity = TimelineWindowOpacity,
            TimelineVisibleSeconds = TimelineVisibleSeconds,
            TimelineMaxVisibleEntries = TimelineMaxVisibleEntries,
            TimelineRowGap = TimelineRowGap,
            EnableTimelineDailyRoutinesTts = EnableTimelineDailyRoutinesTts,
            TimelineTtsMechanic = TimelineTtsMechanic,
            TimelineTtsSkillName = TimelineTtsSkillName,
            TimelineTtsResponse = TimelineTtsResponse,
            TankInvulnerabilityTts = TankInvulnerabilityTts,
            TimelineTtsLeadSeconds = TimelineTtsLeadSeconds,
            TimelineTtsCorrections = new List<TtsCorrectionRule>(TimelineTtsCorrections),
            TimelineAutoDownloadOnEnter = TimelineAutoDownloadOnEnter,
            HighlightNpcRows = HighlightNpcRows,
            StatusObserver = new StatusObserverConfig
            {
                ShowWindow = StatusObserver.ShowWindow,
                LockWindow = StatusObserver.LockWindow,
                WindowOpacity = StatusObserver.WindowOpacity,
                DisplayMode = StatusObserver.DisplayMode,
                ShowSelfStatuses = StatusObserver.ShowSelfStatuses,
                ShowTargetStatuses = StatusObserver.ShowTargetStatuses,
                HidePermanentStatuses = StatusObserver.HidePermanentStatuses,
                ShowSourceInfo = StatusObserver.ShowSourceInfo,
                ShowStatusIdUnderIcon = StatusObserver.ShowStatusIdUnderIcon,
                SelfMaxStatuses = StatusObserver.SelfMaxStatuses,
                TargetMaxStatuses = StatusObserver.TargetMaxStatuses,
                FavoriteStatusIds = new List<uint>(StatusObserver.FavoriteStatusIds),
            },
            ShowDpsTab = ShowDpsTab,
            ShowHpsTab = ShowHpsTab,
            ShowTakenTab = ShowTakenTab,
            ShowOverviewTab = ShowOverviewTab,
            ShowHistoryTab = ShowHistoryTab,
            ShowDpsPlayerColumn = ShowDpsPlayerColumn,
            ShowDpsJobColumn = ShowDpsJobColumn,
            ShowDpsDamageColumn = ShowDpsDamageColumn,
            ShowDpsValueColumn = ShowDpsValueColumn,
            ShowDpsDeathsColumn = ShowDpsDeathsColumn,
            ShowHpsPlayerColumn = ShowHpsPlayerColumn,
            ShowHpsJobColumn = ShowHpsJobColumn,
            ShowHpsHealColumn = ShowHpsHealColumn,
            ShowHpsValueColumn = ShowHpsValueColumn,
            ShowTakenPlayerColumn = ShowTakenPlayerColumn,
            ShowTakenJobColumn = ShowTakenJobColumn,
            ShowTakenDamageColumn = ShowTakenDamageColumn,
            ShowTakenValueColumn = ShowTakenValueColumn,
            DpsVisibleCount = DpsVisibleCount,
            FloatingStatsPlayerColumnMinWidth = FloatingStatsPlayerColumnMinWidth,
            FloatingStatsMetricColumnWidth = FloatingStatsMetricColumnWidth,
            FloatingStatsPlayerColumnWidth = FloatingStatsPlayerColumnWidth,
            FloatingStatsJobColumnWidth = FloatingStatsJobColumnWidth,
            FloatingStatsDamageColumnWidth = FloatingStatsDamageColumnWidth,
            FloatingStatsValueColumnWidth = FloatingStatsValueColumnWidth,
            FloatingStatsDeathsColumnWidth = FloatingStatsDeathsColumnWidth,
            HistoryStartTimeColumnWidth = HistoryStartTimeColumnWidth,
            HistoryEndTimeColumnWidth = HistoryEndTimeColumnWidth,
            HistoryDurationColumnWidth = HistoryDurationColumnWidth,
            FloatingStatsRowHeight = FloatingStatsRowHeight,
            FloatingStatsIkegamiMinimalMode = FloatingStatsIkegamiMinimalMode,
            FloatingStatsIkegamiPanelRaise = FloatingStatsIkegamiPanelRaise,
            FloatingStatsIkegamiDetailRaise = FloatingStatsIkegamiDetailRaise,
            FloatingStatsIkegamiFooterRaise = FloatingStatsIkegamiFooterRaise,
            FloatingStatsIkegamiShowScrollbar = FloatingStatsIkegamiShowScrollbar,
            FloatingStatsIkegamiBoxWidth = FloatingStatsIkegamiBoxWidth,
            FloatingStatsIkegamiBoxHeight = FloatingStatsIkegamiBoxHeight,
            FloatingStatsIkegamiNameHeight = FloatingStatsIkegamiNameHeight,
            FloatingStatsIkegamiHeaderHeight = FloatingStatsIkegamiHeaderHeight,
            FloatingStatsIkegamiHeaderLeftPadding = FloatingStatsIkegamiHeaderLeftPadding,
            FloatingStatsIkegamiDetailLeftPadding = FloatingStatsIkegamiDetailLeftPadding,
            FloatingStatsIkegamiShowMaxHitDetail = FloatingStatsIkegamiShowMaxHitDetail,
            FloatingStatsIkegamiShowVerticalScrollbar = FloatingStatsIkegamiShowVerticalScrollbar,
            FloatingStatsIkegamiShowNameLine = FloatingStatsIkegamiShowNameLine,
            FloatingStatsIkegamiNameAlpha = FloatingStatsIkegamiNameAlpha,
            FloatingStatsIkegamiHeaderAlpha = FloatingStatsIkegamiHeaderAlpha,
            FloatingStatsIkegamiPanelBackgroundAlpha = FloatingStatsIkegamiPanelBackgroundAlpha,
            FloatingStatsIkegamiBodyAlpha = FloatingStatsIkegamiBodyAlpha,
            FloatingStatsIkegamiFooterAlpha = FloatingStatsIkegamiFooterAlpha,
            FloatingStatsIkegamiNameLeftPadding = FloatingStatsIkegamiNameLeftPadding,
            FloatingStatsIkegamiNameRightPadding = FloatingStatsIkegamiNameRightPadding,
            FloatingStatsIkegamiJobBadgeSize = FloatingStatsIkegamiJobBadgeSize,
            FloatingStatsIkegamiFooterHeight = FloatingStatsIkegamiFooterHeight,
            FloatingStatsIkegamiFooterTimeZoneSpacing = FloatingStatsIkegamiFooterTimeZoneSpacing,
            FloatingStatsIkegamiFooterRightPadding = FloatingStatsIkegamiFooterRightPadding,
            FloatingStatsIkegamiNameBackgroundAlpha = FloatingStatsIkegamiNameBackgroundAlpha,
            FloatingStatsIkegamiBodyBackgroundAlpha = FloatingStatsIkegamiBodyBackgroundAlpha,
            FloatingStatsIkegamiContentBackgroundAlpha = FloatingStatsIkegamiContentBackgroundAlpha,
            FloatingStatsIkegamiTabFontScale = FloatingStatsIkegamiTabFontScale,
            FloatingStatsIkegamiNameFontScale = FloatingStatsIkegamiNameFontScale,
            FloatingStatsIkegamiHeaderFontScale = FloatingStatsIkegamiHeaderFontScale,
            FloatingStatsIkegamiBodyFontScale = FloatingStatsIkegamiBodyFontScale,
            FloatingStatsIkegamiFooterFontScale = FloatingStatsIkegamiFooterFontScale,
            FloatingStatsIkegamiTooltipFontScale = FloatingStatsIkegamiTooltipFontScale,
            FloatingStatsIkegamiBoxAlignment = FloatingStatsIkegamiBoxAlignment,
            FloatingStatsMinimalShowHeader = FloatingStatsMinimalShowHeader,
            FloatingStatsMinimalShowSummaryRow = FloatingStatsMinimalShowSummaryRow,
            FloatingStatsMinimalShowPlayerColumn = FloatingStatsMinimalShowPlayerColumn,
            FloatingStatsMinimalShowPlayerNameInShareBar = FloatingStatsMinimalShowPlayerNameInShareBar,
            FloatingStatsMinimalShowJobInShareBar = FloatingStatsMinimalShowJobInShareBar,
            FloatingStatsMinimalShowDamageInShareBar = FloatingStatsMinimalShowDamageInShareBar,
            FloatingStatsMinimalShowDeathsInShareBar = FloatingStatsMinimalShowDeathsInShareBar,
            FloatingStatsMinimalShowRatioInShareBar = FloatingStatsMinimalShowRatioInShareBar,
            FloatingStatsMinimalShowDamageColumn = FloatingStatsMinimalShowDamageColumn,
            FloatingStatsMinimalShowDeathsColumn = FloatingStatsMinimalShowDeathsColumn,
            FloatingStatsMinimalShowDurationInSummaryBar = FloatingStatsMinimalShowDurationInSummaryBar,
            FloatingStatsMinimalShowTitleInSummaryBar = FloatingStatsMinimalShowTitleInSummaryBar,
            FloatingStatsMinimalShowDpsInSummaryBar = FloatingStatsMinimalShowDpsInSummaryBar,
            FloatingStatsMinimalShowDamageInSummaryBar = FloatingStatsMinimalShowDamageInSummaryBar,
            FloatingStatsMinimalShowDeathsInSummaryBar = FloatingStatsMinimalShowDeathsInSummaryBar,
            FloatingStatsMinimalAutoWindowHeight = FloatingStatsMinimalAutoWindowHeight,
            FloatingStatsMinimalRowHeight = FloatingStatsMinimalRowHeight,
            FloatingStatsMinimalFontScale = FloatingStatsMinimalFontScale,
            FloatingStatsMinimalPlayerColumnWidth = FloatingStatsMinimalPlayerColumnWidth,
            FloatingStatsMinimalDamageColumnWidth = FloatingStatsMinimalDamageColumnWidth,
            FloatingStatsMinimalDeathsColumnWidth = FloatingStatsMinimalDeathsColumnWidth,
            FloatingStatsClassicWindowWidth = FloatingStatsClassicWindowWidth,
            FloatingStatsClassicWindowHeight = FloatingStatsClassicWindowHeight,
            FloatingStatsIkegamiWindowWidth = FloatingStatsIkegamiWindowWidth,
            FloatingStatsIkegamiWindowHeight = FloatingStatsIkegamiWindowHeight,
            FloatingStatsMinimalWindowWidth = FloatingStatsMinimalWindowWidth,
            FloatingStatsMinimalWindowHeight = FloatingStatsMinimalWindowHeight,
            BarColorMode = BarColorMode,
            SingleBarColorR = SingleBarColorR,
            SingleBarColorG = SingleBarColorG,
            SingleBarColorB = SingleBarColorB,
            SingleBarColorA = SingleBarColorA,
            ThemeBarOpacity = ThemeBarOpacity,
            HighlightSelfBar = HighlightSelfBar,
            SelfHighlightColor = SelfHighlightColor,
            CustomFriendlyNpcNames = new List<string>(CustomFriendlyNpcNames),
            PartyMonitor = new PartyMonitorConfig
            {
                EnablePartyMonitor = PartyMonitor.EnablePartyMonitor,
                ShowPartyMonitorWindow = PartyMonitor.ShowPartyMonitorWindow,
                PartyMonitorOpacity = PartyMonitor.PartyMonitorOpacity,
                LockPartyMonitorWindow = PartyMonitor.LockPartyMonitorWindow,
                AutoResizePartyMonitorWindow = PartyMonitor.AutoResizePartyMonitorWindow,
                MonitorFood = PartyMonitor.MonitorFood,
                FoodExpiryWarningMinutes = PartyMonitor.FoodExpiryWarningMinutes,
                MonitorRaidBuffs = PartyMonitor.MonitorRaidBuffs,
                MonitorMitigations = PartyMonitor.MonitorMitigations,
                AnonymousMode = PartyMonitor.AnonymousMode,
                HideSkillsOnCooldown = PartyMonitor.HideSkillsOnCooldown,
                MergeSkillGroups = PartyMonitor.MergeSkillGroups,
                HideNameColumn = PartyMonitor.HideNameColumn,
                IconSize = PartyMonitor.IconSize,
                CountdownTextScale = PartyMonitor.CountdownTextScale,
                CountdownTextColor = PartyMonitor.CountdownTextColor,
                CountdownTextBottomCenter = PartyMonitor.CountdownTextBottomCenter,
                EnhancedActiveStyle = PartyMonitor.EnhancedActiveStyle,
                ActiveGlowStrength = PartyMonitor.ActiveGlowStrength,
                IconGap = PartyMonitor.IconGap,
                RowGap = PartyMonitor.RowGap,
                BackgroundColor = PartyMonitor.BackgroundColor,
            },
            SelectedUiTheme = SelectedUiTheme,
        };
    }

    private void ApplyUiSettingsSnapshot(UiSettingsSnapshot snap)
    {
        WindowOpacity = snap.WindowOpacity;
        FloatingStatsOpacity = snap.FloatingStatsOpacity;
        ShowStatsPanel = snap.ShowStatsPanel;
        LockFloatingStatsWindow = snap.LockFloatingStatsWindow;
        FloatingStatsDisplayStyle = snap.FloatingStatsDisplayStyle;
        FloatingStatsParticipantDisplayMode = snap.FloatingStatsParticipantDisplayMode;
        ShowDemoPanel = snap.ShowDemoPanel;
        ShowTimelineWindow = snap.ShowTimelineWindow;
        LockTimelineWindow = snap.LockTimelineWindow;
        TimelineWindowOpacity = snap.TimelineWindowOpacity;
        TimelineVisibleSeconds = snap.TimelineVisibleSeconds;
        TimelineMaxVisibleEntries = snap.TimelineMaxVisibleEntries;
        TimelineRowGap = snap.TimelineRowGap;
        EnableTimelineDailyRoutinesTts = snap.EnableTimelineDailyRoutinesTts;
        TimelineTtsMechanic = snap.TimelineTtsMechanic;
        TimelineTtsSkillName = snap.TimelineTtsSkillName;
        TimelineTtsResponse = snap.TimelineTtsResponse;
        TankInvulnerabilityTts = snap.TankInvulnerabilityTts;
        TimelineTtsLeadSeconds = snap.TimelineTtsLeadSeconds;
        TimelineTtsCorrections = new List<TtsCorrectionRule>(snap.TimelineTtsCorrections);
        TimelineAutoDownloadOnEnter = snap.TimelineAutoDownloadOnEnter;
        HighlightNpcRows = snap.HighlightNpcRows;
        StatusObserver = new StatusObserverConfig
        {
            ShowWindow = snap.StatusObserver.ShowWindow,
            LockWindow = snap.StatusObserver.LockWindow,
            WindowOpacity = snap.StatusObserver.WindowOpacity,
            DisplayMode = snap.StatusObserver.DisplayMode,
            ShowSelfStatuses = snap.StatusObserver.ShowSelfStatuses,
            ShowTargetStatuses = snap.StatusObserver.ShowTargetStatuses,
            HidePermanentStatuses = snap.StatusObserver.HidePermanentStatuses,
            ShowSourceInfo = snap.StatusObserver.ShowSourceInfo,
            ShowStatusIdUnderIcon = snap.StatusObserver.ShowStatusIdUnderIcon,
            SelfMaxStatuses = snap.StatusObserver.SelfMaxStatuses,
            TargetMaxStatuses = snap.StatusObserver.TargetMaxStatuses,
            FavoriteStatusIds = new List<uint>(snap.StatusObserver.FavoriteStatusIds),
        };
        ShowDpsTab = snap.ShowDpsTab;
        ShowHpsTab = snap.ShowHpsTab;
        ShowTakenTab = snap.ShowTakenTab;
        ShowOverviewTab = snap.ShowOverviewTab;
        ShowHistoryTab = snap.ShowHistoryTab;
        ShowDpsPlayerColumn = snap.ShowDpsPlayerColumn;
        ShowDpsJobColumn = snap.ShowDpsJobColumn;
        ShowDpsDamageColumn = snap.ShowDpsDamageColumn;
        ShowDpsValueColumn = snap.ShowDpsValueColumn;
        ShowDpsDeathsColumn = snap.ShowDpsDeathsColumn;
        ShowHpsPlayerColumn = snap.ShowHpsPlayerColumn;
        ShowHpsJobColumn = snap.ShowHpsJobColumn;
        ShowHpsHealColumn = snap.ShowHpsHealColumn;
        ShowHpsValueColumn = snap.ShowHpsValueColumn;
        ShowTakenPlayerColumn = snap.ShowTakenPlayerColumn;
        ShowTakenJobColumn = snap.ShowTakenJobColumn;
        ShowTakenDamageColumn = snap.ShowTakenDamageColumn;
        ShowTakenValueColumn = snap.ShowTakenValueColumn;
        DpsVisibleCount = snap.DpsVisibleCount;
        FloatingStatsPlayerColumnMinWidth = snap.FloatingStatsPlayerColumnMinWidth;
        FloatingStatsMetricColumnWidth = snap.FloatingStatsMetricColumnWidth;
        FloatingStatsPlayerColumnWidth = snap.FloatingStatsPlayerColumnWidth;
        FloatingStatsJobColumnWidth = snap.FloatingStatsJobColumnWidth;
        FloatingStatsDamageColumnWidth = snap.FloatingStatsDamageColumnWidth;
        FloatingStatsValueColumnWidth = snap.FloatingStatsValueColumnWidth;
        FloatingStatsDeathsColumnWidth = snap.FloatingStatsDeathsColumnWidth;
        HistoryStartTimeColumnWidth = snap.HistoryStartTimeColumnWidth;
        HistoryEndTimeColumnWidth = snap.HistoryEndTimeColumnWidth;
        HistoryDurationColumnWidth = snap.HistoryDurationColumnWidth;
        FloatingStatsRowHeight = snap.FloatingStatsRowHeight;
        FloatingStatsIkegamiMinimalMode = snap.FloatingStatsIkegamiMinimalMode;
        FloatingStatsIkegamiPanelRaise = snap.FloatingStatsIkegamiPanelRaise;
        FloatingStatsIkegamiDetailRaise = snap.FloatingStatsIkegamiDetailRaise;
        FloatingStatsIkegamiFooterRaise = snap.FloatingStatsIkegamiFooterRaise;
        FloatingStatsIkegamiShowScrollbar = snap.FloatingStatsIkegamiShowScrollbar;
        FloatingStatsIkegamiBoxWidth = snap.FloatingStatsIkegamiBoxWidth;
        FloatingStatsIkegamiBoxHeight = snap.FloatingStatsIkegamiBoxHeight;
        FloatingStatsIkegamiNameHeight = snap.FloatingStatsIkegamiNameHeight;
        FloatingStatsIkegamiHeaderHeight = snap.FloatingStatsIkegamiHeaderHeight;
        FloatingStatsIkegamiHeaderLeftPadding = snap.FloatingStatsIkegamiHeaderLeftPadding;
        FloatingStatsIkegamiDetailLeftPadding = snap.FloatingStatsIkegamiDetailLeftPadding;
        FloatingStatsIkegamiShowMaxHitDetail = snap.FloatingStatsIkegamiShowMaxHitDetail;
        FloatingStatsIkegamiShowVerticalScrollbar = snap.FloatingStatsIkegamiShowVerticalScrollbar;
        FloatingStatsIkegamiShowNameLine = snap.FloatingStatsIkegamiShowNameLine;
        FloatingStatsIkegamiNameAlpha = snap.FloatingStatsIkegamiNameAlpha;
        FloatingStatsIkegamiHeaderAlpha = snap.FloatingStatsIkegamiHeaderAlpha;
        FloatingStatsIkegamiPanelBackgroundAlpha = snap.FloatingStatsIkegamiPanelBackgroundAlpha;
        FloatingStatsIkegamiBodyAlpha = snap.FloatingStatsIkegamiBodyAlpha;
        FloatingStatsIkegamiFooterAlpha = snap.FloatingStatsIkegamiFooterAlpha;
        FloatingStatsIkegamiNameLeftPadding = snap.FloatingStatsIkegamiNameLeftPadding;
        FloatingStatsIkegamiNameRightPadding = snap.FloatingStatsIkegamiNameRightPadding;
        FloatingStatsIkegamiJobBadgeSize = snap.FloatingStatsIkegamiJobBadgeSize;
        FloatingStatsIkegamiFooterHeight = snap.FloatingStatsIkegamiFooterHeight;
        FloatingStatsIkegamiFooterTimeZoneSpacing = snap.FloatingStatsIkegamiFooterTimeZoneSpacing;
        FloatingStatsIkegamiFooterRightPadding = snap.FloatingStatsIkegamiFooterRightPadding;
        FloatingStatsIkegamiNameBackgroundAlpha = snap.FloatingStatsIkegamiNameBackgroundAlpha;
        FloatingStatsIkegamiBodyBackgroundAlpha = snap.FloatingStatsIkegamiBodyBackgroundAlpha;
        FloatingStatsIkegamiContentBackgroundAlpha = snap.FloatingStatsIkegamiContentBackgroundAlpha;
        FloatingStatsIkegamiTabFontScale = snap.FloatingStatsIkegamiTabFontScale;
        FloatingStatsIkegamiNameFontScale = snap.FloatingStatsIkegamiNameFontScale;
        FloatingStatsIkegamiHeaderFontScale = snap.FloatingStatsIkegamiHeaderFontScale;
        FloatingStatsIkegamiBodyFontScale = snap.FloatingStatsIkegamiBodyFontScale;
        FloatingStatsIkegamiFooterFontScale = snap.FloatingStatsIkegamiFooterFontScale;
        FloatingStatsIkegamiTooltipFontScale = snap.FloatingStatsIkegamiTooltipFontScale;
        FloatingStatsIkegamiBoxAlignment = snap.FloatingStatsIkegamiBoxAlignment;
        FloatingStatsMinimalShowHeader = snap.FloatingStatsMinimalShowHeader;
        FloatingStatsMinimalShowSummaryRow = snap.FloatingStatsMinimalShowSummaryRow;
        FloatingStatsMinimalShowPlayerColumn = snap.FloatingStatsMinimalShowPlayerColumn;
        FloatingStatsMinimalShowPlayerNameInShareBar = snap.FloatingStatsMinimalShowPlayerNameInShareBar;
        FloatingStatsMinimalShowJobInShareBar = snap.FloatingStatsMinimalShowJobInShareBar;
        FloatingStatsMinimalShowDamageInShareBar = snap.FloatingStatsMinimalShowDamageInShareBar;
        FloatingStatsMinimalShowDeathsInShareBar = snap.FloatingStatsMinimalShowDeathsInShareBar;
        FloatingStatsMinimalShowRatioInShareBar = snap.FloatingStatsMinimalShowRatioInShareBar;
        FloatingStatsMinimalShowDamageColumn = snap.FloatingStatsMinimalShowDamageColumn;
        FloatingStatsMinimalShowDeathsColumn = snap.FloatingStatsMinimalShowDeathsColumn;
        FloatingStatsMinimalShowDurationInSummaryBar = snap.FloatingStatsMinimalShowDurationInSummaryBar;
        FloatingStatsMinimalShowTitleInSummaryBar = snap.FloatingStatsMinimalShowTitleInSummaryBar;
        FloatingStatsMinimalShowDpsInSummaryBar = snap.FloatingStatsMinimalShowDpsInSummaryBar;
        FloatingStatsMinimalShowDamageInSummaryBar = snap.FloatingStatsMinimalShowDamageInSummaryBar;
        FloatingStatsMinimalShowDeathsInSummaryBar = snap.FloatingStatsMinimalShowDeathsInSummaryBar;
        FloatingStatsMinimalAutoWindowHeight = snap.FloatingStatsMinimalAutoWindowHeight;
        FloatingStatsMinimalRowHeight = snap.FloatingStatsMinimalRowHeight;
        FloatingStatsMinimalFontScale = snap.FloatingStatsMinimalFontScale;
        FloatingStatsMinimalPlayerColumnWidth = snap.FloatingStatsMinimalPlayerColumnWidth;
        FloatingStatsMinimalDamageColumnWidth = snap.FloatingStatsMinimalDamageColumnWidth;
        FloatingStatsMinimalDeathsColumnWidth = snap.FloatingStatsMinimalDeathsColumnWidth;
        FloatingStatsClassicWindowWidth = snap.FloatingStatsClassicWindowWidth;
        FloatingStatsClassicWindowHeight = snap.FloatingStatsClassicWindowHeight;
        FloatingStatsIkegamiWindowWidth = snap.FloatingStatsIkegamiWindowWidth;
        FloatingStatsIkegamiWindowHeight = snap.FloatingStatsIkegamiWindowHeight;
        FloatingStatsMinimalWindowWidth = snap.FloatingStatsMinimalWindowWidth;
        FloatingStatsMinimalWindowHeight = snap.FloatingStatsMinimalWindowHeight;
        BarColorMode = snap.BarColorMode;
        SingleBarColorR = snap.SingleBarColorR;
        SingleBarColorG = snap.SingleBarColorG;
        SingleBarColorB = snap.SingleBarColorB;
        SingleBarColorA = snap.SingleBarColorA;
        ThemeBarOpacity = snap.ThemeBarOpacity;
        HighlightSelfBar = snap.HighlightSelfBar;
        SelfHighlightColor = snap.SelfHighlightColor;
        CustomFriendlyNpcNames = new List<string>(snap.CustomFriendlyNpcNames);
        PartyMonitor = new PartyMonitorConfig
        {
            EnablePartyMonitor = snap.PartyMonitor.EnablePartyMonitor,
            ShowPartyMonitorWindow = snap.PartyMonitor.ShowPartyMonitorWindow,
            PartyMonitorOpacity = snap.PartyMonitor.PartyMonitorOpacity,
            LockPartyMonitorWindow = snap.PartyMonitor.LockPartyMonitorWindow,
            AutoResizePartyMonitorWindow = snap.PartyMonitor.AutoResizePartyMonitorWindow,
            MonitorFood = snap.PartyMonitor.MonitorFood,
            FoodExpiryWarningMinutes = snap.PartyMonitor.FoodExpiryWarningMinutes,
            MonitorRaidBuffs = snap.PartyMonitor.MonitorRaidBuffs,
            MonitorMitigations = snap.PartyMonitor.MonitorMitigations,
            AnonymousMode = snap.PartyMonitor.AnonymousMode,
            HideSkillsOnCooldown = snap.PartyMonitor.HideSkillsOnCooldown,
            MergeSkillGroups = snap.PartyMonitor.MergeSkillGroups,
            HideNameColumn = snap.PartyMonitor.HideNameColumn,
            IconSize = snap.PartyMonitor.IconSize,
            CountdownTextScale = snap.PartyMonitor.CountdownTextScale,
            CountdownTextColor = snap.PartyMonitor.CountdownTextColor,
            CountdownTextBottomCenter = snap.PartyMonitor.CountdownTextBottomCenter,
            EnhancedActiveStyle = snap.PartyMonitor.EnhancedActiveStyle,
            ActiveGlowStrength = snap.PartyMonitor.ActiveGlowStrength,
            IconGap = snap.PartyMonitor.IconGap,
            RowGap = snap.PartyMonitor.RowGap,
            BackgroundColor = snap.PartyMonitor.BackgroundColor,
        };
        SelectedUiTheme = snap.SelectedUiTheme;
    }

    public void ResetUiSettings()
    {
        WindowOpacity = 1f;
        FloatingStatsOpacity = 0.4f;
        ShowStatsPanel = true;
        LockFloatingStatsWindow = false;
        FloatingStatsDisplayStyle = FloatingStatsDisplayStyle.Minimal;
        FloatingStatsParticipantDisplayMode = FloatingStatsParticipantDisplayMode.Auto;
        ShowDemoPanel = true;
        ShowTimelineWindow = false;
        LockTimelineWindow = false;
        TimelineWindowOpacity = 0.4f;
        TimelineVisibleSeconds = 90;
        TimelineMaxVisibleEntries = 8;
        TimelineRowGap = 1f;
        EnableTimelineDailyRoutinesTts = false;
        TimelineForceLoadPath = string.Empty;
        TimelineTtsMechanic = true;
        TimelineTtsSkillName = true;
        TimelineTtsResponse = true;
        TankInvulnerabilityTts = true;
        TimelineTtsLeadSeconds = 5;
        TimelineTtsCorrections = new List<TtsCorrectionRule>
        {
            new TtsCorrectionRule { From = "AOE", To = "诶欧意", Enabled = true },
            new TtsCorrectionRule { From = "地火", To = "帝火", Enabled = true },
            new TtsCorrectionRule { From = "地动", To = "帝动", Enabled = true },
            new TtsCorrectionRule { From = "对地", To = "对帝", Enabled = true },
            new TtsCorrectionRule { From = "三重猛击", To = "三虫猛击", Enabled = true },
            new TtsCorrectionRule { From = "--middle--", To = "回到中间", Enabled = true },
            new TtsCorrectionRule { From = "--north--", To = "去北侧", Enabled = true },
            new TtsCorrectionRule { From = "--south--", To = "去南侧", Enabled = true },
            new TtsCorrectionRule { From = "--east--", To = "去东侧", Enabled = true },
            new TtsCorrectionRule { From = "--west--", To = "去西侧", Enabled = true },
            new TtsCorrectionRule { From = "--untargetable--", To = "无法选中", Enabled = true },
            new TtsCorrectionRule { From = "--targetable--", To = "可选中", Enabled = true },
            new TtsCorrectionRule { From = "--adds targetable--", To = "小怪可选中", Enabled = true },
        };
        StatusObserver = new StatusObserverConfig
        {
            WindowOpacity = 0.4f,
            DisplayMode = StatusObserverDisplayMode.Icon,
        };
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
        FloatingStatsIkegamiPanelRaise = 3f;
        FloatingStatsIkegamiDetailRaise = 3f;
        FloatingStatsIkegamiFooterRaise = 3f;
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
        FloatingStatsMinimalAutoWindowHeight = true;
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
        HighlightSelfBar = true;
        SelfHighlightColor = SelfHighlightColorMode.SunlightYellow;
        CustomFriendlyNpcNames = new List<string>();
        PartyMonitor = new PartyMonitorConfig
        {
            PartyMonitorOpacity = 0.4f,
            AutoResizePartyMonitorWindow = true,
        };
        TimelineAutoDownloadOnEnter = false;
        HighlightNpcRows = true;
        SelectedUiTheme = UiThemeId.Sakura;
        LogHelper.EnableDebugLog = EnableDebugLog;
        LogHelper.EnabledDebugLogModules = EnabledDebugLogModules;
        LogHelper.Channel = PluginLogChannel.Debug;
    }

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
        CombatTimelineRecordingEnabled = true;
        HighPerformanceMode = false;
        CombatTimelineLightweightMode = false;
        EnableDotAndWildfireAttribution = true;
        CombatTimelineMapEffectEnabled = false;
        CombatTimelineTargetIconEnabled = false;
        CombatTimelineTetherEnabled = false;
        CombatTimelineMaxEntries = 500;
        CombatTimelineShowRawTime = false;
        CombatTimelineShowEncounterTime = true;
        CombatTimelineAutoScroll = true;
        CombatTimelineCharacterFilter = string.Empty;
        CombatTimelineTextSearchFilter = string.Empty;
        CombatTimelineContentFilterMask = 0;
        ReplayStatsMode = false;
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
        TankInvulnerabilityTts = true;
        TimelineTtsLeadSeconds = 5;
        TimelineTtsCorrections = new List<TtsCorrectionRule>
        {
            new TtsCorrectionRule { From = "AOE", To = "诶欧意", Enabled = true },
            new TtsCorrectionRule { From = "地火", To = "帝火", Enabled = true },
            new TtsCorrectionRule { From = "地动", To = "帝动", Enabled = true },
            new TtsCorrectionRule { From = "对地", To = "对帝", Enabled = true },
            new TtsCorrectionRule { From = "三重猛击", To = "三虫猛击", Enabled = true },
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
        EnableEnhancedLog = false;
        LogChannel = PluginLogChannel.Debug;
        StatsUpdateIntervalMs = 250;
        PartyMonitorUpdateIntervalMs = 500;
        StatusObserverUpdateIntervalMs = 500;
        TimelineUpdateIntervalMs = 100;
        AutoRefreshIntervalByArea = true;
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
        FloatingStatsIkegamiPanelRaise = 3f;
        FloatingStatsIkegamiDetailRaise = 3f;
        FloatingStatsIkegamiFooterRaise = 3f;
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
        SelectedUiTheme = UiThemeId.Sakura;
        LogHelper.EnableDebugLog = EnableDebugLog;
        LogHelper.EnabledDebugLogModules = EnabledDebugLogModules;
        LogHelper.Channel = PluginLogChannel.Debug;
    }
}

public sealed class UiSettingsSnapshot
{
    public float WindowOpacity = 1f;
    public float FloatingStatsOpacity = 0.72f;
    public bool ShowStatsPanel = true;
    public bool LockFloatingStatsWindow;
    public FloatingStatsDisplayStyle FloatingStatsDisplayStyle = FloatingStatsDisplayStyle.Minimal;
    public FloatingStatsParticipantDisplayMode FloatingStatsParticipantDisplayMode = FloatingStatsParticipantDisplayMode.Auto;
    public bool ShowDemoPanel = true;
    public UiThemeId SelectedUiTheme = UiThemeId.Sakura;
    public bool ShowTimelineWindow;
    public bool LockTimelineWindow;
    public float TimelineWindowOpacity = 0.9f;
    public int TimelineVisibleSeconds = 90;
    public int TimelineMaxVisibleEntries = 8;
    public float TimelineRowGap = 1f;
    public bool EnableTimelineDailyRoutinesTts;
    public bool TimelineTtsMechanic = true;
    public bool TimelineTtsSkillName = true;
    public bool TimelineTtsResponse = true;
    public bool TankInvulnerabilityTts = true;
    public int TimelineTtsLeadSeconds = 5;
    public List<TtsCorrectionRule> TimelineTtsCorrections = new();
    public bool TimelineAutoDownloadOnEnter;
    public bool HighlightNpcRows = true;
    public StatusObserverConfig StatusObserver = new();
    public bool ShowDpsTab = true;
    public bool ShowHpsTab = true;
    public bool ShowTakenTab = true;
    public bool ShowOverviewTab = true;
    public bool ShowHistoryTab = true;
    public bool ShowDpsPlayerColumn = true;
    public bool ShowDpsJobColumn;
    public bool ShowDpsDamageColumn;
    public bool ShowDpsValueColumn = true;
    public bool ShowDpsDeathsColumn = true;
    public bool ShowHpsPlayerColumn = true;
    public bool ShowHpsJobColumn;
    public bool ShowHpsHealColumn;
    public bool ShowHpsValueColumn = true;
    public bool ShowTakenPlayerColumn = true;
    public bool ShowTakenJobColumn;
    public bool ShowTakenDamageColumn;
    public bool ShowTakenValueColumn = true;
    public int DpsVisibleCount = 9;
    public float FloatingStatsPlayerColumnMinWidth;
    public float FloatingStatsMetricColumnWidth = 48f;
    public float FloatingStatsPlayerColumnWidth = 62f;
    public float FloatingStatsJobColumnWidth = 44f;
    public float FloatingStatsDamageColumnWidth = 73f;
    public float FloatingStatsValueColumnWidth = 48f;
    public float FloatingStatsDeathsColumnWidth = 24f;
    public float HistoryStartTimeColumnWidth = 100f;
    public float HistoryEndTimeColumnWidth = 100f;
    public float HistoryDurationColumnWidth = 100f;
    public float FloatingStatsRowHeight;
    public bool FloatingStatsIkegamiMinimalMode;
    public float FloatingStatsIkegamiPanelRaise = 3f;
    public float FloatingStatsIkegamiDetailRaise = 3f;
    public float FloatingStatsIkegamiFooterRaise = 3f;
    public bool FloatingStatsIkegamiShowScrollbar;
    public float FloatingStatsIkegamiBoxWidth = 132f;
    public float FloatingStatsIkegamiBoxHeight = 40f;
    public float FloatingStatsIkegamiNameHeight = 20f;
    public float FloatingStatsIkegamiHeaderHeight = 24f;
    public float FloatingStatsIkegamiHeaderLeftPadding = 8f;
    public float FloatingStatsIkegamiDetailLeftPadding = 8f;
    public bool FloatingStatsIkegamiShowMaxHitDetail;
    public bool FloatingStatsIkegamiShowVerticalScrollbar;
    public bool FloatingStatsIkegamiShowNameLine = true;
    public float FloatingStatsIkegamiNameAlpha = 1f;
    public float FloatingStatsIkegamiHeaderAlpha = 1f;
    public float FloatingStatsIkegamiPanelBackgroundAlpha = 1f;
    public float FloatingStatsIkegamiBodyAlpha = 1f;
    public float FloatingStatsIkegamiFooterAlpha = 1f;
    public float FloatingStatsIkegamiNameLeftPadding = 40f;
    public float FloatingStatsIkegamiNameRightPadding;
    public float FloatingStatsIkegamiJobBadgeSize = 20f;
    public float FloatingStatsIkegamiFooterHeight = 24f;
    public float FloatingStatsIkegamiFooterTimeZoneSpacing = 15f;
    public float FloatingStatsIkegamiFooterRightPadding = 20f;
    public float FloatingStatsIkegamiNameBackgroundAlpha;
    public float FloatingStatsIkegamiBodyBackgroundAlpha;
    public float FloatingStatsIkegamiContentBackgroundAlpha = 0.3f;
    public float FloatingStatsIkegamiTabFontScale = 1f;
    public float FloatingStatsIkegamiNameFontScale = 1f;
    public float FloatingStatsIkegamiHeaderFontScale = 1f;
    public float FloatingStatsIkegamiBodyFontScale = 1f;
    public float FloatingStatsIkegamiFooterFontScale = 1f;
    public float FloatingStatsIkegamiTooltipFontScale = 1f;
    public IkegamiBoxAlignment FloatingStatsIkegamiBoxAlignment = IkegamiBoxAlignment.Center;
    public bool FloatingStatsMinimalShowHeader;
    public bool FloatingStatsMinimalShowSummaryRow = true;
    public bool FloatingStatsMinimalShowPlayerColumn;
    public bool FloatingStatsMinimalShowPlayerNameInShareBar;
    public bool FloatingStatsMinimalShowJobInShareBar = true;
    public bool FloatingStatsMinimalShowDamageInShareBar;
    public bool FloatingStatsMinimalShowDeathsInShareBar;
    public bool FloatingStatsMinimalShowRatioInShareBar;
    public bool FloatingStatsMinimalShowDamageColumn;
    public bool FloatingStatsMinimalShowDeathsColumn;
    public bool FloatingStatsMinimalShowDurationInSummaryBar = true;
    public bool FloatingStatsMinimalShowTitleInSummaryBar = true;
    public bool FloatingStatsMinimalShowDpsInSummaryBar = true;
    public bool FloatingStatsMinimalShowDamageInSummaryBar;
    public bool FloatingStatsMinimalShowDeathsInSummaryBar;
    public bool FloatingStatsMinimalAutoWindowHeight;
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
    public float ThemeBarOpacity = 0.75f;
    public bool HighlightSelfBar;
    public SelfHighlightColorMode SelfHighlightColor = SelfHighlightColorMode.SunlightYellow;
    public List<string> CustomFriendlyNpcNames = new();
    public PartyMonitorConfig PartyMonitor = new();
}
