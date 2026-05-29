using System;
using System.Globalization;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

internal static class ActorIdentityAccessor
{
    private const uint InvalidActorId = 0xE0000000;

    public static bool IsUsableActorId(uint actorId)
        => actorId is not 0 and not InvalidActorId;

    public static uint NormalizeActorId(uint actorId)
        => IsUsableActorId(actorId) ? actorId : 0;

    public static uint NormalizeActorId(ulong actorId)
        => NormalizeActorId(unchecked((uint)(actorId & uint.MaxValue)));

    public static ulong GetGameObjectId(IGameObject? gameObject)
    {
        if (gameObject == null)
            return 0UL;

        try
        {
            return Convert.ToUInt64(gameObject.GameObjectId, CultureInfo.InvariantCulture);
        }
        catch
        {
            return 0UL;
        }
    }

    public static uint GetBestActorId(IGameObject? gameObject)
    {
        if (gameObject == null)
            return 0;

        var actorId = NormalizeActorId(GetGameObjectId(gameObject));
        if (actorId != 0)
            return actorId;

        var objectId = GetReflectedActorId(gameObject, "ObjectId");
        if (objectId != 0)
            return objectId;

        return NormalizeActorId(gameObject.EntityId);
    }

    public static uint GetObjectOrEntityId(IGameObject? gameObject)
    {
        var objectId = GetReflectedActorId(gameObject, "ObjectId");
        return objectId != 0 ? objectId : NormalizeActorId(gameObject?.EntityId ?? 0);
    }

    public static uint GetReflectedActorId(object? instance, params string[] propertyNames)
    {
        if (instance == null)
            return 0;

        var type = instance.GetType();
        foreach (var propertyName in propertyNames)
        {
            try
            {
                var property = ReflectionPropertyCache.GetProperty(type, propertyName);
                var rawValue = property?.GetValue(instance);
                if (rawValue == null)
                    continue;

                var actorId = NormalizeActorId(Convert.ToUInt64(rawValue, CultureInfo.InvariantCulture));
                if (actorId != 0)
                    return actorId;
            }
            catch
            {
                return 0;
            }
        }

        return 0;
    }

    public static bool MatchesActorId(IGameObject? gameObject, uint actorId)
    {
        actorId = NormalizeActorId(actorId);
        if (gameObject == null || actorId == 0)
            return false;

        var gameObjectActorId = NormalizeActorId(GetGameObjectId(gameObject));
        if (gameObjectActorId != 0 && gameObjectActorId == actorId)
            return true;

        var objectId = GetReflectedActorId(gameObject, "ObjectId");
        if (objectId != 0 && objectId == actorId)
            return true;

        var entityId = NormalizeActorId(gameObject.EntityId);
        return entityId != 0 && entityId == actorId;
    }
}
