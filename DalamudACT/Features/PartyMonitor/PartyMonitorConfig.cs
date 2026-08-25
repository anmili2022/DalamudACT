using System.Collections.Generic;
using System.Numerics;

namespace DalamudACT;

public sealed class PartyMonitorConfig
{
    public bool EnablePartyMonitor { get; set; } = true;

    public bool ShowPartyMonitorWindow { get; set; } = true;

    public float PartyMonitorOpacity { get; set; } = 0.9f;

    public bool LockPartyMonitorWindow { get; set; }

    public bool AutoResizePartyMonitorWindow { get; set; }

    public bool MonitorFood { get; set; } = true;

    public int FoodExpiryWarningMinutes { get; set; } = 10;

    public bool MonitorRaidBuffs { get; set; } = true;

    public bool MonitorMitigations { get; set; } = true;

    public bool MonitorSkills
    {
        get => MonitorRaidBuffs || MonitorMitigations;
        set
        {
            MonitorRaidBuffs = value;
            MonitorMitigations = value;
        }
    }

    public bool AnonymousMode { get; set; } = true;

    public bool HideSkillsOnCooldown { get; set; }

    public bool MergeSkillGroups { get; set; } = true;

    public bool HideNameColumn { get; set; }

    public float IconSize { get; set; } = 30f;

    public float CountdownTextScale { get; set; } = 1f;

    public Vector4 CountdownTextColor { get; set; } = new(1f, 1f, 1f, 1f);

    public bool CountdownTextBottomCenter { get; set; }

    public bool EnhancedActiveStyle { get; set; } = true;

    public float ActiveGlowStrength { get; set; } = 1f;

    public float IconGap { get; set; } = 4f;

    public float RowGap { get; set; } = 3f;

    public Vector4 BackgroundColor { get; set; } = new(0.04f, 0.05f, 0.075f, 1f);

    public Dictionary<uint, PartyMonitorJobConfig> JobConfigs { get; set; } = new();

    public Dictionary<uint, PartyMonitorSkillDefaultConfig> DefaultJobSkillConfigs { get; set; } = new();

    public PartyMonitorJobConfig GetOrCreateJobConfig(uint classJobId)
    {
        if (!JobConfigs.TryGetValue(classJobId, out var jobConfig))
        {
            jobConfig = new PartyMonitorJobConfig();
            JobConfigs[classJobId] = jobConfig;
            InitializeDefaultEnabledSkills(classJobId, jobConfig);
        }
        return jobConfig;
    }

    private static void InitializeDefaultEnabledSkills(uint classJobId, PartyMonitorJobConfig jobConfig)
    {
        var skills = PartySkillCatalog.GetSkillsForJob(classJobId);
        foreach (var skill in skills)
        {
            if (!PartySkillCatalog.IsDefaultEnabled(skill.ActionId))
                continue;

            if (skill.Category == SkillCategory.Mitigation)
                jobConfig.EnabledMitigationActionIds.Add(skill.ActionId);
            else
                jobConfig.EnabledRaidBuffActionIds.Add(skill.ActionId);
        }
    }

    public void RemoveDefaultDisabledBuiltInSkills()
    {
        foreach (var (jobId, jobConfig) in JobConfigs)
        {
            foreach (var skill in PartySkillCatalog.GetSkillsForJob(jobId))
            {
                if (PartySkillCatalog.IsDefaultEnabled(skill.ActionId)
                    || jobConfig.CustomSkills.ContainsKey(skill.ActionId))
                {
                    continue;
                }

                jobConfig.EnabledMitigationActionIds.Remove(skill.ActionId);
                jobConfig.EnabledRaidBuffActionIds.Remove(skill.ActionId);
            }
        }
    }

    public void ResetEnabledSkillsToDefault(IEnumerable<uint> classJobIds)
    {
        foreach (var classJobId in classJobIds)
        {
            var jobConfig = GetOrCreateJobConfig(classJobId);
            jobConfig.EnabledMitigationActionIds.Clear();
            jobConfig.EnabledRaidBuffActionIds.Clear();

            if (DefaultJobSkillConfigs.TryGetValue(classJobId, out var defaultConfig))
            {
                foreach (var actionId in defaultConfig.EnabledMitigationActionIds)
                    jobConfig.EnabledMitigationActionIds.Add(actionId);
                foreach (var actionId in defaultConfig.EnabledRaidBuffActionIds)
                    jobConfig.EnabledRaidBuffActionIds.Add(actionId);
                continue;
            }

            InitializeDefaultEnabledSkills(classJobId, jobConfig);
        }
    }

    public void SaveCurrentEnabledSkillsAsDefault(IEnumerable<uint> classJobIds)
    {
        foreach (var classJobId in classJobIds)
        {
            var jobConfig = GetOrCreateJobConfig(classJobId);
            DefaultJobSkillConfigs[classJobId] = new PartyMonitorSkillDefaultConfig
            {
                EnabledMitigationActionIds = new HashSet<uint>(jobConfig.EnabledMitigationActionIds),
                EnabledRaidBuffActionIds = new HashSet<uint>(jobConfig.EnabledRaidBuffActionIds),
            };
        }
    }
}

public sealed class PartyMonitorJobConfig
{
    public HashSet<uint> EnabledMitigationActionIds { get; set; } = new();
    public HashSet<uint> EnabledRaidBuffActionIds { get; set; } = new();
    public Dictionary<uint, CustomSkillEntry> CustomSkills { get; set; } = new();
}

public sealed class PartyMonitorSkillDefaultConfig
{
    public HashSet<uint> EnabledMitigationActionIds { get; set; } = new();
    public HashSet<uint> EnabledRaidBuffActionIds { get; set; } = new();
}

public sealed record CustomSkillEntry(
    string Name,
    SkillCategory Category,
    float CooldownSeconds);
