using System.Windows;
using System.Windows.Media;
using AlarmClock.Core.Abstractions;
using AlarmClock.Core.Localization;
using AlarmClock.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AlarmClock.App.ViewModels;

/// <summary>One row of the alarm list. | Uma linha da lista de alarmes.</summary>
public sealed partial class AlarmRowViewModel : ObservableObject
{
    private readonly ISystemClock _clock;
    private readonly Action<bool> _onToggled;

    // Guards the toggle callback during the initial load of IsEnabled. | Protege o callback do toggle durante a carga inicial de IsEnabled.
    private readonly bool _suprimirCallback;

    public AlarmRowViewModel(Alarm alarm, ISystemClock clock, Action<bool> onToggled)
    {
        Alarm = alarm;
        _clock = clock;
        _onToggled = onToggled;

        _suprimirCallback = true;
        IsEnabled = alarm.IsEnabled;
        _suprimirCallback = false;

        AccentBrush = Application.Current?.TryFindResource($"Brush.Urgency.{alarm.Urgency}") as Brush
                      ?? Brushes.Gray;
    }

    /// <summary>The underlying alarm. | O alarme por trás da linha.</summary>
    public Alarm Alarm { get; }

    /// <summary>Alarm title. | Título do alarme.</summary>
    public string Title => Alarm.Title;

    /// <summary>Schedule description. | Descrição da agenda.</summary>
    public string ScheduleText => Alarm.Schedule.Describe();

    /// <summary>Localized urgency name. | Nome localizado da urgência.</summary>
    public string UrgencyName => Loc.UrgencyName(Alarm.Urgency);

    /// <summary>Color of the urgency accent. | Cor de destaque da urgência.</summary>
    public Brush AccentBrush { get; }

    /// <summary>Whether the alarm is enabled. | Se o alarme está ligado.</summary>
    [ObservableProperty]
    private bool _isEnabled;

    partial void OnIsEnabledChanged(bool value)
    {
        if (!_suprimirCallback)
        {
            _onToggled(value);
        }
    }

    /// <summary>Relative text of the next occurrence, or off/none. | Texto relativo da próxima ocorrência, ou desligado/nenhuma.</summary>
    public string NextText
    {
        get
        {
            if (!Alarm.IsEnabled)
            {
                return Loc.Get("Row_Off");
            }

            var proxima = Alarm.Schedule.NextOccurrenceAfter(_clock.Now, _clock.LocalTimeZone);

            return proxima is null
                ? Loc.Get("Row_NoNext")
                : TimeFormat.Relative(proxima.Value, _clock);
        }
    }

    /// <summary>Re-reads the relative texts without recreating the row. | Recalcula os textos relativos sem recriar a linha.</summary>
    public void RefreshNext() => OnPropertyChanged(nameof(NextText));
}
