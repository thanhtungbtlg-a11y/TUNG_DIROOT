[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet(2024, 2025, 2026)]
    [int]$RevitVersion,

    [string]$AssemblyPath,

    [string]$ApiPathsFile
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $AssemblyPath) {
    $AssemblyPath = Join-Path $repoRoot "artifacts\addin\$RevitVersion\TUNG_DIROOT.Revit$RevitVersion.dll"
}
if (-not (Test-Path -LiteralPath $AssemblyPath -PathType Leaf)) {
    throw "Add-in assembly does not exist: $AssemblyPath"
}

$apiDirectory = & (Join-Path $repoRoot 'build\Resolve-RevitApi.ps1') -RevitVersion $RevitVersion -ApiPathsFile $ApiPathsFile
$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $simpleName = ([Reflection.AssemblyName]::new($eventArgs.Name)).Name + '.dll'
    $candidate = Join-Path $apiDirectory $simpleName
    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        return [Reflection.Assembly]::ReflectionOnlyLoadFrom($candidate)
    }
    return $null
}
[AppDomain]::CurrentDomain.add_ReflectionOnlyAssemblyResolve($resolver)
try {
    $assembly = [Reflection.Assembly]::ReflectionOnlyLoadFrom((Resolve-Path -LiteralPath $AssemblyPath).Path)
    $app = $assembly.GetType('TungDiroot.Addin.Application', $true)
    $command = $assembly.GetType('TungDiroot.Addin.Command_TableGen', $true)
    $parameterCommand = $assembly.GetType('TungDiroot.Addin.Command_ParameterLoader', $true)
    $dataLinkCommand = $assembly.GetType('TungDiroot.Addin.Command_DataLink', $true)
    $filterCommand = $assembly.GetType('TungDiroot.Addin.Command_Filter', $true)
    if (-not $app.GetInterface('Autodesk.Revit.UI.IExternalApplication')) { throw 'Application contract missing.' }
    if (-not $command.GetInterface('Autodesk.Revit.UI.IExternalCommand')) { throw 'Command contract missing.' }
    if (-not $parameterCommand.GetInterface('Autodesk.Revit.UI.IExternalCommand')) { throw 'Parameter Loader command contract missing.' }
    if (-not $dataLinkCommand.GetInterface('Autodesk.Revit.UI.IExternalCommand')) { throw 'DataLink command contract missing.' }
    if (-not $filterCommand.GetInterface('Autodesk.Revit.UI.IExternalCommand')) { throw 'Filter command contract missing.' }
}
finally {
    [AppDomain]::CurrentDomain.remove_ReflectionOnlyAssemblyResolve($resolver)
}

$outputDirectory = Split-Path -Parent $AssemblyPath
if (Get-ChildItem -LiteralPath $outputDirectory -Filter 'RevitAPI*.dll' -File -ErrorAction SilentlyContinue) {
    throw 'Revit API assemblies must not be shipped with the add-in.'
}

Write-Host "Add-in contract passed for Revit $RevitVersion."
