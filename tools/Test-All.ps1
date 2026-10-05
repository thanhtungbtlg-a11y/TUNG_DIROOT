[CmdletBinding()]
param(
    [switch]$SkipRevitBuild,
    [switch]$DocsOnly,
    [string]$ApiPathsFile
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$dotnet = if ($env:TUNG_DIROOT_DOTNET) { $env:TUNG_DIROOT_DOTNET } else { 'dotnet' }
$requiredDocs = @('README.md', 'docs\user-guide-vi.md', 'docs\user-guide-datalink.md', 'docs\user-guide-filter.md', 'docs\user-guide-sheet-tools.md', 'docs\user-guide-parameter-tools.md', 'docs\user-guide-family-reviser.md', 'docs\testing\filter-revit-2024-checklist.md', 'docs\testing\tablegen-advanced-revit-2024-checklist.md', 'docs\testing\document-link-revit-2024-checklist.md', 'docs\testing\bulk-vg-revit-2024-checklist.md', 'docs\testing\sheet-tools-revit-2024-checklist.md', 'docs\testing\reordering-revit-2024-checklist.md', 'docs\testing\paramanager-revit-2024-checklist.md', 'docs\testing\family-reviser-revit-2024-checklist.md', 'docs\install-guide-vi.md', 'docs\troubleshooting-vi.md', 'docs\architecture.md', 'docs\third-party-licenses.md')
foreach ($relative in $requiredDocs) {
    $path = Join-Path $repoRoot $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing documentation: $relative" }
    if ((Get-Item -LiteralPath $path).Length -lt 500) { throw "Documentation too short: $relative" }
    $directory = Split-Path -Parent $path
    $content = Get-Content -LiteralPath $path -Raw
    foreach ($match in [regex]::Matches($content, '\[[^\]]+\]\(([^)]+)\)')) {
        $target = $match.Groups[1].Value.Split('#')[0]
        if (-not $target -or $target -match '^(https?|mailto):') { continue }
        $resolved = Join-Path $directory $target.Replace('/', '\')
        if (-not (Test-Path -LiteralPath $resolved)) { throw "Broken documentation link in $relative`: $target" }
    }
}
if ($DocsOnly) { Write-Host 'Documentation contract passed.'; return }

& (Join-Path $repoRoot 'samples\Create-TUNG_DIROOT-TestWorkbook.ps1') -OutputPath (Join-Path $repoRoot 'samples\TUNG_DIROOT-Test.xlsx')
& (Join-Path $repoRoot 'samples\Create-TUNG_DIROOT-ParameterLoader.ps1') -OutputPath (Join-Path $repoRoot 'samples\TUNG_DIROOT-ParameterLoader.xlsx')
$sample = Join-Path $repoRoot 'samples\TUNG_DIROOT-Test.xlsx'
$catalogJson = & $dotnet run --project (Join-Path $repoRoot 'tools\TungDiroot.CatalogProbe\TungDiroot.CatalogProbe.csproj') -c Release -- $sample
if ($LASTEXITCODE -ne 0) { throw 'Catalog probe failed.' }
$catalog = $catalogJson | ConvertFrom-Json
if (-not $catalog.sampleContractValid) { throw 'The generated sample workbook contract is incomplete.' }

& (Join-Path $PSScriptRoot 'Test-Core.ps1')
& (Join-Path $PSScriptRoot 'Test-ResponsiveWindowLayout.ps1')
$networkTypes = & rg -n 'HttpClient|WebRequest|TcpClient|Socket' (Join-Path $repoRoot 'src')
if ($LASTEXITCODE -eq 0) { throw "Network/telemetry API is forbidden:`n$networkTypes" }
if ($LASTEXITCODE -gt 1) { throw 'Network type source audit could not run.' }
$urlMatches = @(& rg -n 'https?://' (Join-Path $repoRoot 'src')) | Where-Object { $_ -notmatch 'schemas\.microsoft\.com|schemas\.openxmlformats\.org' }
if ($LASTEXITCODE -gt 1) { throw 'URL source audit could not run.' }
if ($urlMatches.Count -gt 0) { throw "Unexpected network URL in source:`n$urlMatches" }

if (-not $SkipRevitBuild) {
    & (Join-Path $PSScriptRoot 'Test-CleanAddinBuildOutput.ps1') -ApiPathsFile $ApiPathsFile
    & (Join-Path $repoRoot 'build\Build-Matrix.ps1') -Configuration Release -ApiPathsFile $ApiPathsFile
    & (Join-Path $PSScriptRoot 'Test-RevitRuntimeDependencyCompatibility.ps1') -RevitVersion 2024 -ApiPathsFile $ApiPathsFile
    & (Join-Path $PSScriptRoot 'Test-ParameterLoaderAddinContract.ps1') -RevitVersion 2024 -ApiPathsFile $ApiPathsFile
    & (Join-Path $repoRoot 'tests\contracts\Test-DataLinkAddinContract.ps1') -RevitVersion 2024 -ApiPathsFile $ApiPathsFile
    & (Join-Path $repoRoot 'tests\contracts\Test-FilterAddinContract.ps1') -RevitVersion 2024 -ApiPathsFile $ApiPathsFile
    & (Join-Path $repoRoot 'tests\contracts\Test-TableGenLinkActionsContract.ps1') -RevitVersion 2024 -ApiPathsFile $ApiPathsFile
    & (Join-Path $repoRoot 'tests\contracts\Test-DocumentLinkAddinContract.ps1') -RevitVersion 2024 -ApiPathsFile $ApiPathsFile
    & (Join-Path $repoRoot 'tests\contracts\Test-BulkVgAddinContract.ps1') -RevitVersion 2024 -ApiPathsFile $ApiPathsFile
    & (Join-Path $repoRoot 'tests\contracts\Test-SheetToolsAddinContract.ps1') -RevitVersion 2024 -ApiPathsFile $ApiPathsFile
    & (Join-Path $repoRoot 'tests\contracts\Test-ReOrderingAddinContract.ps1') -RevitVersion 2024 -ApiPathsFile $ApiPathsFile
    & (Join-Path $repoRoot 'tests\contracts\Test-ParaManagerAddinContract.ps1') -RevitVersion 2024 -ApiPathsFile $ApiPathsFile
    & (Join-Path $repoRoot 'tests\contracts\Test-FamilyReviserAddinContract.ps1') -RevitVersion 2024 -ApiPathsFile $ApiPathsFile
    & (Join-Path $PSScriptRoot 'Test-RevitSchemaContract.ps1') -ApiPathsFile $ApiPathsFile
    & (Join-Path $repoRoot 'build\Stage-Release.ps1') -Version 1.3.0
    & (Join-Path $PSScriptRoot 'Test-ReleaseLayout.ps1') -StageRoot (Join-Path $repoRoot 'artifacts\staging\TUNG_DIROOT')
    & (Join-Path $PSScriptRoot 'Test-PortableInstall.ps1') -StageRoot (Join-Path $repoRoot 'artifacts\staging\TUNG_DIROOT') -SandboxRoot (Join-Path $repoRoot 'artifacts\install-sandbox')
    & (Join-Path $PSScriptRoot 'Test-InstallBackup.ps1') -StageRoot (Join-Path $repoRoot 'artifacts\staging\TUNG_DIROOT')
    & (Join-Path $PSScriptRoot 'Test-2024OnlyRelease.ps1')
}
Write-Host 'TUNG_DIROOT automated test suite passed.'
