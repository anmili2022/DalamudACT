using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

internal sealed class LocalStatsService
{
    private const uint InvalidActorId = 0xE0000000;
    private static readonly TimeSpan OwnerCacheTtl = TimeSpan.FromMilliseconds(2500);
    private static readonly TimeSpan OwnerCacheWarmupInterval = TimeSpan.FromMilliseconds(500);

    private readonly object gate = new();
    private readonly List<HistoricalCombatData> historicalRecords = new();
    private readonly Dictionary<uint, OwnerCacheEntry> ownerCache = new();
    private readonly Dictionary<uint, uint> partyMemberHpCache = new();
    private readonly PluginConfiguration config;

    private EncounterSession currentEncounter = new();
    private DateTime lastOwnerWarmupUtc;
    private int selectedHistoricalRecordIndex = -1;

    public LocalStatsService(PluginConfiguration config)
    {
        this.config = config;
    }

    public CombatDataWrapper? CurrentCombatData { get; private set; }

    public CombatDataWrapper? DisplayCombatData { get; private set; }

    public IReadOnlyList<HistoricalCombatData> HistoricalRecords
    {
        get
        {
            lock (gate)
                return historicalRecords.ToArray();
        }
    }

    public int SelectedHistoricalRecordIndex
    {
        get
        {
            lock (gate)
                return selectedHistoricalRecordIndex;
        }
    }

    public string DataSourceText => "本地事件采集 / ACTX 统计口径";

    public string StatusText { get; private set; } = "等待战斗数据...";

    public void ClearHistory()
    {
        lock (gate)
        {
            historicalRecords.Clear();
            ownerCache.Clear();
            partyMemberHpCache.Clear();
            CurrentCombatData = null;
            DisplayCombatData = null;
            selectedHistoricalRecordIndex = -1;
            currentEncounter = new EncounterSession();
            StatusText = "等待战斗数据...";
        }
    }

    public void LoadTestData()
    {
        lock (gate)
        {
            var firstSnapshot = BuildRaidTestCombatData();
            var secondSnapshot = BuildTrialTestCombatData();
            var thirdSnapshot = BuildTrainingTestCombatData();
            UpsertHistoricalRecord(CreateHistoricalRecord(firstSnapshot));
            UpsertHistoricalRecord(CreateHistoricalRecord(secondSnapshot));
            UpsertHistoricalRecord(CreateHistoricalRecord(thirdSnapshot));

            ownerCache.Clear();
            partyMemberHpCache.Clear();
            CurrentCombatData = firstSnapshot;
            DisplayCombatData = firstSnapshot;
            selectedHistoricalRecordIndex = -1;
            currentEncounter = new EncounterSession
            {
                ZoneName = CurrentCombatData.Msg?.Encounter?.CurrentZoneName ?? "零式测试场",
            };
            StatusText = "已导入测试数据，可用于预览 DPS 统计面板。";
        }
    }

    public bool LoadHistoricalRecord(int index)
    {
        lock (gate)
        {
            if ((uint)index >= (uint)historicalRecords.Count)
                return false;

            selectedHistoricalRecordIndex = index;
            DisplayCombatData = historicalRecords[index].Snapshot;
            UpdateStatusText();
            return true;
        }
    }

    public void WarmOwnerCacheFromObjectTable()
    {
        var nowUtc = DateTime.UtcNow;
        if (nowUtc - lastOwnerWarmupUtc < OwnerCacheWarmupInterval)
            return;

        lastOwnerWarmupUtc = nowUtc;

        var entries = new List<(uint EntityId, uint OwnerId)>();
        foreach (var obj in DalamudApi.ObjectTable)
        {
            if (obj == null || obj.ObjectKind != ObjectKind.BattleNpc)
                continue;

            if (obj.EntityId is 0 or <= 0x40000000)
                continue;

            if (obj.OwnerId is 0 or InvalidActorId)
                continue;

            entries.Add((obj.EntityId, obj.OwnerId));
        }

        if (entries.Count == 0)
            return;

        lock (gate)
        {
            foreach (var (entityId, ownerId) in entries)
                ownerCache[entityId] = new OwnerCacheEntry(ownerId, nowUtc);
        }
    }

    public void RecordEncounterActivity(string zoneName, DateTime timeUtc)
    {
        lock (gate)
        {
            currentEncounter.ZoneName = NormalizeZoneName(zoneName);
            currentEncounter.MarkActivity(timeUtc);
        }
    }

    public void RecordDamage(
        uint sourceId,
        uint targetId,
        string actionName,
        long amount,
        bool critical,
        DateTime timeUtc,
        string zoneName)
    {
        if (amount <= 0)
            return;

        lock (gate)
        {
            currentEncounter.ZoneName = NormalizeZoneName(zoneName);

            if (TryResolveTrackedSource(sourceId, timeUtc, out var source))
                currentEncounter.RecordOutgoingDamage(source, actionName, amount, critical, timeUtc);

            if (TryGetTrackedActor(targetId, out var target))
                currentEncounter.RecordIncomingDamage(target, amount, timeUtc);
        }
    }

