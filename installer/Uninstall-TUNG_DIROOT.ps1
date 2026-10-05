[CmdletBinding()]
param(
    [string]$InstallRoot,
    [string]$ManifestRoot,
    [string]$Version = '1.3.0',
    [ValidateSet(2024)][int[]]$RevitVersions = @(2024),
    [switch]$RemoveUserSettingsAndLogs
)

$ErrorActionPreference = 'Stop'
if (-not $InstallRoot) { $InstallRoot = Join-Path $env:LocalAppData "TUNG_DIROOT\$Version" }
if (-not $ManifestRoot) { $ManifestRoot = Join-Path $env:AppData 'Autodesk\Revit\Addins' }
$install = [IO.Path]::GetFullPath($InstallRoot)
$marker = Join-Path $install 'TUNG_DIROOT.install-marker'
$expectedMarker = 'TUNG_DIROOT:8A93D06F-E03B-4A7C-9A96-DF5A3D5B0E24'
if (Test-Path -LiteralPath $install -PathType Container) {
    if (-not (Test-Path -LiteralPath $marker -PathType Leaf) -or
        -not [StringComparer]::Ordinal.Equals(([IO.File]::ReadAllText($marker)).Trim(), $expectedMarker)) {
        throw "Refusing to remove an install directory without the TUNG_DIROOT ownership marker: $install"
    }
}
foreach ($year in $RevitVersions) {
    $manifest = Join-Path (Join-Path $ManifestRoot ([string]$year)) 'TUNG_DIROOT.addin'
    if (Test-Path -LiteralPath $manifest -PathType Leaf) {
        try {
            [xml]$xml = Get-Content -LiteralPath $manifest -Raw
            $assembly = [string]$xml.RevitAddIns.AddIn.Assembly
            if ($assembly -and [IO.Path]::GetFullPath($assembly).StartsWith($install + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                Remove-Item -LiteralPath $manifest -Force
            }
        }
        catch { throw "Refusing to remove an unreadable manifest: $manifest" }
    }
}
if (Test-Path -LiteralPath $install -PathType Container) { Remove-Item -LiteralPath $install -Recurse -Force }
if ($RemoveUserSettingsAndLogs) {
    $userData = Join-Path $env:LocalAppData 'TUNG_DIROOT'
    foreach ($target in @((Join-Path $userData 'settings.json'), (Join-Path $userData 'Logs'))) {
        if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Recurse -Force }
    }
}
Write-Host 'Uninstalled TUNG_DIROOT program files and managed manifests. RVT/Excel files were not opened or modified.'
