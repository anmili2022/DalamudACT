using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Network;

namespace DalamudACT;

public sealed partial class ACT
{
    private const string ActorControlCallSignature = "E8 ?? ?? ?? ?? 0F B7 0B 83 E9 64";

    private static string? TryGetFfxivClientStructsAddressSignature(Type structType, string fieldName)
    {
        try
        {
            var addressesType = structType.GetNestedType("Addresses", BindingFlags.Public | BindingFlags.NonPublic);
            var field = addressesType?.GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            var address = field?.GetValue(null);
            return address?.GetType().GetProperty("String", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(address) as string;
        }
        catch
        {
            return null;
        }
    }

    private Hook<ActorControlDelegate> CreateActorControlHook()
    {
        var actorControlSignature = TryGetFfxivClientStructsAddressSignature(typeof(PacketDispatcher), "HandleActorControlPacket");
        if (!string.IsNullOrWhiteSpace(actorControlSignature))
        {
            try
            {
                LogHelper.Debug("插件", "使用 FFXIVClientStructs 提供的 ActorControl 签名安装 Hook。");
                return DalamudApi.Interop.HookFromSignature<ActorControlDelegate>(
                    actorControlSignature,
                    HandleActorControlPacket);
            }
            catch (Exception ex)
            {
                LogHelper.Warning("插件", ex, "使用 FFXIVClientStructs ActorControl 签名安装 Hook 失败，改用本地兼容签名。");
            }
        }
        else
        {
            LogHelper.Debug("插件", "当前 FFXIVClientStructs 未暴露 PacketDispatcher.Addresses.HandleActorControlPacket，改用本地兼容签名安装 ActorControl Hook。");
        }

        var sigScanner = new Dalamud.Game.SigScanner(false, null);
        var callSite = sigScanner.ScanText(ActorControlCallSignature);
        var relativeOffset = Marshal.ReadInt32(IntPtr.Add(callSite, 1));
        var targetAddress = IntPtr.Add(callSite, 5 + relativeOffset);

        LogHelper.Debug(
            "插件",
            $"ActorControl Hook 兼容签名命中：call=0x{callSite.ToInt64():X}，target=0x{targetAddress.ToInt64():X}。");

        return DalamudApi.Interop.HookFromAddress<ActorControlDelegate>(
            targetAddress,
            HandleActorControlPacket);
    }

    private void HandleActorControlPacket(
        uint entityId,
        uint category,
        uint param1,
        uint param2,
        uint param3,
        uint param4,
        uint param5,
        uint param6,
        uint param7,
        uint param8,
        ulong targetId,
        byte replaying)
    {
        actorControlHook!.Original(entityId, category, param1, param2, param3, param4, param5, param6, param7, param8, targetId, replaying);

        try
        {
            if (!TryExtractDebugMarkerId(category, param1, param2, param3, param4, param5, param6, param7, param8, out var markerId))
                return;

            statsService.RecordDebugMarker(
                entityId,
                targetId,
                markerId,
                category,
                param1,
                param2,
                param3,
                param4,
                param5,
                param6,
                param7,
                param8,
                DateTime.UtcNow);

            actorControlFaulted = false;
        }
        catch (Exception ex)
        {
            if (!actorControlFaulted)
            {
                actorControlFaulted = true;
                LogHelper.Warning(
                    "插件",
                    ex,
                    $"处理 ActorControl 事件失败：entity=0x{entityId:X8}，category=0x{category:X}，param1=0x{param1:X}，param2=0x{param2:X}。");
            }
        }
    }

    private static bool TryExtractDebugMarkerId(
        uint category,
        uint param1,
        uint param2,
        uint param3,
        uint param4,
        uint param5,
        uint param6,
        uint param7,
        uint param8,
        out uint markerId)
    {
        markerId = 0;

        // 机制头顶标记在不同运行时 / 日志口径里可能表现为 TargetIcon 或 ActorControl 类事件。
        // 这里先保守抽取“目标为我方角色、且带有非零小型 ID 参数”的 ActorControl，
        // 并在记录里同时保留 category / param，便于后续按真实日志再收窄分类。
        var likelyMarkerCategory = category is 0x22 or 0x23 or 0x1F6 or 0x1F7
                                   || (category >= 0x1C0 && category <= 0x2FF);
        if (!likelyMarkerCategory)
            return false;

        var parameters = new[] { param1, param2, param3, param4, param5, param6, param7, param8 };
        foreach (var candidate in parameters)
        {
            // 头顶 / 特效标记 ID 在 ACT 网络日志里通常是 4 位十六进制数，
            // 而玩家 / 敌对单位 actorId 通常以 0x1 / 0x4 开头。先挑小型 ID，
            // 避免把目标 actorId 误当成 markerId。
            if (candidate > 0 && candidate <= ushort.MaxValue)
            {
                markerId = candidate;
                return true;
            }
        }

        foreach (var candidate in parameters)
        {
            if (candidate != 0 && !LooksLikeCombatActorId(candidate))
            {
                markerId = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeCombatActorId(uint value)
        => (value & 0xF0000000u) is 0x10000000u or 0x40000000u;

    private delegate void ActorControlDelegate(
        uint entityId,
        uint category,
        uint param1,
        uint param2,
        uint param3,
        uint param4,
        uint param5,
        uint param6,
        uint param7,
        uint param8,
        ulong targetId,
        byte replaying);
}
