using System;
using System.Globalization;
using System.Reflection;
using Dalamud.IoC;
using Dalamud.Plugin;

namespace DalamudACT;

/// <summary>
/// Dalamud 服务注入与兼容访问层。
/// 相关参考：
/// - https://dalamud.dev/
/// - https://dalamud.dev/api/
/// 调整 PluginService、IDataManager、IClientState、IFramework 等接口前，先对照上述文档。
/// </summary>
public sealed class DalamudApi
{
    public static void Initialize(IDalamudPluginInterface pluginInterface)
        => pluginInterface.Create<DalamudApi>();

    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static Dalamud.Plugin.Services.IDataManager GameData { get; private set; } = null!;
    [PluginService] public static Dalamud.Plugin.Services.IClientState ClientState { get; private set; } = null!;
    [PluginService] public static Dalamud.Plugin.Services.IFramework Framework { get; private set; } = null!;
    [PluginService] public static Dalamud.Plugin.Services.ICondition Conditions { get; private set; } = null!;
    [PluginService] public static Dalamud.Plugin.Services.IGameInteropProvider Interop { get; private set; } = null!;
    [PluginService] public static Dalamud.Plugin.Services.IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static Dalamud.Plugin.Services.IPluginLog Log { get; private set; } = null!;
    [PluginService] public static Dalamud.Plugin.Services.IPartyList PartyList { get; private set; } = null!;
    [PluginService] public static Dalamud.Plugin.Services.IBuddyList BuddyList { get; private set; } = null!;

    public static uint GetTerritoryTypeId()
        => TryGetUInt32Property(ClientState, "TerritoryType", "TerritoryTypeId", "CurrentTerritoryType");

    public static string? GetLocalPlayerName()
    {
        var localPlayer = GetPropertyValue(ClientState, "LocalPlayer");
        var name = GetPropertyValue(localPlayer, "Name");

        return GetPropertyValue(name, "TextValue") as string
               ?? name?.ToString();
    }

    public static uint GetLocalPlayerActorId()
    {
        var localPlayer = GetPropertyValue(ClientState, "LocalPlayer");
        return TryGetUInt32Property(localPlayer, "EntityId", "ObjectId");
    }

    public static uint GetLocalPlayerClassJobId()
    {
        var localPlayer = GetPropertyValue(ClientState, "LocalPlayer");
        var classJob = GetPropertyValue(localPlayer, "ClassJob");
        return TryGetUInt32Property(classJob, "RowId");
    }

    private static uint TryGetUInt32Property(object? instance, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = GetPropertyValue(instance, propertyName);
            if (TryConvertToUInt32(value, out var result))
                return result;
        }

        return 0;
    }

    private static object? GetPropertyValue(object? instance, string propertyName)
        => instance?.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance);

    private static bool TryConvertToUInt32(object? value, out uint result)
    {
        try
        {
            if (value == null)
            {
                result = 0;
                return false;
            }

            result = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            result = 0;
            return false;
        }
    }
}
