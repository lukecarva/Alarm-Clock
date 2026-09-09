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

A Fase 2 (escalonamento automático, lembretes cíclicos, Pomodoro, detecção de
ociosidade, estatísticas) está no [plano](docs/PLANO.md#fase-2--backlog-o-que-ficou-de-fora-do-mvp-por-escolha).

## Níveis de urgência

| Nível | Como aparece | Como sai |
|---|---|---|
| Sussurro | Toast do Windows, sem som | Sozinho, em 7s |
| Normal | Card no canto, um toque | Um clique. Adia 5/10/15 min à vontade |
| Importante | Janela central, toque a cada 10s | Um clique. Adia 5 min, até 3x |
| Crítico | Tela cheia em todos os monitores, som em loop | Segurar o botão 3s. Adia 2 min, uma vez só |

## Rodando

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
