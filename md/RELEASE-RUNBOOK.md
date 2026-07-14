# RELEASE RUNBOOK

## Purpose

This is the release checklist for DalamudACT. Follow it exactly so releases do not repeat the same mistakes around version metadata, tag creation, packaging `Timeline/Data`, or accidentally committing local scratch files.

## Current Release Model

- Official branch: `main`
- Official release tag format: `*.*.*.*`, for example `0.15.2.51`
- Official workflow: `.github/workflows/release.yml`
- Test tags: `testing_*` use `.github/workflows/test_release.yml`, not the official release workflow.
- Release asset name: `DalamudACT.zip`
- Do not publish from `build.yml` / `latest` for official releases.

## Files To Update

For a release version such as `0.15.2.52`, update every one of these before committing:

- `DalamudACT/DalamudACT.csproj`
  - `<AssemblyVersion>`
- `DalamudACT/DalamudACT.json`
  - `AssemblyVersion`
- `Data/DalamudACT.json`
  - `AssemblyVersion`
- `repo.json`
  - `AssemblyVersion`
  - `TestingAssemblyVersion`
  - `DownloadLinkInstall`
  - `DownloadLinkTesting`
  - `DownloadLinkUpdate`
  - `LastUpdated`
- `md/RELEASE-NOTES.md`
  - Keep `{{VERSION}}` placeholders intact.
  - Update the content to match the actual release.

Use a current Unix timestamp for `repo.json` `LastUpdated`:

```powershell
[DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
```

## Pre-Commit Checks

Always inspect the worktree before staging:

```powershell
git status --short
git diff --stat
git log --oneline -10
```

Do not use `git add .` for releases. Stage explicit files only.

Known local scratch file that must not be committed:

```txt
1.txt
```

If `1.txt` exists, leave it untracked.

## Build Verification

Run this before committing:

```powershell
dotnet build --no-restore
```

Expected result:

```txt
0 warnings
0 errors
```

If local build prints `Dalamud.NET.Sdk: root at ...\Hooks\dev\` but then fails with many missing `Dalamud`, `Dalamud.Bindings.ImGui`, `FFXIVClientStructs`, or `Lumina` references, MSBuild did not pass the local Dalamud path through to reference resolution. Verify the path exists, then build with an explicit `DalamudLibPath`:

```powershell
Test-Path -LiteralPath "$env:APPDATA\XIVLauncherCN\addon\Hooks\dev"
dotnet build "DalamudACT/DalamudACT.csproj" --no-restore -p:DalamudLibPath="$env:APPDATA\XIVLauncherCN\addon\Hooks\dev\"
```

Expected result is still `0 warnings` and `0 errors`. This is a local build-environment fallback, not a source-code fix.

If release-specific build parameters need checking:

```powershell
dotnet build ./DalamudACT/DalamudACT.csproj `
  --configuration Release `
  --no-restore `
  -p:Version=0.15.2.52 `
  -p:FileVersion=0.15.2.52 `
  -p:AssemblyVersion=0.15.2.52
```

## Commit

After staging only intended files:

```powershell
git diff --cached --stat
git commit -m "chore: release 0.15.2.52"
```

Feature-heavy releases may use a feature commit message, but the version metadata must already be included in the same pushed commit or in a separate release commit before tagging.

## Tag Creation

Use a non-interactive annotated tag command:

```powershell
git tag -a 0.15.2.52 -m "0.15.2.52"
```

Do not run plain `git tag 0.15.2.52` on this machine. It may open an editor or signing flow and block the release process.

If local git signing config ever causes trouble, use:

```powershell
git -c tag.gpgSign=false tag -a 0.15.2.52 -m "0.15.2.52"
```

Verify the tag exists before pushing:

```powershell
git tag --list 0.15.2.52
```

## Push

Push `main` first, then the tag:

```powershell
git push origin main
git push origin 0.15.2.52
```

The tag push triggers `.github/workflows/release.yml`.

## Workflow Checks

Watch the release workflow:

```powershell
gh run list --limit 5
gh run watch <run-id> --exit-status
```

Then verify the GitHub Release:

```powershell
gh release view 0.15.2.52 --json url,tagName,name,isDraft,isPrerelease,assets
```

Expected:

- `isDraft: false`
- `isPrerelease: false`
- asset `DalamudACT.zip` exists
- asset digest is present
- release URL matches the tag

## Packaging Rules

Official workflow currently packages:

- `./output/DalamudACT.dll`
- `./output/DalamudACT.json`
- `./output/DalamudACT.deps.json`
- `./output/Timeline`

The workflow must copy built-in timelines before archiving:

```powershell
New-Item -ItemType Directory -Force -Path ./output/Timeline | Out-Null
Copy-Item -Path ./DalamudACT/Features/Timeline/Data -Destination ./output/Timeline/Data -Recurse -Force
```

Do not remove `Timeline/Data` from the release asset. If it is missing, installed users can see `当前区域没有时间轴` even when source files exist.

## Timeline Path Rules

Current local development timeline source path is intentionally hardcoded in runtime code:

```txt
E:\git\DalamudACT\DalamudACT\Features\Timeline\Data
```

Do not change this hardcoded path unless the maintainer explicitly asks for it in the same session.

This hardcoded path is for local development. Official release packages still need bundled `Timeline/Data` as described above.

## Post-Release Checks

After the workflow succeeds, record these in the final response or handoff:

- Release URL
- Commit hash
- Tag
- Asset name and size
- SHA256 digest
- Build/workflow success
- Any remaining untracked files, especially `1.txt`

Example:

```txt
Release: https://github.com/anmili2022/DalamudACT/releases/tag/0.15.2.51
Asset: DalamudACT.zip
SHA256: 58d3e43debcd6e8c6e27d698ac0082954def1ec8f3a38c6bd65a345c24ec2b3c
```

## If Release Fails

Do not immediately edit workflow files. Check in this order:

1. Did the tag match `*.*.*.*` exactly?
2. Did `repo.json` links point to the new tag before tagging?
3. Did the workflow fail before or after archive creation?
4. Did `DalamudACT.zip` include `Timeline/Data`?
5. Did `md/RELEASE-NOTES.md` render correctly with `{{VERSION}}` replaced?
6. Was the tag pushed from the intended commit?

If the workflow file itself was fixed after a failed tag run, prefer `workflow_dispatch` with the existing tag rather than re-running the stale failed run.

## Do Not

- Do not use `git add .` for releases.
- Do not commit `1.txt`.
- Do not use `testing_*` tags for official releases.
- Do not create plain tags that open an editor/signing prompt.
- Do not remove `Timeline/Data` from release packaging.
- Do not change the hardcoded local timeline path without explicit maintainer approval.
- Do not tag before version metadata and release notes are updated.
