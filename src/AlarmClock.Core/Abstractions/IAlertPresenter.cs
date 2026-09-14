using AlarmClock.Core.Scheduling;

namespace AlarmClock.Core.Abstractions;

/// <summary>Shows a triggered alarm to the user. | Mostra ao usuário um alarme que disparou.</summary>
public interface IAlertPresenter
{
    /// <summary>Presents the alert for the given trigger. | Apresenta o alerta do disparo informado.</summary>
    void Show(AlarmTriggeredEventArgs trigger);
}
