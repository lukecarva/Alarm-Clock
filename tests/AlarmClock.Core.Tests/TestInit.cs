using System.Runtime.CompilerServices;
using AlarmClock.Core.Localization;

// Sem paralelismo: o idioma do Loc é estado global do processo, e o teste de
// localização o alterna. Serial mantém tudo determinístico (a suíte é rápida).
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace AlarmClock.Core.Tests;

internal static class TestInit
{
    /// <summary>
    /// Fixa o idioma em português antes de qualquer teste: as asserções de
    /// texto (Describe, recusas de adiamento) foram escritas em pt-BR.
    /// </summary>
    [ModuleInitializer]
    internal static void Init() => Loc.Set(AppLanguage.Portuguese);
}
