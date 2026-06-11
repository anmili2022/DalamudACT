using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DalamudACT;

internal sealed class TimelineWindow : Window
{
    private const float DefaultPanelWidth = 220f;
    private const float RowHeight = 18f;
    private const float Padding = 3f;
    private const float TimeColumnWidth = 54f;
    private static readonly TimeSpan RowCacheDuration = TimeSpan.FromMilliseconds(100);
    private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoScrollbar
                                             | ImGuiWindowFlags.NoScrollWithMouse
                                             | ImGuiWindowFlags.NoTitleBar
                                             | ImGuiWindowFlags.NoBackground;
    private readonly PluginConfiguration config;
    private readonly TimelineService timelineService;
    private readonly Action openSettings;
    private bool resetSizeRequested;
    private bool observedLockTimelineWindow;
    private float? lockedPanelWidth;
    private readonly List<TimelineRow> cachedRows = new(30);
    private DateTime cachedRowsAtUtc = DateTime.MinValue;
    private int cachedRowsMaxRows;
    private int cachedRowsVisibleSeconds;
    private bool cachedRowsDebugMode;
    private float cachedRowsPanelWidth;
    private string cachedRowsStatusText = string.Empty;
    private string cachedRowsDebugText = string.Empty;

    public TimelineWindow(PluginConfiguration config, TimelineService timelineService, Action openSettings)
        : base("时间轴###TimelineWindow", BaseFlags | ImGuiWindowFlags.NoTitleBar)
    {
        this.config = config;
        this.timelineService = timelineService;
        this.openSettings = openSettings;
        observedLockTimelineWindow = config.LockTimelineWindow;
        Size = new Vector2(DefaultPanelWidth, 80f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void RequestResetSize()
    {
        resetSizeRequested = true;
    }

    public override void Draw()
    {
        var maxRows = Math.Clamp(config.TimelineMaxVisibleEntries, 1, 30);
        var rowGap = Math.Clamp(config.TimelineRowGap, 0f, 8f);
        var debugFooterRows = config.TimelineDebugMode ? 1 : 0;
        var panelHeight = Padding * 2f + (maxRows + debugFooterRows) * RowHeight + Math.Max(0, maxRows + debugFooterRows - 1) * rowGap;
        var style = ImGui.GetStyle();
        var windowHeight = panelHeight + style.WindowPadding.Y * 2f;
        var contentWidth = Math.Max(1f, ImGui.GetContentRegionAvail().X);

        if (config.LockTimelineWindow && !lockedPanelWidth.HasValue)
            lockedPanelWidth = contentWidth;

        if (observedLockTimelineWindow != config.LockTimelineWindow)
        {
            observedLockTimelineWindow = config.LockTimelineWindow;
            if (config.LockTimelineWindow)
                lockedPanelWidth = resetSizeRequested
                    ? Math.Max(1f, DefaultPanelWidth - style.WindowPadding.X * 2f)
                    : contentWidth;
            else
                lockedPanelWidth = null;
        }

        if (resetSizeRequested)
        {
            ImGui.SetWindowSize(new Vector2(DefaultPanelWidth, windowHeight), ImGuiCond.Always);
            contentWidth = Math.Max(1f, DefaultPanelWidth - style.WindowPadding.X * 2f);
            if (config.LockTimelineWindow)
                lockedPanelWidth = contentWidth;
            resetSizeRequested = false;
        }

        var panelWidth = config.LockTimelineWindow
            ? lockedPanelWidth ?? contentWidth
            : contentWidth;

        if (config.LockTimelineWindow)
        {
            Size = new Vector2(panelWidth + style.WindowPadding.X * 2f, windowHeight);
            SizeCondition = ImGuiCond.Always;
        }
        else
        {
            Size = null;
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        Flags = config.LockTimelineWindow
            ? BaseFlags | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs
            : BaseFlags;
        BgAlpha = Math.Clamp(config.TimelineWindowOpacity, 0f, 1f);

        var rows = GetRows(maxRows, panelWidth);

        DrawPanel(rows, maxRows, panelWidth, panelHeight, rowGap);

        if (!config.LockTimelineWindow && ImGui.IsMouseClicked(ImGuiMouseButton.Right) && ImGui.IsWindowHovered())
            openSettings();
    }

    private IReadOnlyList<TimelineRow> GetRows(int maxRows, float panelWidth)
    {
        var visibleSeconds = Math.Clamp(config.TimelineVisibleSeconds, 10, 600);
        var debugMode = config.TimelineDebugMode;
        var statusText = debugMode ? timelineService.StatusText : string.Empty;
        var debugText = debugMode ? timelineService.DebugText : string.Empty;
        var nowUtc = DateTime.UtcNow;
        if (cachedRows.Count > 0
            && cachedRowsMaxRows == maxRows
            && cachedRowsVisibleSeconds == visibleSeconds
            && cachedRowsDebugMode == debugMode
            && Math.Abs(cachedRowsPanelWidth - panelWidth) <= 0.5f
            && string.Equals(cachedRowsStatusText, statusText, StringComparison.Ordinal)
            && string.Equals(cachedRowsDebugText, debugText, StringComparison.Ordinal)
            && nowUtc - cachedRowsAtUtc < RowCacheDuration)
        {
            return cachedRows;
        }

        cachedRows.Clear();
        var entries = timelineService.GetVisibleEntries();
        if (entries.Count > 0)
        {
            var rowWidth = Math.Max(1f, panelWidth - Padding * 2f);
            foreach (var entry in entries)
            {
                if (cachedRows.Count >= maxRows)
                    break;

                cachedRows.Add(TimelineRow.FromEntry(entry, visibleSeconds, rowWidth));
            }
        }
        else if (debugMode)
        {
            BuildDebugRows(cachedRows, maxRows, panelWidth, statusText, debugText);
        }

        while (cachedRows.Count < maxRows)
            cachedRows.Add(TimelineRow.Empty);

        cachedRowsAtUtc = nowUtc;
        cachedRowsMaxRows = maxRows;
        cachedRowsVisibleSeconds = visibleSeconds;
        cachedRowsDebugMode = debugMode;
        cachedRowsPanelWidth = panelWidth;
        cachedRowsStatusText = statusText;
        cachedRowsDebugText = debugText;
        return cachedRows;
    }

    private void DrawPanel(IReadOnlyList<TimelineRow> rows, int maxRows, float panelWidth, float panelHeight, float rowGap)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var opacity = Math.Clamp(config.TimelineWindowOpacity, 0f, 1f);
        var panelMax = origin + new Vector2(panelWidth, panelHeight);
        drawList.AddRectFilled(origin, panelMax, ToU32(new Vector4(0f, 0f, 0f, 0.96f * opacity)), 0f);

        for (var i = 0; i < maxRows; i++)
        {
            var row = i < rows.Count ? rows[i] : TimelineRow.Empty;
            var rowMin = origin + new Vector2(Padding, Padding + i * (RowHeight + rowGap));
            var rowMax = rowMin + new Vector2(panelWidth - Padding * 2f, RowHeight);
            DrawRow(drawList, rowMin, rowMax, row, opacity);
        }

        if (config.TimelineDebugMode)
        {
            var footerMin = origin + new Vector2(Padding, Padding + maxRows * (RowHeight + rowGap));
            var footerMax = footerMin + new Vector2(panelWidth - Padding * 2f, RowHeight);
            DrawRow(drawList, footerMin, footerMax, TimelineRow.Status(timelineService.CurrentTimelineLineDebugText), opacity);
        }

        ImGui.Dummy(new Vector2(panelWidth, panelHeight));
    }

    private static void DrawRow(ImDrawListPtr drawList, Vector2 rowMin, Vector2 rowMax, TimelineRow row, float opacity)
    {
        var barMax = new Vector2(rowMax.X, rowMax.Y);
        var isUrgent = row.Seconds <= 5f && !row.IsStatus && !string.IsNullOrWhiteSpace(row.Name);
        var barColor = isUrgent
            ? new Vector4(244f / 255f, 143f / 255f, 177f / 255f, 0.94f)
            : new Vector4(127f / 255f, 168f / 255f, 232f / 255f, 0.86f);
        var rowBackground = new Vector4(0f, 0f, 0f, 0.86f * opacity);

        drawList.AddRectFilled(rowMin, rowMax, ToU32(rowBackground), 0f);
        if (!string.IsNullOrWhiteSpace(row.Name))
        {
            var rowWidth = barMax.X - rowMin.X;
            var fillWidth = Math.Clamp(row.FillRatio, 0f, 1f) * rowWidth;
            if (fillWidth > 0.5f)
                drawList.AddRectFilled(rowMin, new Vector2(rowMin.X + fillWidth, rowMax.Y), ToU32(barColor), 0f);
        }

        var namePos = rowMin + new Vector2(5f, 1f);
        DrawOutlinedText(drawList, namePos, row.Name, new Vector4(1f, 1f, 1f, string.IsNullOrWhiteSpace(row.Name) ? 0.45f : 1f));

        if (!string.IsNullOrWhiteSpace(row.TimeText))
        {
            var timePos = new Vector2(Math.Max(rowMin.X + 5f, rowMax.X - row.TimeTextWidth - 12f), rowMin.Y + 1f);
            DrawOutlinedText(drawList, timePos, row.TimeText, new Vector4(1f, 1f, 1f, 1f));
        }
    }

    private static void BuildDebugRows(
        List<TimelineRow> rows,
        int maxRows,
        float panelWidth,
        string statusText,
        string debugText)
    {
        AddDebugRows(rows, maxRows, statusText, panelWidth);

        foreach (var line in debugText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            AddDebugRows(rows, maxRows, line, panelWidth);
    }

    private static void AddDebugRows(List<TimelineRow> rows, int maxRows, string text, float panelWidth)
    {
        if (rows.Count >= maxRows)
            return;

        foreach (var line in WrapDebugLine(text, panelWidth - Padding * 2f - 10f))
        {
            rows.Add(TimelineRow.Status(line));
            if (rows.Count >= maxRows)
                return;
        }
    }

    private static IEnumerable<string> WrapDebugLine(string text, float maxWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            yield return string.Empty;
            yield break;
        }

        var remaining = text.Trim();
        while (remaining.Length > 0)
        {
            var take = remaining.Length;
            while (take > 1 && ImGui.CalcTextSize(remaining[..take]).X > maxWidth)
                take--;

            yield return remaining[..take];
            remaining = remaining[take..].TrimStart();
        }
    }

    private static string FormatCountdown(float seconds)
        => seconds < 0f ? "0.0" : seconds.ToString("0.0");

    private static void DrawOutlinedText(ImDrawListPtr drawList, Vector2 pos, string text, Vector4 color)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var textColor = ToU32(color);
        var outlineColor = ToU32(new Vector4(0f, 0f, 0f, color.W));
        drawList.AddText(pos + new Vector2(1f, 1f), ToU32(new Vector4(0f, 0f, 0f, color.W * 0.85f)), text);
        drawList.AddText(pos + new Vector2(-1f, 0f), outlineColor, text);
        drawList.AddText(pos + new Vector2(1f, 0f), outlineColor, text);
        drawList.AddText(pos + new Vector2(0f, -1f), outlineColor, text);
        drawList.AddText(pos + new Vector2(0f, 1f), outlineColor, text);
        drawList.AddText(pos, textColor, text);
    }

    private static string TruncateText(string text, float reservedWidth, float rowWidth)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var maxWidth = Math.Max(20f, rowWidth - reservedWidth);
        if (ImGui.CalcTextSize(text).X <= maxWidth)
            return text;

        const string ellipsis = "...";
        while (text.Length > 1 && ImGui.CalcTextSize(text + ellipsis).X > maxWidth)
            text = text[..^1];
        return text + ellipsis;
    }

