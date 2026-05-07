# DalamudACT Handover

Updated: 2026-05-06

## Current Status

- Working directory: `E:\git\DalamudACT`
- Remote: `origin = https://github.com/anmili2022/DalamudACT`
- Main maintenance branch: `master`
- Current branch at handover time: `master`
- Latest verified automation baseline commit: `b1324c9`
- Working tree was clean when the automation baseline above was verified
- Current metadata version: `0.15.2.5`
- Verified local build:
  - Debug: `dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Debug`
  - Release: `dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Release -p:Version=0.15.2.5 -p:FileVersion=0.15.2.5 -p:AssemblyVersion=0.15.2.5`
  - Result: `0 warnings / 0 errors`
  - Output: `E:\git\DalamudACT\output\DalamudACT.dll`

## Automation Status

This repository is a fork of `flyrio/DalamudACT`.

GitHub Actions for this fork have now been verified working on `2026-05-06`.

Verified runs:

- `.github/workflows/build.yml`
  - Trigger check commit: `08c4c30`
  - First observed run: `25427532514`
  - Initial result: workflow triggered correctly, but failed in `Archive`
  - Cause: workflow still archived `DalamudACT/bin/Release/*`, while the project now outputs to `output/`
- `.github/workflows/build.yml` fixed state
  - Fix commit: `b1324c9`
  - Successful run: `25427744876`
  - Result: `Build`, `Archive`, `Upload Artifact`, and `Update Latest Release` all succeeded
- `.github/workflows/release.yml`
  - Verified via `workflow_dispatch` with existing tag `0.15.2.3`
  - Successful run: `25427944539`
  - Result: full release workflow succeeded, including zip packaging and GitHub Release update

Practical conclusion:

- branch builds are now usable again
- official release workflow is now usable again
- the current release automation path no longer needs the earlier manual-only fallback for normal cases

## What Changed In This Handover Window

- released `0.15.2.5`
- confirmed `repo.json`, manifests, and assembly version all point to `0.15.2.5`
- added a floating window lock option in settings
- locking now prevents moving or resizing the floating window
- table column resize handles are disabled while locked, preserving the user's current widths
- settings sections now default collapsed except for window settings and data/status
- synthetic raid test data now uses an eight-character `零式测试场` sample
- adjusted floating window collapse/expand behavior to use the compact DPS tab state consistently
- fixed the UI gating that could still show `等待战斗数据...` after a combat snapshot already existed
- verified local `Release` packaging path remains `output/`
- fixed `.github/workflows/build.yml` to archive from `output/` instead of the old `bin/Release` path
- verified `.github/workflows/release.yml` can successfully rebuild and publish for tag `0.15.2.3`

## What This Repo Is Now

`DalamudACT` is no longer a thin shell around an external overlay.

The current project direction is:

- collect combat events inside Dalamud
- compute local ACTX-style combat statistics in the plugin
- render DPS, HPS, damage taken, overview, and encounter history directly in game
- keep the UI and release artifacts maintained in this repo

## Current Version Scope

The latest checked-in metadata version is `0.15.2.5`.

Recent changes already reflected in the repo:

- `0.15.2.5`
  - added a floating window lock option under window settings
  - locked floating windows cannot be moved or resized
  - current table widths remain in place and header dragging is disabled while locked
  - settings sections now default collapsed except for window settings and data/status
  - synthetic raid test data now covers eight characters in `零式测试场`

- `0.15.2.4`
  - floating window collapsed state now stays in the compact DPS tab flow
  - clicking the compact DPS tab expands directly into live data
  - combat snapshot presence is now enough to display stats instead of staying on the waiting text
  - README includes maintainer handoff links for direct navigation
- `0.15.2.3`
  - maintenance handover entry added at repo root
  - README now links maintainers directly to the handover entry points
  - branch build workflow fixed to archive from `output/`
  - GitHub Actions branch build and formal release workflow both verified on this fork
- `0.15.2.2`
  - floating window defaults to collapsed on plugin load
  - floating window default expanded size changed to `300x300`
  - clicking the waiting text toggles collapsed and expanded state
  - release workflow hardened to read UTF-8 release notes correctly
- `0.15.2.1`
  - NPC and trust party member tracking fixes
  - encounter history and real-time view switching improvements
  - main window version display added
- `0.15.2.0`
  - settings split and encounter-end configuration work
  - history import/export and related UI improvements

For the full release history, see [md/CHANGELOG.md](md/CHANGELOG.md).

## Key Files

Build and metadata:

- `DalamudACT/DalamudACT.csproj`
- `DalamudACT/DalamudACT.json`
- `Data/DalamudACT.json`
- `repo.json`

Main runtime and UI areas:

- `DalamudACT/Plugin/ACT.cs`
- `DalamudACT/DalamudApi.cs`
- `DalamudACT/Stats/LocalStatsService.cs`
- `DalamudACT/UI/MainWindow.cs`
- `DalamudACT/UI/FloatingStatsWindow.cs`
- `DalamudACT/UI/StatsPanel.cs`
- `DalamudACT/UI/SettingsWindow.cs`

Release automation:

- `.github/workflows/release.yml`
- `.github/workflows/test_release.yml`
- `.github/workflows/build.yml`

Current automation note:

- `build.yml` now packages `output/DalamudACT.dll`, `output/DalamudACT.json`, and `output/DalamudACT.deps.json`
- `release.yml` has been validated successfully after Actions were enabled for this fork

