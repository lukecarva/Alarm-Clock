using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using AlarmClock.Core.Localization;
using AlarmClock.Core.Model;
using AlarmClock.Core.Scheduling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AlarmClock.App.ViewModels;

/// <summary>A snooze choice shown on the alert. | Uma opção de adiamento mostrada no alerta.</summary>
public sealed record SnoozeOption(TimeSpan Duration, string Label);

/// <summary>View model for an alert window. | View model de uma janela de alerta.</summary>
public sealed partial class AlertViewModel : ObservableObject, IDisposable
{
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
        UrgencyName = Loc.UrgencyName(perfil.Level);

        AccentBrush = Application.Current?.TryFindResource($"Brush.Urgency.{perfil.Level}") as Brush
                      ?? Brushes.OrangeRed;

        if (mode == PresentationMode.Fullscreen)
        {
            // Live clock, updated each second, for the full-screen overlay. | Relógio ao vivo, atualizado a cada segundo, para o overlay de tela cheia.
            NowText = DateTime.Now.ToString("HH:mm", Loc.Culture);
            _relogio = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _relogio.Tick += (_, _) => NowText = DateTime.Now.ToString("HH:mm", Loc.Culture);
            _relogio.Start();
        }
    }

    /// <summary>The trigger being shown. | O disparo sendo mostrado.</summary>
    public AlarmTriggeredEventArgs Trigger { get; }

    /// <summary>Presentation mode of this alert. | Modo de apresentação deste alerta.</summary>
    public PresentationMode Mode { get; }

    /// <summary>How the alert is dismissed. | Como o alerta é dispensado.</summary>
    public DismissMode DismissMode { get; }

    /// <summary>Phrase required by type-to-dismiss. | Frase exigida pelo dispensar-digitando.</summary>
    public string DismissPhrase { get; }

    /// <summary>Localized urgency name. | Nome localizado da urgência.</summary>
    public string UrgencyName { get; }

    /// <summary>Urgency accent color. | Cor de destaque da urgência.</summary>
    public Brush AccentBrush { get; }

    /// <summary>Available snooze options. | Opções de adiamento disponíveis.</summary>
    public IReadOnlyList<SnoozeOption> SnoozeOptions { get; }

    /// <summary>Alarm title. | Título do alarme.</summary>
    public string Title => Trigger.Alarm.Title;

    /// <summary>Optional detail. | Detalhe opcional.</summary>
    public string? Message => Trigger.Alarm.Message;

    /// <summary>Whether there is a detail message. | Se há mensagem de detalhe.</summary>
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    /// <summary>Whether this is a missed alarm. | Se é um alarme perdido.</summary>
    public bool IsMissed => Trigger.Kind == TriggerKind.Missed;

    /// <summary>Whether snoozing is offered. | Se o adiamento é oferecido.</summary>
    public bool CanSnooze => SnoozeOptions.Count > 0;

    /// <summary>Context line: on time, snoozed, escalated, or missed. | Linha de contexto: na hora, adiado, escalado, ou perdido.</summary>
    public string WhenText
    {
        get
        {
            var hora = Trigger.ScheduledFor.ToLocalTime().ToString("HH:mm", Loc.Culture);

            if (Trigger.Kind == TriggerKind.Escalation)
            {
                return Loc.Format("Alert_When_Escalated", UrgencyName);
            }

            if (Trigger.Kind == TriggerKind.Snooze)
            {
                return Loc.Format("Alert_When_Snoozed", hora);
            }

            if (!IsMissed)
            {
                return Loc.Format("Alert_When_Scheduled", hora);
            }

            var texto = Loc.Format("Alert_When_Missed", hora, Humanizar(Trigger.Delay));

            return Trigger.SkippedOccurrences > 0
                ? Loc.Format("Alert_When_MissedExtra", texto, Trigger.SkippedOccurrences)
                : texto;
        }
    }

    /// <summary>Big clock text on the full-screen overlay. | Texto do relógio grande no overlay de tela cheia.</summary>
    [ObservableProperty]
    private string _nowText = string.Empty;

    /// <summary>Set when the scheduler refuses a snooze. | Preenchido quando o agendador recusa um adiamento.</summary>
    [ObservableProperty]
    private string? _snoozeRefusal;

    /// <summary>0..1, driven by the hold-to-dismiss button. | 0..1, alimentado pelo botão de segurar.</summary>
    [ObservableProperty]
    private double _holdProgress;

    /// <summary>Raised when the window should close. | Emitido quando a janela deve fechar.</summary>
    public event Action? CloseRequested;

    /// <summary>Dismisses the alarm and closes. | Dispensa o alarme e fecha.</summary>
    [RelayCommand]
    private void Dismiss()
    {
        _scheduler.Dismiss(Trigger.Alarm.Id);
        CloseRequested?.Invoke();
    }

    /// <summary>Closes on timeout without dismissing, so escalation can continue. | Fecha por tempo sem dispensar, para a escalada poder continuar.</summary>
    public void TimeoutClose() => CloseRequested?.Invoke();

    /// <summary>Snoozes for the chosen duration, or shows the refusal. | Adia pela duração escolhida, ou mostra a recusa.</summary>
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

    /// <summary>Formats a delay as a short human-readable span. | Formata um atraso como um intervalo curto e legível.</summary>
    private static string Humanizar(TimeSpan intervalo)
    {
        if (intervalo.TotalMinutes < 1)
        {
            return Loc.Get("Dur_LessThanMinute");
        }

        if (intervalo.TotalHours < 1)
        {
            return Loc.Format("Dur_Min", (int)intervalo.TotalMinutes);
        }

        if (intervalo.TotalDays < 1)
        {
            return Loc.Format("Dur_HourMin", intervalo.Hours, intervalo.Minutes);
        }

        return Loc.Format("Dur_Days", (int)intervalo.TotalDays);
    }

    public void Dispose() => _relogio?.Stop();
}
