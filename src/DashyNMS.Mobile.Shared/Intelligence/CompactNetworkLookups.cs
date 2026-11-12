using System.Globalization;
using System.Text.RegularExpressions;
using DashyNMS.Agent.Devices;
using DashyNMS.Agent.Summaries;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Intelligence;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>Creates read-only lookups scoped to the current connection, for a model session.</summary>
public interface IIntelligenceLookups
{
    /// <summary>Creates a fresh set for one model session and the current connection.</summary>
    IReadOnlyList<IntelligenceLookup> Create();

    /// <summary>
    /// As <see cref="Create()"/>, and also reports the typed evidence behind each
    /// reply the model is given. Answer chips are verified against these, never
    /// against the model's own spelling of a name or id (#228).
    /// </summary>
    IReadOnlyList<IntelligenceLookup> Create(Action<IReadOnlyList<IntelligenceEvidence>> evidence) => Create();
}

/// <summary>
/// Keeps network replies within the measured on-device budget. Each created set
/// belongs to one connection; after a switch it refuses calls and discards late replies.
/// Rows lead with the LibreNMS name in the person's chosen style and carry Core's
/// inline marker beside it, so the model names devices rather than writing "device 708",
/// while the stable id stays available for links.
/// </summary>
/// <remarks>
/// <c>find_device</c> runs DashyNMS/mcp's shared device search (#596), so its ranking,
/// filtering and redaction are the MCP agent's rather than a mobile copy; this class
/// only adapts the typed result to the compact profile and its evidence. The other
/// lookups remain mobile queries until their own shared operations exist.
/// </remarks>
/// <param name="deviceTransport">
/// Opens a transport for one captured connection, for <c>find_device</c> alone (see
/// <see cref="ConnectionBoundDeviceList"/>). The app passes Core's HTTP transport; tests
/// pass a synthetic one. Without it <c>find_device</c> fails closed rather than falling
/// back to the session's shared client, whose connection can change under a request.
/// </param>
public sealed partial class CompactNetworkLookups(
    ILibreNmsClient client, ISettingsStore settings, ISessionService session, TimeProvider? timeProvider = null,
    Func<LibreNmsConnection, bool, ILibreNmsTransport>? deviceTransport = null) : IIntelligenceLookups
{
    public const int MaxCharacters = 900;
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    internal const string Failed = "The lookup failed. LibreNMS could not be reached.";
    internal const string Changed = "The connection changed. Start a new chat.";
    internal const string Refused = "The lookup failed. LibreNMS refused this account access to the data.";
    internal const string MacUnavailable = "Search by device name or IP address. MAC address search isn't available here yet.";

    /// <summary>
    /// Rows asked of the shared search. Ten or so fit in <see cref="MaxCharacters"/>;
    /// the rest are counted from its exact total rather than fetched.
    /// </summary>
    internal const int FindLimit = 20;

    /// <summary>True for a reply that reports a failed lookup rather than data or a refusal to run.</summary>
    internal static bool IsFailure(string reply) => reply is Failed or Changed or Refused;

    /// <summary>One displayed row and the evidence it introduces; omitted rows contribute none.</summary>
    internal readonly record struct Row(string Text, IReadOnlyList<IntelligenceEvidence> References)
    {
        public Row(string text) : this(text, []) { }
    }

    /// <summary>The model's text and the evidence behind exactly that text.</summary>
    internal sealed record Reply(string Text, IReadOnlyList<IntelligenceEvidence> References)
    {
        public static Reply Say(string text) => new(text, []);
    }

    public IReadOnlyList<IntelligenceLookup> Create() => Create(static _ => { });

    /// <summary>Captures the connection so replies and their evidence cannot cross between networks.</summary>
    public IReadOnlyList<IntelligenceLookup> Create(Action<IReadOnlyList<IntelligenceEvidence>> evidence)
    {
        var connection = session.Connection;
        var style = settings.Current.DeviceNameStyle;
        var time = timeProvider ?? TimeProvider.System;
        bool IsCurrent() => connection is not null && ReferenceEquals(connection, session.Connection);

        // One shared search, with its own caches and its own transport, per lookup set
        // and so per captured connection. The transport is pinned to that connection
        // object, so a switch can't redirect a search's request to another server.
        // Retiring cancels in-flight waits, refuses further calls (cached ones included)
        // and closes the transport.
        var bound = connection is null || deviceTransport is null ? null
            : new ConnectionBoundDeviceList(connection, client.Failover?.IsOnBackup ?? false, deviceTransport);
        var search = bound is null ? null : HostDeviceSearch.Create(bound, time, settings.Current.ServerTimestampsAreUtc);
        void RetireSearch()
        {
            search?.Dispose();
            bound?.Dispose();
        }
        string Retire()
        {
            RetireSearch();
            return Changed;
        }

        IntelligenceLookup Lookup(string name, string purpose, Func<string, DateTimeOffset, CancellationToken, Task<Reply>> run) =>
            new(name, purpose, async (query, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (connection is null && session.Connection is null)
                    return "Sign in to LibreNMS to use network lookups.";
                if (!IsCurrent()) return Retire();
                using var deadline = new CancellationTokenSource(Timeout, time);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
                try
                {
                    // WaitAsync also bounds transports which do not honour cancellation.
                    var reply = await run(query, time.GetUtcNow(), timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!IsCurrent()) return Retire();
                    if (reply.References.Count > 0) evidence(reply.References);
                    return reply.Text;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception) { return IsCurrent() ? Failed : Retire(); }
            });

        // Names are sourced from LibreNMS only. A device with no usable name says so
        // rather than letting the model invent one.
        string DeviceName(Device? device, string? hostname, int deviceId)
        {
            var name = style.Resolve(device, hostname);
            return string.IsNullOrWhiteSpace(name) || name == "unknown device"
                ? $"Device {deviceId.ToString(CultureInfo.InvariantCulture)} (name unavailable)" : Clean(name);
        }

        IntelligenceEvidence DeviceReference(Device device, string name, DateTimeOffset at)
        {
            var state = device.ReportedState;
            return new(new("device:" + Id(device.DeviceId), name, state.ToDisplayString(), StateTone(state), at, "Device status at lookup time")
            { Object = new("device", device.DeviceId) });
        }

        Row DeviceRow(Device d, DateTimeOffset at)
        {
            var name = DeviceName(d, d.Hostname, d.DeviceId);
            return new($"{name} [[device:{Id(d.DeviceId)}]]: {d.ReportedState.ToDisplayString()}; IP {Clean(d.Ip)}", [DeviceReference(d, name, at)]);
        }

        // A shared search row, already redacted upstream, in the person's naming style.
        // LibreNMS fills display with the hostname when none is set; the shared summary
        // drops that repeat, so it is restored before the style chooses a name.
        // The state comes from the raw record's reported state, not the summary's word:
        // the summary has only up, down and disabled, and says "down" for a device whose
        // state LibreNMS didn't report. A row without its raw record says Unknown.
        Row SearchRow(DeviceSummary d, DateTimeOffset at, IReadOnlyDictionary<int, Device> raw)
        {
            var named = new Device { DeviceId = d.DeviceId, Hostname = d.Hostname, SysName = d.SysName, Display = d.Display ?? d.Hostname, Ip = d.Ip };
            var name = DeviceName(named, d.Hostname, d.DeviceId);
            var state = raw.TryGetValue(d.DeviceId, out var device) ? device.ReportedState : DeviceState.Unknown;
            var (status, tone) = (state.ToDisplayString(), StateTone(state));
            // Up to a minute old: the shared search caches the device list per connection.
            IntelligenceEvidence reference = new(new("device:" + Id(d.DeviceId), name, status, tone, at, "Device status from the device list, up to a minute old")
            { Object = new("device", d.DeviceId) });
            return new($"{name} [[device:{Id(d.DeviceId)}]]: {status}; IP {Clean(d.Ip)}", [reference]);
        }

        // The device list supplies names for every alert at once: one request per
        // lookup, never one per alert. Alerts keep their source-qualified identity.
        Row AlertRow(Alert a, IReadOnlyDictionary<int, Device>? devices, DateTimeOffset at)
        {
            var device = devices is not null && devices.TryGetValue(a.DeviceId, out var found) ? found : null;
            var deviceName = DeviceName(device, a.Hostname, a.DeviceId);
            var (severity, tone) = Severity(a.Severity);
            var rule = Clean(a.DisplayRuleName);
            var references = new List<IntelligenceEvidence>
            {
                new(new("alert:" + Id(a.Id), rule, severity, tone, at, deviceName + " · " + a.State)
                {
                    Object = new(a.Source == AlertSource.Fault ? "fault" : "alert", a.Id,
                        a.DeviceId > 0 ? a.DeviceId : null, a.RuleId > 0 ? a.RuleId : null),
                }),
            };
            var on = deviceName;
            if (device is not null && device.DeviceId > 0)
            {
                references.Add(DeviceReference(device, deviceName, at));
                on += $" [[device:{Id(device.DeviceId)}]]";
            }
            return new($"{severity}; {a.State}; {rule} [[alert:{Id(a.Id)}]] ({a.Identity}) on {on}", references);
        }

        async Task<IReadOnlyDictionary<int, Device>?> DevicesById(CancellationToken ct)
        {
            try
            {
                var devices = await client.Devices.ListAsync(ct).ConfigureAwait(false);
                return ById(devices);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            // Alerts still answer with LibreNMS's own hostname when names can't be read.
            catch (Exception) { return null; }
        }

        return
        [
            Lookup("network_overview", "Network counts and the open alerts. Query is unused.", async (_, at, ct) =>
            {
                var devices = client.Devices.ListAsync(ct);
                var alerts = client.Alerts.ListAsync(AlertQuery.Open, ct);
                await Task.WhenAll(devices, alerts).ConfigureAwait(false);
                var ds = await devices.ConfigureAwait(false);
                var als = await alerts.ConfigureAwait(false);
                var byId = ById(ds);
                return Rows($"Devices: {ds.Count} total, {ds.Count(d => d.ReportedState == DeviceState.Up)} up, {ds.Count(d => d.ReportedState == DeviceState.Down)} down. Open alerts: {als.Count}, {als.Count(a => a.Severity == AlertSeverity.Critical)} critical, {als.Count(a => a.Severity == AlertSeverity.Warning)} warning, {als.Count(a => a.State == AlertState.Acknowledged)} acknowledged.",
                    Ordered(als).Select(a => AlertRow(a, byId, at))
                        .Concat(ds.Where(d => d.ReportedState == DeviceState.Down).OrderBy(d => d.DeviceId).Select(d => DeviceRow(d, at))));
            }),
            Lookup("find_device", "Find devices by name or IP. Query is the search text.", async (query, at, ct) =>
            {
                if (string.IsNullOrWhiteSpace(query)) return Reply.Say("Enter a device name or IP address.");
                // The reviewed profile is text search; MAC search reads ARP tables and isn't offered yet.
                if (MacAddress().IsMatch(query.Trim())) return Reply.Say(MacUnavailable);
                if (search is null || bound is null) return Reply.Say(Failed);
                // Sign-out or a switch mid-search stops the wait at once rather than at the deadline.
                EventHandler retire = (_, _) => { if (!IsCurrent()) RetireSearch(); };
                session.StateChanged += retire;
                try
                {
                    var outcome = await search.FindDevicesAsync(new FindDevicesRequest(query, Limit: FindLimit), ct).ConfigureAwait(false);
                    if (outcome.Result is not { } found)
                        return Reply.Say(outcome.Error?.Code switch
                        {
                            DeviceSearchFailure.Refused => Refused,
                            DeviceSearchFailure.InvalidRequest => "Search by part of a device name or an IP address.",
                            _ => Failed,
                        });
                    // The list behind this result: the search fetched it, or its cache holds it.
                    var raw = bound.Latest;
                    // The heading carries the exact total visible to this account, so rows that
                    // weren't fetched are counted as omitted, never silently dropped.
                    return Rows($"Matching devices: {Id(found.Page.Total)}.", found.Matches.Select(m => SearchRow(m.Device, at, raw)),
                        alsoOmitted: Math.Max(0, found.Page.Total - found.Page.Offset - found.Page.Returned));
                }
                finally
                {
                    session.StateChanged -= retire;
                }
            }),
            Lookup("device_summary", "Device state and hardware. Query is a numeric device id.", async (query, at, ct) =>
            {
                if (!TryId(query, out var id)) return Reply.Say("Supply a numeric device id from find_device.");
                var device = await client.Devices.GetAsync(Id(id), ct).ConfigureAwait(false);
                if (device is null) return Reply.Say("Device not found.");
                var row = DeviceRow(device, at);
                var location = Clean(device.Location);
                var references = new List<IntelligenceEvidence>();
                if (device.LocationId is { } locationId && locationId > 0 && !string.IsNullOrWhiteSpace(device.Location))
                {
                    // The chip shows the cleaned label; Devices filters by LibreNMS's exact name.
                    references.Add(new(new("location:" + Id(locationId), location, "", "unknown", at, "Location")
                    { Object = new("location", locationId) }, device.Location));
                    location += $" [[location:{Id(locationId)}]]";
                }
                return Rows(row.Text,
                    [new($"OS: {Clean(device.Os)}; hardware: {Clean(device.Hardware)}; location: {location}; uptime: {device.Uptime} seconds.", references)],
                    row.References);
            }),
            Lookup("active_alerts", "Open alerts, unacknowledged first. Query is a device id or empty for all.", async (query, at, ct) =>
            {
                if (!string.IsNullOrWhiteSpace(query) && !TryId(query, out _)) return Reply.Say("Supply a numeric device id or an empty query.");
                var devices = DevicesById(ct);
                var alerts = await client.Alerts.ListAsync(AlertQuery.Open, ct).ConfigureAwait(false);
                var byId = await devices.ConfigureAwait(false);
                var found = Ordered(alerts.Where(a => string.IsNullOrWhiteSpace(query) || a.DeviceId == int.Parse(query, CultureInfo.InvariantCulture))).ToList();
                return Rows($"Open alerts: {found.Count}.", found.Select(a => AlertRow(a, byId, at)));
            }),
            Lookup("device_sensors", "Sensor readings. Query is a numeric device id.", async (query, at, ct) =>
            {
                if (!TryId(query, out var id)) return Reply.Say("Supply a numeric device id from find_device.");
                var deviceTask = DeviceOrNull(id, ct);
                var sensors = await client.Sensors.ListAsync(ct).ConfigureAwait(false);
                var device = await deviceTask.ConfigureAwait(false);
                var deviceName = DeviceName(device, null, id);
                var heading = $"Sensors on {deviceName}";
                IReadOnlyList<IntelligenceEvidence> headingReferences = [];
                if (device is not null && device.DeviceId == id)
                {
                    heading += $" [[device:{Id(id)}]]";
                    headingReferences = [DeviceReference(device, deviceName, at)];
                }
                var found = sensors.Where(s => s.DeviceId == id).OrderBy(s => s.SensorId).ToList();
                // Readings are raw values, not a health assessment, so a sensor chip carries no status.
                return Rows($"{heading}: {found.Count}.", found.Select(s =>
                {
                    var description = Clean(s.Description);
                    var reading = s.HasReading ? s.Current!.Value.ToString(CultureInfo.InvariantCulture) : "unavailable";
                    return new Row($"{description} [[sensor:{Id(s.SensorId)}]]: {Clean(s.SensorClass)}; reading {reading}; unit {Clean(s.Unit)}",
                        [new(new("sensor:" + Id(s.SensorId), description, "", "unknown", at, $"{Clean(s.SensorClass)} reading on {deviceName}")
                        { Object = new("sensor", s.SensorId, id) })]);
                }), headingReferences);
            }),
        ];

        async Task<Device?> DeviceOrNull(int id, CancellationToken ct)
        {
            try { return await client.Devices.GetAsync(Id(id), ct).ConfigureAwait(false); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            // Readings still answer if the name can't be read; the heading says so.
            catch (Exception) { return null; }
        }
    }

    private static IEnumerable<Alert> Ordered(IEnumerable<Alert> alerts) => alerts
        .OrderBy(a => a.State == AlertState.Acknowledged ? 1 : 0)
        .ThenBy(a => a.Severity == AlertSeverity.Critical ? 0 : a.Severity == AlertSeverity.Warning ? 1 : 2)
        .ThenBy(a => a.Id);

    private static IReadOnlyDictionary<int, Device> ById(IEnumerable<Device> devices)
    {
        var byId = new Dictionary<int, Device>();
        foreach (var device in devices) byId.TryAdd(device.DeviceId, device);
        return byId;
    }

    /// <summary>The evidence chip's tone: only a reported up or down is coloured as one.</summary>
    private static string StateTone(DeviceState state) => state switch
    {
        DeviceState.Up => "up",
        DeviceState.Down => "down",
        _ => "unknown",
    };

    /// <summary>
    /// The shared search's MAC forms (colon or hyphen pairs, Cisco dotted, bare hex), refused
    /// before any request so MAC lookups can't reach ARP until that profile is reviewed.
    /// </summary>
    [GeneratedRegex(@"^(?:[0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}$|^(?:[0-9A-Fa-f]{4}\.){2}[0-9A-Fa-f]{4}$|^[0-9A-Fa-f]{12}$")]
    private static partial Regex MacAddress();

    private static (string Label, string Tone) Severity(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => ("Critical", "critical"),
        AlertSeverity.Warning => ("Warning", "warning"),
        AlertSeverity.Ok => ("OK", "up"),
        _ => ("Unknown", "unknown"),
    };

    private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);

    /// <summary>Accepts only positive ids returned by device search.</summary>
    private static bool TryId(string query, out int id) => int.TryParse(query, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;

    /// <summary>
    /// One line per entity, with ids kept intact and room reserved for the omitted count.
    /// Only references from rows that fit are reported, so a chip can only point at
    /// something the model was actually shown.
    /// </summary>
    /// <param name="alsoOmitted">Rows known to exist but not supplied, such as beyond a search's page.</param>
    internal static Reply Rows(string heading, IEnumerable<Row> rows, IReadOnlyList<IntelligenceEvidence>? headingReferences = null, int alsoOmitted = 0)
    {
        var text = new System.Text.StringBuilder(heading);
        var references = new List<IntelligenceEvidence>(headingReferences ?? []);
        var omitted = 0;
        foreach (var row in rows)
        {
            if (omitted == 0 && text.Length + 1 + row.Text.Length <= MaxCharacters - 50)
            {
                text.Append('\n').Append(row.Text);
                references.AddRange(row.References);
            }
            else
                omitted++;
        }
        omitted += alsoOmitted;
        if (omitted > 0) text.Append('\n').Append(omitted).Append(" more results omitted.");
        // Several alerts on one device repeat its reference; one is enough.
        return new(text.ToString(), references.DistinctBy(r => r.Reference.Key).ToList());
    }

    /// <summary>Bounds network labels and prevents control characters from creating extra rows or markers.</summary>
    internal static string Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        // A label must never close or open an inline marker of its own.
        var clean = new string(value.Select(c => char.IsControl(c) ? ' ' : c).ToArray());
        while (clean.Contains("[[", StringComparison.Ordinal) || clean.Contains("]]", StringComparison.Ordinal))
            clean = clean.Replace("[[", "[ [", StringComparison.Ordinal).Replace("]]", "] ]", StringComparison.Ordinal);
        clean = clean.Trim();
        return clean.Length <= 80 ? clean : clean[..79] + "…";
    }
}