    public void RecordHeal(
        uint sourceId,
        uint targetId,
        long amount,
        bool critical,
        DateTime timeUtc,
        string zoneName)
    {
        if (amount <= 0)
            return;

        lock (gate)
        {
            currentEncounter.ZoneName = NormalizeZoneName(zoneName);

            if (TryResolveTrackedSource(sourceId, timeUtc, out var source))
                currentEncounter.RecordOutgoingHeal(source, amount, critical, timeUtc);

            if (TryGetTrackedActor(targetId, out var target))
                currentEncounter.RecordIncomingHeal(target, amount, timeUtc);
        }
    }

    public void RecordFailure(
        uint sourceId,
        bool isMiss,
        DateTime timeUtc,
        string zoneName)
    {
        lock (gate)
        {
            currentEncounter.ZoneName = NormalizeZoneName(zoneName);
            if (TryResolveTrackedSource(sourceId, timeUtc, out var source))
                currentEncounter.RecordFailedSwing(source, isMiss, timeUtc);
        }
    }

    public void RecordDeath(uint targetId, DateTime timeUtc, string zoneName)
    {
        lock (gate)
        {
            currentEncounter.ZoneName = NormalizeZoneName(zoneName);
            if (TryGetTrackedActor(targetId, out var target))
                currentEncounter.RecordDeath(target, timeUtc);
        }
    }

    public void Update(string zoneName, bool inCombat)
    {
        var nowUtc = DateTime.UtcNow;

        lock (gate)
        {
            currentEncounter.ZoneName = NormalizeZoneName(zoneName);
            PollPartyMemberDeaths(nowUtc, currentEncounter.ZoneName, inCombat);
            var allPartyMembersOutOfCombat = AreAllPartyMembersOutOfCombat(inCombat);

            if (currentEncounter.ShouldFinalize(
                    nowUtc,
                    TimeSpan.FromSeconds(config.EncounterTimeoutSeconds),
                    allPartyMembersOutOfCombat))
            {
                FinalizeEncounter();
            }
            else if (currentEncounter.Started)
            {
                CurrentCombatData = ActxSnapshotFormatter.Build(currentEncounter, isActive: true);
                DisplayCombatData = CurrentCombatData;
                selectedHistoricalRecordIndex = -1;
            }

            UpdateStatusText();
        }
    }

    public bool IsTrackedActor(uint actorId)
    {
        lock (gate)
            return TryGetTrackedActor(actorId, out _);
    }

    private void PollPartyMemberDeaths(DateTime nowUtc, string zoneName, bool inCombat)
    {
        var activePartyActorIds = new HashSet<uint>();

        foreach (var member in DalamudApi.PartyList)
        {
            var actorId = ResolvePartyMemberActorId(member);
            if (actorId is 0 or InvalidActorId)
                continue;

            activePartyActorIds.Add(actorId);
            var currentHp = member.CurrentHP;

            if (partyMemberHpCache.TryGetValue(actorId, out var previousHp)
                && previousHp > 0
                && currentHp == 0
                && (inCombat || currentEncounter.Started)
                && TryGetTrackedActor(actorId, out var actor))
            {
                currentEncounter.ZoneName = zoneName;
                currentEncounter.RecordDeath(actor, nowUtc);
            }

            partyMemberHpCache[actorId] = currentHp;
        }

        if (partyMemberHpCache.Count == 0)
            return;

        var staleActorIds = new List<uint>();
        foreach (var actorId in partyMemberHpCache.Keys)
        {
            if (!activePartyActorIds.Contains(actorId))
                staleActorIds.Add(actorId);
        }

        foreach (var actorId in staleActorIds)
            partyMemberHpCache.Remove(actorId);
    }

    private void FinalizeEncounter()
    {
        if (!currentEncounter.HasMeaningfulData)
        {
            currentEncounter = new EncounterSession
            {
                ZoneName = currentEncounter.ZoneName,
            };
            return;
        }

        CurrentCombatData = ActxSnapshotFormatter.Build(currentEncounter, isActive: false);
        DisplayCombatData = CurrentCombatData;
        selectedHistoricalRecordIndex = -1;

        var history = new HistoricalCombatData(
            currentEncounter.ZoneName,
            ActxSnapshotFormatter.FormatDuration(currentEncounter.DurationSeconds),
            CurrentCombatData);

        if (historicalRecords.Count == 0 || !HasSameHistoryIdentity(historicalRecords[^1], history))
            historicalRecords.Add(history);
        else
            historicalRecords[^1] = history;

        currentEncounter = new EncounterSession
        {
            ZoneName = history.ZoneName,
        };
    }

    private bool TryResolveTrackedSource(uint actorId, DateTime nowUtc, out TrackedActor actor)
    {
        actor = default;
        if (TryGetTrackedActor(actorId, out actor))
            return true;

        var resolvedActorId = ResolveOwner(actorId, nowUtc);
        if (resolvedActorId is 0 or InvalidActorId || resolvedActorId == actorId)
            return false;

        return TryGetTrackedActor(resolvedActorId, out actor);
    }

