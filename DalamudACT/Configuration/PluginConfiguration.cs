using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
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
    public int Version { get; set; } = 23;

    public float WindowOpacity = 0.92f;
    public float FloatingStatsOpacity = 0.72f;
    public bool ShowStatsPanel = true;
    public bool LockFloatingStatsWindow = false;
    public CombatEndRule CombatEndRule = CombatEndRule.PartyList;
    public int EncounterTimeoutSeconds = 30;
    public int HistoryPreviewSeconds = 8;

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

    public StatsBarColorMode BarColorMode = StatsBarColorMode.Theme;
    public float SingleBarColorR = 0.25f;
    public float SingleBarColorG = 0.65f;
    public float SingleBarColorB = 1f;
    public float SingleBarColorA = 0.9f;
    public Dictionary<string, ThemeBarColorSetting> ThemeBarColors = new();

    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool? ShowDemoPanel;

    [EditorBrowsable(EditorBrowsableState.Never)]
    public string? MiniParseUrl;

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;

        WindowOpacity = Math.Clamp(WindowOpacity, 0.2f, 1f);
        FloatingStatsOpacity = Math.Clamp(FloatingStatsOpacity, 0f, 1f);
        EncounterTimeoutSeconds = Math.Clamp(EncounterTimeoutSeconds, 5, 180);
        HistoryPreviewSeconds = Math.Clamp(HistoryPreviewSeconds <= 0 ? 8 : HistoryPreviewSeconds, 1, 30);
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

        if (!Enum.IsDefined(typeof(CombatEndRule), CombatEndRule))
            CombatEndRule = CombatEndRule.PartyList;

        SingleBarColorR = Math.Clamp(SingleBarColorR, 0f, 1f);
        SingleBarColorG = Math.Clamp(SingleBarColorG, 0f, 1f);
        SingleBarColorB = Math.Clamp(SingleBarColorB, 0f, 1f);
        SingleBarColorA = Math.Clamp(SingleBarColorA, 0.2f, 1f);

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

        ShowDemoPanel = ShowStatsPanel;
        Version = Math.Max(Version, 23);
    }

    public bool HasAnyVisibleStatsTab()
        => ShowDpsTab || ShowHpsTab || ShowTakenTab || ShowOverviewTab || ShowHistoryTab;

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
            return configured.ToVector4();

        return JobThemePalette.TryGetDefaultColor(jobName, out var fallback)
            ? fallback
            : new Vector4(0.25f, 0.65f, 1f, 0.92f);
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
        ShowDemoPanel = true;
        CombatEndRule = CombatEndRule.PartyList;
        EncounterTimeoutSeconds = 30;
        HistoryPreviewSeconds = 8;
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
        BarColorMode = StatsBarColorMode.Theme;
        SingleBarColorR = 0.25f;
        SingleBarColorG = 0.65f;
        SingleBarColorB = 1f;
        SingleBarColorA = 0.9f;
        ResetThemeBarColors();
    }

    public void Save() => pluginInterface?.SavePluginConfig(this);

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

    private static bool ColorsApproximatelyEqual(Vector4 left, Vector4 right, float epsilon = 0.001f)
        => Math.Abs(left.X - right.X) <= epsilon
           && Math.Abs(left.Y - right.Y) <= epsilon
           && Math.Abs(left.Z - right.Z) <= epsilon
           && Math.Abs(left.W - right.W) <= epsilon;
}
