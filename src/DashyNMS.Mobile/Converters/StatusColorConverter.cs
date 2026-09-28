using System.Globalization;
using DashyNMS.Mobile.DeviceSections;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Converters;

/// <summary>
/// An <see cref="AlertSeverity"/>, <see cref="DeviceState"/> or <see cref="RowStatus"/> to its status
/// colour, from the app resources (CriticalColor, WarningColor, ...) - the
/// same colours desktop uses.
/// </summary>
public sealed class StatusColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string? key = value switch
        {
            AlertSeverity.Critical => "CriticalColor",
            AlertSeverity.Warning => "WarningColor",
            AlertSeverity.Ok => "OkColor",
            DeviceState.Up => "OkColor",
            DeviceState.Down => "CriticalColor",
            DeviceState.Maintenance => "MaintenanceColor",
            RowStatus.Ok => "OkColor",
            RowStatus.Warning => "WarningColor",
            RowStatus.Critical => "CriticalColor",
            RowStatus.None => null,
            _ => "InactiveColor",
        };

        // No status: ordinary text colour, whatever the theme.
        if (key is null)
        {
            return Application.Current?.RequestedTheme == AppTheme.Dark ? Colors.White : Colors.Black;
        }

        return Application.Current?.Resources.TryGetValue(key, out var color) == true ? color : Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