    private uint ResolveOwner(uint actorId, DateTime nowUtc)
    {
        if (actorId == 0 || actorId == InvalidActorId)
            return InvalidActorId;

        var obj = DalamudApi.ObjectTable.SearchByEntityId(actorId);
        if (obj != null && obj.OwnerId is > 0 and not InvalidActorId)
        {
            ownerCache[actorId] = new OwnerCacheEntry(obj.OwnerId, nowUtc);
            return obj.OwnerId;
        }

        if (ownerCache.TryGetValue(actorId, out var cached) && nowUtc - cached.UpdatedAtUtc <= OwnerCacheTtl)
            return cached.OwnerId;

        return InvalidActorId;
    }

    private static uint ResolvePartyMemberActorId(Dalamud.Game.ClientState.Party.IPartyMember member)
    {
        var gameObjectEntityId = member.GameObject?.EntityId ?? 0;
        if (gameObjectEntityId > 0 && gameObjectEntityId != InvalidActorId)
            return gameObjectEntityId;

        var objectId = member.ObjectId;
        if (objectId > 0 && objectId != InvalidActorId)
            return objectId;

        return 0;
    }

    private bool TryGetTrackedActor(uint actorId, out TrackedActor actor)
    {
        actor = default;
        if (actorId is 0 or InvalidActorId)
            return false;

        if (TryGetPartyMemberTrackedActor(actorId, out actor))
            return true;

        return TryGetLocalPlayerTrackedActor(actorId, out actor);
    }

    private static bool TryGetPartyMemberTrackedActor(uint actorId, out TrackedActor actor)
    {
        foreach (var member in DalamudApi.PartyList)
        {
            if (ResolvePartyMemberActorId(member) != actorId)
                continue;

            var name = member.Name.TextValue ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                break;

            var jobId = member.ClassJob.RowId;
            actor = new TrackedActor(actorId, name.Trim(), jobId, ResolveJobName(jobId));
            return true;
        }

        actor = default;
        return false;
    }

    private static bool TryGetLocalPlayerTrackedActor(uint actorId, out TrackedActor actor)
    {
        var localPlayerActorId = DalamudApi.GetLocalPlayerActorId();
        if (localPlayerActorId is 0 or InvalidActorId || localPlayerActorId != actorId)
        {
            actor = default;
            return false;
        }

        var name = DalamudApi.GetLocalPlayerName();
        if (string.IsNullOrWhiteSpace(name))
        {
            actor = default;
            return false;
        }

        var jobId = DalamudApi.GetLocalPlayerClassJobId();
        actor = new TrackedActor(actorId, name.Trim(), jobId, ResolveJobName(jobId));
        return true;
    }

    private static bool AreAllPartyMembersOutOfCombat(bool fallbackInCombat)
    {
        var hasUsablePartyState = false;

        foreach (var member in DalamudApi.PartyList)
        {
            if (member.GameObject is not ICharacter character)
                continue;

            hasUsablePartyState = true;
            if ((character.StatusFlags & StatusFlags.InCombat) != 0)
                return false;
        }

        return hasUsablePartyState
            ? true
            : !fallbackInCombat;
    }

    private static string NormalizeZoneName(string? zoneName)
        => string.IsNullOrWhiteSpace(zoneName) ? "未知区域" : zoneName.Trim();

    private void UpdateStatusText()
    {
        if (currentEncounter.Started)
        {
            StatusText = $"战斗中: {currentEncounter.ZoneName} {ActxSnapshotFormatter.FormatDuration(currentEncounter.DurationSeconds)}";
            return;
        }

        if ((uint)selectedHistoricalRecordIndex < (uint)historicalRecords.Count)
        {
            var selected = historicalRecords[selectedHistoricalRecordIndex];
            StatusText = $"查看历史记录: {selected.ZoneName} {selected.Duration}";
            return;
        }

        if (DisplayCombatData?.Msg?.Encounter != null)
        {
            StatusText = "等待新的战斗...";
            return;
        }

        StatusText = "等待战斗数据...";
    }

    private static bool HasSameHistoryIdentity(HistoricalCombatData left, HistoricalCombatData right)
        => string.Equals(left.ZoneName, right.ZoneName, StringComparison.Ordinal)
           && string.Equals(left.Duration, right.Duration, StringComparison.Ordinal);

    private void UpsertHistoricalRecord(HistoricalCombatData record)
    {
        for (var i = 0; i < historicalRecords.Count; i++)
        {
            if (!HasSameHistoryIdentity(historicalRecords[i], record))
                continue;

            historicalRecords[i] = record;
            return;
        }

        historicalRecords.Add(record);
    }

    private static HistoricalCombatData CreateHistoricalRecord(CombatDataWrapper snapshot)
    {
        var encounter = snapshot.Msg?.Encounter;
        return new HistoricalCombatData(
            encounter?.CurrentZoneName ?? "未知区域",
            encounter?.DurationText ?? "00:00",
            snapshot);
    }

