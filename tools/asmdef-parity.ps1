# Every asmdef equals its sidecar csproj (U-07): references, allowUnsafeCode, a csc.rsp with -langversion:14 and a
# Polyfills.cs beside it; and every sidecar csproj maps back to an asmdef. Roots and folders come from tools/repo.json.
#   pwsh -File tools/asmdef-parity.ps1          exit 0 equal, 1 mismatch (one line per problem), 2 usage
# noEngineReferences asmdef X <-> <logicDir>/X/X.csproj; any other asmdef X <-> <checkDir>/X.Check.csproj.
# Unity.* / glTFast references are not repo asmdefs: they come from the refpack and are never compared.
param([string]$Root)
. (Join-Path $PSScriptRoot 'lib/UnidadTools.ps1')
if (!$Root) { $Root = Get-RepoRoot $PSScriptRoot }
$cfg = Get-RepoConfig $Root
$p = $cfg.parity

function Test-Hidden([string]$Rel) { [bool](($Rel -split '/') | Where-Object { $_ -like '*~' -or $_ -like '.*' }) }

$asmdefs = @(foreach ($r in $p.roots) {
    $dir = Join-Path $Root $r
    if (!(Test-Path -LiteralPath $dir)) { continue }
    Get-ChildItem -LiteralPath $dir -Recurse -File -Filter '*.asmdef' | ForEach-Object {
        $rel = [IO.Path]::GetRelativePath($Root, $_.FullName) -replace '\\', '/'
        if (!(Test-Hidden $rel)) { [pscustomobject]@{ Rel = $rel; File = $_ } }
    }
})

$names = @{}; $guids = @{}
foreach ($a in $asmdefs) {
    $json = Get-Content -Raw -LiteralPath $a.File.FullName | ConvertFrom-Json -AsHashtable
    $a | Add-Member Name $json.name
    $a | Add-Member Json $json
    $names[$json.name] = $a
    $meta = "$($a.File.FullName).meta"
    if ((Test-Path -LiteralPath $meta) -and ((Get-Content -Raw -LiteralPath $meta) -match '(?m)^guid:\s*([0-9a-fA-F]{32})')) { $guids[$Matches[1].ToLowerInvariant()] = $json.name }
}
$repoNames = @($names.Keys)

$problems = @()
foreach ($a in $asmdefs) {
    $csproj = if ($a.Json.noEngineReferences) { "$($p.logicDir)/$($a.Name)/$($a.Name).csproj" } else { "$($p.checkDir)/$($a.Name).Check.csproj" }
    $full = Join-Path $Root $csproj
    if (!(Test-Path -LiteralPath $full)) { $problems += "asmdef-parity: $($a.Rel): no csproj (expected $csproj)"; continue }
    $dir = $a.File.DirectoryName
    $rsp = Join-Path $dir 'csc.rsp'
    $rspText = if (Test-Path -LiteralPath $rsp) { Get-Content -Raw -LiteralPath $rsp } else { $null }
    $found = Compare-AsmdefCsproj (Get-Content -Raw -LiteralPath $a.File.FullName) (Get-Content -Raw -LiteralPath $full) $rspText `
        (Test-Path -LiteralPath (Join-Path $dir 'Polyfills.cs')) $repoNames $guids
    foreach ($f in $found) { $problems += "asmdef-parity: $($a.Rel): $f" }
}

# Reverse: a sidecar project that no asmdef mirrors.
$logic = Join-Path $Root $p.logicDir
if (Test-Path -LiteralPath $logic) {
    foreach ($d in Get-ChildItem -LiteralPath $logic -Directory) {
        $n = $d.Name
        if (!(Test-Path -LiteralPath (Join-Path $d.FullName "$n.csproj"))) { continue }
        if (!($p.projectPrefixes | Where-Object { $n.StartsWith($_) }) -or $n.EndsWith('.Smoke') -or $n -eq 'Tools' -or $p.ignoreProjects -contains $n) { continue }
        if (!$names.ContainsKey($n)) { $problems += "asmdef-parity: $($p.logicDir)/$n/$n.csproj: csproj without asmdef" }
    }
}
$check = Join-Path $Root $p.checkDir
if (Test-Path -LiteralPath $check) {
    foreach ($f in Get-ChildItem -LiteralPath $check -File -Filter '*.Check.csproj') {
        $n = $f.Name -replace '\.Check\.csproj$', ''
        if (!$names.ContainsKey($n)) { $problems += "asmdef-parity: $($p.checkDir)/$($f.Name): csproj without asmdef" }
    }
}

$problems | ForEach-Object { Write-Host $_ }
exit ([int][bool]$problems)
