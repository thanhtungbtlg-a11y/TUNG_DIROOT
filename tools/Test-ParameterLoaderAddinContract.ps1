[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet(2024)]
    [int]$RevitVersion,
    [string]$AssemblyPath,
    [string]$ApiPathsFile
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
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
        'TungDiroot.Addin.Parameters.RevitBindingSnapshotReader',
        'TungDiroot.Addin.Parameters.RevitCategoryCatalog',
        'TungDiroot.Addin.Parameters.RevitParameterGroupCatalog',
        'TungDiroot.Addin.Parameters.SharedDefinitionResolver',
        'TungDiroot.Addin.Parameters.RevitParameterAssignmentExecutor')) {
        if (-not $assembly.GetType($name, $false)) { throw "Parameter Loader integration type missing: $name" }
    }
    $appType = $assembly.GetType('TungDiroot.Addin.Application', $true)
    $flags = [Reflection.BindingFlags]'Public,Static'
    $tableButton = [string]$appType.GetField('TableGenButtonId', $flags).GetRawConstantValue()
    $parameterButton = [string]$appType.GetField('ParameterLoaderButtonId', $flags).GetRawConstantValue()
    if ([string]::IsNullOrWhiteSpace($tableButton) -or [string]::IsNullOrWhiteSpace($parameterButton) -or $tableButton -eq $parameterButton) {
        throw 'TableGen and Parameter Loader must have distinct ribbon item ids.'
    }
}
finally {
    [AppDomain]::CurrentDomain.remove_ReflectionOnlyAssemblyResolve($resolver)
}

$runtimeAssembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $AssemblyPath).Path)
$resourceName = $runtimeAssembly.GetManifestResourceNames() | Where-Object { $_ -like '*.g.resources' } | Select-Object -First 1
if (-not $resourceName) { throw 'WPF resource manifest is missing.' }
$stream = $runtimeAssembly.GetManifestResourceStream($resourceName)
$reader = [Resources.ResourceReader]::new($stream)
try { $keys = @($reader | ForEach-Object { [string]$_.Key }) }
finally { $reader.Dispose(); $stream.Dispose() }
foreach ($required in 'resources/parameterloader16.png', 'resources/parameterloader32.png', 'ui/parameterloaderwindow.baml') {
    if ($keys -notcontains $required) { throw "Parameter Loader embedded resource missing: $required" }
}

Write-Host "Parameter Loader add-in contract passed for Revit $RevitVersion."
