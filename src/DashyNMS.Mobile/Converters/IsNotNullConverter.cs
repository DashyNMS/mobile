using System.Globalization;

namespace DashyNMS.Mobile.Converters;

/// <summary>True for anything but null or blank text - for IsVisible on optional lines.</summary>
public sealed class IsNotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is string text ? !string.IsNullOrWhiteSpace(text) : value is not null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
