#define MyAppName "Multi Saves Backup Tool"
#define MyAppVersion "42.0.0.0"
#define MyAppPublisher "Luki"
#define MyAppExeName "Multi Saves Backup Tool.exe"

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
name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[CustomMessages]
english.StartupDesc=Start with Windows
english.AdditionalSettings=Additional settings:
russian.StartupDesc=Автозапуск при старте Windows
russian.AdditionalSettings=Дополнительные параметры:

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "startup"; Description: "{cm:StartupDesc}"; GroupDescription: "{cm:AdditionalSettings}"

[Files]
Source: "publish\release\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
Root: HKLM; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: """{app}\{#MyAppExeName}"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "net.exe"; Parameters: "stop MultiSavesBackup"; Flags: runhidden;
Filename: "sc.exe"; Parameters: "delete MultiSavesBackup"; Flags: runhidden;