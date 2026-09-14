using AlarmClock.Core.Model;
using AlarmClock.Core.Scheduling;

namespace AlarmClock.Core.Tests;

public class HabitCatalogTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 10, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Todos_os_habitos_tem_chave_unica()
    {
        var chaves = HabitCatalog.All.Select(h => h.Key).ToList();
        Assert.Equal(chaves.Count, chaves.Distinct().Count());
    }

    [Fact]
    public void BuildAlarm_marca_a_chave_do_habito_e_e_por_intervalo()
    {
        var agua = HabitCatalog.Find("water")!;

        var alarme = HabitCatalog.BuildAlarm(agua, 45, Now);

        Assert.Equal("water", alarme.HabitKey);
        Assert.Equal(agua.Urgency, alarme.Urgency);
        Assert.Equal(HabitCatalog.SkipIfIdleFor, alarme.SkipIfIdleFor);

        var agenda = Assert.IsType<IntervalSchedule>(alarme.Schedule);
        Assert.Equal(TimeSpan.FromMinutes(45), agenda.Every);
        Assert.Equal(HabitCatalog.WindowFrom, agenda.ActiveFrom);
        Assert.Equal(HabitCatalog.WindowTo, agenda.ActiveTo);
    }

    [Fact]
    public void Ajustar_so_o_intervalo_reinicia_o_ciclo_mas_mantem_o_Id()
    {
        var agua = HabitCatalog.Find("water")!;
        var original = HabitCatalog.BuildAlarm(agua, 45, Now);
        var ancora = ((IntervalSchedule)original.Schedule).Anchor;

        var maisTarde = Now.AddHours(3);
        var ajustado = HabitCatalog.BuildAlarm(agua, 30, maisTarde, original);

        // Updates in place (same Id), but changing the interval resets the anchor. | Atualiza no lugar (mesmo Id), mas mudar o intervalo reinicia a âncora.
        Assert.Equal(original.Id, ajustado.Id);
        Assert.Equal(TimeSpan.FromMinutes(30), ((IntervalSchedule)ajustado.Schedule).Every);
        Assert.NotEqual(ancora, ((IntervalSchedule)ajustado.Schedule).Anchor);
    }

    [Fact]
    public void Reconstruir_com_o_mesmo_intervalo_preserva_a_ancora()
    {
        var agua = HabitCatalog.Find("water")!;
        var original = HabitCatalog.BuildAlarm(agua, 45, Now);
        var ancora = ((IntervalSchedule)original.Schedule).Anchor;

        var depois = HabitCatalog.BuildAlarm(agua, 45, Now.AddHours(3), original);

        Assert.Equal(ancora, ((IntervalSchedule)depois.Schedule).Anchor);
    }
}
