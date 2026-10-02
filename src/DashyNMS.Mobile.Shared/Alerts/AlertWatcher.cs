using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Widgets;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Alerts;

public enum AlertCheckOutcome
{
    /// <summary>Another check was already running; this one did nothing.</summary>
    Skipped,

    /// <summary>No saved session to check with (signed out, or the token no longer works).</summary>
    NotSignedIn,

    /// <summary>First check for this server: states recorded, nothing announced.</summary>
    Baseline,

    Checked,

    Failed,
}

public sealed record AlertCheckResult(AlertCheckOutcome Outcome, int Changes = 0, int Notified = 0, string? Error = null)
{
    /// <summary>Worth the platform scheduling the next check as normal (as opposed to backing off).</summary>
    public bool Succeeded => Outcome is not AlertCheckOutcome.Failed;
}

/// <summary>
/// One alert check: fetch, compare with the last check, notify. The same
/// code runs from the in-app timer, Android's WorkManager and iOS's background
/// app refresh.
/// </summary>
/// <remarks>
/// A background wake is often a brand-new process with nobody signed in yet,
/// so this restores the saved session itself rather than relying on the
/// sign-in page having done so.
/// </remarks>
public sealed class AlertWatcher
{
    private readonly ILibreNmsClient _client;
    private readonly ISessionService _session;
    private readonly SecretCache _secrets;
    private readonly ISettingsStore _settings;
    private readonly IAlertWatchStore _store;
    private readonly ISelfActionTracker _selfActions;
    private readonly IAlertNotifier _notifier;
    private readonly IAppBadge _badge;
    private readonly AlertTabDot? _tabDot;
    private readonly AlertCountThreshold? _countThreshold;
    private readonly IgnoredAlerts? _ignored;
    private readonly IHomeWidgets _widgets;
    private readonly TimeProvider _time;
    private readonly ILogger<AlertWatcher> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<DesktopNMS.Core.Models.Device>? _devices;
    private DateTimeOffset _devicesAt;
    private IReadOnlyList<DesktopNMS.Core.Models.Sensor>? _sensors;
    private DateTimeOffset _sensorsAt;

    public AlertWatcher(
        ILibreNmsClient client,
        ISessionService session,
        SecretCache secrets,
        ISettingsStore settings,
        IAlertWatchStore store,
        ISelfActionTracker selfActions,
        IAlertNotifier notifier,
        IAppBadge badge,
        IHomeWidgets widgets,
        TimeProvider time,
        ILogger<AlertWatcher> logger,
        AlertTabDot? tabDot = null,
        AlertCountThreshold? countThreshold = null,
        IgnoredAlerts? ignored = null)
    {
        _countThreshold = countThreshold;
        _ignored = ignored;
        _client = client;
        _session = session;
        _secrets = secrets;
        _settings = settings;
        _store = store;
        _selfActions = selfActions;
        _notifier = notifier;
        _badge = badge;
        _widgets = widgets;
        _time = time;
        _logger = logger;
        _tabDot = tabDot;
    }

