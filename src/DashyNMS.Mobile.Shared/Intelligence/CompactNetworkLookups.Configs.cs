using System.Globalization;
using DashyNMS.Agent;
using DashyNMS.Agent.Summaries;
using DashyNMS.Agent.Unimus;

namespace DashyNMS.Mobile.Intelligence;

public sealed partial class CompactNetworkLookups
{
    internal const string ConfigOff = "Config lookups are turned off in Intelligence settings.";
    private static Reply ConfigFailure(HostFailure? failure) => Reply.Say(failure switch
    {
        HostFailure.Refused => Refused,
        HostFailure.InvalidRequest => "No matching text backup or comparison is available. Check the device, backup IDs, scope and time window.",
        _ => Failed,
    });

    private static string BackupTime(DateTime? instant, TimeProvider time) => instant is { } at ? IntelligenceTime.At(at, time) : "unknown";
    private static string BackupVersionText(BackupVersion version, TimeProvider time)
        => $"Backup {Id(version.Id)}; valid since {BackupTime(version.ValidSince, time)}"
            + (version.ValidUntil is { } until ? "; valid until " + IntelligenceTime.At(until, time) : "")
            + (version.Newest is { } newest ? "; newest " + (newest ? "yes" : "no") : "");

    /// <summary>Shared backup warnings and paging stay intact; the adapter only localises their dates.</summary>
    private static string BackupSummary(ConfigBackupResult found, TimeProvider time)
    {
        var summary = found.Summary;
        foreach (var instant in found.Data.Backups.Append(found.Data.Backup).SelectMany(b => new[] { b.ValidSince, b.ValidUntil }).OfType<DateTime>().Distinct())
        {
            summary = summary.Replace(instant.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC", IntelligenceTime.At(instant, time), StringComparison.Ordinal);
            summary = summary.Replace(instant.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), IntelligenceTime.At(instant, time), StringComparison.Ordinal);
        }
        return summary;
    }
    private static Row UnimusDeviceRow(UnimusDeviceInfo device)
        => new($"Unimus device {Id(device.Id)}; address {Clean(device.Address)}; description {Clean(device.Description)}"
            + (device.Vendor is null ? "" : "; vendor " + Clean(device.Vendor))
            + (device.Model is null ? "" : "; model " + Clean(device.Model)));

    private static Reply ConfigBackupRows(ConfigBackupResult found, DateTimeOffset at, TimeProvider time, Func<DeviceBrief, string> name)
    {
        var data = found.Data;
        var page = data.Lines;
        var heading = (data.ConfigText is null ? "" : "Config text left out by the saved Summary choice. ")
            + BackupVersionText(data.Backup, time) + ". " + BackupSummary(found, time)
            + $" Lines total {Id(page.Total)}; offset {Id(page.Offset)}; returned {Id(page.Returned)}."
            + (page.NextOffset is { } next ? $" Next offset {Id(next)}." : "") + $" Backups total {Id(data.BackupsTotal)}.";
        var rows = new List<Row> { FleetDevice(data.Device, name, "Config backup", at), UnimusDeviceRow(data.UnimusDevice) };
        if (data.Config is { Length: > 0 } config)
            rows.AddRange(config.Split('\n').Select((line, index) => new Row($"Config line {Id(page.Offset + index + 1)}: " + (line.Length == 0 ? "(blank)" : Clean(line)))));
        rows.AddRange(data.Backups.Select(b => new Row(BackupVersionText(b, time))));
        return Rows(heading, rows, alsoOmitted: Math.Max(0, data.BackupsTotal - data.Backups.Count)
            + (data.Config is null ? 0 : Math.Max(0, page.Total - page.Offset - page.Returned)));
    }
    private static Reply ConfigDiffRows(ConfigDiffResult found, DateTimeOffset at, TimeProvider time, Func<DeviceBrief, string> name)
    {
        var data = found.Data;
        var heading = (data.ConfigText is null ? "" : "Config text left out by the saved Summary choice. ")
            + $"Config diff: changed lines {Id(data.ChangedLines)}; removed {Id(data.LinesRemoved)}; added {Id(data.LinesAdded)}."
            + " From " + BackupVersionText(data.From, time) + ". To " + BackupVersionText(data.To, time) + ".";
        var rows = new List<Row> { FleetDevice(data.Device, name, "Config comparison", at) };
        if (data.ConfigDiff is { Length: > 0 } diff)
            rows.AddRange(diff.Split('\n').Select(line => new Row("Config diff " + Clean(line))));
        return Rows(heading, rows, alsoOmitted: data.ConfigDiffLinesOmitted ?? 0);
    }
    private static Reply ConfigChangesRows(RecentConfigChangesResult found, DateTimeOffset at, TimeProvider time, Func<DeviceBrief, string> name)
    {
        var data = found.Data;
        var heading = $"Unimus devices checked {Id(found.DevicesChecked)}; unreadable {Id(found.UnreadableDevices)}. "
            + "Window " + IntelligenceTime.At(data.Window.Since, time) + " to " + IntelligenceTime.At(data.Window.Until, time) + "." + HistoryPage(data.Page);
        var rows = data.Changes.Select(change => FleetDevice(change.Device, name,
            $"Unimus device {Id(change.UnimusDevice.Id)}; address {Clean(change.UnimusDevice.Address)}; description {Clean(change.UnimusDevice.Description)}; changed at {BackupTime(change.ChangedAt, time)}"
            + (change.PreviousVersionSince is { } previous ? "; previous version since " + IntelligenceTime.At(previous, time) : "")
            + (change.FirstBackup is { } first ? "; first backup " + (first ? "yes" : "no") : "")
            + $"; newest backup ID {Id(change.NewestBackupId)}", at)).ToList();
        rows.AddRange((data.Unreadable ?? []).Select(u => new Row($"Unreadable Unimus device {Clean(u.UnimusDevice)}; {Clean(u.Message)}")));
        return Rows(heading, rows, alsoOmitted: found.UnreadableOmitted + Math.Max(0, data.Page.Total - data.Page.Offset - data.Page.Returned));
    }
}
