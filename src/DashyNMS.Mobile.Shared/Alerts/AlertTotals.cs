using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Alerts;

/// <summary>Displayed group counts, with the full fault total kept separate from the qualifying child rows.</summary>
public sealed record AlertTotals(int Critical, int Warning, int Ok, int Acknowledged, int Faults, bool UsesFaults = true)
{
    public int Groups => Critical + Warning + Ok + Acknowledged;
    public string Description => UsesFaults
        ? $"{Groups} {(Groups == 1 ? "alert" : "alerts")} · {Faults} {(Faults == 1 ? "fault" : "faults")} · visible to this account"
        : $"{Groups} {(Groups == 1 ? "alert" : "alerts")} visible to this account";

    public static AlertTotals From(IEnumerable<Alert> alerts, AlertSeverity minimum = AlertSeverity.Ok, bool includeAcknowledged = true)
    {
        var rows = alerts.ToList();
        var qualifying = AlertGrouping.ByDeviceAndRule(rows.Where(a => AlertCounting.Counts(a, minimum, includeAcknowledged)));
        var active = qualifying.Where(g => g.State != AlertState.Acknowledged).ToList();
        var counts = AlertCounting.CountGroups(active, minimum, includeAcknowledged);
        var keys = qualifying.Select(g => g.Key).ToHashSet();
        var faults = rows.Count(a => a.State != AlertState.Recovered && keys.Contains(new AlertGroupKey(a.DeviceId, a.RuleId)));
        return new(counts.Critical, counts.Warning, counts.Total - counts.Critical - counts.Warning, qualifying.Count - active.Count, faults, rows.Any(a => a.Source == AlertSource.Fault));
    }
}
