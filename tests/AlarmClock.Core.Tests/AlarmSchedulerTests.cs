using AlarmClock.Core.Model;
using AlarmClock.Core.Scheduling;

namespace AlarmClock.Core.Tests;

public class AlarmSchedulerTests
{
    private static DateTimeOffset Utc(int ano, int mes, int dia, int hora, int min = 0) =>
        new(ano, mes, dia, hora, min, 0, TimeSpan.Zero);

    /// <summary>Monta agendador + captura de disparos, com relógio controlado.</summary>
    private static (AlarmScheduler Scheduler, FakeClock Clock, List<AlarmTriggeredEventArgs> Disparos) Montar(
        DateTimeOffset inicio,
        params Alarm[] alarmes)
    {
        var clock = new FakeClock(inicio);
        var scheduler = new AlarmScheduler(clock);
        var disparos = new List<AlarmTriggeredEventArgs>();

        scheduler.Triggered += (_, e) => disparos.Add(e);
        scheduler.Reload(alarmes);

        return (scheduler, clock, disparos);
    }

    [Fact]
    public void Nao_dispara_antes_da_hora()
    {
        var alarme = Alarm.New("Reunião", new DailySchedule(new TimeOnly(7, 0)));
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromMinutes(59));
        scheduler.Tick();

        Assert.Empty(disparos);
    }

    [Fact]
    public void Dispara_na_hora()
    {
        var alarme = Alarm.New("Reunião", new DailySchedule(new TimeOnly(7, 0)));
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();

        var disparo = Assert.Single(disparos);
        Assert.Equal(TriggerKind.OnTime, disparo.Kind);
        Assert.Equal(Utc(2026, 6, 10, 7, 0), disparo.ScheduledFor);
        Assert.Equal(0, disparo.SkippedOccurrences);
    }

    [Fact]
    public void Dispara_uma_vez_so_mesmo_com_varios_ticks()
    {
        var alarme = Alarm.New("Reunião", new DailySchedule(new TimeOnly(7, 0)));
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 59), alarme);

        clock.Advance(TimeSpan.FromMinutes(1));
        scheduler.Tick();
        scheduler.Tick();
        scheduler.Tick();

        Assert.Single(disparos);
    }

    [Fact]
    public void Atraso_dentro_da_janela_ainda_conta_como_na_hora()
    {
        var alarme = Alarm.New("Reunião", new DailySchedule(new TimeOnly(7, 0)));
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        // Ficou 10 minutos sem tick — travadinha do sistema, GC longo, o que for.
        clock.Advance(TimeSpan.FromMinutes(70));
        scheduler.Tick();

        Assert.Equal(TriggerKind.OnTime, disparos.Single().Kind);
        Assert.Equal(TimeSpan.FromMinutes(10), disparos.Single().Delay);
    }

    [Fact]
    public void PC_dormindo_alem_da_janela_marca_o_alarme_como_perdido()
    {
        var alarme = Alarm.New("Remédio", new DailySchedule(new TimeOnly(7, 0)));
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        // Dormiu às 6h, acordou às 10h. O alarme das 7h não pode aparecer como
        // se fosse agora.
        clock.Advance(TimeSpan.FromHours(4));
        scheduler.Tick();

        var disparo = Assert.Single(disparos);
        Assert.Equal(TriggerKind.Missed, disparo.Kind);
        Assert.Equal(TimeSpan.FromHours(3), disparo.Delay);
    }

    [Fact]
    public void Tres_dias_desligado_geram_um_alerta_e_nao_tres()
    {
        var alarme = Alarm.New("Remédio", new DailySchedule(new TimeOnly(7, 0)));
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.SetTo(Utc(2026, 6, 13, 8, 0));
        scheduler.Tick();

        var disparo = Assert.Single(disparos);
        Assert.Equal(TriggerKind.Missed, disparo.Kind);
        Assert.Equal(Utc(2026, 6, 13, 7, 0), disparo.ScheduledFor);
        Assert.Equal(3, disparo.SkippedOccurrences);
    }

    [Fact]
    public void Depois_de_um_disparo_a_proxima_ocorrencia_ja_esta_agendada()
    {
        var alarme = Alarm.New("Reunião", new DailySchedule(new TimeOnly(7, 0)));
        var (scheduler, clock, _) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();

        Assert.Equal(Utc(2026, 6, 11, 7, 0), scheduler.NextFireTime);
    }

    [Fact]
    public void Alarme_unico_some_da_agenda_depois_de_disparar()
    {
        var alarme = Alarm.New("Ligar para o dentista", new OneTimeSchedule(Utc(2026, 6, 10, 7, 0)));
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();

        Assert.Single(disparos);
        Assert.Null(scheduler.NextFireTime);
    }

    [Fact]
    public void Relogio_andando_para_tras_recalcula_em_vez_de_disparar_em_rajada()
    {
        var alarme = Alarm.New("Reunião", new DailySchedule(new TimeOnly(7, 0)));
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        // Usuário corrigiu o relógio do sistema para dez dias antes.
        clock.SetTo(Utc(2026, 5, 31, 6, 0));
        scheduler.Tick();

        Assert.Empty(disparos);
        Assert.Equal(Utc(2026, 5, 31, 7, 0), scheduler.NextFireTime);
    }

    [Fact]
    public void Alarme_desligado_nao_entra_na_agenda()
    {
        var alarme = Alarm.New("Reunião", new DailySchedule(new TimeOnly(7, 0))) with { IsEnabled = false };
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(2));
        scheduler.Tick();

        Assert.Empty(disparos);
        Assert.Null(scheduler.NextFireTime);
    }

    // ---------- Adiamento ----------

    [Fact]
    public void Adiar_reapresenta_o_alarme_depois_do_prazo()
    {
        var alarme = Alarm.New("Reunião", new DailySchedule(new TimeOnly(7, 0)));
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();
        disparos.Clear();

        Assert.True(scheduler.TrySnooze(alarme.Id, TimeSpan.FromMinutes(5), out _));

        clock.Advance(TimeSpan.FromMinutes(4));
        scheduler.Tick();
        Assert.Empty(disparos);

        clock.Advance(TimeSpan.FromMinutes(1));
        scheduler.Tick();

        Assert.Equal(TriggerKind.Snooze, disparos.Single().Kind);
    }

    [Fact]
    public void Nivel_critico_aceita_um_unico_adiamento()
    {
        var alarme = Alarm.New("Sair de casa", new DailySchedule(new TimeOnly(7, 0)), UrgencyLevel.Critical);
        var (scheduler, clock, _) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();

        Assert.True(scheduler.TrySnooze(alarme.Id, TimeSpan.FromMinutes(2), out _));
        Assert.False(scheduler.TrySnooze(alarme.Id, TimeSpan.FromMinutes(2), out var recusa));
        Assert.Contains("uma vez", recusa);
    }

    [Fact]
    public void Nivel_sussurro_nao_aceita_adiamento()
    {
        var alarme = Alarm.New("Beber água", new DailySchedule(new TimeOnly(7, 0)), UrgencyLevel.Whisper);
        var (scheduler, _, _) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        Assert.False(scheduler.TrySnooze(alarme.Id, TimeSpan.FromMinutes(5), out var recusa));
        Assert.Contains("Sussurro", recusa);
    }

    [Fact]
    public void Nova_ocorrencia_devolve_o_orcamento_de_adiamentos()
    {
        var alarme = Alarm.New("Sair de casa", new DailySchedule(new TimeOnly(7, 0)), UrgencyLevel.Critical);
        var (scheduler, clock, _) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();
        Assert.True(scheduler.TrySnooze(alarme.Id, TimeSpan.FromMinutes(2), out _));
        Assert.Equal(1, scheduler.SnoozeCountFor(alarme.Id));

        // Dia seguinte: o limite de adiamentos vale por ocorrência, não para sempre.
        clock.SetTo(Utc(2026, 6, 11, 7, 0));
        scheduler.Tick();

        Assert.Equal(0, scheduler.SnoozeCountFor(alarme.Id));
        Assert.True(scheduler.TrySnooze(alarme.Id, TimeSpan.FromMinutes(2), out _));
    }

    [Fact]
    public void Dispensar_cancela_o_adiamento_pendente()
    {
        var alarme = Alarm.New("Reunião", new DailySchedule(new TimeOnly(7, 0)));
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();
        disparos.Clear();

        scheduler.TrySnooze(alarme.Id, TimeSpan.FromMinutes(5), out _);
        scheduler.Dismiss(alarme.Id);

        clock.Advance(TimeSpan.FromMinutes(10));
        scheduler.Tick();

        Assert.Empty(disparos);
    }

    [Fact]
    public void Remover_o_alarme_leva_junto_o_adiamento_pendente()
    {
        var alarme = Alarm.New("Reunião", new DailySchedule(new TimeOnly(7, 0)));
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();
        scheduler.TrySnooze(alarme.Id, TimeSpan.FromMinutes(5), out _);
        disparos.Clear();

        scheduler.Reload([]);

        clock.Advance(TimeSpan.FromMinutes(10));
        scheduler.Tick();

        Assert.Empty(disparos);
    }

    [Fact]
    public void NextFireTime_considera_o_adiamento_quando_ele_vem_antes()
    {
        var alarme = Alarm.New("Reunião", new DailySchedule(new TimeOnly(7, 0)));
        var (scheduler, clock, _) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();
        scheduler.TrySnooze(alarme.Id, TimeSpan.FromMinutes(5), out _);

        Assert.Equal(Utc(2026, 6, 10, 7, 5), scheduler.NextFireTime);
    }
}
