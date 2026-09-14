using AlarmClock.Core.Model;
using AlarmClock.Core.Scheduling;

namespace AlarmClock.Core.Tests;

public class EscalationTests
{
    private static DateTimeOffset Utc(int ano, int mes, int dia, int hora, int min = 0) =>
        new(ano, mes, dia, hora, min, 0, TimeSpan.Zero);

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

    private static Alarm Escalavel(
        UrgencyLevel baseLevel = UrgencyLevel.Normal,
        TimeSpan? aposIgnorar = null,
        int? aposAdiar = null,
        UrgencyLevel teto = UrgencyLevel.Critical) =>
        Alarm.New("Reunião", new DailySchedule(new TimeOnly(7, 0)), baseLevel) with
        {
            Escalation = new EscalationPolicy
            {
                AfterIgnoredFor = aposIgnorar,
                AfterSnoozes = aposAdiar,
                Ceiling = teto,
            },
        };

    // ---------- Initial trigger | Disparo inicial ----------

    [Fact]
    public void Disparo_normal_carrega_o_nivel_base_como_efetivo()
    {
        var alarme = Alarm.New("Reunião", new DailySchedule(new TimeOnly(7, 0)), UrgencyLevel.Normal);
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();

        Assert.Equal(UrgencyLevel.Normal, disparos.Single().EffectiveUrgency);
    }

    // ---------- Escalation by being ignored | Escalada por ser ignorado ----------

