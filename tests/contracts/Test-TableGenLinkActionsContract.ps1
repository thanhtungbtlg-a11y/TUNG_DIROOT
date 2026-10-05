[CmdletBinding()]
param(
    [ValidateSet(2024)][int]$RevitVersion = 2024,
    [string]$AssemblyPath,
    [string]$ApiPathsFile
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
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
    $executor = $assembly.GetType('TungDiroot.Addin.Links.RevitLinkManagementExecutor', $true)
    $methodNames = @($executor.GetMethods() | ForEach-Object Name)
    foreach ($method in @('OpenView', 'OpenSource', 'OpenSourceFolder', 'UnlinkKeepView', 'DeleteLinkAndView', 'ReadTargetViews', 'ChangeTargetView', 'CreateDuplicateRequest', 'RecreateLink')) {
        if ($methodNames -notcontains $method) { throw "Missing TableGen link action: $method" }
    }
    $recreate = $executor.GetMethod('RecreateLink');
    if ($recreate.ReturnType.FullName -ne 'TungDiroot.Addin.Batch.BatchItemResult') { throw 'RecreateLink must report its per-link result.' }
    $processor = $assembly.GetType('TungDiroot.Addin.Batch.TableGenUpdateProcessor', $true)
    $forcedOverload = @($processor.GetMethods() | Where-Object {
        $_.Name -eq 'ProcessOne' -and $_.GetParameters().Count -eq 3 -and $_.GetParameters()[2].ParameterType.FullName -eq 'System.Boolean'
    })
    if ($forcedOverload.Count -ne 1) { throw 'Missing explicit forceRecreate update overload.' }
}
finally {
    [AppDomain]::CurrentDomain.remove_ReflectionOnlyAssemblyResolve($resolver)
}
Write-Host "TableGen link action contract passed for Revit $RevitVersion."
