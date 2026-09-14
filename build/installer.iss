; Instalador do Despertador Produtivo — Inno Setup.
; Instalação por usuário (sem admin): cai em %LOCALAPPDATA%\Programs.
; Compile com: iscc build\installer.iss  (a partir da raiz do repositório)

#define AppName "Despertador Produtivo"
#define AppVersion "0.1.0"
#define AppPublisher "Lucas"
#define AppExe "DespertadorProdutivo.exe"

[Setup]
; AppId identifica o produto entre versões — nunca mudar depois de publicado,
; senão uma atualização instala lado a lado em vez de por cima.
AppId={{7B3C9E1A-4D52-4C8B-9E2F-1A6D5F0B7C34}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
VersionInfoVersion={#AppVersion}

; Sem admin: instala só para o usuário atual, em %LOCALAPPDATA%\Programs.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto

; Saída
OutputDir=dist
OutputBaseFilename=DespertadorProdutivo-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes

; Aparência e identidade
WizardStyle=modern
SetupIconFile=..\src\AlarmClock.App\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Tasks]
Name: "desktopicon"; Description: "Criar um atalho na área de trabalho"; Flags: unchecked
Name: "startup"; Description: "Iniciar o Despertador junto com o Windows"; Flags: unchecked

[Files]
; O publish é um único .exe self-contained (traz o .NET junto).
Source: "publish\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Início automático opcional. Mesmo nome e formato que o app usa no próprio
; botão "Iniciar com o Windows", então os dois não brigam — e sai na desinstalação.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "DespertadorProdutivo"; \
    ValueData: """{app}\{#AppExe}"" --minimized"; \
    Tasks: startup; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir o Despertador Produtivo agora"; \
    Flags: nowait postinstall skipifsilent

[UninstallRun]
; Fecha o app antes de remover, senão o .exe fica travado.
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#AppExe} /F"; Flags: runhidden; RunOnceId: "FecharApp"

[UninstallDelete]
; Remove a pasta do produto (o exe já sai sozinho); dados do usuário em
; %APPDATA%\AlarmClock são preservados de propósito.
Type: dirifempty; Name: "{app}"
