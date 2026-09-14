using AlarmClock.Core.Abstractions;

namespace AlarmClock.Core.Tests;

/// <summary>Test-controlled clock, to simulate the passage of time. | Relógio controlado pelos testes, para simular a passagem do tempo.</summary>
public sealed class FakeClock(DateTimeOffset start, TimeZoneInfo? zone = null) : ISystemClock
{
    public DateTimeOffset Now { get; private set; } = start;

    public TimeZoneInfo LocalTimeZone { get; } = zone ?? TimeZoneInfo.Utc;

    /// <summary>Advances the clock forward. | Avança o relógio para a frente.</summary>
    public void Advance(TimeSpan by)
    {
        if (by < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(by), "Use SetTo para voltar no tempo.");
        }

        Now += by;
    }

    /// <summary>Jumps to an arbitrary instant, even backwards. | Salta para um instante arbitrário, inclusive para trás.</summary>
    public void SetTo(DateTimeOffset instant) => Now = instant;
}
