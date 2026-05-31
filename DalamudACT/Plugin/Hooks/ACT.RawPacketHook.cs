using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Network;

namespace DalamudACT;

public sealed partial class ACT
{
    private void UpdateRawGamePacketHookState()
    {
        if (Configuration.TimelineRawPacketDebug)
        {
            if (rawGamePacketHook == null && !rawGamePacketHookInstallFailed)
            {
                rawGamePacketHook = RawGamePacketHook.TryInstall(Configuration);
                rawGamePacketHookInstallFailed = rawGamePacketHook == null;
            }
            else
            {
                rawGamePacketHook?.RefreshConfiguration();
            }

            return;
        }

        rawGamePacketHook?.Dispose();
        rawGamePacketHook = null;
        rawGamePacketHookInstallFailed = false;
    }

    private void LogRawPacketsNearSystemMessage(string source, string text)
    {
        if (rawGamePacketHook == null || string.IsNullOrWhiteSpace(text))
            return;

        var normalizedText = text.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal).Trim();
        if (string.Equals(normalizedText, "Dalamud.Game.Chat.LogMessage", StringComparison.Ordinal))
            return;

        var now = DateTime.UtcNow;
        if (string.Equals(lastRawPacketCorrelationText, normalizedText, StringComparison.Ordinal)
            && (now - lastRawPacketCorrelationAtUtc).TotalMilliseconds < 750d)
            return;

