using System.Windows;
using AlarmClock.App.ViewModels;

namespace AlarmClock.App.Views;

public partial class AlarmEditorWindow : Window
{
    public AlarmEditorWindow(AlarmEditorViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.CloseRequested += confirmado =>
        {
            DialogResult = confirmado;
            Close();
        };
    }
}
