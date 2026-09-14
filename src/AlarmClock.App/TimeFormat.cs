using System.Globalization;
using AlarmClock.Core.Abstractions;

namespace AlarmClock.App;

/// <summary>
/// Formatação de datas/horas em pt-BR, num lugar só. Antes a mesma lógica de
/// "em 42 min / hoje / amanhã / dd/MM" estava repetida na lista, no rodapé e no
/// tooltip da bandeja, cada cópia com um detalhe diferente.
/// </summary>
public static class TimeFormat
{
    public static CultureInfo PtBr { get; } = new("pt-BR");

    /// <summary>
    /// Um instante futuro descrito em relação a agora: "em 42 min", "hoje,
    /// 07:00", "amanhã, 07:00", "14/09, 07:00" ou, em outro ano, "14/09/2027,
    /// 07:00".
    /// </summary>
    public static string Relative(DateTimeOffset when, ISystemClock clock)
    {
        var agora = clock.Now;
        var falta = when - agora;

        // Abaixo de uma hora, contagem em minutos é o que interessa. Mínimo de 1
        // para nunca mostrar "em 0 min".
        if (falta < TimeSpan.FromHours(1))
        {
            return $"em {Math.Max(1, (int)falta.TotalMinutes)} min";
        }

        var local = TimeZoneInfo.ConvertTime(when, clock.LocalTimeZone);
        var hoje = TimeZoneInfo.ConvertTime(agora, clock.LocalTimeZone).Date;
        var dia = local.Date;
        var hora = local.ToString("HH:mm", PtBr);

        if (dia == hoje)
        {
            return $"hoje, {hora}";
        }

        if (dia == hoje.AddDays(1))
        {
            return $"amanhã, {hora}";
        }

        // Ano diferente merece o ano no texto; no mesmo ano, dd/MM basta.
        var dataFmt = dia.Year == hoje.Year ? "dd/MM" : "dd/MM/yyyy";
        return $"{local.ToString(dataFmt, PtBr)}, {hora}";
    }
}
