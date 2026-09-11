using AlarmClock.Core.Abstractions;
using AlarmClock.Core.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlarmClock.Core.Scheduling;

public interface IAlarmScheduler
{
    event EventHandler<AlarmTriggeredEventArgs>? Triggered;

    /// <summary>Próximo disparo previsto, contando adiamentos. Nulo = nada agendado.</summary>
    DateTimeOffset? NextFireTime { get; }

    /// <summary>Substitui o conjunto de alarmes e recalcula tudo.</summary>
    void Reload(IEnumerable<Alarm> alarms);

    /// <summary>
    /// Avalia o relógio agora e dispara o que estiver vencido. Chamado uma vez
    /// por segundo pelo app — e diretamente pelos testes, com um relógio falso.
    /// </summary>
    void Tick();

    bool TrySnooze(Guid alarmId, TimeSpan delay, out string? refusal);

    /// <summary>Encerra o alerta: cancela adiamento pendente e zera o contador.</summary>
    void Dismiss(Guid alarmId);

    int SnoozeCountFor(Guid alarmId);
}

/// <summary>
/// O coração do app. Não usa timer longo nem conta deltas: compara o relógio de
/// parede a cada chamada de <see cref="Tick"/>. É o que faz o agendador
/// sobreviver a hibernação, troca de fuso e usuário mexendo no relógio — os
/// três jeitos clássicos de um despertador simplesmente não tocar.
/// </summary>
public sealed class AlarmScheduler : IAlarmScheduler
{
    /// <summary>
    /// Até este atraso, dispara como se fosse na hora. Acima disso é um alarme
    /// perdido: acordar o PC às 15h e ouvir o alarme das 7h como se fosse agora
    /// não ajuda ninguém.
    /// </summary>
    public static readonly TimeSpan DefaultCatchUpWindow = TimeSpan.FromMinutes(15);

    private readonly ISystemClock _clock;
    private readonly ILogger<AlarmScheduler> _log;
    private readonly object _gate = new();

    private readonly Dictionary<Guid, Alarm> _alarms = [];
    private readonly Dictionary<Guid, DateTimeOffset> _next = [];
    private readonly Dictionary<Guid, DateTimeOffset> _snoozes = [];
    private readonly Dictionary<Guid, int> _snoozeCounts = [];

    /// <summary>Estado da escalada de cada alarme com um alerta em curso.</summary>
    private readonly Dictionary<Guid, EscalationState> _escalations = [];

    private DateTimeOffset _lastTick;

    /// <summary>
    /// Escalada em andamento de um alarme. Vive enquanto o alerta não é
    /// dispensado; some no <see cref="Dismiss"/> ou quando o alarme dispara uma
    /// ocorrência nova (que recomeça do nível base).
    /// </summary>
    private sealed class EscalationState
    {
        public required EscalationPolicy Policy { get; init; }

        /// <summary>Nível efetivo atual — sobe a cada degrau da escalada.</summary>
        public required UrgencyLevel Level { get; set; }

        /// <summary>
        /// Quando sobe por ficar ignorado. Nulo enquanto pausado (durante um
        /// adiamento, quando o alerta não está na tela) ou no teto.
        /// </summary>
        public DateTimeOffset? IgnoreDeadline { get; set; }
    }

    public AlarmScheduler(ISystemClock clock, ILogger<AlarmScheduler>? log = null)
    {
        _clock = clock;
        _log = log ?? NullLogger<AlarmScheduler>.Instance;
        _lastTick = _clock.Now;
    }

    public TimeSpan CatchUpWindow { get; init; } = DefaultCatchUpWindow;

    public event EventHandler<AlarmTriggeredEventArgs>? Triggered;

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

            // Adiamentos e contadores de alarmes que sumiram vão junto.
            foreach (var orfao in _snoozes.Keys.Where(id => !_alarms.ContainsKey(id)).ToList())
            {
                _snoozes.Remove(orfao);
            }

