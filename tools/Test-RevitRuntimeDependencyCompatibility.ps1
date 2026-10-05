[CmdletBinding()]
param(
    [ValidateSet(2024)][int]$RevitVersion = 2024,
    [string]$ArtifactDirectory,
    [string]$ApiPathsFile
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $ArtifactDirectory) {
    $ArtifactDirectory = Join-Path $repoRoot "artifacts\addin\$RevitVersion"
}

$revitDirectory = & (Join-Path $repoRoot 'build\Resolve-RevitApi.ps1') `
    -RevitVersion $RevitVersion -ApiPathsFile $ApiPathsFile
$conflicts = @()
foreach ($packagedDependency in Get-ChildItem -LiteralPath $ArtifactDirectory -Filter '*.dll' -File) {
    $runtimeDependency = Join-Path $revitDirectory $packagedDependency.Name
    if (-not (Test-Path -LiteralPath $runtimeDependency -PathType Leaf)) { continue }

    try {
        $runtimeVersion = [Reflection.AssemblyName]::GetAssemblyName($runtimeDependency).Version
        $packagedVersion = [Reflection.AssemblyName]::GetAssemblyName($packagedDependency.FullName).Version
    }
    catch {
        continue
    }

    if ($packagedVersion -ne $runtimeVersion) {
        $conflicts += "$($packagedDependency.Name): packaged $packagedVersion, Revit runtime $runtimeVersion"
    }
}

if ($conflicts.Count -gt 0) {
    throw "Packaged dependencies conflict with modules preloaded by Revit ${RevitVersion}:`n$($conflicts -join "`n")"
}

Write-Host "Revit $RevitVersion runtime dependency compatibility passed."