    private static string ResolveJobName(uint jobId)
    {
        return jobId switch
        {
            1 => "剑术师",
            2 => "格斗家",
            3 => "斧术师",
            4 => "枪术师",
            5 => "弓箭手",
            6 => "幻术师",
            7 => "咒术师",
            19 => "骑士",
            20 => "武僧",
            21 => "战士",
            22 => "龙骑士",
            23 => "吟游诗人",
            24 => "白魔法师",
            25 => "黑魔法师",
            26 => "秘术师",
            27 => "召唤师",
            28 => "学者",
            29 => "双剑师",
            30 => "忍者",
            31 => "机工士",
            32 => "暗黑骑士",
            33 => "占星术士",
            34 => "武士",
            35 => "赤魔法师",
            37 => "绝枪战士",
            38 => "舞者",
            39 => "钐镰客",
            40 => "贤者",
            41 => "蝰蛇剑士",
            42 => "绘灵法师",
            43 => "青魔法师",
            _ => string.Empty,
        };
    }

    private static CombatDataWrapper BuildRaidTestCombatData()
    {
        var combatants = new Dictionary<string, Combatant>(StringComparer.Ordinal)
        {
            ["测试骑士#E0000001"] = CreateTestCombatant(
                name: "测试骑士",
                job: "骑士",
                damagePercentText: "19%",
                damageText: "98.00万",
                encDpsText: "2121",
                encHpsText: "145",
                dtpsText: "964",
                maxHitText: "圣灵-4.82万",
                hitsText: "168",
                critHitsText: "42",
                toHitText: "95.5",
                damageTakenText: "44.54万",
                deathsText: "0"),
            ["古·拉哈·提亚#E0000002"] = CreateTestCombatant(
                name: "古·拉哈·提亚",
                job: "武士",
                damagePercentText: "29%",
                damageText: "145.00万",
                encDpsText: "3139",
                encHpsText: "0",
                dtpsText: "221",
                maxHitText: "照破-9.68万",
                hitsText: "214",
                critHitsText: "63",
                toHitText: "97.3",
                damageTakenText: "10.21万",
                deathsText: "1"),
            ["阿莉塞#E0000003"] = CreateTestCombatant(
                name: "阿莉塞",
                job: "赤魔法师",
                damagePercentText: "26%",
                damageText: "131.00万",
                encDpsText: "2837",
                encHpsText: "38",
                dtpsText: "172",
                maxHitText: "决断-8.84万",
                hitsText: "196",
                critHitsText: "57",
                toHitText: "96.0",
                damageTakenText: "7.96万",
                deathsText: "0"),
            ["阿尔菲诺#E0000004"] = CreateTestCombatant(
                name: "阿尔菲诺",
                job: "贤者",
                damagePercentText: "8%",
                damageText: "42.00万",
                encDpsText: "909",
                encHpsText: "2514",
                dtpsText: "145",
                maxHitText: "注药-3.12万",
                hitsText: "88",
                critHitsText: "18",
                toHitText: "94.6",
                damageTakenText: "6.71万",
                deathsText: "0"),
            ["于里昂热#E0000005"] = CreateTestCombatant(
                name: "于里昂热",
                job: "占星术士",
                damagePercentText: "6%",
                damageText: "31.00万",
                encDpsText: "671",
                encHpsText: "1898",
                dtpsText: "131",
                maxHitText: "重力-2.69万",
                hitsText: "75",
                critHitsText: "14",
                toHitText: "93.8",
                damageTakenText: "6.08万",
                deathsText: "0"),
            ["机工支援兵#E0000006"] = CreateTestCombatant(
                name: "机工支援兵",
                job: "机工士",
                damagePercentText: "10%",
                damageText: "55.00万",
                encDpsText: "1190",
                encHpsText: "0",
                dtpsText: "84",
                maxHitText: "钻头-5.40万",
                hitsText: "102",
                critHitsText: "24",
                toHitText: "95.1",
                damageTakenText: "3.87万",
                deathsText: "0"),
        };

        return BuildTestCombatData(
            zoneName: "零式测试场",
            durationText: "07:42",
            damageText: "502.00万",
            encDpsText: "10867",
            hitsText: "843",
            hitFailedText: "31",
            critHitsText: "218",
            critHitPercentText: "25%",
            maxHitText: "古·拉哈·提亚-照破-9.68万",
            maxHitValueText: "古·拉哈·提亚-9.7万",
            damageTakenText: "79.37万",
            combatants: combatants);
    }

