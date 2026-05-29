using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace DalamudACT;

public static class KamiIconLoader
{
    private static readonly Dictionary<uint, IDalamudTextureWrap?> IconCache = new();
    private static readonly Dictionary<uint, IDalamudTextureWrap?> RawIconCache = new();
    private static readonly Dictionary<uint, uint> ActionIconCache = new();
    private static bool disableIconDrawing;

    public static bool TryDrawIcon(uint actionId, Vector2 size)
    {
        if (disableIconDrawing)
            return false;

        var icon = GetIcon(actionId);
        if (icon == default)
            return false;

        try
        {
            ImGui.Image(icon, size);
            return true;
        }
        catch (Exception ex)
        {
            disableIconDrawing = true;
            LogHelper.Error("队友监控", ex, "绘制技能图标失败，已临时禁用技能图标以避免 UI 原生层崩溃。 ");
            return false;
        }
    }

    public static bool TryDrawIconId(uint iconId, Vector2 size)
    {
        if (disableIconDrawing)
            return false;

        var icon = GetIconId(iconId);
        if (icon == default)
            return false;

        try
        {
            ImGui.Image(icon, size);
            return true;
        }
        catch (Exception ex)
        {
            disableIconDrawing = true;
            LogHelper.Error("队友监控", ex, "绘制图标失败，已临时禁用图标以避免 UI 原生层崩溃。 ");
            return false;
        }
    }

    public static unsafe ImTextureID GetIconId(uint iconId)
    {
        if (RawIconCache.TryGetValue(iconId, out var cached))
            return cached != null ? cached.Handle : default;
        if (iconId == 0 || iconId == 405)
            return default;

        try
        {
            var folder = (iconId / 1000) * 1000;
            var path = $"ui/icon/{folder:000000}/{iconId:000000}.tex";
            var texFile = DalamudApi.GameData.GetFile<Lumina.Data.Files.TexFile>(path);
            if (texFile != null)
            {
                var wrap = DalamudApi.TextureProvider.CreateFromTexFile(texFile);
                if (wrap != null)
                {
                    RawIconCache[iconId] = wrap;
                    return wrap.Handle;
                }
            }
        }
        catch
        {
        }

        RawIconCache[iconId] = null;
        return default;
    }

    public static unsafe ImTextureID GetIcon(uint actionId)
    {
        if (IconCache.TryGetValue(actionId, out var cached))
            return cached != null ? cached.Handle : default;
        if (actionId == 0)
            return default;

        var iconId = ResolveActionIconId(actionId);
        if (iconId == 0 || iconId == 405)
        {
            IconCache[actionId] = null;
            return default;
        }

        try
        {
            var folder = (iconId / 1000) * 1000;
            var path = $"ui/icon/{folder:000000}/{iconId:000000}.tex";
            var texFile = DalamudApi.GameData.GetFile<Lumina.Data.Files.TexFile>(path);
            if (texFile != null)
            {
                var wrap = DalamudApi.TextureProvider.CreateFromTexFile(texFile);
                if (wrap != null)
                {
                    IconCache[actionId] = wrap;
                    return wrap.Handle;
                }
            }
        }
        catch
        {
        }

        IconCache[actionId] = null;
        return default;
    }

    public static bool IsLoaded(uint actionId) => IconCache.ContainsKey(actionId);

    public static void ClearCache()
    {
        foreach (var wrap in IconCache.Values)
            wrap?.Dispose();
        foreach (var wrap in RawIconCache.Values)
            wrap?.Dispose();
        IconCache.Clear();
        RawIconCache.Clear();
        ActionIconCache.Clear();
        disableIconDrawing = false;
    }

    private static uint ResolveActionIconId(uint actionId)
    {
        if (ActionIconCache.TryGetValue(actionId, out var cached))
            return cached;

        try
        {
            var sheet = DalamudApi.GameData.GetExcelSheet<Lumina.Excel.Sheets.Action>();
            if (sheet != null && sheet.TryGetRow(actionId, out var row))
            {
                var iconId = row.Icon;
                ActionIconCache[actionId] = iconId;
                return iconId;
            }
        }
        catch
        {
        }

        ActionIconCache[actionId] = 0;
        return 0;
    }
}
