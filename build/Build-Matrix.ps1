[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [string]$ApiPathsFile,
    [ValidateSet(2024)]
    [int[]]$RevitVersions = @(2024)
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
foreach ($year in $RevitVersions) {
    & (Join-Path $PSScriptRoot 'Build-Addin.ps1') -RevitVersion $year -Configuration $Configuration -ApiPathsFile $ApiPathsFile
    & (Join-Path $repoRoot 'tools\Test-AddinContract.ps1') -RevitVersion $year -ApiPathsFile $ApiPathsFile
}
Write-Host "Build matrix passed: $($RevitVersions -join ', ')."
