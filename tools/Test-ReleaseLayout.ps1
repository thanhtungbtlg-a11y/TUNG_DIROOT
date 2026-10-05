[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$StageRoot,
    [ValidateSet(2024)][int[]]$RevitVersions = @(2024),
    [string]$ExpectedVersion = '1.3.0'
)

$ErrorActionPreference = 'Stop'
foreach ($year in $RevitVersions) {
    $directory = Join-Path $StageRoot ([string]$year)
    $assembly = Join-Path $directory "TUNG_DIROOT.Revit$year.dll"
    if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) { throw "Missing add-in $year" }
    if (Get-ChildItem -LiteralPath $directory -Filter 'RevitAPI*.dll' -File -ErrorAction SilentlyContinue) { throw "Revit API must not ship: $year" }
    $invalidRuntime = @(Get-ChildItem -LiteralPath $directory -File -Recurse | Where-Object { $_.Extension -notin '.dll', '.json' })
    if ($invalidRuntime.Count -gt 0) { throw "Unexpected file in the Revit $year runtime payload: $($invalidRuntime[0].FullName)" }
}
if (Get-ChildItem -LiteralPath $StageRoot -Filter 'RevitAPI*.dll' -File -Recurse -ErrorAction SilentlyContinue) {
    throw 'Revit API must not ship anywhere in the staged payload.'
}
foreach ($relative in @(
    'Samples\TUNG_DIROOT-ParameterLoader.xlsx',
    'Docs\user-guide-vi.md',
    'Docs\user-guide-datalink.md',
    'Docs\user-guide-filter.md',
    'Docs\user-guide-sheet-tools.md',
    'Docs\user-guide-parameter-tools.md',
    'Docs\user-guide-family-reviser.md',
    'Docs\testing\datalink-revit-2024-checklist.md',
    'Docs\testing\filter-revit-2024-checklist.md',
    'Docs\testing\tablegen-advanced-revit-2024-checklist.md',
    'Docs\testing\sheet-tools-revit-2024-checklist.md',
    'Docs\testing\reordering-revit-2024-checklist.md',
    'Docs\testing\paramanager-revit-2024-checklist.md',
    'Docs\testing\family-reviser-revit-2024-checklist.md',
    'Docs\testing\release-matrix.md')) {
    if (-not (Test-Path -LiteralPath (Join-Path $StageRoot $relative) -PathType Leaf)) {
        throw "Missing Parameter Loader release payload: $relative"
    }
}
$releasePath = Join-Path $StageRoot 'release.json'
if (-not (Test-Path -LiteralPath $releasePath -PathType Leaf)) { throw 'Missing release.json.' }
$metadata = Get-Content -LiteralPath $releasePath -Raw | ConvertFrom-Json
if ($metadata.version -ne $ExpectedVersion) { throw "Unexpected release version: $($metadata.version)" }
if ($metadata.files.path -contains 'release.json') { throw 'release.json must not contain a self-referential hash.' }
$declaredVersions = @($metadata.revitVersions | ForEach-Object { [int]$_ } | Sort-Object)
$expectedVersions = @($RevitVersions | Sort-Object)
if (Compare-Object $declaredVersions $expectedVersions) { throw 'release.json declares an unexpected Revit version matrix.' }
foreach ($entry in $metadata.files) {
    $path = Join-Path $StageRoot ([string]$entry.path).Replace('/', '\')
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Metadata references missing file: $($entry.path)" }
    if ((Get-Item -LiteralPath $path).Length -ne [long]$entry.size) { throw "Size mismatch: $($entry.path)" }
    if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.sha256) { throw "Hash mismatch: $($entry.path)" }
}
$root = (Resolve-Path -LiteralPath $StageRoot).Path
$releaseFullPath = [IO.Path]::GetFullPath($releasePath)
$actualFiles = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object { $_.FullName -ne $releaseFullPath } |
    ForEach-Object { $_.FullName.Substring($root.Length).TrimStart('\').Replace('\', '/') } | Sort-Object)
$declaredFiles = @($metadata.files.path | ForEach-Object { [string]$_ } | Sort-Object)
if (Compare-Object $actualFiles $declaredFiles) { throw 'release.json does not cover the exact staged payload.' }
Write-Host "Release layout passed for: $($RevitVersions -join ', ')."
