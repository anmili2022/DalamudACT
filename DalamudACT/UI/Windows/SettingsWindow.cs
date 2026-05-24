using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;


namespace DalamudACT;

/// <summary>
/// 设置窗口封装，负责插件配置项的 ImGui 编辑界面，包括窗口、战斗结束规则、页面显示、配色和历史记录操作。
/// 相关参考：
/// - https://dalamud.dev/
/// - https://dalamud.dev/api/
/// 调整 Window 生命周期、ImGui 控件交互或设置项保存行为前，先对照 Dalamud 文档。
/// </summary>
internal sealed class SettingsWindow : Window
{
    private static readonly string PluginVersion = typeof(SettingsWindow).Assembly.GetName().Version?.ToString() ?? "未知版本";
    private const int DebugRecordToggleCount = 9;
    private readonly PluginConfiguration config;
    private readonly LocalStatsService statsService;
    private readonly PartyMonitorService? monitorService;
    private readonly Action openMainWindow;
    private readonly Action toggleFloatingStatsPanel;
    private readonly Action openCombatTimelineWindow;
    private readonly Action openDebugCombatLogWindow;
    private readonly Dictionary<string, float> adaptiveChildHeights = new();
    private string floatingStyleShareCode = string.Empty;
    private string floatingStyleTransferStatusText = string.Empty;
    private string customFriendlyNpcNameInput = string.Empty;
    private string customFriendlyNpcStatusText = string.Empty;
    private readonly Dictionary<uint, string> customSkillActionIdInputs = new();
    private readonly Dictionary<uint, string> customSkillNameInputs = new();
    private readonly Dictionary<uint, string> customSkillCdInputs = new();
    private readonly Dictionary<uint, bool> customSkillIsMit = new();
    private uint customSkillSelectedJobId;
    private string? customSkillSelectedJobName;

    public SettingsWindow(
        PluginConfiguration config,
        LocalStatsService statsService,
        PartyMonitorService? monitorService,
        Action openMainWindow,
        Action toggleFloatingStatsPanel,
        Action openCombatTimelineWindow,
        Action openDebugCombatLogWindow)
        : base($"DPS统计 设置 v{PluginVersion}###SettingsWindow")
    {
        this.config = config;
        this.statsService = statsService;
        this.monitorService = monitorService;
        this.openMainWindow = openMainWindow;
        this.toggleFloatingStatsPanel = toggleFloatingStatsPanel;
        this.openCombatTimelineWindow = openCombatTimelineWindow;
        this.openDebugCombatLogWindow = openDebugCombatLogWindow;
        Size = new Vector2(620f, 760f);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        BgAlpha = Math.Clamp(config.WindowOpacity, 0.2f, 1f);

        ImGui.TextUnformatted("设置");
        ImGui.Separator();

        if (ImGui.Button("打开主界面"))
            openMainWindow();

        ImGui.SameLine();
        if (ImGui.Button(GetFloatingStatsButtonLabel()))
            toggleFloatingStatsPanel();

        ImGui.SameLine();
        if (ImGui.Button("打开战斗流水"))
            openCombatTimelineWindow();

        ImGui.SameLine();
        if (ImGui.Button("打开debug战斗记录"))
            openDebugCombatLogWindow();

        ImGui.Dummy(new Vector2(0f, 2f));

        DrawWindowSection();
        DrawFloatingPanelSection();
        DrawPartyMonitorSection();
        DrawMaintenanceSection();
    }

