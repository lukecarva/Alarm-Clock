using AlarmClock.Core.Model;

namespace AlarmClock.Core.Scheduling;

/// <summary>Why an alert is showing now. | Por que um alerta está aparecendo agora.</summary>
public enum TriggerKind
{
    /// <summary>On time, or within the catch-up window. | Na hora, ou dentro da janela de tolerância.</summary>
    OnTime,

    /// <summary>Should have fired earlier (PC asleep, off, or app not running). | Deveria ter tocado antes (PC dormindo, desligado ou app fora do ar).</summary>
    Missed,

    /// <summary>Returning from a snooze. | Voltando de um adiamento.</summary>
    Snooze,

    /// <summary>Re-shown one level higher because it was ignored. | Reapresentado um nível acima por ter sido ignorado.</summary>
    Escalation,
}

/// <summary>Details of an alarm that just fired. | Dados de um alarme que acabou de disparar.</summary>
public sealed class AlarmTriggeredEventArgs : EventArgs
{
    /// <summary>The alarm that fired. | O alarme que disparou.</summary>
    public required Alarm Alarm { get; init; }

    /// <summary>When it was scheduled for. | Para quando estava marcado.</summary>
    public required DateTimeOffset ScheduledFor { get; init; }

    /// <summary>When the scheduler noticed. | Quando o agendador percebeu.</summary>
    public required DateTimeOffset FiredAt { get; init; }

    /// <summary>Why it is showing. | Por que está aparecendo.</summary>
    public required TriggerKind Kind { get; init; }

    /// <summary>
    /// Level to show now — the alarm's own, or higher after escalation. | Nível para mostrar agora — o do alarme, ou maior após a escalada.
    /// </summary>
    public required UrgencyLevel EffectiveUrgency { get; init; }

    /// <summary>Profile for <see cref="EffectiveUrgency"/>. | Perfil de <see cref="EffectiveUrgency"/>.</summary>
    public UrgencyProfile EffectiveProfile => UrgencyProfiles.Get(EffectiveUrgency);

    /// <summary>Sound at the current level, with the user's file if any. | Som no nível atual, com o arquivo do usuário se houver.</summary>
    public SoundSpec EffectiveSound =>
        Alarm.CustomSoundPath is null
            ? EffectiveProfile.Sound
            : EffectiveProfile.Sound with { FilePath = Alarm.CustomSoundPath, IsSilent = false };

    /// <summary>Earlier missed occurrences folded into this one alert. | Ocorrências anteriores perdidas, agrupadas neste único alerta.</summary>
    public int SkippedOccurrences { get; init; }

    /// <summary>How late the alert is. | O quanto o alerta está atrasado.</summary>
    public TimeSpan Delay => FiredAt - ScheduledFor;
}
