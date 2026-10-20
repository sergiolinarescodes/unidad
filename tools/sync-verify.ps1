<#
.SYNOPSIS
  Post-conditions of a sync run (U-54, U-62). Game-agnostic: every repo path is a parameter.
  Exit 0 all pass, 1 a check failed, 2 usage/setup error.
.EXAMPLE
  pwsh -File tools/sync-verify.ps1 -Phase Up -Tag v2.0.0-pre.2 -Replayed 12,15
  pwsh -File tools/sync-verify.ps1 -Phase Down -Tag v2.0.0-pre.2 -Game ../game-wt-slot1 -Replayed 12,15 -Report boot.txt
  pwsh -File tools/sync-verify.ps1 -Phase Ship -Tag v2.0.0 -Game ../game-wt-slot1 -Report boot.txt
#>
param(
    [ValidateSet('Up', 'Down', 'Ship')][string]$Phase,
    [string]$Tag,
    [string]$Repo = (Split-Path -Parent $PSScriptRoot),
    [string]$Game,
    [string]$At = 'HEAD',
    [string]$Base = 'origin/master',
    [string[]]$Replayed = @(),
    [string]$UpstreamPrefix = '11',
    [Alias('BootReport')][string]$Report,
    [switch]$Strict,
    # Test seams: the branch the tag must be on, and the ref the kept trees must equal.
    [string]$MainRef = 'origin/main',
    [string]$KeptFrom = 'v0.1.0'
)
$ErrorActionPreference = 'Stop'

$Packages = 'com.unidad.core', 'com.unidad.modules', 'com.unidad.unity'
$KeptTrees = 'Tools~', 'Skills~', 'Documentation~/model-pipeline'
$GameName = '\bCds\b|\bCDS\b|\bcds\.'
$script:failed = 0

function Usage($why) { Write-Host "[sync-verify] usage: $why"; exit 2 }
function Fail($id, $why) { Write-Host "[sync-verify] ${id}: $why"; $script:failed++ }
function GitIn($dir) { $out = & git -c core.quotepath=off -C $dir @args 2>$null; if ($LASTEXITCODE -ne 0) { return $null }; $out }

# Table rows of the markdown section headed $heading: each row's cells, header and separator dropped.
function Get-Rows([string[]]$lines, [string]$heading) {
    $in = $false
    foreach ($l in $lines) {
        if ($l -match '^#+\s') { $in = $l -match [regex]::Escape($heading); continue }
        if (-not $in -or $l -notmatch '^\s*\|') { continue }
        $cells = @($l.Trim().Trim('|') -split '\|' | ForEach-Object { $_.Trim() })
        if ($cells[0] -match '^-+$' -or $cells[0] -in 'PR', 'Package', 'Game PR') { continue }
        , $cells
    }
}
function Get-PrNumber([string]$cell) { if ($cell -match '#(\d+)') { [int]$Matches[1] } }

function Invoke-Up {
    # U1 annotated tag on main
    if ((GitIn $Repo cat-file -t $Tag) -ne 'tag') { Fail 'U1' "$Tag is not an annotated tag" }
    & git -C $Repo merge-base --is-ancestor "$Tag^{commit}" $MainRef 2>$null
    if ($LASTEXITCODE -ne 0) { Fail 'U1' "$Tag is not on $MainRef" }

    # U2 one version for the three packages and their com.unidad.* dependencies
    $version = $Tag.TrimStart('v')
    foreach ($p in $Packages) {
        $text = GitIn $Repo show "${Tag}:Packages/$p/package.json"
        if (-not $text) { Fail 'U2' "Packages/$p/package.json missing at $Tag"; continue }
        try { $json = ($text -join "`n") | ConvertFrom-Json } catch { Fail 'U2' "$p package.json is not JSON"; continue }
        if ($json.version -ne $version) { Fail 'U2' "$p version is $($json.version), expected $version" }
        foreach ($d in @($json.dependencies.PSObject.Properties | Where-Object Name -like 'com.unidad.*')) {
            if ($d.Value -ne $version) { Fail 'U2' "$p depends on $($d.Name) $($d.Value), expected $version" }
        }
    }

    # U3 kept trees byte-identical to 0.1.0 (U-112)
    foreach ($k in $KeptTrees) {
        $now = GitIn $Repo rev-parse "${Tag}:$k"; $was = GitIn $Repo rev-parse "${KeptFrom}:$k"
        if (-not $now -or $now -ne $was) { Fail 'U3' "$k at $Tag differs from $KeptFrom" }
    }

    # U4 an Upstream row per replayed game PR
    $sources = @(GitIn $Repo show "${Tag}:docs/SOURCES.md")
    foreach ($n in $Replayed) {
        if (-not ($sources | Where-Object { $_ -match "^\|\s*$([regex]::Escape($UpstreamPrefix))#$n\s*\|" })) {
            Fail 'U4' "docs/SOURCES.md at $Tag has no Upstream row for $UpstreamPrefix#$n"
        }
    }

    # U5 no game names added under Packages/ since the previous 2.x tag
    $prev = GitIn $Repo tag --list 'v2.*' --sort=-v:refname --merged "$Tag^{commit}" | Where-Object { $_ -ne $Tag } | Select-Object -First 1
    if (-not $prev) { $prev = if (GitIn $Repo rev-parse --verify -q "$KeptFrom^{commit}") { $KeptFrom } else { '4b825dc642cb6eb9a060e54bf8d69288fbee4904' } }  # the empty tree
    foreach ($l in @(GitIn $Repo diff --unified=0 $prev $Tag -- Packages)) {
        if ($l -match '^\+[^+]' -and $l -cmatch $GameName) { Fail 'U5' "game name added under Packages/ since ${prev}: $($l.Substring(1).Trim())" }
    }
}

