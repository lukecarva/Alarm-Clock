namespace AlarmClock.Core.Scheduling;

/// <summary>
/// Resolves a wall-clock time into an absolute instant, handling DST. | Resolve uma hora de parede num instante absoluto, tratando o horário de verão.
/// </summary>
internal static class WallClock
{
    /// <summary>Upper bound for the loop that skips a spring-forward gap. | Limite do laço que pula o buraco da primavera.</summary>
    private const int MaxGapMinutes = 24 * 60;

    /// <summary>
    /// Converts a wall-clock time to an instant; on a nonexistent time fires at the
    /// end of the gap, on an ambiguous time picks the first pass. | Converte uma hora de parede num instante; em hora inexistente dispara no fim
    /// do buraco, em hora ambígua escolhe a primeira passagem.
    /// </summary>
    public static DateTimeOffset Resolve(DateTime wall, TimeZoneInfo zone)
    {
        wall = DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);

        var guard = 0;
        while (zone.IsInvalidTime(wall) && guard++ < MaxGapMinutes)
        {
            wall = wall.AddMinutes(1);
        }

        if (zone.IsAmbiguousTime(wall))
        {
            var offsets = zone.GetAmbiguousTimeOffsets(wall);
            return new DateTimeOffset(wall, offsets.Max());
        }

        return new DateTimeOffset(wall, zone.GetUtcOffset(wall));
    }

    /// <summary>Local wall-clock date of an instant in the given zone. | Data de parede de um instante no fuso informado.</summary>
    public static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
}
