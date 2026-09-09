namespace AlarmClock.Core.Abstractions;

/// <summary>
/// Há quanto tempo o teclado e o mouse não são tocados.
/// </summary>
/// <remarks>
/// Serve para duas coisas opostas: não alertar quem não está na frente do PC, e
/// perceber quem está na frente do PC há tempo demais.
/// </remarks>
public interface IIdleDetector
{
    TimeSpan IdleFor { get; }
}

/// <summary>Sempre presente. Usado nos testes e como padrão inofensivo.</summary>
public sealed class AlwaysPresentIdleDetector : IIdleDetector
{
    public TimeSpan IdleFor => TimeSpan.Zero;
}
