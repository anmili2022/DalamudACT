using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace DalamudACT;

internal sealed class PartyMonitorWindow : Window
{
    private const ImGuiWindowFlags BaseWindowFlags =
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoTitleBar;

    private const float PanelWidth = 500f;
    private const float PanelPaddingX = 8f;
    private const float PanelPaddingY = 7f;
    private const float JobColumnWidthNormal = 92f;
    private const float JobColumnWidthAnonymous = 42f;
    private const float CollapsedPanelHeight = 34f;
    private const float CooldownRevealSeconds = 10f;

    private float JobColumnWidth => config.PartyMonitor.AnonymousMode ? JobColumnWidthAnonymous : JobColumnWidthNormal;

    private float IconGap => config.PartyMonitor.IconGap;

    private float RowGap => config.PartyMonitor.RowGap;

    private static readonly Vector4 GreenColor = new(0.4f, 1f, 0.4f, 1f);
    private static readonly Vector4 OrangeColor = new(1f, 0.6f, 0.2f, 1f);
    private static readonly Vector4 FoodMissingColor = new(1f, 0.22f, 0.18f, 1f);
    private static readonly Vector4 FoodExpiringColor = new(1f, 0.78f, 0.12f, 1f);
    private static readonly Vector4 PanelBorderColor = new(0.70f, 0.82f, 0.90f, 0.18f);
    private static readonly Vector4 TitleColor = new(0.96f, 0.98f, 1f, 1f);
    private static readonly Vector4 RaidBuffColor = new(0.84f, 0.47f, 1f, 1f);
    private static readonly Vector4 MitigationColor = new(0.21f, 0.85f, 1f, 1f);
    private static readonly Vector4 ActiveColor = new(0.35f, 0.78f, 1f, 1f);
    private static readonly Vector4 ActiveBuffColor = new(1f, 0.90f, 0.18f, 1f);
    private static readonly Vector4 ActiveBuffInnerColor = new(1f, 0.54f, 0.08f, 1f);
    private static readonly Vector4 ReadyColor = new(0.74f, 1f, 0.68f, 1f);
    private static readonly Vector4 CooldownColor = new(1f, 0.55f, 0.20f, 1f);
    private static readonly Vector4 PausedBadgeColor = new(0.76f, 0.84f, 0.92f, 0.78f);

    private readonly PluginConfiguration config;
    private readonly PartyMonitorService monitorService;
    private readonly Action toggleSettingsWindow;
    private bool collapsed;
    private bool restoreExpandedSize;
    private Vector2 expandedWindowSize = new(PanelWidth, 260f);

