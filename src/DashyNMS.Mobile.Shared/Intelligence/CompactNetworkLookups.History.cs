using DashyNMS.Agent;
using DashyNMS.Agent.Alerts;
using DashyNMS.Agent.Devices;
using DashyNMS.Agent.Events;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Intelligence;

public sealed partial class CompactNetworkLookups
{
    private static Reply HistoryFailure(HostFailure? failure) => Reply.Say(failure switch
    {
        HostFailure.Refused => Refused,
        HostFailure.InvalidRequest => "The history request could not be completed. Check the scope and time window, or try again.",
        _ => Failed,
    });
    private static string HistoryPage(PageInfo page) => $" Page offset {Id(page.Offset)}; limit {Id(page.Limit)}; returned {Id(page.Returned)}; total {Id(page.Total)}."
        + (page.NextOffset is { } next ? $" Next offset {Id(next)}." : "");
    private static string HistoryWindow(EventWindowInfo window, TimeProvider time)
        => "Window " + IntelligenceTime.At(window.Since, time) + " to " + IntelligenceTime.At(window.Until, time)
            + (window.WindowComplete ? "; complete." : window.CoveredSince is { } covered
                ? "; incomplete; covered since " + IntelligenceTime.At(covered, time) + "."
                : "; coverage unknown; a source could not be read.");
    private static string TimelineText(TimelineEntry e, DateTimeOffset at, TimeProvider time)
        => IntelligenceTime.AtAndAgo(e.Time, at, time) + $"; source {Clean(e.Source)}; kind {Clean(e.Kind).Replace('_', ' ')}; severity {Clean(e.Severity)}"
            + (e.Rule is { } rule ? "; rule " + Clean(rule) : "")
            + (e.Text is { } text ? "; " + Clean(text) : "")
            + (e.User is { } user ? "; user " + Clean(user) : "")
            + (e.DurationMinutes is { } duration ? "; duration " + duration.ToString(System.Globalization.CultureInfo.InvariantCulture) + " minutes" : "");

    private static Reply DeviceEventRows(DeviceEventsResult found, DateTimeOffset at, TimeProvider time, string name)
    {
        var data = found.Data;
        var device = data.Device;
        var header = AlertUnavailable(data.Unavailable) + HistoryWindow(data.Window, time) + HistoryPage(data.Page);
        return Rows(header, new[] { TopologyDevice(device.DeviceId, name,
            $"{name}; hostname {Clean(device.Hostname)}; display {Clean(device.Display)}; IP {Clean(device.Ip)}; location {Clean(device.Location)}; state {device.State.ToDisplayString()}", at) }
            .Concat(data.Entries.Select(e => new Row(TimelineText(e, at, time)))), alsoOmitted: Math.Max(0, data.Page.Total - data.Page.Offset - data.Page.Returned));
    }
    private static Reply NetworkEventRows(NetworkEventsResult found, DateTimeOffset at, TimeProvider time)
    {
        var data = found.Data;
        var heading = (found.RulesUnavailable ? "Rule names unavailable. " : "") + HistoryWindow(data.Window, time) + HistoryPage(data.Page);
        return Rows(heading, data.Entries.Select(e => TopologyDevice(e.DeviceId, e.Display ?? e.Hostname,
            Clean(e.Display ?? e.Hostname) + "; hostname " + Clean(e.Hostname) + "; " + TimelineText(new(e.Time, e.Source, e.Kind, e.Severity, e.Rule, e.Text, e.User), at, time), at)),
            alsoOmitted: Math.Max(0, data.Page.Total - data.Page.Offset - data.Page.Returned));
    }
    private static Reply RaisedRows(RaisedAlertsResult found, DateTimeOffset at, TimeProvider time)
    {
        var data = found.Data;
        var heading = (found.RulesUnavailable ? "Rule severities unavailable. " : "")
            + (found.OpenStatusUnavailable ? "Current open status unavailable. " : "") + HistoryWindow(data.Window, time) + HistoryPage(data.Page);
        var rows = data.Devices.SelectMany(d => d.Alerts.Select(a =>
            TopologyDevice(d.DeviceId, d.Display ?? d.Hostname,
                $"{Clean(d.Display ?? d.Hostname)}; hostname {Clean(d.Hostname)}; state {Clean(d.DeviceStatus)}; rule {Clean(a.Rule)}; severity {Clean(a.Severity)}; raised "
                + IntelligenceTime.AtAndAgo(a.RaisedAt, at, time)
                + (a.RecoveredAt is { } recovered ? "; recovered " + IntelligenceTime.AtAndAgo(recovered, at, time) : "")
                + (a.LastedMinutes is { } lasted ? "; lasted " + lasted.ToString(System.Globalization.CultureInfo.InvariantCulture) + " minutes" : "")
                + "; still open " + (a.StillOpen is { } open ? open ? "yes" : "no" : "unknown")
                + (a.FaultCount is { } faults ? $"; faults {Id(faults)}" : "")
                + (a.RecoveryTimeUnknown == true ? "; recovery time unknown" : ""), at)));
        // A historical occurrence has no live alert ID: only its verified device can link.
        return Rows(heading, rows, alsoOmitted: Math.Max(0, data.Page.Total - data.Page.Offset - data.Page.Returned));
    }
}
