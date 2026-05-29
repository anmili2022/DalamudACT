using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DalamudACT;

internal sealed class StatusObserverWindow : Window
{
    private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoCollapse
                                             | ImGuiWindowFlags.NoTitleBar
                                             | ImGuiWindowFlags.NoBackground;
    private const float MinimumExpandedWidth = 220f;
    private const int AutoResizeIconColumns = 8;
    private const float PanelPaddingX = 8f;
    private const float PanelPaddingY = 7f;
    private const float CollapsedPanelHeight = 34f;
    private const float IconWidth = 25f;
    private const float IconHeight = 30f;
    private const float IconIdTextHeight = 14f;
    private const float IconGap = 5f;
    private static readonly Vector4 PanelBorderColor = new(0.70f, 0.82f, 0.90f, 0.18f);
    private static readonly Vector4 TitleColor = new(0.21f, 0.85f, 1f, 1f);
    private static readonly Vector4 SectionTitleColor = new(0.96f, 0.98f, 1f, 1f);
    private static readonly Vector4 FavoriteColor = new(1f, 0.88f, 0.25f, 1f);
    private readonly PluginConfiguration config;
    private readonly StatusObserverService service;
    private readonly Action openSettings;
    private bool collapsed;
    private bool restoreExpandedSize;
    private Vector2 expandedWindowSize = new(430f, 360f);

    public StatusObserverWindow(PluginConfiguration config, StatusObserverService service, Action openSettings)
        : base("状态监控###StatusObserverWindow", BaseFlags)
    {
        this.config = config;
        this.service = service;
        this.openSettings = openSettings;
        Size = new Vector2(430f, 360f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void PreDraw()
    {
        BgAlpha = Math.Clamp(config.StatusObserver.WindowOpacity, 0f, 1f);
        Flags = BuildWindowFlags();
    }

    public override void Draw()
    {
        if (collapsed)
            ImGui.SetScrollY(0f);

        ApplyLockedAutoResizeWidthHint();
        ApplyCollapsedWindowSize();
        DrawPanelBackground();
        HandleContextClick();
        ImGui.SetCursorPos(new Vector2(PanelPaddingX, PanelPaddingY));

        if (DrawWindowHeader())
            ToggleCollapsed();

        if (collapsed)
            return;

        if (config.StatusObserver.ShowSelfStatuses)
            DrawStatusSection("自身状态", service.GetSelfStatuses(), "self");

        if (config.StatusObserver.ShowTargetStatuses)
            DrawStatusSection("目标状态", service.GetTargetStatuses(), "target");
    }

    private ImGuiWindowFlags BuildWindowFlags()
    {
        var flags = BaseFlags;
        if (collapsed)
            flags |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        if (!config.StatusObserver.LockWindow)
            return flags;

        flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;
        if (!collapsed)
            flags |= ImGuiWindowFlags.AlwaysAutoResize;
        return flags;
    }

    private void DrawStatusSection(string title, IReadOnlyList<StatusObserverEntry> entries, string id)
    {
        ImGui.Separator();
        ImGui.PushStyleColor(ImGuiCol.Text, SectionTitleColor);
        ImGui.TextUnformatted(title);
        ImGui.PopStyleColor();
        if (entries.Count == 0)
        {
            ImGui.TextDisabled(id == "target" ? "无目标或目标没有可显示状态。" : "没有可显示状态。 ");
            return;
        }

        if (config.StatusObserver.DisplayMode == StatusObserverDisplayMode.Icon)
        {
            DrawIconStatusSection(entries, id);
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
        var textColor = entry.IsFavorite ? FavoriteColor : Vector4.One;
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

    private void DrawIconStatusSection(IReadOnlyList<StatusObserverEntry> entries, string id)
    {
        var availableWidth = config.StatusObserver.LockWindow
            ? GetLockedIconContentWidth(entries.Count)
            : Math.Max(IconWidth, ImGui.GetContentRegionAvail().X);
        var columns = Math.Max(1, (int)((availableWidth + IconGap) / (IconWidth + IconGap)));

        for (var i = 0; i < entries.Count; i++)
        {
            if (i > 0 && i % columns != 0)
                ImGui.SameLine(0f, IconGap);

            DrawStatusIcon(entries[i], id, i);
        }
    }

    private void DrawStatusIcon(StatusObserverEntry entry, string id, int index)
    {
        var pos = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var min = pos;
        var iconSize = new Vector2(IconWidth, IconHeight);
        var itemSize = new Vector2(IconWidth, GetIconItemHeight());
        var max = pos + iconSize;
        var frameColor = entry.IsFavorite
            ? new Vector4(1f, 0.80f, 0.18f, 0.92f)
            : new Vector4(0.14f, 0.20f, 0.26f, 0.92f);
        drawList.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(new Vector4(0.04f, 0.06f, 0.08f, 0.92f)), 3f);
        drawList.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(frameColor), 3f, ImDrawFlags.None, 1.4f);

        ImGui.PushID($"{id}_{index}_{entry.StatusId}_{entry.SourceId}");
        ImGui.InvisibleButton("##status_icon_item", itemSize);
        var icon = KamiIconLoader.GetIconId(entry.IconId);
        if (icon != default)
            drawList.AddImage(icon, min, max);
        else
        {
            var label = entry.StatusId.ToString();
            var textSize = ImGui.CalcTextSize(label);
            drawList.AddText(min + (iconSize - textSize) * 0.5f, ImGui.ColorConvertFloat4ToU32(Vector4.One), label);
        }

        if (entry.RemainingSeconds > 0f)
            DrawIconOverlayText(drawList, min + new Vector2(3f, IconHeight - 14f), FormatIconRemaining(entry.RemainingSeconds));

        if (config.StatusObserver.ShowStatusIdUnderIcon)
        {
            var idText = entry.StatusId.ToString();
            var textSize = ImGui.CalcTextSize(idText);
            var textPos = min + new Vector2(MathF.Max(0f, (IconWidth - textSize.X) * 0.5f), IconHeight + 1f);
            drawList.AddText(textPos + new Vector2(1f, 1f), ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.82f)), idText);
            drawList.AddText(textPos, ImGui.ColorConvertFloat4ToU32(Vector4.One), idText);
        }

        if (ImGui.IsItemHovered())
            DrawStatusTooltip(entry);
        DrawContextMenu(entry);
        ImGui.PopID();
    }

