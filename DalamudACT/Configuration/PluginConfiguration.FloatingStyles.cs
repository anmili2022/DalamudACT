using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace DalamudACT;

public sealed partial class PluginConfiguration
{
    public void SwitchFloatingStatsDisplayStyle(FloatingStatsDisplayStyle style)
    {
        if (FloatingStatsDisplayStyle == style)
            return;

        SaveFloatingStyleSettingsFile(FloatingStatsDisplayStyle);

        if (!TryLoadFloatingStyleSettingsFromFile(style))
        {
            FloatingStatsDisplayStyle = style;
            ReinitializeAfterExternalStyleChange();
        }

        Save();
    }

    public bool ResetFloatingStyleToDefaults(FloatingStatsDisplayStyle style, out string message)
    {
        try
        {
            var snapshot = CreateDefaultFloatingStyleSnapshot(style);
            var path = GetFloatingStyleSettingsPath(style);
            if (string.IsNullOrWhiteSpace(path))
            {
                message = "恢复失败：未能定位样式配置文件。";
                return false;
            }

            WriteFloatingStyleSnapshotToPath(snapshot, path);

            if (FloatingStatsDisplayStyle == style)
            {
                CopyPersistentFieldsFrom(snapshot);
                ReinitializeAfterExternalStyleChange();
                Save();
            }

            message = $"已恢复 {GetFloatingStyleDisplayName(style)} 样式默认设置。";
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Warning("配置", ex, $"恢复 {GetFloatingStyleDisplayName(style)} 样式默认设置失败。");
            message = $"恢复失败：{ex.Message}";
            return false;
        }
    }

    public string? GetFloatingStyleSettingsFilePath(FloatingStatsDisplayStyle style)
        => GetFloatingStyleSettingsPath(style);

    public string? GetFloatingStyleSettingsDirectoryPath()
        => pluginInterface?.GetPluginConfigDirectory();

    public string? GetFloatingStyleExportDirectoryPath()
    {
        var configDirectory = GetFloatingStyleSettingsDirectoryPath();
        return string.IsNullOrWhiteSpace(configDirectory)
            ? null
            : Path.Combine(configDirectory, FloatingStyleExportsDirectoryName);
    }

    public bool OpenFloatingStyleSettingsDirectory(out string message)
        => TryOpenDirectory(
            GetFloatingStyleSettingsDirectoryPath(),
            "已打开样式配置目录。",
            out message);

    public bool OpenFloatingStyleExportDirectory(out string message)
    {
        var exportDirectory = GetFloatingStyleExportDirectoryPath();
        if (!string.IsNullOrWhiteSpace(exportDirectory))
            Directory.CreateDirectory(exportDirectory);

        return TryOpenDirectory(exportDirectory, "已打开样式导出目录。", out message);
    }

    public bool ExportFloatingStyleSettingsTo(
        FloatingStatsDisplayStyle style,
        string? exportPath,
        out string message)
    {
        var sourcePath = GetFloatingStyleSettingsPath(style);
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            message = "导出失败：未能定位样式配置文件。";
            return false;
        }

        if (style == FloatingStatsDisplayStyle)
            SaveFloatingStyleSettingsFile(style);
        else
            EnsureFloatingStyleSettingsFileExists(style);

        if (!File.Exists(sourcePath))
        {
            message = $"导出失败：未找到源文件 {sourcePath}";
            return false;
        }

