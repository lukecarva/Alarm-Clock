using System.Diagnostics;
using System.Reflection;
using AlarmClock.Core.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AlarmClock.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly ISystemClock _clock;
    private readonly ILogger<MainViewModel> _log;

    public MainViewModel(ISystemClock clock, ILogger<MainViewModel> log)
    {
        _clock = clock;
        _log = log;

        Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
        StartedAt = _clock.Now;
    }

    public string Version { get; }

    public DateTimeOffset StartedAt { get; }

    public string DataFolder => AppPaths.Root;

    /// <summary>Substituído na Fase 1 pela próxima ocorrência real do agendador.</summary>
    [ObservableProperty]
    private string _nextAlarmSummary = "Nenhum alarme configurado";

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
}
