namespace AlarmClock.Core.Model;

/// <summary>
/// Makes an alarm rise a level when ignored, up to <see cref="Ceiling"/>. | Faz um alarme subir de nível quando ignorado, até o <see cref="Ceiling"/>.
/// </summary>
public sealed record EscalationPolicy
{
    /// <summary>Rise a level after this long undismissed. Null = trigger unused. | Sobe um nível após este tempo sem ser dispensado. Nulo = gatilho não usado.</summary>
    public TimeSpan? AfterIgnoredFor { get; init; }

    /// <summary>Rise a level after this many snoozes. Null = trigger unused. | Sobe um nível após este número de adiamentos. Nulo = gatilho não usado.</summary>
    public int? AfterSnoozes { get; init; }

    /// <summary>Highest level the escalation reaches. | Nível máximo que a escalada alcança.</summary>
    public UrgencyLevel Ceiling { get; init; } = UrgencyLevel.Critical;

    /// <summary>Preset offered in the UI: 10 min ignored, or 2 snoozes. | Preset oferecido na interface: 10 min ignorado, ou 2 adiamentos.</summary>
    public static EscalationPolicy Default { get; } = new()
    {
        AfterIgnoredFor = TimeSpan.FromMinutes(10),
        AfterSnoozes = 2,
    };
}
