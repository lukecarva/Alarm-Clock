using System.Windows;
using AlarmClock.App.ViewModels;

namespace AlarmClock.App.Views;

public partial class OverlayWindow : Window
{
    public OverlayWindow(AlertViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
