using System.ComponentModel;
using System.Windows;
using AlarmClock.App.ViewModels;

namespace AlarmClock.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // A atualização periódica dos textos ("em 42 min") só roda com a janela
        // visível — escondida na bandeja não há o que mostrar.
        IsVisibleChanged += (_, e) => _viewModel.SetActive((bool)e.NewValue);
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
