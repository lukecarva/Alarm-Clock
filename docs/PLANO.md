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
    // O fuso é parâmetro, e não estado, porque agendas recorrentes guardam hora
    // de parede: "todo dia às 7h" continua às 7h depois da virada do horário de
    // verão. Sem ele, a resolução de DST não teria como ser pura nem testável.
    DateTimeOffset? NextOccurrenceAfter(DateTimeOffset from, TimeZoneInfo zone);
    string Describe();
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

### Fase 1 — MVP ✅ concluída

1. ✅ Domínio: `Alarm`, `ISchedule` (once/daily/weekly), `UrgencyProfile`
2. ✅ `AlarmScheduler` + `ISystemClock` + testes (incluindo DST e virada de dia)
3. ✅ `JsonAlarmStore` com escrita atômica
4. ✅ CRUD de alarmes na UI + bandeja + single instance
5. ✅ As 4 janelas de alerta + áudio com fade
6. ✅ Snooze com política por nível
7. ✅ Resiliência a sleep e mudança de hora + catch-up de alarmes perdidos
8. ✅ Autostart via `HKCU\...\CurrentVersion\Run` (não precisa de admin)

Decisões tomadas durante a execução:

- **`Resume` chama `Tick`, `TimeChanged` chama `Reload`.** Acordar da
  hibernação é exatamente quando o catch-up precisa rodar; já mexer no relógio
  é ato deliberado do usuário, e disparar em rajada tudo que "venceu" com a
  conta nova seria pior que perder as ocorrências.
- **Alarme perdido é rebaixado, não silenciado.** `ShowOnReturn` vira card no
  canto sem som: um alarme de três horas atrás não merece tela cheia com
  sirene, mas você precisa saber que ele existiu.
- **O toque é sintetizado** (`AlarmToneProvider`), não um `.wav` embutido —
  sem áudio de terceiros no repositório, e o intervalo entre repetições vira
  parâmetro do perfil. Som do usuário continua suportado por caminho de arquivo.
- **Janelas de alerta são posicionadas em pixels físicos** via `SetWindowPos`.
  WPF trabalha em DIPs e o `Screen` do WinForms devolve pixels; converter entre
  os dois em multi-monitor com escalas diferentes é fonte permanente de janela
  fora do lugar.
- **O card do canto usa `SizeToContent`.** Com altura fixa, os botões de adiar
  ficavam cortados quando o alarme tinha mensagem — foi o que a verificação
  pegou.
- **Enums no JSON como texto** (`"Critical"`, não `3`): o arquivo só é editável
  à mão se der para entender o que está escrito nele.

**Critério de pronto:** criar um alarme Crítico, colocar o PC para dormir,
acordar depois da hora e o alarme disparar com o resumo do atraso.

### Fase 2 — em andamento

- ✅ **Lembretes cíclicos** (`IntervalSchedule`): "a cada 45 min", com faixa de
  horário opcional que pode atravessar a meia-noite
- ✅ **Detecção de ociosidade** (`GetLastInputInfo`): não alertar cadeira vazia
- ✅ **Escalonamento automático** de urgência (`EscalationPolicy`): 10 min
  ignorado ou 2 adiamentos sobem o alarme um nível, até o teto
- ⬜ **"Você está há 3h sem levantar"** — o outro lado da detecção de presença
- ⬜ **Pomodoro / blocos de foco** com contagem regressiva
- ⬜ **Estatísticas**: tempo no PC, alarmes atendidos vs. adiados vs. ignorados
- ⬜ **Acordar o PC** para o alarme (`SetWaitableTimer` com wake, ou uma tarefa
  no Agendador do Windows com *Wake the computer*)
- ⬜ Modo Não Perturbe com janelas de horário; importar/exportar

Decisões da parte já feita:

- **Intervalo é duração absoluta; a faixa de horário é hora de parede.** "A cada
  45 minutos" são 45 minutos reais e o ciclo atravessa o horário de verão sem se
  deslocar; já "só entre 9h e 18h" fala do relógio. São os dois regimes de tempo
  do projeto convivendo na mesma agenda, de propósito.
- **A âncora do ciclo é persistida.** Sem ela, fechar e abrir o app reiniciaria a
  contagem, e o lembrete das 9h45 viraria "45 minutos depois de cada boot".
  Mexer no intervalo reinicia a contagem; mexer no título, não.
- **O pulo por ausência vale só para o disparo na hora.** Alarme perdido já tem
  a política do `WhenAway`, e adiamento foi você que pediu — descartá-lo por
  ausência jogaria fora algo explicitamente adiado.
- **Propriedades calculadas ganharam `[JsonIgnore]`.** Sem isso cada alarme
  gravava uma cópia inteira do perfil de urgência no arquivo — 45 linhas de JSON
  onde bastam 13, desnormalizando justamente o que o desenho mantém num lugar
  só. O defeito vinha da Fase 1 e só apareceu aqui, na primeira vez que o app
  **escreveu** o arquivo em vez de só lê-lo.

Decisões do escalonamento:

- **O nível efetivo viaja no gatilho, não no alarme.** `AlarmTriggeredEventArgs`
  ganhou `EffectiveUrgency`, e janela, som e política de adiamento leem dali. O
  alarme salvo não muda; o que muda é como aquela ocorrência aparece agora.
  Escalar até Crítico já traz junto o snooze de Crítico (1x), sem código extra.
- **Auto-dismiss não é "agir".** Fechar por tempo virou `TimeoutClose`, separado
  do `Dismiss` do usuário — senão o nível Normal, que some em 30s, jamais
  escalaria. Só dispensar de fato ou o teto encerram a escalada.
- **Adiar pausa o relógio de ignorado.** Enquanto adiado o alerta não está na
  tela, então não conta como ignorado; o relógio recomeça quando o adiamento
  reapresenta o alarme. Os dois gatilhos ("ignorado" e "adiado") continuam
  independentes, como o plano pedia.
- **Cada ocorrência recomeça do nível base.** O estado de escalada é do alerta
  em curso, não do alarme — o de amanhã começa em Normal de novo.

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