    private void DrawWindowSection()
    {
        if (!DrawFirstLevelHeader("窗口设置", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        DrawSettingCard(
            "##window_settings_card",
            "窗口与悬浮面板",
            "统一控制主窗口透明度、悬浮统计面板透明度、队友监控窗口透明度与锁定状态。",
            10f,
            () =>
            {
                var opacity = config.WindowOpacity;
                if (ImGui.SliderFloat("主界面透明度", ref opacity, 0.2f, 1f))
                {
                    config.WindowOpacity = opacity;
                    config.Save();
                }

                var statsPanelOpacity = config.FloatingStatsOpacity;
                if (ImGui.SliderFloat("DPS统计面板透明度", ref statsPanelOpacity, 0f, 1f))
                {
                    config.FloatingStatsOpacity = statsPanelOpacity;
                    config.Save();
                }

                var showStats = config.ShowStatsPanel;
                if (ImGui.Checkbox("显示悬浮DPS统计面板", ref showStats))
                {
                    config.ShowStatsPanel = showStats;
                    config.ShowDemoPanel = showStats;
                    config.Save();
                }

                var lockFloatingStatsWindow = config.LockFloatingStatsWindow;
                if (ImGui.Checkbox("锁定悬浮DPS窗口", ref lockFloatingStatsWindow))
                {
                    config.LockFloatingStatsWindow = lockFloatingStatsWindow;
                    config.Save();
                }

                DrawCompactHelp("锁定后不可拖动或缩放。", "启用后，悬浮窗口的位置和大小将无法手动修改。");

                        var enableParty = config.PartyMonitor.EnablePartyMonitor;
                if (ImGui.Checkbox("启用队友监控", ref enableParty))
                {
                    config.PartyMonitor.EnablePartyMonitor = enableParty;
                    config.Save();
                }

                ImGui.SameLine();
                var showParty = config.PartyMonitor.ShowPartyMonitorWindow;
                if (ImGui.Checkbox("显示队友监控窗口", ref showParty))
                {
                    config.PartyMonitor.ShowPartyMonitorWindow = showParty;
                    config.Save();
                }

                var partyOpacity = config.PartyMonitor.PartyMonitorOpacity;
                if (ImGui.SliderFloat("队友监控窗口透明度", ref partyOpacity, 0f, 1f))
                {
                    config.PartyMonitor.PartyMonitorOpacity = partyOpacity;
                    config.Save();
                }

                var lockPartyWindow = config.PartyMonitor.LockPartyMonitorWindow;
                if (ImGui.Checkbox("锁定队友监控窗口", ref lockPartyWindow))
                {
                    config.PartyMonitor.LockPartyMonitorWindow = lockPartyWindow;
                    config.Save();
                }
            });
    }

    private void DrawFloatingPanelSection()
    {
        if (!DrawFirstLevelHeader("悬浮DPS统计面板"))
            return;

        ImGui.Dummy(new Vector2(0f, 2f));

        DrawCombatSection();
        DrawVisibleTabsSection();
        DrawColumnsSection();
        DrawBarColorsSection();
        DrawThemePaletteSection();
    }

    private void DrawCombatSection()
    {
        if (!ImGui.CollapsingHeader("战斗结束设置"))
            return;

        DrawSettingCard(
            "##combat_end_rule_card",
            "战斗结束判定",
            "控制本地统计何时把当前战斗视为结束，并决定何时生成战斗快照。",
            6.4f,
            () =>
            {
                var currentRule = config.CombatEndRule;
                if (ImGui.RadioButton("全队脱战（PartyList）即为战斗结束", currentRule == CombatEndRule.PartyList))
                {
                    config.CombatEndRule = CombatEndRule.PartyList;
                    config.Save();
                }

                if (ImGui.RadioButton("全队脱战，且延迟 X 秒为战斗结束", currentRule == CombatEndRule.PartyListWithDelay))
                {
                    config.CombatEndRule = CombatEndRule.PartyListWithDelay;
                    config.Save();
                }

                if (config.CombatEndRule == CombatEndRule.PartyListWithDelay)
                {
                    var timeoutSeconds = config.EncounterTimeoutSeconds;
                    if (ImGui.SliderInt("X（秒）", ref timeoutSeconds, 5, 180))
                    {
                        config.EncounterTimeoutSeconds = timeoutSeconds;
                        config.Save();
                    }

                    DrawCompactHelp("延迟结束", "全队脱战后，额外等待 X 秒再视为战斗结束。");
                    return;
                }

                DrawCompactHelp("默认立即结束", "当前使用 PartyList 规则：全队脱战后立即视为战斗结束。");
            });
    }

    private void DrawVisibleTabsSection()
    {
        if (!ImGui.CollapsingHeader("悬浮面板样式管理"))
            return;

        DrawSettingCard(
            "##visible_tabs_card",
            "标签页显示",
            "控制悬浮面板中哪些标签页可见，关闭后对应页签不会在悬浮面板中显示。",
            6.2f,
            () =>
            {
                DrawVisibleTabToggleGrid();

                if (!config.HasAnyVisibleStatsTab())
                    ImGui.TextDisabled("当前所有页面都已隐藏。");
            });

        ImGui.Dummy(new Vector2(0f, 2f));
        DrawSettingCard(
            "##floating_display_style_card",
            "悬浮窗展示模式",
            "切换悬浮统计的展示样式；后续如果继续新增样式，也会从这里扩展。",
            10.8f,
            DrawFloatingDisplayStyleSection);

        ImGui.Dummy(new Vector2(0f, 2f));
        DrawSettingCard(
            "##floating_participant_card",
            "悬浮对象显示",
            "控制悬浮窗统计列表中显示玩家、友方 NPC 与敌方 NPC 的组合。",
            6.8f,
            DrawFloatingParticipantModeSection);

    }

    private void DrawColumnsSection()
    {
        if (!ImGui.CollapsingHeader("页面列显示"))
            return;

        DrawCompactHelp("以下设置同时作用于 DPS / HPS / 承伤。", "这里只改共享列；三页会一起生效。");
        ImGui.Dummy(new Vector2(0f, 2f));

        DrawSettingCard(
            "##columns_toggle_card",
            "共享列开关",
            "控制 DPS / HPS / 承伤 三个页面的共享列显示与显示人数。",
            6.6f,
            () =>
            {
                DrawSharedColumnToggleGrid();

                var visibleCount = config.DpsVisibleCount;
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.SliderInt("显示人数", ref visibleCount, 1, 24))
                {
                    config.DpsVisibleCount = visibleCount;
                    config.Save();
                }
            });

        ImGui.Dummy(new Vector2(0f, 2f));
        DrawSettingCard(
            "##columns_semantic_card",
            "列语义映射",
            "下表说明同一组共享设置在 DPS / HPS / 承伤 中分别对应的实际列含义。",
            6.4f,
            () =>
            {
                if (ImGui.CollapsingHeader("查看列语义映射"))
                {
                    DrawColumnSemanticTable();
                }
                else
                {
                    ImGui.TextDisabled("默认先收起这张对照表；只有在核对 DPS / HPS / 承伤 列含义时再展开。");
                }
            });

        ImGui.Dummy(new Vector2(0f, 2f));
        DrawSettingCard(
            "##columns_width_memory_card",
            "列宽记忆",
            "统计页与历史记录页的可拖拽列宽会自动写入配置，并在下次打开插件时恢复。",
            6.8f,
            () =>
            {
                DrawColumnWidthResetButtons();
                if (!PluginConfiguration.UsesLegacyFloatingTableLayout(config.FloatingStatsDisplayStyle))
                    DrawCompactHelp("当前样式不使用统计页列宽记忆。", "历史页列宽记忆仍然有效。");

                if (ImGui.CollapsingHeader("查看当前列宽记忆"))
                {
                    ImGui.TextDisabled("占比列与历史记录页的区域列保持自动拉伸，不参与固定宽度记忆。");
                    DrawStoredWidthTable();
                }
                else
                {
                    ImGui.TextDisabled("默认先收起当前列宽数值；需要核对保存结果时再展开查看。");
                }
            });
    }

    private void DrawBarColorsSection()
    {
        if (!ImGui.CollapsingHeader("占比条配色"))
            return;

        DrawSettingCard(
            "##bar_color_mode_card",
            "占比条颜色模式",
            "可在主题色和单色之间切换；单色模式下所有职业共用同一种颜色。",
            5.2f,
            () =>
            {
                var isThemeMode = config.BarColorMode == StatsBarColorMode.Theme;
                if (ImGui.RadioButton("主题色", isThemeMode))
                {
                    config.BarColorMode = StatsBarColorMode.Theme;
                    config.Save();
                }

                ImGui.SameLine();
                if (ImGui.RadioButton("单色", !isThemeMode))
                {
                    config.BarColorMode = StatsBarColorMode.Single;
                    config.Save();
                }

                if (config.BarColorMode == StatsBarColorMode.Single)
                {
                    var singleColor = config.GetSingleBarColor();
                    if (ImGui.ColorEdit4("单色条颜色", ref singleColor, ImGuiColorEditFlags.AlphaBar))
                    {
                        config.SetSingleBarColor(singleColor);
                        config.Save();
                    }

                    DrawCompactHelp("单色模式会忽略职业主题色。", "切回主题色后会恢复下面的职业配色。");
                    return;
                }

                DrawCompactHelp("主题色模式按职业使用各自颜色。", "可在下方调色板里自定义。");
            });
    }

    private void DrawThemePaletteSection()
    {
        if (!ImGui.CollapsingHeader("主题色调色板"))
            return;

        DrawSettingCard(
            "##theme_palette_card",
            "职业主题色调色板",
            "可统一调整主题色透明度，并分别调整每个职业的 RGB 颜色；主题色模式下，占比条会使用这里的配置。",
            10.8f,
            () =>
            {
                var themeBarOpacity = config.ThemeBarOpacity;
                if (ImGui.SliderFloat("主题色透明度", ref themeBarOpacity, 0.2f, 1f, "%.2f"))
                {
                    config.ThemeBarOpacity = themeBarOpacity;
                    config.Save();
                }

                DrawCompactHelp("主题色透明度只影响统一 Alpha。", "这里统一控制所有职业主题色的透明度；下方单职业颜色编辑只调整 RGB。");
                ImGui.Dummy(new Vector2(0f, 2f));

                if (ImGui.Button("恢复默认主题色"))
                {
                    config.ThemeBarOpacity = PluginConfiguration.DefaultThemeBarOpacity;
                    config.ResetThemeBarColors();
                    config.Save();
                }

                ImGui.SameLine();
                DrawHelpMarker("恢复默认时会同时重置主题色透明度和所有职业颜色。");

                if (!ImGui.CollapsingHeader("职业颜色列表"))
                {
                    ImGui.TextDisabled("默认先收起职业颜色列表；需要微调单职业 RGB 时再展开。");
                    return;
                }

                var themePaletteLineHeight = ImGui.GetTextLineHeightWithSpacing();
                var themePaletteMinHeight = themePaletteLineHeight * 5.8f;
                var themePaletteMaxHeight = themePaletteLineHeight * 10.0f;
                var themePaletteHeight = GetAdaptiveChildHeight("##theme_palette", themePaletteMinHeight, themePaletteMaxHeight);
                if (!ImGui.BeginChild("##theme_palette", new Vector2(0f, themePaletteHeight), true))
                    return;

                foreach (var group in JobThemePalette.GroupedEntries)
                {
                    if (!ImGui.CollapsingHeader(group.Key))
                        continue;

                    foreach (var entry in group)
                    {
                        var color = config.GetThemeBarColor(entry.JobName);
                        if (ImGui.ColorEdit4(
                                $"{entry.JobName}##{entry.JobName}",
                                ref color,
                                ImGuiColorEditFlags.NoAlpha))
                        {
                            config.SetThemeBarColor(entry.JobName, color);
                            config.Save();
                        }
                    }
                }

                RememberAdaptiveChildHeight(
                    "##theme_palette",
                    ImGui.GetCursorPosY() + ImGui.GetStyle().WindowPadding.Y,
                    themePaletteMinHeight,
                    themePaletteMaxHeight);
                ImGui.EndChild();
            });
    }

    private void DrawPartyMonitorSection()
    {
        if (!DrawFirstLevelHeader("队友监控"))
            return;

        var pm = config.PartyMonitor;

        if (!pm.EnablePartyMonitor)
        {
            ImGui.TextDisabled("请在「窗口设置」中启用队友监控。");
            return;
        }

        DrawSettingCard(
            "##party_monitor_modules_card",
            "监控模块",
            "选择你要监控的模块：食物、减伤技能、团辅技能。",
            5.2f,
            () =>
            {
                var monitorFood = pm.MonitorFood;
                if (ImGui.Checkbox("监控食物", ref monitorFood))
                {
                    pm.MonitorFood = monitorFood;
                    config.Save();
                }

                ImGui.SameLine();
                var monitorSkills = pm.MonitorSkills;
                if (ImGui.Checkbox("监控技能", ref monitorSkills))
                {
                    pm.MonitorSkills = monitorSkills;
                    config.Save();
                }

                ImGui.SameLine();
                var anonymousMode = pm.AnonymousMode;
                if (ImGui.Checkbox("匿名模式", ref anonymousMode))
                {
                    pm.AnonymousMode = anonymousMode;
                    config.Save();
                }
            });

        ImGui.Dummy(new Vector2(0f, 2f));

        if (!pm.EnablePartyMonitor)
            return;

        DrawPartyMonitorStyleSettings(pm);
        ImGui.Dummy(new Vector2(0f, 2f));

        if (pm.MonitorSkills)
            DrawPartyMonitorJobSkillSettings(pm);

        ImGui.Dummy(new Vector2(0f, 2f));
        DrawCustomSkillSection(pm);

        DrawCompactHelp("技能 ID 需要与实际游戏数据核对", "部分技能 ID 可能与当前版本不一致，可在运行时通过插件日志确认。");
    }

    private void DrawPartyMonitorStyleSettings(PartyMonitorConfig pm)
    {
        DrawSettingCard(
            "##party_monitor_style_card",
            "样式",
            "调整队友监控悬浮窗的图标、背景和起效高亮显示。",
            8.2f,
            () =>
            {
                var iconSize = pm.IconSize;
                if (ImGui.SliderFloat("图标大小", ref iconSize, 20f, 48f, "%.0f"))
                {
                    pm.IconSize = iconSize;
                    config.Save();
                }

                var countdownScale = pm.CountdownTextScale;
                if (ImGui.SliderFloat("CD倒计时数字大小", ref countdownScale, 0.6f, 2f, "%.2f"))
                {
                    pm.CountdownTextScale = countdownScale;
                    config.Save();
                }

                var enhancedActive = pm.EnhancedActiveStyle;
                if (ImGui.Checkbox("启用起效增强样式", ref enhancedActive))
                {
                    pm.EnhancedActiveStyle = enhancedActive;
                    config.Save();
                }

                ImGui.SameLine();
                var hideSkillsOnCooldown = pm.HideSkillsOnCooldown;
                if (ImGui.Checkbox("CD中技能隐藏", ref hideSkillsOnCooldown))
                {
                    pm.HideSkillsOnCooldown = hideSkillsOnCooldown;
                    config.Save();
                }

                var mergeSkillGroups = pm.MergeSkillGroups;
                if (ImGui.Checkbox("团辅减伤合并", ref mergeSkillGroups))
                {
                    pm.MergeSkillGroups = mergeSkillGroups;
                    config.Save();
                }

                var glowStrength = pm.ActiveGlowStrength;
                if (ImGui.SliderFloat("起效增强强度", ref glowStrength, 0f, 2f, "%.2f"))
                {
                    pm.ActiveGlowStrength = glowStrength;
                    config.Save();
                }

                var bg = pm.BackgroundColor;
                if (ImGui.ColorEdit4("背景默认颜色", ref bg))
                {
                    pm.BackgroundColor = bg;
                    config.Save();
                }
            });
    }

    private void DrawPartyMonitorJobSkillSettings(PartyMonitorConfig pm)
    {
        if (!ImGui.CollapsingHeader("按职业选择监控技能"))
            return;

        DrawSettingCard(
            "##party_monitor_job_skills_card",
            "职业技能列表",
            "展开各职业，勾选你想在监控窗口内显示的减伤和团辅技能。",
            16f,
            () =>
            {
                var monitorJobIds = new uint[]
                {
                    19, 21, 32, 37,
                    24, 28, 33, 40,
                    20, 22, 30, 34, 39, 41,
                    23, 31, 38,
                    25, 27, 35, 42,
                };

                for (var jobIndex = 0; jobIndex < monitorJobIds.Length; jobIndex++)
                {
                    var jobId = monitorJobIds[jobIndex];
                    var jobName = PartyMonitorWindow.GetJobName(jobId);
                    var jobConfig = pm.GetOrCreateJobConfig(jobId);
                    var skills = PartySkillCatalog.GetSkillsForJob(jobId, jobConfig);
                    if (skills.Count == 0)
                        continue;

                    if (!ImGui.CollapsingHeader($"{jobIndex + 1:00}/{monitorJobIds.Length:00} {jobName}###job_skills_{jobId}"))
                        continue;

                    for (var i = 0; i < skills.Count; i++)
                    {
                        var skill = skills[i];
                        var enabled = skill.Category == SkillCategory.Mitigation
                            ? jobConfig.EnabledMitigationActionIds.Contains(skill.ActionId)
                            : jobConfig.EnabledRaidBuffActionIds.Contains(skill.ActionId);

                        if (i % 3 != 0)
                            ImGui.SameLine();

                        var icon = KamiIconLoader.GetIcon(skill.ActionId);
                        if (icon != default)
                        {
                            ImGui.Image(icon, new Vector2(22f, 22f));
                            ImGui.SameLine();
                        }

                        if (ImGui.Checkbox($"{skill.Name}", ref enabled))
                        {
                            if (skill.Category == SkillCategory.Mitigation)
                            {
                                if (enabled)
                                    jobConfig.EnabledMitigationActionIds.Add(skill.ActionId);
                                else
                                    jobConfig.EnabledMitigationActionIds.Remove(skill.ActionId);
                            }
                            else
                            {
                                if (enabled)
                                    jobConfig.EnabledRaidBuffActionIds.Add(skill.ActionId);
                                else
                                    jobConfig.EnabledRaidBuffActionIds.Remove(skill.ActionId);
                            }
                            config.Save();
                        }
                    }
                }
            });
    }

    private void DrawCustomSkillSection(PartyMonitorConfig pm)
    {
        const uint globalCustomKey = 0;

        var actionIdStr = customSkillActionIdInputs.TryGetValue(globalCustomKey, out var aStr) ? aStr : string.Empty;
        var skillName = customSkillNameInputs.TryGetValue(globalCustomKey, out var nStr) ? nStr : string.Empty;
        var cdStr = customSkillCdInputs.TryGetValue(globalCustomKey, out var cStr) ? cStr : string.Empty;

        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.6f, 0.8f, 1f, 1f), "添加自定义技能（当目录中缺少某个技能时使用）");

