using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;

namespace DalamudACT;

internal static partial class StatsPanel
{
    private static Vector4 ResolveBarColor(Combatant combatant, PluginConfiguration config)
    {
        if (config.HighlightNpcRows && TryParseFloatingCombatantKind(combatant.ParticipantKind, out var kind))
        {
            if (kind == FloatingCombatantKind.HostileNpc)
                return HostileNpcBarColor;

            if (kind == FloatingCombatantKind.FriendlyNpc)
            {
                if (config.BarColorMode == StatsBarColorMode.Single)
                    return config.GetSingleBarColor();

                return HasCombatantJob(combatant)
                    ? config.GetThemeBarColor(combatant.Job)
                    : FriendlyNpcBarColor;
            }
        }

        if (config.BarColorMode == StatsBarColorMode.Single)
            return config.GetSingleBarColor();

        return config.GetThemeBarColor(combatant.Job);
    }

    private static bool IsLocalPlayerCombatant(Combatant combatant)
    {
        var localPlayerName = DalamudApi.GetLocalPlayerName()?.Trim();
        return !string.IsNullOrWhiteSpace(localPlayerName)
               && string.Equals(combatant.Name?.Trim(), localPlayerName, StringComparison.Ordinal);
    }

    private static string ResolveCombatantDisplayName(Combatant combatant, PluginConfiguration config)
    {
        var name = combatant.Name ?? string.Empty;
        return config.HighlightSelfBar && IsLocalPlayerCombatant(combatant)
            ? $"★ {name}"
            : name;
    }

    private static bool TryResolveCombatantTextColor(Combatant combatant, PluginConfiguration config, out Vector4 color)
    {
        if (config.HighlightNpcRows && TryParseFloatingCombatantKind(combatant.ParticipantKind, out var kind))
        {
            if (kind == FloatingCombatantKind.FriendlyNpc)
            {
                color = default;
                return false;
            }

            if (kind == FloatingCombatantKind.HostileNpc)
            {
                color = HostileNpcTextColor;
                return true;
            }
        }

        color = default;
        return false;
    }

    private static bool TryResolveCombatantBarTextColor(Combatant combatant, PluginConfiguration config, out Vector4 color)
    {
        color = default;
        return false;
    }

    private static bool TryResolveCombatantRowBackgroundColor(Combatant combatant, PluginConfiguration config, out Vector4 color)
    {
        if (config.HighlightNpcRows && TryParseFloatingCombatantKind(combatant.ParticipantKind, out var kind))
        {
            if (kind == FloatingCombatantKind.FriendlyNpc)
            {
                color = default;
                return false;
            }

            if (kind == FloatingCombatantKind.HostileNpc)
            {
                color = HostileNpcRowBackgroundColor;
                return true;
            }
        }

        color = default;
        return false;
    }

