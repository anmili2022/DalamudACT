using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal sealed partial class SettingsWindow
{
    private enum QuickSettingsPage
    {
        Basic,
        Appearance,
        Timeline,
        Tts,
        Status,
    }

    private QuickSettingsPage quickSettingsPage = QuickSettingsPage.Basic;

    private void DrawQuickSettings()
    {
        var currentSize = Size ?? new Vector2(790f, 600f);
        Size = new Vector2(Math.Max(currentSize.X, 790f), Math.Max(currentSize.Y, 600f));
        DrawQuickSettingsShell();
    }

    private void DrawQuickSettingsShell()
    {
        var t = UiThemeColors.Get(config.SelectedUiTheme);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10f, 10f));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 14f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 10f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 10f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 7f));
        ImGui.PushStyleColor(ImGuiCol.Text, t.Text);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, t.TextDisabled);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, t.Panel);
        ImGui.PushStyleColor(ImGuiCol.Border, t.Border);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, t.AccentSoft);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, t.PanelDark);
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, t.AccentSoft);
        ImGui.PushStyleColor(ImGuiCol.CheckMark, t.CheckMark);
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, t.Accent);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, t.Accent);
        ImGui.PushStyleColor(ImGuiCol.Button, t.AccentSoft);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, t.PanelDark);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, t.Accent);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, t.WindowBg);
        ImGui.PushStyleColor(ImGuiCol.Header, t.AccentSoft);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, t.PanelDark);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, t.PanelDark);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarBg, t.PanelDark);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrab, t.AccentSoft);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabHovered, t.Accent);
        ImGui.PushStyleColor(ImGuiCol.ScrollbarGrabActive, t.Accent);
        try
        {
            DrawQuickConsoleHeader(t);

            var fullWidth = ImGui.GetContentRegionAvail().X;
            var fullHeight = ImGui.GetContentRegionAvail().Y;
            var navWidth = 154f;
            var sideWidth = 196f;
            var contentWidth = Math.Max(260f, fullWidth - navWidth - sideWidth - 16f);
            const ImGuiWindowFlags childFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

            ImGui.PushStyleColor(ImGuiCol.ChildBg, t.PanelDark);
            ImGui.BeginChild("##quick_nav", new Vector2(navWidth, fullHeight), true, childFlags);
            ImGui.PopStyleColor();
            try
            {
                DrawQuickNavItem("基础", QuickSettingsPage.Basic);
                DrawQuickNavItem("外观", QuickSettingsPage.Appearance);
                DrawQuickNavItem("时间轴", QuickSettingsPage.Timeline);
                DrawQuickNavItem("TTS", QuickSettingsPage.Tts);
                DrawQuickNavItem("状态", QuickSettingsPage.Status);
            }
            finally
            {
                ImGui.EndChild();
            }

            ImGui.SameLine(0f, 8f);
            ImGui.BeginChild("##quick_content", new Vector2(contentWidth, fullHeight), true, childFlags);
            try
            {
                DrawQuickPageContent();
            }
            finally
            {
                ImGui.EndChild();
            }

            ImGui.SameLine(0f, 8f);
            ImGui.BeginChild("##quick_status", new Vector2(sideWidth, fullHeight), true, childFlags);
            try
            {
                DrawQuickStatusPanel(t.Ok, t.Accent);
            }
            finally
            {
                ImGui.EndChild();
            }

        }
        finally
        {
            ImGui.PopStyleColor(21);
            ImGui.PopStyleVar(5);
        }
    }

    private void DrawQuickConsoleHeader(UiThemeColors theme)
    {
        const float headerHeight = 50f;
        ImGui.PushStyleColor(ImGuiCol.ChildBg, theme.PanelDark);
        ImGui.BeginChild("##quick_header", new Vector2(0f, headerHeight), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleColor();
        try
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(theme.Accent, "DalamudACT");
            ImGui.SameLine(0f, 6f);
            ImGui.TextUnformatted($"设置 / v{PluginVersion}");
            ImGui.SameLine();
            ImGui.TextColored(theme.Ok, "已启用");

            const float advancedButtonWidth = 92f;
            const float closeButtonWidth = 76f;
            var right = ImGui.GetWindowContentRegionMax().X;
            var buttonY = MathF.Max(0f, (headerHeight - ImGui.GetFrameHeight()) * 0.5f);
            ImGui.SetCursorPos(new Vector2(MathF.Max(0f, right - advancedButtonWidth - closeButtonWidth - 16f), buttonY));
            if (ImGui.Button("完整设置", new Vector2(advancedButtonWidth, 0f)))
            {
                showAdvancedSettings = true;
                pendingWindowSize = new Vector2(1044f, 600f);
            }

            ImGui.SameLine(0f, 8f);
            if (ImGui.Button("关闭窗口", new Vector2(closeButtonWidth, 0f)))
                IsOpen = false;
        }
        finally
        {
            ImGui.EndChild();
        }
    }

    private void DrawQuickNavItem(string label, QuickSettingsPage page)
    {
        var t = UiThemeColors.Get(config.SelectedUiTheme);
        var selected = quickSettingsPage == page;
        if (selected)
            ImGui.PushStyleColor(ImGuiCol.Button, t.WindowBg);
        else
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0f, 0f, 0f, 0f));

        try
        {
            if (ImGui.Button($"      {label}##quick_nav_{page}", new Vector2(-1f, 34f)))
                quickSettingsPage = page;

            DrawQuickNavIcon(page, selected);
        }
        finally
        {
            ImGui.PopStyleColor();
        }
    }

    private void DrawQuickNavIcon(QuickSettingsPage page, bool selected)
    {
        var t = UiThemeColors.Get(config.SelectedUiTheme);
        var min = ImGui.GetItemRectMin();
        var center = min + new Vector2(18f, 17f);
        var drawList = ImGui.GetWindowDrawList();
        var color = ImGui.ColorConvertFloat4ToU32(selected ? t.Accent : t.TextDisabled);
        var fill = ImGui.ColorConvertFloat4ToU32(selected ? t.AccentSoft : new Vector4(t.WindowBg.X, t.WindowBg.Y, t.WindowBg.Z, 0.65f));

        switch (page)
        {
            case QuickSettingsPage.Basic:
                drawList.AddRectFilled(center - new Vector2(8f, 8f), center + new Vector2(8f, 8f), fill, 4f);
                drawList.AddRect(center - new Vector2(8f, 8f), center + new Vector2(8f, 8f), color, 4f, ImDrawFlags.None, 1.5f);
                break;
            case QuickSettingsPage.Appearance:
                drawList.AddCircleFilled(center, 8f, fill, 24);
                drawList.AddCircle(center, 8f, color, 24, 1.5f);
                drawList.AddCircleFilled(center + new Vector2(3f, -3f), 2.2f, color, 12);
                break;
            case QuickSettingsPage.Timeline:
                drawList.AddLine(center + new Vector2(-8f, 5f), center + new Vector2(8f, 5f), color, 1.6f);
                drawList.AddLine(center + new Vector2(-6f, 1f), center + new Vector2(5f, -5f), color, 1.6f);
                drawList.AddCircleFilled(center + new Vector2(-7f, 5f), 2f, color, 10);
                drawList.AddCircleFilled(center + new Vector2(8f, 5f), 2f, color, 10);
                break;
            case QuickSettingsPage.Tts:
                drawList.AddCircleFilled(center + new Vector2(-5f, 0f), 5f, fill, 18);
                drawList.AddCircle(center + new Vector2(-5f, 0f), 5f, color, 18, 1.5f);
                drawList.AddLine(center + new Vector2(2f, -5f), center + new Vector2(8f, -8f), color, 1.5f);
                drawList.AddLine(center + new Vector2(2f, 5f), center + new Vector2(8f, 8f), color, 1.5f);
                drawList.AddLine(center + new Vector2(4f, 0f), center + new Vector2(10f, 0f), color, 1.5f);
                break;
            case QuickSettingsPage.Status:
                drawList.AddCircleFilled(center, 8f, fill, 16);
                drawList.AddCircle(center, 8f, color, 16, 1.5f);
                drawList.AddCircleFilled(center, 3f, color, 12);
                break;
        }
    }

    private void DrawQuickPageContent()
    {
        switch (quickSettingsPage)
        {
            case QuickSettingsPage.Basic:
                DrawQuickBasicPage();
                break;
            case QuickSettingsPage.Appearance:
                DrawQuickAppearancePage();
                break;
            case QuickSettingsPage.Timeline:
                DrawQuickTimelinePage();
                break;
            case QuickSettingsPage.Tts:
                DrawQuickTtsPage();
                break;
            case QuickSettingsPage.Status:
                DrawQuickStatusPage();
                break;
        }
    }

    private void DrawQuickPanel(string title, Action draw)
    {
        var t = UiThemeColors.Get(config.SelectedUiTheme);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(t.WindowBg.X, t.WindowBg.Y, t.WindowBg.Z, 1f));
        ImGui.BeginChild($"##quick_panel_{title}", new Vector2(0f, 0f), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        ImGui.PopStyleColor();
        try
        {
            ImGui.TextUnformatted(title);
            ImGui.Separator();
            draw();
        }
        finally
        {
            ImGui.EndChild();
        }
    }

    private void DrawQuickSettingRow(string label, string? description, Action drawControl)
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings;
        if (!ImGui.BeginTable($"##row_{label}", 2, flags))
            return;

        try
        {
            ImGui.TableSetupColumn("label", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("control", ImGuiTableColumnFlags.WidthFixed, 132f);
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted(label);
            if (!string.IsNullOrWhiteSpace(description))
                ImGui.TextDisabled(description);
            ImGui.TableSetColumnIndex(1);
            ImGui.SetNextItemWidth(-1f);
            drawControl();
        }
        finally
        {
            ImGui.EndTable();
        }

        ImGui.Separator();
    }

    private void DrawQuickBasicPage()
    {
        DrawQuickPanel("窗口控制", () =>
        {
            DrawQuickWindowControlRow(
                "统计面板",
                "DPS / HPS / 承伤统计",
                config.ShowStatsPanel,
                value =>
                {
                    config.ShowStatsPanel = value;
                    config.ShowDemoPanel = value;
                },
                config.LockFloatingStatsWindow,
                value => config.LockFloatingStatsWindow = value);
            DrawQuickWindowControlRow(
                "技能监控",
                "团辅、减伤、食物、自定义技能",
                config.PartyMonitor.EnablePartyMonitor && config.PartyMonitor.ShowPartyMonitorWindow,
                value =>
                {
                    config.PartyMonitor.EnablePartyMonitor = value;
                    config.PartyMonitor.ShowPartyMonitorWindow = value;
                },
                config.PartyMonitor.LockPartyMonitorWindow,
                value => config.PartyMonitor.LockPartyMonitorWindow = value);
            DrawQuickWindowControlRow(
                "时间轴",
                "当前副本机制倒计时",
                config.ShowTimelineWindow,
                value => config.ShowTimelineWindow = value,
                config.LockTimelineWindow,
                value => config.LockTimelineWindow = value);
            DrawQuickWindowControlRow(
                "状态监控",
                "目标、自身、触发状态排查",
                config.StatusObserver.ShowWindow,
                value => config.StatusObserver.ShowWindow = value,
                config.StatusObserver.LockWindow,
                value => config.StatusObserver.LockWindow = value);
        });
    }

    private void DrawQuickWindowControlRow(
        string label,
        string description,
        bool showValue,
        Action<bool> applyShow,
        bool lockValue,
        Action<bool> applyLock)
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.SizingStretchProp | ImGuiTableFlags.NoSavedSettings;
        if (!ImGui.BeginTable($"##window_control_{label}", 3, flags))
            return;

        try
        {
            ImGui.TableSetupColumn("label", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("show", ImGuiTableColumnFlags.WidthFixed, 72f);
            ImGui.TableSetupColumn("lock", ImGuiTableColumnFlags.WidthFixed, 72f);
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted(label);
            ImGui.TextDisabled(description);

            ImGui.TableSetColumnIndex(1);
            var show = showValue;
            if (ImGui.Checkbox($"显示##show_{label}", ref show))
            {
                applyShow(show);
                config.Save();
            }

            ImGui.TableSetColumnIndex(2);
            var locked = lockValue;
            if (ImGui.Checkbox($"锁定##lock_{label}", ref locked))
            {
                applyLock(locked);
                config.Save();
            }
        }
        finally
        {
            ImGui.EndTable();
        }

        ImGui.Separator();
    }

    private void DrawQuickAppearancePage()
    {
        DrawQuickPanel("透明度与样式", () =>
        {
            DrawThemeSwitcher();
            ImGui.Separator();
            DrawQuickFloatRow("主界面透明度", null, config.WindowOpacity, 0.2f, 1f, "%.2f", value => config.WindowOpacity = value);
            DrawQuickFloatRow("DPS统计面板透明度", null, config.FloatingStatsOpacity, 0f, 1f, "%.2f", value => config.FloatingStatsOpacity = value);
            DrawQuickFloatRow("技能监控窗口透明度", null, config.PartyMonitor.PartyMonitorOpacity, 0f, 1f, "%.2f", value => config.PartyMonitor.PartyMonitorOpacity = value);
            DrawQuickFloatRow("时间轴窗口透明度", null, config.TimelineWindowOpacity, 0.2f, 1f, "%.2f", value => config.TimelineWindowOpacity = value);
            DrawQuickFloatRow("状态监控透明度", null, config.StatusObserver.WindowOpacity, 0f, 1f, "%.2f", value => config.StatusObserver.WindowOpacity = value);

            ImGui.Dummy(new Vector2(0f, 4f));
            var currentStyle = config.FloatingStatsDisplayStyle;
            var styleLabel = currentStyle switch
            {
                FloatingStatsDisplayStyle.Classic => "Classic",
                FloatingStatsDisplayStyle.Ikegami => "Ikegami",
                FloatingStatsDisplayStyle.Minimal => "Minimal",
                _ => "Classic",
            };

            if (ImGui.BeginCombo("展示模式", styleLabel))
            {
                foreach (FloatingStatsDisplayStyle style in Enum.GetValues(typeof(FloatingStatsDisplayStyle)))
                {
                    var label = style switch
                    {
                        FloatingStatsDisplayStyle.Classic => "Classic",
                        FloatingStatsDisplayStyle.Ikegami => "Ikegami",
                        FloatingStatsDisplayStyle.Minimal => "Minimal",
                        _ => style.ToString(),
                    };

                    if (ImGui.Selectable(label, currentStyle == style))
                        config.SwitchFloatingStatsDisplayStyle(style);
                }

                ImGui.EndCombo();
            }

            DrawQuickStyleParams(config.FloatingStatsDisplayStyle);
        });
    }

    private void DrawQuickTimelinePage()
    {
        DrawQuickPanel("时间轴", () =>
        {
            DrawQuickBoolRow("进入副本时自动下载时间轴", "同一区域每小时最多下载一次", config.TimelineAutoDownloadOnEnter, value => config.TimelineAutoDownloadOnEnter = value);
            DrawQuickIntRow("显示未来秒数", null, config.TimelineVisibleSeconds, 10, 120, value => config.TimelineVisibleSeconds = value);
            DrawQuickIntRow("最大显示条数", null, config.TimelineMaxVisibleEntries, 5, 50, value => config.TimelineMaxVisibleEntries = value);
            DrawQuickFloatRow("时间条间隔", null, config.TimelineRowGap, 0f, 20f, "%.1f", value => config.TimelineRowGap = value);
            ImGui.TextDisabled("目前时间轴仅涵盖 7.x 版本内容。");
        });
    }

    private void DrawQuickTtsPage()
    {
        DrawQuickPanel("TTS 播报", () =>
        {
            DrawQuickBoolRow("启用时间轴 TTS", "需要 DailyRoutines 文本转语音", config.EnableTimelineDailyRoutinesTts, value => config.EnableTimelineDailyRoutinesTts = value);
            DrawQuickBoolRow("播报机制", "AOE / 死刑等分类提示", config.TimelineTtsMechanic, value => config.TimelineTtsMechanic = value);
            DrawQuickBoolRow("播报技能名", "机制名之外额外播报技能名称", config.TimelineTtsSkillName, value => config.TimelineTtsSkillName = value);
            DrawQuickBoolRow("播报应对方案", "读条ID 即时播，结算ID 到达时播", config.TimelineTtsResponse, value => config.TimelineTtsResponse = value);
            DrawQuickIntRow("TTS 提前秒数", null, config.TimelineTtsLeadSeconds, 0, 10, value => config.TimelineTtsLeadSeconds = value);
        });
    }

    private void DrawQuickStatusPage()
    {
        DrawQuickPanel("当前状态", () =>
        {
            ImGui.TextUnformatted($"版本: {PluginVersion}");
            ImGui.TextUnformatted($"时间轴: {(timelineService?.HasTimeline == true ? "已加载" : "未加载")}");
            ImGui.TextUnformatted($"定义: {timelineService?.DefinitionName ?? "-"}");
            ImGui.TextUnformatted($"运行: {(timelineService?.IsRunning == true ? $"是 ({timelineService.CurrentTimeSeconds:F1}s)" : "否")}");
            DrawQuickRuntimeModeControls();
            ImGui.TextUnformatted($"自动下载: {timelineService?.AutoDownloadStatusText ?? "-"}");
            ImGui.Dummy(new Vector2(0f, 8f));
            DrawTimelinePathButton(showFullPath: true);
        });
    }

    private void DrawQuickRuntimeModeControls()
    {
        var inDutyRecorderPlayback = DalamudApi.Conditions.Any(ConditionFlag.DutyRecorderPlayback);
        if (inDutyRecorderPlayback)
        {
            var replayStatsMode = config.ReplayStatsMode;
            if (ImGui.Checkbox("回顾模式", ref replayStatsMode))
            {
                config.ReplayStatsMode = replayStatsMode;
                config.Save();
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("开启后，战斗回顾播放会按真实战斗进入统计、战斗流水和时间轴。与完整设置里的回顾模式相同。");
        }

        var enableDebugLog = config.EnableDebugLog;
        if (ImGui.Checkbox("启用调试日志", ref enableDebugLog))
        {
            config.EnableDebugLog = enableDebugLog;
            LogHelper.EnableDebugLog = enableDebugLog;
            config.Save();
            LogHelper.Info("设置", enableDebugLog ? "已从简易设置中启用调试日志。" : "已从简易设置中关闭调试日志。");
        }
    }

    private void DrawQuickStatusPanel(Vector4 ok, Vector4 accent)
    {
        ImGui.TextUnformatted("当前状态");
        ImGui.Separator();
        ImGui.TextColored(accent, PluginVersion);
        ImGui.TextDisabled("release version");
        ImGui.Dummy(new Vector2(0f, 6f));
        DrawThemeSwitcher();
        ImGui.Dummy(new Vector2(0f, 6f));
        ImGui.TextUnformatted("快捷入口");
        if (ImGui.Button("打开战斗流水", new Vector2(-1f, 0f)))
            openCombatTimelineWindow();
        if (ImGui.Button("已有时间轴", new Vector2(-1f, 0f)))
            openTimelineListWindow();
        DrawTimelinePathButton(showFullPath: false);
        ImGui.Dummy(new Vector2(0f, 6f));
        if (ImGui.Button("还原默认UI", new Vector2(-1f, 0f)))
        {
            config.ResetUiSettings();
            config.Save();
        }
        if (ImGui.Button("保存当前UI为默认", new Vector2(-1f, 0f)))
        {
            config.SaveCurrentUiAsDefault();
            config.Save();
        }
    }

    private void DrawTimelinePathButton(bool showFullPath)
    {
        var path = timelineService?.SourcePath ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            ImGui.TextDisabled("时间轴文件: 未加载");
            return;
        }

        ImGui.TextDisabled("时间轴文件");
        var fileName = Path.GetFileName(path);
        var buttonLabel = string.IsNullOrWhiteSpace(fileName)
            ? "当前时间轴"
            : fileName;
        if (ImGui.Button(buttonLabel, new Vector2(-1f, 0f)))
            OpenTimelineFolder(path);

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            ImGui.SetClipboardText(path);
            timelineRemoteStatusText = "已复制时间轴文件路径。";
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"左键打开时间轴文件的文件夹\n右键复制时间轴文件路径\n{path}");

        if (showFullPath)
            ImGui.TextWrapped(path);
    }

    private static void OpenTimelineFolder(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return;

            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return;

            Process.Start(new ProcessStartInfo
            {
                FileName = directory,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            LogHelper.Warning("时间轴", ex, $"打开时间轴文件夹失败：{path}");
        }
    }

    private void DrawQuickBoolRow(string label, string? description, bool value, Action<bool> apply)
    {
        DrawQuickSettingRow(label, description, () =>
        {
            var current = value;
            if (ImGui.Checkbox($"##{label}", ref current))
            {
                apply(current);
                config.Save();
            }
        });
    }

    private void DrawQuickFloatRow(string label, string? description, float value, float min, float max, string format, Action<float> apply)
    {
        DrawQuickSettingRow(label, description, () =>
        {
            var current = value;
            if (ImGui.SliderFloat($"##{label}", ref current, min, max, format))
            {
                apply(current);
                config.Save();
            }
        });
    }

    private void DrawQuickIntRow(string label, string? description, int value, int min, int max, Action<int> apply)
    {
        DrawQuickSettingRow(label, description, () =>
        {
            var current = value;
            if (ImGui.SliderInt($"##{label}", ref current, min, max))
            {
                apply(current);
                config.Save();
            }
        });
    }

    private void DrawQuickWindowSection()
    {
        if (!DrawFirstLevelHeader("窗口显隐与锁定", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var showStats = config.ShowStatsPanel;
        if (ImGui.Checkbox("显示统计面板", ref showStats))
        {
            config.ShowStatsPanel = showStats;
            config.ShowDemoPanel = showStats;
            config.Save();
        }

        ImGui.SameLine(0f, 12f);
        var lockStats = config.LockFloatingStatsWindow;
        if (ImGui.Checkbox("锁定统计面板", ref lockStats))
        {
            config.LockFloatingStatsWindow = lockStats;
            config.Save();
        }

        var enableParty = config.PartyMonitor.EnablePartyMonitor && config.PartyMonitor.ShowPartyMonitorWindow;
        if (ImGui.Checkbox("显示监控窗口", ref enableParty))
        {
            config.PartyMonitor.EnablePartyMonitor = enableParty;
            config.PartyMonitor.ShowPartyMonitorWindow = enableParty;
            config.Save();
        }

        ImGui.SameLine(0f, 12f);
        var lockParty = config.PartyMonitor.LockPartyMonitorWindow;
        if (ImGui.Checkbox("锁定监控窗口", ref lockParty))
        {
            config.PartyMonitor.LockPartyMonitorWindow = lockParty;
            config.Save();
        }

        var showTimeline = config.ShowTimelineWindow;
        if (ImGui.Checkbox("显示时间轴窗口", ref showTimeline))
        {
            config.ShowTimelineWindow = showTimeline;
            config.Save();
        }

        ImGui.SameLine(0f, 12f);
        var lockTimeline = config.LockTimelineWindow;
        if (ImGui.Checkbox("锁定时间轴窗口", ref lockTimeline))
        {
            config.LockTimelineWindow = lockTimeline;
            config.Save();
        }

        var showStatus = config.StatusObserver.ShowWindow;
        if (ImGui.Checkbox("显示状态监控窗口", ref showStatus))
        {
            config.StatusObserver.ShowWindow = showStatus;
            config.Save();
        }

        ImGui.SameLine(0f, 12f);
        var lockStatus = config.StatusObserver.LockWindow;
        if (ImGui.Checkbox("锁定状态监控窗口", ref lockStatus))
        {
            config.StatusObserver.LockWindow = lockStatus;
            config.Save();
        }
    }

    private void DrawQuickOpacitySection()
    {
        if (!DrawFirstLevelHeader("透明度", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var mainOpacity = config.WindowOpacity;
        if (ImGui.SliderFloat("主界面透明度", ref mainOpacity, 0.2f, 1f))
        {
            config.WindowOpacity = mainOpacity;
            config.Save();
        }

        var statsOpacity = config.FloatingStatsOpacity;
        if (ImGui.SliderFloat("DPS统计面板透明度", ref statsOpacity, 0f, 1f))
        {
            config.FloatingStatsOpacity = statsOpacity;
            config.Save();
        }

        var partyOpacity = config.PartyMonitor.PartyMonitorOpacity;
        if (ImGui.SliderFloat("技能监控窗口透明度", ref partyOpacity, 0f, 1f))
        {
            config.PartyMonitor.PartyMonitorOpacity = partyOpacity;
            config.Save();
        }

        var timelineOpacity = config.TimelineWindowOpacity;
        if (ImGui.SliderFloat("时间轴窗口透明度", ref timelineOpacity, 0.2f, 1f))
        {
            config.TimelineWindowOpacity = timelineOpacity;
            config.Save();
        }

        var statusOpacity = config.StatusObserver.WindowOpacity;
        if (ImGui.SliderFloat("状态监控透明度", ref statusOpacity, 0f, 1f))
        {
            config.StatusObserver.WindowOpacity = statusOpacity;
            config.Save();
        }
    }

    private void DrawQuickStyleSection()
    {
        if (!DrawFirstLevelHeader("悬浮 DPS 样式", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var currentStyle = config.FloatingStatsDisplayStyle;
        var styleLabel = currentStyle switch
        {
            FloatingStatsDisplayStyle.Classic => "Classic",
            FloatingStatsDisplayStyle.Ikegami => "Ikegami",
            FloatingStatsDisplayStyle.Minimal => "Minimal",
            _ => "Classic",
        };

        if (ImGui.BeginCombo("展示模式", styleLabel))
        {
            foreach (FloatingStatsDisplayStyle style in Enum.GetValues(typeof(FloatingStatsDisplayStyle)))
            {
                var label = style switch
                {
                    FloatingStatsDisplayStyle.Classic => "Classic",
                    FloatingStatsDisplayStyle.Ikegami => "Ikegami",
                    FloatingStatsDisplayStyle.Minimal => "Minimal",
                    _ => style.ToString(),
                };

                if (ImGui.Selectable(label, currentStyle == style))
                {
                    config.SwitchFloatingStatsDisplayStyle(style);
                    currentStyle = config.FloatingStatsDisplayStyle;
                }
            }

            ImGui.EndCombo();
        }

        DrawQuickStyleParams(currentStyle);
    }

    private void DrawQuickStyleParams(FloatingStatsDisplayStyle style)
    {
        switch (style)
        {
            case FloatingStatsDisplayStyle.Classic:
            {
                var rowHeight = config.FloatingStatsRowHeight;
                if (ImGui.SliderFloat("表格行高", ref rowHeight, 20f, 40f))
                {
                    config.FloatingStatsRowHeight = rowHeight;
                    config.Save();
                }

                break;
            }

            case FloatingStatsDisplayStyle.Ikegami:
            {
                var boxWidth = config.FloatingStatsIkegamiBoxWidth;
                if (ImGui.SliderFloat("小框宽度", ref boxWidth, 120f, 400f))
                {
                    config.FloatingStatsIkegamiBoxWidth = boxWidth;
                    config.Save();
                }

                var boxHeight = config.FloatingStatsIkegamiBoxHeight;
                if (ImGui.SliderFloat("小框高度", ref boxHeight, 30f, 200f))
                {
                    config.FloatingStatsIkegamiBoxHeight = boxHeight;
                    config.Save();
                }

                var minimal = config.FloatingStatsIkegamiMinimalMode;
                if (ImGui.Checkbox("极简模式", ref minimal))
                {
                    config.FloatingStatsIkegamiMinimalMode = minimal;
                    config.Save();
                }

                break;
            }

            case FloatingStatsDisplayStyle.Minimal:
            {
                var autoHeight = config.FloatingStatsMinimalAutoWindowHeight;
                if (ImGui.Checkbox("高度自动适配条目数", ref autoHeight))
                {
                    config.FloatingStatsMinimalAutoWindowHeight = autoHeight;
                    config.Save();
                }

                var rowHeight = config.FloatingStatsMinimalRowHeight;
                if (ImGui.SliderFloat("表格行高", ref rowHeight, 16f, 40f))
                {
                    config.FloatingStatsMinimalRowHeight = rowHeight;
                    config.Save();
                }

                var fontScale = config.FloatingStatsMinimalFontScale;
                if (ImGui.SliderFloat("字号缩放", ref fontScale, 0.5f, 2f))
                {
                    config.FloatingStatsMinimalFontScale = fontScale;
                    config.Save();
                }

                break;
            }
        }
    }

    private void DrawQuickTtsSection()
    {
        if (!DrawFirstLevelHeader("TTS 播报", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var ttsEnabled = config.EnableTimelineDailyRoutinesTts;
        if (ImGui.Checkbox("启用时间轴 TTS（需开启DR的文本转语音）", ref ttsEnabled))
        {
            config.EnableTimelineDailyRoutinesTts = ttsEnabled;
            config.Save();
        }

        var ttsMechanic = config.TimelineTtsMechanic;
        if (ImGui.Checkbox("播报机制（AOE/死刑分类）", ref ttsMechanic))
        {
            config.TimelineTtsMechanic = ttsMechanic;
            config.Save();
        }

        var ttsResponse = config.TimelineTtsResponse;
        if (ImGui.Checkbox("播报应对方案（分摊/分散/去背后..等）", ref ttsResponse))
        {
            config.TimelineTtsResponse = ttsResponse;
            config.Save();
        }

        var leadSeconds = config.TimelineTtsLeadSeconds;
        if (ImGui.SliderInt("TTS 提前秒数", ref leadSeconds, 0, 10))
        {
            config.TimelineTtsLeadSeconds = leadSeconds;
            config.Save();
        }
    }

    private void DrawQuickTimelineSection()
    {
        if (!DrawFirstLevelHeader("时间轴", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        var autoDl = config.TimelineAutoDownloadOnEnter;
        if (ImGui.Checkbox("进入副本时自动下载时间轴", ref autoDl))
        {
            config.TimelineAutoDownloadOnEnter = autoDl;
            config.Save();
        }

        ImGui.PushStyleColor(ImGuiCol.Text, UiThemeColors.Get(config.SelectedUiTheme).Accent);
        ImGui.TextUnformatted("注：目前时间轴仅涵盖 7.x 版本内容。6.x 及更早版本暂不支持。");
        ImGui.PopStyleColor();

        var visibleSeconds = config.TimelineVisibleSeconds;
        if (ImGui.SliderInt("显示未来秒数", ref visibleSeconds, 10, 120))
        {
            config.TimelineVisibleSeconds = visibleSeconds;
            config.Save();
        }

        var maxEntries = config.TimelineMaxVisibleEntries;
        if (ImGui.SliderInt("最大显示条数", ref maxEntries, 5, 50))
        {
            config.TimelineMaxVisibleEntries = maxEntries;
            config.Save();
        }

        var rowGap = config.TimelineRowGap;
        if (ImGui.SliderFloat("时间条间隔", ref rowGap, 0f, 20f))
        {
            config.TimelineRowGap = rowGap;
            config.Save();
        }

        ImGui.Dummy(new Vector2(0f, 4f));
        ImGui.Separator();
        ImGui.TextDisabled("DEBUG");

        if (timelineService is null)
        {
            ImGui.TextColored(new Vector4(1f, 0.3f, 0.3f, 1f), "TimelineService 未初始化");
            return;
        }

        var dbg = timelineService.DebugText;
        ImGui.TextUnformatted($"区域: {dbg}");
        ImGui.TextUnformatted($"定义: {timelineService.DefinitionName}");
        ImGui.TextUnformatted($"状态: {(timelineService.HasTimeline ? "已加载" : "未加载")}");
        ImGui.TextUnformatted($"运行: {(timelineService.IsRunning ? $"是 ({timelineService.CurrentTimeSeconds:F1}s)" : "否")}");
        ImGui.TextUnformatted($"自动下载: {timelineService.AutoDownloadStatusText}");
    }
}
