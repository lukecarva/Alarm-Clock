using AlarmClock.Core.Localization;
using AlarmClock.Core.Scheduling;

namespace AlarmClock.Core.Model;

/// <summary>A one-click daily reminder preset. | Um preset de lembrete de dia a dia, de um clique.</summary>
/// <param name="Key">Stable id, stored in <see cref="Alarm.HabitKey"/>. | Id estável, salvo em <see cref="Alarm.HabitKey"/>.</param>
/// <param name="Emoji">Short icon for the list. | Ícone curto para a lista.</param>
/// <param name="DefaultMinutes">Suggested interval. | Intervalo sugerido.</param>
/// <param name="Urgency">How intrusive the reminder is. | Quão intrusivo é o lembrete.</param>
public sealed record HabitDefinition(string Key, string Emoji, int DefaultMinutes, UrgencyLevel Urgency)
{
    /// <summary>Display name in the current language. | Nome exibido no idioma atual.</summary>
    public string Name => Loc.Get($"Habit_{Key}_Name");

    /// <summary>Short description in the current language. | Descrição curta no idioma atual.</summary>
    public string Note => Loc.Get($"Habit_{Key}_Note");
}

/// <summary>The built-in daily reminders. | Os lembretes de dia a dia embutidos.</summary>
public static class HabitCatalog
{
    /// <summary>Reminders stay quiet outside this daily range. | Fora desta faixa diária os lembretes ficam quietos.</summary>
    public static readonly TimeOnly WindowFrom = new(8, 0);
    public static readonly TimeOnly WindowTo = new(22, 0);

    /// <summary>Idle this long means you're away; the reminder is skipped. | Parado por este tempo significa ausente; o lembrete é pulado.</summary>
    public static readonly TimeSpan SkipIfIdleFor = TimeSpan.FromMinutes(5);

    /// <summary>All presets, in display order. | Todos os presets, na ordem de exibição.</summary>
    public static IReadOnlyList<HabitDefinition> All { get; } =
    [
        new("water", "💧", 45, UrgencyLevel.Whisper),
        new("standup", "🧍", 60, UrgencyLevel.Normal),
        new("eyes", "👀", 20, UrgencyLevel.Whisper),
        new("stretch", "🤸", 90, UrgencyLevel.Normal),
    ];

    /// <summary>Finds a preset by key. | Encontra um preset pela chave.</summary>
    public static HabitDefinition? Find(string key) => All.FirstOrDefault(h => h.Key == key);

    /// <summary>
    /// Builds a habit's alarm at the given interval, keeping the running cycle's
    /// anchor and id when possible. | Monta o alarme de um hábito no intervalo dado, preservando a âncora do ciclo
    /// em curso e o id quando possível.
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
