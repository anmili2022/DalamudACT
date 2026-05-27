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

    }

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
