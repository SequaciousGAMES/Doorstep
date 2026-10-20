#define MyAppName "Doorstep"
#define MyAppExeName "Doorstep.exe"
#define MyAppVersion "1.0.0"
#define MyAppId "8F3C1D6A-2E47-4B90-A5C8-1D7E9B04F6A2"
#ifndef PayloadBytes
#define PayloadBytes "200000000"
#endif

[Setup]
AppId={{{#MyAppId}}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName}
AppPublisher=Doorstep
ArchiveExtraction=full
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=no
AllowNoIcons=no
InfoBeforeFile=Disclaimer.txt
OutputDir=..\artifacts
OutputBaseFilename=DoorstepSetup
SetupIconFile=..\src\Doorstep\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Administrative mode is the default, so {autopf} is Program Files.
; The dialog still offers a per-user install under {localappdata}\Programs.
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
UsedUserAreasWarning=no
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
MinVersion=10.0

[Files]
; The app is not compiled into this installer. Setup downloads the latest public release.
Source: "https://github.com/SequaciousGAMES/Doorstep/releases/latest/download/Doorstep-win-x64.zip"; DestDir: "{app}"; DestName: "Doorstep-win-x64.zip"; ExternalSize: {#PayloadBytes}; Flags: external download extractarchive recursesubdirs createallsubdirs ignoreversion
Source: "redist\MicrosoftEdgeWebview2Setup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "Doorstep (unofficial)"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
; Silent updates pass /launch=1. Interactive setup keeps the finish-page checkbox.
Filename: "{app}\{#MyAppExeName}"; Flags: nowait skipifnotsilent; Check: LaunchAfterUpdate

; Doorstep's private leftovers only. The shared WebView2 Runtime is not removed.
[UninstallDelete]
Type: files; Name: "{userprograms}\{#MyAppName}.lnk"
Type: filesandordirs; Name: "{localappdata}\Doorstep"
Type: filesandordirs; Name: "{localappdata}\ToastNotificationManagerCompat\Apps\Local.Doorstep"

[Code]
function IsWebView2Installed: Boolean;
var
  Version: String;
begin
  Result := RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version);
  if not Result then
    Result := RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version);
  if Result then
    Result := (Version <> '') and (Version <> '0.0.0.0');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Result := '';
  if IsWebView2Installed then
    exit;

  if not Exec(ExpandConstant('{tmp}\MicrosoftEdgeWebview2Setup.exe'), '/silent /install', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'Doorstep needs the Microsoft Edge WebView2 Runtime, and it could not be installed.';
    exit;
  end;

  if (ResultCode <> 0) and (ResultCode <> 3010) then
    Result := 'Doorstep needs the Microsoft Edge WebView2 Runtime. The runtime installer returned ' + IntToStr(ResultCode) + '.';
  if ResultCode = 3010 then
    NeedsRestart := True;
end;

function ShortcutTarget(const ShortcutPath: String): String;
var
  Shell: Variant;
begin
  Result := '';
  try
    Shell := CreateOleObject('WScript.Shell');
    Result := Shell.CreateShortcut(ShortcutPath).TargetPath;
  except
  end;
end;

procedure RemoveShortcutsTo(const Dir, ExePath: String);
var
  FindRec: TFindRec;
  ShortcutPath, Target: String;
begin
  if not DirExists(Dir) then
    exit;
  if not FindFirst(AddBackslash(Dir) + '*.lnk', FindRec) then
    exit;
  try
    repeat
      ShortcutPath := AddBackslash(Dir) + FindRec.Name;
      Target := ShortcutTarget(ShortcutPath);
      if (Target <> '') and (CompareText(Target, ExePath) = 0) then
        DeleteFile(ShortcutPath);
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

procedure RemovePrivateRegistration;
var
  Activator, Command: String;
  ExePath: String;
begin
  ExePath := ExpandConstant('{app}\{#MyAppExeName}');
  RemoveShortcutsTo(ExpandConstant('{userprograms}'), ExePath);
  RemoveShortcutsTo(ExpandConstant('{commonprograms}'), ExePath);
  RemoveShortcutsTo(ExpandConstant('{userdesktop}'), ExePath);
  RemoveShortcutsTo(ExpandConstant('{commondesktop}'), ExePath);
  RemoveShortcutsTo(ExpandConstant('{userappdata}\Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar'), ExePath);

  if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Doorstep', Command) then
    if Pos('Doorstep.exe', Command) > 0 then
      RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'Doorstep');
  RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run', 'Doorstep');

  if RegQueryStringValue(HKCU, 'Software\Classes\AppUserModelId\Local.Doorstep', 'CustomActivator', Activator) then
    if RegQueryStringValue(HKCU, 'Software\Classes\CLSID\' + Activator + '\LocalServer32', '', Command) then
      if Pos('Doorstep.exe', Command) > 0 then
        RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\CLSID\' + Activator);
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Classes\AppUserModelId\Local.Doorstep');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Notifications\Settings\Local.Doorstep');
  RegDeleteKeyIncludingSubkeys(HKCU, 'Software\Microsoft\Windows\CurrentVersion\PushNotifications\Backup\Local.Doorstep');
end;

procedure RemovePrivateFiles;
begin
  DelTree(ExpandConstant('{localappdata}\Doorstep'), True, True, True);
  DelTree(ExpandConstant('{localappdata}\ToastNotificationManagerCompat\Apps\Local.Doorstep'), True, True, True);
end;

function LaunchAfterUpdate: Boolean;
begin
  Result := ExpandConstant('{param:launch|0}') = '1';
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Version: String;
  Root: Integer;
begin
  if CurStep <> ssPostInstall then
    exit;
  if not GetVersionNumbersString(ExpandConstant('{app}\{#MyAppExeName}'), Version) then
    exit;
  if IsAdminInstallMode then
    Root := HKLM
  else
    Root := HKCU;
  RegWriteStringValue(Root, 'Software\Microsoft\Windows\CurrentVersion\Uninstall\{8F3C1D6A-2E47-4B90-A5C8-1D7E9B04F6A2}_is1', 'DisplayVersion', Version);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemovePrivateRegistration;
  if CurUninstallStep = usPostUninstall then
    RemovePrivateFiles;
end;
