; Read-only regression probe: InitializeSetup always aborts before installation.
#include "..\installer\AppIdentity.iss"
[Setup]
AppId=StatStudioUpgradeProbe
AppName=StatStudio Upgrade Probe
AppVersion=1.0
DefaultDirName={tmp}\StatStudioUpgradeProbe
CreateAppDir=no
Uninstallable=no
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
OutputDir=..\dist\upgrade-probe
OutputBaseFilename=UpgradeProbe

[Code]
#include "..\installer\UpgradeTarget.iss"

function InitializeSetup: Boolean;
var
  Target, Report, Text: String;
begin
  Target := ExpandConstant('{param:TARGET}');
  Report := ExpandConstant('{param:REPORT}');
  Text := 'identity=' + ExpandConstant('{#MyAppId}') + #13#10;
  if IsRegisteredTarget(Target) then Text := Text + 'registered=true' + #13#10
  else Text := Text + 'registered=false' + #13#10;
  if ValidateInstallTarget(Target) = '' then Text := Text + 'allowed=true' + #13#10
  else Text := Text + 'allowed=false' + #13#10;
  if Report <> '' then SaveStringToFile(Report, Text, False);
  Result := False;
end;
