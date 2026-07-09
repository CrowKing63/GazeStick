; GazeStick Inno Setup installer
; Requires Inno Setup 6+ (https://jrsoftware.org/isdl.php)

#define MyAppName "GazeStick"
#define MyAppPublisher "GazeStick"
#define MyAppURL "https://github.com/CrowKing63/GazeStick"
#define MyAppExeName "GazeStick.exe"
#define MyAppAssocName "GazeStick App"

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif

#ifndef SourcePath
  #define SourcePath "..\publish-installer"
#endif

[Setup]
AppId={{B4E9F3A1-2C8D-4E7F-9A6B-3D5C1E0F8A2B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=admin
OutputDir=..
OutputBaseFilename=GazeStick-setup-{#MyAppVersion}
SetupIconFile=..\Resources\icon.ico
UninstallDisplayIcon={app}\GazeStick.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
Source: "{#SourcePath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: postinstall nowait skipifsilent shellexec

[Code]
const
  ViGEmRegKey = 'SYSTEM\CurrentControlSet\Services\vigem';
  ViGEmUrl = 'https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe';
  DotNet8Url = 'https://aka.ms/dotnet/8.0/desktop/runtime/windows-x64.exe';

function IsViGEmInstalled: Boolean;
var
  Value: string;
begin
  Result := RegQueryStringValue(HKLM, ViGEmRegKey, 'DisplayName', Value);
end;

function IsDotNet8DesktopInstalled: Boolean;
var
  Names: TArrayOfString;
  i: Integer;
begin
  Result := False;
  if RegGetSubkeyNames(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Names) then
  begin
    for i := 0 to GetArrayLength(Names) - 1 do
    begin
      if Copy(Names[i], 1, 3) = '8.0' then
      begin
        Result := True;
        Exit;
      end;
    end;
  end;
end;

function DownloadFile(Url, DestPath: string): Boolean;
var
  ResultCode: Integer;
  TmpScript: string;
begin
  TmpScript := ExpandConstant('{tmp}\dl.ps1');
  SaveStringToFile(TmpScript,
    '[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12;' + #13#10 +
    'Invoke-WebRequest -Uri "' + Url + '" -OutFile "' + DestPath + '"',
    False);
  Result := Exec('powershell.exe', '-ExecutionPolicy Bypass -File "' + TmpScript + '"', '',
    SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function InstallViGEmBus: Boolean;
var
  DestPath: string;
  ResultCode: Integer;
  TmpScript: string;
begin
  DestPath := ExpandConstant('{tmp}\ViGEmBus_1.22.0_x64_x86_arm64.exe');

  if not FileExists(DestPath) then
  begin
    if not DownloadFile(ViGEmUrl, DestPath) then
    begin
      if MsgBox('Failed to download ViGEmBus driver.' + #13#10 +
        'Open the download page in your browser?',
        mbError, MB_YESNO) = IDYES then
      begin
        ShellExec('open', 'https://github.com/nefarius/ViGEmBus/releases/tag/v1.22.0', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
      end;
      Result := False;
      Exit;
    end;
  end;

  if not Exec(DestPath, '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /ALLUSERS', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
  begin
    if MsgBox('ViGEmBus installation failed (error code: ' + IntToStr(ResultCode) + ').' + #13#10 +
      'Open the download page to install it manually?',
      mbError, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://github.com/nefarius/ViGEmBus/releases/tag/v1.22.0', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
    end;
    Result := False;
    Exit;
  end;

  Result := True;
end;

function InstallDotNet8: Boolean;
var
  DestPath: string;
  ResultCode: Integer;
begin
  DestPath := ExpandConstant('{tmp}\windowsdesktop-runtime-8.0-win-x64.exe');

  if not FileExists(DestPath) then
  begin
    if not DownloadFile(DotNet8Url, DestPath) then
    begin
      MsgBox('Failed to download .NET 8 Desktop Runtime. Please install it manually from:' + #13#10 +
        'https://dotnet.microsoft.com/en-us/download/dotnet/8.0',
        mbError, MB_OK);
      Result := False;
      Exit;
    end;
  end;

  if not Exec(DestPath, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) or (ResultCode <> 0) then
  begin
    MsgBox('.NET 8 Desktop Runtime installation failed (error code: ' + IntToStr(ResultCode) + ').' + #13#10 +
      'Please install it manually and run this installer again.',
      mbError, MB_OK);
    Result := False;
    Exit;
  end;

  Result := True;
end;

function InitializeSetup: Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;

  if not IsViGEmInstalled then
  begin
    if not InstallViGEmBus then
    begin
      if MsgBox(
        'ViGEmBus driver is required for GazeStick to work.' + #13#10 +
        'You can install it later manually.' + #13#10#13#10 +
        'Continue with GazeStick installation anyway?',
        mbConfirmation, MB_YESNO) = IDNO then
      begin
        Result := False;
        Exit;
      end;
    end;
  end;

  // .NET 8 Desktop Runtime is bundled with the self-contained installer.
  // No separate .NET installation is needed.
  // If switching to a framework-dependent build in the future,
  // uncomment below to auto-install .NET 8:
  //
  // if not IsDotNet8DesktopInstalled then
  //   if not InstallDotNet8 then
  //   begin
  //     Result := False;
  //     Exit;
  //   end;
end;
