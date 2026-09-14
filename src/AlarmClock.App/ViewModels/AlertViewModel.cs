using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using AlarmClock.Core.Model;
using AlarmClock.Core.Scheduling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlarmClock.App.ViewModels;

public sealed record SnoozeOption(TimeSpan Duration, string Label);

public sealed partial class AlertViewModel : ObservableObject, IDisposable
{
    private static readonly CultureInfo PtBr = TimeFormat.PtBr;

    private readonly IAlarmScheduler _scheduler;
    private readonly DispatcherTimer? _relogio;

    public AlertViewModel(AlarmTriggeredEventArgs trigger, PresentationMode mode, IAlarmScheduler scheduler)
    {
        _scheduler = scheduler;

        Trigger = trigger;
        Mode = mode;

        var perfil = trigger.EffectiveProfile;

        SnoozeOptions = [.. perfil.Snooze.Options.Select(d => new SnoozeOption(d, $"{d.TotalMinutes:0} min"))];
        DismissPhrase = perfil.DismissPhrase;
        DismissMode = perfil.Dismiss;
        UrgencyName = perfil.DisplayName;

        AccentBrush = Application.Current.TryFindResource($"Brush.Urgency.{perfil.Level}") as Brush
                      ?? Brushes.OrangeRed;

        if (mode == PresentationMode.Fullscreen)
        {
            // Um relógio grande no overlay: se o alerta te acordou, a primeira
            // coisa que você quer saber é que horas são.
            NowText = DateTime.Now.ToString("HH:mm", PtBr);
            _relogio = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _relogio.Tick += (_, _) => NowText = DateTime.Now.ToString("HH:mm", PtBr);
            _relogio.Start();
        }
    }

    public AlarmTriggeredEventArgs Trigger { get; }

    public PresentationMode Mode { get; }

    public DismissMode DismissMode { get; }

    public string DismissPhrase { get; }

    public string UrgencyName { get; }

    public Brush AccentBrush { get; }

    public IReadOnlyList<SnoozeOption> SnoozeOptions { get; }

    public string Title => Trigger.Alarm.Title;

    public string? Message => Trigger.Alarm.Message;

    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public bool IsMissed => Trigger.Kind == TriggerKind.Missed;

    public bool CanSnooze => SnoozeOptions.Count > 0;

    /// <summary>Linha de contexto: na hora, adiado, escalado, ou perdido.</summary>
    public string WhenText
    {
        get
        {
            var hora = Trigger.ScheduledFor.ToLocalTime().ToString("HH:mm", PtBr);

            if (Trigger.Kind == TriggerKind.Escalation)
            {
                return $"Ignorado — subiu para {UrgencyName}";
            }

            if (Trigger.Kind == TriggerKind.Snooze)
            {
                return $"Você adiou até {hora}";
            }

            if (!IsMissed)
            {
                return $"Marcado para {hora}";
            }

            var texto = $"Perdido: era {hora}, {Humanizar(Trigger.Delay)} atrás";

            return Trigger.SkippedOccurrences > 0
                ? $"{texto} (+{Trigger.SkippedOccurrences} ocorrência(s) anteriores também perdidas)"
                : texto;
        }
    }

    [ObservableProperty]
    private string _nowText = string.Empty;

    /// <summary>Preenchido quando o agendador recusa um adiamento.</summary>
    [ObservableProperty]
    private string? _snoozeRefusal;

    /// <summary>0..1, alimentado pelo botão de segurar.</summary>
    [ObservableProperty]
    private double _holdProgress;

    public event Action? CloseRequested;

    [RelayCommand]
    private void Dismiss()
    {
        _scheduler.Dismiss(Trigger.Alarm.Id);
        CloseRequested?.Invoke();
    }

    /// <summary>
    /// Fecha por tempo esgotado, <b>sem</b> dispensar no agendador. É o que
    /// deixa a escalada continuar: se o auto-dismiss chamasse
    /// <see cref="DismissCommand"/>, um alarme Normal (que some em 30s) nunca
    /// subiria de nível. Para quem não escala, é indiferente — a ocorrência
    /// seguinte zera tudo de qualquer forma.
    /// </summary>
    public void TimeoutClose() => CloseRequested?.Invoke();

    [RelayCommand]
    private void Snooze(SnoozeOption? option)
    {
        var duracao = option?.Duration ?? Trigger.EffectiveProfile.Snooze.DefaultOption;

        if (_scheduler.TrySnooze(Trigger.Alarm.Id, duracao, out var recusa))
        {
            CloseRequested?.Invoke();
            return;
        }

        SnoozeRefusal = recusa;
    }

    private static string Humanizar(TimeSpan intervalo)
    {
        if (intervalo.TotalMinutes < 1)
        {
            return "menos de um minuto";
        }

        if (intervalo.TotalHours < 1)
        {
            return $"{intervalo.TotalMinutes:0} min";
        }

        if (intervalo.TotalDays < 1)
        {
            return $"{intervalo.Hours}h{intervalo.Minutes:00}";
        }

        return $"{intervalo.TotalDays:0} dia(s)";
    }

    public void Dispose() => _relogio?.Stop();
}
