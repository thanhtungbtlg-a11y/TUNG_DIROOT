[CmdletBinding()]
param(
    [string]$Version = '1.3.0',
    [string]$ApiPathsFile,
    [string]$IsccPath = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe',
    [switch]$VerifyOnly,
    [switch]$ReleaseCandidate
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if ($Version -ne '1.3.0') { throw "This release definition is locked to 1.3.0; requested $Version." }

$unexpectedDirty = @(& git -C $repoRoot status --porcelain) | Where-Object { $_ }
if ($LASTEXITCODE -ne 0) { throw 'Could not inspect git status.' }
if ($unexpectedDirty.Count -gt 0) { throw "Release requires a clean working tree:`n$unexpectedDirty" }

& (Join-Path $repoRoot 'tools\Test-All.ps1') -ApiPathsFile $ApiPathsFile
if ($LASTEXITCODE -ne 0) { throw 'Automated release gate failed.' }

$matrixPath = Join-Path $repoRoot 'docs\testing\release-matrix.md'
$matrix = Get-Content -LiteralPath $matrixPath -Raw
$line = ($matrix -split "`r?`n") | Where-Object { $_ -match '^\| 2024 \|' } | Select-Object -First 1
if (-not $ReleaseCandidate -and (-not $line -or $line -notmatch '^\| 2024 \| PASS[^|]*\| PASS[^|]*\|')) {
    throw 'Release matrix is not fully PASS for Revit 2024. Use -ReleaseCandidate only for a clearly identified pre-checklist artifact.'
}
if ($VerifyOnly) {
    $kind = if ($ReleaseCandidate) { 'release-candidate' } else { 'release' }
    Write-Host "$kind $Version verification passed; no artifacts created."
    return
}
if (-not (Test-Path -LiteralPath $IsccPath -PathType Leaf)) { throw "Inno Setup compiler not found: $IsccPath" }

& (Join-Path $PSScriptRoot 'Stage-Release.ps1') -Version $Version
$dist = Join-Path $repoRoot 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null
foreach ($name in @(
    'TUNG_DIROOT-Setup-Revit2024.exe',
    'TUNG_DIROOT-Portable-Revit2024.zip',
    'TUNG_DIROOT-Setup-Revit2024-2026.exe',
    'TUNG_DIROOT-Portable-Revit2024-2026.zip',
    'SHA256SUMS.txt')) {
    $target = Join-Path $dist $name
    if (Test-Path -LiteralPath $target -PathType Leaf) { Remove-Item -LiteralPath $target -Force }
}
& $IsccPath (Join-Path $repoRoot 'installer\TUNG_DIROOT.iss')
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }

$portable = Join-Path $repoRoot 'artifacts\portable'
if (Test-Path -LiteralPath $portable) { Remove-Item -LiteralPath $portable -Recurse -Force }
New-Item -ItemType Directory -Force -Path $portable | Out-Null
Copy-Item -LiteralPath (Join-Path $repoRoot 'installer\Install-TUNG_DIROOT.ps1') -Destination $portable
Copy-Item -LiteralPath (Join-Path $repoRoot 'installer\Install-TUNG_DIROOT.cmd') -Destination $portable
Copy-Item -LiteralPath (Join-Path $repoRoot 'installer\Uninstall-TUNG_DIROOT.ps1') -Destination $portable
Copy-Item -LiteralPath (Join-Path $repoRoot 'installer\Uninstall-TUNG_DIROOT.cmd') -Destination $portable
Copy-Item -LiteralPath (Join-Path $repoRoot 'installer\New-RevitManifest.ps1') -Destination $portable
Copy-Item -LiteralPath (Join-Path $repoRoot 'installer\Backup-TUNG_DIROOT.ps1') -Destination $portable
Copy-Item -LiteralPath (Join-Path $repoRoot 'installer\Restore-TUNG_DIROOT.ps1') -Destination $portable
Copy-Item -LiteralPath (Join-Path $repoRoot 'installer\THIRD-PARTY-NOTICES.txt') -Destination $portable
Copy-Item -LiteralPath (Join-Path $repoRoot 'installer\TUNG_DIROOT.install-marker') -Destination $portable
Copy-Item -LiteralPath (Join-Path $repoRoot 'README.md') -Destination $portable
Copy-Item -LiteralPath (Join-Path $repoRoot 'artifacts\staging\TUNG_DIROOT') -Destination (Join-Path $portable 'TUNG_DIROOT') -Recurse

$zip = Join-Path $dist 'TUNG_DIROOT-Portable-Revit2024.zip'
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $portable '*') -DestinationPath $zip -CompressionLevel Optimal
$setup = Join-Path $dist 'TUNG_DIROOT-Setup-Revit2024.exe'
if (-not (Test-Path -LiteralPath $setup -PathType Leaf)) { throw 'Setup artifact was not created.' }
if (-not (Test-Path -LiteralPath $zip -PathType Leaf)) { throw 'Portable artifact was not created.' }

$hashLines = foreach ($file in @($setup, $zip)) {
    "{0}  {1}" -f (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash, [IO.Path]::GetFileName($file)
}
$hashLines | Set-Content -LiteralPath (Join-Path $dist 'SHA256SUMS.txt') -Encoding ASCII
$kind = if ($ReleaseCandidate) { 'Release-candidate artifacts' } else { 'Release artifacts' }
Write-Host "$kind created at $dist"
