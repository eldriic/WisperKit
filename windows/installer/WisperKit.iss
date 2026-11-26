; Installeur Windows de WisperKit (Inno Setup 6.7 ou plus récent).
;   iscc /DAppVersion=0.2.1 /DArch=x64 installer\WisperKit.iss
; Attend la sortie de « dotnet publish » dans ..\publish\<Arch>.
; Images (art\) : WisperKit.exe --installer-art installer\art, à relancer si le logo ou la vague changent.
;
; Par défaut pour l'utilisateur courant, sans droits administrateur (%LOCALAPPDATA%\Programs, HKCU) ; une
; boîte au lancement permet aussi d'installer pour tous les utilisateurs (droits admin, Program Files, HKLM).
; Le lancement à l'ouverture de session reste toujours dans HKCU, comme l'interrupteur des Réglages de l'app.
; Journal d'installation : %TEMP%\Setup Log *.txt.

#if VER < EncodeVer(6, 7, 0)
  #error Inno Setup 6.7 ou plus récent est requis (mode sombre, couleur de fond).
#endif
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#define AppId "9267295C-38CD-4681-A255-B15C30766031"
#define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"
#define UninstallKey "Software\Microsoft\Windows\CurrentVersion\Uninstall\{" + AppId + "}_is1"

[Setup]
AppId={{{#AppId}}
AppName=WisperKit
AppVersion={#AppVersion}
AppVerName=WisperKit {#AppVersion}
AppPublisher=eldriic
AppPublisherURL=https://github.com/eldriic/WisperKit
AppSupportURL=https://github.com/eldriic/WisperKit/issues
AppUpdatesURL=https://github.com/eldriic/WisperKit/releases
VersionInfoVersion={#AppVersion}
VersionInfoProductName=WisperKit
VersionInfoDescription=WisperKit Setup
; Utilisateur courant par défaut, « tous les utilisateurs » au choix (boîte au lancement ou /ALLUSERS).
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog commandline
DefaultDirName={autopf}\WisperKit
UsePreviousAppDir=yes
DisableProgramGroupPage=yes
DisableDirPage=auto
DisableWelcomePage=no
ShowLanguageDialog=no
OutputDir=..\dist
OutputBaseFilename=WisperKit-Setup-{#Arch}
SetupIconFile=..\src\WisperKit.App\Assets\WisperKit.ico
UninstallDisplayIcon={app}\WisperKit.exe
UninstallDisplayName=WisperKit
Compression=lzma2/max
SolidCompression=yes
; Même direction artistique que l'app : fond noir, halo violet, vagues.
WizardStyle=modern dark hidebevels includetitlebar
WizardBackColor=#000000
WizardImageFile=art\wizard-100.png,art\wizard-125.png,art\wizard-150.png,art\wizard-200.png,art\wizard-250.png
WizardSmallImageFile=art\wizard-small-100.png,art\wizard-small-125.png,art\wizard-small-150.png,art\wizard-small-200.png,art\wizard-small-250.png
WizardImageBackColor=#000000
; L'app tourne souvent (zone de notification) : on la ferme avant de remplacer ses fichiers.
CloseApplications=force
RestartApplications=no
; Modèle téléchargé au premier lancement (~550 Mo).
ExtraDiskSpaceRequired=600000000
SetupLogging=yes
UsedUserAreasWarning=no
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

[Messages]
english.WelcomeLabel2=This will install [name/ver] on your computer.%n%nPress a shortcut, speak, and your words appear wherever your cursor is. Transcription runs entirely on this PC: no audio ever leaves it.
french.WelcomeLabel2=Cet assistant va installer [name/ver] sur votre ordinateur.%n%nUn raccourci, vous parlez, et le texte s'écrit là où se trouve votre curseur. La transcription se fait entièrement sur ce PC : aucun son ne le quitte.
english.FinishedLabel=WisperKit is installed. It lives in the notification area (the arrow next to the clock).%n%nOn first launch the speech model (~550 MB) downloads once, then press Ctrl + Alt + D anywhere to dictate.
french.FinishedLabel=WisperKit est installé. Il se trouve dans la zone de notification (la flèche à côté de l'horloge).%n%nAu premier lancement, le modèle de transcription (~550 Mo) se télécharge une seule fois, puis appuyez sur Ctrl + Alt + D n'importe où pour dicter.

[CustomMessages]
english.StartWithWindows=Start WisperKit when I sign in to Windows
french.StartWithWindows=Lancer WisperKit à l'ouverture de session Windows
english.DesktopIcon=Create a desktop shortcut
french.DesktopIcon=Créer un raccourci sur le Bureau
english.LaunchApp=Launch WisperKit
french.LaunchApp=Lancer WisperKit
english.CloseFailed=WisperKit is still running and could not be closed.%n%nQuit it from its icon in the notification area (right of the taskbar), then click Retry.
french.CloseFailed=WisperKit est toujours ouvert et n'a pas pu être fermé.%n%nQuittez-le depuis son icône dans la zone de notification (à droite de la barre des tâches), puis cliquez sur Réessayer.
english.OtherInstallRemoved=A previous WisperKit installation was removed so that only one copy remains.
french.OtherInstallRemoved=Une ancienne installation de WisperKit a été supprimée pour n'en garder qu'une.
english.OtherInstallFailed=A previous WisperKit installation (%1) could not be removed automatically (code %2). You can uninstall it later from Settings > Apps.
french.OtherInstallFailed=Une ancienne installation de WisperKit (%1) n'a pas pu être supprimée automatiquement (code %2). Vous pourrez la désinstaller plus tard depuis Paramètres > Applications.
english.AllUsersExists=WisperKit is already installed for all users on this PC. Installing it for you only as well would start it twice at sign-in.%n%nRun this installer again and choose "Install for all users", or uninstall the existing copy first from Settings > Apps.
french.AllUsersExists=WisperKit est déjà installé pour tous les utilisateurs de ce PC. L'installer aussi pour vous seul le lancerait deux fois à l'ouverture de session.%n%nRelancez cet installeur et choisissez « Installer pour tous les utilisateurs », ou désinstallez d'abord la copie existante depuis Paramètres > Applications.
english.StartupFailed=WisperKit is installed, but it could not be set to start with Windows (registry key %1 could not be written).%n%nYou can enable it later in WisperKit > Settings > General.
french.StartupFailed=WisperKit est installé, mais son lancement à l'ouverture de session n'a pas pu être activé (clé de registre %1 non écrite).%n%nVous pourrez l'activer plus tard dans WisperKit > Réglages > Général.
english.MissingExe=Installation is incomplete: %1 is missing. Your antivirus may have blocked it.%n%nAllow it, then run this installer again. Setup log: %2
french.MissingExe=L'installation est incomplète : %1 est absent. Votre antivirus l'a peut-être bloqué.%n%nAutorisez-le, puis relancez cet installeur. Journal d'installation : %2
english.RemoveData=Also delete the downloaded speech models and your WisperKit settings?%n%nChoose No to keep them for a future reinstallation.
french.RemoveData=Supprimer aussi les modèles de transcription téléchargés et vos réglages WisperKit ?%n%nChoisissez Non pour les garder en vue d'une réinstallation.

[Tasks]
Name: "startup"; Description: "{cm:StartWithWindows}"
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; Flags: unchecked

[Files]
Source: "..\publish\{#Arch}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion createallsubdirs

[InstallDelete]
; Fichiers d'une version précédente qui ne font plus partie de l'app (runtimes natifs, etc.).
Type: filesandordirs; Name: "{app}\runtimes"

[Icons]
Name: "{autoprograms}\WisperKit"; Filename: "{app}\WisperKit.exe"; AppUserModelID: "eldriic.WisperKit"
Name: "{autodesktop}\WisperKit"; Filename: "{app}\WisperKit.exe"; Tasks: desktopicon

[Registry]
; Toujours HKCU, même pour « tous les utilisateurs » : l'interrupteur « Lancer à l'ouverture de session »
; de l'app lit et écrit HKCU, et chacun peut ainsi le couper sans droits admin.
Root: HKCU; Subkey: "{#RunKey}"; ValueType: string; ValueName: "WisperKit"; \
  ValueData: """{app}\WisperKit.exe"" --background"; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\WisperKit.exe"; Description: "{cm:LaunchApp}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/im WisperKit.exe /f"; Flags: runhidden; RunOnceId: "StopWisperKit"

[UninstallDelete]
Type: filesandordirs; Name: "{app}"

[Code]
const
  ExeName = 'WisperKit.exe';

function IsAppRunning(): Boolean;
var
  ResultCode: Integer;
begin
  // tasklist renvoie toujours 0 : on filtre sa sortie avec find, qui renvoie 0 seulement si trouvé.
  Result := Exec(ExpandConstant('{cmd}'), '/C ' + AddQuotes(ExpandConstant('{sys}\tasklist.exe')) +
                 ' /FI "IMAGENAME eq ' + ExeName + '" /NH | ' + AddQuotes(ExpandConstant('{sys}\find.exe')) + ' /I "' + ExeName + '"',
                 '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function StopApp(): Boolean;
var
  ResultCode, I: Integer;
begin
  Result := not IsAppRunning();
  if Result then Exit;
  Log('WisperKit est ouvert : fermeture.');
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/im ' + ExeName + ' /f', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  for I := 1 to 20 do
  begin
    if not IsAppRunning() then
    begin
      Result := True;
      Exit;
    end;
    Sleep(250);
  end;
  Log('WisperKit ne s''est pas fermé (taskkill : ' + IntToStr(ResultCode) + ').');
end;

// Désinstalle silencieusement l'installation de l'autre portée (personnelle ↔ tous les utilisateurs),
// pour éviter deux copies lancées à l'ouverture de session.
procedure RemoveInstall(RootKey: Integer; Scope: String);
var
  Uninstaller: String;
  ResultCode: Integer;
begin
  if not RegQueryStringValue(RootKey, '{#UninstallKey}', 'UninstallString', Uninstaller) then Exit;
  Uninstaller := RemoveQuotes(Uninstaller);
  Log('Installation ' + Scope + ' trouvée : ' + Uninstaller);
  if not FileExists(Uninstaller) then
  begin
    // Entrée orpheline (dossier supprimé à la main) : on nettoie juste le registre.
    RegDeleteKeyIncludingSubkeys(RootKey, '{#UninstallKey}');
    RegDeleteValue(RootKey, '{#RunKey}', 'WisperKit');
    Exit;
  end;
  if Exec(Uninstaller, '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART', '', SW_HIDE, ewWaitUntilTerminated, ResultCode)
     and (ResultCode = 0) then
  begin
    Log('Ancienne installation ' + Scope + ' supprimée.');
    SuppressibleMsgBox(CustomMessage('OtherInstallRemoved'), mbInformation, MB_OK, IDOK);
  end
  else
    SuppressibleMsgBox(FmtMessage(CustomMessage('OtherInstallFailed'), [Scope, IntToStr(ResultCode)]), mbError, MB_OK, IDOK);
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  // Installation personnelle alors qu'une copie « tous les utilisateurs » existe : on ne peut pas la
  // retirer sans droits admin, et deux copies se lanceraient. On explique plutôt que d'en créer une seconde.
  if not IsAdminInstallMode() and RegKeyExists(HKLM, '{#UninstallKey}') then
  begin
    SuppressibleMsgBox(CustomMessage('AllUsersExists'), mbError, MB_OK, IDOK);
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not StopApp() then
  begin
    Result := CustomMessage('CloseFailed');
    Exit;
  end;
  if IsAdminInstallMode() then RemoveInstall(HKCU, 'HKCU');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  Exe, Value: String;
begin
  if CurStep <> ssPostInstall then Exit;
  Exe := ExpandConstant('{app}\' + ExeName);
  if not FileExists(Exe) then
  begin
    Log('Fichier absent après copie : ' + Exe);
    SuppressibleMsgBox(FmtMessage(CustomMessage('MissingExe'), [Exe, ExpandConstant('{log}')]), mbCriticalError, MB_OK, IDOK);
    Exit;
  end;
  if WizardIsTaskSelected('startup') then
  begin
    if not RegQueryStringValue(HKCU, '{#RunKey}', 'WisperKit', Value) or (Pos(Lowercase(Exe), Lowercase(Value)) = 0) then
    begin
      Log('Valeur Run absente ou incorrecte : ' + Value);
      SuppressibleMsgBox(FmtMessage(CustomMessage('StartupFailed'), ['{#RunKey}']), mbError, MB_OK, IDOK);
    end
    else
      Log('Démarrage automatique : ' + Value);
  end
  else if RegValueExists(HKCU, '{#RunKey}', 'WisperKit') then
  begin
    // Case décochée lors d'une mise à jour : on retire le lancement automatique de la version précédente.
    RegDeleteValue(HKCU, '{#RunKey}', 'WisperKit');
    Log('Démarrage automatique retiré.');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep <> usPostUninstall then Exit;
  RegDeleteValue(HKCU, '{#RunKey}', 'WisperKit');
  if UninstallSilent() then Exit;
  if MsgBox(CustomMessage('RemoveData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
  begin
    DelTree(ExpandConstant('{localappdata}\WisperKit'), True, True, True);
    DelTree(ExpandConstant('{userappdata}\WisperKit'), True, True, True);
  end;
end;
