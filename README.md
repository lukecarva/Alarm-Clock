# Despertador Produtivo

Despertador de bandeja para Windows, para quem passa o dia no PC. A ideia central
são os **níveis de urgência**: a urgência de um alarme define o quão intrusivo é
o alerta, de um toast silencioso a um overlay em tela cheia que exige uma ação
deliberada para ser dispensado.

O desenho completo está em **[docs/PLANO.md](docs/PLANO.md)**.

## Estado atual

**Fase 0 (esqueleto) concluída.** O app sobe, fica na bandeja e registra log em
arquivo. O agendador, os alarmes e as janelas de alerta são a Fase 1.

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
