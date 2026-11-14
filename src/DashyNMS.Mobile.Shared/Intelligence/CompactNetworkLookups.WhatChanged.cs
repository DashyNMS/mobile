using DashyNMS.Agent.Events;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Intelligence;

public sealed partial class CompactNetworkLookups
{
    private static Reply WhatChangedRows(WhatChangedResult found, DateTimeOffset at, TimeProvider time, string name)
    {
        var data = found.Data;
        var config = data.Unimus;
        var logs = data.Graylog;
        // Integration failure/unknown states precede detail rows, so the budget cannot
        // hide a missing baseline or turn an unreadable source into no change.
        var heading = AlertUnavailable(data.Librenms.Unavailable)
            + (config.Error is not null ? "Config source unavailable. " : "")
            + (logs.Error is not null ? "Log source unavailable. " : "")
            + "Unimus " + (!config.Configured ? "not configured" : config.TurnedOff is not null ? "config data off"
                : config.DeviceFound == false ? "device not found" : config.ConfigChanged is { } changed ? changed ? "config changed" : "no config change" : "config change unknown") + ". "
            + "Graylog " + (!logs.Configured ? "not configured" : logs.Error is not null ? "unavailable" : logs.Skipped is not null ? "skipped for past window"
                : logs.MessageCount is { } count ? count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " messages" : "count unknown") + ". "
            + HistoryWindow(data.Window, time);
        var device = data.Device;
        var rows = new List<Row>
        {
            TopologyDevice(device.DeviceId, name, $"{name}; hostname {Clean(device.Hostname)}; display {Clean(device.Display)}; IP {Clean(device.Ip)}; location {Clean(device.Location)}; state {device.State.ToDisplayString()}", at),
        };
        if (config.Warning is { } warning) rows.Add(new("Config warning: " + Clean(warning)));
        if (config.LatestBackupSince is { } latest) rows.Add(new("Latest config version since " + IntelligenceTime.AtAndAgo(latest, at, time)));
        if (config.LastBackedUp is { } backed) rows.Add(new("Last backed up " + IntelligenceTime.AtAndAgo(backed, at, time)));
        if (config.ChangedAt is { } changedAt) rows.Add(new("Config changed " + IntelligenceTime.AtAndAgo(changedAt, at, time)));
        if (config.PreviousVersionSince is { } previous) rows.Add(new("Previous version since " + IntelligenceTime.AtAndAgo(previous, at, time)));
        if (config.ChangedLines is { } lines) rows.Add(new($"Config changed lines {Id(lines)}; removed {Count(config.LinesRemoved)}; added {Count(config.LinesAdded)}."));
        if (config.ConfigText is not null) rows.Add(new("Config text left out by the saved Summary choice."));
        if (logs.Skipped is not null) rows.Add(new("Past Graylog windows need search_logs with since and until."));
        rows.AddRange(data.Librenms.Entries.Select(e => new Row(TimelineText(e, at, time))));
        if (config.ConfigDiff is { } diff)
            rows.AddRange(diff.Split('\n').Where(line => line.Length > 0).Select(line => new Row("Config diff " + Clean(line))));
        rows.AddRange((logs.LogLines ?? []).Select(l => new Row($"Log {ChangeLogTime(l.Time, time)}; source {Clean(l.Source)}; level {Clean(l.Level)}; {Clean(l.Text)}")));
        return Rows(heading, rows, alsoOmitted: (data.Librenms.Omitted ?? 0) + (config.ConfigDiffLinesOmitted ?? 0));
    }
    private static string ChangeLogTime(string? value, TimeProvider time)
        => DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed)
            ? IntelligenceTime.At(parsed.UtcDateTime, time) : Clean(value);

}
