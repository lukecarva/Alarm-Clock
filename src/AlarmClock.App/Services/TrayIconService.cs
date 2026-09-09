using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MainWindowView = AlarmClock.App.Views.MainWindow;

namespace AlarmClock.App.Services;

/// <summary>
/// A bandeja é o modo de operação normal do app: a janela principal é opcional,
/// o ícone é que fica. Construído em C# em vez de XAML de propósito — são três
/// itens de menu, e assim não há surpresa de DataContext não propagado.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly IServiceProvider _services;
    private readonly ILogger<TrayIconService> _log;

    private TaskbarIcon? _icon;
    private bool _disposed;

    public TrayIconService(IServiceProvider services, ILogger<TrayIconService> log)
    {
        _services = services;
        _log = log;
    }

    public void Show()
    {
        if (_icon is not null)
        {
            return;
        }

        var abrir = new MenuItem { Header = "Abrir" };
        abrir.Click += (_, _) => ShowMainWindow();

        var sair = new MenuItem { Header = "Sair" };
        sair.Click += (_, _) =>
        {
            _log.LogInformation("Encerramento solicitado pelo menu da bandeja.");
            Application.Current.Shutdown();
        };

        _icon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute)),
            ToolTipText = "Despertador Produtivo",
            ContextMenu = new ContextMenu
            {
                Items = { abrir, new Separator(), sair },
            },
        };

        _icon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();

        // enablesEfficiencyMode: false é obrigatório aqui. O modo de eficiência
        // (EcoQoS) deixa o Windows estrangular os timers do processo — que é
        // exatamente o que não pode acontecer num despertador.
        _icon.ForceCreate(enablesEfficiencyMode: false);

        _log.LogInformation("Ícone da bandeja criado.");
    }

    /// <summary>
    /// Texto do tooltip. A Fase 1 alimenta isto com o próximo disparo
    /// ("Próximo: Reunião em 42 min").
    /// </summary>
    public void SetStatus(string text)
    {
        if (_icon is not null)
        {
            _icon.ToolTipText = text;
        }
    }

    private void ShowMainWindow()
    {
        var window = _services.GetRequiredService<MainWindowView>();

        window.Show();

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _icon?.Dispose();
        _icon = null;
    }
}
