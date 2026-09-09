using System.ComponentModel;
using System.Windows;
using AlarmClock.App.ViewModels;

namespace AlarmClock.App.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>
    /// Fechar a janela esconde na bandeja em vez de encerrar. Sair de verdade é
    /// pelo menu da bandeja — senão fica fácil demais desligar o despertador sem
    /// perceber.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!App.IsShuttingDown)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
