using System.Collections.Generic;
using System.Linq;

namespace DalamudACT;

public sealed record PartySkillEntry(
    uint ActionId,
    string Name,
    SkillCategory Category,
    float CooldownSeconds,
    float ActiveDurationSeconds = 0f,
    params uint[] TriggerActionIds);

public enum SkillCategory
{
    Mitigation,
    RaidBuff,
}

internal static class PartySkillCatalog
{
    private static readonly Dictionary<uint, List<PartySkillEntry>> SkillsByJob = new();

    static PartySkillCatalog()
    {
        RegisterTankSkills();
        RegisterHealerSkills();
        RegisterMeleeSkills();
        RegisterRangedSkills();
        RegisterCasterSkills();
    }

    public static List<PartySkillEntry> GetSkillsForJob(uint classJobId, PartyMonitorJobConfig? jobConfig = null)
    {
        var skills = new List<PartySkillEntry>();
        if (SkillsByJob.TryGetValue(classJobId, out var catalogSkills))
            skills.AddRange(catalogSkills);
        if (jobConfig?.CustomSkills != null)
        {
            foreach (var (actionId, custom) in jobConfig.CustomSkills)
                skills.Add(new PartySkillEntry(actionId, custom.Name, custom.Category, custom.CooldownSeconds));
        }
        return skills;
    }

    public static PartySkillEntry? FindSkill(uint actionId)
    {
        foreach (var kv in SkillsByJob)
        {
            foreach (var skill in kv.Value)
            {
                if (skill.ActionId == actionId || skill.TriggerActionIds.Contains(actionId))
                    return skill;
            }
        }
        return null;
    }

    public static HashSet<uint> GetAllActionIds()
    {
        var ids = new HashSet<uint>();
        foreach (var kv in SkillsByJob)
        {
            foreach (var skill in kv.Value)
            {
                ids.Add(skill.ActionId);
                foreach (var triggerId in skill.TriggerActionIds)
                    ids.Add(triggerId);
            }
        }
        return ids;
    }

    public static bool IsDefaultEnabled(uint actionId)
        => actionId is not (
            3542 or 7382 or 17 or 36920 or
            44 or 40 or
            3634 or 36927 or
            16148 or 16161 or 16151 or 36935 or
            25861 or 7432 or 3569 or 7433 or 140 or
            188 or 3585 or 16538 or 16551 or 7434 or 16542 or
            25881 or 3613 or
            24305 or 24298 or 24302 or 24318 or 24301 or 24300 or 24303 or
            22411 or
            7484 or 16485 or
            7408 or
            16015 or
            34685 or
            36976);

    private static void Register(uint classJobId, PartySkillEntry skill)
    {
        if (!SkillsByJob.TryGetValue(classJobId, out var list))
        {
            list = new List<PartySkillEntry>();
            SkillsByJob[classJobId] = list;
        }
        list.Add(skill);
    }

    private static void RegisterTankSkills()
    {
        // 骑士
        Register(19, new(3542, "盾阵", SkillCategory.Mitigation, 15f, 6f, 3542));
        Register(19, new(7382, "干预", SkillCategory.Mitigation, 10f, 10f, 7382));
        Register(19, new(36920, "极致防御", SkillCategory.Mitigation, 120f, 15f, 17, 36920));
        Register(19, new(3540, "圣光幕帘", SkillCategory.Mitigation, 90f, 30f, 3540));
        Register(19, new(7385, "武装戍卫", SkillCategory.Mitigation, 120f, 18f, 7385));
        Register(19, new(30, "神圣领域", SkillCategory.Mitigation, 420f, 10f, 30));
        Register(19, new(7531, "铁壁", SkillCategory.Mitigation, 90f, 20f, 7531));
        Register(19, new(7535, "雪仇", SkillCategory.Mitigation, 60f, 15f, 7535));

        // 战士
        Register(21, new(43, "死斗", SkillCategory.Mitigation, 240f, 10f, 43));
        Register(21, new(36923, "屠戮", SkillCategory.Mitigation, 120f, 15f, 44, 36923));
        Register(21, new(25751, "原初的血气", SkillCategory.Mitigation, 25f, 6f, 3551, 16464, 25751));
        Register(21, new(7388, "摆脱", SkillCategory.Mitigation, 90f, 10f, 7388));
        Register(21, new(40, "战栗", SkillCategory.Mitigation, 90f, 10f, 40));
        Register(21, new(7531, "铁壁", SkillCategory.Mitigation, 90f, 20f, 7531));
        Register(21, new(7535, "雪仇", SkillCategory.Mitigation, 60f, 15f, 7535));

        // 暗黑骑士
        Register(32, new(3638, "行尸走肉", SkillCategory.Mitigation, 300f, 10f, 3638));
        Register(32, new(7393, "至黑之夜", SkillCategory.Mitigation, 15f, 7f, 7393));
        Register(32, new(25754, "献奉", SkillCategory.Mitigation, 60f, 10f, 25754));
        Register(32, new(3634, "弃明投暗", SkillCategory.Mitigation, 120f, 15f, 3634));
        Register(32, new(16471, "暗黑布道", SkillCategory.Mitigation, 90f, 15f, 16471));
        Register(32, new(36927, "暗影卫", SkillCategory.Mitigation, 120f, 15f, 3636, 36927));
        Register(32, new(7531, "铁壁", SkillCategory.Mitigation, 90f, 20f, 7531));
        Register(32, new(7535, "雪仇", SkillCategory.Mitigation, 60f, 15f, 7535));

        // 绝枪战士
        Register(37, new(16152, "超火流星", SkillCategory.Mitigation, 360f, 10f, 16152));
        Register(37, new(36935, "大星云", SkillCategory.Mitigation, 120f, 15f, 16148, 36935));
        Register(37, new(25758, "刚玉之心", SkillCategory.Mitigation, 25f, 8f, 16161, 25758));
        Register(37, new(16160, "光之心", SkillCategory.Mitigation, 90f, 15f, 16160));
        Register(37, new(7531, "铁壁", SkillCategory.Mitigation, 90f, 20f, 7531));
        Register(37, new(7535, "雪仇", SkillCategory.Mitigation, 60f, 15f, 7535));
    }

