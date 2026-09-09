using AlarmClock.Core.Scheduling;

namespace AlarmClock.Core.Abstractions;

/// <summary>
/// Põe um alarme na frente do usuário. O agendador só avisa que chegou a hora;
/// como isso vira pixel na tela é problema da camada de UI.
/// </summary>
public interface IAlertPresenter
{
    void Show(AlarmTriggeredEventArgs trigger);
}
