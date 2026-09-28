using System.Globalization;

namespace DashyNMS.Mobile.Converters;

/// <summary>A share (0-1) as a star column width - for proportion bars like desktop's alerts gauge.</summary>
public sealed class StarConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        new GridLength(value is double share && share > 0 ? share : 0, GridUnitType.Star);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
