[CmdletBinding()]
param(
    [ValidateSet(2024)][int]$RevitVersion = 2024,
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
[void][Reflection.Assembly]::LoadFrom((Join-Path $apiDirectory 'RevitAPI.dll'))

$payloadDirectory = Split-Path -Parent $AssemblyPath
Get-ChildItem -LiteralPath $payloadDirectory -Filter '*.dll' -File |
    Where-Object { $_.FullName -ne $AssemblyPath -and $_.Name -notlike 'RevitAPI*.dll' } |
    ForEach-Object {
        try { [void][Reflection.Assembly]::LoadFrom($_.FullName) }
        catch { }
    }

$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $AssemblyPath).Path)
$coreAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $payloadDirectory 'TungDiroot.Core.dll'))
$schemaType = $assembly.GetType('TungDiroot.Addin.Links.RevitLinkSchema', $true)
$flags = [Reflection.BindingFlags]'Static,Public,NonPublic'
$registry = $schemaType.GetProperty('Registry', $flags).GetValue($null)
$viewMarker = $schemaType.GetProperty('ViewMarker', $flags).GetValue($null)

$taggerType = $assembly.GetType('TungDiroot.Addin.Revit.ElementLinkTagger', $true)
$elementLink = $taggerType.GetMethod('GetOrCreateSchema', [Reflection.BindingFlags]'Static,NonPublic').Invoke($null, @())

$actual = @($registry.SchemaName, $viewMarker.SchemaName, $elementLink.SchemaName)
$expected = @('TUNG_DIROOT_LinkRegistry', 'TUNG_DIROOT_ViewLinkMarker', 'TUNG_DIROOT_ElementLink')
if ([string]::Join('|', $actual) -ne [string]::Join('|', $expected)) {
    throw "Unexpected Extensible Storage schema names: $($actual -join ', ')"
}

$linkRecord = $coreAssembly.GetType('TungDiroot.Core.Links.LinkRecord', $true)
$request = $coreAssembly.GetType('TungDiroot.Core.Import.WorkbookImportRequest', $true)
if ($null -eq $linkRecord.GetProperty('StyleProfileName') -or $null -eq $request.GetProperty('StyleProfileName')) {
    throw 'TableGen company profile identity must round-trip through import requests and JSON link records.'
}

$profileResolver = $assembly.GetType('TungDiroot.Addin.Revit.TableGenCompanyProfileResolver', $true)
$resolve = $profileResolver.GetMethod('Resolve', [Reflection.BindingFlags]'Static,Public,NonPublic')
$fallback = $resolve.Invoke($null, @('__missing-profile-contract__'))
$fallbackName = $fallback.GetType().GetProperty('Name').GetValue($fallback)
$fallbackNumbers = $fallback.GetType().GetProperty('Numbers').GetValue($fallback)
if ($null -ne $fallbackName -or -not $fallbackNumbers.KeepExcelFormat) {
    throw 'A missing TableGen company profile must fall back to the Excel-default formatting profile.'
}

Write-Host "Revit $RevitVersion Extensible Storage schema contract passed."