    public PartyMonitorWindow(
        PluginConfiguration config,
        PartyMonitorService monitorService,
        Action toggleSettingsWindow)
        : base("###PartyMonitorPanel", BaseWindowFlags)
    {
        this.config = config;
        this.monitorService = monitorService;
        this.toggleSettingsWindow = toggleSettingsWindow;
        Size = new Vector2(PanelWidth, 260f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        BgAlpha = 0f;

        Flags = BaseWindowFlags;
        if (config.PartyMonitor.AutoResizePartyMonitorWindow && !collapsed)
            Flags |= ImGuiWindowFlags.AlwaysAutoResize;
        if (config.PartyMonitor.LockPartyMonitorWindow)
            Flags |= ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;

        ApplyCollapsedWindowSize();

        DrawPanelBackground();
        HandleContextClick();

        ImGui.SetCursorPos(new Vector2(PanelPaddingX, PanelPaddingY));

        if (collapsed)
        {
            DrawCollapsedHeader();
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var members = monitorService.GetMemberStates();
        if (members.Count == 0)
        {
            ImGui.TextDisabled("当前没有队伍成员。");
            return;
        }

        DrawOverlayContent(members, nowUtc);
    }

    private void DrawPanelBackground()
    {
        var drawList = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        var max = min + ImGui.GetWindowSize();
        var opacity = Math.Clamp(config.PartyMonitor.PartyMonitorOpacity, 0f, 1f);
        drawList.AddRectFilled(min, max, ImGui.ColorConvertFloat4ToU32(WithAlpha(config.PartyMonitor.BackgroundColor, opacity)), 4f);
        drawList.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(WithAlpha(PanelBorderColor, PanelBorderColor.W * opacity)), 4f);
    }

    private static Vector4 WithAlpha(Vector4 color, float alpha)
        => new(color.X, color.Y, color.Z, alpha);

    private void HandleContextClick()
    {
        if (config.PartyMonitor.LockPartyMonitorWindow)
            return;

        if (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows) && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            toggleSettingsWindow();
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

    private void ToggleCollapsed()
    {
        if (!collapsed)
            expandedWindowSize = ImGui.GetWindowSize();

        collapsed = !collapsed;
        restoreExpandedSize = !collapsed;
    }

    private void DrawCollapsedHeader()
    {
        if (DrawGroupHeader("技能监控", MitigationColor, true, showPausedBadge: monitorService.IsPausedOutOfCombat))
            ToggleCollapsed();
    }

    private void DrawOverlayContent(IReadOnlyList<PartyMonitorService.PartyMemberState> members, DateTime nowUtc)
    {
        if (config.PartyMonitor.MonitorSkills)
        {
            if (config.PartyMonitor.MergeSkillGroups)
                DrawMergedSkillGroup("技能监控", members, config.PartyMonitor, MitigationColor, nowUtc, showPausedBadge: monitorService.IsPausedOutOfCombat);
            else
            {
                if (config.PartyMonitor.MonitorRaidBuffs)
                    DrawSkillGroup(null, members, SkillCategory.RaidBuff, RaidBuffColor, nowUtc);
                if (config.PartyMonitor.MonitorMitigations)
                    DrawSkillGroup(null, members, SkillCategory.Mitigation, MitigationColor, nowUtc);
            }
        }

        DrawFoodOverlay(members);
    }

    private static void DrawPausedBadge()
    {
        var pos = ImGui.GetCursorScreenPos();
        var textSize = ImGui.CalcTextSize("暂停");
        var size = textSize + new Vector2(10f, 4f);
        var drawList = ImGui.GetWindowDrawList();
        var bg = ImGui.ColorConvertFloat4ToU32(new Vector4(0.18f, 0.22f, 0.27f, 0.70f));
        var border = ImGui.ColorConvertFloat4ToU32(new Vector4(PausedBadgeColor.X, PausedBadgeColor.Y, PausedBadgeColor.Z, 0.35f));
        var text = ImGui.ColorConvertFloat4ToU32(PausedBadgeColor);
        drawList.AddRectFilled(pos, pos + size, bg, 4f);
        drawList.AddRect(pos, pos + size, border, 4f);
        drawList.AddText(pos + new Vector2(5f, 1f), text, "暂停");
        ImGui.Dummy(size + new Vector2(0f, 3f));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("非战斗中，已暂停刷新。显示最后一次缓存。");
    }

    private bool ShouldShowSkill(PartyMonitorService.SkillCooldownState state)
        => !config.PartyMonitor.HideSkillsOnCooldown
           || state.IsReady
           || state.IsActive
           || state.RemainingCooldown <= CooldownRevealSeconds;

    private void DrawSkillGroup(
        string? title,
        IReadOnlyList<PartyMonitorService.PartyMemberState> members,
        SkillCategory category,
        Vector4 color,
        DateTime nowUtc,
        bool showPausedBadge = false)
    {
        if (!HasAnySkillRows(members, category))
            return;

        if (!string.IsNullOrWhiteSpace(title))
        {
            var count = CountVisibleSkills(members, category, nowUtc);
            if (DrawGroupHeader($"{title} ({count})", color, collapsible: true, showPausedBadge))
                ToggleCollapsed();
        }

        foreach (var member in members)
        {
            var skills = GetSkills(member, category);
            if (skills.Count == 0)
                continue;

            DrawSkillRow(member, skills, nowUtc);
        }

        ImGui.Dummy(new Vector2(0f, 6f));
    }

    private void DrawMergedSkillGroup(
        string title,
        IReadOnlyList<PartyMonitorService.PartyMemberState> members,
        PartyMonitorConfig cfg,
        Vector4 color,
        DateTime nowUtc,
        bool showPausedBadge = false)
    {
        if (!HasAnyMergedSkillRows(members, cfg))
            return;

        var count = CountVisibleMergedSkills(members, cfg, nowUtc);
        if (DrawGroupHeader($"{title} ({count})", color, collapsible: true, showPausedBadge))
            ToggleCollapsed();

        foreach (var member in members)
        {
            if (!HasMergedSkills(member, cfg))
                continue;

            DrawMergedSkillRow(member, cfg, nowUtc);
        }

        ImGui.Dummy(new Vector2(0f, 6f));
    }

    private static IReadOnlyList<PartyMonitorService.SkillCooldownState> GetSkills(
        PartyMonitorService.PartyMemberState member,
        SkillCategory category)
        => category == SkillCategory.Mitigation ? member.MitigationSkills : member.RaidBuffSkills;

    private static bool HasAnySkillRows(IReadOnlyList<PartyMonitorService.PartyMemberState> members, SkillCategory category)
    {
        foreach (var member in members)
        {
            if (GetSkills(member, category).Count > 0)
                return true;
        }

        return false;
    }

    private static bool HasAnyMergedSkillRows(IReadOnlyList<PartyMonitorService.PartyMemberState> members, PartyMonitorConfig cfg)
    {
        foreach (var member in members)
        {
            if (HasMergedSkills(member, cfg))
                return true;
        }

        return false;
    }

    private static bool HasMergedSkills(PartyMonitorService.PartyMemberState member, PartyMonitorConfig cfg)
        => cfg.MonitorRaidBuffs && member.RaidBuffSkills.Count > 0
           || cfg.MonitorMitigations && member.MitigationSkills.Count > 0;

    private int CountVisibleSkills(
        IReadOnlyList<PartyMonitorService.PartyMemberState> members,
        SkillCategory category,
        DateTime nowUtc)
    {
        var count = 0;
        foreach (var member in members)
        {
            foreach (var skill in GetSkills(member, category))
            {
                if (ShouldShowSkill(skill.WithDynamicTime(nowUtc)))
                    count++;
            }
        }

        return count;
    }

    private int CountVisibleMergedSkills(
        IReadOnlyList<PartyMonitorService.PartyMemberState> members,
        PartyMonitorConfig cfg,
        DateTime nowUtc)
    {
        var count = 0;
        foreach (var member in members)
        {
            if (cfg.MonitorRaidBuffs)
                count += CountVisibleSkills(member.RaidBuffSkills, nowUtc);
            if (cfg.MonitorMitigations)
                count += CountVisibleSkills(member.MitigationSkills, nowUtc);
        }

        return count;
    }

    private int CountVisibleSkills(IReadOnlyList<PartyMonitorService.SkillCooldownState> skills, DateTime nowUtc)
    {
        var count = 0;
        foreach (var skill in skills)
        {
            if (ShouldShowSkill(skill.WithDynamicTime(nowUtc)))
                count++;
        }

        return count;
    }

    private bool DrawGroupHeader(string title, Vector4 color, bool collapsible = false, bool showPausedBadge = false)
    {
        DrawShadowText(title, color, true);
        var clicked = collapsible && ImGui.IsItemClicked(ImGuiMouseButton.Left);
        var titleHovered = collapsible && ImGui.IsItemHovered();
        var afterTitleCursor = ImGui.GetCursorScreenPos();
        if (showPausedBadge)
        {
            if (DrawPausedBadgeRightAligned("非战斗中，已暂停刷新。点击立即刷新一次。"))
                monitorService.RefreshOnce(DateTime.UtcNow);
            ImGui.SetCursorScreenPos(afterTitleCursor);
        }

        if (titleHovered)
            ImGui.SetTooltip(collapsed ? "左键展开技能监控" : "左键折叠技能监控");
        var y = ImGui.GetCursorScreenPos().Y - 2f;
        var minX = ImGui.GetCursorScreenPos().X;
        var maxX = ImGui.GetWindowPos().X + ImGui.GetWindowWidth() - PanelPaddingX;
        ImGui.GetWindowDrawList().AddLine(
            new Vector2(minX, y),
            new Vector2(maxX, y),
            ImGui.ColorConvertFloat4ToU32(new Vector4(color.X, color.Y, color.Z, 0.62f)),
            1f);
        ImGui.Dummy(new Vector2(0f, 3f));
        return clicked;
    }

    private static bool DrawPausedBadgeRightAligned(string tooltip)
    {
        const string text = "暂停";
        var textSize = ImGui.CalcTextSize(text);
        var size = textSize + new Vector2(10f, 4f);
        var right = ImGui.GetWindowPos().X + ImGui.GetWindowWidth() - PanelPaddingX;
        var pos = new Vector2(right - size.X, ImGui.GetItemRectMin().Y + 1f);
        var drawList = ImGui.GetWindowDrawList();
        var bg = ImGui.ColorConvertFloat4ToU32(new Vector4(0.18f, 0.22f, 0.27f, 0.70f));
        var border = ImGui.ColorConvertFloat4ToU32(new Vector4(0.76f, 0.84f, 0.92f, 0.35f));
        var textColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0.76f, 0.84f, 0.92f, 0.78f));
        drawList.AddRectFilled(pos, pos + size, bg, 4f);
        drawList.AddRect(pos, pos + size, border, 4f);
        drawList.AddText(pos + new Vector2(5f, 1f), textColor, text);
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(pos);
        ImGui.InvisibleButton("##party_monitor_paused_badge", size);
        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
        ImGui.SetCursorScreenPos(cursor);
        return clicked;
    }

