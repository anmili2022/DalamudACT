using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;

namespace DalamudACT;

internal sealed class PartyMonitorService
{
    private const uint FoodStatusCategory = 4;
    private const uint FoodStatusId = 48;

    private readonly PluginConfiguration config;
    private readonly LocalStatsService statsService;

    public readonly struct PartyMemberState
    {
        public readonly uint ActorId;
        public readonly string Name;
        public readonly uint JobId;
        public readonly bool HasFood;
        public readonly float FoodRemainingSeconds;
        public readonly IReadOnlyList<SkillCooldownState> MitigationSkills;
        public readonly IReadOnlyList<SkillCooldownState> RaidBuffSkills;

        public PartyMemberState(
            uint actorId, string name, uint jobId,
            bool hasFood, float foodRemainingSeconds,
            IReadOnlyList<SkillCooldownState> mitigationSkills,
            IReadOnlyList<SkillCooldownState> raidBuffSkills)
        {
            ActorId = actorId;
            Name = name;
            JobId = jobId;
            HasFood = hasFood;
            FoodRemainingSeconds = foodRemainingSeconds;
            MitigationSkills = mitigationSkills;
            RaidBuffSkills = raidBuffSkills;
        }
    }

    public readonly struct SkillCooldownState
    {
        public readonly PartySkillEntry Skill;
        public readonly float RemainingCooldown;
        public readonly float RemainingActiveDuration;
        public readonly bool IsReady;
        public readonly bool IsActive;

        public SkillCooldownState(PartySkillEntry skill, float remainingCooldown, float remainingActiveDuration, bool isReady, bool isActive)
        {
            Skill = skill;
            RemainingCooldown = remainingCooldown;
            RemainingActiveDuration = remainingActiveDuration;
            IsReady = isReady;
            IsActive = isActive;
        }
    }

    private readonly Dictionary<long, DateTime> lastSkillUseUtc = new();
    private DateTime lastFoodPollUtc;
    private DateTime lastUpdateUtc;
    private DateTime lastRebuildUtc = DateTime.MinValue;
    private List<PartyMemberState> cachedMemberStates = new();
    private readonly object gate = new();
    private readonly Dictionary<uint, List<PartySkillEntry>> skillsCache = new();

    public PartyMonitorService(PluginConfiguration config, LocalStatsService statsService)
    {
        this.config = config;
        this.statsService = statsService;
    }

    public IReadOnlyList<PartyMemberState> GetMemberStates()
    {
        lock (gate)
        {
            if (cachedMemberStates.Count > 0)
                return cachedMemberStates;
        }

        RebuildMemberStates(DateTime.UtcNow);

        lock (gate)
            return cachedMemberStates;
    }

    public void Update()
    {
        if (!config.PartyMonitor.MonitorFood && !config.PartyMonitor.MonitorSkills)
            return;

        var nowUtc = DateTime.UtcNow;
        if ((nowUtc - lastUpdateUtc).TotalMilliseconds < 250)
            return;

        lastUpdateUtc = nowUtc;
        PollFoodBuffs(nowUtc);
        if ((nowUtc - lastRebuildUtc).TotalMilliseconds >= 200)
        {
            lastRebuildUtc = nowUtc;
            RebuildMemberStates(nowUtc);
        }
    }

    public void InvalidateSkillsCache()
    {
        lock (gate)
            skillsCache.Clear();
    }

    public void RecordSkillUse(uint sourceActorId, uint actionId, DateTime nowUtc)
    {
        if (string.IsNullOrEmpty(DalamudApi.GetLocalPlayerName()))
            return;

        var skill = FindSkillWithCustom(actionId);
        if (skill == null)
            return;

        var cfg = config.PartyMonitor;
        if (!cfg.MonitorSkills)
            return;

        lock (gate)
            lastSkillUseUtc[CombineKey(sourceActorId, skill.ActionId)] = nowUtc;
    }

    private void PollFoodBuffs(DateTime nowUtc)
    {
        if (!config.PartyMonitor.MonitorFood)
            return;

        if ((nowUtc - lastFoodPollUtc).TotalMilliseconds < 500)
            return;

        lastFoodPollUtc = nowUtc;

        lock (gate)
        {
            foreach (var member in DalamudApi.PartyList)
            {
                try
                {
                    var actorId = GetActorId(member);
                    if (actorId == 0) continue;
                    var remaining = GetFoodRemaining(member.Statuses);
                    var key = CombineKey(actorId, 0);
                    if (remaining > 0f)
                        lastSkillUseUtc[key] = nowUtc.AddSeconds(remaining);
                    else
                        lastSkillUseUtc.Remove(key);
                }
                catch
                {
                }
            }
        }
    }

