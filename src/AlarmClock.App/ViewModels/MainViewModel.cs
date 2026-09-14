using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Threading;
using AlarmClock.App.Services;
using AlarmClock.Core.Abstractions;
using AlarmClock.Core.Localization;
using AlarmClock.Core.Model;
using AlarmClock.Core.Scheduling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AlarmClock.App.ViewModels;

/// <summary>View model of the main window: alarms list and daily habits. | View model da janela principal: lista de alarmes e hábitos do dia a dia.</summary>
public sealed partial class MainViewModel : ObservableObject
{
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

        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        VersionText = Loc.Format("Main_Version", version);

        _startWithWindows = _startup.IsEnabled;

        _alarms.Changed += (_, _) => Rebuild();
        Rebuild();

        // Refreshes the relative texts periodically; only runs while visible. | Atualiza os textos relativos periodicamente; só roda enquanto visível.
        _refresh = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _refresh.Tick += (_, _) => RefreshRelativeTexts();
    }

    /// <summary>Starts/stops the periodic refresh with the window's visibility. | Liga/desliga a atualização periódica conforme a visibilidade da janela.</summary>
    public void SetActive(bool active)
    {
        if (active)
        {
            RefreshRelativeTexts();
            _refresh.Start();
        }
        else
        {
            _refresh.Stop();
        }
    }

    /// <summary>User alarms (habits excluded). | Alarmes do usuário (hábitos excluídos).</summary>
    public ObservableCollection<AlarmRowViewModel> Alarms { get; } = [];

    /// <summary>Daily-habit reminders. | Lembretes de dia a dia.</summary>
    public ObservableCollection<HabitRowViewModel> Habits { get; } = [];

    /// <summary>Footer version text. | Texto de versão do rodapé.</summary>
    public string VersionText { get; }

    /// <summary>Whether there are any user alarms. | Se há algum alarme do usuário.</summary>
    public bool HasAlarms => Alarms.Count > 0;

    /// <summary>Subtitle summarizing the next alarm. | Subtítulo resumindo o próximo alarme.</summary>
    [ObservableProperty]
    private string _nextAlarmSummary = string.Empty;

    /// <summary>Whether the app starts with Windows. | Se o app inicia com o Windows.</summary>
    [ObservableProperty]
    private bool _startWithWindows;

    partial void OnStartWithWindowsChanged(bool value) => _startup.SetEnabled(value);

    /// <summary>Opens the editor to create an alarm. | Abre o editor para criar um alarme.</summary>
    [RelayCommand]
    private void NewAlarm()
    {
        _log.LogDebug("Abrindo o editor para um alarme novo.");

        var criado = _dialogs.Edit(null);
        if (criado is not null)
        {
            _alarms.AddOrUpdate(criado);
        }
    }

    /// <summary>Opens the editor for an existing alarm. | Abre o editor para um alarme existente.</summary>
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

    /// <summary>Deletes an alarm after confirmation. | Exclui um alarme após confirmação.</summary>
    [RelayCommand]
    private void Delete(AlarmRowViewModel? row)
    {
        if (row is null || !_dialogs.ConfirmDelete(row.Title))
        {
            return;
        }

        _alarms.Remove(row.Alarm.Id);
    }

    /// <summary>Opens the data folder in Explorer. | Abre a pasta de dados no Explorer.</summary>
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

    /// <summary>Rebuilds the alarm and habit rows from the service state. | Reconstrói as linhas de alarme e de hábito a partir do estado do serviço.</summary>
    private void Rebuild()
    {
        Alarms.Clear();

        // The alarm list hides habits; they live in the "daily" tab. | A lista de alarmes esconde os hábitos; eles ficam na aba "dia a dia".
        var comuns = _alarms.Items
            .Where(a => a.HabitKey is null)
            .OrderBy(a => a.Title, StringComparer.CurrentCultureIgnoreCase);

        foreach (var alarme in comuns)
        {
            var id = alarme.Id;
            Alarms.Add(new AlarmRowViewModel(alarme, _clock, ligado => _alarms.SetEnabled(id, ligado)));
        }

        RebuildHabits();

        OnPropertyChanged(nameof(HasAlarms));
        RefreshRelativeTexts();
    }

    /// <summary>Rebuilds the habit rows from the catalog and saved state. | Reconstrói as linhas de hábito a partir do catálogo e do estado salvo.</summary>
    private void RebuildHabits()
    {
        Habits.Clear();

        foreach (var def in HabitCatalog.All)
        {
            var existente = _alarms.Items.FirstOrDefault(a => a.HabitKey == def.Key);
            Habits.Add(new HabitRowViewModel(def, existente, ToggleHabit, SetHabitInterval));
        }
    }

    /// <summary>Creates or removes a habit's alarm when toggled. | Cria ou remove o alarme de um hábito ao ligar/desligar.</summary>
    private void ToggleHabit(HabitDefinition def, bool active, int minutes)
    {
        var existente = _alarms.Items.FirstOrDefault(a => a.HabitKey == def.Key);

        if (active)
        {
            _alarms.AddOrUpdate(HabitCatalog.BuildAlarm(def, minutes, _clock.Now, existente));
            _log.LogInformation("Hábito {Habito} ativado (a cada {Min} min).", def.Key, minutes);
        }
        else if (existente is not null)
        {
            _alarms.Remove(existente.Id);
            _log.LogInformation("Hábito {Habito} desativado.", def.Key);
        }
    }

    /// <summary>Updates an active habit's interval. | Atualiza o intervalo de um hábito ativo.</summary>
    private void SetHabitInterval(HabitDefinition def, int minutes)
    {
        var existente = _alarms.Items.FirstOrDefault(a => a.HabitKey == def.Key);
        if (existente is not null)
        {
            _alarms.AddOrUpdate(HabitCatalog.BuildAlarm(def, minutes, _clock.Now, existente));
        }
    }

    /// <summary>Refreshes every relative text and the subtitle. | Atualiza todos os textos relativos e o subtítulo.</summary>
    private void RefreshRelativeTexts()
    {
        foreach (var linha in Alarms)
        {
            linha.RefreshNext();
        }

        NextAlarmSummary = DescribeNext();
    }

    /// <summary>Builds the "next alarm …" subtitle. | Monta o subtítulo "próximo alarme …".</summary>
    private string DescribeNext()
    {
        var proxima = _scheduler.NextFireTime;

        if (proxima is null)
        {
            return Loc.Get(HasAlarms ? "Status_NoneActive" : "Status_NoneConfigured");
        }

        return Loc.Format("Status_NextPrefix", TimeFormat.Relative(proxima.Value, _clock));
    }
}