    private static CombatDataWrapper BuildTrialTestCombatData()
    {
        var combatants = new Dictionary<string, Combatant>(StringComparer.Ordinal)
        {
            ["测试骑士#E0000001"] = CreateTestCombatant(
                name: "测试骑士",
                job: "骑士",
                damagePercentText: "17%",
                damageText: "118.60万",
                encDpsText: "2274",
                encHpsText: "182",
                dtpsText: "1178",
                maxHitText: "赎罪剑-6.24万",
                hitsText: "181",
                critHitsText: "39",
                toHitText: "96.8",
                damageTakenText: "61.44万",
                deathsText: "0"),
            ["古·拉哈·提亚#E0000002"] = CreateTestCombatant(
                name: "古·拉哈·提亚",
                job: "武士",
                damagePercentText: "28%",
                damageText: "191.20万",
                encDpsText: "3666",
                encHpsText: "0",
                dtpsText: "264",
                maxHitText: "雪月花-12.46万",
                hitsText: "243",
                critHitsText: "79",
                toHitText: "97.5",
                damageTakenText: "13.78万",
                deathsText: "0"),
            ["阿莉塞#E0000003"] = CreateTestCombatant(
                name: "阿莉塞",
                job: "赤魔法师",
                damagePercentText: "24%",
                damageText: "163.80万",
                encDpsText: "3141",
                encHpsText: "41",
                dtpsText: "238",
                maxHitText: "决断-10.38万",
                hitsText: "221",
                critHitsText: "66",
                toHitText: "96.3",
                damageTakenText: "12.41万",
                deathsText: "1"),
            ["阿尔菲诺#E0000004"] = CreateTestCombatant(
                name: "阿尔菲诺",
                job: "贤者",
                damagePercentText: "11%",
                damageText: "77.50万",
                encDpsText: "1486",
                encHpsText: "2988",
                dtpsText: "189",
                maxHitText: "注药-4.18万",
                hitsText: "108",
                critHitsText: "23",
                toHitText: "95.6",
                damageTakenText: "9.89万",
                deathsText: "0"),
            ["于里昂热#E0000005"] = CreateTestCombatant(
                name: "于里昂热",
                job: "占星术士",
                damagePercentText: "8%",
                damageText: "56.30万",
                encDpsText: "1080",
                encHpsText: "2473",
                dtpsText: "153",
                maxHitText: "重力-3.31万",
                hitsText: "92",
                critHitsText: "19",
                toHitText: "95.1",
                damageTakenText: "8.02万",
                deathsText: "0"),
            ["机工支援兵#E0000006"] = CreateTestCombatant(
                name: "机工支援兵",
                job: "机工士",
                damagePercentText: "12%",
                damageText: "81.10万",
                encDpsText: "1555",
                encHpsText: "0",
                dtpsText: "141",
                maxHitText: "空气锚-5.77万",
                hitsText: "126",
                critHitsText: "31",
                toHitText: "96.0",
                damageTakenText: "7.37万",
                deathsText: "0"),
        };

        return BuildTestCombatData(
            zoneName: "极神兵破坏作战",
            durationText: "08:41",
            damageText: "688.50万",
            encDpsText: "13215",
            hitsText: "971",
            hitFailedText: "22",
            critHitsText: "257",
            critHitPercentText: "26%",
            maxHitText: "古·拉哈·提亚-雪月花-12.46万",
            maxHitValueText: "古·拉哈·提亚-12.5万",
            damageTakenText: "112.91万",
            combatants: combatants);
    }

    private static CombatDataWrapper BuildTrainingTestCombatData()
    {
        var combatants = new Dictionary<string, Combatant>(StringComparer.Ordinal)
        {
            ["测试骑士#E0000001"] = CreateTestCombatant(
                name: "测试骑士",
                job: "骑士",
                damagePercentText: "61%",
                damageText: "64.80万",
                encDpsText: "3375",
                encHpsText: "0",
                dtpsText: "201",
                maxHitText: "圣灵-7.21万",
                hitsText: "116",
                critHitsText: "31",
                toHitText: "98.2",
                damageTakenText: "3.86万",
                deathsText: "0"),
            ["机工支援兵#E0000006"] = CreateTestCombatant(
                name: "机工支援兵",
                job: "机工士",
                damagePercentText: "26%",
                damageText: "27.40万",
                encDpsText: "1427",
                encHpsText: "0",
                dtpsText: "96",
                maxHitText: "钻头-4.88万",
                hitsText: "74",
                critHitsText: "18",
                toHitText: "97.0",
                damageTakenText: "1.84万",
                deathsText: "0"),
            ["阿尔菲诺#E0000004"] = CreateTestCombatant(
                name: "阿尔菲诺",
                job: "贤者",
                damagePercentText: "13%",
                damageText: "13.60万",
                encDpsText: "708",
                encHpsText: "642",
                dtpsText: "318",
                maxHitText: "注药-2.42万",
                hitsText: "41",
                critHitsText: "9",
                toHitText: "95.3",
                damageTakenText: "6.12万",
                deathsText: "0"),
        };

        return BuildTestCombatData(
            zoneName: "木人演练场",
            durationText: "03:12",
            damageText: "105.80万",
            encDpsText: "5510",
            hitsText: "231",
            hitFailedText: "6",
            critHitsText: "58",
            critHitPercentText: "25%",
            maxHitText: "测试骑士-圣灵-7.21万",
            maxHitValueText: "测试骑士-7.2万",
            damageTakenText: "11.82万",
            combatants: combatants);
    }

