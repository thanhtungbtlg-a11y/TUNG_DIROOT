[CmdletBinding()]
param(
    [ValidateSet(2024, 2025, 2026)]
    [int]$RevitVersion = 2024,
    [string]$ApiPathsFile
)

$ErrorActionPreference = 'Stop'
$env:TUNG_DIROOT_DOTNET = if ($env:TUNG_DIROOT_DOTNET) { $env:TUNG_DIROOT_DOTNET } else { 'dotnet' }
& (Join-Path $PSScriptRoot 'Test-Core.ps1')
& (Join-Path (Split-Path -Parent $PSScriptRoot) 'build\Build-Addin.ps1') -RevitVersion $RevitVersion -Configuration Release -ApiPathsFile $ApiPathsFile
& (Join-Path $PSScriptRoot 'Test-AddinContract.ps1') -RevitVersion $RevitVersion -ApiPathsFile $ApiPathsFile
Write-Host "Plan 2 automated verification passed for Revit $RevitVersion. Interactive smoke checklist remains manual."
