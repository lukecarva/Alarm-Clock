namespace AlarmClock.Core.Model;

/// <summary>
/// O quanto um alarme tem direito de atrapalhar sua vida. Não é só um rótulo:
/// cada nível resolve para um <see cref="UrgencyProfile"/> completo.
/// </summary>
public enum UrgencyLevel
{
    /// <summary>Toast silencioso. Passa e some.</summary>
    Whisper,

    /// <summary>Card no canto com um toque. O padrão.</summary>
    Normal,

    /// <summary>Janela central que não sai sozinha.</summary>
    High,

    /// <summary>Tela cheia em todos os monitores, exige ação deliberada.</summary>
    Critical,
}

/// <summary>Que tipo de janela o alerta usa.</summary>
public enum PresentationMode
{
    /// <summary>Notificação nativa do Windows. Sujeita ao Assistente de Foco.</summary>
    Toast,

    /// <summary>Card próprio no canto da tela.</summary>
    Corner,

    /// <summary>Janela centralizada, topmost.</summary>
    Modal,

    /// <summary>Overlay em tela cheia, em todos os monitores.</summary>
    Fullscreen,
}

/// <summary>O que o usuário precisa fazer para o alerta ir embora.</summary>
public enum DismissMode
{
    /// <summary>Some sozinho.</summary>
    Auto,

    /// <summary>Um clique resolve.</summary>
    Click,

    /// <summary>Segurar o botão por alguns segundos — anti-clique-reflexo.</summary>
    HoldButton,

    /// <summary>Digitar uma frase. O mais difícil de fazer no automático.</summary>
    TypePhrase,
}

/// <summary>O que fazer com um alarme que disparou com o PC dormindo ou você longe.</summary>
public enum MissedAlarmBehavior
{
    /// <summary>Esquece. Serve para lembretes que só fazem sentido na hora.</summary>
    Discard,

    /// <summary>Guarda e mostra como "perdido" quando você voltar.</summary>
    ShowOnReturn,

    /// <summary>Dispara de verdade assim que você voltar.</summary>
    FireOnReturn,
}

/// <summary>
/// Como o alerta soa. Sem <see cref="FilePath"/>, o app gera o tom internamente
/// — assim o projeto não depende de arquivos de áudio de terceiros.
/// </summary>
public sealed record SoundSpec
{
    public static SoundSpec Silent { get; } = new() { IsSilent = true };

    public bool IsSilent { get; init; }

    /// <summary>Arquivo de áudio do usuário. Nulo = tom gerado pelo app.</summary>
    public string? FilePath { get; init; }

    /// <summary>0..1.</summary>
    public double Volume { get; init; } = 0.7;

    /// <summary>Toca sem parar até o alerta ser dispensado.</summary>
    public bool Loop { get; init; }

    /// <summary>Intervalo entre repetições. <see cref="TimeSpan.Zero"/> = toca uma vez só.</summary>
    public TimeSpan RepeatEvery { get; init; }

    /// <summary>Sobe o volume gradualmente. Um susto seco às 7h é desnecessário.</summary>
    public TimeSpan FadeIn { get; init; }
}

/// <summary>Quanto o alarme pode ser empurrado para depois, e quantas vezes.</summary>
public sealed record SnoozePolicy
{
    public static SnoozePolicy None { get; } = new() { Options = [], MaxCount = 0 };

    public required IReadOnlyList<TimeSpan> Options { get; init; }

    /// <summary>Quantos adiamentos seguidos são permitidos antes de o app parar de aceitar.</summary>
    public required int MaxCount { get; init; }

    public bool IsEnabled => MaxCount > 0 && Options.Count > 0;

    public TimeSpan DefaultOption => Options.Count > 0 ? Options[0] : TimeSpan.FromMinutes(5);
}

/// <summary>
/// A definição completa de um nível de urgência. Os quatro níveis são apenas
/// presets deste record: mudar o comportamento de "Crítico" é editar dados,
/// não sair caçando <c>if</c> espalhado pela UI.
/// </summary>
public sealed record UrgencyProfile
{
    public required UrgencyLevel Level { get; init; }

    public required string DisplayName { get; init; }

    public required PresentationMode Presentation { get; init; }

    public required SoundSpec Sound { get; init; }

    /// <summary>Nulo = o alerta fica na tela até o usuário agir.</summary>
    public TimeSpan? AutoDismissAfter { get; init; }

    public required DismissMode Dismiss { get; init; }

    public required SnoozePolicy Snooze { get; init; }

    public required MissedAlarmBehavior WhenAway { get; init; }

    /// <summary>Frase exigida quando <see cref="Dismiss"/> é <see cref="DismissMode.TypePhrase"/>.</summary>
    public string DismissPhrase { get; init; } = "acordei";
}

/// <summary>
/// Os presets. A Fase 2 deixa o usuário sobrescrever isto pelo settings.json;
/// por enquanto são as constantes do projeto, num lugar só.
/// </summary>
public static class UrgencyProfiles
{
    private static readonly Dictionary<UrgencyLevel, UrgencyProfile> Presets = new()
    {
        [UrgencyLevel.Whisper] = new UrgencyProfile
        {
            Level = UrgencyLevel.Whisper,
            DisplayName = "Sussurro",
            Presentation = PresentationMode.Toast,
            Sound = SoundSpec.Silent,
            AutoDismissAfter = TimeSpan.FromSeconds(7),
            Dismiss = DismissMode.Auto,
            Snooze = SnoozePolicy.None,
            WhenAway = MissedAlarmBehavior.Discard,
        },

        [UrgencyLevel.Normal] = new UrgencyProfile
        {
            Level = UrgencyLevel.Normal,
            DisplayName = "Normal",
            Presentation = PresentationMode.Corner,
            Sound = new SoundSpec { Volume = 0.5 },
            AutoDismissAfter = TimeSpan.FromSeconds(30),
            Dismiss = DismissMode.Click,
            Snooze = new SnoozePolicy
            {
                Options = [TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(15)],
                MaxCount = int.MaxValue,
            },
            WhenAway = MissedAlarmBehavior.ShowOnReturn,
        },

        [UrgencyLevel.High] = new UrgencyProfile
        {
            Level = UrgencyLevel.High,
            DisplayName = "Importante",
            Presentation = PresentationMode.Modal,
            Sound = new SoundSpec { Volume = 0.7, RepeatEvery = TimeSpan.FromSeconds(10) },
            AutoDismissAfter = null,
            Dismiss = DismissMode.Click,
            Snooze = new SnoozePolicy { Options = [TimeSpan.FromMinutes(5)], MaxCount = 3 },
            WhenAway = MissedAlarmBehavior.ShowOnReturn,
        },

        [UrgencyLevel.Critical] = new UrgencyProfile
        {
            Level = UrgencyLevel.Critical,
            DisplayName = "Crítico",
            Presentation = PresentationMode.Fullscreen,
            Sound = new SoundSpec { Volume = 1.0, Loop = true, FadeIn = TimeSpan.FromSeconds(5) },
            AutoDismissAfter = null,
            Dismiss = DismissMode.HoldButton,
            Snooze = new SnoozePolicy { Options = [TimeSpan.FromMinutes(2)], MaxCount = 1 },
            WhenAway = MissedAlarmBehavior.FireOnReturn,
        },
    };

    public static UrgencyProfile Get(UrgencyLevel level) => Presets[level];

    public static IEnumerable<UrgencyProfile> All => Presets.Values;
}
