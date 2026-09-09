using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Windows.Threading;
using AlarmClock.App.Services;
using AlarmClock.Core.Abstractions;
using AlarmClock.Core.Scheduling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AlarmClock.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    private readonly AlarmsService _alarms;
    private readonly IAlarmScheduler _scheduler;
    private readonly AlarmDialogs _dialogs;
    private readonly StartupRegistrar _startup;
    private readonly ISystemClock _clock;
    private readonly ILogger<MainViewModel> _log;
    private readonly DispatcherTimer _refresh;

    public MainViewModel(
        AlarmsService alarms,
        IAlarmScheduler scheduler,
        AlarmDialogs dialogs,
        StartupRegistrar startup,
        ISystemClock clock,
        ILogger<MainViewModel> log)
    {
        _alarms = alarms;
        _scheduler = scheduler;
        _dialogs = dialogs;
        _startup = startup;
        _clock = clock;
        _log = log;

        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        _startWithWindows = _startup.IsEnabled;

        _alarms.Changed += (_, _) => Rebuild();
        Rebuild();

        // Os textos são relativos ("em 42 min"), então envelhecem sozinhos.
        _refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _refresh.Tick += (_, _) => RefreshRelativeTexts();
        _refresh.Start();
    }

    public ObservableCollection<AlarmRowViewModel> Alarms { get; } = [];

    public string Version { get; }

    public string DataFolder => AppPaths.Root;

    public bool HasAlarms => Alarms.Count > 0;

    [ObservableProperty]
    private string _nextAlarmSummary = "Nenhum alarme configurado";

    [ObservableProperty]
    private bool _startWithWindows;

    partial void OnStartWithWindowsChanged(bool value) => _startup.SetEnabled(value);

    [RelayCommand]
    private void NewAlarm()
    {
        var criado = _dialogs.Edit(null);
        if (criado is not null)
        {
            _alarms.AddOrUpdate(criado);
        }
    }

    [RelayCommand]
    private void Edit(AlarmRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        var editado = _dialogs.Edit(row.Alarm);
        if (editado is not null)
        {
            _alarms.AddOrUpdate(editado);
        }
    }

    [RelayCommand]
    private void Delete(AlarmRowViewModel? row)
    {
        if (row is null || !_dialogs.ConfirmDelete(row.Title))
        {
            return;
        }

        _alarms.Remove(row.Alarm.Id);
    }

    [RelayCommand]
    private void OpenDataFolder()
    {
        AppPaths.EnsureCreated();
        _log.LogInformation("Abrindo a pasta de dados no Explorer.");

        Process.Start(new ProcessStartInfo
        {
            FileName = AppPaths.Root,
            UseShellExecute = true,
        });
    }

    private void Rebuild()
    {
        Alarms.Clear();

        foreach (var alarme in _alarms.Items.OrderBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase))
        {
            var id = alarme.Id;
            Alarms.Add(new AlarmRowViewModel(alarme, _clock, ligado => _alarms.SetEnabled(id, ligado)));
        }

        OnPropertyChanged(nameof(HasAlarms));
        RefreshRelativeTexts();
    }

    private void RefreshRelativeTexts()
    {
        foreach (var linha in Alarms)
        {
            linha.RefreshNext();
        }

        NextAlarmSummary = DescribeNext();
    }

    private string DescribeNext()
    {
        var proxima = _scheduler.NextFireTime;

        if (proxima is null)
        {
            return HasAlarms ? "Nenhum alarme ativo" : "Nenhum alarme configurado";
        }

        var falta = proxima.Value - _clock.Now;
        var local = TimeZoneInfo.ConvertTime(proxima.Value, _clock.LocalTimeZone);

        return falta < TimeSpan.FromHours(1)
            ? $"Próximo alarme em {Math.Max(1, (int)falta.TotalMinutes)} min"
            : $"Próximo alarme: {local.ToString("dd/MM 'às' HH:mm", PtBr)}";
    }
}
