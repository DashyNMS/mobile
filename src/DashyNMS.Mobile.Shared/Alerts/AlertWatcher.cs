using DashyNMS.Mobile.Security;
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
    private readonly TimeProvider _time;
    private readonly ILogger<AlertWatcher> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AlertWatcher(
        ILibreNmsClient client,
        ISessionService session,
        SecretCache secrets,
        ISettingsStore settings,
        IAlertWatchStore store,
        ISelfActionTracker selfActions,
        IAlertNotifier notifier,
        IAppBadge badge,
        TimeProvider time,
        ILogger<AlertWatcher> logger)
    {
        _client = client;
        _session = session;
        _secrets = secrets;
        _settings = settings;
        _store = store;
        _selfActions = selfActions;
        _notifier = notifier;
        _badge = badge;
        _time = time;
        _logger = logger;
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
            _badge.SetCount(AlertBadge.Count(alerts, settings));

            if (!hasBaseline && settings.Notifications.SuppressOnFirstPoll)
            {
                _logger.LogInformation("First alert check for {Server}: {Count} alert(s) recorded, none announced", server, alerts.Count);
                return new AlertCheckResult(AlertCheckOutcome.Baseline, changes.Count);
            }

            var localNow = _time.GetLocalNow().DateTime;
            var plan = AlertNotificationPlanner.Plan(changes, settings.Notifications, localNow, _selfActions);

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
