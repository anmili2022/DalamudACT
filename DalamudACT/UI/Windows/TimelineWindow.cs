using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DalamudACT;

internal sealed class TimelineWindow : Window
{
    private const float PanelWidth = 220f;
    private const float RowHeight = 18f;
    private const float Padding = 3f;
    private const float TimeColumnWidth = 40f;
    private const float ProgressWindowSeconds = 60f;
    private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoScrollbar
                                             | ImGuiWindowFlags.NoScrollWithMouse
                                             | ImGuiWindowFlags.NoTitleBar
                                             | ImGuiWindowFlags.NoResize
                                             | ImGuiWindowFlags.NoBackground;
    private readonly PluginConfiguration config;
    private readonly TimelineService timelineService;
    private readonly Action openSettings;

    public TimelineWindow(PluginConfiguration config, TimelineService timelineService, Action openSettings)
        : base("时间轴###TimelineWindow", BaseFlags | ImGuiWindowFlags.NoTitleBar)
    {
        this.config = config;
        this.timelineService = timelineService;
        this.openSettings = openSettings;
        Size = new Vector2(PanelWidth, 80f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var maxRows = Math.Clamp(config.TimelineMaxVisibleEntries, 1, 30);
        var rowGap = Math.Clamp(config.TimelineRowGap, 0f, 8f);
        var panelHeight = Padding * 2f + maxRows * RowHeight + Math.Max(0, maxRows - 1) * rowGap;
        Size = new Vector2(PanelWidth, panelHeight);
        SizeCondition = ImGuiCond.Always;
        Flags = config.LockTimelineWindow
            ? BaseFlags | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs
            : BaseFlags;
        BgAlpha = Math.Clamp(config.TimelineWindowOpacity, 0f, 1f);

        var entries = timelineService.GetVisibleEntries();
        var rows = entries.Count > 0
            ? entries.Select(entry => TimelineRow.FromEntry(entry)).ToList()
            : config.TimelineDebugMode
                ? BuildDebugRows(maxRows)
                : BuildEmptyRows(maxRows);

        DrawPanel(rows, maxRows, panelHeight, rowGap);

        if (!config.LockTimelineWindow && ImGui.IsMouseClicked(ImGuiMouseButton.Right) && ImGui.IsWindowHovered())
            openSettings();
    }

    private void DrawPanel(IReadOnlyList<TimelineRow> rows, int maxRows, float panelHeight, float rowGap)
    {
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var opacity = Math.Clamp(config.TimelineWindowOpacity, 0f, 1f);
        var panelMax = origin + new Vector2(PanelWidth, panelHeight);
        drawList.AddRectFilled(origin, panelMax, ToU32(new Vector4(0f, 0f, 0f, 0.96f * opacity)), 0f);

        for (var i = 0; i < maxRows; i++)
        {
            var row = i < rows.Count ? rows[i] : TimelineRow.Empty;
            var rowMin = origin + new Vector2(Padding, Padding + i * (RowHeight + rowGap));
            var rowMax = rowMin + new Vector2(PanelWidth - Padding * 2f, RowHeight);
            DrawRow(drawList, rowMin, rowMax, row, opacity);
        }

        ImGui.Dummy(new Vector2(PanelWidth, panelHeight));
    }

    private static void DrawRow(ImDrawListPtr drawList, Vector2 rowMin, Vector2 rowMax, TimelineRow row, float opacity)
    {
        var timeMin = new Vector2(rowMax.X - TimeColumnWidth, rowMin.Y);
        var barMax = new Vector2(rowMax.X, rowMax.Y);
        var isUrgent = row.Seconds <= 5f && !row.IsStatus && !string.IsNullOrWhiteSpace(row.Name);
        var barColor = isUrgent
            ? new Vector4(244f / 255f, 143f / 255f, 177f / 255f, 0.94f)
            : new Vector4(127f / 255f, 168f / 255f, 232f / 255f, 0.86f);
        var rowBackground = new Vector4(0f, 0f, 0f, 0.86f * opacity);

        drawList.AddRectFilled(rowMin, rowMax, ToU32(rowBackground), 0f);
        if (!string.IsNullOrWhiteSpace(row.Name))
        {
            var fillWidth = Math.Clamp(row.FillRatio, 0f, 1f) * (barMax.X - rowMin.X);
            if (fillWidth > 0.5f)
                drawList.AddRectFilled(rowMin, new Vector2(rowMin.X + fillWidth, rowMax.Y), ToU32(barColor), 0f);
        }

        var name = TruncateText(row.Name, row.IsStatus ? 10f : TimeColumnWidth + 12f, rowMax.X - rowMin.X);
        var namePos = rowMin + new Vector2(5f, 1f);
        DrawOutlinedText(drawList, namePos, name, new Vector4(1f, 1f, 1f, string.IsNullOrWhiteSpace(row.Name) ? 0.45f : 1f));

        if (!string.IsNullOrWhiteSpace(row.TimeText))
        {
            var timeSize = ImGui.CalcTextSize(row.TimeText);
            var timePos = new Vector2(rowMax.X - timeSize.X - 5f, rowMin.Y + 1f);
            DrawOutlinedText(drawList, timePos, row.TimeText, new Vector4(1f, 1f, 1f, 1f));
        }
    }

    private List<TimelineRow> BuildDebugRows(int maxRows)
    {
        var rows = new List<TimelineRow>();
        AddDebugRows(rows, maxRows, timelineService.StatusText);

        foreach (var line in timelineService.DebugText.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            AddDebugRows(rows, maxRows, line);

        while (rows.Count < maxRows)
            rows.Add(TimelineRow.Empty);
        return rows.Take(maxRows).ToList();
    }

    private static List<TimelineRow> BuildEmptyRows(int maxRows)
    {
        var rows = new List<TimelineRow>(maxRows);
        while (rows.Count < maxRows)
            rows.Add(TimelineRow.Empty);
        return rows;
    }

    private static void AddDebugRows(List<TimelineRow> rows, int maxRows, string text)
    {
        if (rows.Count >= maxRows)
            return;

        foreach (var line in WrapDebugLine(text, PanelWidth - Padding * 2f - 10f))
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
        drawList.AddText(pos + new Vector2(0.3f, 0f), textColor, text);
        drawList.AddText(pos + new Vector2(-0.3f, 0f), textColor, text);
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

    private readonly record struct TimelineRow(string Name, string TimeText, float Seconds, float FillRatio, bool IsStatus)
    {
        public static readonly TimelineRow Empty = new(string.Empty, string.Empty, 999f, 0f, true);

        public static TimelineRow FromEntry(TimelineVisibleEntry visible)
        {
            var seconds = Math.Max(0f, visible.RelativeSeconds);
            var ratio = 1f - Math.Clamp(seconds / ProgressWindowSeconds, 0f, 1f);
            return new TimelineRow(visible.DisplayText, FormatCountdown(seconds), seconds, ratio, false);
        }

        public static TimelineRow Status(string text)
            => new(text, string.Empty, 999f, 1f, true);
    }
}
