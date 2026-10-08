using System.Globalization;

namespace DashyNMS.Mobile.Converters;

/// <summary>
/// "#3B82F6" to a colour - a graph series' colour square (#158), which Core
/// gives as hex so desktop and the phone draw the same.
/// </summary>
public sealed class HexColourConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var colour = value is string hex && Color.TryParse(hex, out var parsed) ? parsed : Colors.Transparent;
        return targetType == typeof(Brush) ? new SolidColorBrush(colour) : colour;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
