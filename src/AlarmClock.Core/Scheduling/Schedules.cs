using AlarmClock.Core.Localization;

namespace AlarmClock.Core.Scheduling;

/// <summary>A single firing at an absolute instant. | Um disparo único num instante absoluto.</summary>
public sealed record OneTimeSchedule(DateTimeOffset At) : ISchedule
{
    /// <summary>The instant itself, if still in the future. | O próprio instante, se ainda no futuro.</summary>
    public DateTimeOffset? NextOccurrenceAfter(DateTimeOffset from, TimeZoneInfo zone) =>
        At > from ? At : null;

    /// <summary>Localized date and time. | Data e hora localizadas.</summary>
    public string Describe()
    {
        var local = At.ToLocalTime();
        var data = local.ToString(Loc.Get("Fmt_DateLong"), Loc.Culture);
        var hora = local.ToString("HH:mm", Loc.Culture);
        return $"{data} {Loc.Get("Sched_At")} {hora}";
    }
}

/// <summary>Every day at the same wall-clock time. | Todo dia na mesma hora de parede.</summary>
public sealed record DailySchedule(TimeOnly At) : ISchedule
{
    /// <summary>Next occurrence at the set time. | Próxima ocorrência no horário definido.</summary>
    public DateTimeOffset? NextOccurrenceAfter(DateTimeOffset from, TimeZoneInfo zone)
    {
        var date = WallClock.LocalDate(from, zone);

        // Extra days of slack: on a DST turn the same-day candidate may shift back. | Dias de folga extras: numa virada de DST o candidato de hoje pode recuar.
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

    /// <summary>Localized text. | Texto localizado.</summary>
    public string Describe() => Loc.Format("Sched_EveryDay", At.ToString("HH\\:mm", Loc.Culture));
}

/// <summary>On chosen weekdays, at the same wall-clock time. | Nos dias da semana escolhidos, na mesma hora de parede.</summary>
public sealed record WeeklySchedule(WeekDays Days, TimeOnly At) : ISchedule
{
    /// <summary>Next occurrence on an enabled weekday. | Próxima ocorrência num dia da semana ativado.</summary>
    public DateTimeOffset? NextOccurrenceAfter(DateTimeOffset from, TimeZoneInfo zone)
    {
        if (Days == WeekDays.None)
        {
            return null;
        }

        var date = WallClock.LocalDate(from, zone);

        // Eight days cover the whole week even when today's slot has passed. | Oito dias cobrem a semana inteira mesmo quando o horário de hoje já passou.
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

    /// <summary>Localized text (special-cases weekdays/weekend/every day). | Texto localizado (trata dias úteis/fim de semana/todo dia à parte).</summary>
    public string Describe()
    {
        var hora = At.ToString("HH\\:mm", Loc.Culture);

        if (Days == WeekDays.Every)
        {
            return Loc.Format("Sched_EveryDay", hora);
        }

        if (Days == WeekDays.Weekdays)
        {
            return Loc.Format("Sched_Weekdays", hora);
        }

        if (Days == WeekDays.Weekend)
        {
            return Loc.Format("Sched_Weekend", hora);
        }

        var dias = Enumerable.Range(0, 7)
            .Where(i => Days.Includes((DayOfWeek)i))
            .Select(i => Loc.Get($"Day_{i}"));

        return $"{string.Join(", ", dias)}, {hora}";
    }
}
