using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AlarmClock.App.Views;

/// <summary>Some quando o valor é nulo ou uma string vazia.</summary>
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

/// <summary>Inverte um booleano. Útil para habilitar controles por ausência de algo.</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b && !b;
}
