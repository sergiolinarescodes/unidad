# Shared pure functions of the toolset (U-40). Dot-sourced by every script; the smoke tests call them directly.
# Nothing here names a repo, branch, slot prefix or Unity method: those come from tools/repo.json.

$ErrorActionPreference = 'Stop'

function Get-RepoRoot([string]$From = (Get-Location).Path) {
    # A git hook exports GIT_DIR (never GIT_WORK_TREE), which makes rev-parse answer the current directory: ask without it.
    $hookDir = $env:GIT_DIR; $env:GIT_DIR = $null
    try { $root = git -C $From rev-parse --show-toplevel 2>$null } finally { $env:GIT_DIR = $hookDir }
    if ($LASTEXITCODE -ne 0 -or !$root) { throw "not a git checkout: $From" }
    $root.Trim()
}

# The main checkout: the parent of the shared git dir, whichever worktree asks.
function Get-MainCheckout([string]$From = (Get-Location).Path) {
    $common = git -C $From rev-parse --path-format=absolute --git-common-dir 2>$null
    if ($LASTEXITCODE -ne 0 -or !$common) { throw "not a git checkout: $From" }
    (Split-Path $common.Trim() -Parent) -replace '\\', '/'
}

# %LOCALAPPDATA%/Unidad (UNIDAD_TOOLS_HOME overrides): locks, guard, refpacks, seed. Shared by every repo.
function Get-ToolsHome {
    $dir = if ($env:UNIDAD_TOOLS_HOME) { $env:UNIDAD_TOOLS_HOME } else { Join-Path $env:LOCALAPPDATA 'Unidad' }
    $dir -replace '\\', '/'
}

$script:RepoDefaults = @{
    projectSubdir = 'Sandbox'
    editorVersion = '7000.0.0a10'
    codeSlots     = 8
    unitySlots    = @('unity1')
    loopSlot      = 'loop'
    testbedSlot   = 'testbed'
    liveSlots     = @('live1', 'live2')
    live          = @{ size = '1600x900'; idleMinutes = 10; capMinutes = 60; settleSeconds = 3; replayPaths = @(); tour = $null }
    sidecar       = @{ solution = 'dotnet/Unidad.Sidecar.slnx'; viewCheckSolution = 'dotnet/Unidad.ViewCheck.slnx' }
    parity        = @{ roots = @('Packages'); logicDir = 'dotnet'; checkDir = 'dotnet/Check'; projectPrefixes = @('Unidad.'); ignoreProjects = @('Unidad.Analyzers') }
    lane          = @{
        prepare = 'tools/sandbox-prepare.ps1'; msbuildGlobs = $false; beeExitCodeReliable = $false
        execPrefix = 'Unidad.Sandbox.Editor.Lane.'; testbedMethod = 'Unidad.Sandbox.Editor.Testbed.Play'
        playerBuildMethod = 'Unidad.Adapter.Editor.BuildCli.Player'; playerName = 'Sandbox'
        playerBootArgs = @('--set', 'frames=60'); collectRoots = @('Assets')
        capsMinutes = @{ compile = 15; import = 15; exec = 15; player = 45; run = 45; init = 45 }
        postCompileChecks = @()
    }
    refpack       = @{ include = @('^Unity\.', '^UnityEngine\.', '^glTFast'); editorInclude = @('^UnityEditor', '\.Editor\.') }
    gates         = @()
    smokeMap      = @()
    checkMap      = @()
    stagingChecks = @()
    loop          = @{ everyMinutes = 30; reviewers = 4; provider = 'claude'; model = 'opus'; extraArgs = @(); lanePaths = @() }
    binaryAllow   = @()
    renameGuard   = @()
    tree          = @{ forbidden = @(); frameworkPaths = $null; sourcesFile = 'docs/SOURCES.md' }
}

function Merge-Defaults($Value, $Default) {
    if ($Value -is [hashtable] -and $Default -is [hashtable]) {
        $out = @{}
        foreach ($k in $Default.Keys) { $out[$k] = if ($Value.ContainsKey($k)) { Merge-Defaults $Value[$k] $Default[$k] } else { $Default[$k] } }
        foreach ($k in $Value.Keys) { if (!$out.ContainsKey($k)) { $out[$k] = $Value[$k] } }
        return $out
    }
    $Value
}

