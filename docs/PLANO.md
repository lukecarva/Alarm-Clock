# Despertador Produtivo — Plano de Projeto

App de desktop **Windows** para quem passa muitas horas no PC: alarmes com
**níveis de urgência**, onde a urgência define *o quão intrusivo* o alerta é —
de um toast silencioso até um overlay em tela cheia que você não consegue ignorar.

- **Stack:** C# / .NET 8 / WPF (MVVM)
- **Uso:** pessoal, máquina única, sem servidor, sem conta, dados locais
- **Ambiente confirmado:** .NET SDK 8.0.423 + Microsoft.WindowsDesktop.App 8.0.29 (nada extra a instalar)

---

## 1. Princípio central: urgência é um *perfil*, não um `if`

O erro comum é espalhar `if (urgencia == Alta)` pelo código de UI e de som. Aqui,
cada nível é um **`UrgencyProfile`**: um conjunto de parâmetros que descreve
apresentação, som, persistência e forma de dispensar. Os 4 níveis abaixo são
apenas *presets* desse record — dá para editar cada um, ou criar o seu.

| | 🔵 **Sussurro** (`Whisper`) | 🟢 **Normal** (`Normal`) | 🟠 **Importante** (`High`) | 🔴 **Crítico** (`Critical`) |
|---|---|---|---|---|
| **Apresentação** | Toast nativo do Windows | Card no canto da tela (janela própria, topmost) | Janela central, topmost, rouba foco | Overlay fullscreen em **todos** os monitores |
| **Som** | Nenhum | 1 toque curto | Toque repetido a cada 10s | Loop contínuo com fade-in crescente |
| **Duração** | Some em ~7s | Some em 30s | Fica até você agir | Fica até você agir |
| **Dispensar** | Automático | 1 clique | 1 clique em "Ok, entendi" | **Ação deliberada**: segurar botão 3s / digitar palavra-chave |
| **Snooze** | — | 5/10/15 min, ilimitado | 5 min, máx. 3x | Máx. 1x de 2 min (ou bloqueado por config) |
| **Se você estiver ausente** | Descarta | Descarta ou guarda | Guarda e mostra ao voltar | Guarda e dispara ao voltar |
| **Uso típico** | "beber água" | "reunião em 15 min" | "reunião AGORA" | "sair de casa AGORA" / remédio |

### Escalonamento (mata-ignorância)

Cada alarme pode ter uma `EscalationPolicy`: **sobe de nível quando é ignorado**.

```
Normal → (2 snoozes ou 10 min ignorado) → High → (mais 5 min) → Critical
```

É isso que transforma o app de "despertador" em "otimizador de tempo": ele não
aceita ser dispensado no automático quando o compromisso importa.

> ⚠️ **Limite deliberado:** "bloquear" no nível Crítico é *social*, não técnico —
> a janela cobre a tela, fica topmost e se re-foca. O app **não** instala hook
> global de teclado, **não** bloqueia Ctrl+Alt+Del e **não** impede Alt+F4 nem o
> gerenciador de tarefas. Isso é comportamento de malware, quebra em atualização
> do Windows e te tranca fora da tua própria máquina.

---

## 2. Arquitetura

Três projetos. A regra é: **o domínio não sabe que WPF existe** — assim o
agendador é testável sem abrir janela nenhuma.

```
AlarmClock.sln
├── src/
│   ├── AlarmClock.Core/          net8.0          (sem UI, testável)
│   │   ├── Model/                Alarm, UrgencyProfile, EscalationPolicy
│   │   ├── Scheduling/           ISchedule + implementações, AlarmScheduler
│   │   ├── Persistence/          IAlarmStore, JsonAlarmStore
│   │   └── Abstractions/         ISystemClock, IAlertPresenter, IIdleDetector
│   └── AlarmClock.App/           net8.0-windows  (WPF)
│       ├── Views/                MainWindow, ToastWindow, ModalAlertWindow, FullscreenOverlay
│       ├── ViewModels/           MainViewModel, AlarmEditorViewModel, AlertViewModel
│       ├── Services/             WpfAlertPresenter, AudioPlayer, TrayIcon, StartupRegistrar
│       └── App.xaml.cs           bootstrap: DI + host + single-instance
└── tests/
    └── AlarmClock.Core.Tests/    xUnit + FakeClock
```

### Dependências (NuGet, todas leves e estáveis)

