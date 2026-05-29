using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace DalamudACT;

internal sealed class TimelineRemoteResourceDownloader
{
    private const string ResourceBaseUrl = "https://raw.githubusercontent.com/anmili2022/DalamudACT/main/DalamudACT/Features/Timeline/Data";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<string> RefreshCurrentZoneAsync(uint zoneId, string zoneName)
    {
        var entries = await DownloadIndexAsync().ConfigureAwait(false);
        var entry = entries.FirstOrDefault(item => item.Matches(zoneId, zoneName));
        if (entry == null)
            return string.IsNullOrWhiteSpace(zoneName)
                ? $"当前区域没有在线时间轴：{zoneId}。"
                : $"当前区域没有在线时间轴：{zoneName} ({zoneId})。";

        var downloaded = await DownloadTimelineFileAsync(entry.File).ConfigureAwait(false);
        return downloaded
            ? $"已刷新当前副本时间轴：{entry.Name}。"
            : $"刷新当前副本时间轴失败：{entry.Name}。";
    }

    public async Task<string> DownloadAllAsync()
    {
        var entries = await DownloadIndexAsync().ConfigureAwait(false);
        var success = 0;
        var failed = 0;

        foreach (var entry in entries)
        {
            if (await DownloadTimelineFileAsync(entry.File).ConfigureAwait(false))
                success++;
            else
                failed++;
        }

        return $"全部在线时间轴下载完成：成功 {success}，失败 {failed}。";
    }

    private static async Task<List<TimelineIndexEntry>> DownloadIndexAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        var json = await client.GetStringAsync($"{ResourceBaseUrl}/timeline-index.json").ConfigureAwait(false);
        Directory.CreateDirectory(GetCacheDataDirectory());
        await File.WriteAllTextAsync(GetCacheIndexPath(), json, new UTF8Encoding(false)).ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<TimelineIndexEntry>>(json, JsonOptions) ?? [];
    }

    private static async Task<bool> DownloadTimelineFileAsync(string fileName)
    {
        foreach (var candidate in GetLocalizedFileNameCandidates(fileName))
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                var text = await client.GetStringAsync($"{ResourceBaseUrl}/{candidate.Replace('\\', '/')}").ConfigureAwait(false);
                var targetPath = Path.Combine(GetCacheDataDirectory(), candidate);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? GetCacheDataDirectory());
                await File.WriteAllTextAsync(targetPath, text, new UTF8Encoding(false)).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Debug("时间轴", ex, $"下载在线时间轴失败：{candidate}");
            }
        }

        return false;
    }

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
        => Path.Combine(DalamudApi.PluginInterface.ConfigDirectory.FullName, "Timeline", "RemoteCache", "Data");

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
