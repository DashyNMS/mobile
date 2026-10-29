using System.Text.Json;
using System.Text.Json.Serialization;
using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Alerting;
using DashyNMS.Mobile.Alerts;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Widgets;

/// <summary>One alert as a widget row: enough to draw it and to open it.</summary>
/// <param name="RaisedAt">When it was raised, as Unix seconds (0 if LibreNMS didn't say), so a widget can show its age as time passes.</param>
public sealed record WidgetAlert(
    [property: JsonPropertyName("alertId")] int AlertId,
    [property: JsonPropertyName("deviceId")] int DeviceId,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("rule")] string Rule,
    [property: JsonPropertyName("device")] string Device,
    [property: JsonPropertyName("acknowledged")] bool Acknowledged = false,
    [property: JsonPropertyName("raisedAt")] long RaisedAt = 0,
    [property: JsonPropertyName("faultCount")] int FaultCount = 1,
    [property: JsonPropertyName("key")] string? Key = null,
    [property: JsonPropertyName("groupKey")] string? GroupKey = null);

/// <summary>
/// Every device once, by state: up/down/disabled as LibreNMS polls them, and
/// critical/warning/acknowledged/OK by its worst open alert - the pie chart's
/// slices. Disabled devices are left out of the alert slices: nobody's
/// watching them.
/// </summary>
public sealed record WidgetDeviceCounts(
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("up")] int Up,
    [property: JsonPropertyName("down")] int Down,
    [property: JsonPropertyName("disabled")] int Disabled,
    [property: JsonPropertyName("critical")] int Critical,
    [property: JsonPropertyName("warning")] int Warning,
    [property: JsonPropertyName("acknowledged")] int Acknowledged,
    [property: JsonPropertyName("ok")] int Ok,
    [property: JsonPropertyName("unknown")] int Unknown = 0);

/// <summary>A pinned device as a widget row.</summary>
/// <param name="State">"down", "critical", "warning", "acknowledged", "ok" or "disabled" - what it's coloured by.</param>
/// <param name="Status">What's wrong ("Device down", a rule name), or "Up 41d" when nothing is.</param>
/// <param name="Since">For a down device, when its newest alert was raised (Unix seconds), so the widget can say for how long; else 0.</param>
public sealed record WidgetDevice(
    [property: JsonPropertyName("deviceId")] int DeviceId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("location")] string? Location,
    [property: JsonPropertyName("since")] long Since = 0);

/// <summary>A dashboard sensor as a widget row, already read against the app's thresholds.</summary>
/// <param name="Status">"critical", "warning", "ok" or "unknown".</param>
/// <param name="Position">Where the reading sits between its low and high limits, 0 to 1; null when it has no limits to sit between.</param>
public sealed record WidgetSensor(
    [property: JsonPropertyName("sensorId")] int SensorId,
    [property: JsonPropertyName("deviceId")] int DeviceId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("device")] string Device,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("position")] double? Position);

/// <summary>
/// What the home-screen widgets show, written after every alert check. The
/// widgets draw from this alone - they never talk to LibreNMS, and hold no
/// token - so they keep working, showing their last state, while the app sleeps.
/// </summary>
/// <remarks>
/// On iOS a Swift WidgetKit extension reads it from the shared App Group
/// folder, so the JSON names are a contract with
/// <c>ios-widget/DashyNMSWidget/Snapshot.swift</c>: change both together.
/// Version 2 carries qualified alert targets; older snapshots are ignored
/// rather than opening a number in the wrong namespace.
/// </remarks>
public sealed record WidgetSnapshot
{
    public const int CurrentVersion = 2;

    [JsonPropertyName("version")]
    public int Version { get; init; } = CurrentVersion;

    /// <summary>How many alerts a widget can list - the large size's worth.</summary>
    public const int MaxAlerts = 7;

    /// <summary>How many pinned devices or sensors a widget can list.</summary>
    public const int MaxRows = 8;

    /// <summary>An alert-only refresh retains the last successful fleet enrichment on disk.</summary>
    [JsonIgnore]
    public bool PreserveEnrichment { get; init; }

    public WidgetSnapshot RetainEnrichment(WidgetSnapshot previous) => PreserveEnrichment && SignedIn && previous.SignedIn
        ? this with { Devices = previous.Devices, DevicesDown = previous.DevicesDown, Pinned = previous.Pinned, Sensors = previous.Sensors, SensorsReadAt = previous.SensorsReadAt }
        : this;

    [JsonPropertyName("signedIn")]
    public bool SignedIn { get; init; }

    /// <summary>Active (unacknowledged) critical alerts.</summary>
    [JsonPropertyName("critical")]
    public int Critical { get; init; }