    private void DrawSkillRow(
        PartyMonitorService.PartyMemberState member,
        IReadOnlyList<PartyMonitorService.SkillCooldownState> skills,
        DateTime nowUtc)
    {
        var rowStart = ImGui.GetCursorPos();
        var iconSize = GetIconSize();
        var rowHeight = Math.Max(iconSize, ImGui.GetTextLineHeight() + 5f) + RowGap;
        var iconStartX = config.PartyMonitor.HideNameColumn
            ? rowStart.X
            : PanelPaddingX + JobColumnWidth + 4f;

        if (!config.PartyMonitor.HideNameColumn)
        {
            ImGui.SetCursorPos(rowStart);
            var label = GetMemberDisplayName(member);
            DrawNameChip(label, GetJobColor(member.JobId), GetNameChipWidth(label));
        }

        var drawnCount = 0;
        foreach (var skill in skills)
        {
            var displaySkill = skill.WithDynamicTime(nowUtc);
            if (!ShouldShowSkill(displaySkill))
                continue;

            ImGui.SetCursorPos(new Vector2(iconStartX + drawnCount * (iconSize + IconGap), rowStart.Y));
            DrawSkillIcon(displaySkill);
            drawnCount++;
        }

        ImGui.SetCursorPos(new Vector2(rowStart.X, rowStart.Y + rowHeight));
    }

