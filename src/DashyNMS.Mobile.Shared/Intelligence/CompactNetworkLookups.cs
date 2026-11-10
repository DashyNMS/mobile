using System.Globalization;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>Creates read-only lookups scoped to the current connection, for a model session.</summary>
public interface IIntelligenceLookups
{
    /// <summary>Creates a fresh set for one model session and the current connection.</summary>
    IReadOnlyList<IntelligenceLookup> Create();
}

/// <summary>
/// Keeps network replies within the measured on-device budget. Each created set
/// belongs to one connection; after a switch it refuses calls and discards late replies.
/// </summary>
public sealed class CompactNetworkLookups(
    ILibreNmsClient client, ISettingsStore settings, ISessionService session, TimeProvider? timeProvider = null) : IIntelligenceLookups
{
    public const int MaxCharacters = 900;
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    internal const string Failed = "The lookup failed. LibreNMS could not be reached.";
    internal const string Changed = "The connection changed. Start a new chat.";

    /// <summary>Captures the connection so replies cannot cross between networks.</summary>
    public IReadOnlyList<IntelligenceLookup> Create()
    {
        var connection = session.Connection;
        var style = settings.Current.DeviceNameStyle;
        bool IsCurrent() => connection is not null && ReferenceEquals(connection, session.Connection);

        IntelligenceLookup Lookup(string name, string purpose, Func<string, CancellationToken, Task<string>> run) =>
            new(name, purpose, async (query, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (connection is null && session.Connection is null)
                    return "Sign in to LibreNMS to use network lookups.";
                if (!IsCurrent()) return Changed;
                using var deadline = new CancellationTokenSource(Timeout, timeProvider ?? TimeProvider.System);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
                try
                {
                    // WaitAsync also bounds transports which do not honour cancellation.
                    var text = await run(query, timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    return IsCurrent() ? text : Changed;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception) { return IsCurrent() ? Failed : Changed; }
            });

        string DeviceRow(Device d) => $"device {d.DeviceId}: {Clean(style.Resolve(d, d.Hostname))}; {d.ReportedState}; IP {Clean(d.Ip)}";

        return
        [
            Lookup("network_overview", "Network counts and the open alerts. Query is unused.", async (_, ct) =>
            {
                var devices = client.Devices.ListAsync(ct);
                var alerts = client.Alerts.ListAsync(AlertQuery.Open, ct);
                await Task.WhenAll(devices, alerts).ConfigureAwait(false);
                var ds = await devices.ConfigureAwait(false);
                var als = await alerts.ConfigureAwait(false);
                return Rows($"Devices: {ds.Count} total, {ds.Count(d => d.ReportedState == DeviceState.Up)} up, {ds.Count(d => d.ReportedState == DeviceState.Down)} down. Open alerts: {als.Count}, {als.Count(a => a.Severity == AlertSeverity.Critical)} critical, {als.Count(a => a.Severity == AlertSeverity.Warning)} warning, {als.Count(a => a.State == AlertState.Acknowledged)} acknowledged.",
                    als.OrderBy(a => a.State == AlertState.Acknowledged ? 1 : 0)
                        .ThenBy(a => a.Severity == AlertSeverity.Critical ? 0 : a.Severity == AlertSeverity.Warning ? 1 : 2)
                        .Select(AlertRow).Concat(ds.Where(d => d.ReportedState == DeviceState.Down).OrderBy(d => d.DeviceId).Select(DeviceRow)));
            }),
            Lookup("find_device", "Find devices by name or IP. Query is the search text.", async (query, ct) =>
            {
                if (string.IsNullOrWhiteSpace(query)) return "Enter a device name or IP address.";
                var devices = await client.Devices.ListAsync(ct).ConfigureAwait(false);
                var found = devices.Where(d => (d.Hostname?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (d.SysName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (d.Ip?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)).OrderBy(d => d.DeviceId).ToList();
                return Rows($"Matching devices: {found.Count}.", found.Select(DeviceRow));
            }),
            Lookup("device_summary", "Device state and hardware. Query is a numeric device id.", async (query, ct) =>
            {
                if (!TryId(query, out var id)) return "Supply a numeric device id from find_device.";
                var device = await client.Devices.GetAsync(id.ToString(CultureInfo.InvariantCulture), ct).ConfigureAwait(false);
                return device is null ? "Device not found." : Rows(DeviceRow(device),
                    [$"OS: {Clean(device.Os)}; hardware: {Clean(device.Hardware)}; location: {Clean(device.Location)}; uptime: {device.Uptime} seconds."]);
            }),
            Lookup("active_alerts", "Open alerts, unacknowledged first. Query is a device id or empty for all.", async (query, ct) =>
            {
                if (!string.IsNullOrWhiteSpace(query) && !TryId(query, out _)) return "Supply a numeric device id or an empty query.";
                var alerts = await client.Alerts.ListAsync(AlertQuery.Open, ct).ConfigureAwait(false);
                var found = alerts.Where(a => string.IsNullOrWhiteSpace(query) || a.DeviceId == int.Parse(query, CultureInfo.InvariantCulture))
                    .OrderBy(a => a.State == AlertState.Acknowledged ? 1 : 0)
                    .ThenBy(a => a.Severity == AlertSeverity.Critical ? 0 : a.Severity == AlertSeverity.Warning ? 1 : 2)
                    .ThenBy(a => a.Id).ToList();
                return Rows($"Open alerts: {found.Count}.", found.Select(AlertRow));
            }),
            Lookup("device_sensors", "Sensor readings. Query is a numeric device id.", async (query, ct) =>
            {
                if (!TryId(query, out var id)) return "Supply a numeric device id from find_device.";
                var sensors = await client.Sensors.ListAsync(ct).ConfigureAwait(false);
                var found = sensors.Where(s => s.DeviceId == id).OrderBy(s => s.SensorId).ToList();
                return Rows($"Sensors on device {id}: {found.Count}.", found.Select(s =>
                    $"sensor {s.SensorId}, device {s.DeviceId}: {Clean(s.Description)}; {Clean(s.SensorClass)}; reading {(s.HasReading ? s.Current!.Value.ToString(CultureInfo.InvariantCulture) : "unavailable")}; unit {Clean(s.Unit)}"));
            }),
        ];
    }

    /// <summary>Accepts only positive ids returned by device search.</summary>
    private static bool TryId(string query, out int id) => int.TryParse(query, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;

    /// <summary>Keeps source ids alongside alert text for later links.</summary>
    private static string AlertRow(Alert a) =>
        $"{a.Identity}, device {a.DeviceId}: {a.Severity}; {a.State}; {Clean(a.DisplayRuleName)} on {Clean(a.DisplayHostname)}";

    /// <summary>One line per entity, with ids kept intact and room reserved for the omitted count.</summary>
    internal static string Rows(string heading, IEnumerable<string> rows)
    {
        var text = new System.Text.StringBuilder(heading);
        var omitted = 0;
        foreach (var row in rows)
        {
            if (omitted == 0 && text.Length + 1 + row.Length <= MaxCharacters - 50)
                text.Append('\n').Append(row);
            else
                omitted++;
        }
        if (omitted > 0) text.Append('\n').Append(omitted).Append(" more results omitted.");
        return text.ToString();
    }

    /// <summary>Bounds network labels and prevents control characters from creating extra rows.</summary>
    private static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        var clean = new string(value.Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();
        return clean.Length <= 80 ? clean : clean[..79] + "…";
    }
}
