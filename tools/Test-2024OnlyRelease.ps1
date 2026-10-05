[CmdletBinding()]
param(
    [string]$IsccPath = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$runRoot = Join-Path $artifactsRoot ('2024-only-contract-' + [Guid]::NewGuid().ToString('N'))
$stageRoot = Join-Path $runRoot 'stage'
$installSandbox = Join-Path $runRoot 'install'
$innoOutput = Join-Path $runRoot 'inno'

try {
    $broadStageRefused = $false
    try { & (Join-Path $repoRoot 'build\Stage-Release.ps1') -Version '1.3.0' -StageRoot $repoRoot }
    catch { $broadStageRefused = $_.Exception.Message -like 'StageRoot must not*' }
    if (-not $broadStageRefused) { throw 'Staging did not reject the repository root as a destructive target.' }

    $staleRuntime = Join-Path $stageRoot '2024\stale\obsolete.txt'
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $staleRuntime) | Out-Null
    [IO.File]::WriteAllText($staleRuntime, 'must be removed', [Text.UTF8Encoding]::new($false))
    & (Join-Path $repoRoot 'build\Stage-Release.ps1') -Version '1.3.0' -StageRoot $stageRoot
    $metadata = Get-Content -LiteralPath (Join-Path $stageRoot 'release.json') -Raw | ConvertFrom-Json
    $versions = @($metadata.revitVersions)
    if ($versions.Count -ne 1 -or [int]$versions[0] -ne 2024) {
        throw "Default staging must declare only Revit 2024; got: $($versions -join ', ')."
    }
    foreach ($unsupported in 2025, 2026) {
        if (Test-Path -LiteralPath (Join-Path $stageRoot ([string]$unsupported))) {
            throw "Default staging unexpectedly contains Revit $unsupported."
        }
    }

    & (Join-Path $repoRoot 'tools\Test-ReleaseLayout.ps1') -StageRoot $stageRoot -ExpectedVersion '1.3.0'
    & (Join-Path $repoRoot 'tools\Test-PortableInstall.ps1') -StageRoot $stageRoot -SandboxRoot $installSandbox

    if (-not (Test-Path -LiteralPath $IsccPath -PathType Leaf)) { throw "Inno Setup compiler not found: $IsccPath" }
    & $IsccPath "/DStageRoot=$stageRoot" "/DBuildOutputDir=$innoOutput" (Join-Path $repoRoot 'installer\TUNG_DIROOT.iss') | Out-Host
    if ($LASTEXITCODE -ne 0) { throw 'Inno Setup 2024-only contract compilation failed.' }
    $expectedSetup = Join-Path $innoOutput 'TUNG_DIROOT-Setup-Revit2024.exe'
    if (-not (Test-Path -LiteralPath $expectedSetup -PathType Leaf)) { throw 'The Revit 2024 setup filename was not produced.' }
    if (Get-ChildItem -LiteralPath $innoOutput -File | Where-Object Name -Match '2024-2026|2025|2026') {
        throw 'The 2024-only Inno output contains an unsupported-version artifact.'
    }

    Write-Host 'Revit 2024-only release contract passed.'
}
finally {
    if (Test-Path -LiteralPath $runRoot -PathType Container) {
        $resolved = [IO.Path]::GetFullPath($runRoot)
        if (-not $resolved.StartsWith($artifactsRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Refusing to clean a contract directory outside artifacts: $resolved"
        }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
