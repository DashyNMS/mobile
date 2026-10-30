using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;

namespace DashyNMS.Mobile.Diagnostics;

/// <summary>
/// Launch to the first usable page (#218), as desktop's start-up time
/// (DashyNMS/desktop#499): measured once a process, and sampled - one launch
/// in ten.
/// </summary>
/// <remarks>
/// Timed from when the app was built, so a process iOS or Android started in
/// the background for an alert check, and that's opened later, isn't
/// measured: its window came long after it started.
/// </remarks>
public sealed class StartupTiming
{
    /// <summary>A window later than this after launch means the process started in the background.</summary>
    public static readonly TimeSpan BackgroundLaunch = TimeSpan.FromSeconds(10);

    private readonly TimeProvider _time;
    private readonly long _launched;
    private readonly Func<bool> _sample;
    private bool _windowInTime;
    private bool _windowSeen;
    private bool _done;

    public StartupTiming(TimeProvider? time = null, Func<bool>? sample = null)
    {
        _time = time ?? TimeProvider.System;
        _sample = sample ?? (() => Random.Shared.Next(10) == 0);
        _launched = _time.GetTimestamp();
    }

    /// <summary>The window is on screen.</summary>
    public void WindowShown()
    {
        if (!_windowSeen)
        {
            _windowSeen = true;
            _windowInTime = _time.GetElapsedTime(_launched) <= BackgroundLaunch;
        }
    }

    /// <summary>The first page is usable: Sign in showing, or a page's first load done.</summary>
    public void Ready(IAppMetrics metrics)
    {
        if (_done || !_windowSeen)
        {
            return;
        }

        _done = true;
        var took = _time.GetElapsedTime(_launched);
        if (_windowInTime && took <= TimeSpan.FromMinutes(5) && metrics.IsEnabled && _sample())
        {
            metrics.Record(new MetricObservation(MetricName.StartupDuration, took.TotalMilliseconds));
        }
    }
}

/// <summary>
/// The measured pages' first loads (#218, DashyNMS/desktop#500) and Core's
/// LibreNMS requests (#218, DashyNMS/desktop#501, DashyNMS/core#67): a
/// fixed page or request kind, an outcome and a time - never a URL, a name or
/// what came back.
/// </summary>
public static class Measurements
{
    /// <summary>A page's first load, if it's one of the pages measured.</summary>
    public static void PageLoaded(IAppMetrics metrics, object? viewModel, FirstLoad load)
    {
        if (viewModel is null || MetricRules.PageArea(viewModel.GetType().Name) is not { } area
            || load.Took < TimeSpan.Zero || load.Took > TimeSpan.FromMinutes(5))
        {
            return;
        }

        var outcome = load.Outcome switch
        {
            LoadOutcome.Failed => MetricOutcome.Failed,
            LoadOutcome.Cancelled => MetricOutcome.Cancelled,
            _ => MetricOutcome.Loaded,
        };
        metrics.Record(new MetricObservation(MetricName.ScreenLoadDuration, load.Took.TotalMilliseconds, outcome, area));
    }

    /// <summary>
    /// Watches Core's requests. The observer is always attached, but records
    /// nothing unless usage is shared; it never touches the request, so a
    /// failing measurement can't affect one.
    /// </summary>
    public static void Observe(LibreNmsTransport transport, IAppMetrics metrics) =>
        transport.RequestObserver = request =>
        {
            try
            {
                Request(metrics, request);
            }
            catch (Exception)
            {
                // A measurement never fails a request.
            }
        };

    internal static void Request(IAppMetrics metrics, ApiRequestMeasurement request)
    {
        if (!metrics.IsEnabled || !Enum.IsDefined(request.Operation) || !Enum.IsDefined(request.Outcome) || request.Duration < TimeSpan.Zero)
        {
            return;
        }

        // On LibreNMS 26.10 the alert poll is faults, which Core files as
        // Other for now (DashyNMS/core#75).
        var area = request.Operation switch
        {
            ApiOperation.DeviceList => MetricArea.ApiDevices,
            ApiOperation.Alerts => MetricArea.ApiAlerts,
            ApiOperation.Sensors => MetricArea.ApiSensors,
            _ => MetricArea.ApiOther,
        };
        var outcome = request.Outcome switch
        {
            ApiRequestOutcome.Success => MetricOutcome.Success,
            ApiRequestOutcome.ClientError => MetricOutcome.ClientError,
            ApiRequestOutcome.ServerError => MetricOutcome.ServerError,
            ApiRequestOutcome.Timeout => MetricOutcome.Timeout,
            ApiRequestOutcome.NetworkFailure => MetricOutcome.NetworkFailure,
            ApiRequestOutcome.Cancelled => MetricOutcome.Cancelled,
            _ => MetricOutcome.Failed,
        };
        metrics.Record(new MetricObservation(MetricName.ApiRequests, 1, outcome, area));
        metrics.Record(new MetricObservation(MetricName.ApiDuration, request.Duration.TotalMilliseconds, outcome, area));
    }
}
