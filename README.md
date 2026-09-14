# Despertador Produtivo

Despertador de bandeja para Windows, para quem passa o dia no PC. A ideia central
são os **níveis de urgência**: a urgência de um alarme define o quão intrusivo é
o alerta, de um toast silencioso a um overlay em tela cheia que exige uma ação
deliberada para ser dispensado.

O desenho completo está em **[docs/PLANO.md](docs/PLANO.md)**.

## Estado atual

**Fase 1 (MVP) concluída** — o despertador funciona de ponta a ponta: criar,
editar e excluir alarmes; agendas de uma vez / todo dia / dias da semana; os
quatro níveis de urgência com suas janelas; adiamento com limite por nível;
som com fade; catch-up de alarmes perdidos durante hibernação; e início
automático com o Windows.

**Fase 2 em andamento.** Já entraram os lembretes cíclicos ("a cada 45 min",
com faixa de horário opcional que pode atravessar a meia-noite), a detecção de
ausência (pula o alerta quando teclado e mouse estão parados, para não empilhar
avisos numa cadeira vazia) e o **escalonamento automático**: um alarme ignorado
por 10 minutos, ou adiado 2 vezes, volta um nível acima — até chegar a Crítico.
O que falta está no [plano](docs/PLANO.md#fase-2--em-andamento).

## Níveis de urgência

| Nível | Como aparece | Como sai |
|---|---|---|
| Sussurro | Toast do Windows, sem som | Sozinho, em 7s |
| Normal | Card no canto, um toque | Um clique. Adia 5/10/15 min à vontade |
| Importante | Janela central, toque a cada 10s | Um clique. Adia 5 min, até 3x |
| Crítico | Tela cheia em todos os monitores, som em loop | Segurar o botão 3s. Adia 2 min, uma vez só |

## Instalando

Gere o instalador (setup.exe enxuto — ~3 MB — por usuário, sem admin):

```bash
pwsh build/build-installer.ps1
```

Sai em `build/dist/DespertadorProdutivo-Setup-<versão>.exe`. Ele instala em
`%LOCALAPPDATA%\Programs\Despertador Produtivo`, cria atalho no Menu Iniciar e
oferece "iniciar com o Windows" e atalho na área de trabalho como opcionais. A
desinstalação preserva seus dados em `%APPDATA%\AlarmClock`.

O build é *framework-dependent*: exige o **.NET 8 Desktop Runtime (x64)** na
máquina. O instalador detecta a ausência dele e mostra o link antes de seguir.
Baixe em <https://dotnet.microsoft.com/download/dotnet/8.0/runtime> (opção
"Desktop Runtime") — ou `winget install Microsoft.DotNet.DesktopRuntime.8`.

Gerar o instalador requer o [Inno Setup 6](https://jrsoftware.org/isinfo.php)
uma única vez:

```bash
winget install JRSoftware.InnoSetup
```

## Rodando (desenvolvimento)

```bash
dotnet run --project src/AlarmClock.App
```

```bash
dotnet test
```

Fechar a janela esconde o app na bandeja; encerrar de verdade é pelo menu do
ícone (*Sair*).

## Estrutura

| Projeto | TFM | Papel |
|---|---|---|
| `src/AlarmClock.Core` | `net8.0` | Domínio e agendamento. Não conhece WPF, então roda nos testes sem UI |
| `src/AlarmClock.App` | `net8.0-windows` | WPF: bandeja, janelas, áudio, bootstrap |
| `tests/AlarmClock.Core.Tests` | `net8.0` | xUnit, com `FakeClock` no lugar do relógio real |

## Dados

Tudo em `%APPDATA%\AlarmClock`:

- `logs\log-*.txt` — 7 dias, UTF-8 com BOM, aberto em modo compartilhado (dá para
  ler com o app rodando)
- `alarms.json`, `settings.json` — a partir da Fase 1
