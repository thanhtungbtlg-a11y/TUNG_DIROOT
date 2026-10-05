[CmdletBinding()]
param([string]$ApiPathsFile)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sandboxRoot = Join-Path $repoRoot ("artifacts\build-clean-contract-" + [Guid]::NewGuid().ToString('N'))
$output = Join-Path $sandboxRoot '2024'

try {
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $output 'obsolete-from-previous-build.dll') -Value 'stale build output'

    & (Join-Path $repoRoot 'build\Build-Addin.ps1') `
        -RevitVersion 2024 -Configuration Release -ApiPathsFile $ApiPathsFile -OutputDirectory $output

    if (Test-Path -LiteralPath (Join-Path $output 'obsolete-from-previous-build.dll')) {
        throw 'Build-Addin did not remove a stale dependency from its output directory.'
    }

    if (-not (Test-Path -LiteralPath (Join-Path $output 'TUNG_DIROOT.Revit2024.dll') -PathType Leaf)) {
        throw 'Clean build output is missing the Revit add-in assembly.'
    }
}
finally {
    if (Test-Path -LiteralPath $sandboxRoot) {
        Remove-Item -LiteralPath $sandboxRoot -Recurse -Force
    }
}

Write-Host 'Clean add-in build output contract passed.'
