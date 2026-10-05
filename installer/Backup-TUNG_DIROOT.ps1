[CmdletBinding()]
param(
    [string]$InstallRoot,
    [string]$ManifestRoot,
    [string]$BackupRoot,
    [ValidateSet(2024)][int[]]$RevitVersions = @(2024)
)
$ErrorActionPreference = 'Stop'
if (-not $ManifestRoot) { $ManifestRoot = Join-Path $env:AppData 'Autodesk\Revit\Addins' }
if (-not $BackupRoot) { $BackupRoot = Join-Path $env:LocalAppData 'TUNG_DIROOT\Backups' }
if ([StringComparer]::OrdinalIgnoreCase.Equals([IO.Path]::GetFullPath($ManifestRoot).TrimEnd('\'),
        [IO.Path]::GetFullPath((Join-Path $env:AppData 'Autodesk\Revit\Addins')).TrimEnd('\')) -and
    @(Get-Process -Name Revit -ErrorAction SilentlyContinue).Count -gt 0) {
    throw 'Close Revit before upgrading TUNG_DIROOT. No installation files have been changed.'
}
$markerValue = 'TUNG_DIROOT:8A93D06F-E03B-4A7C-9A96-DF5A3D5B0E24'
$backupRootFull = [IO.Path]::GetFullPath($BackupRoot).TrimEnd('\')
if ($InstallRoot) {
    $requestedInstallFull = [IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')
    if ($backupRootFull.Equals($requestedInstallFull, [StringComparison]::OrdinalIgnoreCase) -or
        $backupRootFull.StartsWith($requestedInstallFull + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'BackupRoot must be outside the requested installation directory.'
    }
}
if ($InstallRoot -and (Test-Path -LiteralPath $InstallRoot -PathType Container)) {
    $installFull = [IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')
    $marker = Join-Path $installFull 'TUNG_DIROOT.install-marker'
    if ([StringComparer]::OrdinalIgnoreCase.Equals($installFull, [IO.Path]::GetPathRoot($installFull).TrimEnd('\'))) {
        throw 'InstallRoot must not be a drive root.'
    }
    if (@(Get-ChildItem -LiteralPath $installFull -Force).Count -gt 0 -and
        (-not (Test-Path -LiteralPath $marker -PathType Leaf) -or [IO.File]::ReadAllText($marker).Trim() -ne $markerValue)) {
        throw "Refusing to replace a nonempty directory not owned by TUNG_DIROOT: $installFull"
    }
}
$sources = [System.Collections.Generic.List[string]]::new()
$manifestSources = [System.Collections.Generic.List[object]]::new()
foreach ($year in $RevitVersions) {
    $manifest = Join-Path (Join-Path $ManifestRoot ([string]$year)) 'TUNG_DIROOT.addin'
    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { continue }
    [xml]$xml = Get-Content -LiteralPath $manifest -Raw
    $addin = $xml.RevitAddIns.AddIn
    if ([string]$addin.AddInId -ne '8A93D06F-E03B-4A7C-9A96-DF5A3D5B0E24') { throw "Manifest is not owned by TUNG_DIROOT: $manifest" }
    $assembly = [IO.Path]::GetFullPath([string]$addin.Assembly)
    $source = Split-Path -Parent (Split-Path -Parent $assembly)
    if (-not $sources.Contains($source)) { $sources.Add($source) }
    $manifestSources.Add([pscustomobject]@{ year = $year; sourcePath = $manifest; programRoot = $source })
}
if ($InstallRoot -and (Test-Path -LiteralPath (Join-Path $InstallRoot 'TUNG_DIROOT.install-marker') -PathType Leaf)) {
    $source = [IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')
    if (-not $sources.Contains($source)) { $sources.Add($source) }
}
if ($sources.Count -eq 0 -and $manifestSources.Count -eq 0) { return }
foreach ($source in $sources) {
    $marker = Join-Path $source 'TUNG_DIROOT.install-marker'
    if ([StringComparer]::OrdinalIgnoreCase.Equals($source.TrimEnd('\'), [IO.Path]::GetPathRoot($source).TrimEnd('\')) -or
        -not (Test-Path -LiteralPath $marker -PathType Leaf) -or
        [IO.File]::ReadAllText($marker).Trim() -ne $markerValue) {
        throw "Cannot back up an installation without its ownership marker: $source"
    }
}
foreach ($source in $sources) {
    if ($backupRootFull.Equals($source, [StringComparison]::OrdinalIgnoreCase) -or
        $backupRootFull.StartsWith($source.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'BackupRoot must be outside all installation directories.'
    }
}
$backup = Join-Path $backupRootFull (Get-Date -Format 'yyyyMMdd_HHmmss_ffff')
if (Test-Path -LiteralPath $backup) { throw "Backup already exists: $backup" }
New-Item -ItemType Directory -Path $backup -Force | Out-Null
$programs = @()
for ($index = 0; $index -lt $sources.Count; $index++) {
    $relative = 'Program\' + $index
    $destination = Join-Path $backup $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $sources[$index] -Destination $destination -Recurse
    $programs += [ordered]@{ sourceRoot = $sources[$index]; relativePath = $relative }
}
$manifests = @()
foreach ($entry in $manifestSources) {
    $relative = 'Manifests\' + $entry.year + '\TUNG_DIROOT.addin'
    $destination = Join-Path $backup $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $entry.sourcePath -Destination $destination
    $programIndex = $sources.IndexOf($entry.programRoot)
    $manifests += [ordered]@{ year = $entry.year; relativePath = $relative; programIndex = $programIndex }
}
$files = @(Get-ChildItem -LiteralPath $backup -File -Recurse | ForEach-Object {
    [ordered]@{ relativePath = $_.FullName.Substring($backup.Length + 1); sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
[ordered]@{ format = 'TUNG_DIROOT_INSTALL_BACKUP_V1'; createdUtc = [DateTime]::UtcNow.ToString('o'); programs = $programs; manifests = $manifests; files = $files } |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $backup 'backup.json') -Encoding UTF8
Write-Host "Backed up previous TUNG_DIROOT installation: $backup"
Write-Output $backup
