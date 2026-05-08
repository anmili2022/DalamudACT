# DalamudACT {{VERSION}} Release Notes

- Current release version: `{{VERSION}}`
- This file is used as the GitHub Release body template by `.github/workflows/release.yml`.
- Keep the `{{VERSION}}` placeholder unchanged; the workflow replaces it with the actual tag version when publishing.

## Highlights

- Removed the direct dependency on `PronounModule.ResolvePlaceholder(...)` so runtime signature changes no longer crash the plugin.
- Reworked `ActionEffect` source resolution to use `sourceId + sourceCharacter`, then aligned it with the unified local actor identity model.
- Fixed the issue where combat could start normally but live DPS/HPS data stayed empty.
- The floating stats window now keeps the last encounter visible after combat ends, and clears only when the next combat actually starts.
- Empty-state messaging now distinguishes between waiting for the next combat and collecting fresh data after a new combat has already started.
- Unified the plugin assembly version, manifest version, and repo metadata version to `0.15.2.8` so the plugin window and plugin manager show the same version.
- Verified the local build and in-game data path after the fix.

## Notes

- This release line currently focuses on crash recovery, live combat data recovery, floating-window state behavior, and version alignment.
- The release workflow now substitutes the actual tag version into this file before creating the GitHub Release body.
- Before the next release, update the highlights and notes in this file so the generated release body matches the new changeset.

## Post-release checks

1. Confirm GitHub created `DalamudACT {{VERSION}}`.
2. Confirm the release contains `DalamudACT.zip`.
3. Confirm the release body shows the correct version and current highlights.
4. Confirm the packaged assembly and plugin manifest versions both match `{{VERSION}}`.
