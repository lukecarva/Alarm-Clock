using AlarmClock.Core.Scheduling;

namespace AlarmClock.Core.Model;

/// <summary>
/// Um alarme: o que dizer, quando, e com quanta insistência.
/// </summary>
public sealed record Alarm
{
    public required Guid Id { get; init; }

    /// <summary>Aparece grande no alerta. "Reunião com o time".</summary>
    public required string Title { get; init; }

    /// <summary>Detalhe opcional, abaixo do título.</summary>
    public string? Message { get; init; }

    public required ISchedule Schedule { get; init; }

    public UrgencyLevel Urgency { get; init; } = UrgencyLevel.Normal;

    /// <summary>Sobrescreve o som do nível de urgência. Nulo = usa o do perfil.</summary>
    public string? CustomSoundPath { get; init; }

    /// <summary>Desligado continua salvo, só não é agendado.</summary>
    public bool IsEnabled { get; init; } = true;

    public UrgencyProfile Profile => UrgencyProfiles.Get(Urgency);

    /// <summary>Som efetivo: o do perfil, com o arquivo do usuário se houver.</summary>
    public SoundSpec EffectiveSound =>
        CustomSoundPath is null ? Profile.Sound : Profile.Sound with { FilePath = CustomSoundPath, IsSilent = false };

    public static Alarm New(string title, ISchedule schedule, UrgencyLevel urgency = UrgencyLevel.Normal) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Schedule = schedule,
        Urgency = urgency,
    };
}
