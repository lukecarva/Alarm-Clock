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

public partial class App : Application
{
    // Escopo local do usuário: duas contas no mesmo Windows podem rodar o app
    // ao mesmo tempo, mas a mesma conta não.
    private const string SingleInstanceMutexName = @"Local\AlarmClock.SingleInstance";

    private Mutex? _singleInstanceMutex;
    private IHost? _host;

    /// <summary>
    /// Ligado quando o encerramento é intencional, para a janela principal
    /// saber que ali não é para esconder na bandeja — é para fechar mesmo.
    /// </summary>
    public static bool IsShuttingDown { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            // Fase 1 troca isto por um named pipe que traz a janela da instância
            // já rodando para a frente, em vez de simplesmente sumir.
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
                // CreateDefaultBuilder instala o ConsoleLifetime, que num app de
                // bandeja só serve para escrever "Press Ctrl+C to shut down" num
                // console que não existe. A última registração vence no DI.
                services.AddSingleton<IHostLifetime, WpfHostLifetime>();

                services.AddSingleton<ISystemClock, SystemClock>();
                services.AddSingleton<IIdleDetector, Win32IdleDetector>();
                services.AddSingleton<IAlarmStore>(sp => new JsonAlarmStore(
                    AppPaths.AlarmsFile,
                    sp.GetRequiredService<ILogger<JsonAlarmStore>>()));

                services.AddSingleton<IAlarmScheduler, AlarmScheduler>();
                services.AddSingleton<IAlertPresenter, WpfAlertPresenter>();

                services.AddSingleton<AlarmsService>();
                services.AddSingleton<AlarmDialogs>();
                services.AddSingleton<StartupRegistrar>();
                services.AddSingleton<TrayIconService>();

                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindowView>();

                // Por último: ao subir, ele já carrega os alarmes e começa a
                // tiquetaquear, e por isso precisa da bandeja de pé antes.
                services.AddHostedService<SchedulerHost>();
            })
            .Build();

        // A bandeja antes do host: o SchedulerHost escreve o status nela assim
        // que inicia.
        _host.Services.GetRequiredService<TrayIconService>().Show();

        _host.Start();

        var janela = _host.Services.GetRequiredService<MainWindowView>();
        MainWindow = janela;

        // --minimized: como o app sobe junto com o Windows, jogar a janela na
        // cara de quem acabou de ligar o PC seria péssima educação.
        if (!e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase))
        {
            janela.Show();
        }
    }

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

    private static void ConfigureLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .WriteTo.File(
                AppPaths.LogFilePattern,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                // Sem shared:true o Serilog mantém lock exclusivo e você não
                // consegue abrir o log enquanto o app roda — que é justamente
                // quando você precisa dele.
                shared: true,
                // Com BOM: sem ele o Bloco de Notas e o PowerShell 5.1 leem o
                // arquivo como ANSI e todo acento vira lixo.
                encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    /// <summary>
    /// Decide o idioma no arranque, antes de qualquer janela: a escolha salva
    /// (do instalador ou do seletor no app) vence; sem ela, segue o Windows;
    /// o fallback final é inglês. Não há troca a quente — o app reinicia.
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

        // Alinha as culturas de formatação padrão do processo ao idioma.
        CultureInfo.CurrentCulture = Loc.Culture;
        CultureInfo.CurrentUICulture = Loc.Culture;
        CultureInfo.DefaultThreadCurrentCulture = Loc.Culture;
        CultureInfo.DefaultThreadCurrentUICulture = Loc.Culture;
    }

    /// <summary>
    /// Um despertador que morre calado é pior que um que não existe: se o
    /// processo cair, o alarme não toca e você não fica sabendo. Tudo que
    /// escapar vai para o log antes de o processo sumir.
    /// </summary>
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

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Exceção não tratada na UI.");

        // Manter o processo vivo: uma falha ao desenhar uma janela não pode
        // derrubar o agendador junto.
        e.Handled = true;

        MessageBox.Show(
            Loc.Format("Error_UIBody", e.Exception.Message, AppPaths.LogsFolder),
            Loc.Get("App_Name"),
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }
}
