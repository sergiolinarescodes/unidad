<#
.SYNOPSIS
  The down half of a sync (U-54, U-62): carries a unidad tag into a game. Run inside the game checkout; game-agnostic.
  1. refuses a dirty Packages/com.unidad.*, docs/SOURCES.md or tools/ (exit 2);
  2. replaces Packages/com.unidad.{core,modules,unity} with the tag's (git archive), every file's time set to now (the
     archive keeps commit times, older than obj/: an incremental build would skip the new sources);
  3. -Since <S>: re-applies with --3way the game's framework edits merged after S (U-117); their rows stay;
  4. mirrors the tag's tools/ into the game, except tools/repo.json, tools/meta-roots.txt and repo.json gameTools;
  5. rewrites the carry block of docs/SOURCES.md (tag + commit per package) and deletes the -Replayed rows.
  The result is staged for review, never committed.
  Exit 0 | 1 a re-apply conflict (the files are listed; resolve keeping that PR's intent) | 2 usage, dirty tree, unknown tag
.EXAMPLE
  pwsh -File tools/carry-unidad.ps1 -Tag v2.0.0-pre.2 -Replayed 12,15 -Since <S>
#>
param(
    [string]$Tag,
    [string[]]$Replayed = @(),
    [string]$Since,
    [string]$Source,
    [string]$Game
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/UnidadTools.ps1')
$Packages = 'com.unidad.core', 'com.unidad.modules', 'com.unidad.unity'
$Frameworks = @($Packages | ForEach-Object { "Packages/$_" })

function Usage($why) { Write-Host "[carry] $why"; exit 2 }
function Slice([string[]]$a, [int]$from, [int]$to) { if ($from -ge $to) { return @() }; @($a[$from..($to - 1)]) }
function GitIn($dir) { $out = & git -c core.quotepath=off -C $dir @args 2>$null; if ($LASTEXITCODE -ne 0) { return $null }; $out }

if (-not $Tag) { Usage 'usage: -Tag <tag> [-Replayed <n,..>] [-Since <sha>] [-Source <unidad checkout>] [-Game <game checkout>]' }
try { $Replayed = @($Replayed | ForEach-Object { $_ -split ',' } | Where-Object { $_.Trim() } | ForEach-Object { [int]$_.Trim().TrimStart('#') }) }
catch { Usage "-Replayed takes PR numbers, got '$($Replayed -join ',')'" }
try { $Game = Get-RepoRoot $(if ($Game) { $Game } else { (Get-Location).Path }) } catch { Usage $_.Exception.Message }
if (-not $Source) { $Source = Join-Path (Split-Path (Get-MainCheckout $Game) -Parent) 'unidad' }
if (-not (GitIn $Source rev-parse --git-dir)) { Usage "no unidad checkout at $Source (pass -Source)" }
if (GitIn $Game status --porcelain -- @Frameworks docs/SOURCES.md tools) { Usage "uncommitted changes under $($Frameworks -join ', '), docs/SOURCES.md or tools/ in $Game" }
if ($Since -and -not (GitIn $Game rev-parse --verify -q "$Since^{commit}")) { Usage "unknown commit $Since in $Game" }

$null = GitIn $Source fetch -q --tags origin   # best effort: a checkout without origin still has its own tags
$sha = GitIn $Source rev-parse --verify -q "refs/tags/$Tag^{commit}"
if (-not $sha) { Usage "unknown tag $Tag in $Source" }

$tmp = Join-Path ([IO.Path]::GetTempPath()) "carry-unidad-$PID"
$conflicts = @()
try {
    New-Item -ItemType Directory -Force $tmp | Out-Null
    $zip = Join-Path $tmp 'tag.zip'
    $present = @(GitIn $Source ls-tree --name-only $sha -- @Frameworks tools)
    if (-not $present) { Usage "$Tag has none of $($Frameworks -join ', '), tools" }
    $null = GitIn $Source archive --format=zip "--output=$zip" $sha @present
    if ($LASTEXITCODE -ne 0) { Usage "git archive of $Tag failed" }
    $tree = Join-Path $tmp 'tree'
    [IO.Compression.ZipFile]::ExtractToDirectory($zip, $tree)

    # 2. the three packages
    foreach ($p in $Frameworks) {
        $dst = Join-Path $Game $p
        if (Test-Path -LiteralPath $dst) { Remove-Item -LiteralPath $dst -Recurse -Force }
        if (Test-Path -LiteralPath (Join-Path $tree $p)) {
            New-Item -ItemType Directory -Force (Split-Path $dst -Parent) | Out-Null
            Move-Item -LiteralPath (Join-Path $tree $p) $dst
        }
    }
    $now = Get-Date
    Get-ChildItem -LiteralPath (Join-Path $Game 'Packages') -Recurse -File -Force |
        Where-Object { $_.FullName -replace '\\', '/' -match '/Packages/com\.unidad\.(core|modules|unity)/' } |
        ForEach-Object { $_.LastWriteTime = $now }
    $null = GitIn $Game add -A -- @Frameworks

    # 3. framework edits merged in the game after the sync started
    if ($Since) {
        $patch = Join-Path $tmp 'since.patch'
        $null = GitIn $Game diff --binary "--output=$patch" $Since HEAD -- @Frameworks
        if ((Get-Item -LiteralPath $patch).Length -gt 0) {
            $null = GitIn $Game apply --3way $patch
            if ($LASTEXITCODE -ne 0) {
                $conflicts = @(GitIn $Game diff --name-only --diff-filter=U)
                if (-not $conflicts) { $conflicts = @(GitIn $Game apply --numstat $patch | ForEach-Object { ($_ -split "`t")[2] }) }
            }
        }
    }

    # 4. the toolset (U-62)
    $gameTools = @()
    $repoJson = Join-Path $Game 'tools/repo.json'
    if (Test-Path -LiteralPath $repoJson) {
        try { $gameTools = @((Get-Content -Raw -LiteralPath $repoJson | ConvertFrom-Json).gameTools | Where-Object { $_ } | ForEach-Object { if ($_ -like 'tools/*') { $_ } else { "tools/$_" } }) }
        catch { Usage 'tools/repo.json is not JSON' }
    }
    $kept = { param($rel) $rel -in 'tools/repo.json', 'tools/meta-roots.txt' -or ($gameTools | Where-Object { $rel -like $_ -or $rel -like "$_/*" }) }
    $srcTools = Join-Path $tree 'tools'
    $gameToolsDir = Join-Path $Game 'tools'
    if (Test-Path -LiteralPath $gameToolsDir) {
        Get-ChildItem -LiteralPath $gameToolsDir -Recurse -File -Force | ForEach-Object {
            $rel = 'tools/' + [IO.Path]::GetRelativePath($gameToolsDir, $_.FullName).Replace('\', '/')
            if (-not (& $kept $rel) -and -not (Test-Path -LiteralPath (Join-Path $tree $rel))) { Remove-Item -LiteralPath $_.FullName -Force }
        }
    }
    if (Test-Path -LiteralPath $srcTools) {
        Get-ChildItem -LiteralPath $srcTools -Recurse -File -Force | ForEach-Object {
            $rel = 'tools/' + [IO.Path]::GetRelativePath($srcTools, $_.FullName).Replace('\', '/')
            if (& $kept $rel) { return }
            $dst = Join-Path $Game $rel
            New-Item -ItemType Directory -Force (Split-Path $dst -Parent) | Out-Null
            Copy-Item -LiteralPath $_.FullName $dst -Force
        }
    }
    $null = GitIn $Game add -A -- tools

    # 5. docs/SOURCES.md
    $sources = Join-Path $Game 'docs/SOURCES.md'
    $lines = if (Test-Path -LiteralPath $sources) { @([IO.File]::ReadAllLines($sources)) } else {
        @('# Sources', '', '## Divergences from the carried tag', '', '| PR | Path | Why |', '|---|---|---|')
    }
    $block = @('<!-- carry-unidad:begin -->', '| Package | Tag | unidad commit |', '|---|---|---|') +
        @($Packages | ForEach-Object { "| $_ | $Tag | $sha |" }) + '<!-- carry-unidad:end -->'
    $begin = [array]::IndexOf($lines, '<!-- carry-unidad:begin -->')
    $end = [array]::IndexOf($lines, '<!-- carry-unidad:end -->')
    if ($begin -ge 0 -and $end -gt $begin) {
        $lines = @(Slice $lines 0 $begin) + $block + @(Slice $lines ($end + 1) $lines.Count)
    } else {
        $at = [Math]::Min(2, $lines.Count)   # after the title and its blank line
        $lines = @(Slice $lines 0 $at) + @('## Carried framework', '') + $block + @('') + @(Slice $lines $at $lines.Count)
    }
    $lines = @($lines | Where-Object { -not ($_ -match '^\|\s*#(\d+)\s*\|' -and [int]$Matches[1] -in $Replayed) })
    New-Item -ItemType Directory -Force (Split-Path $sources -Parent) | Out-Null
    [IO.File]::WriteAllText($sources, ($lines -join "`n") + "`n")
    $null = GitIn $Game add -- docs/SOURCES.md
}
finally { Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue }

Write-Host "[carry] $Tag ($sha) carried into $Game$(if ($Replayed) { "; removed rows #$($Replayed -join ',#')" })"
if ($conflicts) { Write-Host "[carry] re-apply of the edits since $Since conflicted in:"; $conflicts | ForEach-Object { Write-Host "  $_" }; exit 1 }
exit 0
