using System;
using System.Reflection;
using FFXIVClientStructs.FFXIV.Client.Network;

namespace DalamudACT;

public sealed partial class ACT
{
    private static string? TryGetActorControlMemberFunctionSignature()
    {
        try
        {
            var methodInfo = typeof(PacketDispatcher).GetMethod("HandleActorControlPacket",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (methodInfo == null)
                return null;

            foreach (var attr in methodInfo.GetCustomAttributes(false))
            {
                var attrType = attr.GetType();
                if (attrType.Name == "MemberFunctionAttribute" || attrType.Name == "SignatureAttribute")
                {
                    var sigProp = attrType.GetProperty("Signature") ?? attrType.GetProperty("Value");
                    if (sigProp?.GetValue(attr) is string sig && !string.IsNullOrWhiteSpace(sig))
                        return sig;
                }
            }
        }
        catch
        {
            // Reflection failed
        }

        return null;
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
