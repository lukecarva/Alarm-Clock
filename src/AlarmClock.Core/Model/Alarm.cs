using System.Text.Json.Serialization;
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

    /// <summary>
    /// Pula o alerta se o teclado e o mouse estiverem parados há pelo menos este
    /// tempo. Nulo = alerta sempre.
    /// </summary>
    /// <remarks>
    /// Existe para os lembretes cíclicos: "beba água" a cada 45 minutos não pode
    /// empilhar avisos numa cadeira vazia enquanto você almoça. Vale só para o
    /// disparo na hora — alarme perdido continua governado pelo
    /// <see cref="UrgencyProfile.WhenAway"/>, e adiamento é algo que você pediu
    /// explicitamente, então não é descartado por ausência.
    /// </remarks>
    public TimeSpan? SkipIfIdleFor { get; init; }

    /// <summary>Faz o alarme subir de nível quando ignorado. Nulo = não escala.</summary>
    public EscalationPolicy? Escalation { get; init; }

    /// <remarks>
    /// <see cref="JsonIgnore"/> é essencial: sem ele, cada alarme gravaria uma
    /// cópia inteira do perfil de urgência no arquivo. Além de inchar o JSON,
    /// isso desnormaliza justamente o que o desenho quer manter num lugar só —
    /// e daria a falsa impressão de que editar aquele bloco muda o
    /// comportamento, quando na leitura ele é ignorado.
    /// </remarks>
    [JsonIgnore]
    public UrgencyProfile Profile => UrgencyProfiles.Get(Urgency);

    /// <summary>Som efetivo: o do perfil, com o arquivo do usuário se houver.</summary>
    [JsonIgnore]
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
