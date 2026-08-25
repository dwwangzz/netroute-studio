#ifndef MyAppVersion
  #define MyAppVersion "1.0.5"
#endif
#ifndef NumericVersion
  #define NumericVersion "1.0.5.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

#define MyAppName "NetRoute Studio"
#define MyAppPublisher "dwwangzz"
#define MyAppURL "https://github.com/dwwangzz/netroute-studio"
#define MyAppExeName "NetRouteStudio.App.exe"

[Setup]
AppId={{6D964C64-5B57-4CB8-9C62-E8E2C73B13A4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\NetRoute Studio
DefaultGroupName=NetRoute Studio
DisableProgramGroupPage=yes
OutputDir={#OutputDir}
OutputBaseFilename=NetRouteStudio-v{#MyAppVersion}-win-x64-setup
SetupIconFile=..\src\NetRouteStudio.App\Assets\NetRouteStudio.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#NumericVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=Windows 可视化网络策略管理工具安装程序
VersionInfoProductName={#MyAppName}
VersionInfoProductVersion={#MyAppVersion}

[Languages]
Name: "chinesesimp"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加快捷方式："; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\NetRoute Studio"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\卸载 NetRoute Studio"; Filename: "{uninstallexe}"
Name: "{autodesktop}\NetRoute Studio"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 NetRoute Studio"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent shellexec