    private void RebuildMemberStates(DateTime nowUtc)
    {
        try
        {
            var cfg = config.PartyMonitor;
            var newStates = new List<PartyMemberState>();
            var seenActors = new HashSet<uint>();
            var seenNames = new HashSet<string>(StringComparer.Ordinal);

            TryAddLocalPlayerState(newStates, nowUtc, cfg);
            foreach (var state in newStates)
            {
                if (state.ActorId != 0)
                    seenActors.Add(state.ActorId);
                if (!string.IsNullOrWhiteSpace(state.Name))
                    seenNames.Add(state.Name.Trim());
            }

            var snapshot = statsService.BuildLocalPartyHelperSnapshot();

            foreach (var chara in snapshot.Party)
            {
                try
                {
                    var actorId = ResolveActorId(chara);
                    if (actorId == 0 || !seenActors.Add(actorId))
                        continue;

                    var name = chara.Name.TextValue?.Trim();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    seenNames.Add(name);

                    var food = cfg.MonitorFood ? GetFoodFromDictOrStatus(actorId, chara, name) : 0f;
                    newStates.Add(new PartyMemberState(
                        actorId, name, chara.ClassJob.RowId,
                        food > 0f, food,
                        cfg.MonitorSkills ? BuildSkillStates(actorId, chara.ClassJob.RowId, SkillCategory.Mitigation, nowUtc, cfg) : [],
                        cfg.MonitorSkills ? BuildSkillStates(actorId, chara.ClassJob.RowId, SkillCategory.RaidBuff, nowUtc, cfg) : []));
                }
                catch
                {
                }
            }

            foreach (var member in snapshot.UnresolvedPartyMemberDisplayInfos)
            {
                try
                {
                    if (member.ActorId == 0 || !seenActors.Add(member.ActorId))
                        continue;
                    if (string.IsNullOrWhiteSpace(member.Name))
                        continue;
                    if (seenNames.Contains(member.Name))
                        continue;

                    var food = cfg.MonitorFood ? GetFoodFromDictOrLocalFallback(member.ActorId, member.Name) : 0f;
                    newStates.Add(new PartyMemberState(
                        member.ActorId, member.Name, 0,
                        food > 0f, food,
                        cfg.MonitorSkills ? BuildSkillStates(member.ActorId, 0, SkillCategory.Mitigation, nowUtc, cfg) : [],
                        cfg.MonitorSkills ? BuildSkillStates(member.ActorId, 0, SkillCategory.RaidBuff, nowUtc, cfg) : []));
                }
                catch
                {
                }
            }

            lock (gate)
                cachedMemberStates = newStates;
        }
        catch
        {
            var fallbackStates = new List<PartyMemberState>();
            TryAddLocalPlayerState(fallbackStates, nowUtc, config.PartyMonitor);
            lock (gate)
                cachedMemberStates = fallbackStates;
        }
    }

    private void TryAddLocalPlayerState(List<PartyMemberState> states, DateTime nowUtc, PartyMonitorConfig cfg)
    {
        try
        {
            if (!DalamudApi.TryGetLocalPlayerInfo(out var actorId, out var name, out var jobId, out var localPlayer))
                return;

            var food = cfg.MonitorFood
                ? localPlayer == null ? GetFoodFromDict(actorId) : GetFoodFromDictOrStatus(actorId, localPlayer, name)
                : 0f;
            states.Add(new PartyMemberState(
                actorId, name, jobId,
                food > 0f, food,
                cfg.MonitorSkills ? BuildSkillStates(actorId, jobId, SkillCategory.Mitigation, nowUtc, cfg) : [],
                cfg.MonitorSkills ? BuildSkillStates(actorId, jobId, SkillCategory.RaidBuff, nowUtc, cfg) : []));
        }
        catch
        {
        }
    }

    private float GetFoodFromDictOrStatus(uint actorId, IBattleChara chara, string? name = null)
    {
        var fromDict = GetFoodFromDict(actorId);
        if (fromDict > 0f) return fromDict;

        var reflected = GetFoodRemainingFromStatuses(StatusReflectionAccessor.GetStatuses(chara));
        if (reflected > 0f)
            return reflected;

        var localName = DalamudApi.GetLocalPlayerName()?.Trim();
        if (!string.IsNullOrWhiteSpace(name)
            && !string.IsNullOrWhiteSpace(localName)
            && string.Equals(localName, name.Trim(), StringComparison.Ordinal))
        {
            return GetLocalPlayerFoodRemaining();
        }

        return 0f;
    }

    private float GetFoodFromDictOrLocalFallback(uint actorId, string name)
    {
        var fromDict = GetFoodFromDict(actorId);
        if (fromDict > 0f)
            return fromDict;

        var localName = DalamudApi.GetLocalPlayerName()?.Trim();
        if (string.IsNullOrWhiteSpace(localName) || !string.Equals(localName, name, StringComparison.Ordinal))
            return 0f;

        var localPlayer = DalamudApi.GetLocalPlayerBattleChara();
        return localPlayer == null ? 0f : GetFoodFromDictOrStatus(DalamudApi.GetLocalPlayerActorId(), localPlayer, localName);
    }

    private static float GetLocalPlayerFoodRemaining()
    {
        var localPlayer = DalamudApi.GetLocalPlayerBattleChara();
        return localPlayer == null ? 0f : GetFoodRemainingFromStatuses(StatusReflectionAccessor.GetStatuses(localPlayer));
    }

