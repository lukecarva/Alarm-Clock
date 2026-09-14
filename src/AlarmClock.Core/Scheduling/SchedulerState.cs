using AlarmClock.Core.Model;

namespace AlarmClock.Core.Scheduling;

/// <summary>Runtime state that must survive a restart: pending snoozes and in-progress escalations. | Estado de execução que precisa sobreviver a um reinício: adiamentos pendentes e escaladas em curso.</summary>
public sealed record SchedulerState
{
    /// <summary>Snooze budget and pending wake time per alarm. | Orçamento de adiamento e hora de retorno pendente por alarme.</summary>
    public List<SnoozeState> Snoozes { get; init; } = [];

    /// <summary>Escalations in progress per alarm. | Escaladas em curso por alarme.</summary>
    public List<EscalationSnapshot> Escalations { get; init; } = [];

    /// <summary>Whether there is nothing to persist. | Se não há nada a persistir.</summary>
    public bool IsEmpty => Snoozes.Count == 0 && Escalations.Count == 0;
}

/// <summary>An alarm's snooze budget, with an optional pending wake time. | O orçamento de adiamento de um alarme, com hora de retorno pendente opcional.</summary>
public sealed record SnoozeState(Guid AlarmId, int Count, DateTimeOffset? DueAt);

/// <summary>An alarm's in-progress escalation; the policy is re-attached from the alarm on restore. | A escalada em curso de um alarme; a política é reanexada a partir do alarme na restauração.</summary>
public sealed record EscalationSnapshot(Guid AlarmId, UrgencyLevel Level, DateTimeOffset? IgnoreDeadline);
