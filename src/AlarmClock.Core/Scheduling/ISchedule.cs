using System.Text.Json.Serialization;

namespace AlarmClock.Core.Scheduling;

/// <summary>Days of the week as combinable flags. | Dias da semana como flags combináveis.</summary>
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
    /// <summary>Converts a day into its flag. | Converte um dia na sua flag.</summary>
    public static WeekDays ToFlag(this DayOfWeek day) => (WeekDays)(1 << (int)day);

    /// <summary>Whether the set contains the given day. | Se o conjunto contém o dia informado.</summary>
    public static bool Includes(this WeekDays days, DayOfWeek day) => (days & day.ToFlag()) != 0;
}

/// <summary>A recurrence rule for an alarm. | Uma regra de recorrência de um alarme.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(OneTimeSchedule), "once")]
[JsonDerivedType(typeof(DailySchedule), "daily")]
[JsonDerivedType(typeof(WeeklySchedule), "weekly")]
[JsonDerivedType(typeof(IntervalSchedule), "interval")]
public interface ISchedule
{
    /// <summary>
    /// Next occurrence strictly after <paramref name="from"/>, or null if none. | Próxima ocorrência estritamente depois de <paramref name="from"/>, ou nulo se não houver.
    /// </summary>
    /// <param name="zone">Local time zone, used to resolve wall-clock recurrences and DST. | Fuso local, usado para resolver recorrências em hora de parede e o horário de verão.</param>
    DateTimeOffset? NextOccurrenceAfter(DateTimeOffset from, TimeZoneInfo zone);

    /// <summary>Short text for the alarm list, e.g. "Every day, 07:00". | Texto curto para a lista, ex.: "Todo dia, 07:00".</summary>
    string Describe();
}
