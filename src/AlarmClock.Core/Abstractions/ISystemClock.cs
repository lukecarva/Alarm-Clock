namespace AlarmClock.Core.Abstractions;

/// <summary>Provides the current time to the domain. | Fornece o horário atual ao domínio.</summary>
public interface ISystemClock
{
    /// <summary>Current instant, with local offset. | Instante atual, com o offset local.</summary>
    DateTimeOffset Now { get; }

    /// <summary>Local time zone. | Fuso horário local.</summary>
    TimeZoneInfo LocalTimeZone { get; }
}

/// <summary>System clock backed by Windows. | Relógio do sistema, ligado ao Windows.</summary>
public sealed class SystemClock : ISystemClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;

    public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
}