        ImGui.TextDisabled("目标职业");
        ImGui.SameLine();
        var selectedJobLabel = customSkillSelectedJobName ?? "请选择";
        ImGui.SetNextItemWidth(120f);
        if (ImGui.BeginCombo($"##custom_skill_job_selector", selectedJobLabel))
        {
            foreach (var jobId in new uint[]
            {
                19, 21, 32, 37,
                24, 28, 33, 40,
                20, 22, 30, 34, 39, 41,
                23, 31, 38,
                25, 27, 35, 42,
            })
            {
                var jobLabel = PartyMonitorWindow.GetJobName(jobId);
                var isSelected = customSkillSelectedJobId == jobId;
                if (ImGui.Selectable(jobLabel, isSelected))
                {
                    customSkillSelectedJobId = jobId;
                    customSkillSelectedJobName = jobLabel;
                }
                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }

        ImGui.SameLine(0f, 6f);
        ImGui.TextDisabled("技能ID");
        ImGui.SameLine(0f, 6f);
        DrawHelpMarker("游戏内 Action 编号，可在 https://ff14.huijiwiki.com 对应职业页中找到。");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(80f);
        if (ImGui.InputText($"##custom_action_id", ref actionIdStr, 16))
            customSkillActionIdInputs[globalCustomKey] = actionIdStr;

        ImGui.SameLine(0f, 8f);
        ImGui.TextDisabled("技能名称");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(100f);
        if (ImGui.InputText($"##custom_action_name", ref skillName, 32))
            customSkillNameInputs[globalCustomKey] = skillName;

        ImGui.SameLine(0f, 8f);
        ImGui.TextDisabled("冷却(秒)");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(50f);
        if (ImGui.InputText($"##custom_action_cd", ref cdStr, 8))
            customSkillCdInputs[globalCustomKey] = cdStr;

        ImGui.SameLine(0f, 12f);
        var isMit = customSkillIsMit.TryGetValue(globalCustomKey, out var mit) ? mit : true;
        if (ImGui.RadioButton($"减伤##cat_mit", isMit))
        {
            isMit = true;
            customSkillIsMit[globalCustomKey] = isMit;
        }
        ImGui.SameLine();
        if (ImGui.RadioButton($"团辅##cat_buff", !isMit))
        {
            isMit = false;
            customSkillIsMit[globalCustomKey] = isMit;
        }

        ImGui.SameLine();
        if (ImGui.SmallButton($"添加##custom_add"))
        {
            if (customSkillSelectedJobId != 0
                && uint.TryParse(actionIdStr, out var actId)
                && !string.IsNullOrWhiteSpace(skillName)
                && float.TryParse(cdStr, out var cd)
                && cd > 0)
            {
                var jobConfig = pm.GetOrCreateJobConfig(customSkillSelectedJobId);
                var category = isMit ? SkillCategory.Mitigation : SkillCategory.RaidBuff;
                if (!jobConfig.CustomSkills.ContainsKey(actId))
                {
                    jobConfig.CustomSkills[actId] = new CustomSkillEntry(skillName, category, cd);
                    if (category == SkillCategory.Mitigation)
                        jobConfig.EnabledMitigationActionIds.Add(actId);
                    else
                        jobConfig.EnabledRaidBuffActionIds.Add(actId);
                    monitorService?.InvalidateSkillsCache();
                    config.Save();
                }
                customSkillActionIdInputs[globalCustomKey] = string.Empty;
                customSkillNameInputs[globalCustomKey] = string.Empty;
                customSkillCdInputs[globalCustomKey] = string.Empty;
            }
        }

        var allCustomSkills = new List<(uint JobId, uint ActionId, CustomSkillEntry Entry)>();
        foreach (var (jobId, jobConfig) in pm.JobConfigs)
        {
            foreach (var (actId, entry) in jobConfig.CustomSkills)
                allCustomSkills.Add((jobId, actId, entry));
        }

        if (allCustomSkills.Count > 0)
        {
            ImGui.Dummy(new Vector2(0f, 2f));
            ImGui.TextDisabled("已添加的自定义技能");
            ImGui.Indent(8f);
            foreach (var (jobId, actId, entry) in allCustomSkills)
            {
                var jobLabel = PartyMonitorWindow.GetJobName(jobId);
                var label = $"[{jobLabel}] [{actId}] {entry.Name} ({entry.CooldownSeconds}s)";
                if (ImGui.SmallButton($"删除##custom_del_{jobId}_{actId}"))
                {
                    var jc = pm.GetOrCreateJobConfig(jobId);
                    jc.CustomSkills.Remove(actId);
                    jc.EnabledMitigationActionIds.Remove(actId);
                    jc.EnabledRaidBuffActionIds.Remove(actId);
                    config.Save();
                }
                ImGui.SameLine();
                ImGui.TextDisabled(label);
            }
            ImGui.Unindent(8f);
        }
    }

