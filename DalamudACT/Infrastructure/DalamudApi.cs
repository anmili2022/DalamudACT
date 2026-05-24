using System;
using System.Globalization;
using System.Reflection;
using Dalamud.Game.ClientState.Objects.Types;
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
    [PluginService] public static Dalamud.Plugin.Services.IChatGui ChatGui { get; private set; } = null!;
    [PluginService] public static Dalamud.Plugin.Services.IPartyList PartyList { get; private set; } = null!;
    [PluginService] public static Dalamud.Plugin.Services.IBuddyList BuddyList { get; private set; } = null!;
    [PluginService] public static Dalamud.Plugin.Services.ITextureProvider TextureProvider { get; private set; } = null!;

    public static uint GetTerritoryTypeId()
        => TryGetUInt32Property(ClientState, "TerritoryType", "TerritoryTypeId", "CurrentTerritoryType");

    public static string? GetLocalPlayerName()
    {
        var localPlayer = GetLocalPlayerObject();
        var name = GetPropertyValue(localPlayer, "Name");

        return GetPropertyValue(name, "TextValue") as string
               ?? name?.ToString();
    }

    public static string? GetCurrentTargetName()
    {
        var localPlayer = GetLocalPlayerObject();
        var target = GetPropertyValue(localPlayer, "TargetObject");
        var name = GetPropertyValue(target, "Name");

        return GetPropertyValue(name, "TextValue") as string
               ?? name?.ToString();
    }

    public static ulong GetLocalPlayerGameObjectId()
    {
        var localPlayer = GetLocalPlayerObject();
        return TryGetUInt64Property(localPlayer, "GameObjectId");
    }

    public static uint GetLocalPlayerEntityId()
    {
        var localPlayer = GetLocalPlayerObject();
        return TryGetUInt32Property(localPlayer, "EntityId");
    }

    public static uint GetLocalPlayerObjectId()
    {
        var localPlayer = GetLocalPlayerObject();
        return TryGetUInt32Property(localPlayer, "ObjectId", "EntityId");
    }

    public static uint GetLocalPlayerActorId()
    {
        var gameObjectId = GetLocalPlayerGameObjectId();
        if (gameObjectId != 0)
            return unchecked((uint)(gameObjectId & uint.MaxValue));

        var entityId = GetLocalPlayerEntityId();
        if (entityId != 0)
            return entityId;

        return GetLocalPlayerObjectId();
    }

    public static uint GetLocalPlayerClassJobId()
    {
        var localPlayer = GetLocalPlayerObject();
        var classJob = GetPropertyValue(localPlayer, "ClassJob");
        var rowId = TryGetRowId(classJob);
        if (rowId != 0)
            return rowId;

        return 0;
    }

    public static uint GetLocalPlayerMaxHp()
    {
        var localPlayer = GetLocalPlayerObject();
        return TryGetUInt32Property(localPlayer, "MaxHp");
    }

    public static IBattleChara? GetLocalPlayerBattleChara()
        => GetLocalPlayerObject() as IBattleChara;

    public static bool TryGetLocalPlayerInfo(out uint actorId, out string name, out uint classJobId, out IBattleChara? battleChara)
    {
        battleChara = GetLocalPlayerBattleChara();
        actorId = GetLocalPlayerActorId();
        name = GetLocalPlayerName()?.Trim() ?? string.Empty;
        classJobId = GetLocalPlayerClassJobId();

        return actorId != 0 && !string.IsNullOrWhiteSpace(name) && classJobId != 0;
    }

    private static object? GetLocalPlayerObject()
    {
        var objectTableLocalPlayer = GetPropertyValue(ObjectTable, "LocalPlayer");
        if (objectTableLocalPlayer != null)
            return objectTableLocalPlayer;

        return GetPropertyValue(ClientState, "Pc")
               ?? GetPropertyValue(ClientState, "LocalPlayer");
    }

    private static uint TryGetRowId(object? rowRefOrSheetRow)
    {
        var direct = TryGetUInt32Property(rowRefOrSheetRow, "RowId");
        if (direct != 0)
            return direct;

        var value = GetPropertyValue(rowRefOrSheetRow, "Value");
        return TryGetUInt32Property(value, "RowId");
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

    private static ulong TryGetUInt64Property(object? instance, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = GetPropertyValue(instance, propertyName);
            if (TryConvertToUInt64(value, out var result))
                return result;
        }

        return 0UL;
    }

    private static object? GetPropertyValue(object? instance, string propertyName)
    {
        try
        {
            return instance?.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)?.GetValue(instance);
        }
        catch
        {
            return null;
        }
    }

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

    private static bool TryConvertToUInt64(object? value, out ulong result)
    {
        try
        {
            if (value == null)
            {
                result = 0;
                return false;
            }

            result = Convert.ToUInt64(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            result = 0;
            return false;
        }
    }
}
