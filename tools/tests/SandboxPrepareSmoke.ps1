# Smoke test of tools/sandbox-prepare.ps1 on a throwaway git repo: the mirror copies the sample, purges stale files and
# keeps the .gen.csproj files MSBuild mode wrote. Exit 1 on any failure.
$ErrorActionPreference = 'Stop'
$prepare = Join-Path (Split-Path $PSScriptRoot -Parent) 'sandbox-prepare.ps1'
$Temp = Join-Path ([IO.Path]::GetTempPath()) "sandbox-prepare-smoke-$PID"
$failed = 0
function Assert($cond, $name) { if ($cond) { Write-Host "ok   $name" } else { Write-Host "FAIL $name"; $script:failed++ } }
function Put([string]$Path, [string]$Text) { New-Item -ItemType Directory -Force (Split-Path $Path -Parent) | Out-Null; [IO.File]::WriteAllText($Path, $Text) }

try {
    git init -q -b main $Temp 2>&1 | Out-Null
    Put "$Temp/Packages/com.unidad.unity/Samples~/Movement/Logic/A.cs" 'class A {}'
    $dst = "$Temp/Sandbox/Assets/Samples/Movement/Logic"
    Put "$dst/Stale.cs" 'class Stale {}'
    Put "$dst/Unidad.Samples.Movement.gen.csproj" '<Project />'
    Push-Location $Temp
    try { & pwsh -NoProfile -File $prepare | Out-Null; $code = $LASTEXITCODE } finally { Pop-Location }
    Assert ($code -eq 0) 'Prepare_Exit0'
    Assert (Test-Path -LiteralPath "$dst/A.cs") 'Prepare_CopiesSample'
    Assert (!(Test-Path -LiteralPath "$dst/Stale.cs")) 'Prepare_PurgesStaleFiles'
    Assert (Test-Path -LiteralPath "$dst/Unidad.Samples.Movement.gen.csproj") 'Prepare_KeepsGeneratedCsproj'
} finally {
    Remove-Item -LiteralPath $Temp -Recurse -Force -ErrorAction SilentlyContinue
}
exit $(if ($failed) { 1 } else { 0 })
