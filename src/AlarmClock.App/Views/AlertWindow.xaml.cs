using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AlarmClock.App.ViewModels;
using AlarmClock.Core.Model;

namespace AlarmClock.App.Views;

public partial class AlertWindow : Window
{
    /// <summary>Quanto tempo o botão do nível Crítico precisa ficar pressionado.</summary>
    private static readonly TimeSpan HoldDuration = TimeSpan.FromSeconds(3);

    private readonly AlertViewModel _viewModel;
    private readonly DispatcherTimer? _autoDismiss;
    private readonly DispatcherTimer _holdTicker;

    private DateTime _holdStart;

    public AlertWindow(AlertViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        viewModel.CloseRequested += Close;

        var auto = viewModel.Trigger.Alarm.Profile.AutoDismissAfter;
        if (auto is not null)
        {
            _autoDismiss = new DispatcherTimer { Interval = auto.Value };
            _autoDismiss.Tick += (_, _) =>
            {
                _autoDismiss.Stop();
                viewModel.DismissCommand.Execute(null);
            };
            _autoDismiss.Start();
        }

        _holdTicker = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _holdTicker.Tick += OnHoldTick;

        HoldButton.PreviewMouseLeftButtonDown += OnHoldStarted;
        HoldButton.PreviewMouseLeftButtonUp += OnHoldCancelled;
        HoldButton.MouseLeave += OnHoldCancelled;

        PhraseBox.TextChanged += OnPhraseChanged;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // O card do canto não rouba foco: interromper quem está digitando é
        // justamente o que o nível Normal não deve fazer.
        if (_viewModel.Mode == PresentationMode.Corner)
        {
            return;
        }

        Activate();

        if (_viewModel.DismissMode == DismissMode.TypePhrase)
        {
            PhraseBox.Focus();
        }
    }

    // ---------- Segurar para dispensar ----------

    private void OnHoldStarted(object sender, MouseButtonEventArgs e)
    {
        _holdStart = DateTime.UtcNow;
        _holdTicker.Start();
    }

    private void OnHoldCancelled(object sender, MouseEventArgs e)
    {
        _holdTicker.Stop();
        _viewModel.HoldProgress = 0;
    }

    private void OnHoldTick(object? sender, EventArgs e)
    {
        var decorrido = DateTime.UtcNow - _holdStart;
        var progresso = decorrido / HoldDuration;

        if (progresso >= 1d)
        {
            _holdTicker.Stop();
            _viewModel.HoldProgress = 1;
            _viewModel.DismissCommand.Execute(null);
            return;
        }

        _viewModel.HoldProgress = progresso;
    }

    // ---------- Digitar a frase ----------

    private void OnPhraseChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var digitado = PhraseBox.Text.Trim();

        if (string.Equals(digitado, _viewModel.DismissPhrase, StringComparison.CurrentCultureIgnoreCase))
        {
            _viewModel.DismissCommand.Execute(null);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _autoDismiss?.Stop();
        _holdTicker.Stop();

        _viewModel.CloseRequested -= Close;
        _viewModel.Dispose();

        base.OnClosed(e);
    }
}
