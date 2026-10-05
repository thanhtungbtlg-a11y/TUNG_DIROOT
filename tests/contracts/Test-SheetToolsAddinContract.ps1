[CmdletBinding()]
param(
    [ValidateSet(2024)][int]$RevitVersion = 2024,
    [string]$AssemblyPath,
    [string]$ApiPathsFile
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $AssemblyPath) { $AssemblyPath = Join-Path $repoRoot "artifacts\addin\$RevitVersion\TUNG_DIROOT.Revit$RevitVersion.dll" }
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
    $executor = $assembly.GetType('TungDiroot.Addin.Sheets.RevitSheetToolExecutor', $true)
    $methods = @($executor.GetMethods() | ForEach-Object Name)
    foreach ($method in @('ReadWorkbookSnapshot', 'ReadReferenceCatalog', 'Apply')) {
        if ($methods -notcontains $method) { throw "Missing Sheet Tools action: $method" }
    }
    if ($null -eq $assembly.GetType('TungDiroot.Addin.Command_SheetTools', $true)) { throw 'Sheet Tools ribbon command is missing.' }
}
finally { [AppDomain]::CurrentDomain.remove_ReflectionOnlyAssemblyResolve($resolver) }
Write-Host "SheetLink / SheetGen add-in contract passed for Revit $RevitVersion."
