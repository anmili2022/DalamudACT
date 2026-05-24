using System;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using ExcelAction = Lumina.Excel.Sheets.Action;

namespace DalamudACT;

/// <summary>
/// 本地战斗统计核心，负责跟踪队伍成员、聚合伤害/治疗/承伤、生成历史记录和历史预览。
/// 相关参考：
/// - https://dalamud.dev/
/// - https://dalamud.dev/api/
/// 调整 ObjectTable、PartyList、BuddyList、BattleChara / GameObject 相关访问逻辑前，先对照 Dalamud 文档。
/// </summary>
internal sealed partial class LocalStatsService
{
    private const uint InvalidActorId = 0xE0000000;
    private readonly object gate = new();
    private readonly PluginConfiguration config;
    private readonly ExcelSheet<ExcelAction>? actionSheet;
    private readonly ExcelSheet<ActionTransient>? actionTransientSheet;

    public LocalStatsService(PluginConfiguration config)
    {
        this.config = config;

        try
        {
            actionSheet = DalamudApi.GameData.GetExcelSheet<ExcelAction>();
        }
        catch (Exception ex)
        {
            LogHelper.Debug("统计", ex, "读取 Action 表失败，debug 战斗记录中的读条技能名称可能只能显示 ID。");
        }

        try
        {
            actionTransientSheet = DalamudApi.GameData.GetExcelSheet<ActionTransient>();
        }
        catch (Exception ex)
        {
            LogHelper.Debug("统计", ex, "读取 ActionTransient 表失败，部分低级 DoT 可能无法自动解析威力。");
        }
    }

}
