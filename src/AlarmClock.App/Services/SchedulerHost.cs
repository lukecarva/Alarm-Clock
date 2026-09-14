using System.Windows;
using System.Windows.Threading;
using AlarmClock.Core.Abstractions;
using AlarmClock.Core.Scheduling;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace AlarmClock.App.Services;

/// <summary>
/// Liga o agendador ao mundo real: o tique de um segundo e os eventos do
/// Windows que invalidam qualquer conta de tempo feita antes deles.
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

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _scheduler.Triggered += OnTriggered;

        _alarms.Load();
        _alarms.Changed += OnAlarmsChanged;
        _scheduler.Reload(_alarms.Items);

        // Um segundo. Não é polling caro: são algumas comparações de
        // DateTimeOffset. O que se ganha é imunidade a hibernação — um timer
        // longo simplesmente não dispara depois que o PC dorme.
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

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Stop();
        Unhook();
        return Task.CompletedTask;
    }

    private void OnAlarmsChanged(object? sender, EventArgs e)
    {
        _scheduler.Reload(_alarms.Items);
        AtualizarStatusDaBandeja();
    }

    private void OnTriggered(object? sender, AlarmTriggeredEventArgs e)
    {
        _presenter.Show(e);
    }

    /// <summary>Tooltip da bandeja. Só escreve quando o texto muda de verdade.</summary>
    private void AtualizarStatusDaBandeja()
    {
        var proxima = _scheduler.NextFireTime;

        var texto = proxima is null
            ? "Despertador Produtivo — nenhum alarme ativo"
            : $"Próximo alarme {TimeFormat.Relative(proxima.Value, _clock)}";

        if (texto == _ultimoStatus)
        {
            return;
        }

        _ultimoStatus = texto;
        _tray.SetStatus(texto);
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume)
        {
            return;
        }

        _log.LogInformation("PC acordou. Avaliando o que venceu enquanto dormia.");

        // Tick, e não Reload: é exatamente aqui que o catch-up precisa rodar e
        // reportar o que passou. Reload jogaria os atrasos fora.
        Application.Current.Dispatcher.BeginInvoke(() => _scheduler.Tick());
    }

    private void OnTimeChanged(object? sender, EventArgs e)
    {
        _log.LogWarning("Relógio ou fuso do sistema mudou. Recalculando a agenda.");

        // Aqui é Reload, e não Tick: o usuário mexeu no relógio de propósito.
        // Disparar em rajada tudo que "venceu" com a conta nova seria pior que
        // perder as ocorrências.
        Application.Current.Dispatcher.BeginInvoke(() => _scheduler.Reload(_alarms.Items));
    }

    private void Unhook()
    {
        if (!_hooked)
        {
            return;
        }

        // SystemEvents são estáticos: sem desinscrever, o objeto vive para sempre.
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