    private static CombatDataWrapper BuildTestCombatData(
        string zoneName,
        string durationText,
        string damageText,
        string encDpsText,
        string hitsText,
        string hitFailedText,
        string critHitsText,
        string critHitPercentText,
        string maxHitText,
        string maxHitValueText,
        string damageTakenText,
        Dictionary<string, Combatant> combatants)
    {
        return new CombatDataWrapper
        {
            Type = "broadcast",
            MsgType = "CombatData",
            Msg = new CombatData
            {
                Type = "CombatData",
                IsActive = "false",
                Encounter = new Encounter
                {
                    CurrentZoneName = zoneName,
                    DurationText = durationText,
                    DamageText = damageText,
                    EncDpsText = encDpsText,
                    HitsText = hitsText,
                    HitFailedText = hitFailedText,
                    CritHitsText = critHitsText,
                    CritHitPercentText = critHitPercentText,
                    MaxHitText = maxHitText,
                    MaxHitValueText = maxHitValueText,
                    DamageTakenText = damageTakenText,
                },
                Combatant = combatants,
            },
        };
    }

    private static Combatant CreateTestCombatant(
        string name,
        string job,
        string damagePercentText,
        string damageText,
        string encDpsText,
        string encHpsText,
        string dtpsText,
        string maxHitText,
        string hitsText,
        string critHitsText,
        string toHitText,
        string damageTakenText,
        string deathsText)
    {
        return new Combatant
        {
            Name = name,
            Job = job,
            DamagePercentText = damagePercentText,
            DamageText = damageText,
            EncDpsText = encDpsText,
            EncHpsText = encHpsText,
            DtpsText = dtpsText,
            MaxHitText = maxHitText,
            HitsText = hitsText,
            CritHitsText = critHitsText,
            ToHitText = toHitText,
            DamageTakenText = damageTakenText,
            BlockPctText = "--",
            ParryPctText = "--",
            DeathsText = deathsText,
        };
    }

    private readonly record struct OwnerCacheEntry(uint OwnerId, DateTime UpdatedAtUtc);

    private readonly record struct TrackedActor(uint ActorId, string Name, uint JobId, string JobName);

    private sealed class EncounterSession
    {
        private readonly Dictionary<uint, CombatantSession> combatants = new();

        public DateTime StartUtc { get; private set; }

        public DateTime LastEventUtc { get; private set; }

        public DateTime EndUtc { get; private set; }

        public string ZoneName { get; set; } = "未知区域";

        public bool Started => StartUtc != default;

        public bool HasMeaningfulData => combatants.Values.Any(static combatant =>
            combatant.Damage > 0
            || combatant.Healed > 0
            || combatant.DamageTaken > 0
            || combatant.HealsTaken > 0
            || combatant.Deaths > 0
            || combatant.Swings > 0
            || combatant.Heals > 0);

        public IReadOnlyCollection<CombatantSession> Combatants => combatants.Values;

        public double DurationSeconds
        {
            get
            {
                if (!Started)
                    return 1d;

                var endUtc = EndUtc == default ? LastEventUtc : EndUtc;
                var seconds = (endUtc - StartUtc).TotalSeconds;
                return seconds < 1d ? 1d : seconds;
            }
        }

        public void MarkActivity(DateTime timeUtc)
        {
            if (!Started)
                StartUtc = timeUtc;

            if (LastEventUtc < timeUtc)
                LastEventUtc = timeUtc;

            if (EndUtc < timeUtc)
                EndUtc = timeUtc;
        }

        public void RecordOutgoingDamage(
            TrackedActor source,
            string actionName,
            long amount,
            bool critical,
            DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(source).NoteOutgoingDamage(actionName, amount, critical, timeUtc);
        }

        public void RecordIncomingDamage(TrackedActor target, long amount, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(target).NoteIncomingDamage(amount, timeUtc);
        }

        public void RecordOutgoingHeal(TrackedActor source, long amount, bool critical, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(source).NoteOutgoingHeal(amount, critical, timeUtc);
        }

        public void RecordIncomingHeal(TrackedActor target, long amount, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(target).NoteIncomingHeal(amount, timeUtc);
        }

        public void RecordFailedSwing(TrackedActor source, bool isMiss, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(source).NoteFailedSwing(isMiss, timeUtc);
        }

        public void RecordDeath(TrackedActor target, DateTime timeUtc)
        {
            MarkActivity(timeUtc);
            EnsureCombatant(target).NoteDeath(timeUtc);
        }

        public bool ShouldFinalize(DateTime nowUtc, TimeSpan timeout, bool allPartyMembersOutOfCombat)
            => Started
               && allPartyMembersOutOfCombat
               && nowUtc - LastEventUtc > timeout;

        private CombatantSession EnsureCombatant(TrackedActor actor)
        {
            if (combatants.TryGetValue(actor.ActorId, out var existing))
            {
                existing.RefreshIdentity(actor);
                return existing;
            }

            var created = new CombatantSession(actor);
            combatants[actor.ActorId] = created;
            return created;
        }
    }

