# The lane's prepare step for unidad (repo.json lane.prepare, U-46): copies the Movement sample out of Samples~ into the
# gitignored Sandbox/Assets/Samples/Movement, so every Sandbox compile covers it. Runs in a slot root after checkout.
$ErrorActionPreference = 'Stop'
$root = (git rev-parse --show-toplevel).Trim()
$src = Join-Path $root 'Packages/com.unidad.unity/Samples~/Movement'
$dst = Join-Path $root 'Sandbox/Assets/Samples/Movement'
if (!(Test-Path -LiteralPath $src)) {
    if (Test-Path -LiteralPath $dst) { Remove-Item -LiteralPath $dst -Recurse -Force }
    Write-Host 'sandbox-prepare: no Movement sample yet'
    exit 0
}
# /XF: /MIR would purge the .gen.csproj files MSBuild mode writes, and the lane would build a .slnx without them.
robocopy $src $dst /MIR /XF *.gen.csproj /NFL /NDL /NJH /NJS /R:1 /W:1 | Out-Null
if ($LASTEXITCODE -ge 8) { Write-Host "sandbox-prepare: robocopy failed ($LASTEXITCODE)"; exit 1 }
Write-Host "sandbox-prepare: Movement sample -> Sandbox/Assets/Samples/Movement"
exit 0