    [Fact]
    public void Ignorado_pelo_prazo_reapresenta_um_nivel_acima()
    {
        var alarme = Escalavel(UrgencyLevel.Normal, aposIgnorar: TimeSpan.FromMinutes(10));
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1)); // fires at 7:00 | dispara às 7:00
        scheduler.Tick();
        disparos.Clear();

        clock.Advance(TimeSpan.FromMinutes(9));
        scheduler.Tick();
        Assert.Empty(disparos); // still within the 10 min | ainda dentro dos 10 min

        clock.Advance(TimeSpan.FromMinutes(1));
        scheduler.Tick();

        var subida = Assert.Single(disparos);
        Assert.Equal(TriggerKind.Escalation, subida.Kind);
        Assert.Equal(UrgencyLevel.High, subida.EffectiveUrgency);
    }

    [Fact]
    public void Ignorado_repetidamente_sobe_degrau_a_degrau_ate_o_teto()
    {
        var alarme = Escalavel(UrgencyLevel.Normal, aposIgnorar: TimeSpan.FromMinutes(10));
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();
        disparos.Clear();

        clock.Advance(TimeSpan.FromMinutes(10));
        scheduler.Tick();
        Assert.Equal(UrgencyLevel.High, disparos[^1].EffectiveUrgency);

        clock.Advance(TimeSpan.FromMinutes(10));
        scheduler.Tick();
        Assert.Equal(UrgencyLevel.Critical, disparos[^1].EffectiveUrgency);

        // At the ceiling (Critical) it stops rising and re-showing. | No teto (Crítico) para de subir e de reapresentar.
        var antes = disparos.Count;
        clock.Advance(TimeSpan.FromMinutes(30));
        scheduler.Tick();
        Assert.Equal(antes, disparos.Count);
    }

    [Fact]
    public void Respeita_o_teto_configurado()
    {
        var alarme = Escalavel(UrgencyLevel.Normal, aposIgnorar: TimeSpan.FromMinutes(5), teto: UrgencyLevel.High);
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();
        disparos.Clear();

        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        Assert.Equal(UrgencyLevel.High, disparos[^1].EffectiveUrgency);

        // Ceiling is High: it never reaches Critical. | Teto é High: nunca chega a Crítico.
        var antes = disparos.Count;
        clock.Advance(TimeSpan.FromMinutes(30));
        scheduler.Tick();
        Assert.Equal(antes, disparos.Count);
    }

    [Fact]
    public void Dispensar_encerra_a_escalada()
    {
        var alarme = Escalavel(UrgencyLevel.Normal, aposIgnorar: TimeSpan.FromMinutes(10));
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();
        scheduler.Dismiss(alarme.Id);
        disparos.Clear();

        clock.Advance(TimeSpan.FromMinutes(20));
        scheduler.Tick();

        Assert.Empty(disparos);
    }

    [Fact]
    public void Sem_politica_nao_escala()
    {
        var alarme = Alarm.New("Reunião", new DailySchedule(new TimeOnly(7, 0)));
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();
        disparos.Clear();

        clock.Advance(TimeSpan.FromHours(2));
        scheduler.Tick();

        Assert.DoesNotContain(disparos, d => d.Kind == TriggerKind.Escalation);
    }

    // ---------- Escalation by snoozing | Escalada por adiamento ----------

    [Fact]
    public void Adiar_ate_o_limite_sobe_o_nivel_na_reapresentacao()
    {
        var alarme = Escalavel(UrgencyLevel.Normal, aposAdiar: 2);
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();
        disparos.Clear();

        // 1st snooze: still Normal when it returns. | 1º adiamento: ainda Normal quando volta.
        Assert.True(scheduler.TrySnooze(alarme.Id, TimeSpan.FromMinutes(5), out _));
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();
        Assert.Equal(UrgencyLevel.Normal, disparos[^1].EffectiveUrgency);

        // 2nd snooze hits the threshold: returns as Important. | 2º adiamento atinge o limite: volta como Importante.
        Assert.True(scheduler.TrySnooze(alarme.Id, TimeSpan.FromMinutes(5), out _));
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick();

        Assert.Equal(TriggerKind.Snooze, disparos[^1].Kind);
        Assert.Equal(UrgencyLevel.High, disparos[^1].EffectiveUrgency);
    }

    [Fact]
    public void Adiar_pausa_o_relogio_de_ignorado()
    {
        // While snoozed the alert is off screen, so the ignore clock is paused. | Enquanto adiado o alerta está fora da tela, então o relógio de ignorado fica pausado.
        var alarme = Escalavel(UrgencyLevel.Normal, aposIgnorar: TimeSpan.FromMinutes(10));
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();
        Assert.True(scheduler.TrySnooze(alarme.Id, TimeSpan.FromMinutes(30), out _));
        disparos.Clear();

        // 20 min snoozed: past 10 min, but paused, so nothing happens. | 20 min adiado: passou dos 10 min, mas pausado, nada acontece.
        clock.Advance(TimeSpan.FromMinutes(20));
        scheduler.Tick();
        Assert.Empty(disparos);
    }

    [Fact]
    public void Nova_ocorrencia_recomeça_a_escalada_do_nivel_base()
    {
        var alarme = Escalavel(UrgencyLevel.Normal, aposIgnorar: TimeSpan.FromMinutes(10));
        var (scheduler, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();
        clock.Advance(TimeSpan.FromMinutes(10));
        scheduler.Tick(); // rose to High | subiu para High
        Assert.Equal(UrgencyLevel.High, disparos[^1].EffectiveUrgency);
        disparos.Clear();

        // Next day: starts over at Normal. | Dia seguinte: começa de novo em Normal.
        clock.SetTo(Utc(2026, 6, 11, 7, 0));
        scheduler.Tick();

        Assert.Equal(UrgencyLevel.Normal, disparos.Single(d => d.Kind == TriggerKind.OnTime).EffectiveUrgency);
    }

    [Fact]
    public void Escalada_ate_critico_passa_a_valer_o_limite_de_adiamento_do_critico()
    {
        // After escalating to Critical, snooze follows Critical's 1x limit. | Após escalar a Crítico, o adiamento segue o limite 1x de Crítico.
        var alarme = Escalavel(UrgencyLevel.Normal, aposIgnorar: TimeSpan.FromMinutes(5));
        var (scheduler, clock, _) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick(); // High
        clock.Advance(TimeSpan.FromMinutes(5));
        scheduler.Tick(); // Critical

        Assert.True(scheduler.TrySnooze(alarme.Id, TimeSpan.FromMinutes(2), out _));
        Assert.False(scheduler.TrySnooze(alarme.Id, TimeSpan.FromMinutes(2), out var recusa));
        Assert.Contains("uma vez", recusa);
    }
}