        var resolvedExportPath = ResolveExportPath(style, exportPath);
        if (string.IsNullOrWhiteSpace(resolvedExportPath))
        {
            message = "导出失败：无法确定导出目标路径。";
            return false;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(resolvedExportPath)!);
            File.Copy(sourcePath, resolvedExportPath, true);
            message = $"已导出 {GetFloatingStyleDisplayName(style)} 样式到 {resolvedExportPath}";
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Warning("配置", ex, $"导出样式设置失败：{resolvedExportPath}");
            message = $"导出失败：{ex.Message}";
            return false;
        }
    }


    public bool ImportFloatingStyleSettingsFrom(
        FloatingStatsDisplayStyle style,
        string importPath,
        out string message)
    {
        if (string.IsNullOrWhiteSpace(importPath))
        {
            message = "导入失败：请先填写要导入的 JSON 路径。";
            return false;
        }

        if (!File.Exists(importPath))
        {
            message = $"导入失败：未找到文件 {importPath}";
            return false;
        }

        var targetPath = GetFloatingStyleSettingsPath(style);
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            message = "导入失败：未能定位目标样式配置文件。";
            return false;
        }

        if (!TryLoadFloatingStyleSnapshotFromFile(importPath, style, out var snapshot, out message))
            return false;

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

            message = $"已导入 {GetFloatingStyleDisplayName(style)} 样式：{importPath}";
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Warning("配置", ex, $"导入样式设置失败：{importPath}");
            message = $"导入失败：{ex.Message}";
            return false;
        }
    }

    public void SyncSharedColumnSettings()
    {
        ShowHpsPlayerColumn = ShowDpsPlayerColumn;
        ShowTakenPlayerColumn = ShowDpsPlayerColumn;

        ShowHpsJobColumn = ShowDpsJobColumn;
        ShowTakenJobColumn = ShowDpsJobColumn;

        ShowHpsHealColumn = ShowDpsDamageColumn;
        ShowTakenDamageColumn = ShowDpsDamageColumn;

        ShowHpsValueColumn = ShowDpsValueColumn;
        ShowTakenValueColumn = ShowDpsValueColumn;
    }

    public void ResetSharedMetricColumnWidths()
    {
        FloatingStatsPlayerColumnWidth = 0f;
        FloatingStatsJobColumnWidth = 0f;
        FloatingStatsDamageColumnWidth = 0f;
        FloatingStatsValueColumnWidth = 0f;
        FloatingStatsDeathsColumnWidth = 0f;
    }

    public void ResetHistoryColumnWidths()
    {
        HistoryStartTimeColumnWidth = 0f;
        HistoryEndTimeColumnWidth = 0f;
        HistoryDurationColumnWidth = 0f;
    }

    private void EnsureFloatingStyleSettingFilesInitialized()
    {
        if (pluginInterface == null)
            return;

        EnsureFloatingStyleSettingsFileExists(FloatingStatsDisplayStyle.Classic);
        EnsureFloatingStyleSettingsFileExists(FloatingStatsDisplayStyle.Ikegami);
        EnsureFloatingStyleSettingsFileExists(FloatingStatsDisplayStyle.Minimal);
        _ = TryLoadFloatingStyleSettingsFromFile(FloatingStatsDisplayStyle);
    }

    private void EnsureFloatingStyleSettingsFileExists(FloatingStatsDisplayStyle style)
    {
        var path = GetFloatingStyleSettingsPath(style);
        if (string.IsNullOrWhiteSpace(path) || File.Exists(path))
            return;

        SaveFloatingStyleSettingsFile(style);
    }

    private bool TryLoadFloatingStyleSettingsFromFile(FloatingStatsDisplayStyle style)
    {
        var path = GetFloatingStyleSettingsPath(style);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            var json = File.ReadAllText(path);
            var snapshot = JsonSerializer.Deserialize<PluginConfiguration>(json, FloatingStyleJsonOptions);
            if (snapshot == null)
                return false;

            CopyPersistentFieldsFrom(snapshot);
            FloatingStatsDisplayStyle = style;
            ReinitializeAfterExternalStyleChange();
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Warning("配置", ex, $"读取样式设置文件失败：{path}");
            return false;
        }
    }

    private void SaveFloatingStyleSettingsFile(FloatingStatsDisplayStyle style)
    {
        var path = GetFloatingStyleSettingsPath(style);
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            var snapshot = CreateFloatingStyleSnapshot(style);
            WriteFloatingStyleSnapshotToPath(snapshot, path);
        }
        catch (Exception ex)
        {
            LogHelper.Warning("配置", ex, $"写入样式设置文件失败：{path}");
        }
    }

    private PluginConfiguration CreateFloatingStyleSnapshot(FloatingStatsDisplayStyle style)
    {
        var snapshot = new PluginConfiguration();
        snapshot.CopyPersistentFieldsFrom(this);
        snapshot.FloatingStatsDisplayStyle = style;
        return snapshot;
    }

    private PluginConfiguration CreateDefaultFloatingStyleSnapshot(FloatingStatsDisplayStyle style)
    {
        var snapshot = new PluginConfiguration();
        snapshot.Reset();
        snapshot.FloatingStatsDisplayStyle = style;

        if (pluginInterface != null)
        {
            snapshot.suppressFloatingStyleSettingsSync = true;
            snapshot.Initialize(pluginInterface);
            snapshot.suppressFloatingStyleSettingsSync = false;
        }

        return snapshot;
    }

    private void CopyPersistentFieldsFrom(PluginConfiguration source)
    {
        Version = source.Version;
        foreach (var field in PersistentFieldInfos)
            field.SetValue(this, field.GetValue(source));
    }

    private void ReinitializeAfterExternalStyleChange()
    {
        if (pluginInterface == null)
            return;

        suppressFloatingStyleSettingsSync = true;
        try
        {
            Initialize(pluginInterface);
        }
        finally
        {
            suppressFloatingStyleSettingsSync = false;
        }
    }

    private string? GetFloatingStyleSettingsPath(FloatingStatsDisplayStyle style)
    {
        if (pluginInterface == null)
            return null;

        var configDirectory = pluginInterface.GetPluginConfigDirectory();
        var fileName = style switch
        {
            FloatingStatsDisplayStyle.Ikegami => FloatingIkegamiSettingsFileName,
            FloatingStatsDisplayStyle.Minimal => FloatingMinimalSettingsFileName,
            _ => FloatingClassicSettingsFileName,
        };

        return Path.Combine(configDirectory, fileName);
    }

    private string ResolveExportPath(FloatingStatsDisplayStyle style, string? exportPath)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

        if (string.IsNullOrWhiteSpace(exportPath))
        {
            var exportDirectory = GetFloatingStyleExportDirectoryPath();
            if (string.IsNullOrWhiteSpace(exportDirectory))
                return string.Empty;

            return Path.Combine(exportDirectory, $"{GetFloatingStyleFileStem(style)}-{timestamp}.json");
        }

        if (Directory.Exists(exportPath))
            return Path.Combine(exportPath, $"{GetFloatingStyleFileStem(style)}-{timestamp}.json");

        if (EndsWithDirectorySeparator(exportPath))
        {
            return Path.Combine(exportPath, $"{GetFloatingStyleFileStem(style)}-{timestamp}.json");
        }

        return Path.GetExtension(exportPath).Length == 0
            ? $"{exportPath}.json"
            : exportPath;
    }

    private bool TryLoadFloatingStyleSnapshotFromFile(
        string importPath,
        FloatingStatsDisplayStyle style,
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
            var json = File.ReadAllText(importPath);
            snapshot = JsonSerializer.Deserialize<PluginConfiguration>(json, FloatingStyleJsonOptions);
            if (snapshot == null)
            {
                message = "导入失败：JSON 内容为空或无法识别。";
                return false;
            }

            snapshot.FloatingStatsDisplayStyle = style;
            snapshot.suppressFloatingStyleSettingsSync = true;
            snapshot.Initialize(pluginInterface);
            snapshot.suppressFloatingStyleSettingsSync = false;
            message = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Warning("配置", ex, $"解析样式设置文件失败：{importPath}");
            message = $"导入失败：{ex.Message}";
            return false;
        }
    }


    private void WriteFloatingStyleSnapshotToPath(PluginConfiguration snapshot, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(snapshot, FloatingStyleJsonOptions);
        File.WriteAllText(path, json);
    }

    private static string GetFloatingStyleDisplayName(FloatingStatsDisplayStyle style)
        => style switch
        {
            FloatingStatsDisplayStyle.Ikegami => "ikegami",
            FloatingStatsDisplayStyle.Minimal => "极简样式",
            _ => "经典表格",
        };

    private static string GetFloatingStyleFileStem(FloatingStatsDisplayStyle style)
        => style switch
        {
            FloatingStatsDisplayStyle.Ikegami => "floating-stats-ikegami",
            FloatingStatsDisplayStyle.Minimal => "floating-stats-minimal",
            _ => "floating-stats-classic",
        };


    private static bool EndsWithDirectorySeparator(string path)
        => !string.IsNullOrWhiteSpace(path)
           && (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
               || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal));

    private static bool TryOpenDirectory(string? directoryPath, string successMessage, out string message)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            message = "操作失败：未能定位目标目录。";
            return false;
        }

        try
        {
            Directory.CreateDirectory(directoryPath);
            Process.Start(new ProcessStartInfo
            {
                FileName = directoryPath,
                UseShellExecute = true,
                Verb = "open",
            });
            message = successMessage;
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Warning("配置", ex, $"打开目录失败：{directoryPath}");
            message = $"打开目录失败：{ex.Message}";
            return false;
        }
    }
}
