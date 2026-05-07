# 2026-05-06 Release Handoff

## Release target

- Release version: `0.15.2.5`
- Repository: `https://github.com/anmili2022/DalamudACT`
- Main branch: `master`
- Official release workflow: `.github/workflows/release.yml`

## What changed in 0.15.2.5

### Floating window lock

- Added a `Lock floating window` option under window settings.
- When enabled, the floating window cannot be moved or resized.
- Metric and history table headers are disabled while locked, so the user's current column widths stay in place and can no longer be dragged.

### Settings defaults

- All settings sections now default collapsed except for window settings and data/status.

### Test data

- The synthetic `零式测试场` sample now contains eight characters for full-party testing.

### Documentation

- `README.md` links maintainers directly to the current handoff entry.
- `HANDOVER.md`, `md/CHANGELOG.md`, and `md/RELEASE-NOTES.md` were updated for `0.15.2.5`.

## Files that must stay in sync

- `DalamudACT/DalamudACT.csproj`
- `DalamudACT/DalamudACT.json`
- `Data/DalamudACT.json`
- `repo.json`
- `md/CHANGELOG.md`
- `md/RELEASE-NOTES.md`

## Verified local build commands

```powershell
dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Debug
dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Release -p:Version=0.15.2.5 -p:FileVersion=0.15.2.5 -p:AssemblyVersion=0.15.2.5
```

Expected result:

- `0 warnings`
- `0 errors`

Output directory:

- `E:\git\DalamudACT\output\`

## Release procedure

1. Commit the code and documentation changes to `master`.
2. Push `master` to `origin`.
3. Create an unsigned annotated tag for the release:

```powershell
git -C E:\git\DalamudACT -c tag.gpgSign=false tag -a 0.15.2.5 -m "DalamudACT 0.15.2.5"
git -C E:\git\DalamudACT push origin 0.15.2.5
```

4. Let `.github/workflows/release.yml` rebuild the release package and create the GitHub Release.

## Republish an existing formal tag

Use this when the formal tag already exists, but you need GitHub Actions to rebuild or recreate the release for that same tag.

1. Open `Create Release` in GitHub Actions.
2. Click `Run workflow`.
3. Enter the exact existing tag in the `tag` input, for example `0.15.2.5` or `0.15.2.6`.
4. Let `.github/workflows/release.yml` rebuild and upload `DalamudACT.zip`.
5. Re-check the Release page for that tag.

Do not use `Re-run jobs` on an old failed run if the workflow file changed after that run was created.

- A re-run still uses the original workflow snapshot and original `GITHUB_SHA`.
- If that old run still referenced stale package paths, the re-run will fail the same way.
- Prefer `workflow_dispatch` with the existing tag when the goal is to republish.

## Quick release shortcut

Use this shortcut when the workflow is already known-good and you only need a normal patch release.

```powershell
$ver = "0.15.2.6"

# 1. Update version references in:
#    DalamudACT.csproj / both manifests / repo.json / CHANGELOG / RELEASE-NOTES

dotnet build E:\git\DalamudACT\DalamudACT\DalamudACT.csproj -c Release -p:Version=$ver -p:FileVersion=$ver -p:AssemblyVersion=$ver
git -C E:\git\DalamudACT add .
git -C E:\git\DalamudACT commit -m "chore: release $ver"
git -C E:\git\DalamudACT push origin master
git -C E:\git\DalamudACT -c tag.gpgSign=false tag -a $ver -m "DalamudACT $ver"
git -C E:\git\DalamudACT push origin $ver
```

Quick checks:

- `repo.json` download links must already point to `$ver`
- the tag must exactly match `$ver`
- the Release page should contain `DalamudACT.zip`

## Testing release flow

Use this only for `testing_*` tags.

```powershell
$testVer = "0.15.2.6"
$testTag = "testing_$testVer"

git -C E:\git\DalamudACT -c tag.gpgSign=false tag -a $testTag -m "DalamudACT $testTag"
git -C E:\git\DalamudACT push origin $testTag
```

Notes:

- make sure `.github/workflows/test_release.yml` on the pushed commit is the current version that packages from `output/`
- do not use `testing_*` tags for a formal release
- after the workflow succeeds, verify the testing asset and the testing download link in `repo.json`

## Why the unsigned tag command matters

- This machine has previously had `tag.gpgSign=true`.
- A plain `git tag 0.15.2.5` may block on signing.
- Use `-c tag.gpgSign=false` for the release tag unless signing has been intentionally reconfigured.

## Post-release checks

1. Confirm GitHub shows tag `0.15.2.5`.
2. Confirm the Release title is `DalamudACT 0.15.2.5`.
3. Confirm the attached asset includes `DalamudACT.zip`.
4. Confirm the three `repo.json` download links all point to the same `0.15.2.5` tag.
5. Confirm the in-game plugin version displays `0.15.2.5`.

## Read this first when taking over

1. [HANDOVER.md](../HANDOVER.md)
2. [RELEASE-RUNBOOK.md](RELEASE-RUNBOOK.md)
3. [CHANGELOG.md](CHANGELOG.md)
4. [RELEASE-NOTES.md](RELEASE-NOTES.md)
5. `DalamudACT/Stats/LocalStatsService.cs`
