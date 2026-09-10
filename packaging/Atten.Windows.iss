; Atten's traditional Windows installer. The compiled app is intentionally
; unpackaged and self-contained: no Python, .NET runtime, Windows App SDK, or
; model download is required on the user's computer.

#ifndef SourceRoot
  #error SourceRoot must point to the staged publish directory.
#endif
#ifndef MyAppVersion
  #error MyAppVersion must be supplied by the release build.
#endif
#ifndef MyOutputBaseName
  #define MyOutputBaseName "Atten-Windows-x64-Setup"
#endif

#define MyAppName "Atten"
#define MyAppExeName "Atten.Windows.exe"

[Setup]
AppId={{D8D1E84C-3A6A-44E7-9C71-13E8EB0F8C65}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=Atten
DefaultDirName={autopf}\Atten
DefaultGroupName=Atten
DisableProgramGroupPage=yes
LicenseFile={#SourceRoot}\Licenses\GPL-3.0-or-later.txt
OutputBaseFilename={#MyOutputBaseName}
OutputDir=.
Compression=lzma2/ultra64
SolidCompression=yes
LZMANumBlockThreads=4
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
PrivilegesRequired=admin
UninstallDisplayName=Atten
UninstallDisplayIcon={app}\{#MyAppExeName}
WizardStyle=modern
DisableWelcomePage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceRoot}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Atten"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\Atten"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Atten"; Flags: nowait postinstall skipifsilent

[Code]
const
  MinimumInstalledRamGiB = 8;
  RecommendedAvailableRamGiB = 4;
  RequiredDiskMiB = 4096;

type
  TMemoryStatusEx = record
    dwLength: DWORD;
    dwMemoryLoad: DWORD;
    ullTotalPhys: Int64;
    ullAvailPhys: Int64;
    ullTotalPageFile: Int64;
    ullAvailPageFile: Int64;
    ullTotalVirtual: Int64;
    ullAvailVirtual: Int64;
    ullAvailExtendedVirtual: Int64;
  end;

function GlobalMemoryStatusEx(var MemoryStatus: TMemoryStatusEx): Boolean;
  external 'GlobalMemoryStatusEx@kernel32.dll stdcall';

function BytesToGiB(const Bytes: Int64): Integer;
begin
  Result := Bytes div 1073741824;
end;

procedure InitializeWizard;
var
  Requirements: TNewStaticText;
begin
  Requirements := TNewStaticText.Create(WizardForm.WelcomePage);
  Requirements.Parent := WizardForm.WelcomePage;
  Requirements.Left := ScaleX(20);
  Requirements.Top := ScaleY(155);
  Requirements.Width := WizardForm.WelcomePage.SurfaceWidth - ScaleX(40);
  Requirements.Height := ScaleY(118);
  Requirements.AutoSize := False;
  Requirements.WordWrap := True;
  Requirements.Caption :=
    'System requirements' + #13#10 +
    '- 64-bit Windows 10 version 1809 or newer, or Windows 11' + #13#10 +
    '- 8 GB RAM (4 GB available is recommended while generating speech)' + #13#10 +
    '- 4 GB free disk space for the app, offline model, and voice files' + #13#10 +
    '- An x64-compatible processor' + #13#10 + #13#10 +
    'Atten includes its speech engine, model, Python runtime, .NET runtime, and Windows App SDK. Internet access is not needed after this installer has been downloaded.';
end;

function InitializeSetup(): Boolean;
var
  MemoryStatus: TMemoryStatusEx;
begin
  Result := False;
  if not IsWin64 then begin
    MsgBox('Atten requires 64-bit Windows on an x64-compatible processor.', mbCriticalError, MB_OK);
    exit;
  end;

  MemoryStatus.dwLength := SizeOf(MemoryStatus);
  if GlobalMemoryStatusEx(MemoryStatus) and
     (MemoryStatus.ullTotalPhys < Int64(MinimumInstalledRamGiB) * 1073741824) then begin
    MsgBox(
      'Atten requires at least ' + IntToStr(MinimumInstalledRamGiB) +
      ' GB of installed RAM. This computer reports ' +
      IntToStr(BytesToGiB(MemoryStatus.ullTotalPhys)) + ' GB.',
      mbCriticalError,
      MB_OK);
    exit;
  end;

  Result := True;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
var
  MemoryStatus: TMemoryStatusEx;
  FreeSpace, TotalSpace: Int64;
begin
  Result := True;
  if CurPageID <> wpSelectDir then
    exit;

  if not GetSpaceOnDisk(WizardDirValue, True, FreeSpace, TotalSpace) or
     (FreeSpace < RequiredDiskMiB) then begin
    MsgBox(
      'Atten needs at least ' + IntToStr(RequiredDiskMiB div 1024) +
      ' GB of free disk space in the selected installation location.',
      mbCriticalError,
      MB_OK);
    Result := False;
    exit;
  end;

  MemoryStatus.dwLength := SizeOf(MemoryStatus);
  if GlobalMemoryStatusEx(MemoryStatus) and
     (MemoryStatus.ullAvailPhys < Int64(RecommendedAvailableRamGiB) * 1073741824) then begin
    Result := MsgBox(
      'Atten works best with at least ' + IntToStr(RecommendedAvailableRamGiB) +
      ' GB of RAM currently available for model loading. Close memory-intensive applications before generating speech. Continue anyway?',
      mbConfirmation,
      MB_YESNO) = IDYES;
  end;
end;
