[CmdletBinding()]
param(
    [string]$Version = '1.3.0',
    [ValidateSet(2024)]
    [int[]]$RevitVersions = @(2024),
    [string]$StageRoot
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $StageRoot) { $StageRoot = Join-Path $repoRoot 'artifacts\staging\TUNG_DIROOT' }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Invalid semantic version: $Version" }

New-Item -ItemType Directory -Force -Path $StageRoot | Out-Null
$stageFullPath = [IO.Path]::GetFullPath($StageRoot)
$stageDriveRoot = [IO.Path]::GetPathRoot($stageFullPath)
if ([StringComparer]::OrdinalIgnoreCase.Equals(
    $stageFullPath.TrimEnd([IO.Path]::DirectorySeparatorChar),
    $stageDriveRoot.TrimEnd([IO.Path]::DirectorySeparatorChar))) {
    throw "Refusing to use a drive root as StageRoot: $stageFullPath"
}
$repoFullPath = [IO.Path]::GetFullPath($repoRoot).TrimEnd([IO.Path]::DirectorySeparatorChar)
$stageBoundary = $stageFullPath.TrimEnd([IO.Path]::DirectorySeparatorChar)
if ([StringComparer]::OrdinalIgnoreCase.Equals($stageBoundary, $repoFullPath) -or
    $repoFullPath.StartsWith($stageBoundary + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "StageRoot must not be the repository or one of its parent directories: $stageFullPath"
}
foreach ($unsupported in 2025, 2026) {
    $unsupportedPath = Join-Path $stageFullPath ([string]$unsupported)
    if (Test-Path -LiteralPath $unsupportedPath -PathType Container) { Remove-Item -LiteralPath $unsupportedPath -Recurse -Force }
}
$releaseMetadataPath = Join-Path $StageRoot 'release.json'
# release.json describes the payload; it cannot safely contain a hash of itself.
Remove-Item -LiteralPath $releaseMetadataPath -Force -ErrorAction SilentlyContinue
$samplesDestination = Join-Path $StageRoot 'Samples'
$docsDestination = Join-Path $StageRoot 'Docs'
foreach ($payloadDirectory in @($samplesDestination, $docsDestination)) {
    $payloadFullPath = [IO.Path]::GetFullPath($payloadDirectory)
    if (-not $payloadFullPath.StartsWith($stageFullPath.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to replace a payload directory outside staging: $payloadFullPath"
    }
    if (Test-Path -LiteralPath $payloadFullPath -PathType Container) {
        Remove-Item -LiteralPath $payloadFullPath -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $payloadFullPath | Out-Null
}
Copy-Item -LiteralPath (Join-Path $repoRoot 'samples\TUNG_DIROOT-ParameterLoader.xlsx') -Destination $samplesDestination -Force
foreach ($name in @('user-guide-vi.md', 'user-guide-datalink.md', 'user-guide-filter.md', 'user-guide-sheet-tools.md', 'user-guide-parameter-tools.md', 'user-guide-family-reviser.md', 'install-guide-vi.md', 'troubleshooting-vi.md')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot "docs\$name") -Destination $docsDestination -Force
}
$testingDocsDestination = Join-Path $docsDestination 'testing'
New-Item -ItemType Directory -Force -Path $testingDocsDestination | Out-Null
foreach ($name in @('datalink-revit-2024-checklist.md', 'filter-revit-2024-checklist.md', 'tablegen-advanced-revit-2024-checklist.md', 'document-link-revit-2024-checklist.md', 'bulk-vg-revit-2024-checklist.md', 'sheet-tools-revit-2024-checklist.md', 'reordering-revit-2024-checklist.md', 'paramanager-revit-2024-checklist.md', 'family-reviser-revit-2024-checklist.md', 'release-matrix.md')) {
    Copy-Item -LiteralPath (Join-Path $repoRoot "docs\testing\$name") -Destination $testingDocsDestination -Force
}
$manifest = [System.Collections.Generic.List[object]]::new()
foreach ($year in $RevitVersions) {
    $source = Join-Path $repoRoot "artifacts\addin\$year"
    $mainAssembly = Join-Path $source "TUNG_DIROOT.Revit$year.dll"
    if (-not (Test-Path -LiteralPath $mainAssembly -PathType Leaf)) { throw "Missing built add-in for Revit $year. Run Build-Matrix first." }
    $destination = Join-Path $StageRoot ([string]$year)
    $destinationFullPath = [IO.Path]::GetFullPath($destination)
    if (-not $destinationFullPath.StartsWith($stageFullPath.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to replace a runtime directory outside staging: $destinationFullPath"
    }
    if (Test-Path -LiteralPath $destinationFullPath -PathType Container) {
        Remove-Item -LiteralPath $destinationFullPath -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $destination | Out-Null
    foreach ($file in Get-ChildItem -LiteralPath $source -File) {
        if ($file.Name -match '^RevitAPI(UI)?\.dll$') { throw "Revit API must not ship: $($file.FullName)" }
        if ($file.Extension -notin '.dll', '.json') { continue }
        Copy-Item -LiteralPath $file.FullName -Destination (Join-Path $destination $file.Name) -Force
    }
    if (Get-ChildItem -LiteralPath $destination -Filter 'RevitAPI*.dll' -File -ErrorAction SilentlyContinue) { throw "Revit API leaked into staging for $year." }
}

$notice = Join-Path $repoRoot 'installer\THIRD-PARTY-NOTICES.txt'
if (Test-Path -LiteralPath $notice -PathType Leaf) { Copy-Item -LiteralPath $notice -Destination (Join-Path $StageRoot 'THIRD-PARTY-NOTICES.txt') -Force }
foreach ($file in Get-ChildItem -LiteralPath $StageRoot -Recurse -File | Sort-Object FullName) {
    $relative = $file.FullName.Substring((Resolve-Path -LiteralPath $StageRoot).Path.Length).TrimStart('\').Replace('\', '/')
    $manifest.Add([ordered]@{ path = $relative; size = $file.Length; sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash })
}
$commit = (& git -C $repoRoot rev-parse HEAD 2>$null)
$metadata = [ordered]@{
    version = $Version
    buildTimeUtc = [DateTime]::UtcNow.ToString('o')
    gitCommit = if ($LASTEXITCODE -eq 0) { [string]$commit } else { 'unknown' }
    revitVersions = @($RevitVersions)
    files = $manifest
}
$metadata | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $releaseMetadataPath -Encoding UTF8
Write-Host "Staged TUNG_DIROOT $Version at $StageRoot"
