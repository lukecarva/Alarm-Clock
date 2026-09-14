using AlarmClock.Core.Abstractions;
using AlarmClock.Core.Localization;

namespace AlarmClock.App;

/// <summary>
/// Formatação de datas/horas relativas, num lugar só e no idioma atual. Antes a
/// mesma lógica de "em 42 min / hoje / amanhã / dd/MM" estava repetida na lista,
/// no rodapé e no tooltip da bandeja, cada cópia com um detalhe diferente.
/// </summary>
public static class TimeFormat
{
    /// <summary>
    /// Um instante futuro descrito em relação a agora: "in 42 min" / "em 42 min",
    /// "today, 07:00" / "hoje, 07:00", "tomorrow, ..." / "amanhã, ...", ou a data
    /// curta ("MM/dd" / "dd/MM") — com o ano quando cai em outro ano.
    /// </summary>
    public static string Relative(DateTimeOffset when, ISystemClock clock)
    {
        var agora = clock.Now;
        var falta = when - agora;

        // Abaixo de uma hora, contagem em minutos é o que interessa. Mínimo de 1
        // para nunca mostrar "em 0 min".
        if (falta < TimeSpan.FromHours(1))
        {
            return Loc.Format("Rel_InMin", Math.Max(1, (int)falta.TotalMinutes));
        }

        var local = TimeZoneInfo.ConvertTime(when, clock.LocalTimeZone);
        var hoje = TimeZoneInfo.ConvertTime(agora, clock.LocalTimeZone).Date;
        var dia = local.Date;
        var hora = local.ToString("HH:mm", Loc.Culture);

        if (dia == hoje)
        {
            return Loc.Format("Rel_Today", hora);
        }

        if (dia == hoje.AddDays(1))
        {
            return Loc.Format("Rel_Tomorrow", hora);
        }

        // Ano diferente merece o ano no texto; no mesmo ano, o formato curto basta.
        var pattern = dia.Year == hoje.Year ? Loc.Get("Fmt_DateShort") : Loc.Get("Fmt_DateLong");
        return $"{local.ToString(pattern, Loc.Culture)}, {hora}";
    }
}
