# The committed analyzer DLL, built deterministically (Deterministic, DebugType none, PathMap: byte-identical from any slot).
#   pwsh -File analyzers/build.ps1          build Release and publish into Packages/com.unidad.core/Analyzers/ (+ fixed-GUID metas)
#   pwsh -File analyzers/build.ps1 -Check   build into a temp folder; exit 1 when its SHA-256 differs from the committed DLL
# Exit 0 ok, 1 red, 2 environment. Exit codes only: the console may be localised.
param([switch]$Check)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '../tools/lib/UnidadTools.ps1')
$root = Get-RepoRoot $PSScriptRoot
$cfg = Get-RepoConfig $root
try {
    $dotnet = Resolve-UnityDotnet (Resolve-UnityEditor (Get-UnityProjectPath $cfg $root) $null $cfg.editorVersion)
} catch { Write-Host "analyzers/build: $($_.Exception.Message)"; exit 2 }

$proj = Join-Path $root 'analyzers/Unidad.Analyzers/Unidad.Analyzers.csproj'
$folder = Join-Path $root 'Packages/com.unidad.core/Analyzers'
$dll = Join-Path $folder 'Unidad.Analyzers.dll'
# The SDK stamps the HEAD commit into AssemblyInformationalVersion, which would change the bytes on every commit.
$fixed = '-p:IncludeSourceRevisionInInformationalVersion=false'

if ($Check) {
    if (!(Test-Path -LiteralPath $dll)) { Write-Host "analyzers/build: no committed DLL at $dll"; exit 1 }
    $out = Join-Path ([IO.Path]::GetTempPath()) "unidad-analyzer-check-$PID"
    try {
        & $dotnet build $proj -c Release -nologo -v:q $fixed -o $out | Out-Host
        if ($LASTEXITCODE -ne 0) { Write-Host 'analyzers/build: build failed'; exit 1 }
        $built = (Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $out 'Unidad.Analyzers.dll')).Hash
        $committed = (Get-FileHash -Algorithm SHA256 -LiteralPath $dll).Hash
        if ($built -ne $committed) {
            Write-Host "analyzers/build: committed DLL $committed is not the build of the source ($built); run pwsh -File analyzers/build.ps1 and commit"
            exit 1
        }
        Write-Host "analyzers/build: committed DLL matches the source ($built)"
        exit 0
    } finally { Remove-Item -Recurse -Force -LiteralPath $out -ErrorAction SilentlyContinue }
}

& $dotnet build $proj -c Release -nologo -v:q $fixed -p:UnidadPublishAnalyzer=true | Out-Host
if ($LASTEXITCODE -ne 0) { Write-Host 'analyzers/build: build failed'; exit 1 }

# Fixed GUIDs (not md5 of the path): games reference the DLL by this GUID wherever they carry it (SPEC unidad-analyzers b.5).
$folderMeta = "$folder.meta"
if (!(Test-Path -LiteralPath $folderMeta)) {
    Set-Content -NoNewline -LiteralPath $folderMeta -Value (@(
        'fileFormatVersion: 2', 'guid: 69659cc545be4dfa9ab241d56a88321c', 'folderAsset: yes', 'DefaultImporter:',
        '  externalObjects: {}', '  userData: ', '  assetBundleName: ', '  assetBundleVariant: ', '') -join "`n")
}
$dllMeta = "$dll.meta"
if (!(Test-Path -LiteralPath $dllMeta)) {
    Set-Content -NoNewline -LiteralPath $dllMeta -Value (@(
        'fileFormatVersion: 2', 'guid: eba4f209027c4322bae53a90fb477b4f', 'labels:', '- RoslynAnalyzer', 'PluginImporter:',
        '  externalObjects: {}', '  serializedVersion: 2', '  iconMap: {}', '  executionOrder: {}', '  defineConstraints: []',
        '  isPreloaded: 0', '  isOverridable: 0', '  isExplicitlyReferenced: 0', '  validateReferences: 1', '  platformData:',
        '  - first:', '      Any: ', '    second:', '      enabled: 0', '      settings: {}', '  - first:', '      Editor: Editor',
        '    second:', '      enabled: 0', '      settings:', '        DefaultValueInitialized: true', '  userData: ',
        '  assetBundleName: ', '  assetBundleVariant: ', '') -join "`n")
}
Write-Host "analyzers/build: published $dll ($((Get-FileHash -Algorithm SHA256 -LiteralPath $dll).Hash))"
exit 0
