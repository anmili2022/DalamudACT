using System;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace DalamudACT;

public sealed partial class ACT
{
    private unsafe void InstallHooks()
    {
        try
        {
            receiveAbilityHook = DalamudApi.Interop.HookFromSignature<ReceiveAbilityDelegate>(
                ActionEffectHandler.Addresses.Receive.String,
                ReceiveAbilityEffect);
            receiveAbilityHook.Enable();
            LogHelper.Info("插件", "已安装 ActionEffect Hook，用于实时战斗统计。");
        }
        catch (Exception ex)
        {
            LogHelper.Error("插件", ex, "安装 ActionEffect Hook 失败。插件会继续加载，但实时 DPS 数据将不可用。");
        }

        if (ShouldInstallActorControlHook)
        {
            try
            {
                actorControlHook = CreateActorControlHook();
                actorControlHook.Enable();
                LogHelper.Info("插件", "已安装 ActorControl Hook，用于 debug 战斗记录中的特效标记采集。");
            }
            catch (Exception ex)
            {
                LogHelper.Warning("插件", ex, "安装 ActorControl Hook 失败。debug 战斗记录的特效标记采集将不可用。");
            }
        }
        else
        {
            actorControlHook = null;
            LogHelper.Warning(
                "插件",
                "ActorControl Hook 已因启动崩溃风险暂时禁用；debug 战斗记录中的友方特效标记 Hook 采集暂不可用。ActionEffect 主统计、BOSS 读条轮询、BUFF/debuff 与 DoT 诊断不受影响。");
        }

        LogHelper.Warning("插件", "Cast Hook 当前按稳定性策略禁用；BOSS 读条使用 Framework 轮询 IBattleChara.IsCasting 采集。ActionEffect 主统计会独立安装；ActorControl 特效标记采集当前已按稳定性策略禁用。");
    }

    private void DisposeHooks()
    {
        try
        {
            receiveAbilityHook?.Disable();
            if (receiveAbilityHook != null)
                LogHelper.Debug("插件", "已关闭 ActionEffect Hook。");

            actorControlHook?.Disable();
            if (actorControlHook != null)
                LogHelper.Debug("插件", "已关闭 ActorControl Hook。");
        }
        catch
        {
            // Ignore hook shutdown failures while disposing.
        }

        receiveAbilityHook?.Dispose();
        actorControlHook?.Dispose();
    }
}