function Test-CarryScope([string]$path, [string[]]$gameTools) {
    if ($path -match '^Packages/com\.unidad\.(core|modules|unity)/' -or $path -eq 'docs/SOURCES.md' -or
        $path -match '^dotnet/Unidad\.[^/]+/' -or $path -match '^dotnet/[^/]+\.slnx$') { return $true }
    if ($path -eq 'dotnet/Directory.Build.props') {
        # only the analyzer path line may change (D-83)
        $changed = @(GitIn $Game diff --unified=0 "$Base...$At" -- $path | Where-Object { $_ -match '^[+-][^+-]' })
        return -not ($changed | Where-Object { $_ -notmatch 'Analyzer' })
    }
    if ($path -notmatch '^tools/' -or $path -in 'tools/repo.json', 'tools/meta-roots.txt') { return $false }
    -not ($gameTools | Where-Object { $path -like $_ -or $path -like "$_/*" })
}

function Invoke-Down {
    $sha = GitIn $Repo rev-parse "$Tag^{commit}"
    $ref = "refs/unidad/$Tag"
    if (-not (GitIn $Game rev-parse --verify -q "$ref^{commit}")) {
        $null = GitIn $Game fetch -q $Repo "+refs/tags/${Tag}:$ref"
        if (-not (GitIn $Game rev-parse --verify -q "$ref^{commit}")) { Usage "$ref missing in $Game and not fetchable from $Repo" }
    }
    if (-not (GitIn $Game rev-parse --verify -q "$At^{commit}")) { Usage "unknown ref $At in $Game" }
    $sources = @(GitIn $Game show "${At}:docs/SOURCES.md")
    if (-not $sources) { Fail 'D1' "docs/SOURCES.md missing at $At"; return }

    # D1 the carry block names the tag and its commit for every package
    $begin = [array]::IndexOf($sources, '<!-- carry-unidad:begin -->'); $end = [array]::IndexOf($sources, '<!-- carry-unidad:end -->')
    $block = if ($begin -ge 0 -and $end -gt $begin) { $sources[($begin + 1)..($end - 1)] } else { @() }
    if (-not $block) { Fail 'D1' 'no carry-unidad block in docs/SOURCES.md' }
    foreach ($p in $Packages) {
        $row = $block | Where-Object { $_ -match "^\|\s*$([regex]::Escape($p))\s*\|" } | Select-Object -First 1
        if (-not $row) { Fail 'D1' "carry block has no $p row"; continue }
        $cells = @($row.Trim().Trim('|') -split '\|' | ForEach-Object { $_.Trim() })
        if ($cells[1] -ne $Tag -or $cells[2] -ne $sha) { Fail 'D1' "carry block names $p $($cells[1]) $($cells[2]), expected $Tag $sha" }
    }

    # D2 framework drift from the tag is covered by a Divergences row still present; D3 replayed rows are gone
    $rows = @(Get-Rows $sources 'Divergences')
    $covered = @($rows | ForEach-Object { $_[1] -split '[,\s]+' } | Where-Object { $_ })
    $drift = @(GitIn $Game diff --name-only $ref $At -- ($Packages | ForEach-Object { "Packages/$_" }))
    foreach ($f in $drift) {
        if (-not ($covered | Where-Object { $f.StartsWith($_.TrimEnd('*')) })) { Fail 'D2' "$f differs from $Tag with no Divergences row" }
    }
    foreach ($r in $rows) {
        $n = Get-PrNumber $r[0]
        if ($n -and $n -in $Replayed) { Fail 'D3' "Divergences row #$n was replayed but is still present" }
    }

    # D4 the carry touches only framework copies, SOURCES, sidecar framework projects and the mirrored toolset (U-62)
    $gameTools = @()
    $repoJson = GitIn $Game show "${At}:tools/repo.json"
    if ($repoJson) {
        try { $gameTools = @((($repoJson -join "`n") | ConvertFrom-Json).gameTools | Where-Object { $_ } | ForEach-Object { if ($_ -like 'tools/*') { $_ } else { "tools/$_" } }) }
        catch { Fail 'D4' 'tools/repo.json is not JSON' }
    }
    $touched = GitIn $Game diff --name-only "$Base...$At"
    if ($null -eq $touched -and $LASTEXITCODE -ne 0) { Usage "cannot diff $Base...$At in $Game" }
    foreach ($f in @($touched)) { if (-not (Test-CarryScope $f $gameTools)) { Fail 'D4' "$f is outside the carry scope" } }

    # D5 the C14 boot report
    if (-not $Report) { return }
    try { $text = [IO.File]::ReadAllLines($Report, [Text.Encoding]::UTF8) } catch { Usage "cannot read report $Report" }
    $kv = @{}
    foreach ($l in $text) { if ($l -match '^\s*([^=\s]+)\s*=(.*)$') { $kv[$Matches[1]] = $Matches[2].Trim() } }
    $required = @('boot', 'errors') + $(if ($Strict) { 'strict', 'stubs.winning' })
    foreach ($k in $required) { if (-not $kv.ContainsKey($k)) { Fail 'D5' "report has no $k" } }
    if ($kv.ContainsKey('boot') -and $kv.boot -ne 'ok') { Fail 'D5' "boot=$($kv.boot)" }
    if ($kv.ContainsKey('errors') -and $kv.errors -ne '0') { Fail 'D5' "errors=$($kv.errors)" }
    if ($Strict) {
        if ($kv.ContainsKey('strict') -and $kv.strict -ne 'true') { Fail 'D5' "strict=$($kv.strict), expected true" }
        if ($kv.ContainsKey('stubs.winning') -and $kv['stubs.winning'] -ne '0') { Fail 'D5' "stubs.winning=$($kv['stubs.winning']), expected 0" }
    }
    elseif ($kv.ContainsKey('stubs.winning')) { Write-Host "stubs.winning=$($kv['stubs.winning'])" }
}