# tools/repo.json (or -Json) with the defaults applied; throws on a missing REQUIRED member.
function Get-RepoConfig([string]$Root, [string]$Json = $null) {
    if (!$Json) {
        $path = Join-Path $Root 'tools/repo.json'
        if (!(Test-Path -LiteralPath $path)) { throw "repo.json: $path missing" }
        $Json = Get-Content -Raw -LiteralPath $path
    }
    $raw = $Json | ConvertFrom-Json -AsHashtable
    foreach ($m in 'name', 'github', 'mainBranch', 'slotPrefix') {
        if (!$raw.ContainsKey($m) -or !$raw[$m]) { throw "repo.json: $m missing" }
    }
    Merge-Defaults $raw $script:RepoDefaults
}

function Get-SlotPath($Cfg, [string]$ParentDir, [string]$Slot) {
    (Join-Path $ParentDir "$($Cfg.slotPrefix)-$Slot") -replace '\\', '/'
}

function Get-UnityProjectPath($Cfg, [string]$CheckoutRoot) {
    $p = if ($Cfg.projectSubdir -and $Cfg.projectSubdir -ne '.') { Join-Path $CheckoutRoot $Cfg.projectSubdir } else { $CheckoutRoot }
    [IO.Path]::GetFullPath($p) -replace '\\', '/'
}

function Get-ProjectEditorVersion([string]$ProjectPath) {
    $file = Join-Path $ProjectPath 'ProjectSettings/ProjectVersion.txt'
    if (!(Test-Path -LiteralPath $file)) { return $null }
    $line = Get-Content -LiteralPath $file | Where-Object { $_ -match '^m_EditorVersion:\s*(\S+)' } | Select-Object -First 1
    if ($line -match '^m_EditorVersion:\s*(\S+)') { $Matches[1] }
}

# -UnityPath, else UNIDAD_UNITY, else the Hub install of the project's version (or -Version).
function Resolve-UnityEditor([string]$ProjectPath, [string]$UnityPath, [string]$Version) {
    $v = if ($ProjectPath) { Get-ProjectEditorVersion $ProjectPath }
    if (!$v) { $v = $Version }
    $explicit = if ($UnityPath) { $UnityPath } elseif ($env:UNIDAD_UNITY) { $env:UNIDAD_UNITY }
    if ($explicit) {
        if ($v -and $explicit -match 'Hub[\\/]Editor[\\/]([^\\/]+)[\\/]' -and $Matches[1] -ne $v) {
            throw "$explicit is Unity $($Matches[1]); the project needs $v"
        }
        return $explicit -replace '\\', '/'
    }
    if (!$v) { throw 'no Unity version: the project has no ProjectSettings/ProjectVersion.txt and none was given' }
    $exe = "C:/Program Files/Unity/Hub/Editor/$v/Editor/Unity.exe"
    if (!(Test-Path -LiteralPath $exe)) { throw "Unity $v is not installed; install it with Unity Hub" }
    $exe
}

# The editor's bundled SDK: always this dotnet (the system one is older).
function Resolve-UnityDotnet([string]$Editor) { (Join-Path (Split-Path $Editor -Parent) 'Data/DotNetSdk/dotnet.exe') -replace '\\', '/' }
function Resolve-YamlMerge([string]$Editor) { (Join-Path (Split-Path $Editor -Parent) 'Data/Tools/UnityYAMLMerge.exe') -replace '\\', '/' }

function Test-AgentSession { [bool]($env:CLAUDECODE -or $env:CLAUDE_CODE_ENTRYPOINT -or $env:CODEX_SANDBOX) }

# No "pid" in the text: a Claude Code worktree lock carries "(pid N)" and worktree-done -Sweep reads that as its own.
function Format-ClaimReason([string]$Branch, [datetime]$At = (Get-Date)) {
    "claimed by $Branch at $($At.ToString('yyyy-MM-ddTHH:mm:ss', [Globalization.CultureInfo]::InvariantCulture))"
}

function ConvertFrom-ClaimReason([string]$Reason) {
    if ($Reason -notmatch 'claimed by (\S+) at (\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d)') { return $null }
    @{ Branch = $Matches[1]; At = [datetime]::ParseExact($Matches[2], 'yyyy-MM-ddTHH:mm:ss', [Globalization.CultureInfo]::InvariantCulture) }
}

