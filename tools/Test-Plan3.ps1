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

$repoRoot = Split-Path -Parent $PSScriptRoot
$unsafeDelete = & rg -n 'Delete\(' (Join-Path $repoRoot 'src\TungDiroot.Addin\Updates')
if ($LASTEXITCODE -gt 1) { throw 'Delete scope audit could not run.' }
$forbiddenSync = & rg -n 'SaveAs|SynchronizeWithCentral|PostCommand' (Join-Path $repoRoot 'src\TungDiroot.Addin\Sync')
if ($LASTEXITCODE -eq 0) { throw "Forbidden automatic save/sync command found:`n$forbiddenSync" }
if ($LASTEXITCODE -gt 1) { throw 'Auto Sync source audit could not run.' }

Write-Host "Delete-scope audit locations:`n$unsafeDelete"
Write-Host "Plan 3 automated verification passed for Revit $RevitVersion. Interactive smoke checklist remains NOT RUN."