    private static uint ToU32(Vector4 color)
        => ImGui.ColorConvertFloat4ToU32(color);

    private readonly record struct TimelineRow(
        string Name,
        string TimeText,
        float TimeTextWidth,
        float Seconds,
        float FillRatio,
        bool IsStatus)
    {
        public static readonly TimelineRow Empty = new(string.Empty, string.Empty, 0f, 999f, 0f, true);

        public static TimelineRow FromEntry(TimelineVisibleEntry visible, int visibleSeconds, float rowWidth)
        {
            var seconds = Math.Max(0f, visible.RelativeSeconds);
            var windowSeconds = Math.Clamp(visibleSeconds <= 0 ? 90f : visibleSeconds, 10f, 600f);
            var normalizedRemaining = Math.Clamp(seconds / windowSeconds, 0f, 1f);
            var ratio = 1f - MathF.Sqrt(normalizedRemaining);
            var timeText = FormatCountdown(seconds);
            var timeWidth = ImGui.CalcTextSize(timeText).X;
            var dynamicTimeWidth = Math.Max(TimeColumnWidth, timeWidth + 12f);
            var name = TruncateText(visible.DisplayText, dynamicTimeWidth + 8f, rowWidth);
            return new TimelineRow(name, timeText, timeWidth, seconds, ratio, false);
        }

        public static TimelineRow Status(string text)
            => new(text, string.Empty, 0f, 999f, 1f, true);
    }
}
