using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AlarmClock.App.Views;

/// <summary>Collapses when the value is null or an empty string. | Some quando o valor é nulo ou string vazia.</summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var vazio = value is null || (value is string texto && string.IsNullOrWhiteSpace(texto));
        return vazio ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Inverts a boolean. | Inverte um booleano.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;
}