    private static void RegisterHealerSkills()
    {
        // 白魔法师
        Register(24, new(25861, "水流幕", SkillCategory.Mitigation, 60f, 8f, 25861));
        Register(24, new(7432, "神祝祷", SkillCategory.Mitigation, 30f, 15f, 7432));
        Register(24, new(3569, "庇护所", SkillCategory.Mitigation, 90f, 15f, 3569));
        Register(24, new(7433, "全大赦", SkillCategory.Mitigation, 60f, 15f, 7433));
        Register(24, new(25862, "礼仪之铃", SkillCategory.Mitigation, 180f, 20f, 25862));
        Register(24, new(16536, "节制", SkillCategory.Mitigation, 120f, 20f, 16536));
        Register(24, new(140, "天赐祝福", SkillCategory.Mitigation, 180f, 0f, 140));

        // 学者
        Register(28, new(188, "野战治疗阵", SkillCategory.Mitigation, 120f, 15f, 188));
        Register(28, new(16545, "炽天召唤", SkillCategory.Mitigation, 120f, 20f, 16545));
        Register(28, new(37014, "炽天附体", SkillCategory.Mitigation, 180f, 20f, 37014));
        Register(28, new(25868, "疾风怒涛之计", SkillCategory.Mitigation, 120f, 20f, 25868));
        Register(28, new(25867, "生命回生法", SkillCategory.Mitigation, 60f, 10f, 25867));
        Register(28, new(3585, "展开战术", SkillCategory.Mitigation, 120f, 30f, 3585));
        Register(28, new(16551, "炽天的幻光", SkillCategory.Mitigation, 90f, 15f, 16538, 16551));
        Register(28, new(7434, "深谋远虑之策", SkillCategory.RaidBuff, 90f, 15f, 7434));
        Register(28, new(7436, "连环计", SkillCategory.RaidBuff, 120f, 20f, 7436));
        Register(28, new(16542, "秘策", SkillCategory.RaidBuff, 90f, 15f, 16542));

        // 占星术士
        Register(33, new(25881, "星位合图", SkillCategory.Mitigation, 120f, 10f, 25881));
        Register(33, new(3613, "命运之轮", SkillCategory.Mitigation, 60f, 15f, 3613));
        Register(33, new(16559, "中间学派", SkillCategory.Mitigation, 120f, 20f, 16559));
        Register(33, new(25874, "大宇宙", SkillCategory.Mitigation, 180f, 15f, 25874));
        Register(33, new(7439, "地星", SkillCategory.Mitigation, 60f, 20f, 7439));
        Register(33, new(16552, "占卜", SkillCategory.RaidBuff, 120f, 15f, 16552));

        // 贤者
        Register(40, new(24311, "泛输血", SkillCategory.Mitigation, 120f, 10f, 24311));
        Register(40, new(24305, "输血", SkillCategory.Mitigation, 45f, 7f, 24305));
        Register(40, new(24298, "坚角清汁", SkillCategory.Mitigation, 120f, 10f, 24298));
        Register(40, new(24302, "自生", SkillCategory.Mitigation, 60f, 15f, 24302));
        Register(40, new(24318, "魂灵风息", SkillCategory.Mitigation, 120f, 15f, 24318));
        Register(40, new(24301, "消化", SkillCategory.Mitigation, 30f, 0f, 24301));
        Register(40, new(24300, "活化", SkillCategory.Mitigation, 60f, 15f, 24300));
        Register(40, new(24303, "白牛清汁", SkillCategory.Mitigation, 45f, 7f, 24303));
        Register(40, new(24310, "整体论", SkillCategory.Mitigation, 120f, 20f, 24310));
        Register(40, new(37035, "智慧之爱", SkillCategory.Mitigation, 180f, 20f, 37035));
    }