        lastRawPacketCorrelationText = normalizedText;
        lastRawPacketCorrelationAtUtc = now;
        rawGamePacketHook.LogRecentPackets($"{source}: {TruncateForLog(normalizedText, 80)}");
    }

    private static string TruncateForLog(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..maxLength] + "...";

    private sealed unsafe class RawGamePacketHook : IDisposable
    {
        private const int MinPreviewBytes = 16;
        private const int MaxPreviewBytes = 512;
        private readonly PluginConfiguration configuration;
        private readonly Hook<ReceivePacketInternalDelegate> receiveHook;
        private readonly List<RawPacketSnapshot> recentPackets = [];
        private readonly HashSet<ushort> opcodeFilter = [];
        private string lastFilterText = string.Empty;
        private int previewBytes = 32;
        private DateTime lastLogUtc = DateTime.MinValue;
        private int suppressedPacketCount;

        private RawGamePacketHook(PluginConfiguration configuration, Hook<ReceivePacketInternalDelegate> receiveHook)
        {
            this.configuration = configuration;
            this.receiveHook = receiveHook;
            RefreshConfiguration();
        }

        private delegate void ReceivePacketInternalDelegate(PacketDispatcher* dispatcher, uint targetId, byte* packet);

        public static RawGamePacketHook? TryInstall(PluginConfiguration configuration)
        {
            try
            {
                var address = GetVFuncByName(PacketDispatcher.StaticVirtualTablePointer, "OnReceivePacket");
                if (address == nint.Zero)
                {
                    LogHelper.Warning("插件", "RawGamePacket Hook 安装失败：PacketDispatcher.OnReceivePacket 地址为空。");
                    return null;
                }

                RawGamePacketHook? instance = null;
                var hook = DalamudApi.Interop.HookFromAddress<ReceivePacketInternalDelegate>(
                    address,
                    (dispatcher, targetId, packet) => instance?.HandleReceivePacket(dispatcher, targetId, packet));

                instance = new RawGamePacketHook(configuration, hook);
                hook.Enable();
                LogHelper.Warning("插件", $"已安装 RawGamePacket Hook（网络包增强模式，address=0x{address:X}）。");
                return instance;
            }
            catch (Exception ex)
            {
                LogHelper.Warning("插件", ex, "安装 RawGamePacket Hook 失败。网络包增强模式不可用。");
                return null;
            }
        }

        public void RefreshConfiguration()
        {
            previewBytes = Math.Clamp(configuration.TimelineRawPacketPreviewBytes, MinPreviewBytes, MaxPreviewBytes);
            var filterText = configuration.TimelineRawPacketOpcodeFilter ?? string.Empty;
            if (string.Equals(filterText, lastFilterText, StringComparison.Ordinal))
                return;

            opcodeFilter.Clear();
            foreach (var token in filterText.Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var normalized = token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? token[2..] : token;
                if (ushort.TryParse(normalized, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var opcode))
                    opcodeFilter.Add(opcode);
            }

            lastFilterText = filterText;
        }

        public void Dispose()
        {
            try
            {
                receiveHook.Disable();
            }
            catch
            {
                // Ignore hook shutdown failures while disposing.
            }

            receiveHook.Dispose();
            LogHelper.Debug("插件", "已关闭 RawGamePacket Hook。");
        }

        private void HandleReceivePacket(PacketDispatcher* dispatcher, uint targetId, byte* packet)
        {
            receiveHook.Original(dispatcher, targetId, packet);

            try
            {
                if (packet == null)
                    return;

                var packetStart = packet - 16;
                var opcode = *(ushort*)(packetStart + 18);
                if (opcodeFilter.Count > 0 && !opcodeFilter.Contains(opcode))
                    return;

                AddRecentPacket(opcode, targetId, packetStart);
                LogPacket(opcode, targetId, packetStart);
            }
            catch (Exception ex)
            {
                LogHelper.Debug("插件", ex, "RawGamePacket Hook 处理收包失败。");
            }
        }

        private void LogPacket(ushort opcode, uint targetId, byte* packetStart)
        {
            if (!LogHelper.IsDebugEnabled("插件"))
                return;

            var now = DateTime.UtcNow;
            if ((now - lastLogUtc).TotalSeconds < 1d)
            {
                suppressedPacketCount++;
                return;
            }

            var preview = FormatHexPreview(packetStart, previewBytes);
            var suppressed = suppressedPacketCount > 0 ? $", suppressed={suppressedPacketCount}" : string.Empty;
            suppressedPacketCount = 0;
            lastLogUtc = now;
            LogHelper.Debug("插件", $"RawGamePacket recv opcode=0x{opcode:X4}, target=0x{targetId:X8}{suppressed}, head={preview}");
        }

        public void LogRecentPackets(string reason)
        {
            if (!LogHelper.IsDebugEnabled("插件"))
                return;

            var cutoff = DateTime.UtcNow.AddSeconds(-2d);
            List<RawPacketSnapshot> snapshot;
            lock (recentPackets)
            {
                recentPackets.RemoveAll(packet => packet.TimestampUtc < DateTime.UtcNow.AddSeconds(-5d));
                snapshot = recentPackets.Where(packet => packet.TimestampUtc >= cutoff).ToList();
            }

            if (snapshot.Count == 0)
            {
                LogHelper.Debug("插件", $"RawGamePacket recent reason={reason}, no packets in last 2s");
                return;
            }

            foreach (var packet in snapshot.TakeLast(12))
            {
                var ageMs = (DateTime.UtcNow - packet.TimestampUtc).TotalMilliseconds;
                var targetName = ResolveObjectName(packet.TargetId);
                var target = string.IsNullOrWhiteSpace(targetName)
                    ? $"0x{packet.TargetId:X8}"
                    : $"0x{packet.TargetId:X8}/{targetName}";
                LogHelper.Debug("插件", $"RawGamePacket recent reason={reason}, age={ageMs:0}ms, opcode=0x{packet.Opcode:X4}, target={target}, head={packet.PreviewHex}");
            }
        }

        private static string ResolveObjectName(uint objectId)
        {
            try
            {
                foreach (var obj in DalamudApi.ObjectTable)
                {
                    if (obj == null || obj.GameObjectId != objectId)
                        continue;

                    var name = obj.Name.ToString();
                    return string.IsNullOrWhiteSpace(name) ? obj.ObjectKind.ToString() : name;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private void AddRecentPacket(ushort opcode, uint targetId, byte* packetStart)
        {
            var snapshot = new RawPacketSnapshot(DateTime.UtcNow, opcode, targetId, FormatHexPreview(packetStart, previewBytes));
            lock (recentPackets)
            {
                recentPackets.Add(snapshot);
                var cutoff = DateTime.UtcNow.AddSeconds(-5d);
                recentPackets.RemoveAll(packet => packet.TimestampUtc < cutoff);
                if (recentPackets.Count > 128)
                    recentPackets.RemoveRange(0, recentPackets.Count - 128);
            }
        }

        private static string FormatHexPreview(byte* data, int length)
        {
            Span<byte> buffer = stackalloc byte[length];
            for (var i = 0; i < length; i++)
                buffer[i] = data[i];

            return Convert.ToHexString(buffer);
        }

        private static nint GetVFuncByName<T>(T* vtablePtr, string fieldName) where T : unmanaged
        {
            if (vtablePtr == null)
                return nint.Zero;

            var field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            var offset = field?.GetCustomAttribute<FieldOffsetAttribute>()?.Value;
            if (offset == null)
                return nint.Zero;

            return *(nint*)((byte*)vtablePtr + offset.Value);
        }

        private sealed record RawPacketSnapshot(DateTime TimestampUtc, ushort Opcode, uint TargetId, string PreviewHex);
    }
}
