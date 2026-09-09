using System.Text.Json.Serialization;

namespace AlarmClock.Core.Scheduling;

/// <summary>
/// Dias da semana como flags. <see cref="DayOfWeek"/> não é combinável, e um
/// alarme "seg/qua/sex" precisa ser um valor só para caber no JSON.
/// </summary>
[Flags]
public enum WeekDays
{
    None = 0,
    Sunday = 1 << 0,
    Monday = 1 << 1,
    Tuesday = 1 << 2,
    Wednesday = 1 << 3,
    Thursday = 1 << 4,
    Friday = 1 << 5,
    Saturday = 1 << 6,

    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    Weekend = Saturday | Sunday,
    Every = Weekdays | Weekend,
}

public static class WeekDaysExtensions
{
    public static WeekDays ToFlag(this DayOfWeek day) => (WeekDays)(1 << (int)day);

    public static bool Includes(this WeekDays days, DayOfWeek day) => (days & day.ToFlag()) != 0;
}

/// <summary>
/// Uma regra de recorrência. Toda a complexidade de calendário do projeto mora
/// atrás desta única pergunta — o agendador não sabe o que é "toda terça".
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(OneTimeSchedule), "once")]
[JsonDerivedType(typeof(DailySchedule), "daily")]
[JsonDerivedType(typeof(WeeklySchedule), "weekly")]
public interface ISchedule
{
    /// <summary>
    /// Próxima ocorrência estritamente depois de <paramref name="from"/>, ou
    /// nulo se não houver mais nenhuma.
    /// </summary>
    /// <param name="zone">
    /// Necessário porque agendas recorrentes guardam hora de parede, não
    /// instante absoluto: "todo dia às 7h" continua sendo 7h depois que entra
    /// o horário de verão.
    /// </param>
    DateTimeOffset? NextOccurrenceAfter(DateTimeOffset from, TimeZoneInfo zone);

    /// <summary>Texto curto para a lista de alarmes. "Todo dia, 07:00".</summary>
    string Describe();
}
