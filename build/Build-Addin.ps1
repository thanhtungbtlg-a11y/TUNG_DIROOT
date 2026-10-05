[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet(2024, 2025, 2026)]
    [int]$RevitVersion,

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$ApiPathsFile,

    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$apiDirectory = & (Join-Path $PSScriptRoot 'Resolve-RevitApi.ps1') -RevitVersion $RevitVersion -ApiPathsFile $ApiPathsFile
$dotnet = if ($env:TUNG_DIROOT_DOTNET) { $env:TUNG_DIROOT_DOTNET } else { 'dotnet' }
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$output = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else {
    [IO.Path]::GetFullPath((Join-Path $repoRoot "artifacts\addin\$RevitVersion"))
}
$artifactsPrefix = $artifactsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $output.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Build output must remain under the repository artifacts directory: $output"
}
if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}

& $dotnet build (Join-Path $repoRoot 'src\TungDiroot.Addin\TungDiroot.Addin.csproj') `
    -c $Configuration `
    -p:RevitVersion=$RevitVersion `
    -p:RevitApiDir=$apiDirectory `
    -p:OutputPath=$output
if ($LASTEXITCODE -ne 0) { throw "TUNG_DIROOT add-in build failed for Revit $RevitVersion." }

$forbidden = Get-ChildItem -LiteralPath $output -Filter 'RevitAPI*.dll' -File -ErrorAction SilentlyContinue
if ($forbidden) { throw "Revit API assemblies must not be copied to add-in output: $($forbidden.Name -join ', ')" }

Write-Host "Built TUNG_DIROOT for Revit $RevitVersion at $output"
