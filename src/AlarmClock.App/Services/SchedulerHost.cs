using System.Windows;
using System.Windows.Threading;
using AlarmClock.Core.Abstractions;
using AlarmClock.Core.Localization;
using AlarmClock.Core.Scheduling;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace AlarmClock.App.Services;

/// <summary>
/// Connects the scheduler to the real world: the one-second tick and the Windows
/// power/time events. | Liga o agendador ao mundo real: o tique de um segundo e os eventos de energia
/// e de relógio do Windows.
/// </summary>
public sealed class SchedulerHost(
    IAlarmScheduler scheduler,
    AlarmsService alarms,
    IAlertPresenter presenter,
    TrayIconService tray,
    ISystemClock clock,
    ILogger<SchedulerHost> log) : IHostedService, IDisposable
{
    private readonly IAlarmScheduler _scheduler = scheduler;
    private readonly AlarmsService _alarms = alarms;
    private readonly IAlertPresenter _presenter = presenter;
    private readonly TrayIconService _tray = tray;
    private readonly ISystemClock _clock = clock;
    private readonly ILogger<SchedulerHost> _log = log;

    private DispatcherTimer? _timer;
    private bool _hooked;
    private string? _ultimoStatus;

    /// <summary>Loads alarms, starts the tick, and hooks system events. | Carrega alarmes, inicia o tique e engancha os eventos do sistema.</summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _scheduler.Triggered += OnTriggered;

        _alarms.Load();
        _alarms.Changed += OnAlarmsChanged;
        _scheduler.Reload(_alarms.Items);

        // One-second tick: cheap, and immune to hibernation. | Tique de um segundo: barato, e imune a hibernação.
        _timer = new DispatcherTimer(DispatcherPriority.Normal, Application.Current.Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1),
        };

        _timer.Tick += (_, _) =>
        {
            _scheduler.Tick();
            AtualizarStatusDaBandeja();
        };

        _timer.Start();
        AtualizarStatusDaBandeja();

        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.TimeChanged += OnTimeChanged;
        _hooked = true;

        _log.LogInformation(
            "Agendador no ar. Próximo disparo: {Proximo}",
            _scheduler.NextFireTime?.ToString("dd/MM HH:mm:ss") ?? "nenhum");

        return Task.CompletedTask;
    }

    /// <summary>Stops the tick and unhooks system events. | Para o tique e desengancha os eventos do sistema.</summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Stop();
        Unhook();
        return Task.CompletedTask;
    }

    /// <summary>Reloads the scheduler when the alarm set changes. | Recarrega o agendador quando o conjunto de alarmes muda.</summary>
    private void OnAlarmsChanged(object? sender, EventArgs e)
    {
        _scheduler.Reload(_alarms.Items);
        AtualizarStatusDaBandeja();
    }

    /// <summary>Shows the alert for a fired alarm. | Mostra o alerta de um alarme disparado.</summary>
    private void OnTriggered(object? sender, AlarmTriggeredEventArgs e)
    {
        _presenter.Show(e);
    }

    /// <summary>Updates the tray tooltip only when the text actually changes. | Atualiza o tooltip da bandeja só quando o texto muda de verdade.</summary>
    private void AtualizarStatusDaBandeja()
    {
        var proxima = _scheduler.NextFireTime;

        var texto = proxima is null
            ? Loc.Format("Tray_Idle", Loc.Get("App_Name"))
            : Loc.Format("Status_NextPrefix", TimeFormat.Relative(proxima.Value, _clock));

        if (texto == _ultimoStatus)
        {
            return;
        }

        _ultimoStatus = texto;
        _tray.SetStatus(texto);
    }

    /// <summary>On resume, ticks so catch-up runs for what came due while asleep. | Ao acordar, tica para o catch-up rodar sobre o que venceu dormindo.</summary>
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume)
        {
            return;
        }

        _log.LogInformation("PC acordou. Avaliando o que venceu enquanto dormia.");

        Application.Current.Dispatcher.BeginInvoke(() => _scheduler.Tick());
    }

    /// <summary>On a clock/zone change, reloads instead of firing a burst. | Numa mudança de relógio/fuso, recarrega em vez de disparar em rajada.</summary>
    private void OnTimeChanged(object? sender, EventArgs e)
    {
        _log.LogWarning("Relógio ou fuso do sistema mudou. Recalculando a agenda.");

        Application.Current.Dispatcher.BeginInvoke(() => _scheduler.Reload(_alarms.Items));
    }

    /// <summary>Unsubscribes from the static SystemEvents and service events. | Desinscreve dos SystemEvents estáticos e dos eventos do serviço.</summary>
    private void Unhook()
    {
        if (!_hooked)
        {
            return;
        }

        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.TimeChanged -= OnTimeChanged;
        _alarms.Changed -= OnAlarmsChanged;
        _scheduler.Triggered -= OnTriggered;
        _hooked = false;
    }

    public void Dispose()
    {
        _timer?.Stop();
        Unhook();
    }
}
