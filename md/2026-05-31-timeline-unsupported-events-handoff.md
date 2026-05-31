# 2026-05-31 Timeline Unsupported Events Handoff

## Context

- Workspace: `E:\git\DalamudACT`
- Timeline source data hardcoded path: `E:\git\DalamudACT\DalamudACT\Features\Timeline\Data`
- Do not change the hardcoded timeline source data path unless the maintainer explicitly asks for it in the same session.
- Current goal completed in this pass:
  - Parser compatibility for `forcejump`, source arrays, and single-quoted ids.
  - Runtime `StartsUsing` sync using the existing stable `IBattleChara.IsCasting` framework polling path.
- Explicitly skipped for now:
  - Timeline `InCombat` event rows as sync/reset triggers.
  - The remaining event types listed below.
- Do not re-enable the old Cast Hook or ActorControl Hook without a fresh stability review.

## Supported After This Pass

- `Ability`
  - Runtime sync via ActionEffect hook.
  - Now filtered to `EventType == "Ability"` so `StartsUsing` rows with the same id do not collide.

- `StartsUsing`
  - Runtime sync via framework polling of hostile `IBattleChara.IsCasting`.
  - Supports `source: "..."` and `source: ["...", "..."]`.
  - Source match tries exact case-insensitive name first, then treats the timeline source as a regex.
  - Uses the same drift protection as ability sync.

- `SystemLogMessage`
  - Runtime sync via Dalamud chat/log message path.
  - Existing language-independent Lumina `LogMessage.ToMacroString()` and `PlaceName` param1 logic remains the correct approach.

- `MapEffect`
  - Runtime sync via MapEffect hook.

- `Timer`
  - Display/TTS only, not a game-event trigger.

## Parser Compatibility Added

- `forcejump` is parsed like `jump`.
- `id` supports single quotes, double quotes, and backticks.
- `id: [...]` supports mixed quote styles.
- `param1` supports single quotes, double quotes, and backticks.
- `source` supports single-value and array forms.

## Still Unsupported Or Incomplete

### InCombat Timeline Rows

Example:

```txt
0.0 "--sync--" InCombat { inGameCombat: "1" } window 0,1
0.0 "--Reset--" InCombat { inGameCombat: "0" } jump 0
```

Current state:

- Parser recognizes `InCombat` as an event type.
- Runtime does not currently match timeline `InCombat` rows as triggers.
- Combat start/stop still controls internal timeline running state directly in `TimelineService.Update()`.

Future implementation notes:

- Add explicit handling for `inGameCombat: "1"` and `inGameCombat: "0"` rows.
- Be careful with `inGameCombat: "0" jump 0`: it can help wipe/reset behavior, but may also fire on normal kill/zone transitions if not guarded.
- Consider only allowing reset behavior for hidden/internal rows or rows with explicit `jump 0`.

### ActorControl

Example:

```txt
0.0 "--Reset--" ActorControl { command: "4000000F" } window 0,100000 jump 0
```

Current state:

- Parser recognizes `ActorControl`.
- Runtime hook is intentionally disabled: `ShouldInstallActorControlHook => false`.
- These rows do not trigger today.

Future implementation notes:

- Do not simply flip the hook back on. It was disabled because of startup crash risk.
- If this becomes necessary, prefer a safer hook install path, extra signature validation, and a config-gated diagnostic mode first.

### AddedCombatant

Examples seen in existing timelines:

```txt
AddedCombatant { npcBaseId: "18701" }
AddedCombatant { name: "Specter Of The Patriarch" }
```

Current state:

- Parser recognizes `AddedCombatant` as an event type.
- No runtime source currently matches ObjectTable additions or NPC spawn rows against timeline entries.

Future implementation notes:

- Add parsed fields for `npcBaseId` and `name` to `TimelineEntry`.
- Runtime options:
  - Poll ObjectTable and detect new object ids within combat.
  - Reuse existing actor tracking if it already sees spawn-like transitions.
- Need dedupe by actor id/object id to avoid repeated sync every framework tick.

### HeadMarker

Example:

```txt
HeadMarker { id: "023D" }
HeadMarker { id: '019C' }
```

Current state:

- Parser does not recognize `HeadMarker`.
- Runtime has no head marker event source.

What it is for:

- Player target markers and mechanic markers above characters.
- Often used for high-end encounter branching, numbered markers, special target selection, spread/stack/death sentence identification, or phase correction.

Future implementation notes:

- Need a reliable Dalamud event or packet source for head marker id and target actor id.
- Add parser fields for marker `id`, optional target/source filters, and jump/window handling.
- Add dedupe because marker updates can repeat or be observed close together.

### Tether

Example:

```txt
Tether { id: "0175", source: "模仿细胞" }
```

Current state:

- Parser does not recognize `Tether`.
- Runtime has no tether event source.

What it is for:

- Lines between player-player, player-NPC, or NPC-NPC.
- Used by timelines to branch or sync when a mechanic is visually assigned by a tether rather than by an ability cast.

Future implementation notes:

- Need event/packet data containing tether id, source actor, and target actor.
- Source/target filters should support exact name and regex, consistent with `StartsUsing` source matching.
- Dedupe by `sourceId:targetId:tetherId`.

### GainsEffect

Example:

```txt
GainsEffect { effectId: "1043" }
```

Current state:

- Parser does not recognize `GainsEffect`.
- Runtime has status polling for combat timeline recording, but not timeline event matching.

What it is for:

- A player or actor gaining a buff/debuff/status.
- Useful when mechanics are assigned by statuses rather than visible casts.

Future implementation notes:

- Reuse or adapt the existing status polling logic from `LocalStatsService.Encounter.Timeline.cs` if feasible.
- Add parsed fields for `effectId`, optional target/source/name filters, and maybe duration/count if existing timelines use them.
- Avoid matching historical statuses when the timeline first loads; prime the observed status set first.

### LosesEffect

Example:

```txt
LosesEffect { effectId: "1022" }
```

Current state:

- Parser does not recognize `LosesEffect`.
- Runtime does not detect status removals as timeline triggers.

What it is for:

- A buff/debuff/status falling off.
- Useful for mechanics where the next phase starts when an assignment expires or is cleansed/resolved.

Future implementation notes:

- Build on the same status observation cache used for `GainsEffect`.
- Removal should only trigger after a status was previously observed during the current encounter/timeline run.
- Guard against mass status cleanup on wipe, kill, zone change, or plugin reload.

## Remaining Parser Semantics To Consider Later

- `window x,y`
  - Current runtime does not fully execute cactbot-style window semantics.
  - Existing sync uses nearest candidate plus drift protection.
  - If implemented, store `WindowBeforeSeconds` and `WindowAfterSeconds` on `TimelineEntry` and apply per event type.

- Additional event fields
  - `npcBaseId`, `name`, `target`, `effectId`, and tether/headmarker-specific fields are not modeled yet.
  - Add fields only when implementing the matching runtime source for that event type.

## Suggested Priority

1. `InCombat` timeline rows, if wipe/reset behavior becomes a real pain point.
2. `AddedCombatant`, because it can be implemented with ObjectTable polling and helps spawn/phase sync.
3. `GainsEffect` / `LosesEffect`, because existing status polling already provides a partial foundation.
4. `HeadMarker`, once a reliable marker source is identified.
5. `Tether`, likely packet/event-source dependent and should come after raw event research.
6. `ActorControl`, only after resolving the previous crash risk.

## Verification Baseline

After the parser and `StartsUsing` work, this passed:

```txt
dotnet build --no-restore
0 warnings, 0 errors
```
