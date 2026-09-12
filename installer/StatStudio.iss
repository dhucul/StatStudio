; Inno Setup script for StatStudio.
; Build:  "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" installer\StatStudio.iss
; Produces dist\StatStudioSetup.exe from the self-contained publish in dist\publish.

#define MyAppName "StatStudio"
#define MyAppVersion "2.2.1"
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
; Install machine-wide. In administrative install mode, {autopf} resolves to the system's
; main Program Files folder instead of the current user's {localappdata}\Programs folder.
PrivilegesRequired=admin
; Custom destinations remain available; PrepareToInstall rejects nonempty folders
; unless they are the registered installation of this application.
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Reliable in-place upgrade: detect & close a running instance and don't auto-restart.
CloseApplications=yes
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

[Files]
; Replace only the named payload files. Never delete by extension or directory wildcard.
Source: "..\dist\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
function IsRegisteredTarget(const Target: String): Boolean;
var
  Previous: String;
begin
  Result := RegQueryStringValue(HKLM64,
    'Software\Microsoft\Windows\CurrentVersion\Uninstall\{F2A7C3D1-9B4E-4A6F-8C2D-1E5B7A9F3C04}_is1',
    'InstallLocation', Previous);
  if Result then
    Result := CompareText(RemoveBackslashUnlessRoot(ExpandFileName(Previous)),
      RemoveBackslashUnlessRoot(ExpandFileName(Target))) = 0;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  Target: String;
  Entry: TFindRec;
begin
  Result := '';
  Target := ExpandConstant('{app}');
  if IsRegisteredTarget(Target) then exit;
  if FindFirst(AddBackslash(Target) + '*', Entry) then begin
    try
      repeat
        if (Entry.Name <> '.') and (Entry.Name <> '..') then begin
          Result := 'Choose an empty folder or the registered StatStudio installation folder. Existing unrelated files will not be overwritten.';
          exit;
        end;
      until not FindNext(Entry);
    finally
      FindClose(Entry);
    end;
  end;
end;
