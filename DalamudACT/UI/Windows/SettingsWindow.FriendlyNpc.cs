using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal sealed partial class SettingsWindow
{
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
}