    private void DrawMergedSkillRow(
        PartyMonitorService.PartyMemberState member,
        PartyMonitorConfig cfg,
        DateTime nowUtc)
    {
        var rowStart = ImGui.GetCursorPos();
        var iconSize = GetIconSize();
        var rowHeight = Math.Max(iconSize, ImGui.GetTextLineHeight() + 5f) + RowGap;
        var iconStartX = config.PartyMonitor.HideNameColumn
            ? rowStart.X
            : PanelPaddingX + JobColumnWidth + 4f;

        if (!config.PartyMonitor.HideNameColumn)
        {
            ImGui.SetCursorPos(rowStart);
            var label = GetMemberDisplayName(member);
            DrawNameChip(label, GetJobColor(member.JobId), GetNameChipWidth(label));
        }

        var drawnCount = 0;
        if (cfg.MonitorRaidBuffs)
            DrawMergedSkillRowIcons(member.RaidBuffSkills, nowUtc, iconStartX, rowStart.Y, iconSize, ref drawnCount);
        if (cfg.MonitorMitigations)
            DrawMergedSkillRowIcons(member.MitigationSkills, nowUtc, iconStartX, rowStart.Y, iconSize, ref drawnCount);

        ImGui.SetCursorPos(new Vector2(rowStart.X, rowStart.Y + rowHeight));
    }

