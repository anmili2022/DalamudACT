# DalamudACT {{VERSION}} Release Notes

- Current release version: `{{VERSION}}`
- This file is used as the GitHub Release body by `.github/workflows/release.yml`.
- Keep the `{{VERSION}}` placeholder unchanged; the workflow replaces it with the Git tag when publishing.

## Highlights

- Reworked the floating stats tables so fixed columns keep their own width memory by semantic slot:
  - Player
  - Job
  - Damage / Heal / Taken total
  - DPS / HPS / DTPS value
  - Deaths
- Hiding a stats column now hides the entire column instead of only hiding its cell content.
- The share/progress column now stretches to absorb remaining table width, reducing layout distortion when fixed columns are hidden.
- Added the plugin assembly version to the settings window title so loaded DLL verification is easier during testing.
- Enforced a 20px minimum width for the deaths column.

## Notes

- This patch mainly focuses on floating stats layout, column-width persistence, and release maintenance.
- The release workflow now substitutes the actual tag version into this file before creating the GitHub Release body.
- Before the next release, update the highlights/notes in this file so the body matches the new changeset.

## Post-release checks

1. Confirm GitHub created `DalamudACT {{VERSION}}`.
2. Confirm the release contains `DalamudACT.zip`.
3. Confirm the release body shows the correct version and current highlights.
4. Confirm the packaged assembly/plugin manifest versions match `{{VERSION}}`.