            foreach (var orfao in _snoozeCounts.Keys.Where(id => !_alarms.ContainsKey(id)).ToList())
            {
                _snoozeCounts.Remove(orfao);
            }

            foreach (var orfao in _escalations.Keys.Where(id => !_alarms.ContainsKey(id)).ToList())
            {
                _escalations.Remove(orfao);
            }

            RecomputeAll(now);
        }
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

        // Fora do lock: um handler que abre janela (ou que chame Reload de
        // volta) não pode travar o agendador.
        foreach (var disparo in disparos)
        {
            Triggered?.Invoke(this, disparo);
        }
    }

    public bool TrySnooze(Guid alarmId, TimeSpan delay, out string? refusal)
    {
        lock (_gate)
        {
            if (!_alarms.TryGetValue(alarmId, out var alarm))
            {
                refusal = "Este alarme não existe mais.";
                return false;
            }

            // A política de adiamento segue o nível efetivo: se a escalada já
            // levou o alarme a Crítico, vale o limite de Crítico (1x), não o do
            // nível base.
            var nivel = _escalations.TryGetValue(alarmId, out var esc) ? esc.Level : alarm.Urgency;
            var perfil = UrgencyProfiles.Get(nivel);
            var policy = perfil.Snooze;

            if (!policy.IsEnabled)
            {
                refusal = $"O nível {perfil.DisplayName} não permite adiar.";
                return false;
            }

            var usados = _snoozeCounts.GetValueOrDefault(alarmId);
            if (usados >= policy.MaxCount)
            {
                refusal = policy.MaxCount == 1
                    ? "Você já adiou este alarme uma vez."
                    : $"Limite de {policy.MaxCount} adiamentos atingido.";

                return false;
            }

            _snoozeCounts[alarmId] = usados + 1;
            _snoozes[alarmId] = _clock.Now + delay;

            if (esc is not null)
            {
                // Adiar tira o alerta da tela: o relógio de "ignorado" pausa e
                // volta a correr quando o adiamento reapresentar o alarme.
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
    }

    public void Dismiss(Guid alarmId)
    {
        lock (_gate)
        {
            _snoozes.Remove(alarmId);
            _snoozeCounts.Remove(alarmId);
            _escalations.Remove(alarmId);
        }
    }

    /// <summary>
    /// Sobe um nível, sem passar do teto nem de <see cref="UrgencyLevel.Critical"/>.
    /// </summary>
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

    private List<AlarmTriggeredEventArgs> Collect(DateTimeOffset now)
    {
        var disparos = new List<AlarmTriggeredEventArgs>();
        var zone = _clock.LocalTimeZone;

        foreach (var (id, quando) in _snoozes.Where(kv => kv.Value <= now).ToList())
        {
            _snoozes.Remove(id);

            if (_alarms.TryGetValue(id, out var alarme))
            {
                // O alerta volta à tela: o relógio de "ignorado" recomeça a partir de agora.
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

            // Avança até voltar para o futuro, contando quantas ocorrências
            // passaram em branco. PC desligado por três dias com alarme diário
            // dá um alerta, não três.
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

            // Nova ocorrência: orçamento de adiamentos zerado e escalada
            // recomeçada do nível base.
            _snoozeCounts.Remove(id);
            ResetEscalation(id, alarme, now);

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

    /// <summary>Arma (ou limpa) o estado de escalada para uma ocorrência nova.</summary>
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

    /// <summary>
    /// Reapresenta, um nível acima, os alarmes cujo prazo de "ignorado" venceu.
    /// </summary>
    private List<AlarmTriggeredEventArgs> CollectEscalations(DateTimeOffset now)
    {
        var disparos = new List<AlarmTriggeredEventArgs>();

        var vencidos = _escalations
            .Where(kv => kv.Value.IgnoreDeadline is { } dl && dl <= now)
            .ToList();

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
                // Já no teto: para de contar, sem reapresentar de novo à toa.
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