    private float GetFoodFromDict(uint actorId)
    {
        var key = CombineKey(actorId, 0);
        if (lastSkillUseUtc.TryGetValue(key, out var expiry) && expiry > DateTime.UtcNow)
            return (float)(expiry - DateTime.UtcNow).TotalSeconds;
        return 0f;
    }

    private static uint ResolveActorId(IBattleChara chara)
    {
        var id = unchecked((uint)(chara.GameObjectId & uint.MaxValue));
        if (id == 0) id = chara.EntityId;
        return id;
    }

    private static uint GetActorId(Dalamud.Game.ClientState.Party.IPartyMember member)
    {
        try
        {
            var id = ReadUIntProperty(member, "EntityId");
            if (id == 0)
                id = ReadUIntProperty(member, "ObjectId");
            if (id != 0) return id;
            var obj = member.GameObject;
            if (obj != null)
            {
                var gid = unchecked((uint)(obj.GameObjectId & uint.MaxValue));
                if (gid != 0) return gid;
            }
            return unchecked((uint)(member.Address & uint.MaxValue));
        }
        catch
        {
            return 0;
        }
    }

    private static float GetFoodRemaining(StatusList statusList)
    {
        var remaining = 0f;
        for (var i = 0; i < statusList.Length; i++)
        {
            var status = statusList[i];
            if (status == null) continue;
            try
            {
                if ((status.StatusId == FoodStatusId || status.GameData.Value.StatusCategory == FoodStatusCategory)
                    && status.RemainingTime > remaining)
                    remaining = status.RemainingTime;
            }
            catch
            {
            }
        }
        return remaining;
    }

    private static float GetFoodRemainingFromStatuses(IReadOnlyList<object> statuses)
    {
        var remaining = 0f;
        foreach (var status in statuses)
        {
            var statusId = StatusReflectionAccessor.GetStatusId(status);
            var category = StatusReflectionAccessor.GetCategory(status);

            if (statusId != FoodStatusId && category != FoodStatusCategory)
                continue;

            var statusRemaining = StatusReflectionAccessor.GetRemainingTime(status);
            if (statusRemaining > remaining)
                remaining = statusRemaining;
        }

        return remaining;
    }

    private static uint ReadUIntProperty(object instance, string propertyName)
        => StatusReflectionAccessor.GetUInt32(instance, propertyName);

    private List<SkillCooldownState> BuildSkillStates(
        uint actorId, uint jobId, SkillCategory category,
        DateTime nowUtc, PartyMonitorConfig cfg)
    {
        var jobConfig = cfg.GetOrCreateJobConfig(jobId);
        var enabledIds = category == SkillCategory.Mitigation
            ? jobConfig.EnabledMitigationActionIds
            : jobConfig.EnabledRaidBuffActionIds;

        if (!skillsCache.TryGetValue(jobId, out var skills))
        {
            skills = PartySkillCatalog.GetSkillsForJob(jobId, jobConfig);
            skillsCache[jobId] = skills;
        }
        var result = new List<SkillCooldownState>(skills.Count);

        foreach (var skill in skills)
        {
            if (skill.Category != category || !enabledIds.Contains(skill.ActionId))
                continue;

            var lastUseUtc = GetLastUseTime(actorId, skill);
            var elapsedSeconds = lastUseUtc is DateTime useTime ? (nowUtc - useTime).TotalSeconds : double.MaxValue;
            var isActive = skill.ActiveDurationSeconds > 0f && elapsedSeconds < skill.ActiveDurationSeconds;
            var isReady = elapsedSeconds >= skill.CooldownSeconds;
            var remainingActive = isActive ? Math.Max(0f, skill.ActiveDurationSeconds - (float)elapsedSeconds) : 0f;

            result.Add(new SkillCooldownState(skill,
                isReady ? 0f : Math.Max(0f, (float)(skill.CooldownSeconds - elapsedSeconds)),
                remainingActive,
                isReady,
                isActive));
        }

        return result;
    }

    private DateTime? GetLastUseTime(uint actorId, PartySkillEntry skill)
    {
        lock (gate)
        {
            if (lastSkillUseUtc.TryGetValue(CombineKey(actorId, skill.ActionId), out var directUse))
                return directUse;

            foreach (var triggerId in skill.TriggerActionIds)
            {
                if (lastSkillUseUtc.TryGetValue(CombineKey(actorId, triggerId), out var triggeredUse))
                    return triggeredUse;
            }

            return null;
        }
    }

    private static long CombineKey(uint actorId, uint actionId)
        => ((long)actorId << 32) | actionId;

    private PartySkillEntry? FindSkillWithCustom(uint actionId)
    {
        var s = PartySkillCatalog.FindSkill(actionId);
        if (s != null) return s;
        foreach (var (_, jc) in config.PartyMonitor.JobConfigs)
        {
            if (jc.CustomSkills.TryGetValue(actionId, out var c))
                return new PartySkillEntry(actionId, c.Name, c.Category, c.CooldownSeconds);
        }
        return null;
    }
}
