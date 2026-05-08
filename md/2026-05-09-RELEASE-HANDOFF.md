# 2026-05-09 Release Handoff

## Release target

- Release version: `0.15.2.8`
- Repository: `https://github.com/anmili2022/DalamudACT`
- Main branch: `main`
- Official release workflow: `.github/workflows/release.yml`

## What changed in 0.15.2.8

### Crash recovery and live data

- Removed the direct dependency on `PronounModule.ResolvePlaceholder("<1>..<8>")`.
- Reworked `ActionEffect` source resolution to use `sourceId + sourceCharacter`.
- Unified tracked-actor resolution around the local identity model in `LocalStatsService`.
- Fixed the issue where combat could start normally but live DPS/HPS data stayed empty.

### Floating window behavior

- The floating stats window now keeps the last encounter visible after combat ends.
- The next combat clears the old floating-window data first, then waits for new live combat events.
- Empty-state messaging now distinguishes between:
  - waiting for the next combat
  - collecting fresh combat data after a new combat has already started

### Version alignment and documentation

- Unified the assembly version, plugin manifests, and repository metadata to `0.15.2.8`.
- Updated `md/CHANGELOG.md`, `md/RELEASE-NOTES.md`, `HANDOVER.md`, and `md/SESSION-HANDOFF.md`.
- Added this release handoff entry for the `0.15.2.8` release line.

## Files that must stay in sync

- `DalamudACT/DalamudACT.csproj`
- `DalamudACT/DalamudACT.json`
- `Data/DalamudACT.json`
- `repo.json`
- `md/CHANGELOG.md`
- `md/RELEASE-NOTES.md`

## Verified local build commands

```powershell
dotnet build E:\git\DalamudACT\DalamudACT.sln
dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Release -p:Version=0.15.2.8 -p:FileVersion=0.15.2.8 -p:AssemblyVersion=0.15.2.8
```

Expected result:

- `0 warnings`
- `0 errors`

Output directory:

- `E:\git\DalamudACT\output\`

## Release procedure

1. Commit the code and documentation changes to `main`.
2. Push `main` to `origin`.
3. Create an unsigned annotated tag for the release:

```powershell
git -C E:\git\DalamudACT -c tag.gpgSign=false tag -a 0.15.2.8 -m "DalamudACT 0.15.2.8"
git -C E:\git\DalamudACT push origin 0.15.2.8
```

4. Let `.github/workflows/release.yml` rebuild the package and create the GitHub Release.

## Republish an existing formal tag

Use this when the formal tag already exists, but you need GitHub Actions to rebuild or recreate the release for that same tag.

1. Open `Create Release` in GitHub Actions.
2. Click `Run workflow`.
3. Enter the exact existing tag in the `tag` input, for example `0.15.2.8`.
4. Let `.github/workflows/release.yml` rebuild and upload `DalamudACT.zip`.
5. Re-check the Release page for that tag.

Do not use `Re-run jobs` on an old failed run if the workflow file changed after that run was created.

- A re-run still uses the original workflow snapshot and original `GITHUB_SHA`.
- If that old run still referenced stale package paths, the re-run will fail the same way.
- Prefer `workflow_dispatch` with the existing tag when the goal is to republish.

## Quick release shortcut

Use this shortcut when the workflow is already known-good and you only need a normal patch release.

```powershell
$ver = "0.15.2.8"

# 1. Update version references in:
#    DalamudACT.csproj / both manifests / repo.json / CHANGELOG / RELEASE-NOTES

dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Release -p:Version=$ver -p:FileVersion=$ver -p:AssemblyVersion=$ver
git -C E:\git\DalamudACT add .
git -C E:\git\DalamudACT commit -m "chore: release $ver"
git -C E:\git\DalamudACT push origin main
git -C E:\git\DalamudACT -c tag.gpgSign=false tag -a $ver -m "DalamudACT $ver"
git -C E:\git\DalamudACT push origin $ver
```

Quick checks:

- `repo.json` download links must already point to `$ver`
- the tag must exactly match `$ver`
- the Release page should contain `DalamudACT.zip`

## Why the unsigned tag command matters

- This machine has previously had `tag.gpgSign=true`.
- A plain `git tag 0.15.2.8` may block on signing.
- Use `-c tag.gpgSign=false` for the release tag unless signing has been intentionally reconfigured.

## Post-release checks

1. Confirm GitHub shows tag `0.15.2.8`.
2. Confirm the Release title is `DalamudACT 0.15.2.8`.
3. Confirm the attached asset includes `DalamudACT.zip`.
4. Confirm the three `repo.json` download links all point to the same `0.15.2.8` tag.
5. Confirm the in-game plugin version displays `0.15.2.8`.
6. Confirm the floating window behavior matches the intended flow:
   - end combat: previous encounter stays visible
   - start next combat: stale data clears first
   - first new event: live data appears again

## Read this first when taking over

1. [HANDOVER.md](../HANDOVER.md)
2. [RELEASE-RUNBOOK.md](RELEASE-RUNBOOK.md)
3. [CHANGELOG.md](CHANGELOG.md)
4. [RELEASE-NOTES.md](RELEASE-NOTES.md)
5. [发版最后检查清单](2026-05-09-RELEASE-CHECKLIST.md)
6. [发版极简清单](2026-05-09-RELEASE-CHECKLIST-ULTRA.md)
7. `DalamudACT/Stats/LocalStatsService.cs`
