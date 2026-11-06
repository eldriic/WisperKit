; Installeur Windows de WisperKit (Inno Setup 6), par utilisateur, sans droits administrateur.
;   iscc /DAppVersion=0.2.0 /DArch=x64 installer\WisperKit.iss
; Attend la sortie de « dotnet publish » dans ..\publish\<Arch>.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef Arch
  #define Arch "x64"
#endif

[Setup]
AppId={{9267295C-38CD-4681-A255-B15C30766031}
AppName=WisperKit
AppVersion={#AppVersion}
AppVerName=WisperKit {#AppVersion}
AppPublisher=eldriic
AppPublisherURL=https://github.com/eldriic/WisperKit
AppSupportURL=https://github.com/eldriic/WisperKit/issues
AppUpdatesURL=https://github.com/eldriic/WisperKit/releases
DefaultDirName={localappdata}\Programs\WisperKit
PrivilegesRequired=lowest
DisableProgramGroupPage=yes
DisableDirPage=yes
OutputDir=..\dist
OutputBaseFilename=WisperKit-Setup-{#AppVersion}-{#Arch}
SetupIconFile=..\src\WisperKit.App\Assets\WisperKit.ico
UninstallDisplayIcon={app}\WisperKit.exe
UninstallDisplayName=WisperKit
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=force
#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[CustomMessages]
english.StartWithWindows=Start WisperKit when I sign in to Windows
french.StartWithWindows=Lancer WisperKit à l'ouverture de session Windows
english.LaunchApp=Launch WisperKit
french.LaunchApp=Lancer WisperKit

[Tasks]
Name: "startup"; Description: "{cm:StartWithWindows}"

[Files]
Source: "..\publish\{#Arch}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion createallsubdirs

[Icons]
Name: "{userprograms}\WisperKit"; Filename: "{app}\WisperKit.exe"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "WisperKit"; \
  ValueData: """{app}\WisperKit.exe"" --background"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\WisperKit.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/im WisperKit.exe /f"; Flags: runhidden; RunOnceId: "StopWisperKit"

[UninstallDelete]
; Les modèles (%LOCALAPPDATA%\WisperKit) et réglages (%APPDATA%\WisperKit) sont conservés.
Type: filesandordirs; Name: "{app}"
