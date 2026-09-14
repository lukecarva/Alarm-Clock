using System.IO;
using System.Text;
using System.Text.Json;

namespace AlarmClock.App.Services;

/// <summary>Preferências do app. Poucas, num JSON à parte dos alarmes.</summary>
public sealed class AppSettings
{
    /// <summary>Código do idioma ("en" ou "pt-BR"). Nulo = decide pelo Windows.</summary>
    public string? Language { get; set; }
}

/// <summary>
/// Lê e grava <c>settings.json</c>. Estático porque é lido no arranque, antes do
/// contêiner de DI existir, e o instalador escreve o mesmo arquivo.
/// </summary>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var json = File.ReadAllText(AppPaths.SettingsFile, Encoding.UTF8);
                return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Preferência corrompida não pode impedir o app de abrir: começa do padrão.
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        AppPaths.EnsureCreated();

        var json = JsonSerializer.Serialize(settings, Options);
        var temp = AppPaths.SettingsFile + ".tmp";

        File.WriteAllText(temp, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        if (File.Exists(AppPaths.SettingsFile))
        {
            File.Replace(temp, AppPaths.SettingsFile, null);
        }
        else
        {
            File.Move(temp, AppPaths.SettingsFile);
        }
    }
}
