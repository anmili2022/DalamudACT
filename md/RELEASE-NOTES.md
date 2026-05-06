# DalamudACT 0.15.2.5 Release Notes

- Current release version: `0.15.2.5`
- This file is used as the GitHub Release body by `.github/workflows/release.yml`.

## Highlights

- Added a `Lock floating window` option under window settings.
- When locked, the floating window can no longer be moved or resized.
- Metric and history table headers are disabled while locked, so the user's current column widths stay in place and can no longer be dragged.
- All settings sections now default collapsed except for window settings and data/status.
- The synthetic `零式测试场` sample now contains eight characters for full-party testing.
- README handoff links now point at the current release handoff entry for maintainers.

## Notes

- This is a UI and maintainability patch release.
- Stats calculation behavior is unchanged in this release.
- `repo.json`, both plugin manifests, and the assembly version are all synchronized to `0.15.2.5`.

## Post-release checks

1. Confirm GitHub created `DalamudACT 0.15.2.5`.
2. Confirm the release contains `DalamudACT.zip`.
3. Confirm the release body renders this file correctly.
4. Confirm all three `repo.json` download links point to `0.15.2.5`.
