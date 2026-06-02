using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal sealed partial class SettingsWindow
{
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
            "选择你要监控的模块：食物、团辅技能、减伤技能。",
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
                var monitorRaidBuffs = pm.MonitorRaidBuffs;
                if (ImGui.Checkbox("监控团辅", ref monitorRaidBuffs))
                {
                    pm.MonitorRaidBuffs = monitorRaidBuffs;
                    config.Save();
                }

                ImGui.SameLine();
                var monitorMitigations = pm.MonitorMitigations;
                if (ImGui.Checkbox("监控减伤", ref monitorMitigations))
                {
                    pm.MonitorMitigations = monitorMitigations;
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
        if (!ImGui.CollapsingHeader("悬浮窗样式", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        ImGui.Dummy(new Vector2(0f, 2f));

        const ImGuiTableFlags flags =
            ImGuiTableFlags.SizingStretchSame
            | ImGuiTableFlags.NoSavedSettings;

        var sectionHeaderColor = UiThemeColors.Get(config.SelectedUiTheme).Accent;

        ImGui.TextColored(sectionHeaderColor, "尺寸与 CD 数字");
        ImGui.Separator();
        ImGui.Dummy(new Vector2(0f, 2f));

        if (ImGui.BeginTable("##party_monitor_style_size_grid", 2, flags))
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);

            var iconSize = pm.IconSize;
            if (DrawLabeledSliderFloat("图标大小", "##party_monitor_icon_size", ref iconSize, 20f, 48f, "%.0f px"))
            {
                pm.IconSize = iconSize;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            var countdownScale = pm.CountdownTextScale;
            if (DrawLabeledSliderFloat("CD数字大小", "##party_monitor_countdown_scale", ref countdownScale, 0.6f, 2f, "%.2f x"))
            {
                pm.CountdownTextScale = countdownScale;
                config.Save();
            }

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextDisabled("CD数字颜色");
            ImGui.SetCursorPosY(Math.Max(0f, ImGui.GetCursorPosY() - 1f));
            ImGui.SetNextItemWidth(-1f);
            var countdownColor = pm.CountdownTextColor;
            if (ImGui.ColorEdit4("##party_monitor_countdown_color", ref countdownColor))
            {
                pm.CountdownTextColor = countdownColor;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            var countdownBottom = pm.CountdownTextBottomCenter;
            if (DrawLabeledCheckbox("CD数字位置", "##party_monitor_countdown_bottom", ref countdownBottom, "底部居中", "垂直居中"))
            {
                pm.CountdownTextBottomCenter = countdownBottom;
                config.Save();
            }

            ImGui.EndTable();
        }

        ImGui.Dummy(new Vector2(0f, 6f));

        ImGui.TextColored(sectionHeaderColor, "布局与显示规则");
        ImGui.Separator();
        ImGui.Dummy(new Vector2(0f, 2f));

        if (ImGui.BeginTable("##party_monitor_style_rule_grid", 2, flags))
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);

            var hideSkillsOnCooldown = pm.HideSkillsOnCooldown;
            if (DrawLabeledCheckbox("CD中技能", "##party_monitor_hide_cd", ref hideSkillsOnCooldown, "隐藏", "显示"))
            {
                pm.HideSkillsOnCooldown = hideSkillsOnCooldown;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            var mergeSkillGroups = pm.MergeSkillGroups;
            if (DrawLabeledCheckbox("团辅/减伤分组合并", "##party_monitor_merge_groups", ref mergeSkillGroups, "已开启", "已关闭"))
            {
                pm.MergeSkillGroups = mergeSkillGroups;
                config.Save();
            }

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            var hideNameColumn = pm.HideNameColumn;
            if (DrawLabeledCheckbox("姓名/职业列", "##party_monitor_hide_name_column", ref hideNameColumn, "隐藏", "显示"))
            {
                pm.HideNameColumn = hideNameColumn;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            var iconGap = pm.IconGap;
            if (DrawLabeledSliderFloat("图标列间距", "##party_monitor_icon_gap", ref iconGap, 1f, 12f, "%.0f px"))
            {
                pm.IconGap = iconGap;
                config.Save();
            }

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            var rowGap = pm.RowGap;
            if (DrawLabeledSliderFloat("行间距", "##party_monitor_row_gap", ref rowGap, 0f, 12f, "%.0f px"))
            {
                pm.RowGap = rowGap;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            DrawCompactHelp("列间距控制技能图标之间的水平间隔", "行间距控制成员行之间的垂直间隔。");

            ImGui.EndTable();
        }

        ImGui.Dummy(new Vector2(0f, 6f));

        ImGui.TextColored(sectionHeaderColor, "起效高亮与背景");
        ImGui.Separator();
        ImGui.Dummy(new Vector2(0f, 2f));

        if (ImGui.BeginTable("##party_monitor_style_effect_grid", 2, flags))
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);

            var enhancedActive = pm.EnhancedActiveStyle;
            if (DrawLabeledCheckbox("起效增强样式", "##party_monitor_enhanced_active", ref enhancedActive))
            {
                pm.EnhancedActiveStyle = enhancedActive;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            var glowStrength = pm.ActiveGlowStrength;
            if (DrawLabeledSliderFloat("起效增强强度", "##party_monitor_glow_strength", ref glowStrength, 0f, 2f, "%.2f x"))
            {
                pm.ActiveGlowStrength = glowStrength;
                config.Save();
            }

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ImGui.TextDisabled("背景默认颜色");
            ImGui.SetCursorPosY(Math.Max(0f, ImGui.GetCursorPosY() - 1f));
            ImGui.SetNextItemWidth(-1f);
            var bg = pm.BackgroundColor;
            if (ImGui.ColorEdit4("##party_monitor_bg_color", ref bg))
            {
                pm.BackgroundColor = bg;
                config.Save();
            }

            ImGui.TableSetColumnIndex(1);
            DrawCompactHelp("默认白色数字、垂直居中。", "CD 数字颜色、大小和位置会立即影响监控窗口；图标大小改变时，数字也会跟随缩放。 ");

            ImGui.EndTable();
        }
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

                var hasAnyRaidBuffEnabled = monitorJobIds.Any(jobId => pm.GetOrCreateJobConfig(jobId).EnabledRaidBuffActionIds.Count > 0);
                if (ImGui.Button(hasAnyRaidBuffEnabled ? "一键关闭所有团辅技能" : "一键开启所有团辅技能"))
                {
                    foreach (var jobId in monitorJobIds)
                    {
                        var jobConfig = pm.GetOrCreateJobConfig(jobId);
                        jobConfig.EnabledRaidBuffActionIds.Clear();

                        if (!hasAnyRaidBuffEnabled)
                        {
                            foreach (var skill in PartySkillCatalog.GetSkillsForJob(jobId, jobConfig))
                            {
                                if (skill.Category == SkillCategory.RaidBuff)
                                    jobConfig.EnabledRaidBuffActionIds.Add(skill.ActionId);
                            }
                        }
                    }

                    config.Save();
                    LogHelper.PrintWithModule(
                        "队友监控",
                        "团辅技能",
                        hasAnyRaidBuffEnabled
                            ? "已关闭所有职业的团辅技能监控。减伤技能设置未改变。"
                            : "已开启所有职业的团辅技能监控。减伤技能设置未改变。");
                }

                ImGui.SameLine();
                DrawHelpMarker("这是一个切换按钮：当前有团辅启用时点击会全部关闭；全部关闭后再点击会开启所有团辅。不会影响减伤技能和食物监控。 ");

                ImGui.SameLine(0f, 12f);
                if (ImGui.Button("重置监控技能"))
                {
                    pm.ResetEnabledSkillsToDefault(monitorJobIds);
                    monitorService?.InvalidateSkillsCache();
                    config.Save();
                    LogHelper.PrintWithModule("队友监控", "技能设置", "已重置监控技能为默认勾选。自定义技能定义未删除。 ");
                }

                ImGui.SameLine();
                DrawHelpMarker("恢复所有职业的默认勾选状态。若已点击“设当前为默认”，会使用你保存的默认；否则使用插件内置默认。不会删除自定义技能。 ");

                ImGui.SameLine(0f, 8f);
                if (ImGui.Button("设当前为默认"))
                {
                    pm.SaveCurrentEnabledSkillsAsDefault(monitorJobIds);
                    config.Save();
                    LogHelper.PrintWithModule("队友监控", "技能设置", "已将当前所有职业的监控技能勾选状态保存为默认。 ");
                }

                ImGui.SameLine();
                DrawHelpMarker("把当前所有职业的团辅/减伤勾选状态保存为你的默认。之后点击“重置监控技能”会恢复到这套勾选。 ");

                ImGui.Dummy(new Vector2(0f, 4f));

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

                        if (KamiIconLoader.TryDrawIcon(skill.ActionId, new Vector2(22f, 22f)))
                        {
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
        ImGui.TextColored(UiThemeColors.Get(config.SelectedUiTheme).Accent, "添加自定义技能（当目录中缺少某个技能时使用）");

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
                var existingSkill = PartySkillCatalog
                    .GetSkillsForJob(customSkillSelectedJobId, jobConfig)
                    .FirstOrDefault(skill => skill.ActionId == actId || skill.TriggerActionIds.Contains(actId));

                if (existingSkill != null)
                {
                    customSkillStatusText = $"[{actId}] 已在 {customSkillSelectedJobName ?? PartyMonitorWindow.GetJobName(customSkillSelectedJobId)} 的监控技能列表中：{existingSkill.Name}。";
                }
                else
                {
                    jobConfig.CustomSkills[actId] = new CustomSkillEntry(skillName, category, cd);
                    if (category == SkillCategory.Mitigation)
                        jobConfig.EnabledMitigationActionIds.Add(actId);
                    else
                        jobConfig.EnabledRaidBuffActionIds.Add(actId);
                    monitorService?.InvalidateSkillsCache();
                    config.Save();
                    customSkillStatusText = $"已添加自定义技能：[{actId}] {skillName}。";
                    customSkillActionIdInputs[globalCustomKey] = string.Empty;
                    customSkillNameInputs[globalCustomKey] = string.Empty;
                    customSkillCdInputs[globalCustomKey] = string.Empty;
                }
            }
            else
            {
                customSkillStatusText = "请选择职业，并填写有效的技能 ID、技能名称和大于 0 的冷却时间。";
            }
        }

        if (!string.IsNullOrWhiteSpace(customSkillStatusText))
            ImGui.TextDisabled(customSkillStatusText);

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
}
