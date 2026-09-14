using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using AlarmClock.Core.Localization;
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
public sealed class TrayIconService(IServiceProvider services, ILogger<TrayIconService> log) : IDisposable
{
    private readonly IServiceProvider _services = services;
    private readonly ILogger<TrayIconService> _log = log;

    private TaskbarIcon? _icon;
    private bool _disposed;

    public void Show()
    {
        if (_icon is not null)
        {
            return;
        }

        var abrir = new MenuItem { Header = Loc.Get("Tray_Open") };
        abrir.Click += (_, _) => ShowMainWindow();

        var sair = new MenuItem { Header = Loc.Get("Tray_Exit") };
        sair.Click += (_, _) =>
        {
            _log.LogInformation("Encerramento solicitado pelo menu da bandeja.");
            Application.Current.Shutdown();
        };

        _icon = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute)),
            ToolTipText = Loc.Get("App_Name"),
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
    /// Notificação nativa do Windows — o alerta do nível Sussurro. Sujeita ao
    /// Assistente de Foco, que pode engoli-la sem avisar: é justamente por isso
    /// que só o nível mais baixo usa este caminho.
    /// </summary>
    public void ShowBalloon(string title, string message)
    {
        _icon?.ShowNotification(title, message);
    }

    /// <summary>Texto do tooltip: "Próximo: Reunião em 42 min".</summary>
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
