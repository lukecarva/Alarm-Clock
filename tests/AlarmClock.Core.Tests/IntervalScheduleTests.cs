using AlarmClock.Core.Scheduling;

namespace AlarmClock.Core.Tests;

public class IntervalScheduleTests
{
    private static readonly TimeZoneInfo Dst = TestZones.SouthernDst;

    private static DateTimeOffset Em(int ano, int mes, int dia, int hora, int min, int offsetHoras) =>
        new(ano, mes, dia, hora, min, 0, TimeSpan.FromHours(offsetHoras));

    // ---------- No time range | Sem faixa de horário ----------

    [Fact]
    public void Sem_faixa_dispara_a_cada_intervalo_a_partir_da_ancora()
    {
        var ancora = Em(2026, 6, 10, 9, 0, -3);
        var agenda = new IntervalSchedule(TimeSpan.FromMinutes(45), ancora);

        var proxima = agenda.NextOccurrenceAfter(ancora, Dst);

        Assert.Equal(Em(2026, 6, 10, 9, 45, -3), proxima);
    }

    [Fact]
    public void Mantem_o_ritmo_mesmo_consultando_no_meio_de_um_ciclo()
    {
        // Asking mid-cycle at 10:00 returns the original grid point (10:30). | Consultar no meio do ciclo às 10:00 retorna o ponto do ciclo original (10:30).
        var ancora = Em(2026, 6, 10, 9, 0, -3);
        var agenda = new IntervalSchedule(TimeSpan.FromMinutes(45), ancora);

        var proxima = agenda.NextOccurrenceAfter(Em(2026, 6, 10, 10, 0, -3), Dst);

        Assert.Equal(Em(2026, 6, 10, 10, 30, -3), proxima);
    }

    [Fact]
    public void Funciona_para_instantes_anteriores_a_ancora()
    {
        var ancora = Em(2026, 6, 10, 9, 0, -3);
        var agenda = new IntervalSchedule(TimeSpan.FromHours(1), ancora);

        var proxima = agenda.NextOccurrenceAfter(Em(2026, 6, 10, 6, 30, -3), Dst);

        Assert.Equal(Em(2026, 6, 10, 7, 0, -3), proxima);
    }

    [Fact]
    public void Intervalo_zero_nao_agenda_nada()
    {
        var agenda = new IntervalSchedule(TimeSpan.Zero, Em(2026, 6, 10, 9, 0, -3));

        Assert.Null(agenda.NextOccurrenceAfter(Em(2026, 6, 10, 9, 0, -3), Dst));
    }

    // ---------- Time range | Faixa de horário ----------

    [Fact]
    public void Antes_da_abertura_espera_a_faixa_comecar()
    {
        var ancora = Em(2026, 6, 10, 9, 0, -3);
        var agenda = new IntervalSchedule(
            TimeSpan.FromMinutes(45),
            ancora,
            new TimeOnly(9, 0),
            new TimeOnly(18, 0));

        var proxima = agenda.NextOccurrenceAfter(Em(2026, 6, 10, 6, 0, -3), Dst);

        Assert.Equal(Em(2026, 6, 10, 9, 0, -3), proxima);
    }

    [Fact]
    public void Depois_do_fechamento_pula_para_a_abertura_do_dia_seguinte()
    {
        // The reminder should not fire at 3am. | O lembrete não deve tocar às 3 da manhã.
        var ancora = Em(2026, 6, 10, 9, 0, -3);
        var agenda = new IntervalSchedule(
            TimeSpan.FromMinutes(45),
            ancora,
            new TimeOnly(9, 0),
            new TimeOnly(18, 0));

        var proxima = agenda.NextOccurrenceAfter(Em(2026, 6, 10, 17, 50, -3), Dst);

        Assert.Equal(Em(2026, 6, 11, 9, 0, -3), proxima);
    }

    [Fact]
    public void O_fim_da_faixa_e_exclusivo()
    {
        var ancora = Em(2026, 6, 10, 9, 0, -3);
        var agenda = new IntervalSchedule(
            TimeSpan.FromHours(1),
            ancora,
            new TimeOnly(9, 0),
            new TimeOnly(18, 0));

        // 18:00 sharp is already out; the last firing of the day is 17:00. | 18:00 em ponto já está fora; o último disparo do dia é 17:00.
        var proxima = agenda.NextOccurrenceAfter(Em(2026, 6, 10, 17, 0, -3), Dst);

        Assert.Equal(Em(2026, 6, 11, 9, 0, -3), proxima);
    }

    [Fact]
    public void Faixa_que_atravessa_a_meia_noite_continua_valendo_depois_das_00h()
    {
        var ancora = Em(2026, 6, 10, 22, 0, -3);
        var agenda = new IntervalSchedule(
            TimeSpan.FromHours(1),
            ancora,
            new TimeOnly(22, 0),
            new TimeOnly(6, 0));

        var proxima = agenda.NextOccurrenceAfter(Em(2026, 6, 10, 23, 30, -3), Dst);

        Assert.Equal(Em(2026, 6, 11, 0, 0, -3), proxima);
    }

    [Fact]
    public void Faixa_que_atravessa_a_meia_noite_reabre_a_noite_seguinte()
    {
        var ancora = Em(2026, 6, 10, 22, 0, -3);
        var agenda = new IntervalSchedule(
            TimeSpan.FromHours(1),
            ancora,
            new TimeOnly(22, 0),
            new TimeOnly(6, 0));

        var proxima = agenda.NextOccurrenceAfter(Em(2026, 6, 11, 6, 30, -3), Dst);

        Assert.Equal(Em(2026, 6, 11, 22, 0, -3), proxima);
    }

    // ---------- DST | Horário de verão ----------

    [Fact]
    public void Intervalo_e_duracao_absoluta_e_nao_se_desloca_no_horario_de_verao()
    {
        // "Every 1 hour" is 60 real minutes; the wall clock reads 01:00 because it jumped. | "A cada 1 hora" são 60 minutos reais; o relógio de parede marca 01:00 porque pulou.
        var ancora = Em(2026, 10, 14, 23, 0, -3);
        var agenda = new IntervalSchedule(TimeSpan.FromHours(1), ancora);

        var proxima = agenda.NextOccurrenceAfter(ancora, Dst);

        Assert.Equal(ancora.AddHours(1), proxima);

        var parede = TimeZoneInfo.ConvertTime(proxima!.Value, Dst);
        Assert.Equal(new TimeSpan(1, 0, 0), parede.TimeOfDay);
        Assert.Equal(TimeSpan.FromHours(-2), parede.Offset);
    }

    // ---------- Describe | Descrição ----------

    [Theory]
    [InlineData(45, "A cada 45 min")]
    [InlineData(60, "A cada 1h")]
    [InlineData(90, "A cada 1h30")]
    public void Descreve_o_intervalo_de_forma_curta(int minutos, string esperado)
    {
        var agenda = new IntervalSchedule(TimeSpan.FromMinutes(minutos), Em(2026, 6, 10, 9, 0, -3));

        Assert.Equal(esperado, agenda.Describe());
    }

    [Fact]
    public void Descreve_a_faixa_junto_quando_ela_existe()
    {
        var agenda = new IntervalSchedule(
            TimeSpan.FromMinutes(45),
            Em(2026, 6, 10, 9, 0, -3),
            new TimeOnly(9, 0),
            new TimeOnly(18, 0));

        Assert.Equal("A cada 45 min, 09:00 às 18:00", agenda.Describe());
    }
}
