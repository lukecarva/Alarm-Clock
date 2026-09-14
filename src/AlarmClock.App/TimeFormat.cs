using AlarmClock.Core.Abstractions;
using AlarmClock.Core.Localization;

namespace AlarmClock.App;

/// <summary>Formats relative dates/times in the current language. | Formata datas/horas relativas no idioma atual.</summary>
public static class TimeFormat
{
    /// <summary>
    /// Describes a future instant relative to now: "in 42 min", "today, 07:00",
    /// "tomorrow, …", or the short date (with the year when in another year). | Descreve um instante futuro em relação a agora: "em 42 min", "hoje, 07:00",
    /// "amanhã, …", ou a data curta (com o ano quando for de outro ano).
    /// </summary>
    public static string Relative(DateTimeOffset when, ISystemClock clock)
    {
        var agora = clock.Now;
        var falta = when - agora;

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

        var pattern = dia.Year == hoje.Year ? Loc.Get("Fmt_DateShort") : Loc.Get("Fmt_DateLong");
        return $"{local.ToString(pattern, Loc.Culture)}, {hora}";
    }
}
