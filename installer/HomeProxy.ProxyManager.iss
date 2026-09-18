#define MyAppName "ProxyManager"
#define MyAppPublisher "ProxyManager"
#define MyAppExeName "ProxyManager.App.exe"

#ifndef AppVersion
#define AppVersion "1.0.0"
#endif

#ifndef SourceDir
#define SourceDir "..\artifacts\publish\win-x64"
#endif

#ifndef DeploymentMode
#define DeploymentMode "SelfContained"
#endif

#ifndef RequiresDotNetRuntime
#define RequiresDotNetRuntime "false"
#endif

#ifndef OutputSuffix
#define OutputSuffix "win-x64-self-contained"
#endif

[Setup]
AppId={{59D933F5-0B23-4E1A-BA1B-06C65B6E9108}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UsePreviousAppDir=no
OutputDir=..\artifacts\installer
OutputBaseFilename=ProxyManager.Setup-{#OutputSuffix}-{#AppVersion}
SetupArchitecture=x64
ArchitecturesAllowed=x64os
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
UninstallDisplayIcon={app}\{#MyAppExeName}

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{src}\assets\*"; DestDir: "{app}\assets"; Flags: external skipifsourcedoesntexist ignoreversion recursesubdirs createallsubdirs

[UninstallDelete]
Type: files; Name: "{app}\small-logo.generated*.ico"
Type: files; Name: "{app}\large-logo.generated*.png"
Type: files; Name: "{app}\small-logo.generated*.ico.tmp"
Type: filesandordirs; Name: "{app}\assets"
Type: dirifempty; Name: "{app}"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Parameters: "--prepare-branding"; Flags: runhidden waituntilterminated; Check: CanLaunchApp
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent runasoriginaluser; Check: CanLaunchApp

[Code]
const
  DeploymentMode = '{#DeploymentMode}';
  RequiresDotNetRuntime = '{#RequiresDotNetRuntime}';
  DotNetDesktopRuntimeUrl = 'https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe';
  DotNetDesktopRuntimeInstaller = 'windowsdesktop-runtime-8-win-x64.exe';
  ProxifierInstallerRelativePath = 'Installers\ProxifierSetup.exe';
  ProxifierSilentInstallArguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART';
  ProxifierSilentUninstallArguments = '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART';
  ProxyManagerRegistryKey = 'Software\ProxyManager';
  ProxifierOwnershipValueName = 'ProxifierInstalledByProxyManager';
  ProxifierUninstallValueName = 'ProxifierUninstallString';
  ProxifierExePathValueName = 'ProxifierExePath';

var
  DotNetRuntimeNeedsRestart: Boolean;
  ShouldInstallProxifier: Boolean;

function ShouldBootstrapDotNetRuntime(): Boolean;
begin
  Result := Lowercase(RequiresDotNetRuntime) = 'true';
end;

function HasDotNetDesktopRuntime8In(const RuntimeRoot: String): Boolean;
var
  FindRec: TFindRec;
begin
  Result := False;

  if not DirExists(RuntimeRoot) then
  begin
    Exit;
  end;

  if FindFirst(RuntimeRoot + '\8.*', FindRec) then
  begin
    try
      repeat
        if ((FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0) and
           (FindRec.Name <> '.') and (FindRec.Name <> '..') then
        begin
          Result := True;
          Exit;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function HasDotNetDesktopRuntime8(): Boolean;
begin
  Result :=
    HasDotNetDesktopRuntime8In(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App')) or
    HasDotNetDesktopRuntime8In(ExpandConstant('{commonpf}\dotnet\shared\Microsoft.WindowsDesktop.App'));
end;

function DetectProxifier(var ProxifierExePath: String; var ProxifierUninstallString: String): Boolean;
begin
  ProxifierExePath := '';
  ProxifierUninstallString := '';
  Result := False;
end;

function BuildProxifierSetupLogPath(): String;
begin
  Result := '';
end;

procedure RecordProxifierOwnership(const ProxifierExePath, ProxifierUninstallString: String);
begin
end;

procedure ClearProxifierOwnership();
begin
  RegDeleteValue(HKLM, ProxyManagerRegistryKey, ProxifierOwnershipValueName);
  RegDeleteValue(HKLM, ProxyManagerRegistryKey, ProxifierExePathValueName);
  RegDeleteValue(HKLM, ProxyManagerRegistryKey, ProxifierUninstallValueName);
end;

function SplitCommandLine(const CommandLine: String; var FileName: String; var Parameters: String): Boolean;
var
  Value: String;
  LowerValue: String;
  EndQuote: Integer;
  FirstSpace: Integer;
  ExePosition: Integer;
begin
  Value := Trim(CommandLine);
  LowerValue := Lowercase(Value);
  FileName := '';
  Parameters := '';
  Result := False;

  if Value = '' then
  begin
    Exit;
  end;

  if Copy(Value, 1, 1) = '"' then
  begin
    EndQuote := Pos('"', Copy(Value, 2, Length(Value) - 1));
    if EndQuote = 0 then
    begin
      Exit;
    end;

    FileName := Copy(Value, 2, EndQuote - 1);
    Parameters := Trim(Copy(Value, EndQuote + 2, Length(Value)));
    Result := FileName <> '';
    Exit;
  end;

  ExePosition := Pos('.exe', LowerValue);
  if ExePosition > 0 then
  begin
    FileName := Copy(Value, 1, ExePosition + 3);
    Parameters := Trim(Copy(Value, ExePosition + 4, Length(Value)));
    Result := FileName <> '';
    Exit;
  end;

  FirstSpace := Pos(' ', Value);
  if FirstSpace = 0 then
  begin
    FileName := Value;
    Parameters := '';
  end
  else
  begin
    FileName := Copy(Value, 1, FirstSpace - 1);
    Parameters := Trim(Copy(Value, FirstSpace + 1, Length(Value)));
  end;

  Result := FileName <> '';
end;

function HasSilentUninstallArguments(const Parameters: String): Boolean;
var
  LowerParameters: String;
begin
  LowerParameters := Lowercase(Parameters);
  Result :=
    (Pos('/verysilent', LowerParameters) > 0) or
    (Pos('/silent', LowerParameters) > 0) or
    (Pos('/quiet', LowerParameters) > 0) or
    (Pos('/qn', LowerParameters) > 0);
end;

function BuildSilentUninstallCommand(
  const StoredUninstallString: String;
  const StoredExePath: String;
  var FileName: String;
  var Parameters: String): Boolean;
var
  FallbackUninstaller: String;
begin
  Result := False;

  if SplitCommandLine(StoredUninstallString, FileName, Parameters) then
  begin
    if not HasSilentUninstallArguments(Parameters) then
    begin
      Parameters := Trim(Parameters + ' ' + ProxifierSilentUninstallArguments);
    end;

    Result := True;
    Exit;
  end;

  if StoredExePath <> '' then
  begin
    FallbackUninstaller := AddBackslash(ExtractFilePath(StoredExePath)) + 'unins000.exe';
    if FileExists(FallbackUninstaller) then
    begin
      FileName := FallbackUninstaller;
      Parameters := ProxifierSilentUninstallArguments;
      Result := True;
    end;
  end;
end;

function IsProxifierOwnedByProxyManager(): Boolean;
begin
  Result := RegValueExists(HKLM, ProxyManagerRegistryKey, ProxifierOwnershipValueName);
end;

procedure StopProxifierProcesses();
var
  ResultCode: Integer;
begin
  Exec(
    ExpandConstant('{cmd}'),
    '/C taskkill /IM Proxifier.exe /T /F',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);
end;

function OnDownloadProgress(const Url, FileName: String; const Progress, ProgressMax: Int64): Boolean;
begin
  Result := True;
end;

function InstallDotNetDesktopRuntime8(var NeedsRestart: Boolean): String;
var
  InstallerPath: String;
  ResultCode: Integer;
begin
  Result := '';

  if HasDotNetDesktopRuntime8() then
  begin
    Log('.NET 8 Desktop Runtime x64 is already installed.');
    Exit;
  end;

  try
    WizardForm.StatusLabel.Caption := 'Downloading Microsoft .NET 8 Desktop Runtime x64...';
    DownloadTemporaryFile(
      DotNetDesktopRuntimeUrl,
      DotNetDesktopRuntimeInstaller,
      '',
      @OnDownloadProgress);
  except
    Result := 'Cannot download Microsoft .NET 8 Desktop Runtime x64. Check the internet connection and run Setup again. Details: ' + GetExceptionMessage;
    Exit;
  end;

  InstallerPath := ExpandConstant('{tmp}\') + DotNetDesktopRuntimeInstaller;
  WizardForm.StatusLabel.Caption := 'Installing Microsoft .NET 8 Desktop Runtime x64...';

  if not Exec(InstallerPath, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    Result := 'Cannot start Microsoft .NET 8 Desktop Runtime installer. Error: ' + SysErrorMessage(ResultCode);
    Exit;
  end;

  if (ResultCode = 0) or (ResultCode = 3010) then
  begin
    DotNetRuntimeNeedsRestart := ResultCode = 3010;
    NeedsRestart := NeedsRestart or DotNetRuntimeNeedsRestart;
    Exit;
  end;

  Result := 'Microsoft .NET 8 Desktop Runtime installer failed with exit code ' + IntToStr(ResultCode) + '.';
end;

procedure MaybeInstallProxifier();
var
  InstallerPath: String;
  LogPath: String;
  Arguments: String;
  ResultCode: Integer;
  ProxifierExePath: String;
  ProxifierUninstallString: String;
begin
  try
  if not ShouldInstallProxifier then
  begin
    Exit;
  end;

  InstallerPath := ExpandConstant('{app}\') + ProxifierInstallerRelativePath;
  if not FileExists(InstallerPath) then
  begin
    MsgBox(
      'Không tìm thấy ProxifierSetup.exe tại: ' + InstallerPath + #13#10 +
      'ProxyManager vẫn được cài đặt, nhưng bạn cần cài Proxifier thủ công hoặc dùng chức năng cài đặt trong app.',
      mbInformation,
      MB_OK);
    Exit;
  end;

  WizardForm.StatusLabel.Caption := 'Đang cài đặt Proxifier Standard Edition...';
  LogPath := BuildProxifierSetupLogPath();
  Arguments := ProxifierSilentInstallArguments + ' /LOG="' + LogPath + '"';

  if not Exec(InstallerPath, Arguments, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    MsgBox(
      'Không thể khởi chạy ProxifierSetup.exe. ProxyManager vẫn được cài đặt.' + #13#10 +
      'Log dự kiến: ' + LogPath,
      mbInformation,
      MB_OK);
    Exit;
  end;

  if (ResultCode <> 0) and (ResultCode <> 3010) then
  begin
    MsgBox(
      'Cài đặt Proxifier kết thúc với exit code ' + IntToStr(ResultCode) + '.' + #13#10 +
      'ProxyManager vẫn được cài đặt. Log: ' + LogPath,
      mbInformation,
      MB_OK);
    Exit;
  end;

  if DetectProxifier(ProxifierExePath, ProxifierUninstallString) and
     (Trim(ProxifierUninstallString) <> '') then
  begin
    RecordProxifierOwnership(ProxifierExePath, ProxifierUninstallString);
    if ResultCode = 3010 then
    begin
      DotNetRuntimeNeedsRestart := True;
    end;
  end
  else
  begin
    MsgBox(
      'Proxifier installer đã chạy xong nhưng không tìm thấy thông tin uninstall để ghi ownership.' + #13#10 +
      'ProxyManager sẽ không tự gỡ Proxifier khi uninstall. Log: ' + LogPath,
      mbInformation,
      MB_OK);
  end;
  except
    MsgBox(
      'Cài đặt Proxifier gặp lỗi: ' + GetExceptionMessage + #13#10 +
      'ProxyManager vẫn được cài đặt.',
      mbInformation,
      MB_OK);
  end;
end;

procedure MaybeUninstallOwnedProxifier();
var
  StoredUninstallString: String;
  StoredExePath: String;
  FileName: String;
  Parameters: String;
  ResultCode: Integer;
begin
  if not IsProxifierOwnedByProxyManager() then
  begin
    Exit;
  end;

  RegQueryStringValue(HKLM, ProxyManagerRegistryKey, ProxifierUninstallValueName, StoredUninstallString);
  RegQueryStringValue(HKLM, ProxyManagerRegistryKey, ProxifierExePathValueName, StoredExePath);

  if not BuildSilentUninstallCommand(StoredUninstallString, StoredExePath, FileName, Parameters) then
  begin
    MsgBox(
      'ProxyManager đã cài Proxifier trước đó nhưng không tìm thấy lệnh gỡ Proxifier. ' +
      'Bạn có thể gỡ Proxifier thủ công từ Windows Settings.',
      mbInformation,
      MB_OK);
    ClearProxifierOwnership();
    Exit;
  end;

  StopProxifierProcesses();

  if not Exec(FileName, Parameters, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    MsgBox(
      'Không thể khởi chạy Proxifier uninstaller. Bạn có thể gỡ Proxifier thủ công từ Windows Settings.',
      mbInformation,
      MB_OK);
    ClearProxifierOwnership();
    Exit;
  end;

  if (ResultCode <> 0) and (ResultCode <> 3010) then
  begin
    MsgBox(
      'Proxifier uninstaller kết thúc với exit code ' + IntToStr(ResultCode) + '. ' +
      'Bạn có thể kiểm tra và gỡ Proxifier thủ công nếu cần.',
      mbInformation,
      MB_OK);
  end;

  ClearProxifierOwnership();
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ProxifierExePath: String;
  ProxifierUninstallString: String;
begin
  Result := '';

  if ShouldBootstrapDotNetRuntime() then
  begin
    Result := InstallDotNetDesktopRuntime8(NeedsRestart);
  end;

  if Result <> '' then
  begin
    Exit;
  end;

  ShouldInstallProxifier := False;

  if DetectProxifier(ProxifierExePath, ProxifierUninstallString) then
  begin
    Log('Proxifier is already installed. ProxyManager will not take uninstall ownership.');
    Exit;
  end;

  ShouldInstallProxifier := True;

end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    MaybeUninstallOwnedProxifier();
  end;
end;

function NeedRestart(): Boolean;
begin
  Result := DotNetRuntimeNeedsRestart;
end;

function CanLaunchApp(): Boolean;
begin
  Result := not DotNetRuntimeNeedsRestart;
end;