    [JsonPropertyName("warning")]
    public int Warning { get; init; }

    [JsonPropertyName("ok")]
    public int Ok { get; init; }

    [JsonPropertyName("usesFaults")]
    public bool UsesFaults { get; init; }

    [JsonPropertyName("faults")]
    public int Faults { get; init; }

    [JsonIgnore]
    public int AlertGroups => Critical + Warning + Ok + Acknowledged;

    [JsonIgnore]
    public string AlertTotalsText => new AlertTotals(Critical, Warning, Ok, Acknowledged, Faults, UsesFaults).Description;

    [JsonPropertyName("acknowledged")]
    public int Acknowledged { get; init; }

    /// <summary>Null until the device list has been read.</summary>
    [JsonPropertyName("devicesDown")]
    public int? DevicesDown { get; init; }

    /// <summary>
    /// The alerts most needing attention - desktop's order, critical first,
    /// newest first - then acknowledged ones, newest first, if there's room.
    /// </summary>
    [JsonPropertyName("alerts")]
    public IReadOnlyList<WidgetAlert> Alerts { get; init; } = [];

    /// <summary>Null until the device list has been read.</summary>
    [JsonPropertyName("devices")]
    public WidgetDeviceCounts? Devices { get; init; }

    /// <summary>The devices pinned in the app, in pinned order.</summary>
    [JsonPropertyName("pinned")]
    public IReadOnlyList<WidgetDevice> Pinned { get; init; } = [];

    /// <summary>The dashboard Sensors card's sensors, in its order.</summary>
    [JsonPropertyName("sensors")]
    public IReadOnlyList<WidgetSensor> Sensors { get; init; } = [];

    /// <summary>When the sensors were last read (Unix seconds) - less often than alerts, so they say so.</summary>
    [JsonPropertyName("sensorsReadAt")]
    public long SensorsReadAt { get; init; }

    /// <summary>
    /// Lock-screen widgets show counts only, no device or rule names - the
    /// same care as issue #8 takes over notifications. The platform's widgets
    /// set this from their own preference as they save.
    /// </summary>
    [JsonPropertyName("hideLockScreenDetails")]
    public bool HideLockScreenDetails { get; init; }

    /// <summary>When the check ran, as Unix seconds (simple for Swift's decoder).</summary>
    [JsonPropertyName("checkedAt")]
    public long CheckedAt { get; init; }

    /// <summary>What a widget shows before the first check, and after signing out.</summary>
    public static WidgetSnapshot SignedOut { get; } = new();

    [JsonIgnore]
    public int Active => Critical + Warning + Ok;

    /// <param name="devices">The device list, when it's been read: device counts, pinned devices and better names come from it.</param>
    /// <param name="sensors">Every sensor, when they've been read: the dashboard's picked ones are shown.</param>
    public static WidgetSnapshot Build(
        IEnumerable<Alert> alerts,
        DateTimeOffset checkedAt,
        AppSettings settings,
        IReadOnlyList<Device>? devices = null,
        IReadOnlyList<Sensor>? sensors = null,
        DateTimeOffset? sensorsReadAt = null)
    {
        var open = alerts.Where(a => a.State is AlertState.Active or AlertState.Acknowledged).ToList();
        var minimum = settings.Notifications.CountFrom;
        var includeAcknowledged = settings.AlertTabBadgeIncludesAcknowledged;
        var totals = AlertTotals.From(open, minimum, includeAcknowledged);
        var qualifying = open.Where(a => AlertCounting.Counts(a, minimum, includeAcknowledged)).ToList();
        var groups = AlertGrouping.ByDeviceAndRule(qualifying);
        var fullGroups = AlertGrouping.ByDeviceAndRule(open).ToDictionary(g => g.Key);
        var utc = settings.ServerTimestampsAreUtc;
        var style = settings.DeviceNameStyle;
        var names = devices?.ToDictionary(d => d.DeviceId, d => new DeviceItem(d, style).Name) ?? new Dictionary<int, string>();

        string DeviceName(Alert a) => names.TryGetValue(a.DeviceId, out var name) ? name : a.DisplayHostname;

        var listed = groups.OrderBy(g => g.State == AlertState.Acknowledged)
            .ThenByDescending(g => g.Severity.SortRank()).ThenByDescending(g => g.LastChanged)
            .Take(MaxAlerts)
            .Select(g => new WidgetAlert(
                g.Primary.Id, g.DeviceId, SeverityKey(g.Severity), g.DisplayRuleName, DeviceName(g.Primary),
                g.State == AlertState.Acknowledged, UnixTime(g.LastChanged, utc), fullGroups[g.Key].FaultCount, g.Primary.Identity.ToString(), g.Key.ToString()))
            .ToList();

        return new WidgetSnapshot
        {
            SignedIn = true,
            Critical = totals.Critical,
            Warning = totals.Warning,
            Ok = totals.Ok,
            Acknowledged = totals.Acknowledged,
            Faults = totals.Faults,
            UsesFaults = totals.UsesFaults,
            DevicesDown = devices?.Count(d => d.ReportedState == DeviceState.Down),
            Alerts = listed,
            Devices = devices is null ? null : CountDevices(devices, open),
            Pinned = devices is null ? [] : PinnedDevices(settings, devices, open, names),
            Sensors = sensors is null ? [] : PickedSensors(settings, sensors, names),
            SensorsReadAt = sensors is null ? 0 : (sensorsReadAt ?? checkedAt).ToUnixTimeSeconds(),
            CheckedAt = checkedAt.ToUnixTimeSeconds(),
        };
    }

