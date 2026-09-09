using AlarmClock.Core.Model;

namespace AlarmClock.Core.Scheduling;

/// <summary>Por que este alerta está aparecendo agora.</summary>
public enum TriggerKind
{
    /// <summary>Na hora (ou com atraso dentro da janela de tolerância).</summary>
    OnTime,

    /// <summary>
    /// Devia ter tocado bem antes — o PC estava dormindo, desligado ou o app
    /// não estava rodando. Quem decide o que fazer com isso é o
    /// <see cref="UrgencyProfile.WhenAway"/>.
    /// </summary>
    Missed,

    /// <summary>Voltando de um adiamento.</summary>
    Snooze,
}

public sealed class AlarmTriggeredEventArgs : EventArgs
{
    public required Alarm Alarm { get; init; }

    /// <summary>Para quando estava marcado.</summary>
    public required DateTimeOffset ScheduledFor { get; init; }

    /// <summary>Quando o agendador percebeu.</summary>
    public required DateTimeOffset FiredAt { get; init; }

    public required TriggerKind Kind { get; init; }

    /// <summary>
    /// Ocorrências anteriores que também foram perdidas e não serão mostradas
    /// uma a uma. Três dias de PC desligado com um alarme diário viram um único
    /// alerta com <c>SkippedOccurrences = 2</c>, não três alertas empilhados.
    /// </summary>
    public int SkippedOccurrences { get; init; }

    public TimeSpan Delay => FiredAt - ScheduledFor;
}
