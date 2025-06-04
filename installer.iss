#define MyAppName "Multi Saves Backup Tool"
#define MyAppVersion "1.0"
#define MyAppPublisher "Luki"
#define MyAppExeName "Multi Saves Backup Tool.exe"
#define MyServiceExeName "MultiSavesBackup.Service.exe"

[Setup]
AppId={{3FC85902-9E9B-45CF-9C89-6D937C8B1DD4}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
LicenseFile=
OutputDir=installer
OutputBaseFilename=MultiSavesBackupSetup
Compression=lzma
SolidCompression=yes
PrivilegesRequired=admin

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "publish\release\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\release\{#MyServiceExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\release\*.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "publish\release\settings.json"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
Filename: "sc.exe"; Parameters: "delete MultiSavesBackup"; Flags: runhidden;
Filename: "sc.exe"; Parameters: "create ""MultiSavesBackup"" binPath= ""{app}\{#MyServiceExeName}"" start= auto DisplayName= ""Multi Saves Backup Service"""; Flags: runhidden;
Filename: "sc.exe"; Parameters: "description MultiSavesBackup ""Service for automatic backup of game saves"""; Flags: runhidden;
Filename: "net.exe"; Parameters: "start MultiSavesBackup"; Flags: runhidden waituntilterminated;

[UninstallRun]
Filename: "net.exe"; Parameters: "stop MultiSavesBackup"; Flags: runhidden
Filename: "sc.exe"; Parameters: "delete MultiSavesBackup"; Flags: runhidden
