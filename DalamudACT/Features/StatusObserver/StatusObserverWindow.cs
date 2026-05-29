using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DalamudACT;

internal sealed class StatusObserverWindow : Window
{
    private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoCollapse;
    private readonly PluginConfiguration config;
    private readonly StatusObserverService service;
    private readonly Action openSettings;

    public StatusObserverWindow(PluginConfiguration config, StatusObserverService service, Action openSettings)
        : base("状态观察###StatusObserverWindow", BaseFlags)
    {
        this.config = config;
        this.service = service;
        this.openSettings = openSettings;
        Size = new Vector2(430f, 360f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        Flags = config.StatusObserver.LockWindow
            ? BaseFlags | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize
            : BaseFlags;

        if (!config.StatusObserver.LockWindow && ImGui.Button("设置"))
            openSettings();

        if (config.StatusObserver.ShowSelfStatuses)
            DrawStatusSection("自身状态", service.GetSelfStatuses(), "self");

        if (config.StatusObserver.ShowTargetStatuses)
            DrawStatusSection("目标状态", service.GetTargetStatuses(), "target");
    }

    private void DrawStatusSection(string title, IReadOnlyList<StatusObserverEntry> entries, string id)
    {
        ImGui.Separator();
        ImGui.TextUnformatted(title);
        if (entries.Count == 0)
        {
            ImGui.TextDisabled(id == "target" ? "无目标或目标没有可显示状态。" : "没有可显示状态。 ");
            return;
        }

        if (!ImGui.BeginTable($"##status_observer_{id}", config.StatusObserver.ShowSourceInfo ? 6 : 5, ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp))
            return;

        try
        {
            ImGui.TableSetupColumn("ID", ImGuiTableColumnFlags.WidthFixed, 58f);
            ImGui.TableSetupColumn("名称");
            ImGui.TableSetupColumn("剩余", ImGuiTableColumnFlags.WidthFixed, 58f);
            ImGui.TableSetupColumn("层数", ImGuiTableColumnFlags.WidthFixed, 44f);
            ImGui.TableSetupColumn("Param", ImGuiTableColumnFlags.WidthFixed, 48f);
            if (config.StatusObserver.ShowSourceInfo)
                ImGui.TableSetupColumn("来源", ImGuiTableColumnFlags.WidthFixed, 72f);
            ImGui.TableHeadersRow();

            foreach (var entry in entries)
                DrawStatusRow(entry);
        }
        finally
        {
            ImGui.EndTable();
        }
    }

    private void DrawStatusRow(StatusObserverEntry entry)
    {
        ImGui.TableNextRow();
        var textColor = entry.IsFavorite ? new Vector4(1f, 0.88f, 0.25f, 1f) : Vector4.One;
        ImGui.PushStyleColor(ImGuiCol.Text, textColor);
        try
        {
            ImGui.TableSetColumnIndex(0);
            ImGui.Selectable(entry.StatusId.ToString(), false, ImGuiSelectableFlags.SpanAllColumns);
            DrawContextMenu(entry);

            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(entry.Name);

            ImGui.TableSetColumnIndex(2);
            ImGui.TextUnformatted(FormatRemaining(entry.RemainingSeconds));

            ImGui.TableSetColumnIndex(3);
            ImGui.TextUnformatted(entry.StackCount == 0 ? "-" : entry.StackCount.ToString());

            ImGui.TableSetColumnIndex(4);
            ImGui.TextUnformatted(entry.Param == 0 ? "-" : entry.Param.ToString());

            if (config.StatusObserver.ShowSourceInfo)
            {
                ImGui.TableSetColumnIndex(5);
                ImGui.TextUnformatted(entry.SourceId == 0 ? "-" : entry.SourceIsSelf ? "自己" : entry.SourceId.ToString("X8"));
            }
        }
        finally
        {
            ImGui.PopStyleColor();
        }
    }

    private void DrawContextMenu(StatusObserverEntry entry)
    {
        if (!ImGui.BeginPopupContextItem($"##status_menu_{entry.StatusId}_{entry.SourceId}"))
            return;

        try
        {
            if (ImGui.MenuItem("复制状态ID"))
                ImGui.SetClipboardText(entry.StatusId.ToString());
            if (ImGui.MenuItem("复制状态名"))
                ImGui.SetClipboardText(entry.Name);
            if (ImGui.MenuItem("复制完整信息"))
                ImGui.SetClipboardText($"StatusId={entry.StatusId} Name={entry.Name} Remaining={entry.RemainingSeconds:0.0} Param={entry.Param} Stack={entry.StackCount} SourceId={entry.SourceId:X8} SourceIsSelf={entry.SourceIsSelf}");

            var favorites = config.StatusObserver.FavoriteStatusIds;
            if (entry.IsFavorite)
            {
                if (ImGui.MenuItem("取消关注"))
                {
                    favorites.RemoveAll(id => id == entry.StatusId);
                    config.Save();
                }
            }
            else if (ImGui.MenuItem("加入关注"))
            {
                if (!favorites.Contains(entry.StatusId))
                    favorites.Add(entry.StatusId);
                config.Save();
            }
        }
        finally
        {
            ImGui.EndPopup();
        }
    }

    private static string FormatRemaining(float seconds)
    {
        if (seconds <= 0f)
            return "永久";

        if (seconds >= 60f)
            return $"{(int)(seconds / 60f):00}:{(int)(seconds % 60f):00}";

        return $"{seconds:0.0}s";
    }
}
