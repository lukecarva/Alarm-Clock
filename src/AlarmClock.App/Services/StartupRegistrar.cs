using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace AlarmClock.App.Services;

/// <summary>
/// Iniciar com o Windows. Usa HKCU, que não pede elevação — um despertador
/// pessoal não tem por que pedir permissão de administrador.
/// </summary>
public sealed class StartupRegistrar
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DespertadorProdutivo";

    private readonly ILogger<StartupRegistrar> _log;

    public StartupRegistrar(ILogger<StartupRegistrar> log) => _log = log;

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null)
        {
            _log.LogError("Chave Run do usuário indisponível.");
            return;
        }

        if (enabled)
        {
            var caminho = ExecutablePath();
            if (caminho is null)
            {
                _log.LogError("Não deu para descobrir o caminho do executável.");
                return;
            }

            // --minimized: subir direto para a bandeja, sem jogar a janela na
            // cara de quem acabou de ligar o PC.
            key.SetValue(ValueName, $"\"{caminho}\" --minimized");
            _log.LogInformation("Início automático ligado.");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            _log.LogInformation("Início automático desligado.");
        }
    }

    private static string? ExecutablePath()
    {
        // Environment.ProcessPath aponta para o .exe real, inclusive quando o
        // app é publicado single-file — Assembly.Location viria vazio nesse caso.
        return Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
    }
}
