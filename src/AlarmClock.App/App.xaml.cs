using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using AlarmClock.App.Services;
using AlarmClock.App.ViewModels;
using AlarmClock.Core.Abstractions;
using AlarmClock.Core.Localization;
using AlarmClock.Core.Persistence;
using AlarmClock.Core.Scheduling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using MainWindowView = AlarmClock.App.Views.MainWindow;

namespace AlarmClock.App;

/// <summary>Application entry point: DI, logging, single instance and startup. | Ponto de entrada do app: DI, log, instância única e arranque.</summary>
public partial class App : Application
{
    // Per-user scope for the single-instance mutex. | Escopo por usuário para o mutex de instância única.
    private const string SingleInstanceMutexName = @"Local\AlarmClock.SingleInstance";

    private Mutex? _singleInstanceMutex;
    private IHost? _host;

    /// <summary>True during an intentional shutdown, so the window really closes. | Verdadeiro durante o encerramento intencional, para a janela fechar de fato.</summary>
    public static bool IsShuttingDown { get; private set; }

    /// <summary>Wires up services and shows the tray icon and main window. | Monta os serviços e mostra o ícone da bandeja e a janela principal.</summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            // A second instance just exits. | Uma segunda instância apenas encerra.
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        AppPaths.EnsureCreated();
        ConfigureLogging();
        HookGlobalExceptionHandlers();
        ResolveLanguage();

        Log.Information("Despertador Produtivo iniciando ({Idioma}). Dados em {Root}", Loc.Code, AppPaths.Root);

        _host = Host.CreateDefaultBuilder(e.Args)
            .UseSerilog()
            .ConfigureServices(services =>
            {
                // Replace the console lifetime; this is a tray app. | Substitui o console lifetime; este é um app de bandeja.
                services.AddSingleton<IHostLifetime, WpfHostLifetime>();

                services.AddSingleton<ISystemClock, SystemClock>();
                services.AddSingleton<IIdleDetector, Win32IdleDetector>();
                services.AddSingleton<IAlarmStore>(sp => new JsonAlarmStore(
                    AppPaths.AlarmsFile,
                    sp.GetRequiredService<ILogger<JsonAlarmStore>>()));
                services.AddSingleton<ISchedulerStateStore>(sp => new JsonSchedulerStateStore(
                    AppPaths.SchedulerStateFile,
                    sp.GetRequiredService<ILogger<JsonSchedulerStateStore>>()));

                services.AddSingleton<IAlarmScheduler, AlarmScheduler>();
                services.AddSingleton<IAlertPresenter, WpfAlertPresenter>();

                services.AddSingleton<AlarmsService>();
                services.AddSingleton<AlarmDialogs>();
                services.AddSingleton<StartupRegistrar>();
                services.AddSingleton<TrayIconService>();

                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindowView>();

                services.AddHostedService<SchedulerHost>();
            })
            .Build();

        // Show the tray before starting the host, which writes its status. | Mostra a bandeja antes de iniciar o host, que escreve nela.
        _host.Services.GetRequiredService<TrayIconService>().Show();

        _host.Start();

        var janela = _host.Services.GetRequiredService<MainWindowView>();
        MainWindow = janela;

        // --minimized: start hidden in the tray. | --minimized: inicia escondido na bandeja.
        if (!e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase))
        {
            janela.Show();
        }
    }

    /// <summary>Tears down the host and releases the single-instance mutex. | Desmonta o host e libera o mutex de instância única.</summary>
    protected override void OnExit(ExitEventArgs e)
    {
        IsShuttingDown = true;
        Log.Information("Encerrando (código {Code}).", e.ApplicationExitCode);

        if (_host is not null)
        {
            _host.Services.GetRequiredService<TrayIconService>().Dispose();
            _host.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
            _host.Dispose();
        }

        Log.CloseAndFlush();

        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();

        base.OnExit(e);
    }

    /// <summary>Configures file logging (Serilog, rolling, shared, UTF-8 BOM). | Configura o log em arquivo (Serilog, rotativo, compartilhado, UTF-8 com BOM).</summary>
    private static void ConfigureLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .WriteTo.File(
                AppPaths.LogFilePattern,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                shared: true,
                encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    /// <summary>
    /// Picks the language at startup: saved setting, else Windows, else English. | Escolhe o idioma no arranque: preferência salva, senão o Windows, senão inglês.
    /// </summary>
    private static void ResolveLanguage()
    {
        var settings = SettingsStore.Load();

        var idioma = settings.Language is { Length: > 0 } code
            ? Loc.Parse(code)
            : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pt"
                ? AppLanguage.Portuguese
                : AppLanguage.English;

        Loc.Set(idioma);

        CultureInfo.CurrentCulture = Loc.Culture;
        CultureInfo.CurrentUICulture = Loc.Culture;
        CultureInfo.DefaultThreadCurrentCulture = Loc.Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Loc.Culture;
    }

    /// <summary>Logs any unhandled exception so the process never dies silently. | Registra qualquer exceção não tratada para o processo nunca morrer calado.</summary>
    private void HookGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Fatal(args.ExceptionObject as Exception, "Exceção não tratada no AppDomain. Encerrando: {Terminating}", args.IsTerminating);

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Exceção não observada em Task.");
            args.SetObserved();
        };
    }

    /// <summary>Logs a UI exception, keeps the app running, and warns the user. | Registra uma exceção da UI, mantém o app vivo e avisa o usuário.</summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Exceção não tratada na UI.");

        // Keep the process alive: a UI failure must not take down the scheduler. | Mantém o processo vivo: uma falha de UI não pode derrubar o agendador.
        e.Handled = true;

        MessageBox.Show(
            Loc.Format("Error_UIBody", e.Exception.Message, AppPaths.LogsFolder),
            Loc.Get("App_Name"),
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
