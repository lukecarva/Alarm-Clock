using AlarmClock.Core.Model;
using AlarmClock.Core.Scheduling;

namespace AlarmClock.Core.Tests;

/// <summary>Snooze/escalation survive a restart via CaptureState/RestoreState. | Adiamento/escalada sobrevivem a um reinício via CaptureState/RestoreState.</summary>
public class SchedulerStatePersistenceTests
{
    private static DateTimeOffset Utc(int ano, int mes, int dia, int hora, int min = 0) =>
        new(ano, mes, dia, hora, min, 0, TimeSpan.Zero);

    private static Alarm Diario(UrgencyLevel nivel = UrgencyLevel.Normal) =>
        Alarm.New("Reunião", new DailySchedule(new TimeOnly(7, 0)), nivel);

    private static Alarm Escalavel(TimeSpan aposIgnorar) =>
        Diario() with
        {
            Escalation = new EscalationPolicy { AfterIgnoredFor = aposIgnorar, Ceiling = UrgencyLevel.Critical },
        };

    /// <summary>Builds a scheduler with the given alarms already loaded. | Monta um agendador com os alarmes dados já carregados.</summary>
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
    public void Capturar_traz_o_adiamento_pendente_com_contador()
    {
        var alarme = Diario();
        var (scheduler, clock, _) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick(); // dispara às 7:00 | fires at 7:00
        Assert.True(scheduler.TrySnooze(alarme.Id, TimeSpan.FromMinutes(10), out _));

        var estado = scheduler.CaptureState();

        var soneca = Assert.Single(estado.Snoozes);
        Assert.Equal(alarme.Id, soneca.AlarmId);
        Assert.Equal(1, soneca.Count);
        Assert.Equal(Utc(2026, 6, 10, 7, 10), soneca.DueAt);
    }

    [Fact]
    public void Restaurar_recoloca_o_contador_de_adiamentos()
    {
        var alarme = Diario();
        var (antigo, clock, _) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        antigo.Tick();
        Assert.True(antigo.TrySnooze(alarme.Id, TimeSpan.FromMinutes(10), out _));
        var estado = antigo.CaptureState();

        // "Restart": a fresh scheduler reloads the alarms, then restores the state. | "Reinício": um agendador novo recarrega os alarmes e depois restaura o estado.
        var novoClock = new FakeClock(Utc(2026, 6, 10, 7, 5));
        var novo = new AlarmScheduler(novoClock);
        novo.Reload([alarme]);
        novo.RestoreState(estado);

        Assert.Equal(1, novo.SnoozeCountFor(alarme.Id));
    }

    [Fact]
    public void Adiamento_pendente_dispara_no_primeiro_tique_apos_reiniciar()
    {
        var alarme = Diario();
        var (antigo, clock, _) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        antigo.Tick();
        Assert.True(antigo.TrySnooze(alarme.Id, TimeSpan.FromMinutes(10), out _)); // volta às 7:10
        var estado = antigo.CaptureState();

        // Restart at 7:15: the snooze came due while the app was closed. | Reinício às 7:15: o adiamento venceu com o app fechado.
        var novoClock = new FakeClock(Utc(2026, 6, 10, 7, 15));
        var novo = new AlarmScheduler(novoClock);
        var disparos = new List<AlarmTriggeredEventArgs>();
        novo.Triggered += (_, e) => disparos.Add(e);
        novo.Reload([alarme]);
        novo.RestoreState(estado);

        novo.Tick();

        var disparo = Assert.Single(disparos);
        Assert.Equal(TriggerKind.Snooze, disparo.Kind);
        Assert.Equal(alarme.Id, disparo.Alarm.Id);
    }

