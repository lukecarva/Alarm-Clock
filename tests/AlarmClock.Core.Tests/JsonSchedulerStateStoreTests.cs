using AlarmClock.Core.Model;
using AlarmClock.Core.Persistence;
using AlarmClock.Core.Scheduling;

namespace AlarmClock.Core.Tests;

public class JsonSchedulerStateStoreTests : IDisposable
{
    private readonly string _pasta;
    private readonly string _arquivo;

    public JsonSchedulerStateStoreTests()
    {
        _pasta = Path.Combine(Path.GetTempPath(), "AlarmClockTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_pasta);
        _arquivo = Path.Combine(_pasta, "scheduler-state.json");
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
    public void Arquivo_inexistente_carrega_estado_vazio()
    {
        var store = new JsonSchedulerStateStore(_arquivo);

        var estado = store.Load();

        Assert.True(estado.IsEmpty);
    }

    [Fact]
    public void Round_trip_preserva_adiamentos_e_escaladas()
    {
        var store = new JsonSchedulerStateStore(_arquivo);
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var quando = new DateTimeOffset(2026, 6, 10, 9, 30, 0, TimeSpan.FromHours(-3));
        var prazo = new DateTimeOffset(2026, 6, 10, 9, 45, 0, TimeSpan.FromHours(-3));

        var original = new SchedulerState
        {
            Snoozes = [new SnoozeState(id1, 2, quando)],
            Escalations = [new EscalationSnapshot(id2, UrgencyLevel.High, prazo)],
        };

        store.Save(original);
        var lido = store.Load();

        var soneca = Assert.Single(lido.Snoozes);
        Assert.Equal(id1, soneca.AlarmId);
        Assert.Equal(2, soneca.Count);
        Assert.Equal(quando, soneca.DueAt);

        var escalada = Assert.Single(lido.Escalations);
        Assert.Equal(id2, escalada.AlarmId);
        Assert.Equal(UrgencyLevel.High, escalada.Level);
        Assert.Equal(prazo, escalada.IgnoreDeadline);
    }

    [Fact]
    public void Adiamento_sem_hora_pendente_sobrevive_ao_round_trip()
    {
        // A consumed snooze keeps its counter but has no pending wake time. | Um adiamento já consumido mantém o contador mas não tem hora de retorno.
        var store = new JsonSchedulerStateStore(_arquivo);
        var id = Guid.NewGuid();

        store.Save(new SchedulerState { Snoozes = [new SnoozeState(id, 1, null)] });
        var lido = store.Load();

        var soneca = Assert.Single(lido.Snoozes);
        Assert.Equal(1, soneca.Count);
        Assert.Null(soneca.DueAt);
    }

    [Fact]
    public void Salvar_por_cima_deixa_backup_da_versao_anterior()
    {
        var store = new JsonSchedulerStateStore(_arquivo);

        store.Save(new SchedulerState { Snoozes = [new SnoozeState(Guid.NewGuid(), 1, null)] });
        store.Save(new SchedulerState());

        Assert.True(File.Exists(_arquivo + ".bak"), "A gravação atômica deve deixar um .bak.");
        Assert.False(File.Exists(_arquivo + ".tmp"), "O temporário não pode sobrar.");
        Assert.True(store.Load().IsEmpty);
    }

    [Fact]
    public void Enum_de_nivel_e_gravado_como_nome()
    {
        var store = new JsonSchedulerStateStore(_arquivo);

        store.Save(new SchedulerState
        {
            Escalations = [new EscalationSnapshot(Guid.NewGuid(), UrgencyLevel.Critical, null)],
        });

        Assert.Contains("\"Critical\"", File.ReadAllText(_arquivo));
    }

    [Fact]
    public void Arquivo_corrompido_vai_para_quarentena_em_vez_de_ser_sobrescrito()
    {
        File.WriteAllText(_arquivo, "{ isto não é json válido");

        var store = new JsonSchedulerStateStore(_arquivo);
        var estado = store.Load();

        Assert.True(estado.IsEmpty);
        Assert.False(File.Exists(_arquivo), "O arquivo ilegível deve sair do caminho.");

        var quarentena = Directory.GetFiles(_pasta, "scheduler-state.json.corrompido-*");
        Assert.Single(quarentena);
    }
}
