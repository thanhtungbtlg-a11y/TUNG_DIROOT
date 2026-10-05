[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet(2024, 2025, 2026)][int]$RevitVersion,
    [Parameter(Mandatory = $true)][string]$ManifestPath
)

$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) { throw "Manifest does not exist: $ManifestPath" }
[xml]$manifest = Get-Content -LiteralPath $ManifestPath -Raw
$addin = $manifest.RevitAddIns.AddIn
if ($addin.Type -ne 'Application') { throw 'Manifest type must be Application.' }
if ($addin.Name -ne 'TUNG_DIROOT') { throw 'Wrong add-in name.' }
if ($addin.FullClassName -ne 'TungDiroot.Addin.Application') { throw 'Wrong application class.' }
if ($addin.AddInId -ne '8A93D06F-E03B-4A7C-9A96-DF5A3D5B0E24') { throw 'Wrong AddInId.' }
if ($addin.VendorId -ne 'TUNG') { throw 'Wrong VendorId.' }
if (-not [IO.Path]::IsPathRooted([string]$addin.Assembly)) { throw 'Assembly path must be absolute.' }
if (-not (Test-Path -LiteralPath ([string]$addin.Assembly) -PathType Leaf)) { throw 'Assembly path does not exist.' }
if ([IO.Path]::GetFileName([string]$addin.Assembly) -ne "TUNG_DIROOT.Revit$RevitVersion.dll") { throw 'Manifest targets the wrong Revit assembly.' }
Write-Host "Manifest contract passed for Revit $RevitVersion."
