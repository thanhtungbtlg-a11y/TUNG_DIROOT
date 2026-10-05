[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$StageRoot,
    [Parameter(Mandatory = $true)][string]$SandboxRoot,
    [ValidateSet(2024)][int[]]$RevitVersions = @(2024)
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sandbox = [IO.Path]::GetFullPath($SandboxRoot)
$install = Join-Path $sandbox 'Program\1.3.0'
$manifests = Join-Path $sandbox 'AppData\Autodesk\Revit\Addins'
$backups = Join-Path $sandbox 'Backups'
$userData = Join-Path $sandbox 'UserData'
New-Item -ItemType Directory -Force -Path $userData | Out-Null
$sentinel = Join-Path $userData 'settings.json'
[IO.File]::WriteAllText($sentinel, '{"keep":true}', [Text.UTF8Encoding]::new($false))

& (Join-Path $repoRoot 'installer\Install-TUNG_DIROOT.ps1') -StageRoot $StageRoot -InstallRoot $install -ManifestRoot $manifests -BackupRoot $backups -RevitVersions $RevitVersions
if (-not (Test-Path -LiteralPath (Join-Path $install 'TUNG_DIROOT.install-marker') -PathType Leaf)) { throw 'Portable install did not create its ownership marker.' }
foreach ($relative in @('Samples\TUNG_DIROOT-ParameterLoader.xlsx', 'Docs\user-guide-vi.md', 'Docs\user-guide-datalink.md')) {
    if (-not (Test-Path -LiteralPath (Join-Path $install $relative) -PathType Leaf)) {
        throw "Installed portable payload is missing: $relative"
    }
}
foreach ($year in $RevitVersions) {
    & (Join-Path $PSScriptRoot 'Test-Manifest.ps1') -RevitVersion $year -ManifestPath (Join-Path (Join-Path $manifests ([string]$year)) 'TUNG_DIROOT.addin')
}
$obsolete = Join-Path (Join-Path $install ([string]$RevitVersions[0])) 'obsolete-from-old-version.dll'
[IO.File]::WriteAllText($obsolete, 'obsolete', [Text.UTF8Encoding]::new($false))
& (Join-Path $repoRoot 'installer\Install-TUNG_DIROOT.ps1') -StageRoot $StageRoot -InstallRoot $install -ManifestRoot $manifests -BackupRoot $backups -RevitVersions $RevitVersions
if (Test-Path -LiteralPath $obsolete) { throw 'Install-over left an obsolete runtime dependency.' }
& (Join-Path $repoRoot 'installer\Uninstall-TUNG_DIROOT.ps1') -InstallRoot $install -ManifestRoot $manifests -RevitVersions $RevitVersions
if (Test-Path -LiteralPath $install) { throw 'Portable uninstall left program files.' }
foreach ($year in $RevitVersions) {
    if (Test-Path -LiteralPath (Join-Path (Join-Path $manifests ([string]$year)) 'TUNG_DIROOT.addin')) { throw "Portable uninstall left the $year manifest." }
}
if (-not (Test-Path -LiteralPath $sentinel -PathType Leaf)) { throw 'Portable uninstall removed user settings.' }
$unowned = Join-Path $sandbox 'Unowned\1.3.0'
New-Item -ItemType Directory -Force -Path $unowned | Out-Null
$unownedSentinel = Join-Path $unowned 'keep.txt'
[IO.File]::WriteAllText($unownedSentinel, 'keep', [Text.UTF8Encoding]::new($false))
$refused = $false
try { & (Join-Path $repoRoot 'installer\Uninstall-TUNG_DIROOT.ps1') -InstallRoot $unowned -ManifestRoot $manifests -RevitVersions $RevitVersions }
catch { $refused = $_.Exception.Message -like '*ownership marker*' }
if (-not $refused -or -not (Test-Path -LiteralPath $unownedSentinel -PathType Leaf)) { throw 'Portable uninstaller did not safely reject an unowned directory.' }
Write-Host "Portable install/uninstall contract passed for: $($RevitVersions -join ', ')."
