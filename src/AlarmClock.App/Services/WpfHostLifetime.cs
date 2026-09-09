using Microsoft.Extensions.Hosting;

namespace AlarmClock.App.Services;

/// <summary>
/// Substitui o <c>ConsoleLifetime</c> padrão do host genérico. Num app WPF de
/// bandeja quem manda no ciclo de vida é a classe <see cref="App"/>: o host não
/// deve tentar encerrar o processo por conta própria nem prender handlers de
/// Ctrl+C num console inexistente.
/// </summary>
public sealed class WpfHostLifetime : IHostLifetime
{
    public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
