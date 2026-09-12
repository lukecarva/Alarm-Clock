using System.Windows;
using AlarmClock.App.ViewModels;

namespace AlarmClock.App.Views;

public partial class AlarmEditorWindow : Window
{
    public AlarmEditorWindow(AlarmEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        // SizeToContent sozinho deixaria a janela passar da tela. O teto vem da
        // área de trabalho real, então funciona igual em 768p e em 4K.
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
                "Despertador Produtivo",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
    }
}
