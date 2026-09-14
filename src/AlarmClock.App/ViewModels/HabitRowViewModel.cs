using System.Globalization;
using AlarmClock.Core.Localization;
using AlarmClock.Core.Model;
using AlarmClock.Core.Scheduling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AlarmClock.App.ViewModels;

/// <summary>Uma linha da aba "dia a dia": um lembrete pronto, com liga/desliga.</summary>
public sealed partial class HabitRowViewModel : ObservableObject
{
    private readonly HabitDefinition _habit;
    private readonly Action<HabitDefinition, bool, int> _onToggled;
    private readonly Action<HabitDefinition, int> _onIntervalChanged;

    // Enquanto a linha é (re)montada a partir do estado salvo, os setters não
    // devem disparar de volta para o serviço.
    private readonly bool _building;

    public HabitRowViewModel(
        HabitDefinition habit,
        Alarm? existing,
        Action<HabitDefinition, bool, int> onToggled,
        Action<HabitDefinition, int> onIntervalChanged)
    {
        _habit = habit;
        _onToggled = onToggled;
        _onIntervalChanged = onIntervalChanged;

        _building = true;

        var minutos = existing?.Schedule is IntervalSchedule agenda
            ? (int)agenda.Every.TotalMinutes
            : habit.DefaultMinutes;

        _isActive = existing is { IsEnabled: true };
        _intervalText = minutos.ToString(CultureInfo.InvariantCulture);

        _building = false;
    }

    public string Emoji => _habit.Emoji;

    public string Name => _habit.Name;

    public string Note => _habit.Note;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private string _intervalText;

    partial void OnIsActiveChanged(bool value)
    {
        if (!_building)
        {
            _onToggled(_habit, value, CurrentMinutes());
        }
    }

    partial void OnIntervalTextChanged(string value)
    {
        if (!_building && IsActive)
        {
            _onIntervalChanged(_habit, CurrentMinutes());
        }
    }

    private int CurrentMinutes() =>
        int.TryParse(IntervalText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) && m >= 1
            ? m
            : _habit.DefaultMinutes;
}
