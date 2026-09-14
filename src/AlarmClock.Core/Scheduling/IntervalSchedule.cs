namespace AlarmClock.Core.Scheduling;

/// <summary>
/// Fires every N minutes, optionally only within a daily time range. | Dispara a cada N minutos, opcionalmente só dentro de uma faixa de horário.
/// </summary>
/// <param name="Every">Interval between firings (absolute duration). | Intervalo entre disparos (duração absoluta).</param>
/// <param name="Anchor">Instant the cycle started counting from. | Instante em que o ciclo começou a contar.</param>
/// <param name="ActiveFrom">Start of the active range. Null = all day. | Início da faixa ativa. Nulo = o dia inteiro.</param>
/// <param name="ActiveTo">End of the active range (exclusive). | Fim da faixa ativa (exclusivo).</param>
public sealed record IntervalSchedule(
    TimeSpan Every,
    DateTimeOffset Anchor,
    TimeOnly? ActiveFrom = null,
    TimeOnly? ActiveTo = null) : ISchedule
{
    /// <summary>Upper bound for the loop that seeks the next window opening. | Limite do laço que procura a próxima abertura de janela.</summary>
    private const int MaxWindowHops = 400;

    /// <summary>Whether an active time range is set. | Se há uma faixa de horário definida.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasWindow => ActiveFrom is not null && ActiveTo is not null;

    /// <summary>Next firing after <paramref name="from"/>, skipping outside the range. | Próximo disparo depois de <paramref name="from"/>, pulando fora da faixa.</summary>
    public DateTimeOffset? NextOccurrenceAfter(DateTimeOffset from, TimeZoneInfo zone)
    {
        if (Every <= TimeSpan.Zero)
        {
            return null;
        }

        var candidato = AlignToGrid(from);

        for (var salto = 0; salto < MaxWindowHops; salto++)
        {
            if (IsInWindow(candidato, zone))
            {
                return candidato;
            }

            var abertura = NextWindowOpening(candidato, zone);
            if (abertura is null)
            {
                return null;
            }

            candidato = AlignToGrid(abertura.Value - TimeSpan.FromTicks(1));
        }

        return null;
    }

    /// <summary>First cycle instant strictly after <paramref name="from"/>. | Primeiro instante do ciclo estritamente depois de <paramref name="from"/>.</summary>
    private DateTimeOffset AlignToGrid(DateTimeOffset from)
    {
        var decorrido = from - Anchor;
        var ciclos = Math.Floor(decorrido / Every) + 1;

        var candidato = Anchor + (Every * ciclos);

        while (candidato <= from)
        {
            candidato += Every;
        }

        return candidato;
    }

    /// <summary>Whether an instant falls inside the active range. | Se um instante cai dentro da faixa ativa.</summary>
    private bool IsInWindow(DateTimeOffset instant, TimeZoneInfo zone)
    {
        if (!HasWindow)
        {
            return true;
        }

        var hora = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

        // Range crossing midnight (22:00-06:00) needs the inverted test. | Faixa que atravessa a meia-noite (22:00-06:00) precisa do teste invertido.
        return ActiveFrom!.Value <= ActiveTo!.Value
            ? hora >= ActiveFrom.Value && hora < ActiveTo.Value
            : hora >= ActiveFrom.Value || hora < ActiveTo.Value;
    }

    /// <summary>Next time the active range opens after <paramref name="from"/>. | Próxima vez que a faixa ativa abre depois de <paramref name="from"/>.</summary>
    private DateTimeOffset? NextWindowOpening(DateTimeOffset from, TimeZoneInfo zone)
    {
        if (!HasWindow)
        {
            return null;
        }

        var data = WallClock.LocalDate(from, zone);

        for (var dia = 0; dia <= 2; dia++)
        {
            var abertura = WallClock.Resolve(data.AddDays(dia).ToDateTime(ActiveFrom!.Value), zone);
            if (abertura > from)
            {
                return abertura;
            }
        }

        return null;
    }

    /// <summary>Short text, e.g. "Every 45 min, 09:00 to 18:00". | Texto curto, ex.: "A cada 45 min, 09:00 às 18:00".</summary>
    public string Describe()
    {
        var texto = Localization.Loc.Format("Sched_Every", FormatEvery());

        return HasWindow
            ? $"{texto}, {ActiveFrom:HH\\:mm} {Localization.Loc.Get("Sched_WindowSep")} {ActiveTo:HH\\:mm}"
            : texto;
    }

    /// <summary>Formats the interval as "45 min", "1h" or "1h30". | Formata o intervalo como "45 min", "1h" ou "1h30".</summary>
    private string FormatEvery()
    {
        if (Every < TimeSpan.FromHours(1))
        {
            return $"{Every.TotalMinutes:0} min";
        }

        var horas = (int)Every.TotalHours;
        var minutos = Every.Minutes;

        return minutos == 0 ? $"{horas}h" : $"{horas}h{minutos:00}";
    }
}
