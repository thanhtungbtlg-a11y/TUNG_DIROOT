[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet(2024, 2025, 2026)]
    [int]$RevitVersion,

    [string]$ApiPathsFile
)

$ErrorActionPreference = 'Stop'

function Test-RevitApiDirectory([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { return $false }
    return (Test-Path -LiteralPath (Join-Path $Path 'RevitAPI.dll') -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $Path 'RevitAPIUI.dll') -PathType Leaf)
}

$candidates = [System.Collections.Generic.List[string]]::new()
$environmentName = "REVIT${RevitVersion}_DIR"
$environmentPath = [Environment]::GetEnvironmentVariable($environmentName)
if ($environmentPath) { $candidates.Add($environmentPath) }

if ($ApiPathsFile -and (Test-Path -LiteralPath $ApiPathsFile -PathType Leaf)) {
    $configured = Get-Content -LiteralPath $ApiPathsFile -Raw | ConvertFrom-Json
    $property = $configured.PSObject.Properties[[string]$RevitVersion]
    if ($property -and $property.Value) { $candidates.Add([string]$property.Value) }
}

$candidates.Add((Join-Path $env:ProgramFiles "Autodesk\Revit $RevitVersion"))

foreach ($candidate in $candidates) {
    if (Test-RevitApiDirectory $candidate) {
        return (Resolve-Path -LiteralPath $candidate).Path
    }
}

throw "Could not resolve Revit $RevitVersion API. Set $environmentName, install Revit $RevitVersion, or provide -ApiPathsFile with both RevitAPI.dll and RevitAPIUI.dll."
