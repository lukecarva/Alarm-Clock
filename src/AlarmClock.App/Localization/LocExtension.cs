using System.Windows.Markup;
using AlarmClock.Core.Localization;

namespace AlarmClock.App.Localization;

/// <summary>
/// Extensão de marcação para localizar texto no XAML: <c>{loc:Loc Editor_Name}</c>.
/// Resolve na carga da janela — como o idioma é fixado no arranque, isso basta
/// (não há troca a quente; o app reinicia para mudar de idioma).
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key) => Key = key;

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.Get(Key);
}
