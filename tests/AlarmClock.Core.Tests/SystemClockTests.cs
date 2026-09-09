using AlarmClock.Core.Abstractions;

namespace AlarmClock.Core.Tests;

/// <summary>
/// Testes de fumaça da Fase 0: garantem que a abstração de tempo se comporta
/// como o agendador da Fase 1 vai assumir que ela se comporta.
/// </summary>
public class SystemClockTests
{
    [Fact]
    public void SystemClock_Now_esta_proximo_do_relogio_real()
    {
        ISystemClock clock = new SystemClock();

        var diferenca = (clock.Now - DateTimeOffset.Now).Duration();

        Assert.True(diferenca < TimeSpan.FromSeconds(1), $"Diferença inesperada: {diferenca}.");
    }

    [Fact]
    public void SystemClock_Now_carrega_o_offset_local()
    {
        ISystemClock clock = new SystemClock();

        Assert.Equal(TimeZoneInfo.Local.GetUtcOffset(clock.Now), clock.Now.Offset);
    }

    [Fact]
    public void FakeClock_avanca_exatamente_o_pedido()
    {
        var inicio = new DateTimeOffset(2026, 3, 14, 7, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(inicio);

        clock.Advance(TimeSpan.FromMinutes(90));

        Assert.Equal(inicio.AddMinutes(90), clock.Now);
    }

    [Fact]
    public void FakeClock_recusa_avanco_negativo()
    {
        var clock = new FakeClock(DateTimeOffset.UnixEpoch);

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void FakeClock_permite_voltar_no_tempo_via_SetTo()
    {
        var inicio = new DateTimeOffset(2026, 3, 14, 7, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(inicio);

        clock.SetTo(inicio.AddHours(-2));

        Assert.Equal(inicio.AddHours(-2), clock.Now);
    }
}
