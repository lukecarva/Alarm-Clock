using System.Globalization;

namespace AlarmClock.Core.Scheduling;

/// <summary>
/// Um disparo único num instante absoluto. Guarda offset — ao contrário das
/// recorrentes, "dia 3 às 14h" não deve andar se o fuso mudar.
/// </summary>
public sealed record OneTimeSchedule(DateTimeOffset At) : ISchedule
{
    public DateTimeOffset? NextOccurrenceAfter(DateTimeOffset from, TimeZoneInfo zone) =>
        At > from ? At : null;

    public string Describe() =>
        At.ToLocalTime().ToString("dd/MM/yyyy 'às' HH:mm", new CultureInfo("pt-BR"));
}

/// <summary>Todo dia na mesma hora de parede.</summary>
public sealed record DailySchedule(TimeOnly At) : ISchedule
{
    public DateTimeOffset? NextOccurrenceAfter(DateTimeOffset from, TimeZoneInfo zone)
    {
        var date = WallClock.LocalDate(from, zone);

        // Hoje, amanhã e depois. Mais de um dia de folga porque numa virada de
        // horário de verão o candidato de hoje pode acabar deslocado para trás.
        for (var offset = 0; offset <= 2; offset++)
        {
            var candidate = WallClock.Resolve(date.AddDays(offset).ToDateTime(At), zone);
            if (candidate > from)
            {
                return candidate;
            }
        }

        return null;
    }

    public string Describe() => $"Todo dia, {At:HH\\:mm}";
}

/// <summary>Nos dias da semana escolhidos, sempre na mesma hora de parede.</summary>
public sealed record WeeklySchedule(WeekDays Days, TimeOnly At) : ISchedule
{
    public DateTimeOffset? NextOccurrenceAfter(DateTimeOffset from, TimeZoneInfo zone)
    {
        if (Days == WeekDays.None)
        {
            return null;
        }

        var date = WallClock.LocalDate(from, zone);

        // Oito dias cobrem a semana inteira mesmo quando o candidato de hoje já passou.
        for (var offset = 0; offset <= 8; offset++)
        {
            var day = date.AddDays(offset);
            if (!Days.Includes(day.DayOfWeek))
            {
                continue;
            }

            var candidate = WallClock.Resolve(day.ToDateTime(At), zone);
            if (candidate > from)
            {
                return candidate;
            }
        }

        return null;
    }

    public string Describe()
    {
        var hora = At.ToString("HH\\:mm");

        if (Days == WeekDays.Every)
        {
            return $"Todo dia, {hora}";
        }

        if (Days == WeekDays.Weekdays)
        {
            return $"Dias úteis, {hora}";
        }

        if (Days == WeekDays.Weekend)
        {
            return $"Fim de semana, {hora}";
        }

        string[] siglas = ["dom", "seg", "ter", "qua", "qui", "sex", "sáb"];
        var dias = Enumerable.Range(0, 7)
            .Where(i => Days.Includes((DayOfWeek)i))
            .Select(i => siglas[i]);

        return $"{string.Join(", ", dias)}, {hora}";
    }
}