    [Fact]
    public void Escalada_continua_do_nivel_salvo_apos_reiniciar()
    {
        var alarme = Escalavel(TimeSpan.FromMinutes(10));
        var (antigo, clock, disparos) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        antigo.Tick(); // 7:00, Normal
        clock.Advance(TimeSpan.FromMinutes(10));
        antigo.Tick(); // sobe para High | rises to High
        Assert.Equal(UrgencyLevel.High, disparos[^1].EffectiveUrgency);

        var estado = antigo.CaptureState();
        var escalada = Assert.Single(estado.Escalations);
        Assert.Equal(UrgencyLevel.High, escalada.Level);

        // Restart: escalation resumes from High and rises to Critical. | Reinício: a escalada retoma de High e sobe para Crítico.
        var novoClock = new FakeClock(Utc(2026, 6, 10, 7, 20));
        var novo = new AlarmScheduler(novoClock);
        var novosDisparos = new List<AlarmTriggeredEventArgs>();
        novo.Triggered += (_, e) => novosDisparos.Add(e);
        novo.Reload([alarme]);
        novo.RestoreState(estado);

        novo.Tick();

        var subida = Assert.Single(novosDisparos, d => d.Kind == TriggerKind.Escalation);
        Assert.Equal(UrgencyLevel.Critical, subida.EffectiveUrgency);
    }

    [Fact]
    public void Restaurar_ignora_estado_de_alarme_que_nao_existe_mais()
    {
        var alarme = Diario();
        var estado = new SchedulerState
        {
            Snoozes = [new SnoozeState(Guid.NewGuid(), 3, Utc(2026, 6, 10, 7, 10))],
        };

        var (scheduler, _, _) = Montar(Utc(2026, 6, 10, 6, 0), alarme);
        scheduler.RestoreState(estado);

        // The orphan snooze was dropped, so nothing extra is pending. | O adiamento órfão foi descartado, então nada extra fica pendente.
        Assert.True(scheduler.CaptureState().IsEmpty);
    }

    [Fact]
    public void Restaurar_descarta_escalada_quando_a_politica_foi_removida()
    {
        // The alarm still exists but no longer escalates: the saved escalation is dropped. | O alarme ainda existe mas não escala mais: a escalada salva é descartada.
        var alarme = Diario();
        var estado = new SchedulerState
        {
            Escalations = [new EscalationSnapshot(alarme.Id, UrgencyLevel.Critical, Utc(2026, 6, 10, 7, 10))],
        };

        var (scheduler, _, _) = Montar(Utc(2026, 6, 10, 6, 0), alarme);
        scheduler.RestoreState(estado);

        Assert.Empty(scheduler.CaptureState().Escalations);
    }

    [Fact]
    public void Dispensar_deixa_o_estado_vazio()
    {
        var alarme = Diario();
        var (scheduler, clock, _) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();
        Assert.True(scheduler.TrySnooze(alarme.Id, TimeSpan.FromMinutes(10), out _));

        scheduler.Dismiss(alarme.Id);

        Assert.True(scheduler.CaptureState().IsEmpty);
    }

    [Fact]
    public void Adiar_emite_StateChanged()
    {
        var alarme = Diario();
        var (scheduler, clock, _) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        var mudou = 0;
        scheduler.StateChanged += (_, _) => mudou++;

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick(); // nova ocorrência já mexe no estado
        var aposTick = mudou;

        Assert.True(scheduler.TrySnooze(alarme.Id, TimeSpan.FromMinutes(10), out _));

        Assert.True(mudou > aposTick, "Adiar deve emitir StateChanged.");
    }

    [Fact]
    public void Remover_alarme_no_reload_emite_StateChanged_para_limpar_o_disco()
    {
        var alarme = Diario();
        var (scheduler, clock, _) = Montar(Utc(2026, 6, 10, 6, 0), alarme);

        clock.Advance(TimeSpan.FromHours(1));
        scheduler.Tick();
        Assert.True(scheduler.TrySnooze(alarme.Id, TimeSpan.FromMinutes(10), out _));

        var mudou = 0;
        scheduler.StateChanged += (_, _) => mudou++;

        // The alarm is gone: Reload drops its snooze and must persist the removal. | O alarme sumiu: o Reload descarta o adiamento e precisa persistir a remoção.
        scheduler.Reload([]);

        Assert.True(mudou > 0, "Descartar órfãos no Reload deve emitir StateChanged.");
        Assert.True(scheduler.CaptureState().IsEmpty);
    }
}