# -File hands '12,15' over as one string
try { $Replayed = @($Replayed | ForEach-Object { $_ -split ',' } | Where-Object { $_.Trim() } | ForEach-Object { [int]$_.Trim().TrimStart('#') }) }
catch { Usage "-Replayed takes PR numbers, got '$($Replayed -join ',')'" }
if (-not $Phase -or -not $Tag) { Usage '-Phase Up|Down|Ship -Tag <v...> [-Game <checkout>] [-Replayed <n,..>] [-Report <file>] [-Strict]' }
if (-not (GitIn $Repo rev-parse --git-dir)) { Usage "not a git repo: $Repo" }
if (-not (GitIn $Repo rev-parse --verify -q "refs/tags/$Tag")) { Usage "unknown tag $Tag in $Repo" }
if ($Phase -ne 'Up') {
    if (-not $Game) { Usage "-Phase $Phase needs -Game" }
    if (-not (GitIn $Game rev-parse --git-dir)) { Usage "not a git repo: $Game" }
}
if ($Phase -eq 'Ship') {
    if ($Tag -ne 'v2.0.0') { Usage "-Phase Ship needs -Tag v2.0.0, got $Tag" }
    $Strict = $true
}
if ($Report -and -not (Test-Path -LiteralPath $Report -PathType Leaf)) { Usage "cannot read report $Report" }

if ($Phase -in 'Up', 'Ship') { Invoke-Up }
if ($Phase -in 'Down', 'Ship') { Invoke-Down }

if ($script:failed) { Write-Host "sync-verify: $script:failed failed"; exit 1 }
Write-Host 'sync-verify: ok'
exit 0
