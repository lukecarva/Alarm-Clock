namespace AlarmClock.Core.Scheduling;

/// <summary>
/// A cada N minutos, opcionalmente só dentro de uma faixa de horário.
/// É a agenda dos lembretes de saúde: beber água, levantar, descansar a vista.
/// </summary>
/// <remarks>
/// Diferente de <see cref="DailySchedule"/> e <see cref="WeeklySchedule"/>, esta
/// agenda é de <b>duração absoluta</b>: "a cada 45 minutos" são 45 minutos reais,
/// e a conta é feita em instantes, não em hora de parede. Na virada do horário de
/// verão isso significa que o ciclo simplesmente continua — que é o
/// comportamento certo. Só a <see cref="ActiveFrom"/>/<see cref="ActiveTo"/> é
/// hora de parede, porque "só me lembre entre 9h e 18h" fala do relógio.
/// </remarks>
/// <param name="Every">Intervalo entre disparos.</param>
/// <param name="Anchor">
/// Instante em que o ciclo começou a contar. Guardado para o ritmo sobreviver a
/// reinício do app: sem âncora, fechar e abrir o programa reiniciaria a contagem.
/// </param>
/// <param name="ActiveFrom">Início da faixa ativa. Nulo = o dia inteiro.</param>
/// <param name="ActiveTo">Fim da faixa ativa (exclusivo).</param>
public sealed record IntervalSchedule(
    TimeSpan Every,
    DateTimeOffset Anchor,
    TimeOnly? ActiveFrom = null,
    TimeOnly? ActiveTo = null) : ISchedule
{
    /// <summary>Limite do laço que procura a próxima abertura de janela.</summary>
    private const int MaxWindowHops = 400;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasWindow => ActiveFrom is not null && ActiveTo is not null;

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

            // Fora da faixa: pula para a próxima abertura e volta a encaixar no
            // ritmo do ciclo, para os disparos não desalinharem dia após dia.
            var abertura = NextWindowOpening(candidato, zone);
            if (abertura is null)
            {
                return null;
            }

            candidato = AlignToGrid(abertura.Value - TimeSpan.FromTicks(1));
        }

        return null;
    }

    /// <summary>Primeiro instante do ciclo estritamente depois de <paramref name="from"/>.</summary>
    private DateTimeOffset AlignToGrid(DateTimeOffset from)
    {
        var decorrido = from - Anchor;
        var ciclos = Math.Floor(decorrido / Every) + 1;

        var candidato = Anchor + (Every * ciclos);

        // Bordas de arredondamento: garante "estritamente depois".
        while (candidato <= from)
        {
            candidato += Every;
        }

        return candidato;
    }

    private bool IsInWindow(DateTimeOffset instant, TimeZoneInfo zone)
    {
        if (!HasWindow)
        {
            return true;
        }

        var hora = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

        // Faixa que atravessa a meia-noite (22:00–06:00) precisa da lógica ao contrário.
        return ActiveFrom!.Value <= ActiveTo!.Value
            ? hora >= ActiveFrom.Value && hora < ActiveTo.Value
            : hora >= ActiveFrom.Value || hora < ActiveTo.Value;
    }

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

    public string Describe()
    {
        var texto = Localization.Loc.Format("Sched_Every", FormatEvery());

        return HasWindow
            ? $"{texto}, {ActiveFrom:HH\\:mm} {Localization.Loc.Get("Sched_WindowSep")} {ActiveTo:HH\\:mm}"
            : texto;
    }

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
