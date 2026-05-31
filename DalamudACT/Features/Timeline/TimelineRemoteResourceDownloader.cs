using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DalamudACT;

internal sealed class TimelineRemoteResourceDownloader
{
    private const string ResourceBaseUrl = "https://raw.githubusercontent.com/anmili2022/DalamudACT/main/DalamudACT/Features/Timeline/Data";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<string> RefreshCurrentZoneAsync(uint zoneId, string zoneName, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report("正在下载在线时间轴索引...");
        var entries = await DownloadIndexAsync(cancellationToken).ConfigureAwait(false);
        var entry = entries.FirstOrDefault(item => item.Matches(zoneId, zoneName));
        if (entry == null)
            return string.IsNullOrWhiteSpace(zoneName)
                ? $"当前区域没有在线时间轴：{zoneId}。"
                : $"当前区域没有在线时间轴：{zoneName} ({zoneId})。";

        progress?.Report($"正在下载当前副本时间轴：{entry.Name}...");
        var downloaded = await DownloadTimelineFileAsync(entry.File, cancellationToken).ConfigureAwait(false);
        return downloaded
            ? $"已刷新当前副本时间轴：{entry.Name}。"
            : $"刷新当前副本时间轴失败：{entry.Name}。";
    }

    public async Task<string> DownloadAllAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        progress?.Report("正在下载在线时间轴索引...");
        var entries = await DownloadIndexAsync(cancellationToken).ConfigureAwait(false);
        var success = 0;
        var failed = 0;
        var total = entries.Count;

        for (var i = 0; i < entries.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entry = entries[i];
            progress?.Report($"正在下载在线时间轴 {i + 1}/{total}：{entry.Name}");
            if (await DownloadTimelineFileAsync(entry.File, cancellationToken).ConfigureAwait(false))
                success++;
            else
                failed++;
        }

        return $"全部在线时间轴下载完成：成功 {success}，失败 {failed}。";
    }

    private static async Task<List<TimelineIndexEntry>> DownloadIndexAsync(CancellationToken cancellationToken)
    {
        using var client = CreateHttpClient();
        var json = await client.GetStringAsync($"{ResourceBaseUrl}/timeline-index.json", cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(GetCacheDataDirectory());
        await File.WriteAllTextAsync(GetCacheIndexPath(), json, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<TimelineIndexEntry>>(json, JsonOptions) ?? [];
    }

    private static async Task<bool> DownloadTimelineFileAsync(string fileName, CancellationToken cancellationToken)
    {
        using var client = CreateHttpClient();
        foreach (var candidate in GetLocalizedFileNameCandidates(fileName))
        {
            try
            {
                var text = await client.GetStringAsync($"{ResourceBaseUrl}/{candidate.Replace('\\', '/')}", cancellationToken).ConfigureAwait(false);
                var targetPath = Path.Combine(GetCacheDataDirectory(), candidate);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? GetCacheDataDirectory());
                await File.WriteAllTextAsync(targetPath, text, new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogHelper.Debug("时间轴", ex, $"下载在线时间轴失败：{candidate}");
            }
        }

        return false;
    }

    public async Task<string> AutoDownloadForZoneAsync(uint zoneId, string zoneName)
    {
        try
        {
            List<TimelineIndexEntry> entries;
            try
            {
                entries = await DownloadIndexAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                return "索引下载失败";
            }

            var entry = entries.FirstOrDefault(item => item.Matches(zoneId, zoneName));
            if (entry == null)
                return string.IsNullOrWhiteSpace(zoneName)
                    ? $"ZoneId={zoneId}：无在线时间轴"
                    : $"{zoneName} ({zoneId})：无在线时间轴";

            var hadLocal = GetLocalizedFileNameCandidates(entry.File)
                .Select(candidate => Path.Combine(GetCacheDataDirectory(), candidate))
                .Any(File.Exists);

            var downloaded = await DownloadTimelineFileAsync(entry.File, CancellationToken.None).ConfigureAwait(false);
            if (!downloaded)
                return $"{Path.GetFileNameWithoutExtension(entry.File)}：下载失败";

            return hadLocal
                ? $"{Path.GetFileNameWithoutExtension(entry.File)}：已更新"
                : $"{Path.GetFileNameWithoutExtension(entry.File)}：已下载";
        }
        catch (OperationCanceledException)
        {
            return "下载已取消";
        }
        catch (Exception ex)
        {
            LogHelper.Debug("时间轴", ex, "自动下载时间轴失败");
            return "下载失败";
        }
    }

    private static HttpClient CreateHttpClient()
        => new() { Timeout = TimeSpan.FromSeconds(8) };

    private static IEnumerable<string> GetLocalizedFileNameCandidates(string fileName)
    {
        if (fileName.EndsWith(".cn.txt", StringComparison.OrdinalIgnoreCase))
        {
            yield return fileName;
            yield break;
        }

        if (fileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
            yield return fileName[..^4] + ".cn.txt";

        yield return fileName;
    }

    public static string GetCacheDataDirectory()
        => Path.Combine(GetCacheRootDirectory(), "Data");

    public static string GetCacheRootDirectory()
        => Path.Combine(DalamudApi.PluginInterface.ConfigDirectory.FullName, "Timeline", "RemoteCache");

    private static string GetCacheIndexPath()
        => Path.Combine(GetCacheDataDirectory(), "timeline-index.json");

    private sealed record TimelineIndexEntry(
        string Id,
        string Name,
        uint? ZoneId,
        string[]? ZoneNameContains,
        string File)
    {
        public bool Matches(uint zoneId, string zoneName)
        {
            if (ZoneId.HasValue && ZoneId.Value == zoneId)
                return true;

            if (ZoneNameContains == null || string.IsNullOrWhiteSpace(zoneName))
                return false;

            return ZoneNameContains.Any(fragment => zoneName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }
    }
}
