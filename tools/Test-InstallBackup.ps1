[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$StageRoot)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$sandbox = Join-Path $repoRoot ('artifacts\backup-contract-' + [Guid]::NewGuid().ToString('N'))
$install = Join-Path $sandbox 'Program\old'
$manifests = Join-Path $sandbox 'Addins'
$backups = Join-Path $sandbox 'Backups'
$restore = Join-Path $sandbox 'Restored'
$malformedBackup = Join-Path $sandbox 'MalformedBackup'
New-Item -ItemType Directory -Path $malformedBackup -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $malformedBackup 'backup.json'), '{"format":"TUNG_DIROOT_INSTALL_BACKUP_V1","programs":null,"manifests":[],"files":null}')
$malformedRestore = Join-Path $sandbox 'MalformedRestore'
$refused = $false
try {
    & (Join-Path $repoRoot 'installer\Restore-TUNG_DIROOT.ps1') -BackupPath $malformedBackup -RestoreRoot $malformedRestore -ManifestRoot $manifests
} catch { $refused = $true }
if (-not $refused -or (Test-Path -LiteralPath $malformedRestore)) { throw 'Restore must reject null metadata collections before writing anything.' }
if (@(Get-Process -Name Revit -ErrorAction SilentlyContinue).Count -gt 0) {
    $guarded = $false
    $blockedBackup = Join-Path $sandbox 'RunningRevitBlocked'
    try {
        & (Join-Path $repoRoot 'installer\Backup-TUNG_DIROOT.ps1') -InstallRoot (Join-Path $sandbox 'RunningInstall') -BackupRoot $blockedBackup
    } catch { $guarded = $_.Exception.Message -like 'Close Revit before upgrading*' }
    if (-not $guarded -or (Test-Path -LiteralPath $blockedBackup)) { throw 'Running Revit guard must reject real-manifest upgrades before backup/install writes.' }
}
$unowned = Join-Path $sandbox 'Unowned'
New-Item -ItemType Directory -Path (Join-Path $unowned '2024') -Force | Out-Null
$unownedFile = Join-Path $unowned '2024\keep.txt'
[IO.File]::WriteAllText($unownedFile, 'user-data')
$refused = $false
try {
    & (Join-Path $repoRoot 'installer\Install-TUNG_DIROOT.ps1') -StageRoot $StageRoot -InstallRoot $unowned -ManifestRoot (Join-Path $sandbox 'UnownedAddins') -BackupRoot $backups
} catch { $refused = $true }
if (-not $refused -or -not (Test-Path -LiteralPath $unownedFile)) { throw 'Install must refuse an unowned nonempty directory without changing its files.' }
& (Join-Path $repoRoot 'installer\Install-TUNG_DIROOT.ps1') -StageRoot $StageRoot -InstallRoot $install -ManifestRoot $manifests -BackupRoot $backups
$sentinel = Join-Path $install '2024\old-only.dll'
[IO.File]::WriteAllText($sentinel, 'previous-runtime', [Text.UTF8Encoding]::new($false))
$oldManifest = [IO.File]::ReadAllText((Join-Path $manifests '2024\TUNG_DIROOT.addin'))
$newInstall = Join-Path $sandbox 'NewProgram'
$refused = $false
try {
    & (Join-Path $repoRoot 'installer\Install-TUNG_DIROOT.ps1') -StageRoot $StageRoot -InstallRoot $newInstall -ManifestRoot $manifests -BackupRoot (Join-Path $newInstall '2024\Backups')
} catch { $refused = $true }
if (-not $refused -or (Test-Path -LiteralPath $newInstall) -or
    [IO.File]::ReadAllText((Join-Path $manifests '2024\TUNG_DIROOT.addin')) -ne $oldManifest) { throw 'Backup inside a new installation root must be rejected before replacing the previous runtime.' }
& (Join-Path $repoRoot 'installer\Install-TUNG_DIROOT.ps1') -StageRoot $StageRoot -InstallRoot $install -ManifestRoot $manifests -BackupRoot $backups
if (Test-Path -LiteralPath $sentinel) { throw 'Old runtime should be replaced after backup.' }
$backup = @(Get-ChildItem -LiteralPath $backups -Directory)
if ($backup.Count -ne 1) { throw 'Upgrade must produce exactly one backup of the old installation.' }
$metadata = Get-Content -LiteralPath (Join-Path $backup[0].FullName 'backup.json') -Raw | ConvertFrom-Json
$savedManifest = Join-Path $backup[0].FullName $metadata.manifests[0].relativePath
if ([IO.File]::ReadAllText($savedManifest) -ne $oldManifest) { throw 'Manifest backup must preserve original bytes.' }
$extra = Join-Path (Join-Path $backup[0].FullName $metadata.programs[0].relativePath) '2024\unlisted.dll'
[IO.File]::WriteAllText($extra, 'not-hashed')
$unlistedRestore = Join-Path $sandbox 'UnlistedRestore'
$refused = $false
try {
    & (Join-Path $repoRoot 'installer\Restore-TUNG_DIROOT.ps1') -BackupPath $backup[0].FullName -RestoreRoot $unlistedRestore -ManifestRoot $manifests
} catch { $refused = $true }
if (-not $refused -or (Test-Path -LiteralPath $unlistedRestore)) { throw 'Restore must reject an unlisted backup payload before creating its destination.' }
Remove-Item -LiteralPath $extra -Force
& (Join-Path $repoRoot 'installer\Restore-TUNG_DIROOT.ps1') -BackupPath $backup[0].FullName -RestoreRoot $restore -ManifestRoot $manifests
[xml]$restoredManifest = Get-Content -LiteralPath (Join-Path $manifests '2024\TUNG_DIROOT.addin') -Raw
$restoredRuntime = Split-Path -Parent ([string]$restoredManifest.RevitAddIns.AddIn.Assembly)
if ([IO.File]::ReadAllText((Join-Path $restoredRuntime 'old-only.dll')) -ne 'previous-runtime') { throw 'Restore did not reactivate backed-up runtime.' }
if (-not (Test-Path -LiteralPath (Join-Path $install '2024\TUNG_DIROOT.Revit2024.dll'))) { throw 'Restore must preserve current install for recovery.' }
$manifestBeforeTamper = [IO.File]::ReadAllText((Join-Path $manifests '2024\TUNG_DIROOT.addin'))
[IO.File]::AppendAllText($savedManifest, 'tamper')
$rejectedRestore = Join-Path $sandbox 'RejectedRestore'
$refused = $false
try {
    & (Join-Path $repoRoot 'installer\Restore-TUNG_DIROOT.ps1') -BackupPath $backup[0].FullName -RestoreRoot $rejectedRestore -ManifestRoot $manifests
} catch { $refused = $true }
if (-not $refused -or (Test-Path -LiteralPath $rejectedRestore) -or
    [IO.File]::ReadAllText((Join-Path $manifests '2024\TUNG_DIROOT.addin')) -ne $manifestBeforeTamper) { throw 'Damaged backup must fail before modifying the current manifest.' }
Write-Host 'Install backup and restore contract passed.'
