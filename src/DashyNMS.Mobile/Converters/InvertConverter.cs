using System.Globalization;

namespace DashyNMS.Mobile.Converters;

/// <summary>Not: for IsVisible on "nothing here yet" hints.</summary>
public sealed class InvertConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}
