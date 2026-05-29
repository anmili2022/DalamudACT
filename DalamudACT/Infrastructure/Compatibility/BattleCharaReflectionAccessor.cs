using System;
using System.Globalization;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

internal static class BattleCharaReflectionAccessor
{
    public static bool IsLikelyHostileBattleNpc(object battleChara)
    {
        var objectKind = GetValue(battleChara, "ObjectKind")?.ToString();
        if (!string.Equals(objectKind, "BattleNpc", StringComparison.OrdinalIgnoreCase))
            return false;

        var subKind = GetValue(battleChara, "SubKind")?.ToString();
        return string.IsNullOrWhiteSpace(subKind)
               || subKind.Contains("Enemy", StringComparison.OrdinalIgnoreCase)
               || subKind.Contains("BattleNpc", StringComparison.OrdinalIgnoreCase)
               || subKind == "5";
    }

    public static uint GetCastingActionId(object battleChara)
    {
        if (GetValue(battleChara, "IsCasting") is not true)
            return 0;

        return GetUInt32(battleChara, "CastActionId", "CastActionID", "CurrentCastActionId", "CurrentCastId");
    }

    public static uint GetActorId(object battleChara)
    {
        if (battleChara is IGameObject gameObject)
            return ActorIdentityAccessor.GetObjectOrEntityId(gameObject) is var objectOrEntityId && objectOrEntityId != 0
                ? objectOrEntityId
                : ActorIdentityAccessor.GetBestActorId(gameObject);

        var entityId = GetUInt32(battleChara, "EntityId", "ObjectId");
        return entityId != 0 ? entityId : ActorIdentityAccessor.NormalizeActorId(GetUInt64(battleChara, "GameObjectId"));
    }

    public static uint GetUInt32(object? instance, params string[] propertyNames)
    {
        var value = GetValue(instance, propertyNames);
        try
        {
            return value == null ? 0 : Convert.ToUInt32(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0;
        }
    }

    private static ulong GetUInt64(object? instance, params string[] propertyNames)
    {
        var value = GetValue(instance, propertyNames);
        try
        {
            return value == null ? 0UL : Convert.ToUInt64(value, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0UL;
        }
    }

    private static object? GetValue(object? instance, params string[] propertyNames)
    {
        if (instance == null)
            return null;

        var type = instance.GetType();
        foreach (var propertyName in propertyNames)
        {
            try
            {
                var property = ReflectionPropertyCache.GetProperty(type, propertyName);
                if (property == null)
                    continue;

                return property.GetValue(instance);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}
