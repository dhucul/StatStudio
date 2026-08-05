; Inno Setup script for StatStudio.
; Build:  "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" installer\StatStudio.iss
; Produces dist\StatStudioSetup.exe from the self-contained publish in dist\publish.

#define MyAppName "StatStudio"
#define MyAppVersion "2.2.0"
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
; Per-user install with no UAC prompt, as documented in README.md. {autopf} resolves to
; {localappdata}\Programs here; pass /ALLUSERS on the command line for a machine-wide install.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline
; NOTE: do not set DisableDirPage=yes here — Inno ignores the /DIR= command-line switch when the
; directory page is disabled, and tools/e2e-install.ps1 relies on /DIR to install into an isolated
; test folder. The [InstallDelete] section below is scoped to this installer's own payload instead,
; so a retargeted {app} can no longer be wiped recursively.
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
; Derived from MyAppVersion so the two cannot drift apart on the next bump.
VersionInfoVersion={#MyAppVersion}.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
; Clear the previous build's payload so an older install is fully replaced. Scoped to what this
; installer actually writes rather than "{app}\*", which would recursively wipe whatever {app}
; happened to point at.
Type: files; Name: "{app}\*.exe"
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.json"
Type: files; Name: "{app}\*.pdb"
Type: filesandordirs; Name: "{app}\runtimes"

[Files]
; The entire self-contained app (exe + .NET runtime).
Source: "..\dist\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
