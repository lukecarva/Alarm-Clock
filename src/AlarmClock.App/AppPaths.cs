// Projetos WPF não trazem System.IO nos implicit usings (evita colisão entre
// System.IO.Path e System.Windows.Shapes.Path), então aqui é explícito.
using System.IO;

namespace AlarmClock.App;

/// <summary>
/// Tudo que o app grava fica em %APPDATA%\AlarmClock. Um lugar só, legível e
/// editável à mão — a Fase 1 escreve alarms.json aqui com escrita atômica.
/// </summary>
public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AlarmClock");

    public static string LogsFolder => Path.Combine(Root, "logs");

    public static string LogFilePattern => Path.Combine(LogsFolder, "log-.txt");

    public static string AlarmsFile => Path.Combine(Root, "alarms.json");

    public static string SettingsFile => Path.Combine(Root, "settings.json");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogsFolder);
    }
}
