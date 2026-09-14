using AlarmClock.Core.Model;
using AlarmClock.Core.Persistence;
using AlarmClock.Core.Scheduling;

namespace AlarmClock.Core.Tests;

public class JsonAlarmStoreTests : IDisposable
{
    private readonly string _pasta;
    private readonly string _arquivo;

    public JsonAlarmStoreTests()
    {
        _pasta = Path.Combine(Path.GetTempPath(), "AlarmClockTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_pasta);
        _arquivo = Path.Combine(_pasta, "alarms.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_pasta, recursive: true);
        }
        catch (IOException)
        {
            // Best-effort cleanup; a locked file must not fail the test. | Limpeza best-effort; um arquivo preso não pode derrubar o teste.
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Arquivo_inexistente_carrega_lista_vazia()
    {
        var store = new JsonAlarmStore(_arquivo);

        Assert.Empty(store.Load());
    }

    [Fact]
    public void Round_trip_preserva_o_tipo_de_agenda()
    {
        // ISchedule is polymorphic; the "$type" discriminator must round-trip. | ISchedule é polimórfico; o discriminador "$type" precisa sobreviver ao round-trip.
        var store = new JsonAlarmStore(_arquivo);

        var originais = new[]
        {
            Alarm.New("Diário", new DailySchedule(new TimeOnly(7, 0))),
            Alarm.New("Semanal", new WeeklySchedule(WeekDays.Weekdays, new TimeOnly(8, 30)), UrgencyLevel.High),
            Alarm.New("Único", new OneTimeSchedule(new DateTimeOffset(2026, 12, 25, 9, 0, 0, TimeSpan.FromHours(-3)))),
            Alarm.New(
                "Cíclico",
                new IntervalSchedule(
                    TimeSpan.FromMinutes(45),
                    new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.FromHours(-3)))),
        };

        store.Save(originais);
        var lidos = store.Load();

        Assert.Equal(4, lidos.Count);
        Assert.Equal<Alarm>(originais, lidos);
        Assert.IsType<DailySchedule>(lidos[0].Schedule);
        Assert.IsType<WeeklySchedule>(lidos[1].Schedule);
        Assert.IsType<OneTimeSchedule>(lidos[2].Schedule);
        Assert.IsType<IntervalSchedule>(lidos[3].Schedule);
        Assert.Equal(UrgencyLevel.High, lidos[1].Urgency);
    }

    [Fact]
    public void Lembrete_ciclico_preserva_ancora_faixa_e_regra_de_ausencia()
    {
        var store = new JsonAlarmStore(_arquivo);
        var ancora = new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.FromHours(-3));

        var original = Alarm.New(
            "Beber água",
            new IntervalSchedule(TimeSpan.FromMinutes(45), ancora, new TimeOnly(9, 0), new TimeOnly(18, 0)),
            UrgencyLevel.Whisper) with
        {
            SkipIfIdleFor = TimeSpan.FromMinutes(5),
        };

        store.Save([original]);
        var lido = store.Load().Single();

        Assert.Equal(original, lido);

        var agenda = Assert.IsType<IntervalSchedule>(lido.Schedule);

        // The anchor must round-trip so the cycle survives an app restart. | A âncora precisa sobreviver ao round-trip para o ciclo resistir a reiniciar o app.
        Assert.Equal(ancora, agenda.Anchor);
        Assert.Equal(new TimeOnly(9, 0), agenda.ActiveFrom);
        Assert.Equal(TimeSpan.FromMinutes(5), lido.SkipIfIdleFor);
    }

    [Fact]
    public void Salvar_por_cima_deixa_backup_da_versao_anterior()
    {
        var store = new JsonAlarmStore(_arquivo);

        store.Save([Alarm.New("Primeiro", new DailySchedule(new TimeOnly(7, 0)))]);
        store.Save([Alarm.New("Segundo", new DailySchedule(new TimeOnly(8, 0)))]);

        Assert.True(File.Exists(_arquivo + ".bak"), "A gravação atômica deve deixar um .bak.");
        Assert.False(File.Exists(_arquivo + ".tmp"), "O temporário não pode sobrar.");
        Assert.Equal("Segundo", store.Load().Single().Title);
    }

    [Fact]
    public void Nao_grava_propriedades_calculadas_no_arquivo()
    {
        // Derived (computed) members must not be serialized. | Membros derivados (calculados) não podem ser serializados.
        var store = new JsonAlarmStore(_arquivo);

        store.Save(
        [
            Alarm.New(
                "Alongar",
                new IntervalSchedule(TimeSpan.FromHours(1), DateTimeOffset.UnixEpoch, new TimeOnly(9, 0), new TimeOnly(18, 0)),
                UrgencyLevel.High),
        ]);

        var json = File.ReadAllText(_arquivo);

        Assert.DoesNotContain("\"Profile\"", json);
        Assert.DoesNotContain("\"EffectiveSound\"", json);
        Assert.DoesNotContain("\"DisplayName\"", json);
        Assert.DoesNotContain("\"HasWindow\"", json);
        Assert.DoesNotContain("\"DefaultOption\"", json);

        // What must be present is still present. | O que precisa estar continua lá.
        Assert.Contains("\"Urgency\": \"High\"", json);
        Assert.Contains("\"$type\": \"interval\"", json);
        Assert.Contains("\"ActiveFrom\"", json);
    }

    [Fact]
    public void Arquivo_corrompido_vai_para_quarentena_em_vez_de_ser_sobrescrito()
    {
        File.WriteAllText(_arquivo, "{ isto não é json válido");

        var store = new JsonAlarmStore(_arquivo);
        var lidos = store.Load();

        Assert.Empty(lidos);
        Assert.False(File.Exists(_arquivo), "O arquivo ilegível deve sair do caminho.");

        var quarentena = Directory.GetFiles(_pasta, "alarms.json.corrompido-*");
        Assert.Single(quarentena);
        Assert.Contains("isto não é json válido", File.ReadAllText(quarentena[0]));
    }

    [Fact]
    public void Alarme_desligado_e_com_mensagem_sobrevive_ao_round_trip()
    {
        var store = new JsonAlarmStore(_arquivo);

        var original = Alarm.New("Alongar", new DailySchedule(new TimeOnly(15, 0)), UrgencyLevel.Whisper) with
        {
            Message = "Levanta e anda um pouco",
            IsEnabled = false,
            CustomSoundPath = @"C:\sons\gongo.wav",
        };

        store.Save([original]);
        var lido = store.Load().Single();

        Assert.Equal(original, lido);
        Assert.False(lido.IsEnabled);
        Assert.Equal(@"C:\sons\gongo.wav", lido.CustomSoundPath);
    }
}