# Paths this branch touches against -Base: committed since the merge base, plus staged and unstaged.
function Get-TouchedPaths([string]$Base, [string]$Root = (Get-Location).Path) {
    $mb = git -C $Root merge-base HEAD $Base 2>$null
    $paths = @()
    if ($LASTEXITCODE -eq 0 -and $mb) { $paths += git -C $Root -c core.quotepath=off diff --name-only $mb.Trim() HEAD }
    $paths += git -C $Root -c core.quotepath=off diff --name-only --cached
    $paths += git -C $Root -c core.quotepath=off diff --name-only
    @($paths | Where-Object { $_ } | ForEach-Object { $_ -replace '\\', '/' } | Select-Object -Unique)
}

function Test-MatchesAny([string[]]$Paths, [string[]]$Regexes) {
    foreach ($p in $Paths) { foreach ($r in $Regexes) { if ($p -match $r) { return $true } } }
    $false
}

# Each map entry is { match: <regex>, project: <path with $1..$9> }; unique, in first-seen order.
function Get-MappedProjects([string[]]$Paths, $Map) {
    $out = [Collections.Generic.List[string]]::new()
    foreach ($p in $Paths) {
        foreach ($e in $Map) {
            $m = [regex]::Match($p, $e.match)
            if (!$m.Success) { continue }
            $proj = $e.project
            for ($g = $m.Groups.Count - 1; $g -ge 1; $g--) { $proj = $proj.Replace("`$$g", $m.Groups[$g].Value) }
            if (!$out.Contains($proj)) { $out.Add($proj) }
        }
    }
    $out.ToArray()
}

# Glob to regex: '**' any path, '*' one segment, '?' one char. A glob without '/' matches the file name anywhere.
function ConvertTo-GlobRegex([string]$Glob) {
    $re = [regex]::Escape($Glob).Replace('\*\*/', '(.*/)?').Replace('\*\*', '.*').Replace('\*', '[^/]*').Replace('\?', '[^/]')
    if ($Glob -notmatch '/') { return "(^|/)$re`$" }
    "^$re`$"
}

function Test-GlobAny([string]$Path, [string[]]$Globs) {
    foreach ($g in $Globs) { if ($Path -match (ConvertTo-GlobRegex $g)) { return $true } }
    $false
}

# asmdef vs its sidecar csproj (c.4). Returns the problems; empty when they agree.
function Compare-AsmdefCsproj([string]$AsmdefJson, [string]$CsprojXml, [string]$CscRsp, [bool]$HasPolyfills,
                              [string[]]$RepoAsmdefNames, [hashtable]$GuidToName = @{}) {
    $problems = @()
    $asm = $AsmdefJson | ConvertFrom-Json -AsHashtable
    $mine = @(foreach ($r in @($asm.references)) {
        $name = if ($r -match '^GUID:([0-9a-fA-F]{32})$') { $GuidToName[$Matches[1].ToLowerInvariant()] } else { $r }
        if ($name -and $RepoAsmdefNames -contains $name) { $name }
    })
    [xml]$x = $CsprojXml
    $theirs = @(foreach ($n in $x.SelectNodes('//*[local-name()="ProjectReference"]')) {
        $leaf = ($n.GetAttribute('Include') -split '[\\/]')[-1] -replace '\.csproj$', '' -replace '\.Check$', ''
        if ($RepoAsmdefNames -contains $leaf) { $leaf }
    })
    foreach ($r in $mine | Sort-Object -Unique) { if ($theirs -notcontains $r) { $problems += "references: asmdef has $r, csproj lacks it" } }
    foreach ($r in $theirs | Sort-Object -Unique) { if ($mine -notcontains $r) { $problems += "references: csproj has $r, asmdef lacks it" } }
    $asmUnsafe = [bool]$asm.allowUnsafeCode
    $node = $x.SelectSingleNode('//*[local-name()="AllowUnsafeBlocks"]')
    $projUnsafe = $node -and $node.InnerText.Trim() -eq 'true'
    if ($asmUnsafe -ne $projUnsafe) { $problems += "allowUnsafeCode: asmdef $($asmUnsafe.ToString().ToLower()), csproj $($projUnsafe.ToString().ToLower())" }
    if ($null -eq $CscRsp) { $problems += 'csc.rsp missing' }
    elseif (!($CscRsp -split "`r?`n" | Where-Object { $_.Trim() -ieq '-langversion:14' })) { $problems += 'csc.rsp lacks -langversion:14' }
    if (!$HasPolyfills) { $problems += 'Polyfills.cs missing' }
    , $problems
}

