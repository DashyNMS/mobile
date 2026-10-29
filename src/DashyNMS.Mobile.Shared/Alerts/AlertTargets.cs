using System.Globalization;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Alerts;

/// <summary>Only qualified identities can cross a persisted notification or widget boundary safely.</summary>
public static class AlertTargets
{
    public const string MovedNotice = "This alert has moved on. Find its current group in Alerts.";

    public static AlertKey? ParseKey(string? text) => text is not null
        && (text.StartsWith("fault:", StringComparison.Ordinal) || text.StartsWith("alert:", StringComparison.Ordinal))
        && AlertKey.TryParse(text, out var key) && key.Id > 0 && key.ToString() == text ? key : null;

    public static AlertGroupKey? ParseGroup(string? text)
    {
        if (text is null || !text.StartsWith("device:", StringComparison.Ordinal)) return null;
        var parts = text[7..].Split("/rule:", StringSplitOptions.None);
        return parts.Length == 2 && Positive(parts[0], out var device) && Positive(parts[1], out var rule)
            && new AlertGroupKey(device, rule).ToString() == text ? new AlertGroupKey(device, rule) : null;
    }

    private static bool Positive(string text, out int id) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;
}
