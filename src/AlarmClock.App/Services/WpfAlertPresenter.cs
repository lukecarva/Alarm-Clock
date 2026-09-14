using System.Windows;
using AlarmClock.App.Audio;
using AlarmClock.App.Interop;
using AlarmClock.App.ViewModels;
using AlarmClock.App.Views;
using AlarmClock.Core.Abstractions;
using AlarmClock.Core.Model;
using AlarmClock.Core.Scheduling;
using Microsoft.Extensions.Logging;

namespace AlarmClock.App.Services;

/// <summary>Chooses and builds the alert window from the urgency profile. | Escolhe e monta a janela do alerta a partir do perfil de urgência.</summary>
public sealed class WpfAlertPresenter(
    IAlarmScheduler scheduler,
    TrayIconService tray,
    IIdleDetector idle,
    ILogger<WpfAlertPresenter> log) : IAlertPresenter
{
    private readonly IAlarmScheduler _scheduler = scheduler;
    private readonly TrayIconService _tray = tray;
    private readonly IIdleDetector _idle = idle;
    private readonly ILogger<WpfAlertPresenter> _log = log;

    /// <summary>On-screen alerts by alarm; prevents two alerts for one alarm. | Alertas na tela por alarme; impede dois alertas do mesmo alarme.</summary>
    private readonly Dictionary<Guid, AlertSession> _abertos = [];

    /// <summary>Presents a triggered alarm (skips, toast, window or escalation). | Apresenta um alarme disparado (pula, toast, janela ou escalada).</summary>
    public void Show(AlarmTriggeredEventArgs trigger)
    {
        if (ShouldSkipForAbsence(trigger, out var ocioso))
        {
            _log.LogInformation(
                "Alarme {Titulo} pulado: teclado e mouse parados há {Ocioso}.",
                trigger.Alarm.Title,
                ocioso);

            _scheduler.Dismiss(trigger.Alarm.Id);
            return;
        }

        var resolvido = Resolve(trigger);

        if (resolvido is null)
        {
            _log.LogInformation(
                "Alarme {Titulo} perdido e descartado conforme o nível {Nivel}.",
                trigger.Alarm.Title,
                trigger.EffectiveUrgency);

            return;
        }

        var (modo, som) = resolvido.Value;

        if (_abertos.TryGetValue(trigger.Alarm.Id, out var existente))
        {
            if (trigger.Kind != TriggerKind.Escalation)
            {
                _log.LogDebug("Alerta de {Titulo} já está na tela.", trigger.Alarm.Title);
                return;
            }

            // Escalation replaces the current window with a more intrusive one. | Escalada troca a janela atual por uma mais intrusiva.
            _log.LogInformation("Alerta de {Titulo} substituído pela versão escalada.", trigger.Alarm.Title);
            existente.Window.Close();
        }

        _log.LogInformation(
            "Alerta: {Titulo} [{Nivel}/{Modo}] motivo={Motivo}.",
            trigger.Alarm.Title,
            trigger.EffectiveUrgency,
            modo.ToString(),
            trigger.Kind.ToString());

        if (modo == PresentationMode.Toast)
        {
            _tray.ShowBalloon(trigger.Alarm.Title, trigger.Alarm.Message ?? trigger.Alarm.Schedule.Describe());
            _scheduler.Dismiss(trigger.Alarm.Id);
            return;
        }

        MostrarJanela(trigger, modo, som);
    }

    /// <summary>Opens the alert window and starts its sound. | Abre a janela do alerta e inicia o som.</summary>
    private void MostrarJanela(AlarmTriggeredEventArgs trigger, PresentationMode modo, SoundSpec som)
    {
        var viewModel = new AlertViewModel(trigger, modo, _scheduler);
        var janela = new AlertWindow(viewModel);
        var sessao = new AlertSession(janela, new AlarmAudioPlayer(_log));

        _abertos[trigger.Alarm.Id] = sessao;
        janela.Closed += (_, _) => Encerrar(trigger.Alarm.Id);

        if (modo == PresentationMode.Fullscreen)
        {
            AbrirEmTelaCheia(janela, viewModel, sessao);
        }
        else
        {
            // The content sizes the card; a fixed height would clip the buttons. | O conteúdo dimensiona o card; altura fixa cortaria os botões.
            janela.SizeToContent = SizeToContent.WidthAndHeight;
            janela.MinWidth = 380;
            janela.MaxWidth = 460;

            janela.WindowStartupLocation = modo == PresentationMode.Modal
                ? WindowStartupLocation.CenterScreen
                : WindowStartupLocation.Manual;

            janela.ShowActivated = modo != PresentationMode.Corner;
            janela.Show();

            if (modo == PresentationMode.Corner)
            {
                janela.UpdateLayout();
                Win32Windows.PlaceInCorner(janela);
            }
        }

        sessao.Audio.Play(som);
    }

    /// <summary>Covers every monitor: main window plus one overlay per extra screen. | Cobre todos os monitores: janela principal mais um overlay por tela extra.</summary>
    private static void AbrirEmTelaCheia(AlertWindow janela, AlertViewModel viewModel, AlertSession sessao)
    {
        var telas = System.Windows.Forms.Screen.AllScreens;
        var principal = System.Windows.Forms.Screen.PrimaryScreen ?? telas[0];

        janela.Show();
        Win32Windows.PlacePhysical(janela, principal.Bounds, activate: true);

        foreach (var tela in telas.Where(t => !t.Equals(principal)))
        {
            var overlay = new OverlayWindow(viewModel);
            overlay.Show();
            Win32Windows.PlacePhysical(overlay, tela.Bounds, activate: false);
            sessao.Overlays.Add(overlay);
        }
    }

    /// <summary>Whether to skip an on-time alert because the user is away. | Se deve pular um alerta na hora por o usuário estar ausente.</summary>
    private bool ShouldSkipForAbsence(AlarmTriggeredEventArgs trigger, out TimeSpan ocioso)
    {
        ocioso = TimeSpan.Zero;

        if (trigger.Kind != TriggerKind.OnTime || trigger.Alarm.SkipIfIdleFor is not { } limite)
        {
            return false;
        }

        ocioso = _idle.IdleFor;
        return ocioso >= limite;
    }

    /// <summary>Resolves how to show now, downgrading missed alarms per policy. | Resolve como mostrar agora, rebaixando alarmes perdidos conforme a política.</summary>
    private static (PresentationMode Mode, SoundSpec Sound)? Resolve(AlarmTriggeredEventArgs trigger)
    {
        var perfil = trigger.EffectiveProfile;

        if (trigger.Kind != TriggerKind.Missed)
        {
            return (perfil.Presentation, trigger.EffectiveSound);
        }

        return perfil.WhenAway switch
        {
            MissedAlarmBehavior.Discard => null,

            // A missed alarm returns as a silent corner card. | Um alarme perdido volta como card silencioso no canto.
            MissedAlarmBehavior.ShowOnReturn => (PresentationMode.Corner, SoundSpec.Silent),

            MissedAlarmBehavior.FireOnReturn => (perfil.Presentation, trigger.EffectiveSound),

            _ => null,
        };
    }

    /// <summary>Closes an alert session: stops sound and closes overlays. | Encerra uma sessão de alerta: para o som e fecha os overlays.</summary>
    private void Encerrar(Guid alarmId)
    {
        if (!_abertos.Remove(alarmId, out var sessao))
        {
            return;
        }

        sessao.Audio.Dispose();

        foreach (var overlay in sessao.Overlays)
        {
            overlay.Close();
        }
    }

    /// <summary>An open alert: its window, sound and extra-monitor overlays. | Um alerta aberto: sua janela, som e overlays dos monitores extras.</summary>
    private sealed record AlertSession(AlertWindow Window, AlarmAudioPlayer Audio)
    {
        public List<OverlayWindow> Overlays { get; } = [];
    }
}
