namespace AlarmClock.Core.Tests;

/// <summary>Hand-built time zones with explicit rules, for deterministic DST tests. | Fusos construídos à mão com regras explícitas, para testes de DST determinísticos.</summary>
public static class TestZones
{
    /// <summary>Southern-hemisphere zone: −03:00, +1h DST between Oct 15 and Feb 15. | Fuso do hemisfério sul: −03:00, +1h de horário de verão entre 15/out e 15/fev.</summary>
    public static TimeZoneInfo SouthernDst { get; } = BuildSouthernDst();

    private static TimeZoneInfo BuildSouthernDst()
    {
        var inicio = TimeZoneInfo.TransitionTime.CreateFixedDateRule(
            timeOfDay: new DateTime(1, 1, 1, 0, 0, 0),
            month: 10,
            day: 15);

        var fim = TimeZoneInfo.TransitionTime.CreateFixedDateRule(
            timeOfDay: new DateTime(1, 1, 1, 0, 0, 0),
            month: 2,
            day: 15);

        var regra = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            dateStart: DateTime.MinValue.Date,
            dateEnd: DateTime.MaxValue.Date,
            daylightDelta: TimeSpan.FromHours(1),
            daylightTransitionStart: inicio,
            daylightTransitionEnd: fim);

        return TimeZoneInfo.CreateCustomTimeZone(
            id: "Teste/HorarioDeVerao",
            baseUtcOffset: TimeSpan.FromHours(-3),
            displayName: "Teste (horário de verão)",
            standardDisplayName: "Padrão de Teste",
            daylightDisplayName: "Verão de Teste",
            adjustmentRules: [regra]);
    }
}
