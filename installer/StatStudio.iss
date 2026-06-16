; Inno Setup script for StatStudio.
; Build:  "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" installer\StatStudio.iss
; Produces dist\StatStudioSetup.exe from the self-contained publish in dist\publish.

#define MyAppName "StatStudio"
#define MyAppVersion "2.0"
#define MyAppPublisher "StatStudio"
#define MyAppExeName "StatStudio.exe"

[Setup]
AppId={{F2A7C3D1-9B4E-4A6F-8C2D-1E5B7A9F3C04}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\StatStudio
DefaultGroupName=StatStudio
DisableProgramGroupPage=yes
UninstallDisplayName={#MyAppName}
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\src\StatStudio.Wpf\app.ico
; The app does not need elevation, so install per-user (no UAC prompt).
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Reliable in-place upgrade: detect & close a running instance and don't auto-restart.
CloseApplications=force
CloseApplicationsFilter=*.exe,*.dll
RestartApplications=no
OutputDir=..\dist
OutputBaseFilename=StatStudioSetup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
VersionInfoVersion=2.0.0.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
; Wipe the install folder before copying so an older install is fully replaced.
Type: filesandordirs; Name: "{app}\*"

[Files]
; The entire self-contained app (exe + .NET runtime).
Source: "..\dist\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
