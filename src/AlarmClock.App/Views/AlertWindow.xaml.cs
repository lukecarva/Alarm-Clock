using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using AlarmClock.App.ViewModels;
using AlarmClock.Core.Model;

namespace AlarmClock.App.Views;

/// <summary>Alert window: corner card, modal, or full-screen overlay. | Janela de alerta: card no canto, modal, ou overlay de tela cheia.</summary>
public partial class AlertWindow : Window
{
    /// <summary>How long the hold-to-dismiss button must be pressed. | Quanto tempo o botão de segurar precisa ficar pressionado.</summary>
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

        var auto = viewModel.Trigger.EffectiveProfile.AutoDismissAfter;
        if (auto is not null)
        {
            _autoDismiss = new DispatcherTimer { Interval = auto.Value };
            _autoDismiss.Tick += (_, _) =>
            {
                _autoDismiss.Stop();

                // Timeout closes without dismissing, so escalation can continue. | Tempo esgotado fecha sem dispensar, para a escalada poder continuar.
                viewModel.TimeoutClose();
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

    /// <summary>Activates the window (except the corner card, which keeps focus). | Ativa a janela (exceto o card do canto, que não rouba o foco).</summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
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

    // ---------- Hold to dismiss | Segurar para dispensar ----------

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

    /// <summary>Advances the hold progress and dismisses when it completes. | Avança o progresso do segurar e dispensa quando completa.</summary>
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

    // ---------- Type the phrase | Digitar a frase ----------

    /// <summary>Dismisses once the typed text matches the phrase. | Dispensa quando o texto digitado bate com a frase.</summary>
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
