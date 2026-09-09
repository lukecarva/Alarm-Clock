using System.Globalization;
using System.Windows;
using System.Windows.Media;
using AlarmClock.Core.Abstractions;
using AlarmClock.Core.Model;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AlarmClock.App.ViewModels;

/// <summary>Uma linha da lista de alarmes.</summary>
public sealed partial class AlarmRowViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    private readonly ISystemClock _clock;
    private readonly Action<bool> _onToggled;

    private bool _suprimirCallback;

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

            var agora = _clock.Now;
            var proxima = Alarm.Schedule.NextOccurrenceAfter(agora, _clock.LocalTimeZone);

            if (proxima is null)
            {
                return "sem próxima ocorrência";
            }

            var local = TimeZoneInfo.ConvertTime(proxima.Value, _clock.LocalTimeZone);
            var falta = proxima.Value - agora;

            if (falta < TimeSpan.FromHours(1))
            {
                return $"em {Math.Max(1, (int)falta.TotalMinutes)} min";
            }

            var hoje = TimeZoneInfo.ConvertTime(agora, _clock.LocalTimeZone).Date;
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

            return local.ToString("dd/MM", PtBr) + $", {hora}";
        }
    }

    /// <summary>Recalcula os textos relativos ("em 42 min") sem recriar a linha.</summary>
    public void RefreshNext() => OnPropertyChanged(nameof(NextText));
}
