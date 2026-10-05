[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$BackupPath, [string]$RestoreRoot, [string]$ManifestRoot)
$ErrorActionPreference = 'Stop'
if (-not $RestoreRoot) { $RestoreRoot = Join-Path $env:LocalAppData ('TUNG_DIROOT\Restored\' + (Get-Date -Format 'yyyyMMdd_HHmmss_ffff')) }
if (-not $ManifestRoot) { $ManifestRoot = Join-Path $env:AppData 'Autodesk\Revit\Addins' }
if ([StringComparer]::OrdinalIgnoreCase.Equals([IO.Path]::GetFullPath($ManifestRoot).TrimEnd('\'),
        [IO.Path]::GetFullPath((Join-Path $env:AppData 'Autodesk\Revit\Addins')).TrimEnd('\')) -and
    @(Get-Process -Name Revit -ErrorAction SilentlyContinue).Count -gt 0) {
    throw 'Close Revit before restoring TUNG_DIROOT. No installation files have been changed.'
}
$backup = [IO.Path]::GetFullPath($BackupPath).TrimEnd('\')
$restore = [IO.Path]::GetFullPath($RestoreRoot).TrimEnd('\')
if ($restore.Equals($backup, [StringComparison]::OrdinalIgnoreCase) -or
    $restore.StartsWith($backup + '\', [StringComparison]::OrdinalIgnoreCase) -or
    $backup.StartsWith($restore + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'RestoreRoot and BackupPath must not overlap.' }
if (Test-Path -LiteralPath $restore) { throw "RestoreRoot must be a new directory: $restore" }
$metadata = Get-Content -LiteralPath (Join-Path $backup 'backup.json') -Raw | ConvertFrom-Json
if ($metadata.format -ne 'TUNG_DIROOT_INSTALL_BACKUP_V1') { throw 'Unsupported installation backup.' }
if (-not $metadata.PSObject.Properties['programs'] -or -not $metadata.PSObject.Properties['manifests'] -or
    -not $metadata.PSObject.Properties['files'] -or -not $metadata.programs -or -not $metadata.files -or
    @($metadata.programs).Count -eq 0 -or @($metadata.files).Count -eq 0) {
    throw 'Installation backup metadata is incomplete.'
}
if (@(Get-ChildItem -LiteralPath $backup -Force -Recurse | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }).Count -gt 0) {
    throw 'Backup must not contain reparse points.'
}
function Resolve-BackupFile([string]$relative) {
    if ([IO.Path]::IsPathRooted($relative)) { throw 'Backup relative path must not be rooted.' }
    $resolved = [IO.Path]::GetFullPath((Join-Path $backup $relative))
    if (-not $resolved.StartsWith($backup + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Backup path escapes its directory.' }
    return $resolved
}
$verifiedFiles = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($file in $metadata.files) {
    $path = Resolve-BackupFile $file.relativePath
    if (-not $verifiedFiles.Add($path)) { throw 'Backup contains duplicate file entries.' }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $file.sha256) {
        throw "Backup file is missing or changed: $($file.relativePath)"
    }
}
$actualFiles = @(Get-ChildItem -LiteralPath $backup -File -Recurse | Where-Object {
    -not [StringComparer]::OrdinalIgnoreCase.Equals($_.FullName, (Join-Path $backup 'backup.json'))
})
if ($actualFiles.Count -ne $verifiedFiles.Count -or @($actualFiles | Where-Object { -not $verifiedFiles.Contains($_.FullName) }).Count -gt 0) {
    throw 'Backup contains unlisted payload files.'
}
foreach ($program in $metadata.programs) {
    $source = Resolve-BackupFile $program.relativePath
    $marker = Join-Path $source 'TUNG_DIROOT.install-marker'
    if (-not (Test-Path -LiteralPath $marker -PathType Leaf) -or
        [IO.File]::ReadAllText($marker).Trim() -ne 'TUNG_DIROOT:8A93D06F-E03B-4A7C-9A96-DF5A3D5B0E24') {
        throw 'Backup program ownership marker is invalid.'
    }
}
foreach ($entry in $metadata.manifests) {
    if ([int]$entry.year -ne 2024) { throw 'Backup contains an unsupported Revit version.' }
    if ([int]$entry.programIndex -lt 0 -or [int]$entry.programIndex -ge @($metadata.programs).Count) { throw 'Backup program index is invalid.' }
    $source = Resolve-BackupFile $metadata.programs[[int]$entry.programIndex].relativePath
    $assembly = Join-Path (Join-Path $source ([string]$entry.year)) "TUNG_DIROOT.Revit$($entry.year).dll"
    if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) { throw 'Backed-up Revit assembly is missing.' }
    $target = Join-Path (Join-Path $ManifestRoot ([string]$entry.year)) 'TUNG_DIROOT.addin'
    if (Test-Path -LiteralPath $target -PathType Leaf) {
        [xml]$currentManifest = Get-Content -LiteralPath $target -Raw
        if ([string]$currentManifest.RevitAddIns.AddIn.AddInId -ne '8A93D06F-E03B-4A7C-9A96-DF5A3D5B0E24') { throw 'Current manifest is not owned by TUNG_DIROOT.' }
    }
}
New-Item -ItemType Directory -Path $restore -Force | Out-Null
for ($index = 0; $index -lt @($metadata.programs).Count; $index++) {
    $source = Resolve-BackupFile $metadata.programs[$index].relativePath
    Copy-Item -LiteralPath $source -Destination (Join-Path $restore ([string]$index)) -Recurse
}
$manifestSafetyBackup = Join-Path $restore 'PreviousManifests'
foreach ($entry in $metadata.manifests) {
    $target = Join-Path (Join-Path $ManifestRoot ([string]$entry.year)) 'TUNG_DIROOT.addin'
    if (Test-Path -LiteralPath $target -PathType Leaf) {
        New-Item -ItemType Directory -Path (Join-Path $manifestSafetyBackup ([string]$entry.year)) -Force | Out-Null
        Copy-Item -LiteralPath $target -Destination (Join-Path (Join-Path $manifestSafetyBackup ([string]$entry.year)) 'TUNG_DIROOT.addin')
    }
    $assembly = Join-Path (Join-Path (Join-Path $restore ([string]$entry.programIndex)) ([string]$entry.year)) "TUNG_DIROOT.Revit$($entry.year).dll"
    & (Join-Path $PSScriptRoot 'New-RevitManifest.ps1') -RevitVersion $entry.year -AssemblyPath $assembly -OutputPath $target
}
Write-Host "Restored previous runtime at $restore. Restart Revit to use it. Current installation remains available."
