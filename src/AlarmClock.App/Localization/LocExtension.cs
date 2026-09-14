using System.Windows.Markup;
using AlarmClock.Core.Localization;

namespace AlarmClock.App.Localization;

/// <summary>
/// XAML markup extension that localizes text: <c>{loc:Loc Editor_Name}</c>. | Extensão de marcação que localiza texto no XAML: <c>{loc:Loc Editor_Name}</c>.
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key) => Key = key;

    /// <summary>Localization key to look up. | Chave de localização a buscar.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Returns the localized string for <see cref="Key"/>. | Retorna a string localizada de <see cref="Key"/>.</summary>
    public override object ProvideValue(IServiceProvider serviceProvider) => Loc.Get(Key);
}
