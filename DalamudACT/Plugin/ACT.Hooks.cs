using System;
using System.Linq;
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

        InstallActorControlEventHook();
        LogHelper.Warning("插件", "Cast Hook 当前按稳定性策略禁用；BOSS 读条使用 Framework 轮询 IBattleChara.IsCasting 采集。ActionEffect 主统计会独立安装。");
    }

    private unsafe void InstallActorControlEventHook()
    {
        try
        {
            var signature = TryGetActorControlMemberFunctionSignature();
            if (string.IsNullOrWhiteSpace(signature))
            {
                // Last resort: hardcoded known signature from FFXIVClientStructs source
                signature = "40 55 53 57 41 54 41 56 48 8D AC 24 ?? ?? ?? ?? B8 ?? ?? ?? ?? E8 ?? ?? ?? ?? 48 2B E0 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 85 ?? ?? ?? ?? 8B 85";
            }

            actorControlEventHook = DalamudApi.Interop.HookFromSignature<ActorControlDelegate>(signature, HandleActorControlEventPacket);
            actorControlEventHook.Enable();
            LogHelper.Info("插件", "已安装 ActorControl Hook（处理 MapEffect、头顶标记、连线与团灭淡出），用于时间轴地图特效同步和队友技能冷却重置。");
        }
        catch (Exception ex)
        {
            LogHelper.Warning("插件", ex, "安装 ActorControl 事件 Hook 失败，时间轴地图特效同步和团灭淡出重置不可用。");
            actorControlEventHook = null;
        }
    }

    private void HandleActorControlEventPacket(uint entityId, uint category, uint param1, uint param2, uint param3, uint param4, uint param5, uint param6, uint param7, uint param8, ulong targetId, byte replaying)
    {
        try
        {
            actorControlEventHook!.Original(entityId, category, param1, param2, param3, param4, param5, param6, param7, param8, targetId, replaying);

            if (isDisposing)
                return;

            if (ShouldSuppressCombatModuleWork || Configuration.HighPerformanceMode)
                return;

            switch (category)
            {
                // 0x1F9 = 505 = MapEffect category.
                case 0x1F9:
                {
                    var utcNow = DateTime.UtcNow;
                    if (IsTimelineModuleEnabledInCurrentArea)
                        timelineService.ObserveMapEffect(entityId, param1, param2, utcNow);
                    if (IsStatsModuleEnabled)
                        statsService.RecordCombatTimelineMapEffect(param1, param2, utcNow);
                    break;
                }

                // 34 = TargetIcon. targetId is the marked actor, param1 is the marker id.
                case 34:
                    if (IsStatsModuleEnabled)
                        HandleActorControlTargetIcon(entityId, targetId, param1, param2, param3, param4, param5, param6, param7, param8);
                    break;

                // 35 = Tether. entityId is source actor, param2 is tether id, param3 is target actor.
                case 35:
                    if (IsStatsModuleEnabled)
                        HandleActorControlTether(entityId, param3, param2, cancelled: false);
                    break;

                // 47 = TetherCancel. Parameters match Tether on current references.
                case 47:
                    if (IsStatsModuleEnabled)
                        HandleActorControlTether(entityId, param3, param2, cancelled: true);
                    break;

                // DelvUI uses 0x4000000F as a wipe fadeout signal for party cooldown reset.
                case 0x4000000F:
                    HandleActorControlWipeFadeout();
                    break;
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error("插件", ex, "ActorControl 事件 Hook 处理异常。");
        }
    }

    private void HandleActorControlTargetIcon(uint entityId, ulong targetId, uint iconId, uint param2, uint param3, uint param4, uint param5, uint param6, uint param7, uint param8)
    {
        var utcNow = DateTime.UtcNow;
        var rawTargetId = targetId is > 0 and <= uint.MaxValue ? (uint)targetId : 0u;
        var candidates = new[] { rawTargetId, param2, param3, param4, param5, param6, param7, param8 };
        var targetActorId = 0u;
        foreach (var candidate in candidates)
        {
            if (candidate is 0 or InvalidActorId)
                continue;

            if (statsService.TryResolveTrackedFriendlyActorId(candidate, utcNow, out targetActorId))
                break;
        }

        if (targetActorId == 0)
        {
            foreach (var candidate in candidates)
            {
                if (candidate is 0 or InvalidActorId || candidate == entityId)
                    continue;

                targetActorId = candidate;
                break;
            }
        }

        var candidateText = $"target={rawTargetId:X8}, p2={param2:X8}, p3={param3:X8}, p4={param4:X8}, p5={param5:X8}, p6={param6:X8}, p7={param7:X8}, p8={param8:X8}";
        statsService.RecordCombatTimelineTargetIcon(entityId, targetActorId, iconId, candidateText, utcNow);
        LogHelper.Debug(
            "ActorControl",
            $"TargetIcon entity={entityId:X8}, {candidateText}, resolved={targetActorId:X8}, icon={iconId:X}, utc={utcNow:O}");
        LogHelper.Debug("ActorControl", $"TargetIcon party={BuildTargetIconPartySnapshotText()}");
    }

    private string BuildTargetIconPartySnapshotText()
    {
        try
        {
            var members = statsService.GetCurrentPartyMemberDisplayInfos();
            return members.Count == 0
                ? "empty"
                : string.Join(", ", members.Select(static member => $"{member.ActorId:X8}={member.Name}/{member.JobName}/{member.KindName}"));
        }
        catch (Exception ex)
        {
            return $"unavailable:{ex.GetType().Name}";
        }
    }

    private void HandleActorControlTether(uint sourceActorId, uint targetActorId, uint tetherId, bool cancelled)
    {
        var utcNow = DateTime.UtcNow;
        statsService.RecordCombatTimelineTether(sourceActorId, targetActorId, tetherId, cancelled, utcNow);
        var label = cancelled ? "TetherCancel" : "Tether";
        LogHelper.Debug("ActorControl", $"{label} source={sourceActorId:X8}, target={targetActorId:X8}, id={tetherId:X}, utc={utcNow:O}");
    }

    private void HandleActorControlWipeFadeout()
    {
        if (DalamudApi.ClientState.IsPvP)
            return;

        var utcNow = DateTime.UtcNow;
        if ((utcNow - lastActorControlWipeResetUtc).TotalSeconds < 2d)
            return;

        lastActorControlWipeResetUtc = utcNow;
        monitorService.ResetSkillCooldowns(utcNow);
        LogHelper.Info("队友监控", "检测到团灭淡出，已重置队友技能冷却。");
    }

    private void DisposeHooks()
    {
        try
        {
            receiveAbilityHook?.Disable();
            if (receiveAbilityHook != null)
                LogHelper.Debug("插件", "已关闭 ActionEffect Hook。");

            actorControlEventHook?.Disable();
            if (actorControlEventHook != null)
                LogHelper.Debug("插件", "已关闭 ActorControl 事件 Hook。");

            rawGamePacketHook?.Dispose();
            rawGamePacketHook = null;

        }
        catch
        {
            // Ignore hook shutdown failures while disposing.
        }

        receiveAbilityHook?.Dispose();
        actorControlEventHook?.Dispose();
    }
}