    private sealed class CombatantSession
    {
        public CombatantSession(TrackedActor actor)
        {
            ActorId = actor.ActorId;
            Name = actor.Name;
            JobId = actor.JobId;
            JobName = actor.JobName;
        }

        public uint ActorId { get; }

        public string Name { get; private set; }

        public uint JobId { get; private set; }

        public string JobName { get; private set; }

        public long Damage { get; private set; }

        public long Healed { get; private set; }

        public long DamageTaken { get; private set; }

        public long HealsTaken { get; private set; }

        public int Swings { get; private set; }

        public int Hits { get; private set; }

        public int CritHits { get; private set; }

        public int Misses { get; private set; }

        public int HitFailed { get; private set; }

        public int Heals { get; private set; }

        public int CritHeals { get; private set; }

        public int Deaths { get; private set; }

        public DateTime FirstEventUtc { get; private set; }

        public DateTime LastEventUtc { get; private set; }

        public long MaxHitValue { get; private set; }

        public string MaxHitActionName { get; private set; } = string.Empty;

        public double PersonalDurationSeconds
        {
            get
            {
                if (FirstEventUtc == default || LastEventUtc <= FirstEventUtc)
                    return 1d;

                var seconds = (LastEventUtc - FirstEventUtc).TotalSeconds;
                return seconds < 1d ? 1d : seconds;
            }
        }

        public void RefreshIdentity(TrackedActor actor)
        {
            if (!string.IsNullOrWhiteSpace(actor.Name))
                Name = actor.Name;

            if (actor.JobId != 0)
                JobId = actor.JobId;

            if (!string.IsNullOrWhiteSpace(actor.JobName))
                JobName = actor.JobName;
        }

        public void NoteOutgoingDamage(string actionName, long amount, bool critical, DateTime timeUtc)
        {
            Touch(timeUtc);
            Damage += amount;
            Swings++;
            Hits++;
            if (critical)
                CritHits++;

            if (amount > MaxHitValue)
            {
                MaxHitValue = amount;
                MaxHitActionName = actionName;
            }
        }

        public void NoteIncomingDamage(long amount, DateTime timeUtc)
        {
            Touch(timeUtc);
            DamageTaken += amount;
        }

        public void NoteOutgoingHeal(long amount, bool critical, DateTime timeUtc)
        {
            Touch(timeUtc);
            Healed += amount;
            Heals++;
            if (critical)
                CritHeals++;
        }

        public void NoteIncomingHeal(long amount, DateTime timeUtc)
        {
            Touch(timeUtc);
            HealsTaken += amount;
        }

        public void NoteFailedSwing(bool isMiss, DateTime timeUtc)
        {
            Touch(timeUtc);
            Swings++;
            if (isMiss)
                Misses++;
            else
                HitFailed++;
        }

        public void NoteDeath(DateTime timeUtc)
        {
            Touch(timeUtc);
            Deaths++;
        }

        private void Touch(DateTime timeUtc)
        {
            if (FirstEventUtc == default || timeUtc < FirstEventUtc)
                FirstEventUtc = timeUtc;

            if (LastEventUtc < timeUtc)
                LastEventUtc = timeUtc;
        }
    }