    private void DrawMaintenanceSection()
    {
        if (!DrawFirstLevelHeader("数据与状态", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        DrawSettingCard(
            "##maintenance_actions_card",
            "数据操作",
            "用于导入测试数据、历史记录导入导出、清空历史以及恢复插件默认设置。",
            6.4f,
            DrawMaintenanceActionGrid);

        ImGui.Dummy(new Vector2(0f, 2f));
        DrawSettingCard(
            "##friendly_npc_name_list_card",
            "NPC 队友识别名单",
            "只显示当前可识别到的队伍成员，用于核对玩家与 NPC 队友是否已纳入统计。",
            10.0f,
            DrawFriendlyNpcNameListSection);

        ImGui.Dummy(new Vector2(0f, 2f));
        DrawSettingCard(
            "##maintenance_logging_card",
            "日志与调试",
            "控制是否输出调试（Debug）/详细（Verbose）级别日志，便于排查问题；普通信息 / 警告 / 错误日志不受影响。",
            9.8f,
            DrawLoggingSection);

        ImGui.Dummy(new Vector2(0f, 2f));
        DrawSettingCard(
            "##maintenance_debug_combat_record_card",
            "debug战斗记录",
            "控制 Boss/小怪平A、BUFF/debuff、技能、读条，以及友方标记、技能、BUFF 和 debuff 记录项；详细内容在独立 debug 战斗记录悬浮窗中查看和复制。",
            13f,
            DrawDebugCombatRecordSection);

        ImGui.Dummy(new Vector2(0f, 2f));
        DrawSettingCard(
            "##maintenance_status_card",
            "历史预览与状态",
            "控制历史记录预览时长，并查看当前历史文件路径、数据源和插件状态信息。",
            6.6f,
            () =>
            {
                var historyPreviewSeconds = config.HistoryPreviewSeconds;
                if (ImGui.SliderInt("历史记录预览时长（秒）", ref historyPreviewSeconds, 1, 30))
                {
                    config.HistoryPreviewSeconds = historyPreviewSeconds;
                    config.Save();
                }

                DrawCompactHelp("预览规则说明", "未进入战斗时，点击历史记录会无限预览该快照；进入战斗后，才按这里设置的秒数开始倒计时并自动回到当前统计。");
                if (!string.IsNullOrWhiteSpace(statsService.HistoryTransferStatusText))
                    ImGui.TextDisabled(statsService.HistoryTransferStatusText);

                if (ImGui.CollapsingHeader("路径与状态详情"))
                {
                    ImGui.TextDisabled($"历史文件: {statsService.HistoryTransferFilePath}");
                    ImGui.Dummy(new Vector2(0f, 2f));
                    ImGui.TextDisabled(statsService.DataSourceText);
                    ImGui.TextDisabled(statsService.StatusText);
                }
                else
                {
                    ImGui.TextDisabled("默认先收起路径与状态详情；需要排查时再展开查看。");
                }
            });
    }

    private void DrawLoggingSection()
    {
        var enableDebugLog = config.EnableDebugLog;
        if (ImGui.Checkbox("启用调试日志", ref enableDebugLog))
        {
            config.EnableDebugLog = enableDebugLog;
            LogHelper.EnableDebugLog = enableDebugLog;
            config.Save();
            LogHelper.Info("设置", enableDebugLog ? "已从设置中启用调试日志。" : "已从设置中关闭调试日志。");
        }

        DrawCompactHelp("日志写入规则", "开启后，会把调试（Debug）与详细（Verbose）日志写入 Dalamud 插件日志。");
        ImGui.TextDisabled($"当前状态：{(config.EnableDebugLog ? "已开启" : "已关闭")}");

        if (!ImGui.CollapsingHeader("最近日志摘要"))
        {
            ImGui.TextDisabled(LogUiHelper.HasRecentLogs
                ? "默认先收起最近日志摘要；需要查看最近输出时再展开。"
                : "当前没有最近日志摘要。");
            return;
        }

        LogUiHelper.DrawRecentLogToolbar();
        LogUiHelper.DrawRecentLogList(10);
    }

    private void DrawDebugCombatRecordSection()
    {
        var recordingEnabled = config.DebugCombatRecordingEnabled;
        if (ImGui.Checkbox("开始记录debug战斗记录", ref recordingEnabled))
            statsService.SetDebugCombatRecordingEnabled(recordingEnabled);

        ImGui.SameLine();
        if (ImGui.Button("打开窗口"))
            openDebugCombatLogWindow();

        ImGui.SameLine();
        if (ImGui.Button("清空debug记录"))
            statsService.ClearDebugCombatLog();

        DrawCompactHelp("记录只在开始记录后写入。", "关闭记录不会清空已记录内容；可在 debug 战斗记录窗口中复制当前筛选结果。插件每次加载时默认关闭。");

        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled($"记录项：{BuildDebugRecordToggleSummary()}");
        ImGui.SameLine();
        DrawDebugRecordPresetButtons("settings");

        ImGui.Dummy(new Vector2(0f, 2f));
        if (ImGui.CollapsingHeader("展开记录项开关###settings_debug_combat_record_options"))
        {
            DrawDebugCombatRecordToggleGrid("settings");
        }
        else
        {
            ImGui.TextDisabled("详细开关已收起；常用操作可直接用“全开 / 全关 / 默认”。");
        }

        var maxEntries = config.DebugCombatLogMaxEntries;
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.SliderInt("debug记录保留条数（0=全部）", ref maxEntries, 0, 50000))
        {
            config.DebugCombatLogMaxEntries = maxEntries <= 0 ? 0 : Math.Clamp(maxEntries, 100, 50000);
            statsService.ApplyDebugCombatLogRetentionLimit();
            config.Save();
        }
    }

    private void DrawDebugCombatRecordToggleGrid(string idPrefix)
    {
        const ImGuiTableFlags flags =
            ImGuiTableFlags.SizingStretchSame
            | ImGuiTableFlags.NoSavedSettings;

        if (!ImGui.BeginTable($"##{idPrefix}_debug_combat_record_toggle_grid", 2, flags))
            return;

        ImGui.TableNextRow();

        ImGui.TableSetColumnIndex(0);
        DrawDebugCombatRecordToggleGroup("Boss / 小怪", () =>
        {
            DrawDebugCombatRecordCheckbox($"平A##{idPrefix}_boss_auto", ref config.DebugRecordBossAutoAttack);
            DrawDebugCombatRecordCheckbox($"BUFF/debuff##{idPrefix}_boss_buff", ref config.DebugRecordBossBuff);
            DrawDebugCombatRecordCheckbox($"技能##{idPrefix}_boss_action", ref config.DebugRecordBossAction);
            DrawDebugCombatRecordCheckbox($"读条##{idPrefix}_boss_cast", ref config.DebugRecordBossCast);
            DrawDebugCombatRecordCheckbox($"小怪按 Boss##{idPrefix}_small_as_boss", ref config.DebugRecordSmallHostileNpcAsBoss);
        });

        ImGui.TableSetColumnIndex(1);
        DrawDebugCombatRecordToggleGroup("友方", () =>
        {
            DrawDebugCombatRecordFriendlyCheckbox($"标记##{idPrefix}_friendly_marker", ref config.DebugRecordPartyMarker, ref config.DebugRecordSelfMarker);
            DrawDebugCombatRecordFriendlyCheckbox($"技能##{idPrefix}_friendly_action", ref config.DebugRecordPartyAction, ref config.DebugRecordSelfAction);
            DrawDebugCombatRecordFriendlyCheckbox($"BUFF##{idPrefix}_friendly_buff", ref config.DebugRecordPartyBuff, ref config.DebugRecordSelfBuff);
            DrawDebugCombatRecordFriendlyCheckbox($"debuff##{idPrefix}_friendly_debuff", ref config.DebugRecordPartyDebuff, ref config.DebugRecordSelfDebuff);
        });

        ImGui.EndTable();
    }

    private static void DrawDebugCombatRecordToggleGroup(string title, Action drawContent)
    {
        ImGui.TextDisabled(title);
        drawContent();
    }

    private void DrawDebugCombatRecordCheckbox(string label, ref bool value)
    {
        var current = value;
        if (ImGui.Checkbox(label, ref current))
        {
            value = current;
            config.Save();
        }
    }

    private void DrawDebugCombatRecordFriendlyCheckbox(string label, ref bool partyValue, ref bool selfValue)
    {
        var current = partyValue || selfValue;
        if (!ImGui.Checkbox(label, ref current))
            return;

        partyValue = current;
        selfValue = current;
        config.Save();
    }

    private void DrawDebugRecordPresetButtons(string idPrefix)
    {
        if (ImGui.SmallButton($"全开##{idPrefix}_debug_record_all_on"))
            SetAllDebugRecordToggles(true);

        ImGui.SameLine();
        if (ImGui.SmallButton($"全关##{idPrefix}_debug_record_all_off"))
            SetAllDebugRecordToggles(false);

        ImGui.SameLine();
        if (ImGui.SmallButton($"默认##{idPrefix}_debug_record_default"))
            ResetDebugRecordTogglesToDefault();

        ImGui.SameLine();
        DrawHelpMarker("默认：除“小怪按 Boss”外，其他 debug 记录项全部开启。详细开关默认收起，避免设置卡片被拉得太长。");
    }

    private string BuildDebugRecordToggleSummary()
    {
        var enabledCount = CountEnabledDebugRecordToggles();
        return enabledCount switch
        {
            0 => "全部关闭",
            DebugRecordToggleCount => "全部开启",
            _ => $"已开 {enabledCount}/{DebugRecordToggleCount}",
        };
    }

    private int CountEnabledDebugRecordToggles()
    {
        var count = 0;
        if (config.DebugRecordBossAutoAttack) count++;
        if (config.DebugRecordBossBuff) count++;
        if (config.DebugRecordBossAction) count++;
        if (config.DebugRecordBossCast) count++;
        if (config.DebugRecordSmallHostileNpcAsBoss) count++;
        if (config.DebugRecordPartyMarker || config.DebugRecordSelfMarker) count++;
        if (config.DebugRecordPartyAction || config.DebugRecordSelfAction) count++;
        if (config.DebugRecordPartyBuff || config.DebugRecordSelfBuff) count++;
        if (config.DebugRecordPartyDebuff || config.DebugRecordSelfDebuff) count++;
        return count;
    }

    private void SetAllDebugRecordToggles(bool enabled)
    {
        config.DebugRecordBossAutoAttack = enabled;
        config.DebugRecordBossBuff = enabled;
        config.DebugRecordBossAction = enabled;
        config.DebugRecordBossCast = enabled;
        config.DebugRecordSmallHostileNpcAsBoss = enabled;
        config.DebugRecordPartyMarker = enabled;
        config.DebugRecordPartyAction = enabled;
        config.DebugRecordPartyBuff = enabled;
        config.DebugRecordPartyDebuff = enabled;
        config.DebugRecordSelfMarker = enabled;
        config.DebugRecordSelfAction = enabled;
        config.DebugRecordSelfBuff = enabled;
        config.DebugRecordSelfDebuff = enabled;
        config.Save();
    }

    private void ResetDebugRecordTogglesToDefault()
    {
        config.DebugRecordBossAutoAttack = true;
        config.DebugRecordBossBuff = true;
        config.DebugRecordBossAction = true;
        config.DebugRecordBossCast = true;
        config.DebugRecordSmallHostileNpcAsBoss = false;
        config.DebugRecordPartyMarker = true;
        config.DebugRecordPartyAction = true;
        config.DebugRecordPartyBuff = true;
        config.DebugRecordPartyDebuff = true;
        config.DebugRecordSelfMarker = true;
        config.DebugRecordSelfAction = true;
        config.DebugRecordSelfBuff = true;
        config.DebugRecordSelfDebuff = true;
        config.Save();
    }

    private string GetFloatingStatsButtonLabel()
        => config.ShowStatsPanel ? "隐藏悬浮DPS统计面板" : "打开悬浮DPS统计面板";

    private void DrawSettingCard(string id, string title, string description, float heightInLines, Action drawContent)
    {
        var style = ImGui.GetStyle();
        var lineHeight = ImGui.GetTextLineHeightWithSpacing();
        const float cardVerticalPadding = 6f;
        const float cardContentGap = 2f;
        const float cardMinLines = 2.2f;
        var minHeight = (lineHeight * cardMinLines) + cardVerticalPadding * 2f;
        var fallbackHeight = (lineHeight * Math.Max(heightInLines, cardMinLines)) + cardVerticalPadding * 2f;
        var height = GetAdaptiveCardHeight(id, fallbackHeight, minHeight);

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(style.WindowPadding.X, cardVerticalPadding));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(style.ItemSpacing.X, 4f));
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1f);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(1f, 1f, 1f, 0.035f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 1f, 0.12f));

        ImGui.BeginChild(id, new Vector2(0f, height), true);
        ImGui.TextUnformatted(title);
        if (!string.IsNullOrWhiteSpace(description))
        {
            ImGui.SameLine(0f, 6f);
            DrawHelpMarker(description);
        }
        ImGui.Dummy(new Vector2(0f, cardContentGap));
        drawContent();
        RememberAdaptiveCardHeight(
            id,
            ImGui.GetCursorPosY() + cardVerticalPadding,
            minHeight);
        ImGui.EndChild();

        ImGui.PopStyleColor(2);
        ImGui.PopStyleVar(4);
    }

    private float GetAdaptiveCardHeight(string key, float fallbackHeight, float minHeight)
    {
        if (adaptiveChildHeights.TryGetValue(key, out var cachedHeight))
            return Math.Max(minHeight, cachedHeight);

        return Math.Max(minHeight, fallbackHeight);
    }

    private void RememberAdaptiveCardHeight(string key, float contentHeight, float minHeight)
    {
        var resolvedHeight = Math.Max(minHeight, contentHeight);
        if (adaptiveChildHeights.TryGetValue(key, out var currentHeight)
            && Math.Abs(currentHeight - resolvedHeight) < 0.5f)
            return;

        adaptiveChildHeights[key] = resolvedHeight;
    }

    private void DrawHelpMarker(string tooltip)
    {
        ImGui.TextDisabled("(?)");
        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(Math.Min(ImGui.GetFontSize() * 26f, 560f));
            ImGui.TextUnformatted(tooltip);
            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
        }
    }

    private void DrawCompactHelp(string summary, string tooltip)
    {
        ImGui.TextDisabled(summary);
        ImGui.SameLine(0f, 2f);
        DrawHelpMarker(tooltip);
    }

    private bool DrawLabeledSliderFloat(
        string label,
        string id,
        ref float value,
        float minValue,
        float maxValue,
        string format)
    {
        ImGui.TextDisabled(label);
        ImGui.SetCursorPosY(Math.Max(0f, ImGui.GetCursorPosY() - 1f));
        ImGui.SetNextItemWidth(-1f);
        return ImGui.SliderFloat(id, ref value, minValue, maxValue, format);
    }

    private bool DrawLabeledSliderInt(
        string label,
        string id,
        ref int value,
        int minValue,
        int maxValue,
        string format)
    {
        ImGui.TextDisabled(label);
        ImGui.SetCursorPosY(Math.Max(0f, ImGui.GetCursorPosY() - 1f));
        ImGui.SetNextItemWidth(-1f);
        return ImGui.SliderInt(id, ref value, minValue, maxValue, format);
    }

    private bool BeginLabeledCombo(string label, string id, string previewValue)
    {
        ImGui.TextDisabled(label);
        ImGui.SetCursorPosY(Math.Max(0f, ImGui.GetCursorPosY() - 1f));
        ImGui.SetNextItemWidth(-1f);
        return ImGui.BeginCombo(id, previewValue);
    }

    private bool DrawLabeledCheckbox(
        string label,
        string id,
        ref bool value,
        string enabledText = "已开启",
        string disabledText = "已关闭")
    {
        ImGui.TextDisabled(label);
        ImGui.SetCursorPosY(Math.Max(0f, ImGui.GetCursorPosY() - 1f));
        var changed = ImGui.Checkbox(id, ref value);
        ImGui.SameLine(0f, 6f);
        ImGui.TextDisabled(value ? enabledText : disabledText);
        return changed;
    }

    private float GetAdaptiveChildHeight(string key, float minHeight, float maxHeight)
    {
        if (adaptiveChildHeights.TryGetValue(key, out var cachedHeight))
            return Math.Clamp(cachedHeight, minHeight, maxHeight);

        return maxHeight;
    }

    private void RememberAdaptiveChildHeight(string key, float contentHeight, float minHeight, float maxHeight)
    {
        var clampedHeight = Math.Clamp(contentHeight, minHeight, maxHeight);
        if (adaptiveChildHeights.TryGetValue(key, out var currentHeight)
            && Math.Abs(currentHeight - clampedHeight) < 0.5f)
            return;

        adaptiveChildHeights[key] = clampedHeight;
    }

    private void DrawSharedColumnToggleGrid()
    {
        const ImGuiTableFlags flags =
            ImGuiTableFlags.SizingStretchSame
            | ImGuiTableFlags.NoSavedSettings;

        if (!ImGui.BeginTable("##shared_column_toggle_grid", 2, flags))
            return;

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        DrawSharedColumnCheckbox("显示玩家列", ref config.ShowDpsPlayerColumn, true);
        ImGui.TableSetColumnIndex(1);
        DrawSharedColumnCheckbox("显示职业列", ref config.ShowDpsJobColumn, true);

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        DrawSharedColumnCheckbox("显示伤害列", ref config.ShowDpsDamageColumn, true);
        ImGui.TableSetColumnIndex(1);
        DrawSharedColumnCheckbox("显示秒伤列", ref config.ShowDpsValueColumn, true);

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        DrawSharedColumnCheckbox("显示死亡列", ref config.ShowDpsDeathsColumn, false);
        ImGui.TableSetColumnIndex(1);
        ImGui.TextDisabled("同时作用于三页");

        ImGui.EndTable();
    }

    private void DrawVisibleTabToggleGrid()
    {
        const ImGuiTableFlags flags =
            ImGuiTableFlags.SizingStretchSame
            | ImGuiTableFlags.NoSavedSettings;

        if (!ImGui.BeginTable("##visible_tab_toggle_grid", 2, flags))
            return;

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        DrawToggle("显示 DPS", ref config.ShowDpsTab);
        ImGui.TableSetColumnIndex(1);
        DrawToggle("显示 HPS", ref config.ShowHpsTab);

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        DrawToggle("显示 承伤", ref config.ShowTakenTab);
        ImGui.TableSetColumnIndex(1);
        DrawToggle("显示 概览", ref config.ShowOverviewTab);

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        DrawToggle("显示 历史记录", ref config.ShowHistoryTab);
        ImGui.TableSetColumnIndex(1);
        ImGui.TextDisabled("至少保留一个页签");

        ImGui.EndTable();
    }

    private void DrawMaintenanceActionGrid()
    {
        const ImGuiTableFlags flags =
            ImGuiTableFlags.SizingStretchSame
            | ImGuiTableFlags.NoSavedSettings;

        if (!ImGui.BeginTable("##maintenance_action_grid", 2, flags))
            return;

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        if (ImGui.Button("导入测试数据", new Vector2(-1f, 0f)))
            statsService.LoadTestData();
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Button("导出历史记录", new Vector2(-1f, 0f)))
            statsService.ExportHistoricalRecords();

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        if (ImGui.Button("导入历史记录", new Vector2(-1f, 0f)))
            statsService.ImportHistoricalRecords();
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Button("清空历史", new Vector2(-1f, 0f)))
            statsService.ClearHistory();

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        if (ImGui.Button("恢复默认", new Vector2(-1f, 0f)))
        {
            config.Reset();
            StatsPanel.RequestMetricColumnWidthReset();
            StatsPanel.RequestHistoryColumnWidthReset();
            config.Save();
            LogHelper.PrintWithModule("设置", "恢复默认", "已恢复插件默认配置，并重置统计页与历史页列宽记忆。");
        }
        ImGui.TableSetColumnIndex(1);
        if (ImGui.Button("打印当前BUFF", new Vector2(-1f, 0f)))
            DumpLocalPlayerBuffs();

        ImGui.EndTable();
    }

    private static void DumpLocalPlayerBuffs()
    {
        var localPlayer = DalamudApi.GetLocalPlayerBattleChara();
        if (localPlayer == null)
        {
            LogHelper.PrintErrorWithModule("调试", "BUFF", "未读取到本地玩家对象，无法打印当前BUFF。请确认已登录且角色已加载。");
            return;
        }

        var rawStatusList = localPlayer.GetType().GetProperty("StatusList")?.GetValue(localPlayer)
                            ?? localPlayer.GetType().GetProperty("Statuses")?.GetValue(localPlayer);
        if (rawStatusList == null)
        {
            LogHelper.PrintErrorWithModule("调试", "BUFF", "未读取到本地玩家 StatusList/Statuses。请检查当前 Dalamud API 版本。 ");
            return;
        }

        var name = localPlayer.Name.TextValue?.Trim();
        var actorId = unchecked((uint)(localPlayer.GameObjectId & uint.MaxValue));
        if (actorId == 0)
            actorId = localPlayer.EntityId;

        LogHelper.PrintWithModule("调试", "BUFF", $"开始打印当前BUFF：name={name}，actorId=0x{actorId:X8}，job={localPlayer.ClassJob.RowId}。");

        var printed = 0;
        var length = ReadIntProperty(rawStatusList, "Length");
        if (length <= 0)
            length = ReadIntProperty(rawStatusList, "Count");

        for (var i = 0; i < length; i++)
        {
            var status = ReadIndexedValue(rawStatusList, i);
            if (status == null)
                continue;

            var statusId = ReadUIntProperty(status, "StatusId");
            if (statusId == 0)
                statusId = ReadUIntProperty(status, "Id");
            if (statusId == 0)
                continue;

            printed++;
            var statusName = "未知";
            var category = 0u;
            try
            {
                var gameDataRef = status.GetType().GetProperty("GameData")?.GetValue(status);
                var gameData = gameDataRef?.GetType().GetProperty("Value")?.GetValue(gameDataRef);
                statusName = gameData?.GetType().GetProperty("Name")?.GetValue(gameData)?.ToString() ?? statusName;
                category = ReadUIntProperty(gameData!, "StatusCategory");
            }
            catch
            {
            }

            LogHelper.PrintWithModule(
                "调试",
                "BUFF",
                $"#{i:00} id={statusId} name={statusName} category={category} remaining={ReadFloatProperty(status, "RemainingTime"):0.0}s param={ReadProperty(status, "Param")} stacks={ReadProperty(status, "StackCount")} source=0x{ReadUIntProperty(status, "SourceId"):X8} actor=0x{ReadUIntProperty(status, "ActorId"):X8}.");
        }

        LogHelper.PrintWithModule("调试", "BUFF", $"当前BUFF打印完成，共 {printed} 个非空状态。食物通常应关注 id/category/remaining 字段。");
    }

    private static string ReadProperty(object instance, string propertyName)
    {
        try
        {
            return instance.GetType().GetProperty(propertyName)?.GetValue(instance)?.ToString() ?? "-";
        }
        catch
        {
            return "-";
        }
    }

    private static object? ReadIndexedValue(object instance, int index)
    {
        try
        {
            return instance.GetType().GetProperty("Item")?.GetValue(instance, [index]);
        }
        catch
        {
            return null;
        }
    }

    private static int ReadIntProperty(object instance, string propertyName)
    {
        try
        {
            var value = instance.GetType().GetProperty(propertyName)?.GetValue(instance);
            return value == null ? 0 : Convert.ToInt32(value);
        }
        catch
        {
            return 0;
        }
    }

    private static float ReadFloatProperty(object instance, string propertyName)
    {
        try
        {
            var value = instance.GetType().GetProperty(propertyName)?.GetValue(instance);
            return value == null ? 0f : Convert.ToSingle(value);
        }
        catch
        {
            return 0f;
        }
    }

    private static uint ReadUIntProperty(object instance, string propertyName)
    {
        try
        {
            var value = instance.GetType().GetProperty(propertyName)?.GetValue(instance);
            return value == null ? 0 : Convert.ToUInt32(value);
        }
        catch
        {
            return 0;
        }
    }

    private void DrawColumnWidthResetButtons()
    {
        if (ImGui.Button("重置统计页列宽记忆"))
        {
            config.ResetSharedMetricColumnWidths();
            StatsPanel.RequestMetricColumnWidthReset();
            config.Save();
            LogHelper.PrintWithModule("设置", "列宽记忆", "已重置统计页列宽记忆。");
        }

        ImGui.SameLine();
        if (ImGui.Button("重置历史页列宽记忆"))
        {
            config.ResetHistoryColumnWidths();
            StatsPanel.RequestHistoryColumnWidthReset();
            config.Save();
            LogHelper.PrintWithModule("设置", "列宽记忆", "已重置历史页列宽记忆。");
        }
    }

    private void DrawColumnSemanticTable()
    {
        const ImGuiTableFlags flags =
            ImGuiTableFlags.RowBg
            | ImGuiTableFlags.BordersInnerH
            | ImGuiTableFlags.SizingFixedFit
            | ImGuiTableFlags.NoSavedSettings;

        if (!ImGui.BeginTable("##column_semantics", 4, flags))
            return;

        ImGui.TableSetupColumn("设置项");
        ImGui.TableSetupColumn("DPS");
        ImGui.TableSetupColumn("HPS");
        ImGui.TableSetupColumn("承伤");
        ImGui.TableHeadersRow();

        DrawSemanticRow("玩家列", "玩家", "玩家", "玩家");
        DrawSemanticRow("职业列", "职业", "职业", "职业");
        DrawSemanticRow("伤害列", "伤害量", "治疗量", "承伤量");
        DrawSemanticRow("秒伤列", "秒伤", "秒疗", "秒承伤");
        DrawSemanticRow("死亡列", "死亡", "死亡", "死亡");
        DrawSemanticRow("显示人数", "限制显示条目数", "限制显示条目数", "限制显示条目数");

        ImGui.EndTable();
    }

    private void DrawFloatingDisplayStyleSection()
    {
        var currentStyle = config.FloatingStatsDisplayStyle;
        var currentLabel = GetFloatingDisplayStyleLabel(currentStyle);
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo("展示模式", currentLabel))
        {
            foreach (FloatingStatsDisplayStyle style in Enum.GetValues(typeof(FloatingStatsDisplayStyle)))
            {
                var isSelected = currentStyle == style;
                if (ImGui.Selectable(GetFloatingDisplayStyleLabel(style), isSelected))
                {
                    config.SwitchFloatingStatsDisplayStyle(style);
                    currentStyle = config.FloatingStatsDisplayStyle;
                }

                if (isSelected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        DrawCompactHelp("当前样式说明", GetFloatingDisplayStyleDescription(currentStyle));
        DrawFloatingStyleFileManagementSection();

        if (currentStyle == FloatingStatsDisplayStyle.Ikegami)
        {
            DrawIkegamiFloatingDisplayStyleSection();
            return;
        }

        if (currentStyle == FloatingStatsDisplayStyle.Minimal)
        {
            DrawMinimalFloatingDisplayStyleSection();
            return;
        }

        if (!PluginConfiguration.UsesLegacyFloatingTableLayout(currentStyle))
        {
            ImGui.Dummy(new Vector2(0f, 2f));
            DrawCompactHelp("当前样式不再使用旧表格参数。", "如果后续为该样式补专属参数，也会放在这里。");
            return;
        }

        ImGui.Dummy(new Vector2(0f, 2f));
        ImGui.Separator();
        ImGui.TextDisabled("经典表格样式参数");

        var playerColumnMinWidth = config.FloatingStatsPlayerColumnMinWidth;
        if (ImGui.SliderFloat("玩家列最小宽度", ref playerColumnMinWidth, 0f, 360f, "%.0f"))
        {
            config.FloatingStatsPlayerColumnMinWidth = playerColumnMinWidth;
            config.Save();
        }

        var metricColumnWidth = config.FloatingStatsMetricColumnWidth;
        if (ImGui.SliderFloat("固定列宽", ref metricColumnWidth, 48f, 220f, "%.0f"))
        {
            config.FloatingStatsMetricColumnWidth = metricColumnWidth;
            config.Save();
        }

        var rowHeight = config.FloatingStatsRowHeight;
        if (ImGui.SliderFloat("表格行高", ref rowHeight, 0f, 60f, "%.0f"))
        {
            config.FloatingStatsRowHeight = rowHeight;
            config.Save();
        }

        DrawCompactHelp("玩家列宽 / 行高设为 0 时自动取值。", "把玩家列最小宽度或表格行高拖到 0，会回退到自动布局。");
    }

    private void DrawMinimalFloatingDisplayStyleSection()
    {
        const ImGuiTableFlags compactTableFlags =
            ImGuiTableFlags.SizingStretchSame
            | ImGuiTableFlags.NoSavedSettings;
        var style = ImGui.GetStyle();

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(style.ItemSpacing.X, 2f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(style.FramePadding.X, 2f));
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(style.CellPadding.X, 2f));

        ImGui.Dummy(new Vector2(0f, 2f));
        ImGui.Separator();
        ImGui.TextDisabled("极简样式参数");
        DrawCompactHelp("极简样式固定隐藏页签与秒伤列。", "现在可更细地控制占比条里显示职业、伤害、死亡、占比，以及总DPS条里的时间、标题、总DPS、总伤和总死亡。");

        var showHeader = config.FloatingStatsMinimalShowHeader;
        var showSummaryRow = config.FloatingStatsMinimalShowSummaryRow;
        var showPlayerColumn = config.FloatingStatsMinimalShowPlayerColumn;
        var showDamageColumn = config.FloatingStatsMinimalShowDamageColumn;
        var showDeathsColumn = config.FloatingStatsMinimalShowDeathsColumn;
        var showPlayerNameInShareBar = config.FloatingStatsMinimalShowPlayerNameInShareBar;
        var showJobInShareBar = config.FloatingStatsMinimalShowJobInShareBar;
        var showDamageInShareBar = config.FloatingStatsMinimalShowDamageInShareBar;
        var showDeathsInShareBar = config.FloatingStatsMinimalShowDeathsInShareBar;
        var showRatioInShareBar = config.FloatingStatsMinimalShowRatioInShareBar;
        var showDurationInSummaryBar = config.FloatingStatsMinimalShowDurationInSummaryBar;
        var showTitleInSummaryBar = config.FloatingStatsMinimalShowTitleInSummaryBar;
        var showDpsInSummaryBar = config.FloatingStatsMinimalShowDpsInSummaryBar;
        var showDamageInSummaryBar = config.FloatingStatsMinimalShowDamageInSummaryBar;
        var showDeathsInSummaryBar = config.FloatingStatsMinimalShowDeathsInSummaryBar;
        var minimalAutoWindowHeight = config.FloatingStatsMinimalAutoWindowHeight;

        if (DrawLabeledCheckbox("高度自动适配条目数", "##minimal_auto_window_height", ref minimalAutoWindowHeight))
        {
            config.FloatingStatsMinimalAutoWindowHeight = minimalAutoWindowHeight;
            config.Save();
        }

        ImGui.TextDisabled("基础显示");
        if (ImGui.BeginTable("##minimal_basic_toggle_grid", 3, compactTableFlags))
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Checkbox("显示表头", ref showHeader))
            {
                config.FloatingStatsMinimalShowHeader = showHeader;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            if (ImGui.Checkbox("显示总DPS行", ref showSummaryRow))
            {
                config.FloatingStatsMinimalShowSummaryRow = showSummaryRow;
                config.Save();
            }

            ImGui.TableSetColumnIndex(2);
            if (ImGui.Checkbox("显示玩家列", ref showPlayerColumn))
            {
                config.FloatingStatsMinimalShowPlayerColumn = showPlayerColumn;
                config.Save();
            }

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Checkbox("显示伤害量列", ref showDamageColumn))
            {
                config.FloatingStatsMinimalShowDamageColumn = showDamageColumn;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            if (ImGui.Checkbox("显示死亡列", ref showDeathsColumn))
            {
                config.FloatingStatsMinimalShowDeathsColumn = showDeathsColumn;
                config.Save();
            }

            ImGui.EndTable();
        }

        ImGui.TextDisabled("占比条内容");
        if (ImGui.BeginTable("##minimal_share_toggle_grid", 3, compactTableFlags))
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Checkbox("占比条显示玩家名", ref showPlayerNameInShareBar))
            {
                config.FloatingStatsMinimalShowPlayerNameInShareBar = showPlayerNameInShareBar;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            if (ImGui.Checkbox("占比条显示职业", ref showJobInShareBar))
            {
                config.FloatingStatsMinimalShowJobInShareBar = showJobInShareBar;
                config.Save();
            }

            ImGui.TableSetColumnIndex(2);
            if (ImGui.Checkbox("占比条显示伤害", ref showDamageInShareBar))
            {
                config.FloatingStatsMinimalShowDamageInShareBar = showDamageInShareBar;
                config.Save();
            }

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Checkbox("占比条显示死亡", ref showDeathsInShareBar))
            {
                config.FloatingStatsMinimalShowDeathsInShareBar = showDeathsInShareBar;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            if (ImGui.Checkbox("占比条显示占比", ref showRatioInShareBar))
            {
                config.FloatingStatsMinimalShowRatioInShareBar = showRatioInShareBar;
                config.Save();
            }

            ImGui.EndTable();
        }

        ImGui.TextDisabled("总DPS条内容");
        if (ImGui.BeginTable("##minimal_summary_toggle_grid", 3, compactTableFlags))
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Checkbox("显示时间", ref showDurationInSummaryBar))
            {
                config.FloatingStatsMinimalShowDurationInSummaryBar = showDurationInSummaryBar;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            if (ImGui.Checkbox("显示标题", ref showTitleInSummaryBar))
            {
                config.FloatingStatsMinimalShowTitleInSummaryBar = showTitleInSummaryBar;
                config.Save();
            }

            ImGui.TableSetColumnIndex(2);
            if (ImGui.Checkbox("显示总DPS", ref showDpsInSummaryBar))
            {
                config.FloatingStatsMinimalShowDpsInSummaryBar = showDpsInSummaryBar;
                config.Save();
            }

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (ImGui.Checkbox("显示总伤害", ref showDamageInSummaryBar))
            {
                config.FloatingStatsMinimalShowDamageInSummaryBar = showDamageInSummaryBar;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            if (ImGui.Checkbox("显示总死亡数", ref showDeathsInSummaryBar))
            {
                config.FloatingStatsMinimalShowDeathsInSummaryBar = showDeathsInSummaryBar;
                config.Save();
            }

            ImGui.EndTable();
        }

        var minimalRowHeight = config.FloatingStatsMinimalRowHeight;
        var minimalFontScale = config.FloatingStatsMinimalFontScale;
        var minimalPlayerColumnWidth = config.FloatingStatsMinimalPlayerColumnWidth;
        var minimalDamageColumnWidth = config.FloatingStatsMinimalDamageColumnWidth;
        var minimalDeathsColumnWidth = config.FloatingStatsMinimalDeathsColumnWidth;

        ImGui.TextDisabled("尺寸与字号");
        if (ImGui.BeginTable("##minimal_size_grid", 2, compactTableFlags))
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (DrawLabeledSliderFloat("表格行高", "##minimal_row_height", ref minimalRowHeight, 1f, 60f, "%.0f"))
            {
                config.FloatingStatsMinimalRowHeight = minimalRowHeight;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            if (DrawLabeledSliderFloat("字号缩放", "##minimal_font_scale", ref minimalFontScale, 0.6f, 1.2f, "%.2f x"))
            {
                config.FloatingStatsMinimalFontScale = minimalFontScale;
                config.Save();
            }

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (DrawLabeledSliderFloat("玩家列宽", "##minimal_player_width", ref minimalPlayerColumnWidth, 1f, 400f, "%.0f"))
            {
                config.FloatingStatsMinimalPlayerColumnWidth = minimalPlayerColumnWidth;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            if (DrawLabeledSliderFloat("伤害量列宽", "##minimal_damage_width", ref minimalDamageColumnWidth, 1f, 400f, "%.0f"))
            {
                config.FloatingStatsMinimalDamageColumnWidth = minimalDamageColumnWidth;
                config.Save();
            }

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            if (DrawLabeledSliderFloat("死亡列宽", "##minimal_deaths_width", ref minimalDeathsColumnWidth, 1f, 200f, "%.0f"))
            {
                config.FloatingStatsMinimalDeathsColumnWidth = minimalDeathsColumnWidth;
                config.Save();
            }

            ImGui.EndTable();
        }

        DrawCompactHelp(
            "以上都是极简样式专属属性。",
            "开启“高度自动适配条目数”后，极简悬浮窗会按当前显示条目数、表头和总DPS行自动伸缩高度；关闭后可继续手动拖拽窗口高度。表格行高会同时影响单元格高度、占比条高度，并自动限制极简字号上限。");
        ImGui.PopStyleVar(3);
    }

    private void DrawIkegamiFloatingDisplayStyleSection()
    {
        const ImGuiTableFlags compactTableFlags =
            ImGuiTableFlags.SizingStretchSame
            | ImGuiTableFlags.NoSavedSettings;
        var style = ImGui.GetStyle();

        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(style.ItemSpacing.X, 2f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(style.FramePadding.X, 2f));
        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(style.CellPadding.X, 2f));

        ImGui.Dummy(new Vector2(0f, 2f));
        ImGui.Separator();
        ImGui.TextDisabled("Ikegami 专属布局微调");
        DrawCompactHelp("这些参数只影响 Ikegami 样式。", "用于微调名字行、色块、正文、footer、滚动条与字号。");

        if (ImGui.CollapsingHeader("结构与显示", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var ikegamiPanelRaise = config.FloatingStatsIkegamiPanelRaise;
            var ikegamiDetailRaise = config.FloatingStatsIkegamiDetailRaise;
            var ikegamiFooterRaise = config.FloatingStatsIkegamiFooterRaise;
            var ikegamiShowMaxHitDetail = config.FloatingStatsIkegamiShowMaxHitDetail;
            var ikegamiShowNameLine = config.FloatingStatsIkegamiShowNameLine;
            var ikegamiShowScrollbar = config.FloatingStatsIkegamiShowScrollbar;
            var ikegamiShowVerticalScrollbar = config.FloatingStatsIkegamiShowVerticalScrollbar;

            if (ImGui.BeginTable("##ikegami_structure_grid", 2, compactTableFlags))
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("色块上移", "##ikegami_panel_raise", ref ikegamiPanelRaise, 0f, 60f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiPanelRaise = ikegamiPanelRaise;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("最高伤害行上移", "##ikegami_detail_raise", ref ikegamiDetailRaise, 0f, 60f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiDetailRaise = ikegamiDetailRaise;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("footer 上移距离", "##ikegami_footer_raise", ref ikegamiFooterRaise, 0f, 80f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiFooterRaise = ikegamiFooterRaise;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledCheckbox("显示最高伤害技能", "##ikegami_show_max_hit_detail", ref ikegamiShowMaxHitDetail))
                {
                    config.FloatingStatsIkegamiShowMaxHitDetail = ikegamiShowMaxHitDetail;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledCheckbox("显示姓名行", "##ikegami_show_name_line", ref ikegamiShowNameLine))
                {
                    config.FloatingStatsIkegamiShowNameLine = ikegamiShowNameLine;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledCheckbox("显示横向滚动条", "##ikegami_show_scrollbar", ref ikegamiShowScrollbar))
                {
                    config.FloatingStatsIkegamiShowScrollbar = ikegamiShowScrollbar;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledCheckbox("显示纵向滚动条", "##ikegami_show_vertical_scrollbar", ref ikegamiShowVerticalScrollbar))
                {
                    config.FloatingStatsIkegamiShowVerticalScrollbar = ikegamiShowVerticalScrollbar;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                ImGui.Dummy(Vector2.Zero);
                ImGui.EndTable();
            }

            DrawCompactHelp("控制条带布局与显示开关。", "这里集中调整色块、最高伤害文本、footer 的纵向位置，以及 Ikegami 模式的显示开关。");
        }

        if (ImGui.CollapsingHeader("尺寸与对齐", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var ikegamiBoxWidth = config.FloatingStatsIkegamiBoxWidth;
            var ikegamiBoxHeight = config.FloatingStatsIkegamiBoxHeight;
            var ikegamiBoxAlignment = config.FloatingStatsIkegamiBoxAlignment;
            var ikegamiBoxAlignmentLabel = GetIkegamiBoxAlignmentLabel(ikegamiBoxAlignment);
            var ikegamiNameHeight = config.FloatingStatsIkegamiNameHeight;
            var ikegamiNameLeftPadding = config.FloatingStatsIkegamiNameLeftPadding;
            var ikegamiNameRightPadding = config.FloatingStatsIkegamiNameRightPadding;
            var ikegamiJobBadgeSize = config.FloatingStatsIkegamiJobBadgeSize;
            var ikegamiHeaderHeight = config.FloatingStatsIkegamiHeaderHeight;
            var ikegamiHeaderLeftPadding = config.FloatingStatsIkegamiHeaderLeftPadding;
            var ikegamiDetailLeftPadding = config.FloatingStatsIkegamiDetailLeftPadding;

            if (ImGui.BeginTable("##ikegami_size_grid", 2, compactTableFlags))
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("小框宽度", "##ikegami_box_width", ref ikegamiBoxWidth, 1f, 260f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiBoxWidth = ikegamiBoxWidth;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("小框高度", "##ikegami_box_height", ref ikegamiBoxHeight, 1f, 140f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiBoxHeight = ikegamiBoxHeight;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (BeginLabeledCombo("小框对齐", "##ikegami_box_alignment", ikegamiBoxAlignmentLabel))
                {
                    foreach (var alignment in Enum.GetValues<IkegamiBoxAlignment>())
                    {
                        var isSelected = alignment == ikegamiBoxAlignment;
                        if (ImGui.Selectable(GetIkegamiBoxAlignmentLabel(alignment), isSelected))
                        {
                            config.FloatingStatsIkegamiBoxAlignment = alignment;
                            ikegamiBoxAlignment = alignment;
                            ikegamiBoxAlignmentLabel = GetIkegamiBoxAlignmentLabel(alignment);
                            config.Save();
                        }

                        if (isSelected)
                            ImGui.SetItemDefaultFocus();
                    }

                    ImGui.EndCombo();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("姓名行高度", "##ikegami_name_height", ref ikegamiNameHeight, 16f, 40f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiNameHeight = ikegamiNameHeight;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("姓名左边距", "##ikegami_name_left_padding", ref ikegamiNameLeftPadding, 0f, 40f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiNameLeftPadding = ikegamiNameLeftPadding;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("姓名右边距", "##ikegami_name_right_padding", ref ikegamiNameRightPadding, 0f, 40f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiNameRightPadding = ikegamiNameRightPadding;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("职业框尺寸", "##ikegami_job_badge_size", ref ikegamiJobBadgeSize, 12f, 36f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiJobBadgeSize = ikegamiJobBadgeSize;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("色块高度", "##ikegami_header_height", ref ikegamiHeaderHeight, 20f, 80f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiHeaderHeight = ikegamiHeaderHeight;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("色块左内边距", "##ikegami_header_left_padding", ref ikegamiHeaderLeftPadding, 0f, 32f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiHeaderLeftPadding = ikegamiHeaderLeftPadding;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("正文左内边距", "##ikegami_detail_left_padding", ref ikegamiDetailLeftPadding, 0f, 32f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiDetailLeftPadding = ikegamiDetailLeftPadding;
                    config.Save();
                }

                ImGui.EndTable();
            }

            DrawCompactHelp("小框居中相对于整个悬浮窗。", "这里可同时调小框尺寸、对齐方式、名字行高度、职业框尺寸，以及色块和正文的内边距。");
        }

        if (ImGui.CollapsingHeader("透明度"))
        {
            var ikegamiNameAlpha = config.FloatingStatsIkegamiNameAlpha;
            var ikegamiHeaderAlpha = config.FloatingStatsIkegamiHeaderAlpha;
            var ikegamiPanelBackgroundAlpha = config.FloatingStatsIkegamiPanelBackgroundAlpha;
            var ikegamiBodyAlpha = config.FloatingStatsIkegamiBodyAlpha;
            var ikegamiFooterAlpha = config.FloatingStatsIkegamiFooterAlpha;
            var ikegamiNameBackgroundAlpha = config.FloatingStatsIkegamiNameBackgroundAlpha;
            var ikegamiBodyBackgroundAlpha = config.FloatingStatsIkegamiBodyBackgroundAlpha;
            var ikegamiContentBackgroundAlpha = config.FloatingStatsIkegamiContentBackgroundAlpha;

            if (ImGui.BeginTable("##ikegami_alpha_grid", 3, compactTableFlags))
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("姓名字透", "##ikegami_name_alpha", ref ikegamiNameAlpha, 0f, 1f, "%.2f"))
                {
                    config.FloatingStatsIkegamiNameAlpha = ikegamiNameAlpha;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("色块字透", "##ikegami_header_alpha", ref ikegamiHeaderAlpha, 0f, 1f, "%.2f"))
                {
                    config.FloatingStatsIkegamiHeaderAlpha = ikegamiHeaderAlpha;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(2);
                if (DrawLabeledSliderFloat("外层底透", "##ikegami_panel_background_alpha", ref ikegamiPanelBackgroundAlpha, 0f, 1f, "%.2f"))
                {
                    config.FloatingStatsIkegamiPanelBackgroundAlpha = ikegamiPanelBackgroundAlpha;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("正文字透", "##ikegami_body_alpha", ref ikegamiBodyAlpha, 0f, 1f, "%.2f"))
                {
                    config.FloatingStatsIkegamiBodyAlpha = ikegamiBodyAlpha;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("Footer字透", "##ikegami_footer_alpha", ref ikegamiFooterAlpha, 0f, 1f, "%.2f"))
                {
                    config.FloatingStatsIkegamiFooterAlpha = ikegamiFooterAlpha;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(2);
                if (DrawLabeledSliderFloat("姓名底透", "##ikegami_name_background_alpha", ref ikegamiNameBackgroundAlpha, 0f, 1f, "%.2f"))
                {
                    config.FloatingStatsIkegamiNameBackgroundAlpha = ikegamiNameBackgroundAlpha;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("正文底透", "##ikegami_body_background_alpha", ref ikegamiBodyBackgroundAlpha, 0f, 1f, "%.2f"))
                {
                    config.FloatingStatsIkegamiBodyBackgroundAlpha = ikegamiBodyBackgroundAlpha;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("内容底透", "##ikegami_content_background_alpha", ref ikegamiContentBackgroundAlpha, 0f, 1f, "%.2f"))
                {
                    config.FloatingStatsIkegamiContentBackgroundAlpha = ikegamiContentBackgroundAlpha;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(2);
                ImGui.Dummy(Vector2.Zero);

                ImGui.EndTable();
            }

            DrawCompactHelp("分别控制文字与底色透明度。", "内容区底色是整块滚动内容背景；外层底板是单个小框外轮廓；footer 文字透明度单独控制底部条。");
        }

        if (ImGui.CollapsingHeader("Footer 与字号"))
        {
            var ikegamiFooterHeight = config.FloatingStatsIkegamiFooterHeight;
            var ikegamiFooterTimeZoneSpacing = config.FloatingStatsIkegamiFooterTimeZoneSpacing;
            var ikegamiFooterRightPadding = config.FloatingStatsIkegamiFooterRightPadding;
            var ikegamiTabFontScale = config.FloatingStatsIkegamiTabFontScale;
            var ikegamiNameFontScale = config.FloatingStatsIkegamiNameFontScale;
            var ikegamiHeaderFontScale = config.FloatingStatsIkegamiHeaderFontScale;
            var ikegamiBodyFontScale = config.FloatingStatsIkegamiBodyFontScale;
            var ikegamiFooterFontScale = config.FloatingStatsIkegamiFooterFontScale;
            var ikegamiTooltipFontScale = config.FloatingStatsIkegamiTooltipFontScale;

            if (ImGui.BeginTable("##ikegami_footer_font_grid", 3, compactTableFlags))
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("Footer高", "##ikegami_footer_height", ref ikegamiFooterHeight, 18f, 48f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiFooterHeight = ikegamiFooterHeight;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("时间区域距", "##ikegami_footer_time_zone_spacing", ref ikegamiFooterTimeZoneSpacing, 0f, 32f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiFooterTimeZoneSpacing = ikegamiFooterTimeZoneSpacing;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(2);
                if (DrawLabeledSliderFloat("DPS右边距", "##ikegami_footer_right_padding", ref ikegamiFooterRightPadding, 0f, 40f, "%.0f px"))
                {
                    config.FloatingStatsIkegamiFooterRightPadding = ikegamiFooterRightPadding;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("页签字号", "##ikegami_tab_font_scale", ref ikegamiTabFontScale, 0.6f, 2.0f, "%.2f x"))
                {
                    config.FloatingStatsIkegamiTabFontScale = ikegamiTabFontScale;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("姓名字号", "##ikegami_name_font_scale", ref ikegamiNameFontScale, 0.6f, 2.0f, "%.2f x"))
                {
                    config.FloatingStatsIkegamiNameFontScale = ikegamiNameFontScale;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(2);
                if (DrawLabeledSliderFloat("色块字号", "##ikegami_header_font_scale", ref ikegamiHeaderFontScale, 0.6f, 2.0f, "%.2f x"))
                {
                    config.FloatingStatsIkegamiHeaderFontScale = ikegamiHeaderFontScale;
                    config.Save();
                }

                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                if (DrawLabeledSliderFloat("正文字号", "##ikegami_body_font_scale", ref ikegamiBodyFontScale, 0.6f, 2.0f, "%.2f x"))
                {
                    config.FloatingStatsIkegamiBodyFontScale = ikegamiBodyFontScale;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(1);
                if (DrawLabeledSliderFloat("Footer字号", "##ikegami_footer_font_scale", ref ikegamiFooterFontScale, 0.6f, 2.0f, "%.2f x"))
                {
                    config.FloatingStatsIkegamiFooterFontScale = ikegamiFooterFontScale;
                    config.Save();
                }

                ImGui.TableSetColumnIndex(2);
                if (DrawLabeledSliderFloat("Tooltip字号", "##ikegami_tooltip_font_scale", ref ikegamiTooltipFontScale, 0.6f, 2.0f, "%.2f x"))
                {
                    config.FloatingStatsIkegamiTooltipFontScale = ikegamiTooltipFontScale;
                    config.Save();
                }
                ImGui.EndTable();
            }

            DrawCompactHelp("统一调整 footer 与各区字号。", "页签、姓名行、色块、正文、footer 与 tooltip 的字号倍率都在这里。");
        }

        DrawCompactHelp("修改后会立即保存并实时生效。", "这些参数只写入 Ikegami 配置；切回 Classic 时不会覆盖经典样式参数。");
        ImGui.PopStyleVar(3);
    }

    private void DrawFloatingStyleFileManagementSection()
    {
        ImGui.Dummy(new Vector2(0f, 2f));
        ImGui.Separator();
        if (!ImGui.CollapsingHeader("样式管理"))
            return;

        ImGui.TextDisabled("样式分享码");
        foreach (var style in new[]
                 {
                     FloatingStatsDisplayStyle.Classic,
                     FloatingStatsDisplayStyle.Ikegami,
                     FloatingStatsDisplayStyle.Minimal,
                 })
        {
            if (style != FloatingStatsDisplayStyle.Classic)
                ImGui.SameLine();

            if (!ImGui.Button($"生成并复制 {GetFloatingStyleShareCodeStyleLabel(style)} 分享码"))
                continue;

            if (config.TryGenerateFloatingStyleShareCode(
                    style,
                    out var shareCode,
                    out var message))
            {
                floatingStyleShareCode = shareCode;
                ImGui.SetClipboardText(shareCode);
            }

            floatingStyleTransferStatusText = message;
        }

        DrawCompactHelp("生成后会自动复制到剪贴板。", "对外分享时直接发送整段文本即可。");

        var shareCodeBoxHeight = ImGui.GetTextLineHeightWithSpacing() * 3.0f;

        DrawCompactHelp("同一个输入框可粘贴或暂存分享码。", "复制按钮适合转发现成内容；导入时会自动识别 Classic / Ikegami / Minimal。");
        if (ImGui.Button("复制当前分享码"))
        {
            ImGui.SetClipboardText(floatingStyleShareCode ?? string.Empty);
            floatingStyleTransferStatusText = "已复制当前分享码。";
        }

        ImGui.SameLine();
        if (ImGui.Button("清空分享码"))
        {
            floatingStyleShareCode = string.Empty;
            floatingStyleTransferStatusText = "已清空分享码输入框。";
        }

        floatingStyleShareCode ??= string.Empty;
        ImGui.InputTextMultiline(
            "##floating_style_share_code",
            ref floatingStyleShareCode,
            65535,
            new Vector2(-1f, shareCodeBoxHeight));

        if (config.TryPeekFloatingStyleShareCodeStyle(floatingStyleShareCode, out var detectedStyle))
        {
            ImGui.TextDisabled($"已识别分享码样式：{GetFloatingStyleShareCodeStyleLabel(detectedStyle)}");
        }
        else if (!string.IsNullOrWhiteSpace(floatingStyleShareCode))
        {
            ImGui.TextDisabled("当前输入内容还不是可识别的分享码。");
        }

        if (ImGui.Button("按分享码标识导入"))
        {
            config.ImportFloatingStyleShareCode(
                floatingStyleShareCode,
                out floatingStyleTransferStatusText);
        }

        ImGui.Dummy(new Vector2(0f, 4f));
        ImGui.Separator();
        ImGui.TextDisabled("按样式恢复默认");

        foreach (var style in new[]
                 {
                     FloatingStatsDisplayStyle.Classic,
                     FloatingStatsDisplayStyle.Ikegami,
                     FloatingStatsDisplayStyle.Minimal,
                 })
        {
            if (style != FloatingStatsDisplayStyle.Classic)
                ImGui.SameLine();

            if (!ImGui.Button($"恢复 {GetFloatingStyleShareCodeStyleLabel(style)} 默认"))
                continue;

            config.ResetFloatingStyleToDefaults(style, out floatingStyleTransferStatusText);
            if (style == config.FloatingStatsDisplayStyle)
            {
                StatsPanel.RequestMetricColumnWidthReset();
                StatsPanel.RequestHistoryColumnWidthReset();
            }
        }

        DrawCompactHelp("只恢复指定样式的默认设置。", "恢复当前正在使用的样式时，会立即刷新当前界面；其它样式会写回各自样式文件，等切过去时生效。");

        if (!string.IsNullOrWhiteSpace(floatingStyleTransferStatusText))
            ImGui.TextWrapped(floatingStyleTransferStatusText);
    }

    private void DrawFloatingParticipantModeSection()
    {
        var currentMode = config.FloatingStatsParticipantDisplayMode;

        if (ImGui.RadioButton("智能：多人仅玩家，单人可含友方 NPC", currentMode == FloatingStatsParticipantDisplayMode.Auto))
        {
            config.FloatingStatsParticipantDisplayMode = FloatingStatsParticipantDisplayMode.Auto;
            currentMode = config.FloatingStatsParticipantDisplayMode;
            config.Save();
        }

        if (ImGui.RadioButton("仅玩家", currentMode == FloatingStatsParticipantDisplayMode.PlayersOnly))
        {
            config.FloatingStatsParticipantDisplayMode = FloatingStatsParticipantDisplayMode.PlayersOnly;
            currentMode = config.FloatingStatsParticipantDisplayMode;
            config.Save();
        }

        if (ImGui.RadioButton("玩家 + 友方 NPC", currentMode == FloatingStatsParticipantDisplayMode.PlayersAndFriendlyNpc))
        {
            config.FloatingStatsParticipantDisplayMode = FloatingStatsParticipantDisplayMode.PlayersAndFriendlyNpc;
            currentMode = config.FloatingStatsParticipantDisplayMode;
            config.Save();
        }

        if (ImGui.RadioButton("玩家 + 敌方 NPC", currentMode == FloatingStatsParticipantDisplayMode.PlayersAndHostileNpc))
        {
            config.FloatingStatsParticipantDisplayMode = FloatingStatsParticipantDisplayMode.PlayersAndHostileNpc;
            currentMode = config.FloatingStatsParticipantDisplayMode;
            config.Save();
        }

        ImGui.Dummy(new Vector2(0f, 2f));
        var hostileNpcMinHpMultiplier = config.HostileNpcMinHpMultiplier;
        if (ImGui.SliderInt("敌方 NPC 最低血量倍率", ref hostileNpcMinHpMultiplier, 1, 100, "%d x"))
        {
            config.HostileNpcMinHpMultiplier = hostileNpcMinHpMultiplier;
            config.Save();
        }

        var highlightNpcRows = config.HighlightNpcRows;
        if (ImGui.Checkbox("高亮 NPC 行", ref highlightNpcRows))
        {
            config.HighlightNpcRows = highlightNpcRows;
            config.Save();
        }

        if (ImGui.CollapsingHeader("规则说明"))
        {
            ImGui.TextDisabled("友方 NPC 包括信赖/NPC 队友、Buddy、幻体等可识别的友方对象。");
            ImGui.TextDisabled("“玩家 + 敌方 NPC” 模式下会隐藏友方 NPC，只保留玩家与敌方对象。");
            ImGui.TextDisabled("敌方 NPC 只有在最大生命值达到本地玩家最大生命值指定倍率后，才会进入悬浮统计。");
            ImGui.TextDisabled("关闭“高亮 NPC 行”后，NPC 会回退到普通条形配色与默认文本颜色。");
        }
        else
        {
            ImGui.TextDisabled("默认先收起规则说明；需要核对 NPC 纳入规则时再展开。");
        }
    }

    private void DrawFriendlyNpcNameListSection()
    {
        DrawCurrentPartyMemberList();
    }

    private void DrawCurrentPartyMemberList()
    {
        var members = statsService.GetCurrentPartyMemberDisplayInfos();
        if (!ImGui.CollapsingHeader($"当前队伍成员（{members.Count}）###current_party_member_names", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.TextDisabled("当前队伍成员默认展开；收起后这里只显示人数。");
            return;
        }

        if (members.Count == 0)
        {
            ImGui.TextDisabled("当前没有可显示的队伍成员。");
            return;
        }

        const ImGuiTableFlags tableFlags =
            ImGuiTableFlags.RowBg
            | ImGuiTableFlags.BordersInnerH
            | ImGuiTableFlags.SizingStretchProp
            | ImGuiTableFlags.NoSavedSettings;

        if (!ImGui.BeginTable("##current_party_member_name_table", 5, tableFlags))
            return;

        ImGui.TableSetupColumn("名字");
        ImGui.TableSetupColumn("职业", ImGuiTableColumnFlags.WidthFixed, 78f);
        ImGui.TableSetupColumn("类型", ImGuiTableColumnFlags.WidthFixed, 78f);
        ImGui.TableSetupColumn("生命", ImGuiTableColumnFlags.WidthFixed, 96f);
        ImGui.TableSetupColumn("操作", ImGuiTableColumnFlags.WidthFixed, 64f);
        ImGui.TableHeadersRow();

        for (var index = 0; index < members.Count; index++)
        {
            var member = members[index];
            ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(0);
            ImGui.TextUnformatted(member.Name);

            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(member.JobName);

            ImGui.TableSetColumnIndex(2);
            ImGui.TextUnformatted(member.KindName);

            ImGui.TableSetColumnIndex(3);
            ImGui.TextUnformatted(member.MaxHp > 0 ? $"{member.CurrentHp}/{member.MaxHp}" : "--");

            ImGui.TableSetColumnIndex(4);
            if (ImGui.SmallButton($"填入##fill_custom_friendly_npc_from_party_{index}"))
            {
                customFriendlyNpcNameInput = member.Name;
                customFriendlyNpcStatusText = $"已填入当前队伍成员名字：“{member.Name}”。";
            }
        }

        ImGui.EndTable();
    }

    private void AddCustomFriendlyNpcNameFromInput()
    {
        var rawName = customFriendlyNpcNameInput;
        var usedCurrentTarget = false;
        if (string.IsNullOrWhiteSpace(rawName))
        {
            rawName = DalamudApi.GetCurrentTargetName();
            usedCurrentTarget = !string.IsNullOrWhiteSpace(rawName);
            if (usedCurrentTarget)
                customFriendlyNpcNameInput = rawName!;
        }

        var normalizedName = PluginConfiguration.NormalizeFriendlyNpcNameForCatalog(rawName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            customFriendlyNpcStatusText = "请输入 NPC 名字，或先选中一个目标后再点“添加”。";
            return;
        }

        if (normalizedName.EndsWith("的幻体", StringComparison.Ordinal))
        {
            customFriendlyNpcStatusText = $"“{normalizedName}”已被“的幻体”规则自动识别，不需要加入自定义名单。";
            customFriendlyNpcNameInput = string.Empty;
            return;
        }

        if (LocalStatsService.IsBuiltInFriendlyNpcName(normalizedName))
        {
            customFriendlyNpcStatusText = $"“{normalizedName}”已经在内置名单中。";
            customFriendlyNpcNameInput = string.Empty;
            return;
        }

        config.CustomFriendlyNpcNames ??= new List<string>();
        config.NormalizeCustomFriendlyNpcNames();
        foreach (var existingName in config.CustomFriendlyNpcNames)
        {
            if (!string.Equals(existingName, normalizedName, StringComparison.Ordinal))
                continue;

            customFriendlyNpcStatusText = $"“{normalizedName}”已经在自定义名单中。";
            customFriendlyNpcNameInput = string.Empty;
            return;
        }

        config.CustomFriendlyNpcNames.Add(normalizedName);
        config.NormalizeCustomFriendlyNpcNames();
        config.Save();
        customFriendlyNpcNameInput = string.Empty;
        customFriendlyNpcStatusText = usedCurrentTarget
            ? $"已把当前目标加入自定义 NPC 队友名单：“{normalizedName}”。"
            : $"已加入自定义 NPC 队友名单：“{normalizedName}”。";
    }

    private void DrawCustomFriendlyNpcNameTable()
    {
        config.CustomFriendlyNpcNames ??= new List<string>();
        if (config.CustomFriendlyNpcNames.Count == 0)
        {
            ImGui.TextDisabled("暂无自定义 NPC 名字。遇到漏识别的剧情/任务友方 NPC 时，把名字填到上方添加即可。");
            return;
        }

        var removeIndex = -1;
        const ImGuiTableFlags tableFlags =
            ImGuiTableFlags.RowBg
            | ImGuiTableFlags.BordersInnerH
            | ImGuiTableFlags.SizingStretchProp
            | ImGuiTableFlags.NoSavedSettings;

        if (ImGui.BeginTable("##custom_friendly_npc_name_table", 2, tableFlags))
        {
            ImGui.TableSetupColumn("名字");
            ImGui.TableSetupColumn("操作", ImGuiTableColumnFlags.WidthFixed, 72f);
            ImGui.TableHeadersRow();

            for (var index = 0; index < config.CustomFriendlyNpcNames.Count; index++)
            {
                ImGui.TableNextRow();
                ImGui.TableSetColumnIndex(0);
                ImGui.TextUnformatted(config.CustomFriendlyNpcNames[index]);

                ImGui.TableSetColumnIndex(1);
                if (ImGui.SmallButton($"删除##remove_custom_friendly_npc_name_{index}"))
                    removeIndex = index;
            }

            ImGui.EndTable();
        }

        if (removeIndex >= 0 && removeIndex < config.CustomFriendlyNpcNames.Count)
        {
            var removedName = config.CustomFriendlyNpcNames[removeIndex];
            config.CustomFriendlyNpcNames.RemoveAt(removeIndex);
            config.NormalizeCustomFriendlyNpcNames();
            config.Save();
            customFriendlyNpcStatusText = $"已删除自定义 NPC 名字：“{removedName}”。";
        }

        if (ImGui.Button("复制自定义名单##copy_custom_friendly_npc_names"))
        {
            ImGui.SetClipboardText(string.Join(Environment.NewLine, config.CustomFriendlyNpcNames));
            customFriendlyNpcStatusText = "已复制自定义名单。";
        }

        ImGui.SameLine();
        if (ImGui.Button("清空自定义名单##clear_custom_friendly_npc_names"))
        {
            config.CustomFriendlyNpcNames.Clear();
            config.Save();
            customFriendlyNpcStatusText = "已清空自定义 NPC 队友名单。";
        }
    }

    private void DrawBuiltInFriendlyNpcNameTable()
    {
        if (ImGui.Button("复制内置名单##copy_builtin_friendly_npc_names"))
        {
            ImGui.SetClipboardText(string.Join(Environment.NewLine, LocalStatsService.BuiltInFriendlyNpcNames));
            customFriendlyNpcStatusText = "已复制内置名单。";
        }

        const ImGuiTableFlags tableFlags =
            ImGuiTableFlags.RowBg
            | ImGuiTableFlags.BordersInnerH
            | ImGuiTableFlags.SizingStretchProp
            | ImGuiTableFlags.NoSavedSettings;

        if (!ImGui.BeginTable("##builtin_friendly_npc_name_table", 3, tableFlags))
            return;

        ImGui.TableSetupColumn("内置名字");
        ImGui.TableSetupColumn("内置名字");
        ImGui.TableSetupColumn("内置名字");

        var names = LocalStatsService.BuiltInFriendlyNpcNames;
        for (var index = 0; index < names.Count; index++)
        {
            if (index % 3 == 0)
                ImGui.TableNextRow();

            ImGui.TableSetColumnIndex(index % 3);
            ImGui.TextUnformatted(names[index]);
        }

        ImGui.EndTable();
    }

    private static void DrawSemanticRow(string setting, string dps, string hps, string taken)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(setting);
        ImGui.TableSetColumnIndex(1);
        ImGui.TextUnformatted(dps);
        ImGui.TableSetColumnIndex(2);
        ImGui.TextUnformatted(hps);
        ImGui.TableSetColumnIndex(3);
        ImGui.TextUnformatted(taken);
    }

    private void DrawStoredWidthTable()
    {
        const ImGuiTableFlags flags =
            ImGuiTableFlags.RowBg
            | ImGuiTableFlags.BordersInnerH
            | ImGuiTableFlags.SizingFixedFit
            | ImGuiTableFlags.NoSavedSettings;

        if (!ImGui.BeginTable("##stored_widths", 4, flags))
            return;

        ImGui.TableSetupColumn("统计页");
        ImGui.TableSetupColumn("当前值");
        ImGui.TableSetupColumn("历史页");
        ImGui.TableSetupColumn("当前值");
        ImGui.TableHeadersRow();

        DrawStoredWidthPairRow("玩家列", config.FloatingStatsPlayerColumnWidth, "开始时间", config.HistoryStartTimeColumnWidth);
        DrawStoredWidthPairRow("职业列", config.FloatingStatsJobColumnWidth, "结束时间", config.HistoryEndTimeColumnWidth);
        DrawStoredWidthPairRow("伤害列", config.FloatingStatsDamageColumnWidth, "时长", config.HistoryDurationColumnWidth);
        DrawStoredWidthPairRow("秒伤列", config.FloatingStatsValueColumnWidth, null, 0f);
        DrawStoredWidthPairRow("死亡列", config.FloatingStatsDeathsColumnWidth, null, 0f);

        ImGui.EndTable();
    }

    private void DrawSharedColumnCheckbox(string label, ref bool value, bool syncSharedSettings)
    {
        var current = value;
        if (!ImGui.Checkbox(label, ref current))
            return;

        value = current;
        if (syncSharedSettings)
            config.SyncSharedColumnSettings();

        config.Save();
    }

    private static void DrawStoredWidthPairRow(string? leftLabel, float leftWidth, string? rightLabel, float rightWidth)
    {
        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(string.IsNullOrWhiteSpace(leftLabel) ? "-" : leftLabel);
        ImGui.TableSetColumnIndex(1);
        ImGui.TextUnformatted(string.IsNullOrWhiteSpace(leftLabel) ? "-" : FormatStoredWidth(leftWidth));
        ImGui.TableSetColumnIndex(2);
        ImGui.TextUnformatted(string.IsNullOrWhiteSpace(rightLabel) ? "-" : rightLabel);
        ImGui.TableSetColumnIndex(3);
        ImGui.TextUnformatted(string.IsNullOrWhiteSpace(rightLabel) ? "-" : FormatStoredWidth(rightWidth));
    }

    private static string FormatStoredWidth(float width)
        => width > 0f ? $"{width:0}px" : "自动";

    private static string GetFloatingDisplayStyleLabel(FloatingStatsDisplayStyle style)
        => style switch
        {
            FloatingStatsDisplayStyle.Classic => "Classic（经典表格）",
            FloatingStatsDisplayStyle.Ikegami => "Ikegami",
            FloatingStatsDisplayStyle.Minimal => "Minimal（极简样式）",
            _ => style.ToString(),
        };

    private static string GetFloatingDisplayStyleDescription(FloatingStatsDisplayStyle style)
        => style switch
        {
            FloatingStatsDisplayStyle.Classic => "经典表格布局，保留列宽、固定列宽和表格行高等旧参数。",
            FloatingStatsDisplayStyle.Ikegami => "横向条带卡片布局，使用专属的尺寸、透明度、滚动条与 footer 参数。",
            FloatingStatsDisplayStyle.Minimal => "极简表格布局：固定只显示 DPS，无页签；职业列与秒伤列会合并到占比条文字。",
            _ => "未识别的展示样式。",
        };

    private static string GetIkegamiBoxAlignmentLabel(IkegamiBoxAlignment alignment)
        => alignment switch
        {
            IkegamiBoxAlignment.Left => "左对齐",
            IkegamiBoxAlignment.Center => "居中",
            IkegamiBoxAlignment.Right => "右对齐",
            _ => alignment.ToString(),
        };

    private static string GetFloatingStyleShareCodeStyleLabel(FloatingStatsDisplayStyle style)
        => style switch
        {
            FloatingStatsDisplayStyle.Ikegami => "Ikegami",
            FloatingStatsDisplayStyle.Minimal => "Minimal",
            _ => "Classic",
        };

    private static bool DrawFirstLevelHeader(string label, ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None)
    {
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.25f, 0.45f, 0.75f, 0.45f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.25f, 0.45f, 0.75f, 0.55f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.25f, 0.45f, 0.75f, 0.65f));
        var result = ImGui.CollapsingHeader(label, flags);
        ImGui.PopStyleColor(3);
        return result;
    }

    private void DrawToggle(string label, ref bool value)
    {
        var current = value;
        if (ImGui.Checkbox(label, ref current))
        {
            value = current;
            config.Save();
        }
    }
}
