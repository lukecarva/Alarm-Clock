using System.Windows;
using AlarmClock.App.ViewModels;
using AlarmClock.Core.Localization;

namespace AlarmClock.App.Views;

/// <summary>Modal window to create or edit an alarm. | Janela modal para criar ou editar um alarme.</summary>
public partial class AlarmEditorWindow : Window
{
    public AlarmEditorWindow(AlarmEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // Cap the height to the work area so the form never exceeds the screen. | Limita a altura à área de trabalho para o formulário nunca passar da tela.
        MaxHeight = Math.Max(420, SystemParameters.WorkArea.Height - 60);

        viewModel.CloseRequested += confirmado =>
        {
            DialogResult = confirmado;
            Close();
        };

        viewModel.ValidationFailed += mensagem =>
            MessageBox.Show(
                this,
                mensagem,
                Loc.Get("App_Name"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
    }
}
