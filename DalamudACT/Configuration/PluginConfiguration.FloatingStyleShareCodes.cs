using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace DalamudACT;

public sealed partial class PluginConfiguration
{
    public bool TryGenerateFloatingStyleShareCode(
        FloatingStatsDisplayStyle style,
        out string shareCode,
        out string message)
    {
        shareCode = string.Empty;

        try
        {
            var snapshot = CreateFloatingStyleSnapshot(style);
            var json = JsonSerializer.Serialize(snapshot, FloatingStyleShareCodeJsonOptions);
            var payloadBytes = Encoding.UTF8.GetBytes(json);

            using var compressedStream = new MemoryStream();
            using (var gzip = new GZipStream(compressedStream, CompressionLevel.SmallestSize, true))
                gzip.Write(payloadBytes, 0, payloadBytes.Length);

            shareCode = $"{FloatingStyleShareCodePrefix}|{GetFloatingStyleShareCodeStyleToken(style)}|{Convert.ToBase64String(compressedStream.ToArray())}";
            message = $"已生成 {GetFloatingStyleDisplayName(style)} 分享码。";
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Warning("配置", ex, $"生成 {GetFloatingStyleDisplayName(style)} 分享码失败。");
            message = $"生成分享码失败：{ex.Message}";
            return false;
        }
    }

    public bool ImportFloatingStyleShareCode(
        FloatingStatsDisplayStyle style,
        string shareCode,
        out string message)
    {
        if (string.IsNullOrWhiteSpace(shareCode))
        {
            message = "导入失败：请先粘贴分享码。";
            return false;
        }

        if (!TryDecodeFloatingStyleShareCode(shareCode, style, out var snapshot, out message))
            return false;

        var targetPath = GetFloatingStyleSettingsPath(style);
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            message = "导入失败：未能定位目标样式配置文件。";
            return false;
        }

        try
        {
            WriteFloatingStyleSnapshotToPath(snapshot!, targetPath);

            if (FloatingStatsDisplayStyle == style)
            {
                CopyPersistentFieldsFrom(snapshot!);
                FloatingStatsDisplayStyle = style;
                ReinitializeAfterExternalStyleChange();
                Save();
            }

            message = $"已导入 {GetFloatingStyleDisplayName(style)} 分享码。";
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Warning("配置", ex, $"导入 {GetFloatingStyleDisplayName(style)} 分享码失败。");
            message = $"导入失败：{ex.Message}";
            return false;
        }
    }

    public bool ImportFloatingStyleShareCode(
        string shareCode,
        out string message)
    {
        if (string.IsNullOrWhiteSpace(shareCode))
        {
            message = "导入失败：请先粘贴分享码。";
            return false;
        }

        if (!TryResolveFloatingStyleFromShareCode(shareCode, out var style, out message))
            return false;

        return ImportFloatingStyleShareCode(style, shareCode, out message);
    }

    public bool TryPeekFloatingStyleShareCodeStyle(
        string shareCode,
        out FloatingStatsDisplayStyle style)
    {
        style = FloatingStatsDisplayStyle.Classic;
        return !string.IsNullOrWhiteSpace(shareCode)
               && TryResolveFloatingStyleFromShareCode(shareCode, out style, out _);
    }


    private bool TryDecodeFloatingStyleShareCode(
        string shareCode,
        FloatingStatsDisplayStyle expectedStyle,
        out PluginConfiguration? snapshot,
        out string message)
    {
        snapshot = null;

        if (pluginInterface == null)
        {
            message = "导入失败：插件接口尚未初始化。";
            return false;
        }

        try
        {
            var trimmed = shareCode.Trim();
            var parts = trimmed.Split('|', 3, StringSplitOptions.TrimEntries);
            if (parts.Length != 3 || !string.Equals(parts[0], FloatingStyleShareCodePrefix, StringComparison.Ordinal))
            {
                message = "导入失败：分享码格式不正确。";
                return false;
            }

            var styleFromCode = TryParseFloatingStyleShareCodeStyleToken(parts[1], out var parsedStyle)
                ? parsedStyle
                : (FloatingStatsDisplayStyle?)null;
            if (styleFromCode == null)
            {
                message = "导入失败：分享码里的样式标记无法识别。";
                return false;
            }

            if (styleFromCode.Value != expectedStyle)
            {
                message = $"导入失败：这是一份{GetFloatingStyleDisplayName(styleFromCode.Value)}分享码，不是{GetFloatingStyleDisplayName(expectedStyle)}。";
                return false;
            }

            var compressedBytes = Convert.FromBase64String(parts[2]);
            using var compressedStream = new MemoryStream(compressedBytes);
            using var gzip = new GZipStream(compressedStream, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip, Encoding.UTF8);
            var json = reader.ReadToEnd();

            snapshot = JsonSerializer.Deserialize<PluginConfiguration>(json, FloatingStyleShareCodeJsonOptions);
            if (snapshot == null)
            {
                message = "导入失败：分享码内容为空或无法识别。";
                return false;
            }

            snapshot.FloatingStatsDisplayStyle = expectedStyle;
            snapshot.suppressFloatingStyleSettingsSync = true;
            snapshot.Initialize(pluginInterface);
            snapshot.suppressFloatingStyleSettingsSync = false;
            message = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Warning("配置", ex, "解析分享码失败。");
            message = $"导入失败：{ex.Message}";
            return false;
        }
    }

    private bool TryResolveFloatingStyleFromShareCode(
        string shareCode,
        out FloatingStatsDisplayStyle style,
        out string message)
    {
        style = FloatingStatsDisplayStyle.Classic;
        var trimmed = shareCode.Trim();
        var parts = trimmed.Split('|', 3, StringSplitOptions.TrimEntries);
        if (parts.Length != 3 || !string.Equals(parts[0], FloatingStyleShareCodePrefix, StringComparison.Ordinal))
        {
            message = "导入失败：分享码格式不正确。";
            return false;
        }

        if (!TryParseFloatingStyleShareCodeStyleToken(parts[1], out style))
        {
            message = "导入失败：分享码里的样式标记无法识别。";
            return false;
        }

        message = string.Empty;
        return true;
    }


    private static string GetFloatingStyleShareCodeStyleToken(FloatingStatsDisplayStyle style)
        => style switch
        {
            FloatingStatsDisplayStyle.Ikegami => "Ikegami",
            FloatingStatsDisplayStyle.Minimal => "Minimal",
            _ => "Classic",
        };

    private static bool TryParseFloatingStyleShareCodeStyleToken(string code, out FloatingStatsDisplayStyle style)
    {
        style = FloatingStatsDisplayStyle.Classic;
        if (string.Equals(code, "Ikegami", StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, "I", StringComparison.OrdinalIgnoreCase))
        {
            style = FloatingStatsDisplayStyle.Ikegami;
            return true;
        }

        if (string.Equals(code, "Minimal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, "M", StringComparison.OrdinalIgnoreCase))
        {
            style = FloatingStatsDisplayStyle.Minimal;
            return true;
        }

        if (string.Equals(code, "Classic", StringComparison.OrdinalIgnoreCase)
            || string.Equals(code, "C", StringComparison.OrdinalIgnoreCase))
        {
            style = FloatingStatsDisplayStyle.Classic;
            return true;
        }

        return false;
    }
}
