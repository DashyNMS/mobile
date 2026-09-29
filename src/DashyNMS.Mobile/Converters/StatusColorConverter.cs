using System.Globalization;
using DashyNMS.Mobile.DeviceSections;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Converters;

/// <summary>What a status value is drawn as: its colour (dot, fill), its text colour, or its tint (pill background).</summary>
public enum StatusShade
{
    Fill,
    Text,
    Tint,
}

/// <summary>
/// An <see cref="AlertSeverity"/>, <see cref="DeviceState"/> or <see cref="RowStatus"/> to its status
/// colour, from the app resources (CriticalColor, WarningColor, ...) - the
/// same colours desktop uses. <see cref="Shade"/> picks the readable text
/// version or the pale tint instead, for the current theme.
/// </summary>
public sealed class StatusColorConverter : IValueConverter
{
    public StatusShade Shade { get; set; }

    /// <summary>"Critical", "Warning", "Ok", "Maintenance" or "Inactive"; null for no status.</summary>
    internal static string? Status(object? value) => value switch
    {
        AlertSeverity.Critical => "Critical",
        AlertSeverity.Warning => "Warning",
        AlertSeverity.Ok => "Ok",
        DeviceState.Up => "Ok",
        DeviceState.Down => "Critical",
        DeviceState.Maintenance => "Maintenance",
        RowStatus.Ok => "Ok",
        RowStatus.Warning => "Warning",
        RowStatus.Critical => "Critical",
        RowStatus.None => null,
        _ => "Inactive",
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var status = Status(value);

        // No status: ordinary text colour, or nothing behind it.
        if (status is null)
        {
            return Shade switch
            {
                StatusShade.Tint => Colors.Transparent,
                _ => Resource(dark ? "TextDark" : "TextLight") ?? (dark ? Colors.White : Colors.Black),
            };
        }

        var key = Shade switch
        {
            StatusShade.Text => status + (dark ? "TextDark" : "TextLight"),
            StatusShade.Tint => status + "Tint",
            _ => status + "Color",
        };
        return Resource(key) ?? Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static Color? Resource(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var color) == true ? color as Color : null;
}
