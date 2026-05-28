using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace DalamudACT;

internal sealed class AeAssistResourceDownloader
{
    private const string ResourceBaseUrl = "https://raw.githubusercontent.com/aeassist-acr/Resource/main";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
    };

    private readonly object gate = new();
    private readonly HashSet<uint> aoeActions = [];
    private readonly HashSet<uint> tankDeathSentence = [];
    private bool loadedLocal;
    private bool refreshStarted;

    public string? GetHint(uint actionId)
    {
        EnsureLoaded();

        lock (gate)
        {
            if (tankDeathSentence.Contains(actionId))
                return "死刑";

            if (aoeActions.Contains(actionId))
                return "AOE";
        }

        return null;
    }

    public void RefreshNow()
    {
        EnsureLoaded();

        try
        {
            DownloadResourceAsync("AoeActions", aoeActions).GetAwaiter().GetResult();
            DownloadResourceAsync("TankDeathSentence", tankDeathSentence).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            LogHelper.Debug("时间轴", ex, "同步刷新 AEAssist 机制资源失败。将继续使用本地缓存。 ");
        }
    }

    public static string RefreshResourcesForSettings()
    {
        try
        {
            var downloader = new AeAssistResourceDownloader();
            downloader.RefreshNow();
            return "已刷新额外资源：AoeActions / TankDeathSentence。";
        }
        catch (Exception ex)
        {
            LogHelper.Warning("时间轴", ex, "刷新额外资源失败。 ");
            return $"刷新额外资源失败：{ex.Message}";
        }
    }

    private void EnsureLoaded()
    {
        if (!loadedLocal)
        {
            lock (gate)
            {
                if (!loadedLocal)
                {
                    LoadLocalLocked("AoeActions", aoeActions);
                    LoadLocalLocked("TankDeathSentence", tankDeathSentence);
                    loadedLocal = true;
                }
            }
        }

        if (refreshStarted)
            return;

        refreshStarted = true;
        _ = Task.Run(RefreshAsync);
    }

    private async Task RefreshAsync()
    {
        try
        {
            await DownloadResourceAsync("AoeActions", aoeActions).ConfigureAwait(false);
            await DownloadResourceAsync("TankDeathSentence", tankDeathSentence).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogHelper.Debug("时间轴", ex, "刷新 AEAssist 机制资源失败。将继续使用本地缓存或 cactbot 分类。 ");
        }
    }

    private async Task DownloadResourceAsync(string name, HashSet<uint> target)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        var url = $"{ResourceBaseUrl}/{name}.json";
        var json = await client.GetStringAsync(url).ConfigureAwait(false);
        var values = JsonSerializer.Deserialize<HashSet<uint>>(json, JsonOptions);
        if (values == null)
            return;

        Directory.CreateDirectory(GetCacheDirectory());
        await File.WriteAllTextAsync(GetCachePath(name), json).ConfigureAwait(false);

        lock (gate)
        {
            target.Clear();
            foreach (var value in values)
                target.Add(value);
        }

        LogHelper.Debug("时间轴", $"已刷新 AEAssist 资源 {name}：{values.Count} 条。 ");
    }

    private void LoadLocalLocked(string name, HashSet<uint> target)
    {
        var path = GetCachePath(name);
        if (!File.Exists(path))
            return;

        try
        {
            var values = JsonSerializer.Deserialize<HashSet<uint>>(File.ReadAllText(path), JsonOptions);
            if (values == null)
                return;

            target.Clear();
            foreach (var value in values)
                target.Add(value);

            LogHelper.Debug("时间轴", $"已加载 AEAssist 本地资源 {name}：{values.Count} 条。 ");
        }
        catch (Exception ex)
        {
            LogHelper.Debug("时间轴", ex, $"读取 AEAssist 本地资源失败：{path}");
        }
    }

    private static string GetCacheDirectory()
        => Path.Combine(DalamudApi.PluginInterface.ConfigDirectory.FullName, "Timeline", "Resource");

    private static string GetCachePath(string name)
        => Path.Combine(GetCacheDirectory(), $"{name}.json");
}
