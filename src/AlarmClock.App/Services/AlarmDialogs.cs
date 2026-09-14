using System.Windows;
using AlarmClock.App.ViewModels;
using AlarmClock.App.Views;
using AlarmClock.Core.Abstractions;
using AlarmClock.Core.Localization;
using AlarmClock.Core.Model;

namespace AlarmClock.App.Services;

/// <summary>Opens the app's modal dialogs, keeping view models UI-free. | Abre os diálogos modais do app, mantendo as view models sem UI.</summary>
public sealed class AlarmDialogs
{
    private readonly ISystemClock _clock;

    public AlarmDialogs(ISystemClock clock) => _clock = clock;

    /// <summary>Shows the editor; returns the edited alarm, or null if cancelled. | Mostra o editor; retorna o alarme editado, ou nulo se cancelado.</summary>
    public Alarm? Edit(Alarm? existente)
    {
        var viewModel = new AlarmEditorViewModel(_clock, existente);

        var janela = new AlarmEditorWindow(viewModel)
        {
            Owner = Application.Current.MainWindow,
        };

        return janela.ShowDialog() == true ? viewModel.Result : null;
    }

    /// <summary>Asks the user to confirm deleting an alarm. | Pede ao usuário para confirmar a exclusão de um alarme.</summary>
    public bool ConfirmDelete(string titulo)
    {
        var resposta = MessageBox.Show(
            Loc.Format("Confirm_Delete", titulo),
            Loc.Get("App_Name"),
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        return resposta == MessageBoxResult.Yes;
    }
}