| Pacote | Para quê |
|---|---|
| `CommunityToolkit.Mvvm` | `[ObservableProperty]` / `[RelayCommand]` via source generator — MVVM sem boilerplate |
| `Microsoft.Extensions.Hosting` | DI + ciclo de vida + configuração |
| `Serilog.Sinks.File` | Log em arquivo. **Não é opcional**: quando um alarme não disparar, o log é a única forma de descobrir por quê |
| `H.NotifyIcon.Wpf` | Ícone na bandeja com menu de contexto |
| `NAudio` | Áudio com controle próprio de volume e fade, independente do que o app está tocando |

`System.Text.Json` já vem no runtime — zero dependência de serialização.

---

## 3. Modelo de domínio

```csharp
public sealed record Alarm
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }        // "Reunião com o time"
    public string? Message { get; init; }              // texto extra no alerta
    public required ISchedule Schedule { get; init; }
    public required UrgencyLevel Urgency { get; init; }
    public EscalationPolicy? Escalation { get; init; }
    public string? CustomSoundPath { get; init; }
    public bool IsEnabled { get; init; } = true;
}

public enum UrgencyLevel { Whisper, Normal, High, Critical }

public sealed record UrgencyProfile
{
    public required PresentationMode Presentation { get; init; } // Toast|Corner|Modal|Fullscreen
    public required SoundSpec Sound { get; init; }               // arquivo, loop, fade, volume
    public TimeSpan? AutoDismissAfter { get; init; }             // null = exige ação
    public required DismissMode Dismiss { get; init; }           // Click|HoldButton|TypePhrase
    public required SnoozePolicy Snooze { get; init; }           // durações + máx. repetições
    public required MissedAlarmBehavior WhenAway { get; init; }  // Discard|ShowOnReturn|FireOnReturn
}
```