    public async Task<AlertCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        // The in-app timer and a background wake can land together; one
        // check at a time, or both would announce the same change.
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken).ConfigureAwait(false))
        {
            return new AlertCheckResult(AlertCheckOutcome.Skipped);
        }

        try
        {
            if (!await EnsureSignedInAsync(cancellationToken).ConfigureAwait(false))
            {
                return new AlertCheckResult(AlertCheckOutcome.NotSignedIn);
            }

            var server = _session.Connection!.WebRoot.ToString();
            var settings = _settings.Current;

            // Recoveries only show up if recovered alerts are fetched: the open
            // list just loses them.
            var query = settings.Notifications.NotifyOnRecovery || settings.IncludeRecoveredAlerts
                ? AlertQuery.All
                : AlertQuery.Open;
            var alerts = await _client.Alerts.ListAsync(query, cancellationToken).ConfigureAwait(false);

            var saved = _store.Load();
            var hasBaseline = saved is not null && saved.Server == server;
            var changes = AlertChangeDetector.Detect(hasBaseline ? saved!.States : new Dictionary<int, int>(), alerts);
            _store.Save(new AlertWatchState(server, AlertChangeDetector.Snapshot(alerts)));
            _badge.SetCount(AlertBadge.Count(alerts, settings, _countThreshold?.Minimum ?? DesktopNMS.Core.Models.AlertSeverity.Ok));
            _tabDot?.Update(alerts, settings);

            if (_widgets.IsInUse)
            {
                var devices = await DevicesAsync(cancellationToken).ConfigureAwait(false);
                var sensors = await SensorsAsync(settings, cancellationToken).ConfigureAwait(false);
                _widgets.Update(WidgetSnapshot.Build(alerts, _time.GetUtcNow(), settings, devices, sensors, _sensorsAt));
            }

            if (!hasBaseline && settings.Notifications.SuppressOnFirstPoll)
            {
                _logger.LogInformation("First alert check for {Server}: {Count} alert(s) recorded, none announced", server, alerts.Count);
                return new AlertCheckResult(AlertCheckOutcome.Baseline, changes.Count);
            }

            // Devices named as the user chose to see them (#80), not by the
            // alert's own hostname - often an IP. The device list (cached, as
            // the widgets use it) is only read when there's something to say.
            IReadOnlyDictionary<int, DesktopNMS.Core.Models.Device>? devicesById = null;
            if (changes.Count > 0 && settings.Notifications.Enabled
                && await DevicesAsync(cancellationToken).ConfigureAwait(false) is { } known)
            {
                devicesById = known.GroupBy(d => d.DeviceId).ToDictionary(g => g.Key, g => g.First());
            }

            var nameStyle = settings.DeviceNameStyle;
            var localNow = _time.GetLocalNow().DateTime;

            // Alerts the user chose to ignore (#102) say nothing - only here:
            // the badge, dot and widgets above still count them.
            var plan = AlertNotificationPlanner.Plan(
                _ignored?.Filter(changes) ?? changes,
                settings.Notifications,
                localNow,
                _selfActions,
                alert => nameStyle.Resolve(devicesById?.GetValueOrDefault(alert.DeviceId), alert.DisplayHostname),
                alert => devicesById?.GetValueOrDefault(alert.DeviceId)?.LocationName());

            foreach (var tag in plan.Remove)
            {
                _notifier.Remove(tag);
            }

            foreach (var notification in plan.Show)
            {
                await _notifier.ShowAsync(notification).ConfigureAwait(false);
            }

            if (changes.Count > 0)
            {
                _logger.LogInformation("Alert check: {Changes} change(s), {Notified} notified", changes.Count, plan.Show.Count);
            }

            return new AlertCheckResult(AlertCheckOutcome.Checked, changes.Count, plan.Show.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The platform took the time back (iOS gives a background task ~30s).
            return new AlertCheckResult(AlertCheckOutcome.Failed, Error: "Cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Alert check failed");
            return new AlertCheckResult(AlertCheckOutcome.Failed, Error: ex.Message);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>How long a device list stays good enough for the widgets.</summary>
    internal static readonly TimeSpan DevicesMaxAge = TimeSpan.FromMinutes(5);

    /// <summary>How long sensor readings stay good enough for the widgets - LibreNMS itself only polls every five minutes or so.</summary>
    internal static readonly TimeSpan SensorsMaxAge = TimeSpan.FromMinutes(15);

    /// <summary>
    /// The device list, for the widgets' device counts and pinned devices -
    /// read at most every few minutes, since it's far bigger than the alert
    /// list, and kept (or left unknown) if reading it fails: the alerts
    /// matter more.
    /// </summary>
    private async Task<IReadOnlyList<DesktopNMS.Core.Models.Device>?> DevicesAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        if (_devices is not null && now - _devicesAt < DevicesMaxAge)
        {
            return _devices;
        }

        try
        {
            _devices = await _client.Devices.ListAsync(cancellationToken).ConfigureAwait(false);
            _devicesAt = now;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Could not read devices for names and the widgets");
        }

        return _devices;
    }

    /// <summary>
    /// Every sensor, for the Sensors widget - only when the dashboard has
    /// sensors picked (LibreNMS lists every sensor at once, which on a big
    /// network is a big fetch), and at most every quarter of an hour.
    /// </summary>
    private async Task<IReadOnlyList<DesktopNMS.Core.Models.Sensor>?> SensorsAsync(DesktopNMS.Core.Configuration.AppSettings settings, CancellationToken cancellationToken)
    {
        if (WidgetSnapshot.PickedSensors(settings).Count == 0)
        {
            return null;
        }

        var now = _time.GetUtcNow();
        if (_sensors is not null && now - _sensorsAt < SensorsMaxAge)
        {
            return _sensors;
        }

        try
        {
            _sensors = await _client.Sensors.ListAsync(cancellationToken).ConfigureAwait(false);
            _sensorsAt = now;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Could not read sensors for the widgets");
        }

        return _sensors;
    }

    private async Task<bool> EnsureSignedInAsync(CancellationToken cancellationToken)
    {
        if (_session.IsConnected)
        {
            return true;
        }

        await _secrets.EnsureLoadedAsync(ServiceCollectionExtensions.SecretKeys).ConfigureAwait(false);
        var restored = await _session.TryRestoreAsync(cancellationToken).ConfigureAwait(false);
        return restored?.Succeeded == true;
    }
}
