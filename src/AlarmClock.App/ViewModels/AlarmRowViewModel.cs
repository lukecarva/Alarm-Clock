using System.Windows;
using System.Windows.Media;
using AlarmClock.Core.Abstractions;
using AlarmClock.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AlarmClock.App.ViewModels;

/// <summary>Uma linha da lista de alarmes.</summary>
public sealed partial class AlarmRowViewModel : ObservableObject
{
    private readonly ISystemClock _clock;
    private readonly Action<bool> _onToggled;

    // readonly: só muda dentro do construtor (guarda o callback durante a carga
    // inicial de IsEnabled). C# permite reatribuir readonly no próprio ctor.
    private readonly bool _suprimirCallback;

    public AlarmRowViewModel(Alarm alarm, ISystemClock clock, Action<bool> onToggled)
    {
        Alarm = alarm;
        _clock = clock;
        _onToggled = onToggled;

        _suprimirCallback = true;
        IsEnabled = alarm.IsEnabled;
        _suprimirCallback = false;

        AccentBrush = Application.Current.TryFindResource($"Brush.Urgency.{alarm.Urgency}") as Brush
                      ?? Brushes.Gray;
    }

    public Alarm Alarm { get; }

    public string Title => Alarm.Title;

    public string ScheduleText => Alarm.Schedule.Describe();

    public string UrgencyName => Alarm.Profile.DisplayName;

    public Brush AccentBrush { get; }

    [ObservableProperty]
    private bool _isEnabled;

    partial void OnIsEnabledChanged(bool value)
    {
        if (!_suprimirCallback)
        {
            _onToggled(value);
        }
    }

    public string NextText
    {
        get
        {
            if (!Alarm.IsEnabled)
            {
                return "desligado";
            }

            var proxima = Alarm.Schedule.NextOccurrenceAfter(_clock.Now, _clock.LocalTimeZone);

            return proxima is null
                ? "sem próxima ocorrência"
                : TimeFormat.Relative(proxima.Value, _clock);
        }
    }

    /// <summary>Recalcula os textos relativos ("em 42 min") sem recriar a linha.</summary>
    public void RefreshNext() => OnPropertyChanged(nameof(NextText));
}
