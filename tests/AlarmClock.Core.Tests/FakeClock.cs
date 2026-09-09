using AlarmClock.Core.Abstractions;

namespace AlarmClock.Core.Tests;

/// <summary>
/// Relógio controlado pelos testes. A partir da Fase 1 é o que permite simular
/// "o PC dormiu 3 horas" ou "entrou o horário de verão" sem esperar nada.
/// </summary>
public sealed class FakeClock : ISystemClock
{
    public FakeClock(DateTimeOffset start, TimeZoneInfo? zone = null)
    {
        Now = start;
        LocalTimeZone = zone ?? TimeZoneInfo.Utc;
    }

    public DateTimeOffset Now { get; private set; }

    public TimeZoneInfo LocalTimeZone { get; }

    /// <summary>Avança o relógio. Use para simular a passagem do tempo.</summary>
    public void Advance(TimeSpan by)
    {
        if (by < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(by), "Use SetTo para voltar no tempo.");
        }

        Now += by;
    }

    /// <summary>
    /// Salta para um instante arbitrário — inclusive para trás, simulando o
    /// usuário mexendo no relógio do sistema.
    /// </summary>
    public void SetTo(DateTimeOffset instant) => Now = instant;
}
