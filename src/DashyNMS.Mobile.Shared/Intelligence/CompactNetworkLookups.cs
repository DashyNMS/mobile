using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using DashyNMS.Agent.Alerts;
using DashyNMS.Agent.Devices;
using DashyNMS.Agent.Overview;
using DashyNMS.Agent.Health;
using DashyNMS.Agent.Summaries;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Intelligence;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Sensors;
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

    /// <summary>
    /// As <see cref="Create(Action{IReadOnlyList{IntelligenceEvidence}})"/>, owned by the
    /// caller: disposing the set retires its lookups and closes anything they opened (the
    /// device search's transport). A conversation opens one per model attempt and disposes
    /// it however the attempt ends, so a chat never leaves a connection behind.
    /// </summary>
    IntelligenceLookupSet Open(Action<IReadOnlyList<IntelligenceEvidence>> evidence) => new(Create(evidence));
}

/// <summary>
/// One model attempt's lookups and whatever they hold open. Disposal is idempotent and
/// safe while a lookup is still running: that lookup then fails rather than reading.
/// </summary>
public sealed class IntelligenceLookupSet(IReadOnlyList<IntelligenceLookup> lookups, Action? retire = null) : IDisposable
{
    private Action? retire = retire;

    public IReadOnlyList<IntelligenceLookup> Lookups { get; } = lookups;

    public void Dispose() => Interlocked.Exchange(ref retire, null)?.Invoke();
}