    private static bool HasCombatantJob(Combatant combatant)
        => !string.IsNullOrWhiteSpace(combatant.Job)
           && !string.Equals(combatant.Job, "友方NPC", StringComparison.Ordinal)
           && !string.Equals(combatant.Job, "敌方NPC", StringComparison.Ordinal)
           && !string.Equals(combatant.Job, "NPC", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<Combatant> GetVisibleCombatants(CombatDataWrapper combatData, PluginConfiguration config)
        => GetVisibleCombatantRows(combatData, config)
            .Select(static row => row.Combatant)
            .ToList();

    private static IReadOnlyList<DisplayCombatantRow> GetVisibleCombatantRows(CombatDataWrapper combatData, PluginConfiguration config)
    {
        var combatants = combatData.Msg?.Combatant;
        if (combatants == null || combatants.Count == 0)
            return Array.Empty<DisplayCombatantRow>();

        var rows = combatants
            .Where(static pair => !string.IsNullOrWhiteSpace(pair.Value.Name))
            .Select(static pair => new DisplayCombatantRow(pair.Value, ResolveFloatingCombatantKind(pair.Value, ParseCombatantActorId(pair.Key))))
            .ToList();

        var playerCount = rows.Count(static row => row.Kind == FloatingCombatantKind.Player);
        rows = config.FloatingStatsParticipantDisplayMode switch
        {
            FloatingStatsParticipantDisplayMode.PlayersOnly => rows
                .Where(static row => row.Kind != FloatingCombatantKind.FriendlyNpc && row.Kind != FloatingCombatantKind.HostileNpc)
                .ToList(),
            FloatingStatsParticipantDisplayMode.PlayersAndFriendlyNpc => rows
                .Where(static row => row.Kind != FloatingCombatantKind.HostileNpc)
                .ToList(),
            FloatingStatsParticipantDisplayMode.PlayersAndHostileNpc => rows
                .Where(static row => row.Kind != FloatingCombatantKind.FriendlyNpc)
                .ToList(),
            _ when playerCount >= 2 => rows
                .Where(static row => row.Kind != FloatingCombatantKind.FriendlyNpc && row.Kind != FloatingCombatantKind.HostileNpc)
                .ToList(),
            _ => rows
                .Where(static row => row.Kind != FloatingCombatantKind.HostileNpc)
                .ToList(),
        };

        return rows;
    }

    private static FloatingCombatantKind ResolveFloatingCombatantKind(Combatant combatant, uint actorId)
    {
        if (TryParseFloatingCombatantKind(combatant.ParticipantKind, out var metadataKind))
            return metadataKind;

        if (actorId is 0 or InvalidActorId)
            return FloatingCombatantKind.Unknown;

        if (CombatantKindCache.TryGetValue(actorId, out var cachedKind))
            return cachedKind;

        var localPlayerActorIds = new[]
        {
            NormalizeActorId(DalamudApi.GetLocalPlayerActorId()),
            NormalizeActorId(DalamudApi.GetLocalPlayerObjectId()),
            NormalizeActorId(DalamudApi.GetLocalPlayerEntityId()),
        };
        if (localPlayerActorIds.Any(id => id != 0 && id == actorId))
            return CombatantKindCache[actorId] = FloatingCombatantKind.Player;

        var gameObject = FindObjectByActorId(actorId);
        if (gameObject == null)
            return FloatingCombatantKind.Unknown;

        if (gameObject is IPlayerCharacter)
            return CombatantKindCache[actorId] = FloatingCombatantKind.Player;

        if (gameObject is not IBattleNpc battleNpc)
            return FloatingCombatantKind.Unknown;

        return CombatantKindCache[actorId] = (battleNpc.StatusFlags & StatusFlags.Hostile) != 0
            ? FloatingCombatantKind.HostileNpc
            : FloatingCombatantKind.FriendlyNpc;
    }

    private static bool TryParseFloatingCombatantKind(string? participantKind, out FloatingCombatantKind kind)
    {
        kind = participantKind switch
        {
            "player" => FloatingCombatantKind.Player,
            "friendlyNpc" => FloatingCombatantKind.FriendlyNpc,
            "hostileNpc" => FloatingCombatantKind.HostileNpc,
            _ => FloatingCombatantKind.Unknown,
        };

        return kind != FloatingCombatantKind.Unknown;
    }

    private static uint ParseCombatantActorId(string? combatantKey)
    {
        if (string.IsNullOrWhiteSpace(combatantKey))
            return 0;

        var separatorIndex = combatantKey.LastIndexOf('#');
        if (separatorIndex < 0 || separatorIndex >= combatantKey.Length - 1)
            return 0;

        var actorText = combatantKey[(separatorIndex + 1)..];
        return uint.TryParse(actorText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var actorId)
            ? NormalizeActorId(actorId)
            : 0;
    }

    private static IGameObject? FindObjectByActorId(uint actorId)
    {
        foreach (var gameObject in DalamudApi.ObjectTable)
        {
            if (MatchesObjectActorId(gameObject, actorId))
                return gameObject;
        }

        return null;
    }

    private static bool MatchesObjectActorId(IGameObject? gameObject, uint actorId)
    {
        if (gameObject == null || actorId is 0 or InvalidActorId)
            return false;

        return ActorIdentityAccessor.MatchesActorId(gameObject, actorId);
    }

    private static uint NormalizeActorId(uint actorId)
        => actorId is 0 or InvalidActorId ? 0 : actorId;

    private static void DrawCombatantBarTooltip(
        Combatant combatant,
        string primaryLabel,
        string? primaryValue,
        string rateLabel,
        string? rateValue)
    {
        if (!ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        ImGui.TextUnformatted($"玩家: {FallbackText(combatant.Name, "-")}");
        ImGui.TextUnformatted($"职业: {FallbackText(combatant.Job, "-")}");
        ImGui.TextUnformatted($"{primaryLabel}: {FallbackText(primaryValue, "0")}");
        ImGui.TextUnformatted($"{rateLabel}: {FallbackText(rateValue, "0")}");
        ImGui.TextUnformatted($"死亡: {FallbackText(combatant.DeathsText, "0")}");
        var maxHitText = ResolveCombatantTooltipMaxHitText(combatant);
        if (!string.IsNullOrWhiteSpace(maxHitText))
            ImGui.TextUnformatted($"最高伤害: {maxHitText}");
        ImGui.EndTooltip();
    }

    private static string? ResolveCombatantTooltipMaxHitText(Combatant combatant)
    {
        if (string.IsNullOrWhiteSpace(combatant.MaxHitText) || combatant.MaxHitText == "--")
            return null;

        return combatant.MaxHitText;
    }
}
