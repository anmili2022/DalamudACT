using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DalamudACT;

internal sealed class TimelineListWindow : Window
{
    private readonly TimelineService timelineService;
    private string searchText = string.Empty;

    public TimelineListWindow(TimelineService timelineService)
        : base("已有时间轴###TimelineListWindow")
    {
        this.timelineService = timelineService;
        Size = new Vector2(900f, 560f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("已有时间轴");
        ImGui.SameLine();
        ImGui.TextDisabled("显示当前配置、硬编码源码、插件目录和在线缓存中可发现的时间轴。 ");
        ImGui.Separator();

        ImGui.SetNextItemWidth(-1f);
        ImGui.InputText("##timeline_list_search", ref searchText, 256);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("按副本名、ZoneId、id、文件名或路径搜索。 ");

        var entries = timelineService.GetAvailableTimelineEntries();
        var visibleEntries = FilterEntries(entries);
        ImGui.TextDisabled($"显示 {visibleEntries.Count} / {entries.Count}");

        ImGui.BeginChild("##timeline_list_entries", new Vector2(0f, 0f), true);
        try
        {
            foreach (var entry in visibleEntries)
                DrawEntry(entry);
        }
        finally
        {
            ImGui.EndChild();
        }
    }

    private List<TimelineService.TimelineAvailableEntry> FilterEntries(IReadOnlyList<TimelineService.TimelineAvailableEntry> entries)
    {
        var trimmedSearch = searchText.Trim();
        if (string.IsNullOrWhiteSpace(trimmedSearch))
            return [.. entries];

        var filtered = new List<TimelineService.TimelineAvailableEntry>();
        foreach (var entry in entries)
        {
            if (IsMatch(entry, trimmedSearch))
                filtered.Add(entry);
        }

        return filtered;
    }

    private static bool IsMatch(TimelineService.TimelineAvailableEntry entry, string search)
        => entry.Id.Contains(search, StringComparison.OrdinalIgnoreCase)
           || entry.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
           || entry.FileName.Contains(search, StringComparison.OrdinalIgnoreCase)
           || entry.ResolvedPath.Contains(search, StringComparison.OrdinalIgnoreCase)
           || (entry.ZoneId?.ToString().Contains(search, StringComparison.OrdinalIgnoreCase) == true);

    private static void DrawEntry(TimelineService.TimelineAvailableEntry entry)
    {
        var zoneText = entry.ZoneId.HasValue ? entry.ZoneId.Value.ToString() : "-";
        var pathText = string.IsNullOrWhiteSpace(entry.ResolvedPath) ? "未找到文件" : entry.ResolvedPath;
        ImGui.TextUnformatted($"{zoneText}  {entry.Name}  ({entry.Id})");
        ImGui.TextDisabled(entry.FileName);
        ImGui.TextDisabled(pathText);

        if (!string.IsNullOrWhiteSpace(entry.ResolvedPath))
        {
            if (ImGui.SmallButton($"打开文件夹##open_timeline_folder_{entry.Id}"))
                OpenTimelineFolder(entry.ResolvedPath);
        }

        ImGui.Separator();
    }

    private static void OpenTimelineFolder(string filePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true,
                Verb = "open",
            });
        }
        catch (Exception ex)
        {
            LogHelper.Warning("时间轴", ex, $"打开时间轴文件夹失败：{filePath}");
        }
    }
}
