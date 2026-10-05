[CmdletBinding()]
param(
    [ValidateSet(2024)]
    [int]$RevitVersion = 2024,
    [string]$AssemblyPath,
    [string]$ApiPathsFile
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$sourceRoot = Join-Path $repoRoot 'src\TungDiroot.Addin\Filter'
$requiredSources = @(
    'RevitFilterCatalogReader.cs',
    'RevitFilterSnapshotBatch.cs',
    'RevitFilterSnapshotProjector.cs',
    'RevitFilterActions.cs',
    'RevitFilterVisualization.cs'
)
foreach ($name in $requiredSources) {
    $path = Join-Path $sourceRoot $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Filter adapter source missing: $name" }
}

$source = ($requiredSources | ForEach-Object { Get-Content -LiteralPath (Join-Path $sourceRoot $_) -Raw }) -join "`n"
if ($source -notmatch 'BatchSize\s*=\s*500') { throw 'Filter snapshot batches must be capped at 500 elements.' }
if ($source -notmatch 'FilterScope\.WholeModel' -or $source -notmatch 'FilterScope\.ActiveView' -or
    $source -notmatch 'FilterScope\.CurrentSelection') {
    throw 'Filter collectors must implement Whole Model, Active View and Current Selection scopes.'
}
if ($source -notmatch 'ExpectedDocumentIdentity' -or $source -notmatch 'ActiveUIDocument') {
    throw 'Every Filter batch must carry and revalidate the originating document identity.'
}
foreach ($state in @('Cancelled', 'Completed', 'StaleDocument')) {
    if ($source -notmatch "FilterBatchStatus\.$state") { throw "Filter batch state missing: $state" }
}
if ($source -notmatch 'Take\s*\(BatchSize\)' -or $source -notmatch 'query\.Rules') {
    throw 'Filter batch projection must cap the slice and derive requested parameters from query rules.'
}
if ($source -notmatch '\.IsLinked') { throw 'Filter must explicitly reject linked documents.' }
if ($source -match '\.Save\s*\(|SynchronizeWithCentral\s*\(') {
    throw 'Filter collection must not save or synchronize the Revit document.'
}
if ($source -match '(private|protected)\s+(readonly\s+)?(Element|Parameter)(\[\]|\?|\s)') {
    throw 'Filter batch state must not retain Autodesk Element or Parameter instances between callbacks.'
}
if ($source -notmatch 'SelectionFilterElement' -or $source -notmatch 'ShowElements\s*\(' -or
    $source -notmatch 'IsolateElementsTemporary\s*\(' -or $source -notmatch 'HideElementsTemporary\s*\(') {
    throw 'Filter result actions must support saved selection sets, zoom, temporary isolation and temporary hide.'
}
if ($source -notmatch 'TUNG_FILTER_' -or $source -notmatch 'CreateIsometric\s*\(') {
    throw 'Filter visualization must use a dedicated TUNG_FILTER_ isometric 3D view.'
}
if ($source -notmatch 'SetElementOverrides\s*\(' -or $source -notmatch 'DisableTemporaryViewMode\s*\(') {
    throw 'Filter visualization must refresh only its dedicated view overrides and isolation state.'
}
if ($source -notmatch 'FilterResultWorkbookWriter\.Write\s*\(') {
    throw 'Filter actions must export detached results through the Core workbook writer.'
}
if ($source -match 'ActiveView\.SetElementOverrides\s*\(') {
    throw 'Filter must never write per-element overrides to the source active view.'
}

if (-not $AssemblyPath) {
    $AssemblyPath = Join-Path $repoRoot "artifacts\addin\$RevitVersion\TUNG_DIROOT.Revit$RevitVersion.dll"
}
if (-not (Test-Path -LiteralPath $AssemblyPath -PathType Leaf)) { throw "Missing add-in assembly: $AssemblyPath" }
$apiDirectory = & (Join-Path $repoRoot 'build\Resolve-RevitApi.ps1') -RevitVersion $RevitVersion -ApiPathsFile $ApiPathsFile
$outputDirectory = Split-Path -Parent $AssemblyPath
$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $simpleName = ([Reflection.AssemblyName]::new($eventArgs.Name)).Name + '.dll'
    foreach ($directory in @($apiDirectory, $outputDirectory)) {
        $candidate = Join-Path $directory $simpleName
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return [Reflection.Assembly]::ReflectionOnlyLoadFrom($candidate) }
    }
    return $null
}
[AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve($resolver)
try {
    $assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom((Resolve-Path -LiteralPath $AssemblyPath).Path)
    foreach ($name in @(
        'TungDiroot.Addin.Filter.RevitFilterCatalogReader',
        'TungDiroot.Addin.Filter.RevitFilterSnapshotBatch',
        'TungDiroot.Addin.Filter.RevitFilterSnapshotProjector',
        'TungDiroot.Addin.Filter.RevitFilterActions',
        'TungDiroot.Addin.Filter.RevitFilterVisualization')) {
        if (-not $assembly.GetType($name, $false)) { throw "Filter integration type missing: $name" }
    }
}
finally {
    [AppDomain]::CurrentDomain.remove_ReflectionOnlyAssemblyResolve($resolver)
}

Write-Host "Filter add-in contract passed for Revit $RevitVersion."
