using System.Text.Json.Serialization;
using AlarmClock.Core.Scheduling;

namespace AlarmClock.Core.Model;

/// <summary>An alarm: what to say, when, and how insistently. | Um alarme: o que dizer, quando, e com quanta insistência.</summary>
public sealed record Alarm
{
    /// <summary>Unique identifier. | Identificador único.</summary>
    public required Guid Id { get; init; }

    /// <summary>Shown large in the alert. | Aparece grande no alerta.</summary>
    public required string Title { get; init; }

    /// <summary>Optional detail below the title. | Detalhe opcional, abaixo do título.</summary>
    public string? Message { get; init; }

    /// <summary>When the alarm fires. | Quando o alarme dispara.</summary>
    public required ISchedule Schedule { get; init; }

    /// <summary>Urgency level. | Nível de urgência.</summary>
    public UrgencyLevel Urgency { get; init; } = UrgencyLevel.Normal;

    /// <summary>Overrides the level's sound with a file. Null = use the profile's. | Sobrescreve o som do nível por um arquivo. Nulo = usa o do perfil.</summary>
    public string? CustomSoundPath { get; init; }

    /// <summary>Disabled alarms stay saved but are not scheduled. | Alarmes desligados continuam salvos, só não são agendados.</summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>
    /// Marks a daily-habit reminder and which one; null = a normal user alarm. | Marca um lembrete de "dia a dia" e qual; nulo = alarme comum do usuário.
    /// </summary>
    public string? HabitKey { get; init; }

    /// <summary>
    /// Skips the on-time alert when keyboard/mouse have been idle at least this long. | Pula o alerta na hora quando teclado/mouse estão parados há pelo menos este tempo.
    /// </summary>
    public TimeSpan? SkipIfIdleFor { get; init; }

    /// <summary>Makes the alarm escalate when ignored. Null = no escalation. | Faz o alarme escalar quando ignorado. Nulo = sem escalada.</summary>
    public EscalationPolicy? Escalation { get; init; }

    /// <summary>
    /// Urgency profile for this alarm; not serialized (derived from <see cref="Urgency"/>). | Perfil de urgência do alarme; não serializado (derivado de <see cref="Urgency"/>).
    /// </summary>
    [JsonIgnore]
    public UrgencyProfile Profile => UrgencyProfiles.Get(Urgency);

    /// <summary>Effective sound: the profile's, with the user's file if any. | Som efetivo: o do perfil, com o arquivo do usuário se houver.</summary>
    [JsonIgnore]
    public SoundSpec EffectiveSound =>
        CustomSoundPath is null ? Profile.Sound : Profile.Sound with { FilePath = CustomSoundPath, IsSilent = false };

    /// <summary>Creates a new alarm with a fresh id. | Cria um alarme novo com id próprio.</summary>
    public static Alarm New(string title, ISchedule schedule, UrgencyLevel urgency = UrgencyLevel.Normal) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Schedule = schedule,
        Urgency = urgency,
    };
}