# Binaries, forbidden patterns and the Divergences row (c.8, U-63). $ReadText = { param($path) <text> }.
$script:BinaryRegex = '\.(dll|exe|so|dylib|pdb|fbx|obj|blend|ttf|otf|png|jpe?g|gif|psd|tga|exr|hdr|wav|ogg|mp3|aif|zip|7z|bin)$'
function Test-TreeFiles([string[]]$Files, [scriptblock]$ReadText, $Cfg, [string[]]$PrPaths = @(), [string]$SourcesDiff = '', [int]$Pr = 0) {
    $problems = @()
    foreach ($f in $Files) {
        if ($f -match '(^|/)Library/') { $problems += "Library/ is never committed: $f"; continue }
        if ($f -match $script:BinaryRegex -and !(Test-GlobAny $f @($Cfg.binaryAllow))) { $problems += "binary not allowed: $f" }
        foreach ($rule in @($Cfg.tree.forbidden)) {
            if ((Test-GlobAny $f @($rule.glob)) -and ((& $ReadText $f) -match $rule.regex)) { $problems += "${f}: $($rule.why)" }
        }
    }
    if ($Pr -gt 0 -and $Cfg.tree.frameworkPaths -and (Test-MatchesAny $PrPaths @($Cfg.tree.frameworkPaths))) {
        $added = @($SourcesDiff -split "`r?`n" | Where-Object { $_ -match '^[+-][^+-]' })
        # shortcut: any changed SOURCES line naming carry-unidad counts as the carry block; tighten once carry-unidad.ps1 fixes its markers.
        $carry = $added | Where-Object { $_ -match 'carry-unidad' }
        if (!$carry -and !($added | Where-Object { $_.StartsWith("+| #$Pr |") })) { $problems += "framework edit without a Divergences row | #$Pr |" }
    }
    , $problems
}

# Smallest i in 1..Count with IsGreen(i) false, given IsGreen(0) true and IsGreen(Count) false; ceil(log2 Count) calls.
function Find-FirstRed([int]$Count, [scriptblock]$IsGreen) {
    $lo = 0; $hi = $Count
    while ($hi - $lo -gt 1) {
        $mid = [int][Math]::Floor(($lo + $hi) / 2)
        if (& $IsGreen $mid) { $lo = $mid } else { $hi = $mid }
    }
    $hi
}

function Test-VerdictUsable($Verdict, [string]$CurrentHead) {
    [bool]($Verdict -and $Verdict.verdict -eq 'pass' -and $Verdict.head -and $Verdict.head -eq $CurrentHead)
}

# A repo.json command line: whitespace-separated tokens, "{dotnet}" replaced. Runs in $Root; returns the exit code.
function Invoke-RepoCommand([string]$Run, [string]$Root, [string]$Dotnet, [switch]$Quiet) {
    $tokens = @($Run.Trim() -split '\s+' | ForEach-Object { if ($_ -eq '{dotnet}') { $Dotnet } else { $_ } })
    Push-Location $Root
    try {
        $exe = $tokens[0]; $rest = @($tokens | Select-Object -Skip 1)
        if ($Quiet) { $out = & $exe @rest 2>&1; $code = $LASTEXITCODE; $script:LastCommandOutput = $out }
        else { & $exe @rest 2>&1 | Tee-Object -Variable out | Out-Host; $code = $LASTEXITCODE; $script:LastCommandOutput = $out }
        $code
    } finally { Pop-Location }
}

# The refpack directory current.txt points at, or $null (U-43).
function Get-RefpackPointer([string]$RepoName) {
    $ptr = Join-Path (Get-ToolsHome) "refpacks/$RepoName/current.txt"
    if (!(Test-Path -LiteralPath $ptr)) { return $null }
    $dir = (Get-Content -Raw -LiteralPath $ptr).Trim()
    if ($dir -and (Test-Path -LiteralPath $dir)) { $dir }
}
