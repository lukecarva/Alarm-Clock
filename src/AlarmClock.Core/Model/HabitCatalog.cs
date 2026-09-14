using AlarmClock.Core.Localization;
using AlarmClock.Core.Scheduling;

namespace AlarmClock.Core.Model;

/// <summary>
/// Um lembrete de dia a dia pronto para ativar num clique. É só um preset de
/// <see cref="Alarm"/> por intervalo — o trabalho pesado (ciclo, faixa de
/// horário, não avisar cadeira vazia) já existe no agendamento.
/// </summary>
/// <param name="Key">Identificador estável, salvo em <see cref="Alarm.HabitKey"/>.</param>
/// <param name="Emoji">Ícone curto para a lista.</param>
/// <param name="DefaultMinutes">Intervalo sugerido.</param>
/// <param name="Urgency">Quão intrusivo é o lembrete.</param>
public sealed record HabitDefinition(string Key, string Emoji, int DefaultMinutes, UrgencyLevel Urgency)
{
    /// <summary>Nome exibido, no idioma atual.</summary>
    public string Name => Loc.Get($"Habit_{Key}_Name");

    /// <summary>Descrição curta, no idioma atual.</summary>
    public string Note => Loc.Get($"Habit_{Key}_Note");
}

public static class HabitCatalog
{
    /// <summary>Fora dessa faixa o lembrete não incomoda (madrugada).</summary>
    public static readonly TimeOnly WindowFrom = new(8, 0);
    public static readonly TimeOnly WindowTo = new(22, 0);

    /// <summary>Parado tempo demais = você não está; o lembrete é pulado.</summary>
    public static readonly TimeSpan SkipIfIdleFor = TimeSpan.FromMinutes(5);

    public static IReadOnlyList<HabitDefinition> All { get; } =
    [
        new("water", "💧", 45, UrgencyLevel.Whisper),
        new("standup", "🧍", 60, UrgencyLevel.Normal),
        new("eyes", "👀", 20, UrgencyLevel.Whisper),
        new("stretch", "🤸", 90, UrgencyLevel.Normal),
    ];

    public static HabitDefinition? Find(string key) => All.FirstOrDefault(h => h.Key == key);

    /// <summary>
    /// Monta o alarme de um hábito com o intervalo pedido. Preserva a âncora do
    /// ciclo em curso (se houver) para não reiniciar a contagem a cada ajuste, e
    /// o Id para atualizar no lugar em vez de duplicar.
    /// </summary>
    public static Alarm BuildAlarm(
        HabitDefinition habit,
        int minutes,
        DateTimeOffset now,
        Alarm? existing = null)
    {
        var interval = TimeSpan.FromMinutes(minutes);
        var anchor = existing?.Schedule is IntervalSchedule antigo && antigo.Every == interval
            ? antigo.Anchor
            : now;

        return new Alarm
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            Title = habit.Name,
            Schedule = new IntervalSchedule(interval, anchor, WindowFrom, WindowTo),
            Urgency = habit.Urgency,
            SkipIfIdleFor = SkipIfIdleFor,
            HabitKey = habit.Key,
        };
    }
}