    private static void DrawIconOverlayText(ImDrawListPtr drawList, Vector2 pos, string text)
    {
        var shadow = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 1f));
        var color = ImGui.ColorConvertFloat4ToU32(Vector4.One);
        drawList.AddText(pos + new Vector2(1f, 1f), shadow, text);
        drawList.AddText(pos, color, text);
    }

    private static string FormatIconRemaining(float seconds)
        => seconds >= 60f ? $"{(int)(seconds / 60f)}m" : $"{Math.Ceiling(seconds):0}";

    private float GetIconItemHeight()
        => IconHeight + (config.StatusObserver.ShowStatusIdUnderIcon ? IconIdTextHeight : 0f);

    private static void DrawStatusTooltip(StatusObserverEntry entry)
    {
        ImGui.BeginTooltip();
        ImGui.TextUnformatted($"{entry.Name} ({entry.StatusId})");
        ImGui.TextUnformatted($"剩余：{FormatRemaining(entry.RemainingSeconds)}");
        ImGui.TextUnformatted($"层数：{(entry.StackCount == 0 ? "-" : entry.StackCount.ToString())}");
        ImGui.TextUnformatted($"Param：{(entry.Param == 0 ? "-" : entry.Param.ToString())}");
        ImGui.TextUnformatted($"来源：{(entry.SourceId == 0 ? "-" : entry.SourceIsSelf ? "自己" : entry.SourceId.ToString("X8"))}");
        ImGui.EndTooltip();
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

    private void DrawPanelBackground()
    {
        var drawList = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();
        var opacity = Math.Clamp(config.StatusObserver.WindowOpacity, 0f, 1f);
        drawList.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(new Vector4(0.03f, 0.05f, 0.07f, 0.90f * opacity)), 4f);
        drawList.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(new Vector4(PanelBorderColor.X, PanelBorderColor.Y, PanelBorderColor.Z, PanelBorderColor.W * opacity)), 4f);
    }

    private void HandleContextClick()
    {
        if (config.StatusObserver.LockWindow)
            return;

        if (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows) && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            openSettings();
    }

    private void ApplyCollapsedWindowSize()
    {
        if (collapsed)
        {
            ImGui.SetWindowSize(new Vector2(Math.Max(ImGui.GetWindowWidth(), 140f), CollapsedPanelHeight), ImGuiCond.Always);
            return;
        }

        if (!restoreExpandedSize)
            return;

        ImGui.SetWindowSize(new Vector2(Math.Max(expandedWindowSize.X, 140f), Math.Max(expandedWindowSize.Y, 120f)), ImGuiCond.Always);
        restoreExpandedSize = false;
    }

    private void ApplyLockedAutoResizeWidthHint()
    {
        if (!config.StatusObserver.LockWindow || collapsed)
            return;

        var width = config.StatusObserver.DisplayMode == StatusObserverDisplayMode.Icon
            ? GetLockedIconWindowWidth()
            : MinimumExpandedWidth;
        ImGui.SetNextWindowSize(new Vector2(width, 0f), ImGuiCond.Always);
    }

    private float GetLockedIconWindowWidth()
    {
        var entries = 0;
        if (config.StatusObserver.ShowSelfStatuses)
            entries += service.GetSelfStatuses().Count;
        if (config.StatusObserver.ShowTargetStatuses)
            entries += service.GetTargetStatuses().Count;

        var contentWidth = GetLockedIconContentWidth(entries);
        return Math.Max(MinimumExpandedWidth, contentWidth + PanelPaddingX * 2f + ImGui.GetStyle().WindowPadding.X * 2f);
    }

    private static float GetLockedIconContentWidth(int entryCount)
    {
        var columns = Math.Clamp(entryCount <= 0 ? AutoResizeIconColumns : entryCount, 1, AutoResizeIconColumns);
        return columns * IconWidth + Math.Max(0, columns - 1) * IconGap;
    }

    private void ToggleCollapsed()
    {
        if (!collapsed)
            expandedWindowSize = ImGui.GetWindowSize();

        collapsed = !collapsed;
        restoreExpandedSize = !collapsed;
    }

    private bool DrawWindowHeader()
    {
        DrawShadowText("状态监控", TitleColor, true);
        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(collapsed ? "左键展开状态监控" : "左键折叠状态监控");

        var y = ImGui.GetCursorScreenPos().Y - 2f;
        var minX = ImGui.GetCursorScreenPos().X;
        var maxX = ImGui.GetWindowPos().X + ImGui.GetWindowWidth() - PanelPaddingX;
        ImGui.GetWindowDrawList().AddLine(
            new Vector2(minX, y),
            new Vector2(maxX, y),
            ImGui.ColorConvertFloat4ToU32(new Vector4(TitleColor.X, TitleColor.Y, TitleColor.Z, 0.62f)),
            1f);
        ImGui.Dummy(new Vector2(0f, 3f));
        return clicked;
    }

    private static void DrawShadowText(string text, Vector4 color, bool bold = false)
    {
        var pos = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        var shadow = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.82f));
        var textColor = ImGui.ColorConvertFloat4ToU32(color);
        drawList.AddText(pos + new Vector2(1f, 1f), shadow, text);
        drawList.AddText(pos, textColor, text);
        if (bold)
            drawList.AddText(pos + new Vector2(0.6f, 0f), textColor, text);
        ImGui.Dummy(ImGui.CalcTextSize(text));
    }
}
