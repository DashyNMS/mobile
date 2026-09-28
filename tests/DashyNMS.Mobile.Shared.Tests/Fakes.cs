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
        return client;
    }

    public static ISettingsStore Settings(AppSettings? settings = null)
    {
        var store = Substitute.For<ISettingsStore>();
        store.Current.Returns(settings ?? new AppSettings());
        return store;
    }

    public static SecretCache Secrets() =>
        new(Substitute.For<ISecureStorage>(), Microsoft.Extensions.Logging.Abstractions.NullLogger<SecretCache>.Instance);

    public static Device Device(int id, string name, bool up = true, bool disabled = false, bool ignore = false, string? ip = null) =>
        new() { DeviceId = id, Hostname = name, Status = up, Disabled = disabled, Ignore = ignore, Ip = ip };

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
