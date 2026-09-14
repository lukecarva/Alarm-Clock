namespace AlarmClock.Core.Abstractions;

/// <summary>Reports how long keyboard and mouse have been idle. | Informa há quanto tempo teclado e mouse estão parados.</summary>
public interface IIdleDetector
{
    /// <summary>Time since the last keyboard/mouse input. | Tempo desde a última entrada de teclado/mouse.</summary>
    TimeSpan IdleFor { get; }
}

/// <summary>Always reports present; used in tests and as a safe default. | Sempre reporta presença; usado em testes e como padrão seguro.</summary>
public sealed class AlwaysPresentIdleDetector : IIdleDetector
{
    public TimeSpan IdleFor => TimeSpan.Zero;
}
