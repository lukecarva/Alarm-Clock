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

    private DateTimeOffset _lastTick;

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

            var policy = alarm.Profile.Snooze;

            if (!policy.IsEnabled)
            {
                refusal = $"O nível {alarm.Profile.DisplayName} não permite adiar.";
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
        }
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
                disparos.Add(new AlarmTriggeredEventArgs
                {
                    Alarm = alarme,
                    ScheduledFor = quando,
                    FiredAt = now,
                    Kind = TriggerKind.Snooze,
                });
            }
        }

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

            // Nova ocorrência: orçamento de adiamentos zerado.
            _snoozeCounts.Remove(id);

            var atraso = now - vencida;

            disparos.Add(new AlarmTriggeredEventArgs
            {
                Alarm = alarme,
                ScheduledFor = vencida,
                FiredAt = now,
                Kind = atraso <= CatchUpWindow ? TriggerKind.OnTime : TriggerKind.Missed,
                SkippedOccurrences = ocorrencias - 1,
            });
        }

        return disparos;
    }
}