### Agendamento — polimórfico e serializável

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(OneTimeSchedule), "once")]
[JsonDerivedType(typeof(DailySchedule),   "daily")]
[JsonDerivedType(typeof(WeeklySchedule),  "weekly")]
public interface ISchedule
{
    DateTimeOffset? NextOccurrenceAfter(DateTimeOffset from);
}
```

Uma única função pura por tipo de agenda. Tudo que o scheduler faz é perguntar
"qual a próxima?" — o que torna cada regra trivialmente testável.

---

## 4. O agendador (a parte que realmente pode dar errado)

Um despertador que "quase sempre" toca é inútil. Estas são as armadilhas reais,
e a decisão para cada uma:

| Armadilha | O que acontece na prática | Decisão |
|---|---|---|
| **Timer longo** (`Timer(6h)`) | Não dispara depois de sleep/hibernação — o app acorda atrasado e você perdeu a hora | Timer de **1 segundo** comparando **relógio de parede** (`DateTimeOffset.Now`), nunca acumulando deltas. O custo de CPU é irrelevante |
| **PC dormiu ou desligou** | Alarme das 14h com o PC dormindo até 15h | `SystemEvents.PowerModeChanged` (`Resume`) → reavalia tudo. Política de *catch-up*: atraso < 15 min dispara normal; maior que isso vira "alarme perdido" no resumo |
| **Relógio ou fuso mudou** | A fila de próximas ocorrências vira lixo | `SystemEvents.TimeChanged` → recalcula a fila inteira |
| **Horário de verão** | Alarme diário das 7h "anda" 1h ou dispara duas vezes | Recorrentes guardam **hora local de parede** (`TimeOnly` + regra), nunca UTC. One-time guarda `DateTimeOffset` absoluto |
| **Focus Assist / Não Perturbe** | O Windows engole os toasts silenciosamente | Só o nível `Whisper` usa toast nativo. Do `Normal` para cima é janela própria, que o Focus Assist não toca |
| **`SetForegroundWindow` bloqueado** | O Windows recusa roubo de foco vindo de app em background; a janela só pisca na barra | `Topmost = true` + `Activate()` + `Show()`, sem brigar com a API. Para o caso Crítico, o fullscreen resolve |
| **Duas instâncias abertas** | Alarme dispara duplicado e o arquivo corrompe | `Mutex` nomeado no boot; a 2ª instância manda "abre a janela" via named pipe e encerra |
| **Crash na hora de salvar** | `alarms.json` truncado = todos os alarmes perdidos | Escrita atômica: grava `.tmp` → `File.Replace` com backup |

**Interface:**

```csharp
public interface IAlarmScheduler
{
    event EventHandler<AlarmTriggeredEventArgs> Triggered;
    void Reload(IEnumerable<Alarm> alarms);   // re-hidrata a fila de prioridade
    DateTimeOffset? NextFireTime { get; }     // alimenta o tooltip da bandeja
}
```

Todo acesso a tempo passa por **`ISystemClock`** injetado. Nos testes usa-se um
`FakeClock`, e um mês inteiro de alarmes é verificado em milissegundos.

---

## 5. Persistência

`%APPDATA%\AlarmClock\`

- `alarms.json` — os alarmes
- `settings.json` — perfis de urgência customizados, autostart, volume, DND
- `log-YYYYMMDD.txt` — Serilog (rolling, 7 dias)

JSON legível e editável à mão, escrita atômica. **Nada de SQLite** — para uso
pessoal com dezenas de alarmes é peso morto e migração desnecessária.

---

## 6. UI

**Janela principal** (só abre quando chamada pela bandeja): lista de alarmes com
toggle on/off, próxima ocorrência em texto ("amanhã, 07:00"), badge colorido do
nível de urgência, editor lateral.

**Bandeja** é o modo de operação normal: fechar a janela minimiza para lá.
O tooltip mostra "Próximo: Reunião em 42 min". Menu: *Abrir · Silenciar por 1h ·
Novo alarme rápido · Sair*.

**Janelas de alerta** — `IAlertPresenter` recebe um `Alarm` + o `UrgencyProfile`
e escolhe a janela. O overlay fullscreen instancia **uma janela por monitor**
(`Screen.AllScreens`), todas topmost, com o botão de dispensar só na primária.

---

## 7. Roadmap

### Fase 0 — Esqueleto ✅ concluída

Solution, 3 projetos, DI + Serilog, ícone na bandeja funcionando, janela vazia,
CI básico (`dotnet build` + `dotnet test`).

Decisões tomadas durante a execução, que valem para o resto do projeto:

- **`H.NotifyIcon.Wpf` fixado em 2.3.2.** A 2.4.1 só publica `net10.0-windows` e
  `net462`; num projeto `net8.0-windows` ela cai no fallback do .NET Framework.
  A 2.3.2 tem `net8.0-windows7.0` de verdade.
- **`ForceCreate(enablesEfficiencyMode: false)`** no ícone da bandeja. O modo de
  eficiência (EcoQoS) autoriza o Windows a estrangular os timers do processo —
  inaceitável num despertador.
- **`WpfHostLifetime`** substitui o `ConsoleLifetime` do host genérico, que num
  app de bandeja só polui o log e tenta encerrar o processo por conta própria.
- **Log em UTF-8 com BOM e `shared: true`** — sem BOM os acentos viram lixo no
  Bloco de Notas, e sem `shared` o arquivo fica travado justo enquanto o app roda.
- **Projetos WPF não trazem `System.IO` nos implicit usings** (para não colidir
  `System.IO.Path` com `System.Windows.Shapes.Path`); precisa de `using` explícito.

### Fase 1 — MVP ✅ *este é o entregável*

1. Domínio: `Alarm`, `ISchedule` (once/daily/weekly), `UrgencyProfile`
2. `AlarmScheduler` + `ISystemClock` + testes (incluindo DST e virada de dia)
3. `JsonAlarmStore` com escrita atômica
4. CRUD de alarmes na UI + bandeja + single instance
5. As 4 janelas de alerta + áudio com fade
6. Snooze com política por nível
7. Resiliência a sleep e mudança de hora + catch-up de alarmes perdidos
8. Autostart via `HKCU\...\CurrentVersion\Run` (não precisa de admin)

**Critério de pronto:** criar um alarme Crítico, colocar o PC para dormir,
acordar depois da hora e o alarme disparar com o resumo do atraso.

### Fase 2 — Backlog (o que ficou de fora do MVP por escolha)

- **Escalonamento automático** de urgência
- **Lembretes cíclicos de saúde**: água a cada 45min, alongar a cada 1h, 20-20-20 ocular
- **Pomodoro / blocos de foco** com contagem regressiva
- **Detecção de ociosidade** (`GetLastInputInfo` via P/Invoke): não alarmar quando você não está, e alertar quando você está há 3h sem levantar
- **Estatísticas**: tempo no PC, alarmes atendidos vs. adiados vs. ignorados
- **Acordar o PC** para o alarme (`SetWaitableTimer` com wake, ou uma tarefa no Agendador do Windows com *Wake the computer*)
- Modo Não Perturbe com janelas de horário; sons customizados; importar/exportar

---

## 8. Riscos principais

1. **Alarme que não toca** é falha total do produto → por isso log em arquivo e
   testes do scheduler são Fase 1, não "depois".
2. **Fadiga de alerta**: se tudo virar Crítico, você aprende a dispensar no
   automático e o app morre. Mitigação: o editor sugere `Normal` por padrão, e a
   tela de estatísticas (Fase 2) mostra a taxa de ignorados por nível.
3. **Escopo**: Pomodoro, saúde e estatísticas são três apps diferentes
   disfarçados. Ficam na Fase 2 de propósito — o MVP precisa ser um despertador
   que funciona.
