using System.ComponentModel;
using System.Windows;
using AlarmClock.App.ViewModels;

namespace AlarmClock.App.Views;

/// <summary>The main window: alarms and daily-habits tabs. | A janela principal: abas de alarmes e de dia a dia.</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // Refresh the relative texts only while the window is visible. | Atualiza os textos relativos só enquanto a janela está visível.
        IsVisibleChanged += (_, e) => _viewModel.SetActive((bool)e.NewValue);
    }

    /// <summary>Closing hides to the tray instead of quitting. | Fechar esconde na bandeja em vez de encerrar.</summary>
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
