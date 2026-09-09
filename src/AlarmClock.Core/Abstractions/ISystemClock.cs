namespace AlarmClock.Core.Abstractions;

/// <summary>
/// Toda leitura de tempo no domínio passa por aqui. É o que torna o agendador
/// testável: nos testes injeta-se um relógio falso e um mês inteiro de alarmes
/// é verificado em milissegundos, sem esperar relógio real.
/// </summary>
public interface ISystemClock
{
    /// <summary>Instante atual, com o offset local (nunca UTC puro).</summary>
    DateTimeOffset Now { get; }

    /// <summary>
    /// Fuso local. Exposto separadamente porque alarmes recorrentes guardam
    /// hora de parede e precisam do fuso para resolver horário de verão.
    /// </summary>
    TimeZoneInfo LocalTimeZone { get; }
}

/// <summary>Implementação real, ligada ao relógio do Windows.</summary>
public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;

    // Não cachear: o usuário pode trocar o fuso com o app aberto, e o
    // TimeZoneInfo.Local reflete isso após TimeZoneInfo.ClearCachedData().
    public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
}
