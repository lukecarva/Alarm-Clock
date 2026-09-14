using AlarmClock.Core.Abstractions;
using AlarmClock.Core.Localization;
using AlarmClock.Core.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlarmClock.Core.Scheduling;

/// <summary>Schedules alarms and raises an event when one is due. | Agenda alarmes e emite um evento quando algum vence.</summary>
public interface IAlarmScheduler
{
    /// <summary>Raised when an alarm fires. | Emitido quando um alarme dispara.</summary>
    event EventHandler<AlarmTriggeredEventArgs>? Triggered;

    /// <summary>Raised when the snooze/escalation state changes and should be persisted. | Emitido quando o estado de adiamento/escalada muda e deve ser persistido.</summary>
    event EventHandler? StateChanged;

    /// <summary>Next expected firing, snoozes included. Null = nothing scheduled. | Próximo disparo previsto, adiamentos incluídos. Nulo = nada agendado.</summary>
    DateTimeOffset? NextFireTime { get; }

    /// <summary>Replaces the alarm set and recomputes everything. | Substitui o conjunto de alarmes e recalcula tudo.</summary>
    void Reload(IEnumerable<Alarm> alarms);

    /// <summary>Evaluates the clock now and fires whatever is due. | Avalia o relógio agora e dispara o que estiver vencido.</summary>
    void Tick();

    /// <summary>Tries to snooze an alarm; on failure returns a reason. | Tenta adiar um alarme; em caso de falha retorna o motivo.</summary>
    bool TrySnooze(Guid alarmId, TimeSpan delay, out string? refusal);

    /// <summary>Ends the alert: cancels a pending snooze and clears the counter. | Encerra o alerta: cancela adiamento pendente e zera o contador.</summary>
    void Dismiss(Guid alarmId);

    /// <summary>How many times the alarm has been snoozed. | Quantas vezes o alarme foi adiado.</summary>
    int SnoozeCountFor(Guid alarmId);

    /// <summary>Snapshots the current snooze/escalation state for persistence. | Tira um retrato do estado atual de adiamento/escalada para persistência.</summary>
    SchedulerState CaptureState();

    /// <summary>Restores a saved state; must be called after <see cref="Reload"/>. | Restaura um estado salvo; deve ser chamado após <see cref="Reload"/>.</summary>
    void RestoreState(SchedulerState state);
}

/// <summary>
/// Compares the wall clock on every <see cref="Tick"/> instead of using long
/// timers, so it survives sleep, time-zone and clock changes. | Compara o relógio de parede a cada <see cref="Tick"/> em vez de usar timers
/// longos, sobrevivendo a hibernação e a mudanças de fuso e de relógio.
/// </summary>
public sealed class AlarmScheduler : IAlarmScheduler
{
    /// <summary>Up to this lateness fires as on-time; beyond it counts as missed. | Até este atraso dispara como na hora; além disso conta como perdido.</summary>
    public static readonly TimeSpan DefaultCatchUpWindow = TimeSpan.FromMinutes(15);

    private readonly ISystemClock _clock;
    private readonly ILogger<AlarmScheduler> _log;
    private readonly object _gate = new();

    private readonly Dictionary<Guid, Alarm> _alarms = [];
    private readonly Dictionary<Guid, DateTimeOffset> _next = [];
    private readonly Dictionary<Guid, DateTimeOffset> _snoozes = [];
    private readonly Dictionary<Guid, int> _snoozeCounts = [];

    /// <summary>Escalation state per alarm with an alert in progress. | Estado da escalada por alarme com um alerta em curso.</summary>
    private readonly Dictionary<Guid, EscalationState> _escalations = [];

    private DateTimeOffset _lastTick;

    /// <summary>An alarm's in-progress escalation; lives until dismissed or the next occurrence. | A escalada em curso de um alarme; vive até ser dispensada ou a próxima ocorrência.</summary>
    private sealed class EscalationState
    {
        /// <summary>The policy driving the escalation. | A política que rege a escalada.</summary>
        public required EscalationPolicy Policy { get; init; }

        /// <summary>Current effective level; rises each step. | Nível efetivo atual; sobe a cada degrau.</summary>
        public required UrgencyLevel Level { get; set; }

        /// <summary>When it rises for being ignored. Null while paused or at ceiling. | Quando sobe por ficar ignorado. Nulo enquanto pausado ou no teto.</summary>
        public DateTimeOffset? IgnoreDeadline { get; set; }
    }

    public AlarmScheduler(ISystemClock clock, ILogger<AlarmScheduler>? log = null)
    {
        _clock = clock;
        _log = log ?? NullLogger<AlarmScheduler>.Instance;
        _lastTick = _clock.Now;
    }

