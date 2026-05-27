using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace DalamudACT;

internal static partial class StatsPanel
{
    private static void DrawOverviewTab(CombatDataWrapper combatData, PluginConfiguration config)
    {
        if (!ImGui.BeginChild("##overview_scroll", new Vector2(0f, 0f), false))
        {
            ImGui.EndChild();
            return;
        }

        var encounter = combatData.Msg!.Encounter!;
        ImGui.TextUnformatted($"区域: {encounter.CurrentZoneName ?? "Unknown"}");
        ImGui.TextUnformatted($"战斗时长: {encounter.DurationText ?? "00:00"}");
        ImGui.Separator();

        if (ImGui.BeginTable("##overview_summary", 2, ReadOnlyTableFlags))
        {
            DrawOverviewRow("总伤害", encounter.DamageText, config);
            DrawOverviewRow("团队 DPS", encounter.EncDpsText, config);
            DrawOverviewRow("命中 / 失败", $"{encounter.HitsText ?? "0"}/{encounter.HitFailedText ?? "0"}", config);
            DrawOverviewRow("暴击次数", $"{encounter.CritHitsText ?? "0"} ({encounter.CritHitPercentText ?? "0%"})", config);
            DrawOverviewRow("最大伤害", JoinPair(encounter.MaxHitText, encounter.MaxHitValueText), config);
            DrawOverviewRow("总承伤", encounter.DamageTakenText, config);
            ImGui.EndTable();
        }

        ImGui.Separator();
        foreach (var combatant in GetVisibleCombatants(combatData, config))
        {
            var header = string.IsNullOrWhiteSpace(combatant.Job)
                ? combatant.Name!
                : $"{combatant.Name} ({combatant.Job})";
            if (!ImGui.CollapsingHeader(header))
                continue;

            if (!ImGui.BeginTable($"##combatant_{combatant.Name}", 2, ReadOnlyTableFlags))
                continue;

            DrawOverviewRow("伤害占比", combatant.DamagePercentText, config);
            DrawOverviewRow("总伤害", combatant.DamageText, config);
            DrawOverviewRow("DPS", combatant.EncDpsText, config);
            DrawOverviewRow("HPS", combatant.EncHpsText, config);
            DrawOverviewRow("DTPS", combatant.DtpsText, config);
            DrawOverviewRow("最大伤害技能", combatant.MaxHitText, config);
            DrawOverviewRow("命中次数", combatant.HitsText, config);
            DrawOverviewRow("暴击次数", combatant.CritHitsText, config);
            DrawOverviewRow("命中率", combatant.ToHitText, config);
            DrawOverviewRow("承受伤害", combatant.DamageTakenText, config);
            DrawOverviewRow("格挡率", combatant.BlockPctText, config);
            DrawOverviewRow("招架率", combatant.ParryPctText, config);
            DrawOverviewRow("死亡", combatant.DeathsText, config);
            DrawOverviewRow("DoT总伤害", combatant.DotDamageText, config);
            ImGui.EndTable();
        }

        ImGui.EndChild();
    }

    private static void DrawOverviewRow(string label, string? value, PluginConfiguration config)
    {
        TableNextRow(ResolveRowHeight(config));
        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted(label);
        ImGui.TableSetColumnIndex(1);
        ImGui.TextUnformatted(string.IsNullOrWhiteSpace(value) ? "--" : value);
    }
}
