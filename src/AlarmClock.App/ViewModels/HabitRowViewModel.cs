using System.Globalization;
using AlarmClock.Core.Localization;
using AlarmClock.Core.Model;
using AlarmClock.Core.Scheduling;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AlarmClock.App.ViewModels;

/// <summary>One row of the "daily" tab: a ready reminder with an on/off toggle. | Uma linha da aba "dia a dia": um lembrete pronto com liga/desliga.</summary>
public sealed partial class HabitRowViewModel : ObservableObject
{
    private readonly HabitDefinition _habit;
    private readonly Action<HabitDefinition, bool, int> _onToggled;
    private readonly Action<HabitDefinition, int> _onIntervalChanged;

    // While the row is (re)built from saved state, setters must not call back. | Enquanto a linha é (re)montada do estado salvo, os setters não podem retornar chamada.
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

    /// <summary>Habit icon. | Ícone do hábito.</summary>
    public string Emoji => _habit.Emoji;

    /// <summary>Habit name. | Nome do hábito.</summary>
    public string Name => _habit.Name;

    /// <summary>Short description. | Descrição curta.</summary>
    public string Note => _habit.Note;

    /// <summary>Whether the reminder is active. | Se o lembrete está ativo.</summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>Interval in minutes, as text. | Intervalo em minutos, como texto.</summary>
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

    /// <summary>Parsed interval, or the default when invalid. | Intervalo lido, ou o padrão quando inválido.</summary>
    private int CurrentMinutes() =>
        int.TryParse(IntervalText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) && m >= 1
            ? m
            : _habit.DefaultMinutes;
}
