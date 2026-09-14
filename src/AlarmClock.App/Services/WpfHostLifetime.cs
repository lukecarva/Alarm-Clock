using Microsoft.Extensions.Hosting;

namespace AlarmClock.App.Services;

/// <summary>
/// No-op host lifetime for a WPF tray app; the <see cref="App"/> class owns the
/// process lifecycle. | Ciclo de vida vazio para um app WPF de bandeja; quem controla o processo é a
/// classe <see cref="App"/>.
/// </summary>
public sealed class WpfHostLifetime : IHostLifetime
{
    public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
