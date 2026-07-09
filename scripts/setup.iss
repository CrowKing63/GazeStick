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

function IsViGEmInstalled: Boolean;
var
  Value: string;
begin
  Result := RegQueryStringValue(HKLM, ViGEmRegKey, 'DisplayName', Value);
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  if not IsViGEmInstalled then
  begin
    MsgBox(
      'ViGEmBus driver was not detected on this system.' + #13#10 + #13#10 +
      'GazeStick requires ViGEmBus to create a virtual Xbox 360 controller.' + #13#10 +
      'Please install it from: https://github.com/nefarius/ViGEmBus/releases/latest' + #13#10 +
      '(You can also continue with the installation, but GazeStick will not work without ViGEmBus.)',
      mbInformation, MB_OK);
  end;
end;