    private static void RegisterMeleeSkills()
    {
        // 武僧
        Register(20, new(7396, "义结金兰", SkillCategory.RaidBuff, 120f, 20f, 7396));
        Register(20, new(7549, "牵制", SkillCategory.Mitigation, 90f, 10f, 7549));

        // 龙骑士
        Register(22, new(3557, "战斗连祷", SkillCategory.RaidBuff, 120f, 20f, 3557));
        Register(22, new(7549, "牵制", SkillCategory.Mitigation, 90f, 10f, 7549));

        // 忍者
        Register(30, new(36957, "介毒之术", SkillCategory.RaidBuff, 120f, 20f, 2248, 36957));
        Register(30, new(22411, "残影", SkillCategory.Mitigation, 120f));
        Register(30, new(7549, "牵制", SkillCategory.Mitigation, 90f, 15f, 7549));

        // 武士
        Register(34, new(7495, "叶隐", SkillCategory.RaidBuff, 120f));
        Register(34, new(16482, "意气冲天", SkillCategory.RaidBuff, 120f));
        Register(34, new(7549, "牵制", SkillCategory.Mitigation, 90f, 15f, 7549));

        // 钐镰客
        Register(39, new(24405, "神秘环", SkillCategory.RaidBuff, 120f, 20f, 24405));
        Register(39, new(24404, "神秘纹", SkillCategory.Mitigation, 30f, 0f, 24404));
        Register(39, new(7549, "牵制", SkillCategory.Mitigation, 90f, 15f, 7549));

        // 蝰蛇剑士
        Register(41, new(34647, "蛇灵气", SkillCategory.RaidBuff, 120f));
        Register(41, new(7549, "牵制", SkillCategory.Mitigation, 90f, 15f, 7549));
    }

    private static void RegisterRangedSkills()
    {
        // 吟游诗人
        Register(23, new(118, "战斗之声", SkillCategory.RaidBuff, 120f, 20f, 118));
        Register(23, new(7405, "行吟", SkillCategory.Mitigation, 90f, 15f, 7405));
        Register(23, new(7408, "大地神", SkillCategory.Mitigation, 120f, 15f, 7408));
        Register(23, new(25785, "光明神的最终乐章", SkillCategory.RaidBuff, 110f, 20f, 25785));

        // 机工士
        Register(31, new(2887, "武装解除", SkillCategory.Mitigation, 120f, 10f, 2887));
        Register(31, new(16889, "策动", SkillCategory.Mitigation, 90f, 15f, 16889));

        // 舞者
        Register(38, new(16012, "防守之桑巴", SkillCategory.Mitigation, 90f));
        Register(38, new(16196, "技巧舞步结束", SkillCategory.RaidBuff, 115f, 20f, 16196));
        Register(38, new(16011, "进攻之探戈", SkillCategory.RaidBuff, 120f, 20f, 16011));
        Register(38, new(16014, "即兴表演", SkillCategory.Mitigation, 120f, 15f, 16014));
    }

    private static void RegisterCasterSkills()
    {
        // 黑魔法师
        Register(25, new(7560, "昏乱", SkillCategory.Mitigation, 90f));

        // 召唤师
        Register(27, new(25801, "灼热之光", SkillCategory.RaidBuff, 120f, 30f, 25801));
        Register(27, new(7560, "昏乱", SkillCategory.Mitigation, 90f, 15f, 7560));

        // 赤魔法师
        Register(35, new(7520, "鼓励", SkillCategory.RaidBuff, 120f, 20f, 7520));
        Register(35, new(25857, "抗死", SkillCategory.Mitigation, 120f, 10f, 25857));
        Register(35, new(7560, "昏乱", SkillCategory.Mitigation, 90f, 15f, 7560));

        // 绘灵法师
        Register(42, new(34675, "星空构想", SkillCategory.RaidBuff, 120f, 20f, 34675));
        Register(42, new(34685, "坦培拉涂层", SkillCategory.Mitigation, 120f, 0f, 34685));
        Register(42, new(7560, "昏乱", SkillCategory.Mitigation, 90f, 15f, 7560));
    }
}
