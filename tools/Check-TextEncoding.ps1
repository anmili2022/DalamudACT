[CmdletBinding()]
param(
    [string]$Root = '',
    [switch]$All
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Root)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $Root = Join-Path $scriptDir '..'
}

$rootPath = (Resolve-Path $Root).Path
$utf8Strict = New-Object System.Text.UTF8Encoding($false, $true)
$textExtensions = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
@(
    '.md', '.cs', '.json', '.xml', '.yml', '.yaml', '.ps1', '.props', '.targets', '.csproj', '.sln', '.config', '.editorconfig', '.gitattributes'
) | ForEach-Object { [void]$textExtensions.Add($_) }

$excludedRelativePaths = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
@(
    'md/HANDOVER-LEGACY-MOJIBAKE-ARCHIVE.md'
) | ForEach-Object { [void]$excludedRelativePaths.Add($_) }

$mojibakeChars = @(
    [char]0x951B, # garbled Chinese punctuation marker often rendered as mojibake
    [char]0x9286,
    [char]0x9428,
    [char]0x7ECB,
    [char]0x7F01,
    [char]0x95C2,
    [char]0x9366,
    [char]0x93C2,
    [char]0x93C3
)
$mojibakePattern = '[' + [Regex]::Escape((-join $mojibakeChars)) + ']'
$replacementChar = [char]0xFFFD

function Get-RelativePath([string]$Path) {
    $full = [System.IO.Path]::GetFullPath($Path)
    if ($full.StartsWith($rootPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        return $full.Substring($rootPath.Length).TrimStart('\', '/').Replace('\', '/')
    }

    return $full.Replace('\', '/')
}

function Test-SkipPath([string]$Path) {
    $relative = Get-RelativePath $Path
    if ($excludedRelativePaths.Contains($relative)) {
        return $true
    }

    $parts = $relative -split '/'
    foreach ($part in $parts) {
        if ($part -in @('.git', '.vs', 'bin', 'obj', 'output')) {
            return $true
        }
    }

    return $false
}

function Get-CandidateFiles {
    if ($All) {
        return Get-ChildItem -LiteralPath $rootPath -Recurse -File -Force |
            Where-Object { $textExtensions.Contains($_.Extension) -and -not (Test-SkipPath $_.FullName) }
    }

    $names = New-Object System.Collections.Generic.List[string]

    try {
        (& git -C $rootPath -c core.quotepath=false diff --name-only) | ForEach-Object {
            if (-not [string]::IsNullOrWhiteSpace($_)) { [void]$names.Add($_) }
        }

        (& git -C $rootPath -c core.quotepath=false ls-files --others --exclude-standard) | ForEach-Object {
            if (-not [string]::IsNullOrWhiteSpace($_)) { [void]$names.Add($_) }
        }
    } catch {
        Write-Warning "Cannot read git changed files. Falling back to full scan. Error: $($_.Exception.Message)"
        return Get-ChildItem -LiteralPath $rootPath -Recurse -File -Force |
            Where-Object { $textExtensions.Contains($_.Extension) -and -not (Test-SkipPath $_.FullName) }
    }

    $unique = $names | Sort-Object -Unique
    $files = New-Object System.Collections.Generic.List[System.IO.FileInfo]
    foreach ($name in $unique) {
        $full = [System.IO.Path]::GetFullPath((Join-Path $rootPath $name))
        if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { continue }
        $item = Get-Item -LiteralPath $full
        if (-not $textExtensions.Contains($item.Extension)) { continue }
        if (Test-SkipPath $item.FullName) { continue }
        [void]$files.Add($item)
    }

    return $files
}

$issues = New-Object System.Collections.Generic.List[string]
$files = @(Get-CandidateFiles)

foreach ($file in $files) {
    $relative = Get-RelativePath $file.FullName
    $bytes = [System.IO.File]::ReadAllBytes($file.FullName)
    $text = $null

    try {
        $text = $utf8Strict.GetString($bytes)
    } catch {
        [void]$issues.Add("${relative}: invalid UTF-8: $($_.Exception.Message)")
        continue
    }

    if ($file.Extension -ieq '.md') {
        if ($text -match '\?{4,}') {
            [void]$issues.Add("${relative}: suspicious mojibake, found 4 or more consecutive question marks.")
        }

        if ($text.IndexOf($replacementChar) -ge 0) {
            [void]$issues.Add("${relative}: suspicious mojibake, found Unicode replacement character.")
        }

        if ($text -match $mojibakePattern) {
            [void]$issues.Add("${relative}: suspicious mojibake, found common mojibake characters.")
        }
    }
}

if ($issues.Count -gt 0) {
    Write-Host "Text encoding check failed:" -ForegroundColor Red
    foreach ($issue in $issues) {
        Write-Host "- $issue" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "If this is legacy damaged text, archive it and add the archive file to the exclusion list. If this is a new change, rewrite it as UTF-8." -ForegroundColor Yellow
    exit 1
}

if ($All) { $mode = 'all repository text files' } else { $mode = 'changed and untracked text files' }
Write-Host "Text encoding check passed: $mode, checked $($files.Count) files." -ForegroundColor Green
