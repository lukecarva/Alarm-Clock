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

    /// <summary>
    /// Reapresentação num nível mais alto porque o alerta foi ignorado. O
    /// nível novo vem em <see cref="AlarmTriggeredEventArgs.EffectiveUrgency"/>.
    /// </summary>
    Escalation,
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
    /// Nível com que o alarme deve aparecer <b>agora</b> — igual ao
    /// <see cref="Model.Alarm.Urgency"/> na maioria das vezes, mas maior quando
    /// a escalada já subiu o alarme. Sempre preenchido pelo agendador; todo o
    /// resto (janela, som, adiamento) lê daqui, nunca do alarme direto.
    /// </summary>
    public required UrgencyLevel EffectiveUrgency { get; init; }

    /// <summary>Perfil correspondente ao <see cref="EffectiveUrgency"/>.</summary>
    public UrgencyProfile EffectiveProfile => UrgencyProfiles.Get(EffectiveUrgency);

    /// <summary>Som efetivo no nível atual, com o arquivo do usuário se houver.</summary>
    public SoundSpec EffectiveSound =>
        Alarm.CustomSoundPath is null
            ? EffectiveProfile.Sound
            : EffectiveProfile.Sound with { FilePath = Alarm.CustomSoundPath, IsSilent = false };

    /// <summary>
    /// Ocorrências anteriores que também foram perdidas e não serão mostradas
    /// uma a uma. Três dias de PC desligado com um alarme diário viram um único
    /// alerta com <c>SkippedOccurrences = 2</c>, não três alertas empilhados.
    /// </summary>
    public int SkippedOccurrences { get; init; }

    public TimeSpan Delay => FiredAt - ScheduledFor;
}
