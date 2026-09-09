using AlarmClock.Core.Scheduling;

namespace AlarmClock.Core.Tests;

public class ScheduleTests
{
    private static readonly TimeZoneInfo Dst = TestZones.SouthernDst;

    private static DateTimeOffset Em(int ano, int mes, int dia, int hora, int min, int offsetHoras) =>
        new(ano, mes, dia, hora, min, 0, TimeSpan.FromHours(offsetHoras));

    // ---------- Diário ----------

    [Fact]
    public void Diario_pega_o_horario_de_hoje_quando_ainda_nao_passou()
    {
        var agenda = new DailySchedule(new TimeOnly(7, 0));

        var proxima = agenda.NextOccurrenceAfter(Em(2026, 6, 10, 5, 30, -3), Dst);

        Assert.Equal(Em(2026, 6, 10, 7, 0, -3), proxima);
    }

    [Fact]
    public void Diario_vira_para_amanha_quando_o_horario_de_hoje_ja_passou()
    {
        var agenda = new DailySchedule(new TimeOnly(7, 0));

        var proxima = agenda.NextOccurrenceAfter(Em(2026, 6, 10, 7, 0, -3), Dst);

        Assert.Equal(Em(2026, 6, 11, 7, 0, -3), proxima);
    }

    [Fact]
    public void Diario_mantem_a_hora_de_parede_quando_entra_o_horario_de_verao()
    {
        // O alarme das 7h continua às 7h no dia seguinte à virada — o que muda
        // é o offset, não o número no relógio. Guardar isto em UTC faria o
        // alarme "andar" uma hora duas vezes por ano.
        var agenda = new DailySchedule(new TimeOnly(7, 0));

        var proxima = agenda.NextOccurrenceAfter(Em(2026, 10, 14, 12, 0, -3), Dst);

        Assert.Equal(Em(2026, 10, 15, 7, 0, -2), proxima);
    }

    [Fact]
    public void Diario_dispara_no_fim_do_buraco_quando_a_hora_nao_existe()
    {
        // 15/out às 00:30 não existe: o relógio pula de 00:00 para 01:00.
        // Adiantar alguns minutos é infinitamente melhor que não tocar.
        var agenda = new DailySchedule(new TimeOnly(0, 30));

        var proxima = agenda.NextOccurrenceAfter(Em(2026, 10, 14, 23, 0, -3), Dst);

        Assert.Equal(Em(2026, 10, 15, 1, 0, -2), proxima);
    }

    [Fact]
    public void Diario_escolhe_a_primeira_passagem_quando_a_hora_acontece_duas_vezes()
    {
        // 14/fev às 23:30 acontece duas vezes. A primeira é a do offset maior
        // (ainda no horário de verão): dispara nela, não uma hora depois.
        var agenda = new DailySchedule(new TimeOnly(23, 30));

        var proxima = agenda.NextOccurrenceAfter(Em(2026, 2, 14, 12, 0, -2), Dst);

        Assert.Equal(Em(2026, 2, 14, 23, 30, -2), proxima);
    }

    [Fact]
    public void Diario_nunca_devolve_o_proprio_instante_consultado()
    {
        var agenda = new DailySchedule(new TimeOnly(7, 0));
        var agora = Em(2026, 6, 10, 7, 0, -3);

        var proxima = agenda.NextOccurrenceAfter(agora, Dst);

        Assert.True(proxima > agora, "NextOccurrenceAfter tem que ser estritamente posterior.");
    }

    // ---------- Semanal ----------

    [Fact]
    public void Semanal_pula_para_o_proximo_dia_marcado()
    {
        // 10/06/2026 é uma quarta-feira; o alarme é seg/sex.
        var agenda = new WeeklySchedule(WeekDays.Monday | WeekDays.Friday, new TimeOnly(9, 0));

        var proxima = agenda.NextOccurrenceAfter(Em(2026, 6, 10, 12, 0, -3), Dst);

        Assert.Equal(Em(2026, 6, 12, 9, 0, -3), proxima);
        Assert.Equal(DayOfWeek.Friday, proxima!.Value.DayOfWeek);
    }

    [Fact]
    public void Semanal_atravessa_a_virada_da_semana()
    {
        // Sexta à noite, alarme só de segunda.
        var agenda = new WeeklySchedule(WeekDays.Monday, new TimeOnly(9, 0));

        var proxima = agenda.NextOccurrenceAfter(Em(2026, 6, 12, 22, 0, -3), Dst);

        Assert.Equal(Em(2026, 6, 15, 9, 0, -3), proxima);
    }

    [Fact]
    public void Semanal_sem_nenhum_dia_marcado_nao_agenda_nada()
    {
        var agenda = new WeeklySchedule(WeekDays.None, new TimeOnly(9, 0));

        Assert.Null(agenda.NextOccurrenceAfter(Em(2026, 6, 10, 12, 0, -3), Dst));
    }

    [Fact]
    public void Semanal_com_dias_uteis_descreve_de_forma_curta()
    {
        var agenda = new WeeklySchedule(WeekDays.Weekdays, new TimeOnly(8, 30));

        Assert.Equal("Dias úteis, 08:30", agenda.Describe());
    }

    [Fact]
    public void Semanal_descreve_dias_avulsos_com_siglas()
    {
        var agenda = new WeeklySchedule(WeekDays.Monday | WeekDays.Wednesday, new TimeOnly(8, 30));

        Assert.Equal("seg, qua, 08:30", agenda.Describe());
    }

    // ---------- Única ----------

    [Fact]
    public void Unica_no_futuro_devolve_o_instante_exato()
    {
        var quando = Em(2026, 6, 10, 15, 0, -3);
        var agenda = new OneTimeSchedule(quando);

        Assert.Equal(quando, agenda.NextOccurrenceAfter(Em(2026, 6, 10, 14, 0, -3), Dst));
    }

    [Fact]
    public void Unica_no_passado_nao_tem_proxima_ocorrencia()
    {
        var agenda = new OneTimeSchedule(Em(2026, 6, 10, 15, 0, -3));

        Assert.Null(agenda.NextOccurrenceAfter(Em(2026, 6, 10, 15, 0, -3), Dst));
    }
}
