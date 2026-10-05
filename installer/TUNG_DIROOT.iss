#define MyAppName "TUNG_DIROOT"
#define MyAppVersion "1.3.0"
#define MyAppPublisher "TUNG"
#ifndef StageRoot
  #define StageRoot "..\artifacts\staging\TUNG_DIROOT"
#endif
#ifndef BuildOutputDir
  #define BuildOutputDir "..\dist"
#endif

[Setup]
AppId={{8A93D06F-E03B-4A7C-9A96-DF5A3D5B0E24}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppComments=Excel, sheet, parameter, family and model productivity tools for Autodesk Revit 2024
DefaultDirName={localappdata}\TUNG_DIROOT\{#MyAppVersion}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#BuildOutputDir}
OutputBaseFilename=TUNG_DIROOT-Setup-Revit2024
Compression=lzma2
SolidCompression=yes
UninstallDisplayName=TUNG_DIROOT {#MyAppVersion}

[Files]
Source: "Backup-TUNG_DIROOT.ps1"; DestDir: "{app}\Installer"; Flags: ignoreversion
Source: "{#StageRoot}\2024\*"; DestDir: "{app}\2024"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageRoot}\Samples\*"; DestDir: "{app}\Samples"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#StageRoot}\Docs\*"; DestDir: "{app}\Docs"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "New-RevitManifest.ps1"; DestDir: "{app}\Installer"; Flags: ignoreversion
Source: "Uninstall-TUNG_DIROOT.ps1"; DestDir: "{app}\Installer"; Flags: ignoreversion
Source: "Restore-TUNG_DIROOT.ps1"; DestDir: "{app}\Installer"; Flags: ignoreversion
Source: "{#StageRoot}\release.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "TUNG_DIROOT.install-marker"; DestDir: "{app}"; Flags: ignoreversion

[InstallDelete]
Type: filesandordirs; Name: "{app}\2024"
Type: filesandordirs; Name: "{app}\Samples"
Type: filesandordirs; Name: "{app}\Docs"

[Run]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Installer\New-RevitManifest.ps1"" -RevitVersion 2024 -AssemblyPath ""{app}\2024\TUNG_DIROOT.Revit2024.dll"" -OutputPath ""{userappdata}\Autodesk\Revit\Addins\2024\TUNG_DIROOT.addin"""; Flags: runhidden waituntilterminated

[UninstallRun]
Filename: "{sys}\WindowsPowerShell\v1.0\powershell.exe"; Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\Installer\Uninstall-TUNG_DIROOT.ps1"" -InstallRoot ""{app}"""; Flags: runhidden waituntilterminated; RunOnceId: "TUNG_DIROOT_Uninstall_1_3_0"

[Code]
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ExitCode: Integer;
  Parameters: String;
begin
  Result := '';
  ExtractTemporaryFile('Backup-TUNG_DIROOT.ps1');
  Parameters := '-NoProfile -ExecutionPolicy Bypass -File ' +
    AddQuotes(ExpandConstant('{tmp}\Backup-TUNG_DIROOT.ps1')) +
    ' -InstallRoot ' + AddQuotes(ExpandConstant('{app}')) +
    ' -ManifestRoot ' + AddQuotes(ExpandConstant('{userappdata}\Autodesk\Revit\Addins')) +
    ' -BackupRoot ' + AddQuotes(ExpandConstant('{localappdata}\TUNG_DIROOT\Backups'));
  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    Parameters, '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
    Result := 'Cannot start backup of the previous TUNG_DIROOT installation.';
  if (Result = '') and (ExitCode <> 0) then
    Result := 'Backup of the previous TUNG_DIROOT installation failed. Installation has not started.';
end;