    private static class ActxSnapshotFormatter
    {
        public static CombatDataWrapper Build(EncounterSession encounter, bool isActive)
        {
            var durationSeconds = encounter.DurationSeconds;
            var combatants = encounter.Combatants
                .OrderByDescending(combatant => combatant.Damage / durationSeconds)
                .ThenBy(combatant => combatant.Name, StringComparer.Ordinal)
                .ToList();

            var totalDamage = combatants.Sum(static combatant => combatant.Damage);
            var totalHealing = combatants.Sum(static combatant => combatant.Healed);
            var totalDamageTaken = combatants.Sum(static combatant => combatant.DamageTaken);
            var totalHits = combatants.Sum(static combatant => combatant.Hits);
            var totalHitFailed = combatants.Sum(static combatant => combatant.HitFailed);
            var totalCritHits = combatants.Sum(static combatant => combatant.CritHits);

            var maxHitCombatant = combatants
                .Where(static combatant => combatant.MaxHitValue > 0)
                .OrderByDescending(static combatant => combatant.MaxHitValue)
                .ThenBy(combatant => combatant.Name, StringComparer.Ordinal)
                .FirstOrDefault();

            var encounterMaxHit = "--";
            var encounterShortMaxHit = "--";
            if (maxHitCombatant != null)
            {
                var actionName = SafeActionName(maxHitCombatant.MaxHitActionName);
                encounterMaxHit =
                    $"{maxHitCombatant.Name}-{actionName}-{CreateDamageString(maxHitCombatant.MaxHitValue, useSuffix: true, useDecimals: true)}";
                encounterShortMaxHit =
                    $"{maxHitCombatant.Name}-{CreateDamageString(maxHitCombatant.MaxHitValue, useSuffix: true, useDecimals: false)}";
            }

            var combatantPayload = new Dictionary<string, Combatant>(combatants.Count, StringComparer.Ordinal);
            foreach (var combatant in combatants)
            {
                var damagePercent = totalDamage > 0
                    ? $"{(int)(combatant.Damage / (float)totalDamage * 100f)}%"
                    : "--";

                var encDps = combatant.Damage / durationSeconds;
                var encHps = combatant.Healed / durationSeconds;
                var dtps = combatant.DamageTaken / durationSeconds;
                var toHit = combatant.Swings > 0
                    ? combatant.Hits / (float)combatant.Swings * 100f
                    : 0f;

                combatantPayload[$"{combatant.Name}#{combatant.ActorId:X8}"] = new Combatant
                {
                    Name = combatant.Name,
                    Job = string.IsNullOrWhiteSpace(combatant.JobName) ? "-" : combatant.JobName,
                    DamagePercentText = damagePercent,
                    DamageText = CreateDamageString(combatant.Damage, useSuffix: true, useDecimals: true),
                    EncDpsText = encDps.ToString("0", CultureInfo.InvariantCulture),
                    EncHpsText = encHps.ToString("0", CultureInfo.InvariantCulture),
                    DtpsText = dtps.ToString("0", CultureInfo.InvariantCulture),
                    MaxHitText = combatant.MaxHitValue > 0
                        ? $"{SafeActionName(combatant.MaxHitActionName)}-{CreateDamageString(combatant.MaxHitValue, useSuffix: true, useDecimals: true)}"
                        : "--",
                    HitsText = combatant.Hits.ToString(CultureInfo.InvariantCulture),
                    CritHitsText = combatant.CritHits.ToString(CultureInfo.InvariantCulture),
                    ToHitText = toHit.ToString("F", CultureInfo.InvariantCulture),
                    DamageTakenText = CreateDamageString(combatant.DamageTaken, useSuffix: true, useDecimals: true),
                    BlockPctText = "--",
                    ParryPctText = "--",
                    DeathsText = combatant.Deaths.ToString(CultureInfo.InvariantCulture),
                };
            }

            return new CombatDataWrapper
            {
                Type = "broadcast",
                MsgType = "CombatData",
                Msg = new CombatData
                {
                    Type = "CombatData",
                    IsActive = isActive ? "true" : "false",
                    Encounter = new Encounter
                    {
                        CurrentZoneName = encounter.ZoneName,
                        DurationText = FormatDuration(durationSeconds),
                        DamageText = CreateDamageString(totalDamage, useSuffix: true, useDecimals: true),
                        EncDpsText = (totalDamage / durationSeconds).ToString("0", CultureInfo.InvariantCulture),
                        HitsText = totalHits.ToString(CultureInfo.InvariantCulture),
                        HitFailedText = totalHitFailed.ToString(CultureInfo.InvariantCulture),
                        CritHitsText = totalCritHits.ToString(CultureInfo.InvariantCulture),
                        CritHitPercentText = totalHits > 0
                            ? $"{(int)(totalCritHits / (float)totalHits * 100f)}%"
                            : "0%",
                        MaxHitText = encounterMaxHit,
                        MaxHitValueText = encounterShortMaxHit,
                        DamageTakenText = CreateDamageString(totalDamageTaken, useSuffix: true, useDecimals: true),
                    },
                    Combatant = combatantPayload,
                },
            };
        }

        public static string FormatDuration(double durationSeconds)
        {
            var wholeSeconds = durationSeconds < 1d
                ? 1
                : (int)Math.Round(durationSeconds, MidpointRounding.AwayFromZero);
            var span = TimeSpan.FromSeconds(wholeSeconds);
            return span.TotalHours >= 1d
                ? span.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
                : span.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        }

        private static string SafeActionName(string? actionName)
            => string.IsNullOrWhiteSpace(actionName) ? "未知技能" : actionName;

        private static string CreateDamageString(long damage, bool useSuffix, bool useDecimals)
        {
            const long trillion = 1_000_000_000_000L;
            const long hundredMillion = 100_000_000L;
            const long tenThousand = 10_000L;

            if (!useSuffix)
                return damage.ToString(CultureInfo.InvariantCulture);

            var abs = Math.Abs(damage);
            if (abs >= trillion)
                return FormatChineseDamageUnit(damage, trillion, "兆", useDecimals ? "0.00" : "0.#");

            if (abs >= hundredMillion)
                return FormatChineseDamageUnit(damage, hundredMillion, "亿", useDecimals ? "0.00" : "0.#");

            if (abs >= tenThousand)
                return FormatChineseDamageUnit(damage, tenThousand, "万", useDecimals ? "0.00" : "0.#");

            return damage.ToString(CultureInfo.InvariantCulture);
        }

        private static string FormatChineseDamageUnit(long value, long unitBase, string unit, string numericFormat)
            => (value / (double)unitBase).ToString(numericFormat, CultureInfo.InvariantCulture) + unit;
    }
}
