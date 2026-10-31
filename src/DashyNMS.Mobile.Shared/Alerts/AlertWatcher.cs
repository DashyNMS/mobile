using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Widgets;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using CoreWatchState = DesktopNMS.Core.Alerting.AlertWatchState;
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
    /// <summary>Changes to alerts the notification rules leave out (#167) - not announced.</summary>
    public int Ignored { get; init; }

    /// <summary>What a failed check threw, for an error report (#217) if it wasn't an expected state.</summary>
    public Exception? Exception { get; init; }

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
    private readonly CorePolling _polling;
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
    private readonly IHomeWidgets _widgets;
    private readonly TimeProvider _time;
    private readonly ILogger<AlertWatcher> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Diagnostics.IErrorReports? _errors;
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
        NotifyRules? rules = null, CorePolling? polling = null,
        Diagnostics.IErrorReports? errors = null)
    {
        _errors = errors;
        // Asked for only so a phone's pre-1.1.0 ignored list has moved into
        // the settings (#167) before the first check: the planner reads them.
        _ = rules;
        _polling = polling ?? new CorePolling(client, session, settings);
        _countThreshold = countThreshold;
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

    /// <param name="background">Run by iOS or Android while the app was closed, rather than by the in-app timer - for the diagnostics.</param>
    public async Task<AlertCheckResult> CheckAsync(CancellationToken cancellationToken = default, bool background = false)
    {
        var started = _time.GetTimestamp();
        var result = await CheckOnceAsync(cancellationToken).ConfigureAwait(false);
        Describe(result, background, _time.GetElapsedTime(started));

        // A check that failed unexpectedly is a warning report (#217) - but
        // never from a background launch, which is never reported.
        if (!background && result.Exception is { } failure)
        {
            _errors?.Capture(failure, Diagnostics.ErrorOrigin.Caught);
        }

        return result;
    }

    /// <summary>
    /// Each check in a line of the diagnostics (#126): where it ran, what
    /// changed, what was ignored, announced or not, and how long it took. An
    /// in-app check with nothing new reads the same each time, so a run of
    /// them is one line ("× 12").
    /// </summary>
    private void Describe(AlertCheckResult result, bool background, TimeSpan took)
    {
        var where = background ? "Background alert check" : "Alert check";
        var time = $"{took.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} s";
        switch (result.Outcome)
        {
            case AlertCheckOutcome.Skipped when background:
                _logger.LogInformation("{Where}: skipped, another check was running", where);
                break;
            case AlertCheckOutcome.NotSignedIn:
                _logger.LogInformation("{Where}: not signed in", where);
                break;
            case AlertCheckOutcome.Checked when result.Changes == 0:
                if (background)
                {
                    _logger.LogInformation("{Where}: no changes · {Time}", where, time);
                }
                else
                {
                    _logger.LogInformation("{Where}: no changes", where);
                }

                break;
            case AlertCheckOutcome.Checked:
                var quiet = result.Changes - result.Ignored - result.Notified;
                _logger.LogInformation(
                    "{Where}: {Changes} change(s) · {Ignored} ignored · {Notified} notified · {Quiet} not announced (severity settings, quiet hours or grouped) · {Time}",
                    where, result.Changes, result.Ignored, result.Notified, Math.Max(quiet, 0), time);
                break;
            case AlertCheckOutcome.Failed:
                _logger.LogWarning("{Where} failed after {Time}: {Error}", where, time, result.Error);
                break;
        }
    }

    private async Task<AlertCheckResult> CheckOnceAsync(CancellationToken cancellationToken)
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

            // Open alerts only, whatever the recovery settings - as desktop,
            // whose recovery notifications don't widen its list either.
            // Recoveries come from looking up each alert that leaves the open
            // list (below), and nothing here shows a recovered one. Asking for
            // recovered faults too got HTTP 500 from a LibreNMS 26.10 server on
            // every check, which stopped notifications, the badge and the
            // widgets with it.
            var fetchStarted = _time.GetTimestamp();
            var source = await _client.Alerts.GetSourceAsync(cancellationToken).ConfigureAwait(false);
            var alerts = await _client.Alerts.ListAsync(AlertQuery.Open, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Alert list completed in {Milliseconds} ms", _time.GetElapsedTime(fetchStarted).TotalMilliseconds);
            cancellationToken.ThrowIfCancellationRequested();
            if (alerts.Any(a => a.Source != source)) throw new InvalidOperationException("Alert list returned an inconsistent source");

            var saved = _store.Load();
            var hasBaseline = saved is not null && saved.Server == server;
            var previous = hasBaseline ? saved!.Snapshot : CoreWatchState.Empty;
            var rows = alerts.ToList();
            var unresolved = new System.Collections.Concurrent.ConcurrentBag<AlertKey>();
            if (!previous.NeedsBaseline && previous.Source == source)
            {
                var present = rows.Select(a => a.Identity).ToHashSet();
                var missing = previous.Outstanding.Where(e => !present.Contains(e.Key)).ToList();
                var resolved = new System.Collections.Concurrent.ConcurrentBag<Alert>();
                var recoveryStarted = _time.GetTimestamp();
                await Parallel.ForEachAsync(missing, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cancellationToken }, async (entry, token) =>
                {
                    try
                    {
                        var row = await _client.Alerts.GetAsync(entry.Key.Id, token).ConfigureAwait(false);
                        if (row is not null && row.Identity == entry.Key && row.DeviceId == entry.Value.DeviceId && row.RuleId == entry.Value.RuleId)
                            resolved.Add(row);
                        else unresolved.Add(entry.Key);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                    catch (Exception ex)
                    {
                        unresolved.Add(entry.Key);
                        _logger.LogWarning("Recovery lookup for {Key} failed: {Reason}", entry.Key, ex.Message);
                    }
                }).ConfigureAwait(false);
                rows.AddRange(resolved);
                _logger.LogInformation("Alert recovery lookups completed in {Milliseconds} ms; {Unresolved} unresolved", _time.GetElapsedTime(recoveryStarted).TotalMilliseconds, unresolved.Count);
            }
            var detected = AlertGroupChangeDetector.Detect(previous, source, rows, _time.GetUtcNow().UtcDateTime, unresolved.ToArray());
            var changes = detected.Changes;
            var nextState = new AlertWatchState(server, detected.Next);
            if (detected.IsBaseline)
            {
                _notifier.RemoveAll();
                _logger.LogInformation("Quiet alert baseline: {Reason}", detected.BaselineReason);
            }
            _badge.SetCount(AlertBadge.Count(alerts, settings, _countThreshold?.Minimum ?? settings.Notifications.CountFrom));
            _tabDot?.Update(alerts, settings);

            var quietBaseline = detected.IsBaseline || (!hasBaseline && settings.Notifications.SuppressOnFirstPoll);
            // Use only names already loaded by a page or a previous widget refresh.
            var devicesById = _polling.CachedDeviceNames;

            var nameStyle = settings.DeviceNameStyle;
            var localNow = _time.GetLocalNow().DateTime;

            // Which alerts notify (#167) is Core's planner's to decide, from the
            // settings - only here: the badge, dot and widgets above still
            // count a left-out alert.
            var plan = AlertNotificationPlanner.Plan(
                quietBaseline ? [] : changes,
                settings.Notifications,
                localNow,
                _selfActions,
                alert => nameStyle.Resolve(devicesById?.GetValueOrDefault(alert.DeviceId), alert.DisplayHostname),
                alert => devicesById?.GetValueOrDefault(alert.DeviceId)?.LocationName());

            cancellationToken.ThrowIfCancellationRequested();
            var deliveryStarted = _time.GetTimestamp();
            foreach (var tag in plan.Remove)
            {
                _notifier.Remove(tag);
            }

            foreach (var notification in plan.Show)
            {
                await _notifier.ShowAsync(notification).ConfigureAwait(false);
            }

            // Commit only after the platform accepted every notification. A cancelled
            // fetch or failed delivery leaves the previous state available to retry.
            _store.Save(nextState);
            _logger.LogInformation("Alert notification delivery completed in {Milliseconds} ms", _time.GetElapsedTime(deliveryStarted).TotalMilliseconds);
            if (_widgets.IsInUse)
            {
                // Alert data is current immediately; fleet enrichment is optional.
                _widgets.Update(WidgetSnapshot.Build(alerts, _time.GetUtcNow(), settings) with { PreserveEnrichment = true });
                await EnrichWidgetsAsync(alerts, settings, cancellationToken).ConfigureAwait(false);
            }

            if (quietBaseline)
                return new AlertCheckResult(AlertCheckOutcome.Baseline, changes.Count);

            if (plan.SuppressionReason is { } why)
            {
                _logger.LogInformation("Not announced: {Why}", why);
            }

            return new AlertCheckResult(AlertCheckOutcome.Checked, changes.Count, plan.Show.Count) { Ignored = plan.LeftOutByRuleCount };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The platform took the time back (iOS gives a background task ~30s).
            return new AlertCheckResult(AlertCheckOutcome.Failed, Error: "Cancelled");
        }
        catch (Exception ex)
        {
            // Said once, with the time, by Describe.
            return new AlertCheckResult(AlertCheckOutcome.Failed, Error: $"{ex.GetType().Name}: {ex.Message}") { Exception = ex };
        }
        finally
        {
            _gate.Release();
        }
    }

    internal TimeSpan WidgetRefreshBudget { get; init; } = TimeSpan.FromSeconds(5);

    private async Task EnrichWidgetsAsync(IReadOnlyList<DesktopNMS.Core.Models.Alert> alerts, AppSettings settings, CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(WidgetRefreshBudget);
        var started = _time.GetTimestamp();
        var stage = "widget devices";
        try
        {
            var devices = await DevicesAsync(budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
            if (devices is null) throw new InvalidOperationException("Device refresh failed");
            stage = "widget sensors";
            var sensors = await SensorsAsync(settings, budget.Token).WaitAsync(budget.Token).ConfigureAwait(false);
            budget.Token.ThrowIfCancellationRequested();
            _widgets.Update(WidgetSnapshot.Build(alerts, _time.GetUtcNow(), settings, devices, sensors, _sensorsAt));
            _logger.LogInformation("Widget enrichment completed in {Milliseconds} ms", _time.GetElapsedTime(started).TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("{Stage} stopped after {Milliseconds} ms; retaining alert snapshot: {Reason}", stage, _time.GetElapsedTime(started).TotalMilliseconds, ex.Message);
        }
    }

    private Task<IReadOnlyList<DesktopNMS.Core.Models.Device>?> DevicesAsync(CancellationToken cancellationToken) =>
        _polling.CachedDevicesAsync(cancellationToken);

    private async Task<IReadOnlyList<DesktopNMS.Core.Models.Sensor>?> SensorsAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        if (WidgetSnapshot.PickedSensors(settings).Count == 0) return null;
        var result = await _polling.WidgetSensorsAsync(cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded) throw new InvalidOperationException(result.ErrorMessage);
        _sensorsAt = result.ObservedAt ?? default;
        return result.Sensors;
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