    private void DrawMergedSkillRowIcons(
        IReadOnlyList<PartyMonitorService.SkillCooldownState> skills,
        DateTime nowUtc,
        float iconStartX,
        float rowY,
        float iconSize,
        ref int drawnCount)
    {
        foreach (var skill in skills)
        {
            var displaySkill = skill.WithDynamicTime(nowUtc);
            if (!ShouldShowSkill(displaySkill))
                continue;

            ImGui.SetCursorPos(new Vector2(iconStartX + drawnCount * (iconSize + IconGap), rowY));
            DrawSkillIcon(displaySkill);
            drawnCount++;
        }
    }

    private void DrawSkillIcon(PartyMonitorService.SkillCooldownState state)
    {
        var iconSize = GetIconSize();
        var pos = ImGui.GetCursorScreenPos();
        var border = GetSkillStateColor(state);

        if (!KamiIconLoader.TryDrawIcon(state.Skill.ActionId, new Vector2(iconSize, iconSize)))
            ImGui.Dummy(new Vector2(iconSize, iconSize));

        var drawList = ImGui.GetWindowDrawList();
        if (state.IsActive && state.Skill.Category == SkillCategory.Mitigation)
            DrawActiveMitigationBackplate(drawList, pos, iconSize, config.PartyMonitor);
        else
            drawList.AddRectFilled(pos, pos + new Vector2(iconSize, iconSize), ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.22f)), 2f);

        if (state.IsActive)
            DrawActiveBuffBorder(drawList, pos, iconSize, config.PartyMonitor);
        else
            drawList.AddRect(pos, pos + new Vector2(iconSize, iconSize), ImGui.ColorConvertFloat4ToU32(border), 2f, ImDrawFlags.None, 1.2f);
        drawList.AddRect(pos + new Vector2(1f, 1f), pos + new Vector2(iconSize - 1f, iconSize - 1f), ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.55f)), 2f);

        if (state.IsActive && state.Skill.Category == SkillCategory.Mitigation)
            DrawActiveMitigationBadge(drawList, pos);

        var text = GetSkillStateText(state);
        if (!string.IsNullOrEmpty(text))
            DrawIconText(pos, iconSize, text, config.PartyMonitor);

        if (ImGui.IsItemHovered())
            DrawSkillTooltip(state);
    }

    private static Vector4 GetSkillStateColor(PartyMonitorService.SkillCooldownState state)
    {
        if (state.IsActive)
            return ActiveBuffColor;
        return state.IsReady ? ReadyColor : CooldownColor;
    }

    private static string GetSkillStateText(PartyMonitorService.SkillCooldownState state)
    {
        if (state.IsActive)
            return Math.Ceiling(state.RemainingActiveDuration).ToString("0");
        if (!state.IsReady)
            return Math.Ceiling(state.RemainingCooldown).ToString("0");
        return string.Empty;
    }

    private float GetIconSize()
        => Math.Clamp(config.PartyMonitor.IconSize, 20f, 48f);

    private static void DrawIconText(Vector2 iconPos, float iconSize, string text, PartyMonitorConfig cfg)
    {
        var drawList = ImGui.GetWindowDrawList();
        var scale = Math.Clamp(cfg.CountdownTextScale * (iconSize / 30f), 0.6f, 2.6f);
        var textSize = ImGui.CalcTextSize(text) * scale;
        var textY = cfg.CountdownTextBottomCenter
            ? iconSize - textSize.Y - 2f
            : (iconSize - textSize.Y) * 0.5f;
        var pos = iconPos + new Vector2((iconSize - textSize.X) * 0.5f, textY);

        if (scale != 1f)
            ImGui.SetWindowFontScale(scale);
        var outlineColor = ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 1f));
        var textColor = ImGui.ColorConvertFloat4ToU32(cfg.CountdownTextColor);
        drawList.AddText(pos + new Vector2(-1f, 0f), outlineColor, text);
        drawList.AddText(pos + new Vector2(1f, 0f), outlineColor, text);
        drawList.AddText(pos + new Vector2(0f, -1f), outlineColor, text);
        drawList.AddText(pos + new Vector2(0f, 1f), outlineColor, text);
        drawList.AddText(pos + new Vector2(1f, 1f), outlineColor, text);
        drawList.AddText(pos + new Vector2(0.35f, 0f), textColor, text);
        drawList.AddText(pos + new Vector2(-0.35f, 0f), textColor, text);
        drawList.AddText(pos + new Vector2(0f, 0.35f), textColor, text);
        drawList.AddText(pos, textColor, text);
        if (scale != 1f)
            ImGui.SetWindowFontScale(1f);
    }

    private static void DrawActiveMitigationBackplate(ImDrawListPtr drawList, Vector2 pos, float iconSize, PartyMonitorConfig cfg)
    {
        if (!cfg.EnhancedActiveStyle)
            return;

        var strength = Math.Clamp(cfg.ActiveGlowStrength, 0f, 2f);
        drawList.AddRectFilled(pos - new Vector2(3f, 3f), pos + new Vector2(iconSize + 3f, iconSize + 3f), ImGui.ColorConvertFloat4ToU32(new Vector4(0.72f, 0.46f, 0.02f, 0.38f * strength)), 5f);
        drawList.AddRectFilled(pos, pos + new Vector2(iconSize, iconSize), ImGui.ColorConvertFloat4ToU32(new Vector4(0.34f, 0.20f, 0.00f, 0.30f * strength)), 2f);
    }

    private static void DrawActiveBuffBorder(ImDrawListPtr drawList, Vector2 pos, float iconSize, PartyMonitorConfig cfg)
    {
        if (!cfg.EnhancedActiveStyle)
        {
            drawList.AddRect(pos, pos + new Vector2(iconSize, iconSize), ImGui.ColorConvertFloat4ToU32(ActiveBuffColor), 2f, ImDrawFlags.None, 2f);
            return;
        }

        var strength = Math.Clamp(cfg.ActiveGlowStrength, 0f, 2f);
        var outerMin = pos - new Vector2(2f, 2f);
        var outerMax = pos + new Vector2(iconSize + 2f, iconSize + 2f);
        drawList.AddRect(outerMin, outerMax, ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.92f, 0.24f, 0.95f)), 4f, ImDrawFlags.None, 1.6f + 0.8f * strength);
        drawList.AddRect(pos - new Vector2(0.5f, 0.5f), pos + new Vector2(iconSize + 0.5f, iconSize + 0.5f), ImGui.ColorConvertFloat4ToU32(ActiveBuffInnerColor), 3f, ImDrawFlags.None, 1.4f + 0.7f * strength);
        drawList.AddRect(pos + new Vector2(2f, 2f), pos + new Vector2(iconSize - 2f, iconSize - 2f), ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.96f, 0.58f, 0.45f + 0.2f * strength)), 2f, ImDrawFlags.None, 1.0f);
    }

    private static void DrawActiveMitigationBadge(ImDrawListPtr drawList, Vector2 pos)
    {
        var badgeMin = pos + new Vector2(1f, 1f);
        var badgeMax = pos + new Vector2(14f, 13f);
        drawList.AddRectFilled(badgeMin, badgeMax, ImGui.ColorConvertFloat4ToU32(new Vector4(0.08f, 0.62f, 0.92f, 0.92f)), 2f);
        drawList.AddRect(badgeMin, badgeMax, ImGui.ColorConvertFloat4ToU32(new Vector4(0.80f, 0.96f, 1f, 0.88f)), 2f);
        drawList.AddText(badgeMin + new Vector2(3f, -1f), ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.90f)), "效");
        drawList.AddText(badgeMin + new Vector2(2f, -2f), ImGui.ColorConvertFloat4ToU32(TitleColor), "效");
    }

    private static void DrawSkillTooltip(PartyMonitorService.SkillCooldownState state)
    {
        ImGui.BeginTooltip();
        try
        {
            ImGui.TextUnformatted(state.Skill.Name);
            if (state.IsActive)
                ImGui.TextColored(ActiveColor, $"激活中 {state.RemainingActiveDuration:0}s");
            else if (state.IsReady)
                ImGui.TextColored(ReadyColor, "就绪");
            else
                ImGui.TextColored(CooldownColor, $"冷却中 {state.RemainingCooldown:0}s");
        }
        finally
        {
            ImGui.EndTooltip();
        }
    }

    private void DrawFoodOverlay(IReadOnlyList<PartyMonitorService.PartyMemberState> members)
    {
        if (!config.PartyMonitor.MonitorFood)
            return;

        var warningMinutes = Math.Clamp(config.PartyMonitor.FoodExpiryWarningMinutes, 1, 60);
        var warningSeconds = warningMinutes * 60f;
        var warningCount = 0;
        foreach (var member in members)
        {
            if (!member.HasFood || member.FoodRemainingSeconds <= warningSeconds)
                warningCount++;
        }

        if (warningCount == 0)
            return;

        DrawGroupHeader($"需补食 ({warningCount})", OrangeColor);
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var usedWidth = 0f;
        const float chipGap = 6f;
        foreach (var member in members)
        {
            if (member.HasFood && member.FoodRemainingSeconds > warningSeconds)
                continue;

            var label = GetMemberDisplayName(member);
            var itemWidth = Math.Min(128f, ImGui.CalcTextSize(label).X + 12f);

            if (usedWidth > 0f && usedWidth + chipGap + itemWidth > availableWidth)
            {
                usedWidth = 0f;
            }

            if (usedWidth > 0f)
                ImGui.SameLine(0f, chipGap);

            var warningColor = member.HasFood ? FoodExpiringColor : FoodMissingColor;
            DrawNameChip(label, GetJobColor(member.JobId), itemWidth, warningColor, 2f);
            if (ImGui.IsItemHovered())
            {
                if (member.HasFood)
                {
                    ImGui.SetTooltip(
                        $"食物剩余时间：{FormatFoodRemaining(member.FoodRemainingSeconds)}\n" +
                        $"提醒阈值：{warningMinutes}分钟");
                }
                else
                {
                    ImGui.SetTooltip("未检测到食物效果");
                }
            }
            usedWidth += (usedWidth > 0f ? chipGap : 0f) + itemWidth;
        }
        ImGui.Dummy(new Vector2(0f, 2f));
    }

    private static string FormatFoodRemaining(float remainingSeconds)
    {
        var seconds = Math.Max(0, (int)Math.Ceiling(remainingSeconds));
        if (seconds >= 3600)
            return $"{seconds / 3600}小时 {(seconds % 3600) / 60}分";
        if (seconds >= 60)
            return $"{seconds / 60}分 {seconds % 60}秒";
        return $"{seconds}秒";
    }

    private static void DrawShadowText(string text, Vector4 color, bool bold = false, float width = 0f)
    {
        var pos = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddText(pos + new Vector2(1f, 1f), ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.92f)), text);
        drawList.AddText(pos, ImGui.ColorConvertFloat4ToU32(color), text);

        var textSize = ImGui.CalcTextSize(text);
        ImGui.Dummy(new Vector2(Math.Max(width, textSize.X), textSize.Y + (bold ? 1f : 0f)));
    }

    private static void DrawClippedShadowText(string text, Vector4 color, float width)
    {
        var pos = ImGui.GetCursorScreenPos();
        var height = ImGui.GetTextLineHeight();
        var clipMax = pos + new Vector2(width, height + 2f);
        var drawList = ImGui.GetWindowDrawList();
        drawList.PushClipRect(pos, clipMax, true);
        drawList.AddText(pos + new Vector2(1f, 1f), ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.92f)), text);
        drawList.AddText(pos, ImGui.ColorConvertFloat4ToU32(color), text);
        drawList.PopClipRect();
        ImGui.Dummy(new Vector2(width, height + 1f));
    }

    private static void DrawNameChip(
        string text,
        Vector4 color,
        float width,
        Vector4? warningBorderColor = null,
        float warningBorderThickness = 1f)
    {
        var pos = ImGui.GetCursorScreenPos();
        var height = ImGui.GetTextLineHeight() + 5f;
        var drawList = ImGui.GetWindowDrawList();
        drawList.AddRectFilled(pos, pos + new Vector2(width, height), ImGui.ColorConvertFloat4ToU32(new Vector4(color.X, color.Y, color.Z, 0.82f)), 3f);
        var borderColor = warningBorderColor ?? new Vector4(0f, 0f, 0f, 0.58f);
        drawList.AddRect(
            pos,
            pos + new Vector2(width, height),
            ImGui.ColorConvertFloat4ToU32(borderColor),
            3f,
            ImDrawFlags.None,
            warningBorderThickness);
        var textPos = pos + new Vector2(6f, 2f);
        drawList.PushClipRect(pos + new Vector2(4f, 0f), pos + new Vector2(width - 4f, height), true);
        drawList.AddText(textPos + new Vector2(1f, 1f), ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.92f)), text);
        drawList.AddText(textPos, ImGui.ColorConvertFloat4ToU32(TitleColor), text);
        drawList.PopClipRect();
        ImGui.Dummy(new Vector2(width, height));
    }

    private float GetNameChipWidth(string text)
        => Math.Clamp(ImGui.CalcTextSize(text).X + 12f, 34f, JobColumnWidth);

    private static string GetShortJobName(uint jobId)
    {
        return jobId switch
        {
            19 => "骑士",
            20 => "武僧",
            21 => "战士",
            22 => "龙骑",
            23 => "诗人",
            24 => "白魔",
            25 => "黑魔",
            27 => "召唤",
            28 => "学者",
            30 => "忍者",
            31 => "机工",
            32 => "暗骑",
            33 => "占星",
            34 => "武士",
            35 => "赤魔",
            37 => "绝枪",
            38 => "舞者",
            39 => "镰刀",
            40 => "贤者",
            41 => "蝰蛇",
            42 => "绘灵",
            _ => "未知",
        };
    }

    private string GetMemberDisplayName(PartyMonitorService.PartyMemberState member)
    {
        if (config.PartyMonitor.AnonymousMode)
            return GetShortJobName(member.JobId);

        return string.IsNullOrWhiteSpace(member.Name) ? GetShortJobName(member.JobId) : member.Name;
    }

    private Vector4 GetJobColor(uint jobId)
    {
        var color = config.GetThemeBarColor(GetJobName(jobId));
        return new Vector4(color.X, color.Y, color.Z, 1f);
    }

    private string FormatMemberName(PartyMonitorService.PartyMemberState member)
    {
        var jobName = GetJobName(member.JobId);
        return string.IsNullOrEmpty(jobName)
            ? member.Name
            : $"{member.Name}[{jobName}]";
    }

    public static string GetJobName(uint jobId) => ResolveJobDisplayName(jobId);

    private static string ResolveJobDisplayName(uint jobId)
    {
        return jobId switch
        {
            19 => "骑士",
            20 => "武僧",
            21 => "战士",
            22 => "龙骑士",
            23 => "吟游诗人",
            24 => "白魔法师",
            25 => "黑魔法师",
            27 => "召唤师",
            28 => "学者",
            30 => "忍者",
            31 => "机工士",
            32 => "暗黑骑士",
            33 => "占星术士",
            34 => "武士",
            35 => "赤魔法师",
            36 => "青魔法师",
            37 => "绝枪战士",
            38 => "舞者",
            39 => "钐镰客",
            40 => "贤者",
            41 => "蝰蛇剑士",
            42 => "绘灵法师",
            _ => jobId.ToString(),
        };
    }
}
