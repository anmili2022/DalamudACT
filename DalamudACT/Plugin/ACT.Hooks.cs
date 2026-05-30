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

        InstallMapEffectHook();
        LogHelper.Warning("插件", "Cast Hook 当前按稳定性策略禁用；BOSS 读条使用 Framework 轮询 IBattleChara.IsCasting 采集。ActionEffect 主统计会独立安装；ActorControl 特效标记采集当前已按稳定性策略禁用。");
    }

    private unsafe void InstallMapEffectHook()
    {
        try
        {
            var signature = TryGetActorControlMemberFunctionSignature();
            if (string.IsNullOrWhiteSpace(signature))
            {
                // Last resort: hardcoded known signature from FFXIVClientStructs source
                signature = "40 55 53 57 41 54 41 56 48 8D AC 24 ?? ?? ?? ?? B8 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 2B E0 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? 8B 85";
            }

            mapEffectHook = DalamudApi.Interop.HookFromSignature<ActorControlDelegate>(signature, HandleMapEffectPacket);
            mapEffectHook.Enable();
            LogHelper.Info("插件", "已安装 MapEffect Hook（仅处理 category 0x1F9），用于时间轴地图特效同步。");
        }
        catch (Exception ex)
        {
            LogHelper.Warning("插件", ex, "安装 MapEffect Hook 失败，时间轴地图特效同步不可用。");
            mapEffectHook = null;
        }
    }

    private void HandleMapEffectPacket(uint entityId, uint category, uint param1, uint param2, uint param3, uint param4, uint param5, uint param6, uint param7, uint param8, ulong targetId, byte replaying)
    {
        try
        {
            mapEffectHook!.Original(entityId, category, param1, param2, param3, param4, param5, param6, param7, param8, targetId, replaying);

            // 0x1F9 = 505 = MapEffect category
            if (category == 0x1F9)
            {
                var utcNow = DateTime.UtcNow;
                timelineService.ObserveMapEffect(entityId, param1, param2, utcNow);
                statsService.RecordCombatTimelineMapEffect(param1, param2, utcNow);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error("插件", ex, "MapEffect Hook 处理异常。");
        }
    }

    private void DisposeHooks()
    {
        try
        {
            receiveAbilityHook?.Disable();
            if (receiveAbilityHook != null)
                LogHelper.Debug("插件", "已关闭 ActionEffect Hook。");

            mapEffectHook?.Disable();
            if (mapEffectHook != null)
                LogHelper.Debug("插件", "已关闭 MapEffect Hook。");

            actorControlHook?.Disable();
            if (actorControlHook != null)
                LogHelper.Debug("插件", "已关闭 ActorControl Hook。");
        }
        catch
        {
            // Ignore hook shutdown failures while disposing.
        }

        receiveAbilityHook?.Dispose();
        mapEffectHook?.Dispose();
        actorControlHook?.Dispose();
    }
}
