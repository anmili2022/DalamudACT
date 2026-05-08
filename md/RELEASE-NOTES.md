# DalamudACT {{VERSION}} Release Notes

- Current release version: `{{VERSION}}`
- This file is used as the GitHub Release body template by `.github/workflows/release.yml`.
- Keep the `{{VERSION}}` placeholder unchanged; the workflow replaces it with the actual tag version when publishing.

## Highlights

- Reworked the floating stats tables so fixed columns keep their own width memory by semantic slot:
  - Player
  - Job
  - Damage / Heal / Taken total
  - DPS / HPS / DTPS value
  - Deaths
- Hiding a stats column now hides the entire column instead of only hiding its cell content.
- The share/progress column stretches to absorb remaining width, reducing layout distortion when fixed columns are hidden.
- The settings window title displays the currently loaded plugin assembly version for easier build verification.
- The deaths column keeps a hard minimum width of `20px`.

## Notes

- This release line currently focuses on floating stats layout, column-width persistence, and release maintenance.
- The release workflow now substitutes the actual tag version into this file before creating the GitHub Release body.
- Before the next release, update the highlights and notes in this file so the generated release body matches the new changeset.

## Post-release checks

1. Confirm GitHub created `DalamudACT {{VERSION}}`.
2. Confirm the release contains `DalamudACT.zip`.
3. Confirm the release body shows the correct version and current highlights.
4. Confirm the packaged assembly and plugin manifest versions both match `{{VERSION}}`.