/// <summary>
/// Keeps network replies within the measured on-device budget. Each created set
/// belongs to one connection; after a switch it refuses calls and discards late replies.
/// Rows lead with the LibreNMS name in the person's chosen style and carry Core's
/// inline marker beside it, so the model names devices rather than writing "device 708",
/// while the stable id stays available for links.
/// </summary>
/// <remarks>
/// <c>find_device</c> runs DashyNMS/mcp's shared device search (#596), <c>active_alerts</c>
/// its shared open alerts (#601), <c>network_overview</c> its shared overview (#600) and
/// <c>device_summary</c> its shared device summary (#602) and <c>device_sensors</c> its shared
/// sensor assessment (#603). Their reading, ranking, grouping, ordering, judging, filtering and
/// redaction are the MCP agent's rather than a mobile copy; this class only adapts the typed
/// results to the compact profile and its evidence.
/// </remarks>
/// <param name="deviceTransport">
/// Opens a transport for one captured connection and its session generation, for the shared
/// operations (<c>find_device</c>, <c>active_alerts</c>, <c>network_overview</c>,
/// <c>device_summary</c> and <c>device_sensors</c>) alone (see
/// <see cref="ConnectionBoundClient"/>); the set owns and closes it. The app passes
/// <see cref="IntelligenceDeviceTransports.Open"/>; tests pass a synthetic one. Without it
/// those lookups fail closed rather than falling back to the session's shared client,
/// whose connection can change under a request.
/// </param>
/// <param name="sensorDefinitions">
/// The installed sensor definitions, read-only (<see cref="Services.SensorDefinitions.ReadOnlyStore"/>),
/// so a state sensor reads as it does on the Health page. Read-only as desktop gives its MCP
/// server: a question never downloads a catalogue. Without it state sensors stay unassessed.
/// </param>
public sealed partial class CompactNetworkLookups(
    ILibreNmsClient client, ISettingsStore settings, ISessionService session, TimeProvider? timeProvider = null,
    Func<SessionScope, ILibreNmsTransport>? deviceTransport = null, SensorDefinitionStore? sensorDefinitions = null) : IIntelligenceLookups
{
    public const int MaxCharacters = 900;
    internal static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    internal const string Failed = "The lookup failed. LibreNMS could not be reached.";
    internal const string Changed = "The connection changed. Start a new chat.";
    internal const string Refused = "The lookup failed. LibreNMS refused this account access to the data.";
    internal const string InvalidSearch = "Supply a name, IP or MAC, or a status (up, down, disabled), group or location. Use an unambiguous group or location name.";

    /// <summary>
    /// Rows asked of the shared search. Ten or so fit in <see cref="MaxCharacters"/>;
    /// the rest are counted from its exact total rather than fetched.
    /// </summary>
    internal const int FindLimit = 20;

    /// <summary>
    /// Alert groups asked of the shared operation. Six or so fit in <see cref="MaxCharacters"/>;
    /// the rest are counted from its totals rather than fetched.
    /// </summary>
    internal const int AlertLimit = 20;

    internal const string UnknownDevice = "No device with that id is visible to this account. Use find_device for its id.";

    /// <summary>Faults named on a group's row; the rest are counted.</summary>
    internal const int NamedFaults = 3;

    /// <summary>
    /// The reviewed contract behind each lookup name: the allowlisted shared tool it is a profile
    /// of (Core's <c>IntelligenceToolAllowlist</c>). <c>device_summary</c> has no tool of its own
    /// name upstream: it is a reviewed narrower profile of <c>device_health</c>, its device and
    /// uptime only, built on the shared summary that <c>device_health</c> uses for that part (#602).
    /// A new lookup name needs an entry here, so no name reaches the model without a reviewed tool.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string> ReviewedTools = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["network_overview"] = "network_overview",
        ["find_device"] = "find_device",
        ["device_summary"] = "device_health",
        ["active_alerts"] = "active_alerts",
        ["device_sensors"] = "device_sensors",
    };

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

    /// <summary>
    /// An unowned set, for callers that only need the lookups. Its device search transport
    /// is closed when the set retires on a connection switch; anything that keeps sets
    /// beyond one use (the conversation) calls <see cref="Open"/> and disposes it instead.
    /// </summary>
    public IReadOnlyList<IntelligenceLookup> Create(Action<IReadOnlyList<IntelligenceEvidence>> evidence) => Open(evidence).Lookups;

    /// <summary>Captures the connection so replies and their evidence cannot cross between networks.</summary>
    public IntelligenceLookupSet Open(Action<IReadOnlyList<IntelligenceEvidence>> evidence)
    {
        // By session generation, not the connection's reference: trusting a certificate or
        // changing the timeout gives the session a new connection object for the same server,
        // and must not end a chat (see SessionScope). Sign-in, a switch or sign-out does.
        var scope = SessionScope.Capture(session);
        var style = settings.Current.DeviceNameStyle;
        var time = timeProvider ?? TimeProvider.System;
        bool IsCurrent() => scope is { } captured && captured.IsCurrent(session);

        // One shared search, open-alerts and device summary operation, each with its own caches
        // (the summary has none), over one transport per lookup set and so per captured connection. The transport is
        // pinned to that connection, so a switch can't redirect a request to another server.
        // Retiring cancels in-flight waits, refuses further calls (cached ones included)
        // and closes the transport. It happens on a connection switch and when the owner
        // disposes the set, whichever comes first.
        var bound = scope is not { } open || deviceTransport is null ? null
            : new ConnectionBoundClient(open, deviceTransport);
        var search = bound is null ? null : HostDeviceSearch.Create(bound, time, settings.Current.ServerTimestampsAreUtc);
        var openAlerts = bound is null ? null : HostOpenAlerts.Create(bound, time, settings.Current.ServerTimestampsAreUtc);
        var overviewBound = scope is not { } capturedOverview || deviceTransport is null ? null
            : new ConnectionBoundClient(capturedOverview, deviceTransport, overview: true);
        var overview = overviewBound is null ? null : HostNetworkOverview.Create(overviewBound, time, settings.Current.ServerTimestampsAreUtc);
        var summaries = bound is null ? null : HostDeviceSummary.Create(bound, settings.Current.ServerTimestampsAreUtc);
        // Sensors get their own profile too: the sensor list and health routes, never ARP,
        // groups or alerts.
        var sensorBound = scope is not { } capturedSensors || deviceTransport is null ? null
            : new ConnectionBoundClient(capturedSensors, deviceTransport, sensors: true);
        var deviceSensors = sensorBound is null ? null
            : HostDeviceSensors.Create(sensorBound, sensorBound.SensorTransport, time, settings.Current.ServerTimestampsAreUtc, sensorDefinitions);
        void RetireShared()
        {
            search?.Dispose();
            openAlerts?.Dispose();
            deviceSensors?.Dispose();
            summaries?.Dispose();
            bound?.Dispose();
            overview?.Dispose();
            overviewBound?.Dispose();
            sensorBound?.Dispose();
        }
        string Retire()
        {
            RetireShared();
            return Changed;
        }

        // Sign-out or a switch mid-call stops a shared operation's wait at once rather than
        // at the deadline.
        async Task<T> WhileCurrent<T>(Func<Task<T>> call)
        {
            EventHandler retire = (_, _) => { if (!IsCurrent()) RetireShared(); };
            session.StateChanged += retire;
            try
            {
                return await call().ConfigureAwait(false);
            }
            finally
            {
                session.StateChanged -= retire;
            }
        }

        IntelligenceLookup Lookup(string name, string purpose, Func<string, DateTimeOffset, CancellationToken, Task<Reply>> run) =>
            new(name, purpose, async (query, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (scope is null && session.Connection is null)
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

        // A shared device summary, already redacted upstream, named in the person's style.
        // LibreNMS fills display with the hostname when none is set; the shared summary
        // drops that repeat, so it is restored before the style chooses a name.
        string SummaryName(DeviceSummary d) =>
            DeviceName(new Device { DeviceId = d.DeviceId, Hostname = d.Hostname, SysName = d.SysName, Display = d.Display ?? d.Hostname, Ip = d.Ip }, d.Hostname, d.DeviceId);

        // A shared search row in the person's naming style.
        // The state is the summary's typed State (Core's ReportedState), not its status word:
        // the word has only up, down and disabled, says "down" when LibreNMS left a flag out,
        // and doesn't tell ignored devices apart.
        Row SearchRow(DeviceSummary d, DateTimeOffset at)
        {
            var name = SummaryName(d);
            var (status, tone) = (d.State.ToDisplayString(), StateTone(d.State));
            // Up to a minute old: the shared search caches the device list per connection.
            IntelligenceEvidence reference = new(new("device:" + Id(d.DeviceId), name, status, tone, at, "Device status from the device list, up to a minute old")
            { Object = new("device", d.DeviceId) });
            return new($"{name} [[device:{Id(d.DeviceId)}]]: {status}; IP {Clean(d.Ip)}", [reference]);
        }

        // The shared device summary (#602), already redacted upstream: the device in the person's
        // naming style with its reported state, then OS, hardware, location and uptime. The read is
        // devices/{id}, so the state is as of this lookup, not the cached device list's. The location
        // chip shows the cleaned name and keeps LibreNMS's exact name only as its Devices filter.
        Reply SummaryRows(DeviceSummaryResult found, DateTimeOffset at)
        {
            var d = found.Device;
            var name = SummaryName(d);
            var (status, tone) = (d.State.ToDisplayString(), StateTone(d.State));
            IntelligenceEvidence device = new(new("device:" + Id(d.DeviceId), name, status, tone, at, "Device status at lookup time")
            { Object = new("device", d.DeviceId) });
            var location = Clean(d.Location);
            var references = new List<IntelligenceEvidence>();
            if (found.Location is { } place)
            {
                location = Clean(place.Name);
                references.Add(new(new("location:" + Id(place.Id), location, "", "unknown", at, "Location")
                { Object = new("location", place.Id) }, place.FilterName));
                location += $" [[location:{Id(place.Id)}]]";
            }
            var uptime = found.UptimeSeconds is { } seconds ? seconds.ToString(CultureInfo.InvariantCulture) + " seconds" : "unknown";
            return Rows($"{name} [[device:{Id(d.DeviceId)}]]: {status}; IP {Clean(d.Ip)}",
                [new($"OS: {Clean(d.Os)}; hardware: {Clean(d.Hardware)}; location: {location}; uptime: {uptime}.", references)],
                [device]);
        }

        // A shared alert group, already redacted upstream: one row per rule firing on a device.
        // The marker is the representative fault's; its source travels in the evidence's typed
        // object, so navigation retains the fault or legacy alert source. The group key
        // (device and rule) is never offered as an id; the evidence carries the rule instead.
        Row GroupRow(OpenAlertGroup g, DateTimeOffset at)
        {
            var d = g.Device;
            var named = d is null ? null
                : new Device { DeviceId = d.DeviceId, Hostname = d.Hostname, SysName = d.SysName, Display = d.Display ?? d.Hostname, Ip = d.Ip };
            var deviceName = DeviceName(named, g.Hostname, g.DeviceId);
            var (severity, tone) = Severity(g.Severity);
            var rule = Clean(g.Rule);
            var state = g.State switch
            {
                OpenAlertState.PartlyAcknowledged => $"partly acknowledged ({Id(g.AcknowledgedCount)} of {Id(g.FaultCount)})",
                OpenAlertState.Acknowledged => "acknowledged",
                _ => "active",
            };
            var representative = g.Representative;
            var references = new List<IntelligenceEvidence>
            {
                new(new("alert:" + Id(representative.Id), rule, severity, tone, at, deviceName + " · " + state)
                {
                    Object = new(representative.Source == AlertSource.Fault ? "fault" : "alert", representative.Id,
                        g.DeviceId > 0 ? g.DeviceId : null, g.RuleId > 0 ? g.RuleId : null),
                }),
            };
            var on = deviceName;
            if (d is not null && d.DeviceId > 0)
            {
                // Up to a minute old: the shared operation caches the device list per connection.
                references.Add(new(new("device:" + Id(d.DeviceId), deviceName, d.State.ToDisplayString(), StateTone(d.State), at, "Device status from the device list, up to a minute old")
                { Object = new("device", d.DeviceId) }));
                on += $" [[device:{Id(d.DeviceId)}]]";
            }
            var text = $"{severity}; {state}; {rule} [[alert:{Id(representative.Id)}]] on {on}";
            if (g.FaultCount > 1)
            {
                var names = g.Faults.Select(f => f.Entity).Where(e => !string.IsNullOrWhiteSpace(e)).Take(NamedFaults).Select(e => Clean(e)).ToList();
                var rest = g.FaultCount - names.Count;
                text += $"; {Id(g.FaultCount)} faults" + (names.Count == 0 ? "" : ": " + string.Join(", ", names) + (rest > 0 ? $" and {Id(rest)} more" : ""));
            }
            else if (!string.IsNullOrWhiteSpace(g.Entity))
                text += "; for " + Clean(g.Entity);
            return new(text, references);
        }

        IReadOnlyList<IntelligenceLookup> lookups =
        [
            Lookup("network_overview", "Network counts and the open alerts. Query is unused.", async (_, at, ct) =>
            {
                if (overview is null) return Reply.Say(Failed);
                var outcome = await WhileCurrent(() => overview.ReadAsync(ct)).ConfigureAwait(false);
                if (outcome.Result is not { } found)
                    return Reply.Say(outcome.Error?.Code == DashyNMS.Agent.HostFailure.Refused ? Refused : Failed);
                var d = found.Devices;
                var heading = $"Visible devices: {Id(d.Total)} total, {Id(d.Up)} up, {Id(d.Down)} down, {Id(d.Disabled)} disabled, {Id(d.Ignored)} ignored. " +
                    (found.Alerts is { } a ? AlertHeading(a) + $" {Id(a.Acknowledged)} acknowledged, {Id(a.PartlyAcknowledged)} partly acknowledged. " : "Alerts unavailable. ") +
                    (found.Ports is { } p ? $"Ports: {Id(p.Total)} total, {Id(p.DownWhileEnabled)} down while enabled, {Id(p.CountingErrors)} counting errors. " : "Ports unavailable. ") +
                    (found.LibreNmsVersion is { } v ? $"LibreNMS: {Clean(v)}." : "Version unavailable.");
                // Reserve failure information before rows so the budget cannot hide a failed source.
                if (found.Unavailable.Count > 0)
                    heading += " Unavailable: " + string.Join(", ", found.Unavailable.Select(u => u.Source)) + ".";
                return Rows(heading, found.AlertGroups.Select(g => GroupRow(g, at))
                    .Concat(found.DownDevices.Select(down => SearchRow(down, at)))
                    .Concat(found.DownLocations.Select(l => new Row($"Location {Clean(l.Location)}: {Id(l.Down)} down."))),
                    alsoOmitted: found.AlertGroupsOmitted + found.DownDevicesOmitted + found.DownLocationsOmitted);
            }),
            Lookup("find_device", "Find devices by name, IP or MAC; filter by status, group or location.", async (query, at, ct) =>
            {
                if (!TrySearch(query, out var request)) return Reply.Say(InvalidSearch);
                if (search is not { } searching) return Reply.Say(Failed);
                var outcome = await WhileCurrent(() => searching.FindDevicesAsync(request, ct)).ConfigureAwait(false);
                if (outcome.Result is not { } found)
                    return Reply.Say(outcome.Error?.Code switch
                    {
                        DeviceSearchFailure.Refused => Refused,
                        DeviceSearchFailure.InvalidRequest => InvalidSearch,
                        _ => Failed,
                    });
                // The heading carries the exact total visible to this account, so rows that
                // weren't fetched are counted as omitted, never silently dropped.
                return Rows(found.IsMacSearch
                    ? $"Devices with stored ARP observations: {Id(found.Page.Total)}. Observations do not prove ownership."
                    : $"Matching devices: {Id(found.Page.Total)}.", found.Matches.Select(m =>
                    {
                        var row = SearchRow(m.Device, at);
                        if (!found.IsMacSearch) return row;
                        var sightings = m.SeenAs.Take(2).Select(a => $"{Clean(a.Ip)} on port {Id(a.PortId)}");
                        return row with { Text = row.Text + "; ARP observed " + string.Join(", ", sightings)
                            + (m.SeenAs.Count > 2 ? $"; {Id(m.SeenAs.Count - 2)} more observations" : "") };
                    }),
                    alsoOmitted: Math.Max(0, found.Page.Total - found.Page.Offset - found.Page.Returned));
            }),
            Lookup("device_summary", "Device state and hardware. Query is a numeric device id.", async (query, at, ct) =>
            {
                if (!TryId(query, out var id)) return Reply.Say("Supply a numeric device id from find_device.");
                if (summaries is not { } reading) return Reply.Say(Failed);
                var outcome = await WhileCurrent(() => reading.ReadAsync(new DeviceSummaryRequest(id), ct)).ConfigureAwait(false);
                if (outcome.Result is not { } found)
                    return Reply.Say(outcome.Error?.Code switch
                    {
                        DashyNMS.Agent.HostFailure.NotFound => "Device not found.",
                        DashyNMS.Agent.HostFailure.Refused => Refused,
                        _ => Failed,
                    });
                return SummaryRows(found, at);
            }),
            Lookup("active_alerts", "Open alerts by device and rule, worst first. Query is a device id or empty for all.", async (query, at, ct) =>
            {
                int? deviceId = null;
                if (!string.IsNullOrWhiteSpace(query))
                {
                    if (!TryId(query, out var id)) return Reply.Say("Supply a numeric device id or an empty query.");
                    deviceId = id;
                }
                if (openAlerts is not { } alerts) return Reply.Say(Failed);
                var outcome = await WhileCurrent(() => alerts.ListAsync(new OpenAlertsRequest(deviceId, Limit: AlertLimit), ct)).ConfigureAwait(false);
                // An unreadable device list or alert list fails the whole lookup: never "no alerts".
                if (outcome.Result is not { } found)
                    return Reply.Say(outcome.Error?.Code == DashyNMS.Agent.HostFailure.Refused ? Refused : Failed);
                // Groups and faults are totals over every match, so groups not fetched or not
                // fitting are counted as omitted, never silently dropped.
                return Rows(AlertHeading(found.Totals), found.Groups.Select(g => GroupRow(g, at)),
                    alsoOmitted: Math.Max(0, found.Page.Total - found.Page.Offset - found.Page.Returned));
            }),
            Lookup("device_sensors", "Sensor readings and their health, problems first. Query is a numeric device id.", async (query, at, ct) =>
            {
                if (!TryId(query, out var id)) return Reply.Say("Supply a numeric device id from find_device.");
                if (deviceSensors is not { } sensors) return Reply.Say(Failed);
                var outcome = await WhileCurrent(() => sensors.ReadAsync(new DeviceSensorsRequest(id), ct)).ConfigureAwait(false);
                // An unreadable device list or sensor source fails the whole lookup: never "no sensors".
                if (outcome.Result is not { } found)
                    return Reply.Say(outcome.Error?.Code switch
                    {
                        DashyNMS.Agent.HostFailure.Refused => Refused,
                        DashyNMS.Agent.HostFailure.InvalidRequest => UnknownDevice,
                        _ => Failed,
                    });
                var d = found.Device;
                var named = new Device { DeviceId = d.DeviceId, Hostname = d.Hostname, SysName = d.SysName, Display = d.Display ?? d.Hostname, Ip = d.Ip };
                var deviceName = DeviceName(named, d.Hostname, d.DeviceId);
                IntelligenceEvidence device = new(new("device:" + Id(d.DeviceId), deviceName, d.State.ToDisplayString(), StateTone(d.State), at, "Device status from the device list, up to a minute old")
                { Object = new("device", d.DeviceId) });
                // Totals are over every reading, so rows that don't fit are counted as omitted.
                return Rows(SensorHeading($"{deviceName} [[device:{Id(d.DeviceId)}]]", found.Totals),
                    found.Readings.Select(r => SensorRow(r, d.DeviceId, deviceName, at)), [device]);
            }),
        ];
        return new IntelligenceLookupSet(lookups, RetireShared);
    }

    /// <summary>"Open alerts: 3 (11 faults) on 2 devices; 1 critical, 2 warning." Groups and faults counted apart, as MCP does.</summary>
    private static string AlertHeading(OpenAlertTotals totals) => totals.AlertGroups == 0
        ? "Open alerts: 0."
        : $"Open alerts: {Id(totals.AlertGroups)} ({Count(totals.Faults, "fault")}) on {Count(totals.Devices, "device")}; {Id(totals.Critical)} critical, {Id(totals.Warning)} warning.";

    /// <summary>
    /// "Sensors on core-sw1 [[device:1]]: 9; 2 critical, 2 warning, 1 OK, 4 unknown." Unknown is
    /// counted on its own and never folded into OK: no reading, nothing to judge by, or a
    /// "nothing plugged in" value.
    /// </summary>
    private static string SensorHeading(string device, SensorTotals totals) => totals.Readings == 0
        ? $"Sensors on {device}: 0."
        : $"Sensors on {device}: {Id(totals.Readings)}; {Id(totals.Critical)} critical, {Id(totals.Warning)} warning, {Id(totals.Ok)} OK, {Id(totals.Unknown)} unknown.";

    /// <summary>
    /// One judged reading, already redacted upstream: "Module 2 [[sensor:2]]: temperature 81 °C;
    /// critical (high critical limit 80)". A missing reading says so and is never written as zero, and a
    /// reading with nothing to judge it by says that rather than any health word, so the model
    /// can't call it healthy. The chip carries the same state.
    /// </summary>
    private static Row SensorRow(SensorReading r, int deviceId, string deviceName, DateTimeOffset at)
    {
        var description = string.IsNullOrWhiteSpace(r.Description) ? $"Sensor {Id(r.SensorId)}" : Clean(r.Description);
        var type = Clean(r.Type);
        var value = r.StateLabel is { Length: > 0 } label ? Clean(label)
            : r.Value is { } v ? Number(v) + (r.Unit is { Length: > 0 } unit ? " " + Clean(unit) : "")
            : "no reading";
        var (status, tone) = SensorState(r);
        var state = r.State switch
        {
            "critical" or "warning" => status.ToLowerInvariant() + Limits(r),
            "ok" => "OK",
            _ => r.Assessment is SensorAssessment.NoLimits ? "not assessed: no limits" : "state unknown",
        };
        return new($"{description} [[sensor:{Id(r.SensorId)}]]: {type} {value}; {state}",
            [new(new("sensor:" + Id(r.SensorId), description, status, tone, at, $"{type} reading on {deviceName}")
            { Object = new("sensor", r.SensorId, deviceId) })]);
    }

    /// <summary>The chip's status and tone: only a judged reading is coloured, and unknown is never OK.</summary>
    private static (string Label, string Tone) SensorState(SensorReading r) => r.State switch
    {
        "critical" => ("Critical", "critical"),
        "warning" => ("Warning", "warning"),
        "ok" => ("OK", "up"),
        _ => r.Assessment is SensorAssessment.NoLimits ? ("Not assessed", "unknown") : ("Unknown", "unknown"),
    };

    /// <summary>The sensor's own critical limits beside a problem, so the model can say what was crossed.</summary>
    private static string Limits(SensorReading r)
    {
        var parts = new List<string>(2);
        if (r.LowCritical is { } low) parts.Add("low critical limit " + Number(low));
        if (r.HighCritical is { } high) parts.Add("high critical limit " + Number(high));
        return parts.Count == 0 ? "" : " (" + string.Join(", ", parts) + ")";
    }

    /// <summary>
    /// Upstream rounds readings to two places, but a double can still print as 3.2200000000000002;
    /// two places at most keeps the row short and the number as the Health page shows it.
    /// </summary>
    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Count(int count, string noun) => $"{Id(count)} {noun}{(count == 1 ? "" : "s")}";

    /// <summary>The evidence chip's tone: only a reported up or down is coloured as one.</summary>
    private static string StateTone(DeviceState state) => state switch
    {
        DeviceState.Up => "up",
        DeviceState.Down => "down",
        _ => "unknown",
    };

    /// <summary>
    /// Named arguments cross the native string bridge as JSON. Plain text remains a query.
    /// Exact filter names are passed untouched, never through display-label cleaning.
    /// </summary>
    internal static bool TrySearch(string input, out FindDevicesRequest request)
    {
        request = new(Limit: FindLimit);
        if (input.Length > 4096) return false;
        if (!input.TrimStart().StartsWith('{'))
        {
            request = new(input, Limit: FindLimit);
            return !string.IsNullOrWhiteSpace(input);
        }
        try
        {
            using var json = JsonDocument.Parse(input);
            var values = new Dictionary<string, string?>();
            foreach (var property in json.RootElement.EnumerateObject())
            {
                if (property.Name is not ("query" or "status" or "group" or "location")
                    || property.Value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null)
                    || !values.TryAdd(property.Name, property.Value.GetString())) return false;
            }
            request = new(values.GetValueOrDefault("query"), values.GetValueOrDefault("status"),
                values.GetValueOrDefault("group"), values.GetValueOrDefault("location"), Limit: FindLimit);
            return values.Values.Any(v => !string.IsNullOrWhiteSpace(v));
        }
        catch (JsonException) { return false; }
    }

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
        return new(text.ToString(), Distinct(references));
    }

    /// <summary>
    /// One reference per object. Several alerts on one device repeat its reference; one is
    /// enough. Two different objects behind one marker key (a fault and a legacy alert both
    /// numbered 12, which share <c>[[alert:12]]</c>) are both kept, so the conversation sees the
    /// conflict: <see cref="IntelligenceTurn.AddEvidence"/> then withdraws that key for the rest
    /// of the answer, including a chip an earlier lookup supplied, rather than letting either
    /// object claim the other's marker.
    /// </summary>
    internal static IReadOnlyList<IntelligenceEvidence> Distinct(IEnumerable<IntelligenceEvidence> references) => references
        .DistinctBy(r => (r.Reference.Key, r.Reference.Object?.Kind, r.Reference.Object?.Id))
        .ToList();

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