## Release Entry Points

Official release flow:

1. Update code and docs.
2. Sync the version across:
   - `DalamudACT/DalamudACT.csproj`
   - `DalamudACT/DalamudACT.json`
   - `Data/DalamudACT.json`
   - `repo.json`
   - `md/CHANGELOG.md`
   - `md/RELEASE-NOTES.md`
3. Run `dotnet build E:\git\DalamudACT\DalamudACT.sln`
4. Push `master`
5. Create and push the formal version tag
6. Let `.github/workflows/release.yml` create the GitHub Release

Existing tag republish flow:

Use this when the version tag already exists on GitHub, but you still need GitHub Actions to rebuild or publish that exact tag again.

1. Open the `Create Release` workflow in GitHub Actions.
2. Use `Run workflow`.
3. Fill the `tag` input with the existing formal tag, for example `0.15.2.6`.
4. Let `.github/workflows/release.yml` rebuild and upload `DalamudACT.zip`.
5. Confirm the Release page for that tag now contains the expected asset.

Important:

- do not use `Re-run jobs` on an old failed release run if the workflow file has changed since then
- GitHub re-runs the old workflow against the original `GITHUB_SHA` and `GITHUB_REF`
- if the old run used a stale workflow snapshot, the same stale packaging logic will run again
- when in doubt, use `workflow_dispatch` with the exact existing tag instead

Quick release flow for normal patch releases:

Use this when:

- release automation is already healthy
- this is a normal version bump, not a workflow repair session
- you only need the shortest trusted path from local changes to GitHub Release

1. Set the target version once, for example `0.15.2.6`.
2. Update these files to that exact version:
   - `DalamudACT/DalamudACT.csproj`
   - `DalamudACT/DalamudACT.json`
   - `Data/DalamudACT.json`
   - `repo.json`
   - `md/CHANGELOG.md`
   - `md/RELEASE-NOTES.md`
3. Run the release build locally:

```powershell
$ver = "0.15.2.6"
dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Release -p:Version=$ver -p:FileVersion=$ver -p:AssemblyVersion=$ver
```

4. Commit and push `master`:

```powershell
git -C E:\git\DalamudACT status --short
git -C E:\git\DalamudACT add .
git -C E:\git\DalamudACT commit -m "chore: release $ver"
git -C E:\git\DalamudACT push origin master
```

5. Create and push the release tag:

```powershell
git -C E:\git\DalamudACT -c tag.gpgSign=false tag -a $ver -m "DalamudACT $ver"
git -C E:\git\DalamudACT push origin $ver
```

6. Confirm GitHub created the Release and attached `DalamudACT.zip`.

Testing release flow:

Use this only for `testing_*` tags, not for a formal release.

1. Confirm `.github/workflows/test_release.yml` on the target commit is the current version that packages from `output/`.
2. Create a testing tag from the commit you want to validate, for example `testing_0.15.2.6`.
3. Push that testing tag.
4. Let `.github/workflows/test_release.yml` build the plugin, create the test release zip, and update the testing fields in `repo.json`.
5. Verify the testing release page and confirm `repo.json` points its testing download link at the same `testing_*` tag.

Shortcut reminder:

- do not use `testing_*`
- do not use `latest`
- do not skip `repo.json`
- do not use plain `git tag` on this machine if signing blocks
- keep the tag name exactly equal to the release version

Current verified state:

- branch build flow: verified
- formal release flow by existing tag and `workflow_dispatch`: verified
- manual fallback release creation is still worth keeping in the runbook, but it is no longer the only trusted path

Important workflow roles:

- `.github/workflows/release.yml`: official release by version tag
- `.github/workflows/test_release.yml`: testing tag flow only
- `.github/workflows/build.yml`: branch/nightly style build flow

This machine has previously needed unsigned tag creation. If tag signing blocks release, see [md/RELEASE-RUNBOOK.md](md/RELEASE-RUNBOOK.md) and [md/2026-05-06-RELEASE-HANDOFF.md](md/2026-05-06-RELEASE-HANDOFF.md).

## What To Read First

If you are taking over maintenance, read in this order:

1. [HANDOVER.md](HANDOVER.md)
2. [md/2026-05-06-RELEASE-HANDOFF.md](md/2026-05-06-RELEASE-HANDOFF.md)
3. [md/RELEASE-RUNBOOK.md](md/RELEASE-RUNBOOK.md)
4. [md/SESSION-HANDOFF.md](md/SESSION-HANDOFF.md)
5. [md/CHANGELOG.md](md/CHANGELOG.md)

If you are debugging runtime behavior, prioritize:

1. `DalamudACT/Stats/LocalStatsService.cs`
2. `DalamudACT/Plugin/ACT.cs`
3. `DalamudACT/DalamudApi.cs`
4. `DalamudACT/UI/FloatingStatsWindow.cs`
5. `DalamudACT/UI/StatsPanel.cs`

## Notes

- The repo was clean at the time this handover document was created.
- The currently trusted local artifact path is `output\DalamudACT.dll`.
- The currently trusted branch build fix is in commit `b1324c9`.
- The first successful branch build run is `25427744876`.
- The verified successful formal release workflow run is `25427944539`.
- Existing detailed day-by-day notes are already stored under `md/`; this file is intended to be the short maintainer entry point, not a replacement for those records.
