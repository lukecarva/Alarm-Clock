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

/// <summary>
/// Escolhe e monta a janela do alerta a partir do perfil de urgência.
/// </summary>
public sealed class WpfAlertPresenter : IAlertPresenter
{
    private readonly IAlarmScheduler _scheduler;
    private readonly TrayIconService _tray;
    private readonly IIdleDetector _idle;
    private readonly ILogger<WpfAlertPresenter> _log;

    /// <summary>Alertas na tela, por alarme. Impede dois alertas do mesmo alarme.</summary>
    private readonly Dictionary<Guid, AlertSession> _abertos = [];

    public WpfAlertPresenter(
        IAlarmScheduler scheduler,
        TrayIconService tray,
        IIdleDetector idle,
        ILogger<WpfAlertPresenter> log)
    {
        _scheduler = scheduler;
        _tray = tray;
        _idle = idle;
        _log = log;
    }

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
                trigger.Alarm.Profile.DisplayName);

            return;
        }

        var (modo, som) = resolvido.Value;

        if (_abertos.ContainsKey(trigger.Alarm.Id))
        {
            _log.LogDebug("Alerta de {Titulo} já está na tela.", trigger.Alarm.Title);
            return;
        }

        // ToString() nos enums: sem isso o Serilog os renderiza entre aspas e o
        // log fica com [Normal/"Corner"] "OnTime".
        _log.LogInformation(
            "Alerta: {Titulo} [{Nivel}/{Modo}] motivo={Motivo}.",
            trigger.Alarm.Title,
            trigger.Alarm.Profile.DisplayName,
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
            // O conteúdo manda no tamanho: um alarme sem mensagem e sem
            // adiamento é um card baixo; um com três opções de adiar é alto.
            // Altura fixa cortaria os botões de baixo.
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

    private static void AbrirEmTelaCheia(AlertWindow janela, AlertViewModel viewModel, AlertSession sessao)
    {
        var telas = System.Windows.Forms.Screen.AllScreens;
        var principal = System.Windows.Forms.Screen.PrimaryScreen ?? telas[0];

        janela.Show();
        Win32Windows.PlacePhysical(janela, principal.Bounds, activate: true);

        // Uma janela por monitor: cobrir só a tela principal deixaria a saída
        // pela lateral, que é exatamente o que o nível Crítico não quer.
        foreach (var tela in telas.Where(t => !t.Equals(principal)))
        {
            var overlay = new OverlayWindow(viewModel);
            overlay.Show();
            Win32Windows.PlacePhysical(overlay, tela.Bounds, activate: false);
            sessao.Overlays.Add(overlay);
        }
    }

    /// <summary>
    /// Vale a pena alertar uma cadeira vazia?
    /// </summary>
    /// <remarks>
    /// Só para o disparo na hora: alarme perdido já tem a política do
    /// <c>WhenAway</c>, e adiamento foi você que pediu — descartar por ausência
    /// jogaria fora algo explicitamente adiado.
    /// </remarks>
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

    /// <summary>
    /// Como este alarme deve aparecer agora — o que pode não ser o que o perfil
    /// diz, se ele chegou atrasado.
    /// </summary>
    private static (PresentationMode Mode, SoundSpec Sound)? Resolve(AlarmTriggeredEventArgs trigger)
    {
        var perfil = trigger.Alarm.Profile;

        if (trigger.Kind != TriggerKind.Missed)
        {
            return (perfil.Presentation, trigger.Alarm.EffectiveSound);
        }

        return perfil.WhenAway switch
        {
            MissedAlarmBehavior.Discard => null,

            // Rebaixado de propósito: um alarme de três horas atrás não merece
            // tela cheia com sirene, mas você precisa saber que ele existiu.
            MissedAlarmBehavior.ShowOnReturn => (PresentationMode.Corner, SoundSpec.Silent),

            MissedAlarmBehavior.FireOnReturn => (perfil.Presentation, trigger.Alarm.EffectiveSound),

            _ => null,
        };
    }

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

    private sealed record AlertSession(AlertWindow Window, AlarmAudioPlayer Audio)
    {
        public List<OverlayWindow> Overlays { get; } = [];
    }
}
