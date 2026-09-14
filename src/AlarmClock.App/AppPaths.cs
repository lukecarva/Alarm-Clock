using System.IO;

namespace AlarmClock.App;

/// <summary>Filesystem paths the app reads and writes, under %APPDATA%\AlarmClock. | Caminhos que o app lê e grava, em %APPDATA%\AlarmClock.</summary>
public static class AppPaths
{
    /// <summary>Root data folder. | Pasta raiz de dados.</summary>
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AlarmClock");

    /// <summary>Folder for log files. | Pasta dos arquivos de log.</summary>
    public static string LogsFolder => Path.Combine(Root, "logs");

    /// <summary>Rolling log file name pattern. | Padrão de nome do log rotativo.</summary>
    public static string LogFilePattern => Path.Combine(LogsFolder, "log-.txt");

    /// <summary>Path of the alarms file. | Caminho do arquivo de alarmes.</summary>
    public static string AlarmsFile => Path.Combine(Root, "alarms.json");

    /// <summary>Path of the settings file. | Caminho do arquivo de preferências.</summary>
    public static string SettingsFile => Path.Combine(Root, "settings.json");

    /// <summary>Path of the scheduler runtime-state file (snoozes and escalations). | Caminho do arquivo de estado de execução do agendador (adiamentos e escaladas).</summary>
    public static string SchedulerStateFile => Path.Combine(Root, "scheduler-state.json");

    /// <summary>Creates the data and log folders if missing. | Cria as pastas de dados e de log se não existirem.</summary>
    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogsFolder);
    }
}
