using System;
namespace DalamudACT;

public sealed partial class ACT
{
    private void OnDutyWiped(object? sender, ushort eventHandlerId)
    {
        try
        {
            if (DalamudApi.ClientState.IsPvP)
                return;

            monitorService.ResetSkillCooldowns(DateTime.UtcNow);
            LogHelper.Info("队友监控", $"检测到团灭，已重置队友技能冷却。eventHandlerId={eventHandlerId}。");
        }
        catch (Exception ex)
        {
            LogHelper.Warning("队友监控", ex, "处理团灭技能重置失败。");
        }
    }
}
