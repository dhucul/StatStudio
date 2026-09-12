function RegisteredPathMatches(const Root: Integer; const Key, Target: String): Boolean;
var
  Previous: String;
begin
  Result := RegQueryStringValue(Root, Key, 'InstallLocation', Previous);
  if Result then
    Result := CompareText(RemoveBackslashUnlessRoot(ExpandFileName(Previous)),
      RemoveBackslashUnlessRoot(ExpandFileName(Target))) = 0;
end;

function IsRegisteredTarget(const Target: String): Boolean;
var
  Key: String;
begin
  // Use the same constant expansion as [Setup] AppId instead of a second GUID literal.
  Key := 'Software\Microsoft\Windows\CurrentVersion\Uninstall\' +
    ExpandConstant('{#MyAppId}') + '_is1';
  Result := RegisteredPathMatches(HKLM64, Key, Target) or
    RegisteredPathMatches(HKLM32, Key, Target) or
    RegisteredPathMatches(HKCU64, Key, Target) or
    RegisteredPathMatches(HKCU32, Key, Target);
end;

function ValidateInstallTarget(const Target: String): String;
var
  Entry: TFindRec;
begin
  Result := '';
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
