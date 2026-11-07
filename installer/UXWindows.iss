; Installateur UX-windows (Inno Setup 6).
; Installation pour l'utilisateur courant uniquement : aucun droit administrateur demandé.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish"
#endif

[Setup]
AppId={{9CBA7A16-19AE-410D-974A-06A5F8E60A2C}
AppName=UX-windows
AppVersion={#AppVersion}
AppPublisher=Florian
AppPublisherURL=https://github.com/Snooze561/UX-windows
DefaultDirName={localappdata}\Programs\UX-windows
DefaultGroupName=UX-windows
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\out
OutputBaseFilename=UX-windows-Setup-{#AppVersion}
SetupIconFile=..\src\UXWindows\app.ico
UninstallDisplayIcon={app}\UXWindows.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "startup"; Description: "Lancer UX-windows avec Windows"; GroupDescription: "Options :"

[Files]
Source: "{#SourceDir}\UXWindows.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\UX-windows"; Filename: "{app}\UXWindows.exe"

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "UX-windows"; ValueData: """{app}\UXWindows.exe"""; Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\UXWindows.exe"; Description: "Lancer UX-windows maintenant"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/IM UXWindows.exe /F"; Flags: runhidden; RunOnceId: "StopApp"
Filename: "{sys}\reg.exe"; Parameters: "delete HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v UX-windows /f"; Flags: runhidden; RunOnceId: "RemoveStartup"

[UninstallDelete]
Type: filesandordirs; Name: "{localappdata}\UXWindows"
