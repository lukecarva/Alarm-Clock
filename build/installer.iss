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
; O publish é um único .exe enxuto que usa o .NET 8 Desktop Runtime da máquina
; (a presença dele é checada em InitializeSetup, abaixo).
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

[Code]
{ O build é framework-dependent: sem o .NET 8 Desktop Runtime o app não abre.
  Checamos antes de instalar e avisamos com o link, em vez de deixar o usuário
  esbarrar num erro seco ao dar duplo-clique depois. }
function Net8DesktopInstalado(): Boolean;
var
  Base: String;
  Rec: TFindRec;
begin
  Result := False;
  Base := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App');
  if not DirExists(Base) then
    exit;

  if FindFirst(Base + '\8.*', Rec) then
  try
    repeat
      if (Rec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0 then
      begin
        Result := True;
        break;
      end;
    until not FindNext(Rec);
  finally
    FindClose(Rec);
  end;
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  if Net8DesktopInstalado() then
    exit;

  { Pergunta em vez de bloquear: se a detecção falhar num caso de borda, o
    usuário ainda consegue seguir por conta própria. }
  Result := MsgBox(
    'O Despertador precisa do .NET 8 Desktop Runtime (x64), que não foi ' +
    'encontrado nesta máquina.' + #13#10#13#10 +
    'Baixe em: https://dotnet.microsoft.com/download/dotnet/8.0/runtime ' +
    '(opção "Desktop Runtime").' + #13#10#13#10 +
    'Deseja continuar a instalação mesmo assim?',
    mbConfirmation, MB_YESNO) = IDYES;
end;