    /// <summary>
    /// The sensors the dashboard's Sensors cards have picked - every card's, in
    /// the cards' order, each sensor once (#87) - whether or not they're showing.
    /// </summary>
    public static IReadOnlyList<PinnedSensor> PickedSensors(AppSettings settings) => settings.DashboardWidgets
        .Where(w => w.WidgetType == DashboardLayout.Sensors)
        .SelectMany(w => w.Sensors)
        .DistinctBy(s => s.SensorId)
        .ToList();

    private static WidgetDeviceCounts CountDevices(IReadOnlyList<Device> devices, IReadOnlyList<Alert> open)
    {
        var watched = devices.Where(d => d.ReportedState is not (DeviceState.Disabled or DeviceState.Unknown) && d.ReportedStatus is not null).ToList();
        var byDevice = open.ToLookup(a => a.DeviceId);
        int critical = 0, warning = 0, acknowledged = 0;
        foreach (var device in watched)
        {
            switch (WorstState(byDevice[device.DeviceId]))
            {
                case "critical": critical++; break;
                case "warning": warning++; break;
                case "acknowledged": acknowledged++; break;
            }
        }

        return new WidgetDeviceCounts(
            Total: devices.Count,
            Up: watched.Count(d => d.ReportedStatus == true),
            Down: watched.Count(d => d.ReportedStatus == false),
            Disabled: devices.Count(d => d.ReportedState == DeviceState.Disabled),
            Critical: critical,
            Warning: warning,
            Acknowledged: acknowledged,
            Ok: watched.Count - critical - warning - acknowledged,
            Unknown: devices.Count - watched.Count - devices.Count(d => d.ReportedState == DeviceState.Disabled));
    }

    /// <summary>A device's worst open alert: an active critical, any other active, only acknowledged, or none ("ok").</summary>
    private static string WorstState(IEnumerable<Alert> alerts)
    {
        var list = alerts.ToList();
        return list.Any(a => a.State == AlertState.Active && a.Severity == AlertSeverity.Critical) ? "critical"
            : list.Any(a => a.State == AlertState.Active) ? "warning"
            : list.Count > 0 ? "acknowledged"
            : "ok";
    }

    private static IReadOnlyList<WidgetDevice> PinnedDevices(
        AppSettings settings, IReadOnlyList<Device> devices, IReadOnlyList<Alert> open, IReadOnlyDictionary<int, string> names)
    {
        if (!settings.EnablePinnedDevices)
        {
            return [];
        }

        var byId = devices.ToDictionary(d => d.DeviceId);
        var byDevice = open.ToLookup(a => a.DeviceId);
        var utc = settings.ServerTimestampsAreUtc;

        // As the dashboard: pinned order, and a pin whose device has gone just doesn't show.
        return settings.PinnedDevices
            .Where(pin => byId.ContainsKey(pin.DeviceId))
            .Take(MaxRows)
            .Select(pin =>
            {
                var device = byId[pin.DeviceId];
                var worst = byDevice[device.DeviceId]
                    .OrderBy(a => a.State == AlertState.Acknowledged)
                    .ThenByDescending(a => a.Severity.SortRank())
                    .ThenByDescending(a => a.Timestamp)
                    .FirstOrDefault();
                var location = string.IsNullOrWhiteSpace(device.Location) ? null : device.Location;

                if (device.ReportedState is DeviceState.Disabled or DeviceState.Ignored)
                {
                    return new WidgetDevice(device.DeviceId, names[device.DeviceId], "disabled", device.ReportedState.ToDisplayString(), location);
                }

                if (device.ReportedState == DeviceState.Down)
                {
                    return new WidgetDevice(device.DeviceId, names[device.DeviceId], "down", "Down", location, worst is null ? 0 : UnixTime(worst.Timestamp, utc));
                }

                if (device.ReportedState == DeviceState.Unknown)
                    return new WidgetDevice(device.DeviceId, names[device.DeviceId], "unknown", "State unknown", location);
                var state = WorstState(byDevice[device.DeviceId]);
                var status = worst is null ? device.ReportedUptime is { } uptime ? $"Up {Formatting.Uptime(uptime)}" : "Up · uptime unknown" : worst.DisplayRuleName;
                if (worst is null && device.ReportedUptime is <= 0)
                {
                    status = "Up";
                }

                return new WidgetDevice(device.DeviceId, names[device.DeviceId], state, status, location);
            })
            .ToList();
    }

