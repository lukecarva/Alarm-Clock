namespace AlarmClock.Core.Scheduling;

/// <summary>
/// Converte uma hora de parede ("15 de outubro às 00:30") no instante absoluto
/// correspondente. É aqui que o horário de verão é resolvido, num lugar só, em
/// vez de em cada tipo de agenda.
/// </summary>
internal static class WallClock
{
    /// <summary>
    /// Maior salto de horário de verão que se espera encontrar. Serve como
    /// limite do laço que procura o fim do buraco da primavera.
    /// </summary>
    private const int MaxGapMinutes = 24 * 60;

    public static DateTimeOffset Resolve(DateTime wall, TimeZoneInfo zone)
    {
        wall = DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);

        // Buraco da primavera: às 00:00 o relógio pula para 01:00 e a hora
        // pedida simplesmente não existe naquele dia. Dispara no instante em
        // que o relógio pula — adiantado por alguns minutos é muito melhor que
        // não tocar.
        var guard = 0;
        while (zone.IsInvalidTime(wall) && guard++ < MaxGapMinutes)
        {
            wall = wall.AddMinutes(1);
        }

        // Dobra do outono: a mesma hora de parede acontece duas vezes. Escolhe
        // a primeira — que é a do maior offset, ainda no horário de verão —
        // para o alarme não atrasar uma hora inteira.
        if (zone.IsAmbiguousTime(wall))
        {
            var offsets = zone.GetAmbiguousTimeOffsets(wall);
            return new DateTimeOffset(wall, offsets.Max());
        }

        return new DateTimeOffset(wall, zone.GetUtcOffset(wall));
    }

    /// <summary>Data de parede correspondente a um instante, no fuso dado.</summary>
    public static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
}
