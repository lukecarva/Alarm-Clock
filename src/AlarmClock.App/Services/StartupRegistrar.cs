using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace AlarmClock.App.Services;

/// <summary>Registers/unregisters "start with Windows" under HKCU (no admin). | Registra/remove o "iniciar com o Windows" em HKCU (sem admin).</summary>
public sealed class StartupRegistrar(ILogger<StartupRegistrar> log)
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DespertadorProdutivo";

    private readonly ILogger<StartupRegistrar> _log = log;

    /// <summary>Whether start-with-Windows is currently set. | Se o início com o Windows está ativo.</summary>
    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string;
        }
    }

    /// <summary>Turns start-with-Windows on or off. | Liga ou desliga o início com o Windows.</summary>
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

            // --minimized: start straight to the tray. | --minimized: sobe direto para a bandeja.
            key.SetValue(ValueName, $"\"{caminho}\" --minimized");
            _log.LogInformation("Início automático ligado.");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            _log.LogInformation("Início automático desligado.");
        }
    }

    /// <summary>Path of the running executable. | Caminho do executável em execução.</summary>
    private static string? ExecutablePath() =>
        Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
}
