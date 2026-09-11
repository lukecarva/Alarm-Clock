namespace AlarmClock.Core.Model;

/// <summary>
/// Faz um alarme <b>subir de nível</b> quando é ignorado. É o "mata-ignorância":
/// um lembrete Normal que você deixa passar volta como Importante e, se ainda
/// assim for ignorado, como Crítico — até o <see cref="Ceiling"/>.
/// </summary>
/// <remarks>
/// Os dois gatilhos são independentes e valem juntos ("2 adiamentos <b>ou</b>
/// 10 min ignorado"):
/// <list type="bullet">
/// <item><see cref="AfterIgnoredFor"/>: o alerta ficou na tela sem você agir.</item>
/// <item><see cref="AfterSnoozes"/>: você adiou vezes demais.</item>
/// </list>
/// Fechamento automático (auto-dismiss) <b>não</b> conta como agir — senão o
/// nível Normal, que some sozinho em 30s, nunca escalaria. Só dispensar de fato
/// ou o teto interrompem a escalada.
/// </remarks>
public sealed record EscalationPolicy
{
    /// <summary>Sobe um nível se ficar sem ser dispensado por este tempo. Nulo = não usa este gatilho.</summary>
    public TimeSpan? AfterIgnoredFor { get; init; }

    /// <summary>Sobe um nível ao atingir este número de adiamentos. Nulo = não usa este gatilho.</summary>
    public int? AfterSnoozes { get; init; }

    /// <summary>Nível máximo que a escalada alcança.</summary>
    public UrgencyLevel Ceiling { get; init; } = UrgencyLevel.Critical;

    /// <summary>O preset oferecido na interface: 10 min ignorado, ou 2 adiamentos.</summary>
    public static EscalationPolicy Default { get; } = new()
    {
        AfterIgnoredFor = TimeSpan.FromMinutes(10),
        AfterSnoozes = 2,
    };
}
