[CmdletBinding()]
param(
    [string]$StageRoot,
    [string]$InstallRoot,
    [string]$ManifestRoot,
    [string]$BackupRoot,
    [string]$Version = '1.3.0',
    [ValidateSet(2024)][int[]]$RevitVersions = @(2024)
)

$ErrorActionPreference = 'Stop'
if (-not $StageRoot) {
    $portableStage = Join-Path $PSScriptRoot 'TUNG_DIROOT'
    $repoStage = Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\staging\TUNG_DIROOT'
    $StageRoot = if (Test-Path -LiteralPath $portableStage -PathType Container) { $portableStage } else { $repoStage }
}
if (-not $InstallRoot) { $InstallRoot = Join-Path $env:LocalAppData "TUNG_DIROOT\$Version" }
if (-not $ManifestRoot) { $ManifestRoot = Join-Path $env:AppData 'Autodesk\Revit\Addins' }

$stage = [IO.Path]::GetFullPath($StageRoot)
$install = [IO.Path]::GetFullPath($InstallRoot)
$separator = [IO.Path]::DirectorySeparatorChar
if ([StringComparer]::OrdinalIgnoreCase.Equals($stage.TrimEnd($separator), $install.TrimEnd($separator)) -or
    $stage.StartsWith($install.TrimEnd($separator) + $separator, [StringComparison]::OrdinalIgnoreCase) -or
    $install.StartsWith($stage.TrimEnd($separator) + $separator, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'StageRoot and InstallRoot must not overlap.'
}
$markerName = 'TUNG_DIROOT.install-marker'
$markerValue = 'TUNG_DIROOT:8A93D06F-E03B-4A7C-9A96-DF5A3D5B0E24'
foreach ($year in $RevitVersions) {
    $requiredAssembly = Join-Path (Join-Path $stage ([string]$year)) "TUNG_DIROOT.Revit$year.dll"
    if (-not (Test-Path -LiteralPath $requiredAssembly -PathType Leaf)) { throw "Portable payload is missing Revit $year." }
}
$backup = & (Join-Path $PSScriptRoot 'Backup-TUNG_DIROOT.ps1') -InstallRoot $install -ManifestRoot $ManifestRoot -BackupRoot $BackupRoot -RevitVersions $RevitVersions
New-Item -ItemType Directory -Force -Path $install | Out-Null
[IO.File]::WriteAllText((Join-Path $install $markerName), $markerValue, [Text.UTF8Encoding]::new($false))
foreach ($year in $RevitVersions) {
    $source = Join-Path $stage ([string]$year)
    $assemblyName = "TUNG_DIROOT.Revit$year.dll"
    $destination = Join-Path $install ([string]$year)
    if (Test-Path -LiteralPath $destination -PathType Container) {
        $fullDestination = [IO.Path]::GetFullPath($destination)
        if (-not $fullDestination.StartsWith($install + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to replace a directory outside the install root: $fullDestination"
        }
        Remove-Item -LiteralPath $fullDestination -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    Get-ChildItem -LiteralPath $source -File | Copy-Item -Destination $destination -Force
    $manifest = Join-Path (Join-Path $ManifestRoot ([string]$year)) 'TUNG_DIROOT.addin'
    & (Join-Path $PSScriptRoot 'New-RevitManifest.ps1') -RevitVersion $year -AssemblyPath (Join-Path $destination $assemblyName) -OutputPath $manifest
}
foreach ($name in @('Samples', 'Docs')) {
    $source = Join-Path $stage $name
    if (-not (Test-Path -LiteralPath $source -PathType Container)) { continue }
    $destination = [IO.Path]::GetFullPath((Join-Path $install $name))
    if (-not $destination.StartsWith($install.TrimEnd($separator) + $separator, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to replace a payload directory outside the install root: $destination"
    }
    if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Recurse -Force }
    Copy-Item -LiteralPath $source -Destination $destination -Recurse
}
foreach ($name in @('release.json', 'THIRD-PARTY-NOTICES.txt')) {
    $source = Join-Path $stage $name
    if (Test-Path -LiteralPath $source -PathType Leaf) { Copy-Item -LiteralPath $source -Destination $install -Force }
}
Write-Host "Installed TUNG_DIROOT $Version for Revit $($RevitVersions -join ', '). Restart Revit if it is open."
