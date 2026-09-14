using AlarmClock.Core.Abstractions;
using AlarmClock.Core.Model;
using AlarmClock.Core.Persistence;

namespace AlarmClock.App.Tests;

/// <summary>Fixed clock for tests. | Relógio fixo para os testes.</summary>
public sealed class FixedClock(DateTimeOffset now, TimeZoneInfo? zone = null) : ISystemClock
{
    public DateTimeOffset Now { get; set; } = now;

    public TimeZoneInfo LocalTimeZone { get; } = zone ?? TimeZoneInfo.Utc;
}

/// <summary>In-memory alarm store for tests. | Armazenamento de alarmes em memória para os testes.</summary>
public sealed class InMemoryAlarmStore : IAlarmStore
{
    private List<Alarm> _items = [];

    /// <summary>How many times Save was called. | Quantas vezes Save foi chamado.</summary>
    public int SaveCount { get; private set; }

    public IReadOnlyList<Alarm> Load() => _items;

    public void Save(IEnumerable<Alarm> alarms)
    {
        _items = [.. alarms];
        SaveCount++;
    }
}
