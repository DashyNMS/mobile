using System.Globalization;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Converters;

/// <summary>
/// An <see cref="AlertSeverity"/> or <see cref="DeviceState"/> to its status
/// colour, from the app resources (CriticalColor, WarningColor, ...) - the
/// same colours desktop uses.
/// </summary>
public sealed class StatusColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            AlertSeverity.Critical => "CriticalColor",
            AlertSeverity.Warning => "WarningColor",
            AlertSeverity.Ok => "OkColor",
            DeviceState.Up => "OkColor",
            DeviceState.Down => "CriticalColor",
            DeviceState.Maintenance => "MaintenanceColor",
            _ => "InactiveColor",
        };

        return Application.Current?.Resources.TryGetValue(key, out var color) == true ? color : Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
