using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

internal static class Fakes
{
    public static ILibreNmsClient Client(IReadOnlyList<Device>? devices = null, IReadOnlyList<Alert>? alerts = null)
    {
        var client = Substitute.For<ILibreNmsClient>();
        client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(devices ?? []);
        client.Alerts.ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>()).Returns(alerts ?? []);
        client.Alerts.GetSourceAsync(Arg.Any<CancellationToken>()).Returns(alerts?.FirstOrDefault()?.Source ?? AlertSource.Alert);
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Sensor>());
        return client;
    }

    /// <summary>
    /// A session substitute whose <see cref="DesktopNMS.Services.ISessionService.CurrentScope"/>
    /// follows whatever <c>Connection</c> and <c>ConnectionGeneration</c> are set to, as the
    /// real session's does, so tests can keep configuring those two.
    /// </summary>
    public static DesktopNMS.Services.ISessionService Session()
    {
        var session = Substitute.For<DesktopNMS.Services.ISessionService>();
        session.CurrentScope.Returns(_ => session.Connection is { } connection
            ? new SessionScope(session.ConnectionGeneration, connection)
            : (SessionScope?)null);
        return session;
    }

    public static ISettingsStore Settings(AppSettings? settings = null)
    {
        var store = Substitute.For<ISettingsStore>();
        store.Current.Returns(settings ?? new AppSettings());
        return store;
    }

    public static SecretCache Secrets() =>
        new(Substitute.For<ISecureStorage>(), Microsoft.Extensions.Logging.Abstractions.NullLogger<SecretCache>.Instance);

    public static Device Device(
        int id,
        string name,
        bool up = true,
        bool disabled = false,
        bool ignore = false,
        string? ip = null,
        string? type = null,
        string? location = null,
        long uptime = 0,
        string? sysName = null) =>
        new()
        {
            DeviceId = id,
            Hostname = name,
            SysName = sysName,
            Status = up,
            Disabled = disabled,
            Ignore = ignore,
            Ip = ip,
            Type = type,
            Location = location,
            Uptime = uptime,
        };

    public static DesktopNMS.Core.Alerting.AlertWatchState Watch(params Alert[] alerts) =>
        DesktopNMS.Core.Alerting.AlertGroupChangeDetector.Detect(DesktopNMS.Core.Alerting.AlertWatchState.Empty,
            alerts.FirstOrDefault()?.Source ?? AlertSource.Alert, alerts, DateTime.UtcNow).Next;

    /// <summary>StateValue 1 is active, 2 acknowledged - see AlertStateExtensions.FromValue.</summary>
    public static Alert Alert(int id, int deviceId, string severity, bool acknowledged = false, DateTime? at = null) =>
        new()
        {
            Id = id,
            DeviceId = deviceId,
            RuleId = id * 10,
            RuleName = $"Rule {id}",
            Hostname = $"host{deviceId}",
            SeverityText = severity,
            StateValue = acknowledged ? 2 : 1,
            Open = true,
            Timestamp = at ?? new DateTime(2026, 1, 1, 12, 0, 0),
        };

    /// <summary>
    /// The shared operations' per-connection transport for tests written against <see cref="Client"/>:
    /// its device-list request is answered by the fake client's <c>Devices.ListAsync</c> and its open
    /// alerts by <c>Alerts.ListAsync(AlertQuery.Open)</c>, behind Core's <c>/faults</c> probe (which
    /// follows <c>Alerts.GetSourceAsync</c>), so stubs and received-call checks on the fake keep
    /// working. Anything else fails the test.
    /// </summary>
    public static Func<DashyNMS.Mobile.Services.SessionScope, ILibreNmsTransport> DeviceTransport(ILibreNmsClient client) =>
        scope => new ClientDeviceTransport(client, scope.Connection);

    private sealed class ClientDeviceTransport(ILibreNmsClient client, LibreNmsConnection connection) : ILibreNmsTransport
    {
        public LibreNmsConnection? Connection => connection;

        public async Task<IReadOnlyList<T>> GetCollectionAsync<T>(string relativeUrl, string collectionProperty, CancellationToken cancellationToken = default)
        {
            if (relativeUrl == "devices" && typeof(T) == typeof(Device))
                return (IReadOnlyList<T>)(object)await client.Devices.ListAsync(cancellationToken);
            if (typeof(T) != typeof(Alert))
                throw new InvalidOperationException("Only the device list and open alerts are expected");
            if (relativeUrl == "faults?state=-1")
                return await client.Alerts.GetSourceAsync(cancellationToken) == AlertSource.Fault
                    ? []
                    : throw new LibreNmsApiException("No faults route", System.Net.HttpStatusCode.NotFound);
            if (relativeUrl == AlertQuery.Open.ToRelativeUrl("faults") || relativeUrl == AlertQuery.Open.ToRelativeUrl("alerts"))
                return (IReadOnlyList<T>)(object)await client.Alerts.ListAsync(AlertQuery.Open, cancellationToken);
            throw new InvalidOperationException("Only the open alerts are expected, not " + relativeUrl);
        }

        public Task<System.Text.Json.JsonDocument> SendAsync(HttpMethod method, string relativeUrl, object? body = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Only the device list and open alerts are expected");

        public Task<string> SendRawAsync(string relativeUrl, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Only the device list and open alerts are expected");
    }
}

/// <summary>Records what would have been shown on the phone.</summary>
internal sealed class RecordingNotifier : IAlertNotifier
{
    public bool PermissionGranted { get; set; } = true;

    public int PermissionRequests { get; private set; }

    public List<AlertNotification> Shown { get; } = new();

    public List<string> Removed { get; } = new();

    public Task<bool> RequestPermissionAsync()
    {
        PermissionRequests++;
        return Task.FromResult(PermissionGranted);
    }

    public Task ShowAsync(AlertNotification notification)
    {
        Shown.Add(notification);
        return Task.CompletedTask;
    }

    public void Remove(string tag) => Removed.Add(tag);

    public int RemoveAllCalls { get; private set; }
    public void RemoveAll() => RemoveAllCalls++;
}

/// <summary>Records the app icon's count, as a phone that can show one (an iPhone).</summary>
internal sealed class RecordingBadge : IAppBadge
{
    public bool IsSupported { get; set; } = true;

    public int? Count { get; private set; }

    public void SetCount(int count) => Count = count;
}

/// <summary>Records what the home-screen widgets were given.</summary>
internal sealed class RecordingWidgets : DashyNMS.Mobile.Widgets.IHomeWidgets
{
    public bool IsInUse { get; set; } = true;

    public bool HasLockScreenWidgets => true;

    public bool HideLockScreenDetails { get; set; } = true;

    public List<DashyNMS.Mobile.Widgets.WidgetSnapshot> Updates { get; } = new();

    public void Update(DashyNMS.Mobile.Widgets.WidgetSnapshot snapshot) => Updates.Add(snapshot);
}

/// <summary>Records where the view model asked to go.</summary>
internal sealed class RecordingNavigation : INavigationService
{
    public List<(string Route, IDictionary<string, object>? Parameters)> Visits { get; } = new();

    public Task GoToAsync(string route, IDictionary<string, object>? parameters = null)
    {
        Visits.Add((route, parameters));
        return Task.CompletedTask;
    }
}
