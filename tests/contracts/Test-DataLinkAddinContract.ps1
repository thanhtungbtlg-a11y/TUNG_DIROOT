[CmdletBinding()]
param(
    [ValidateSet(2024)]
    [int]$RevitVersion = 2024,
    [string]$AssemblyPath,
    [string]$ApiPathsFile
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$sourceRoot = Join-Path $repoRoot 'src\TungDiroot.Addin\DataLink'
$addinRoot = Join-Path $repoRoot 'src\TungDiroot.Addin'
$requiredSources = @(
    'RevitDataLinkSnapshotReader.cs',
    'RevitDataLinkCatalogReader.cs',
    'RevitDataLinkCurrentValueReader.cs',
    'RevitDataLinkAssignmentExecutor.cs',
    'RevitParameterValueCodec.cs'
)
foreach ($name in $requiredSources) {
    $path = Join-Path $sourceRoot $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "DataLink adapter source missing: $name" }
}

$commandPath = Join-Path $addinRoot 'Command_DataLink.cs'
$windowPath = Join-Path $addinRoot 'UI\DataLinkWindow.xaml.cs'
$xamlPath = Join-Path $addinRoot 'UI\DataLinkWindow.xaml'
$applicationPath = Join-Path $addinRoot 'Application.cs'
foreach ($path in @($commandPath, $windowPath, $xamlPath, $applicationPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "DataLink workflow source missing: $path" }
}
foreach ($name in @('DataLink16.png', 'DataLink32.png')) {
    if (-not (Test-Path -LiteralPath (Join-Path $addinRoot "Resources\$name") -PathType Leaf)) {
        throw "DataLink ribbon icon missing: $name"
    }
}

$applicationSource = Get-Content -LiteralPath $applicationPath -Raw
$commandSource = Get-Content -LiteralPath $commandPath -Raw
$windowSource = Get-Content -LiteralPath $windowPath -Raw
$xamlSource = Get-Content -LiteralPath $xamlPath -Raw
if ($applicationSource -notmatch 'DataLinkButtonId\s*=\s*"TUNG_DIROOT\.DataLink"') {
    throw 'DataLink must have the separate TUNG_DIROOT.DataLink ribbon button.'
}
if ($applicationSource -notmatch 'typeof\(Command_DataLink\)' -or
    $applicationSource -notmatch 'DataLink16\.png' -or $applicationSource -notmatch 'DataLink32\.png') {
    throw 'DataLink ribbon registration must reference its command and both icons.'
}
if ($commandSource -notmatch 'class\s+Command_DataLink\s*:\s*IExternalCommand' -or
    $commandSource -notmatch 'DataLinkWindow\.ShowOrActivate') {
    throw 'Command_DataLink must open or activate the modeless DataLink window.'
}
if ($windowSource -notmatch 'static\s+DataLinkWindow\?\s+_instance' -or
    $windowSource -notmatch 'RevitActionExternalEvent' -or
    $windowSource -notmatch 'EnsureActiveDocument\s*\(application\)') {
    throw 'DataLink window must be a modeless singleton and guard document identity inside external callbacks.'
}
if ($windowSource -match 'TableGenWindow\s*\.\s*Close') {
    throw 'Closing DataLink must never close TableGen.'
}
if ($xamlSource -notmatch 'MinWidth="([0-9.]+)"' -or [double]$Matches[1] -gt 900) {
    throw 'DataLink MinWidth must be at most 900.'
}
if ($xamlSource -notmatch 'MinHeight="([0-9.]+)"' -or [double]$Matches[1] -gt 600) {
    throw 'DataLink MinHeight must be at most 600.'
}
if ($xamlSource -notmatch '<ScrollViewer' -or $xamlSource -notmatch '<TabControl') {
    throw 'DataLink must provide scrollable three-step content.'
}

$source = ($requiredSources | ForEach-Object { Get-Content -LiteralPath (Join-Path $sourceRoot $_) -Raw }) -join "`n"
if ($source -notmatch 'new\s+TransactionGroup\s*\([^,]+,\s*"TUNG_DIROOT - DataLink"\s*\)') {
    throw 'DataLink assignments must use the named TransactionGroup.'
}
if ($source -notmatch '\.IsLinked') { throw 'DataLink must explicitly reject linked documents.' }
if ($source -match '\.Save\s*\(|SynchronizeWithCentral\s*\(') {
    throw 'DataLink must not save or synchronize the Revit document.'
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
        'TungDiroot.Addin.Command_DataLink',
        'TungDiroot.Addin.DataLink.RevitDataLinkCatalogReader',
        'TungDiroot.Addin.DataLink.RevitDataLinkCurrentValueReader',
        'TungDiroot.Addin.DataLink.RevitDataLinkSnapshotReader',
        'TungDiroot.Addin.DataLink.RevitDataLinkAssignmentExecutor',
        'TungDiroot.Addin.DataLink.RevitParameterValueCodec')) {
        if (-not $assembly.GetType($name, $false)) { throw "DataLink integration type missing: $name" }
    }
}
finally {
    [AppDomain]::CurrentDomain.remove_ReflectionOnlyAssemblyResolve($resolver)
}

Write-Host "DataLink add-in contract passed for Revit $RevitVersion."
