using System.Runtime.CompilerServices;
using AlarmClock.Core.Localization;

// No parallelism: the Loc language is global state that one test switches. | Sem paralelismo: o idioma do Loc é estado global que um teste alterna.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace AlarmClock.Core.Tests;

/// <summary>Test-run setup. | Configuração da execução dos testes.</summary>
internal static class TestInit
{
    /// <summary>Sets the language to Portuguese before any test runs. | Fixa o idioma em português antes de qualquer teste.</summary>
    [ModuleInitializer]
    internal static void Init() => Loc.Set(AppLanguage.Portuguese);
}
