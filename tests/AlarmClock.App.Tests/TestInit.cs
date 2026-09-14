using System.Runtime.CompilerServices;
using AlarmClock.Core.Localization;

// No parallelism: the Loc language is global state that tests switch. | Sem paralelismo: o idioma do Loc é estado global que os testes alternam.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace AlarmClock.App.Tests;

/// <summary>Test-run setup. | Configuração da execução dos testes.</summary>
internal static class TestInit
{
    /// <summary>Sets the language to English before any test runs. | Fixa o idioma em inglês antes de qualquer teste.</summary>
    [ModuleInitializer]
    internal static void Init() => Loc.Set(AppLanguage.English);
}
