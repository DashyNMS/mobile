using System.Text;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>
/// The first lookup (#570): how the whole network is doing, as a few lines of
/// text. Counts cover everything; names are capped, so the result stays about
/// the same size on a network of ten devices or ten thousand.
/// </summary>
/// <remarks>
/// Read-only, over the session's own client, so certificate trust and the
/// backup address apply. Desktop's equivalent returns detailed JSON for a
/// large cloud model, which doesn't fit here.
/// </remarks>
public sealed class NetworkOverviewLookup
{
    /// <summary>How many down devices, and how many alerts, are named before "and N more".</summary>
    internal const int MaxNamed = 8;

    /// <summary>A name longer than this is cut, so one long sysName can't spend the budget.</summary>
    internal const int MaxNameLength = 40;

    public const string Name = "network_overview";

    public const string Purpose =
        "Current state of the whole network from LibreNMS: how many devices are up or down, which are down, and the open alerts.";

    private readonly ILibreNmsClient _client;
    private readonly ISettingsStore _settings;

    public NetworkOverviewLookup(ILibreNmsClient client, ISettingsStore settings)
    {
        _client = client;
        _settings = settings;
    }

    public IntelligenceLookup AsLookup() => new(Name, Purpose, (_, cancellationToken) => RunAsync(cancellationToken));

    public async Task<string> RunAsync(CancellationToken cancellationToken = default)
    {
        var devicesTask = _client.Devices.ListAsync(cancellationToken);
        var alertsTask = _client.Alerts.ListAsync(AlertQuery.Open, cancellationToken);
        await Task.WhenAll(devicesTask, alertsTask).ConfigureAwait(false);
        return Describe(await devicesTask.ConfigureAwait(false), await alertsTask.ConfigureAwait(false), _settings.Current.DeviceNameStyle);
    }

    internal static string Describe(IReadOnlyList<Device> devices, IReadOnlyList<Alert> alerts, DeviceNameStyle nameStyle)
    {
        var text = new StringBuilder();
        var byId = new Dictionary<int, Device>();
        foreach (var device in devices)
            byId[device.DeviceId] = device;

        int Count(DeviceState state) => devices.Count(d => d.ReportedState == state);
        var down = devices.Where(d => d.ReportedState == DeviceState.Down)
            .Select(d => Short(nameStyle.Resolve(d, d.Hostname)))
            .OrderBy(name => name, NaturalOrder.Instance)
            .ToList();

        text.Append("Devices: ").Append(devices.Count).Append(" total, ")
            .Append(Count(DeviceState.Up)).Append(" up, ")
            .Append(down.Count).Append(" down");
        AppendCount(text, Count(DeviceState.Disabled), "disabled");
        AppendCount(text, Count(DeviceState.Ignored), "ignored");
        AppendCount(text, Count(DeviceState.Unknown), "unknown");
        text.Append(".\n");

        if (down.Count > 0)
            text.Append("Down: ").Append(Listed(down)).Append(".\n");

        if (alerts.Count == 0)
        {
            text.Append("Open alerts: none.");
            return text.ToString();
        }

        text.Append("Open alerts: ").Append(alerts.Count).Append(" total, ")
            .Append(alerts.Count(a => a.Severity == AlertSeverity.Critical)).Append(" critical, ")
            .Append(alerts.Count(a => a.Severity == AlertSeverity.Warning)).Append(" warning");
        AppendCount(text, alerts.Count(a => a.State == AlertState.Acknowledged), "acknowledged");
        text.Append(".\n");

        // Worst first, and unacknowledged before acknowledged: what a person would want named.
        var named = alerts
            .OrderBy(a => a.Severity == AlertSeverity.Critical ? 0 : a.Severity == AlertSeverity.Warning ? 1 : 2)
            .ThenBy(a => a.State == AlertState.Acknowledged ? 1 : 0)
            .Select(a => (a.Severity == AlertSeverity.Critical ? "critical" : a.Severity == AlertSeverity.Warning ? "warning" : "ok")
                + ": " + Short(a.DisplayRuleName)
                + " on " + Short(nameStyle.Resolve(byId.GetValueOrDefault(a.DeviceId), a.DisplayHostname)))
            .ToList();
        text.Append("Alerts: ").Append(Listed(named)).Append('.');
        return text.ToString();
    }

    private static void AppendCount(StringBuilder text, int count, string what)
    {
        if (count > 0)
            text.Append(", ").Append(count).Append(' ').Append(what);
    }

    private static string Listed(IReadOnlyList<string> names) =>
        names.Count <= MaxNamed
            ? string.Join("; ", names)
            : string.Join("; ", names.Take(MaxNamed)) + "; and " + (names.Count - MaxNamed) + " more";

    private static string Short(string? name)
    {
        var text = string.IsNullOrWhiteSpace(name) ? "unnamed" : name.Trim();
        return text.Length <= MaxNameLength ? text : text[..(MaxNameLength - 1)] + "…";
    }
}