    /// <summary>Lateness threshold between on-time and missed. | Limite de atraso entre "na hora" e "perdido".</summary>
    public TimeSpan CatchUpWindow { get; init; } = DefaultCatchUpWindow;

    public event EventHandler<AlarmTriggeredEventArgs>? Triggered;

    public event EventHandler? StateChanged;

    /// <summary>Set under the lock whenever persistent state changes; drained after the lock. | Marcado sob o lock sempre que o estado persistido muda; drenado após o lock.</summary>
    private bool _stateDirty;

    /// <summary>Raises <see cref="StateChanged"/> once if state changed since the last drain. | Emite <see cref="StateChanged"/> uma vez se o estado mudou desde a última drenagem.</summary>
    private void RaiseStateChangedIfDirty()
    {
        bool mudou;
        lock (_gate)
        {
            mudou = _stateDirty;
            _stateDirty = false;
        }

        if (mudou)
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public DateTimeOffset? NextFireTime
    {
        get
        {
            lock (_gate)
            {
                var candidates = _next.Values.Concat(_snoozes.Values);
                return candidates.Any() ? candidates.Min() : null;
            }
        }
    }

    public void Reload(IEnumerable<Alarm> alarms)
    {
        lock (_gate)
        {
            var now = _clock.Now;

            _alarms.Clear();
            foreach (var alarm in alarms.Where(a => a.IsEnabled))
            {
                _alarms[alarm.Id] = alarm;
            }

            // Drop snoozes, counters and escalations of alarms that are gone. | Descarta adiamentos, contadores e escaladas de alarmes que sumiram.
            foreach (var orfao in _snoozes.Keys.Where(id => !_alarms.ContainsKey(id)).ToList())
            {
                _snoozes.Remove(orfao);
                _stateDirty = true;
            }

            foreach (var orfao in _snoozeCounts.Keys.Where(id => !_alarms.ContainsKey(id)).ToList())
            {
                _snoozeCounts.Remove(orfao);
                _stateDirty = true;
            }

            foreach (var orfao in _escalations.Keys.Where(id => !_alarms.ContainsKey(id)).ToList())
            {
                _escalations.Remove(orfao);
                _stateDirty = true;
            }

            RecomputeAll(now);
        }

        RaiseStateChangedIfDirty();
    }

    public void Tick()
    {
        List<AlarmTriggeredEventArgs> disparos;

        lock (_gate)
        {
            var now = _clock.Now;

            if (now < _lastTick)
            {
                _log.LogWarning(
                    "Relógio andou para trás ({Anterior:o} -> {Agora:o}). Recalculando todas as ocorrências.",
                    _lastTick,
                    now);

                RecomputeAll(now);
            }

            disparos = Collect(now);
            _lastTick = now;
        }

        // Raise outside the lock: a handler may open a window or call Reload. | Emite fora do lock: um handler pode abrir janela ou chamar Reload.
        foreach (var disparo in disparos)
        {
            Triggered?.Invoke(this, disparo);
        }

        RaiseStateChangedIfDirty();
    }

    public bool TrySnooze(Guid alarmId, TimeSpan delay, out string? refusal)
    {
        bool sucesso;

        lock (_gate)
        {
            sucesso = TrySnoozeLocked(alarmId, delay, out refusal);
        }

        // Persist outside the lock so the handler's disk write doesn't block ticks. | Persiste fora do lock para a gravação em disco do handler não travar os tiques.
        if (sucesso)
        {
            RaiseStateChangedIfDirty();
        }

        return sucesso;
    }

    /// <summary>Snooze logic, run under the lock. | Lógica do adiamento, executada sob o lock.</summary>
    private bool TrySnoozeLocked(Guid alarmId, TimeSpan delay, out string? refusal)
    {
        if (!_alarms.TryGetValue(alarmId, out var alarm))
        {
            refusal = Loc.Get("Snooze_NotFound");
            return false;
        }

        // Snooze policy follows the effective (possibly escalated) level. | A política de adiamento segue o nível efetivo (possivelmente escalado).
        var nivel = _escalations.TryGetValue(alarmId, out var esc) ? esc.Level : alarm.Urgency;
        var perfil = UrgencyProfiles.Get(nivel);
        var policy = perfil.Snooze;

        if (!policy.IsEnabled)
        {
            refusal = Loc.Format("Snooze_NotAllowed", Loc.UrgencyName(nivel));
            return false;
        }

        var usados = _snoozeCounts.GetValueOrDefault(alarmId);
        if (usados >= policy.MaxCount)
        {
            refusal = policy.MaxCount == 1
                ? Loc.Get("Snooze_Once")
                : Loc.Format("Snooze_Max", policy.MaxCount);

            return false;
        }

        _snoozeCounts[alarmId] = usados + 1;
        _snoozes[alarmId] = _clock.Now + delay;
        _stateDirty = true;

        if (esc is not null)
        {
            // Snoozing hides the alert, so the "ignored" clock pauses. | Adiar tira o alerta da tela, então o relógio de "ignorado" pausa.
            esc.IgnoreDeadline = null;

            if (esc.Policy.AfterSnoozes is { } max && usados + 1 >= max)
            {
                var subido = Bump(esc.Level, esc.Policy.Ceiling);
                if (subido != esc.Level)
                {
                    _log.LogInformation(
                        "Alarme {Titulo} subiu para {Nivel} por adiamento.",
                        alarm.Title,
                        subido);
                }

                esc.Level = subido;
            }
        }

        _log.LogInformation(
            "Alarme {Titulo} adiado por {Minutos} min ({Usados}/{Max}).",
            alarm.Title,
            delay.TotalMinutes,
            usados + 1,
            policy.MaxCount);

        refusal = null;
        return true;
    }

    public void Dismiss(Guid alarmId)
    {
        lock (_gate)
        {
            var removeu = _snoozes.Remove(alarmId);
            removeu |= _snoozeCounts.Remove(alarmId);
            removeu |= _escalations.Remove(alarmId);

            if (removeu)
            {
                _stateDirty = true;
            }
        }

        RaiseStateChangedIfDirty();
    }

    /// <summary>Rises one level, capped at the ceiling and Critical. | Sobe um nível, limitado pelo teto e por Crítico.</summary>
    private static UrgencyLevel Bump(UrgencyLevel level, UrgencyLevel ceiling)
    {
        var proximo = (UrgencyLevel)((int)level + 1);

        if (proximo > ceiling)
        {
            proximo = ceiling;
        }

        return proximo > UrgencyLevel.Critical ? UrgencyLevel.Critical : proximo;
    }

    public int SnoozeCountFor(Guid alarmId)
    {
        lock (_gate)
        {
            return _snoozeCounts.GetValueOrDefault(alarmId);
        }
    }

    public SchedulerState CaptureState()
    {
        lock (_gate)
        {
            var ids = _snoozeCounts.Keys.Union(_snoozes.Keys);

            var snoozes = ids
                .Select(id => new SnoozeState(
                    id,
                    _snoozeCounts.GetValueOrDefault(id),
                    _snoozes.TryGetValue(id, out var quando) ? quando : null))
                .ToList();

            var escalations = _escalations
                .Select(kv => new EscalationSnapshot(kv.Key, kv.Value.Level, kv.Value.IgnoreDeadline))
                .ToList();

            return new SchedulerState { Snoozes = snoozes, Escalations = escalations };
        }
    }

    public void RestoreState(SchedulerState state)
    {
        lock (_gate)
        {
            foreach (var s in state.Snoozes)
            {
                // Skip state for alarms that no longer exist. | Ignora o estado de alarmes que não existem mais.
                if (!_alarms.ContainsKey(s.AlarmId))
                {
                    continue;
                }

                if (s.Count > 0)
                {
                    _snoozeCounts[s.AlarmId] = s.Count;
                }

                if (s.DueAt is { } quando)
                {
                    _snoozes[s.AlarmId] = quando;
                }
            }

            foreach (var e in state.Escalations)
            {
                // Re-attach the policy from the alarm; drop it if the alarm or its policy is gone. | Reanexa a política a partir do alarme; descarta se o alarme ou a política sumiu.
                if (!_alarms.TryGetValue(e.AlarmId, out var alarme) || alarme.Escalation is not { } pol)
                {
                    continue;
                }

                _escalations[e.AlarmId] = new EscalationState
                {
                    Policy = pol,
                    Level = e.Level,
                    IgnoreDeadline = e.IgnoreDeadline,
                };
            }
        }
    }

    /// <summary>Recomputes the next occurrence of every alarm. | Recalcula a próxima ocorrência de cada alarme.</summary>
    private void RecomputeAll(DateTimeOffset now)
    {
        var zone = _clock.LocalTimeZone;

        _next.Clear();

        foreach (var alarm in _alarms.Values)
        {
            var proxima = alarm.Schedule.NextOccurrenceAfter(now, zone);
            if (proxima is not null)
            {
                _next[alarm.Id] = proxima.Value;
            }
        }
    }

    /// <summary>Collects everything due now: snoozes, escalations and occurrences. | Coleta tudo que vence agora: adiamentos, escaladas e ocorrências.</summary>
    private List<AlarmTriggeredEventArgs> Collect(DateTimeOffset now)
    {
        var disparos = new List<AlarmTriggeredEventArgs>();
        var zone = _clock.LocalTimeZone;

        foreach (var (id, quando) in _snoozes.Where(kv => kv.Value <= now).ToList())
        {
            _snoozes.Remove(id);
            _stateDirty = true;

            if (_alarms.TryGetValue(id, out var alarme))
            {
                // Alert is back on screen: the "ignored" clock restarts from now. | O alerta volta à tela: o relógio de "ignorado" recomeça a partir de agora.
                var nivel = alarme.Urgency;
                if (_escalations.TryGetValue(id, out var esc))
                {
                    nivel = esc.Level;
                    esc.IgnoreDeadline = esc.Policy.AfterIgnoredFor is { } d && esc.Level < esc.Policy.Ceiling
                        ? now + d
                        : null;
                }

                disparos.Add(new AlarmTriggeredEventArgs
                {
                    Alarm = alarme,
                    ScheduledFor = quando,
                    FiredAt = now,
                    Kind = TriggerKind.Snooze,
                    EffectiveUrgency = nivel,
                });
            }
        }

        disparos.AddRange(CollectEscalations(now));

        foreach (var (id, _) in _next.Where(kv => kv.Value <= now).ToList())
        {
            var alarme = _alarms[id];

            // Advance to the future, counting how many occurrences were skipped. | Avança até o futuro, contando quantas ocorrências foram puladas.
            var vencida = _next[id];
            var ocorrencias = 0;
            DateTimeOffset? cursor = vencida;

            while (cursor is not null && cursor.Value <= now)
            {
                vencida = cursor.Value;
                ocorrencias++;
                cursor = alarme.Schedule.NextOccurrenceAfter(cursor.Value, zone);

                if (ocorrencias > 100_000)
                {
                    _log.LogError("Agenda de {Titulo} não avança. Desagendando.", alarme.Title);
                    cursor = null;
                }
            }

            if (cursor is null)
            {
                _next.Remove(id);
            }
            else
            {
                _next[id] = cursor.Value;
            }

            // New occurrence: reset the snooze budget and restart escalation. | Nova ocorrência: zera o orçamento de adiamentos e reinicia a escalada.
            _snoozeCounts.Remove(id);
            ResetEscalation(id, alarme, now);
            _stateDirty = true;

            var atraso = now - vencida;

            disparos.Add(new AlarmTriggeredEventArgs
            {
                Alarm = alarme,
                ScheduledFor = vencida,
                FiredAt = now,
                Kind = atraso <= CatchUpWindow ? TriggerKind.OnTime : TriggerKind.Missed,
                SkippedOccurrences = ocorrencias - 1,
                EffectiveUrgency = alarme.Urgency,
            });
        }

        return disparos;
    }

    /// <summary>Arms (or clears) the escalation state for a new occurrence. | Arma (ou limpa) o estado de escalada para uma ocorrência nova.</summary>
    private void ResetEscalation(Guid id, Alarm alarme, DateTimeOffset now)
    {
        if (alarme.Escalation is not { } pol)
        {
            _escalations.Remove(id);
            return;
        }

        _escalations[id] = new EscalationState
        {
            Policy = pol,
            Level = alarme.Urgency,
            IgnoreDeadline = pol.AfterIgnoredFor is { } d && alarme.Urgency < pol.Ceiling
                ? now + d
                : null,
        };
    }

    /// <summary>Re-shows, one level higher, alarms whose "ignored" deadline passed. | Reapresenta, um nível acima, os alarmes cujo prazo de "ignorado" venceu.</summary>
    private List<AlarmTriggeredEventArgs> CollectEscalations(DateTimeOffset now)
    {
        var disparos = new List<AlarmTriggeredEventArgs>();

        var vencidos = _escalations
            .Where(kv => kv.Value.IgnoreDeadline is { } dl && dl <= now)
            .ToList();

        // Every branch below mutates escalation state, so persist afterwards. | Todo ramo abaixo altera o estado de escalada, então persiste depois.
        if (vencidos.Count > 0)
        {
            _stateDirty = true;
        }

        foreach (var (id, esc) in vencidos)
        {
            if (!_alarms.TryGetValue(id, out var alarme))
            {
                _escalations.Remove(id);
                continue;
            }

            var subido = Bump(esc.Level, esc.Policy.Ceiling);

            if (subido == esc.Level)
            {
                // Already at ceiling: stop counting. | Já no teto: para de contar.
                esc.IgnoreDeadline = null;
                continue;
            }

            esc.Level = subido;
            esc.IgnoreDeadline = esc.Policy.AfterIgnoredFor is { } d && subido < esc.Policy.Ceiling
                ? now + d
                : null;

            _log.LogInformation("Alarme {Titulo} subiu para {Nivel} por ficar ignorado.", alarme.Title, subido);

            disparos.Add(new AlarmTriggeredEventArgs
            {
                Alarm = alarme,
                ScheduledFor = now,
                FiredAt = now,
                Kind = TriggerKind.Escalation,
                EffectiveUrgency = subido,
            });
        }

        return disparos;
    }
}