    private static IReadOnlyList<WidgetSensor> PickedSensors(
        AppSettings settings, IReadOnlyList<Sensor> sensors, IReadOnlyDictionary<int, string> names)
    {
        var byId = sensors.ToDictionary(s => s.SensorId);
        return PickedSensors(settings)
            .Take(MaxRows)
            .Select(pin =>
            {
                var device = names.TryGetValue(pin.DeviceId, out var name) ? name : pin.DeviceName ?? $"Device {pin.DeviceId}";
                if (!byId.TryGetValue(pin.SensorId, out var sensor))
                {
                    // Gone from LibreNMS (or not read yet): its saved label, no reading.
                    return new WidgetSensor(pin.SensorId, pin.DeviceId, pin.Description ?? $"Sensor {pin.SensorId}", device, "–", "unknown", null);
                }

                var reading = DeviceSectionLoader.ReadSensor(sensor, settings);
                return new WidgetSensor(
                    sensor.SensorId, sensor.DeviceId, reading.Name, device, reading.Value,
                    reading.Status switch
                    {
                        RowStatus.Critical => "critical",
                        RowStatus.Warning => "warning",
                        RowStatus.Ok => "ok",
                        _ => "unknown",
                    },
                    Position(sensor));
            })
            .ToList();
    }

    /// <summary>Where the reading sits between the sensor's own low and high limits.</summary>
    internal static double? Position(Sensor sensor) =>
        sensor is { Current: { } current, LimitLow: { } low, LimitHigh: { } high } && sensor.HasReading && double.IsFinite(low) && double.IsFinite(high) && high > low
            ? Math.Round(Math.Clamp((current - low) / (high - low), 0, 1), 3)
            : null;

    private static long UnixTime(DateTime? timestamp, bool serverTimestampsAreUtc) =>
        ServerTime.ToLocal(timestamp, serverTimestampsAreUtc) is { } local
            ? new DateTimeOffset(DateTime.SpecifyKind(local, DateTimeKind.Local)).ToUnixTimeSeconds()
            : 0;

    public string ToJson() => JsonSerializer.Serialize(this);

    /// <summary>The saved snapshot, or <see cref="SignedOut"/> when there's none or it can't be read.</summary>
    public static WidgetSnapshot FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return SignedOut;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("version", out var version)
                || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != CurrentVersion)
                return SignedOut;
            return JsonSerializer.Deserialize<WidgetSnapshot>(json) ?? SignedOut;
        }
        catch (JsonException)
        {
            return SignedOut;
        }
    }

    /// <summary>"critical", "warning" or "ok" - what the widgets colour by.</summary>
    internal static string SeverityKey(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Critical => "critical",
        AlertSeverity.Warning => "warning",
        _ => "ok",
    };
}

/// <summary>
/// The platform's home-screen widgets - Android app widgets, an iOS
/// WidgetKit extension.
/// </summary>
public interface IHomeWidgets
{
    /// <summary>
    /// True when there's a widget to keep up to date, so checks are worth
    /// running (and the device list worth reading) for it alone.
    /// </summary>
    bool IsInUse { get; }

    /// <summary>Whether this platform has lock-screen widgets (iPhone), so the privacy option is worth offering.</summary>
    bool HasLockScreenWidgets { get; }

    /// <summary>
    /// Lock-screen widgets show counts only. A phone preference, applied to
    /// the saved snapshot straight away rather than at the next check.
    /// </summary>
    bool HideLockScreenDetails { get; set; }

    /// <summary>Saves <paramref name="snapshot"/> where the widgets read it, and asks them to redraw.</summary>
    void Update(WidgetSnapshot snapshot);
}

/// <summary>No widgets: the default, and in tests.</summary>
public sealed class NoHomeWidgets : IHomeWidgets
{
    public bool IsInUse => false;

    public bool HasLockScreenWidgets => false;

    public bool HideLockScreenDetails { get; set; } = true;

    public void Update(WidgetSnapshot snapshot)
    {
    }
}
