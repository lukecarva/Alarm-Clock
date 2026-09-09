namespace AlarmClock.Core.Tests;

/// <summary>
/// Fusos construídos à mão. Usar "E. South America Standard Time" de verdade
/// seria pior: o Windows não carrega regras históricas de forma confiável, o
/// Brasil não tem mais horário de verão desde 2019, e o teste passaria a
/// depender da base de fusos da máquina. Aqui a regra é explícita e o resultado
/// é o mesmo na sua máquina e no CI.
/// </summary>
public static class TestZones
{
    /// <summary>
    /// Hemisfério sul: padrão −03:00, com +1h de horário de verão entre
    /// 15/out e 15/fev.
    /// </summary>
    /// <remarks>
    /// Consequências que os testes exploram:
    /// <list type="bullet">
    /// <item>15/out, 00:00–00:59 <b>não existe</b> (o relógio pula para 01:00).</item>
    /// <item>14/fev, 23:00–23:59 <b>acontece duas vezes</b> (o relógio volta).</item>
    /// </list>
    /// </remarks>
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
