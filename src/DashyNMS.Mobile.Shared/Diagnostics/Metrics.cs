namespace DashyNMS.Mobile.Diagnostics;

/// <summary>
/// What may be measured (#218), as desktop measures it (DashyNMS/desktop#498
/// to #501). Every dimension is one of these fixed lists - there are no
/// caller-supplied names, tags or values - so nothing identifying can be
/// measured by mistake.
/// </summary>
public enum MetricName
{
    /// <summary>A launch to the screen, once per process: counted.</summary>
    AppStart,

    /// <summary>Launch to the first usable page: sampled.</summary>
    StartupDuration,

    /// <summary>A page's first load: sampled, with its outcome.</summary>
    ScreenLoadDuration,

    /// <summary>LibreNMS requests after their retries: counted, by operation and outcome.</summary>
    ApiRequests,

    /// <summary>How long those took: sampled.</summary>
    ApiDuration,

    /// <summary>A feature opened or used (#219): counted, by feature.</summary>
    FeatureUse,

    /// <summary>The main thread late by half a second or more, on screen (#219): counted.</summary>
    UiStalls,

    /// <summary>How late it was: sampled.</summary>
    UiStallDuration,

    /// <summary>The app's memory as the OS counts it (#219) - iOS's footprint, Android's PSS - in bytes.</summary>
    MemoryFootprint,

    /// <summary>The .NET heap, in bytes.</summary>
    ManagedHeap,

    /// <summary>How a foreground session ended (#219): counted, by outcome.</summary>
    SessionOutcome,

    /// <summary>How long a session that ended on its own lasted: sampled.</summary>
    SessionDuration,
}

public enum MetricOutcome
{
    Success,
    Loaded,
    Cached,
    Failed,
    Cancelled,
    ClientError,
    ServerError,
    Timeout,
    NetworkFailure,
    Healthy,
    Errored,
    Crashed,
    Unknown,
}

/// <summary>The page measured, or for a request the kind of request.</summary>
public enum MetricArea
{
    Application,
    Dashboard,
    Alerts,
    Devices,
    Health,
    DeviceView,
    Logs,
    Graylog,
    Map,
    NetworkMap,
    Neighbours,
    ApiDevices,
    ApiAlerts,
    ApiSensors,
    ApiOther,

    /// <summary>Not on the phone yet (#236): listed so the counts match desktop's when it is.</summary>
    Intelligence,
    DashboardEditor,
    CardAdded,
    CsvExport,
}

public sealed record MetricObservation(MetricName Name, double Value, MetricOutcome Outcome = MetricOutcome.Success, MetricArea Area = MetricArea.Application);

public sealed record MetricPoint(DateTimeOffset Timestamp, MetricObservation Observation);

/// <summary>A minute's measurements, queued and sent like an error report but only while usage is shared.</summary>
public sealed record MetricsReport(int Schema, Guid EventId, DateTimeOffset Timestamp,
    ErrorReportContext Context, IReadOnlyList<MetricPoint> Points)
    : DiagnosticReport(Schema, EventId, Timestamp, Context);

/// <summary>Where measurements are recorded (#218, #219).</summary>
public interface IAppMetrics
{
    /// <summary>Usage is shared and the app is on screen, so a measurement would be kept.</summary>
    bool IsEnabled { get; }

    void Record(MetricObservation observation);
}

public static class MetricRules
{
    /// <summary>The most points one report carries.</summary>
    public const int MaximumPoints = 32;

    public static bool IsCounter(MetricName name) => name is MetricName.AppStart or MetricName.ApiRequests
        or MetricName.FeatureUse or MetricName.UiStalls or MetricName.SessionOutcome;

    /// <summary>A reading rather than a count or a time: kept as it is, not sampled.</summary>
    public static bool IsGauge(MetricName name) => name is MetricName.MemoryFootprint or MetricName.ManagedHeap;

    /// <summary>
    /// Within the fixed lists, finite, and in range: a count is a whole number
    /// up to a million, memory at most 1 TiB, a session at most 90 days and
    /// any other duration at most a day.
    /// </summary>
    public static bool IsValid(MetricObservation? observation) =>
        observation is not null
        && Enum.IsDefined(observation.Name) && Enum.IsDefined(observation.Outcome) && Enum.IsDefined(observation.Area)
        && double.IsFinite(observation.Value) && observation.Value >= 0
        && (IsCounter(observation.Name)
            ? observation.Value is > 0 and <= 1_000_000 && observation.Value == Math.Truncate(observation.Value)
            : observation.Value <= observation.Name switch
            {
                MetricName.MemoryFootprint or MetricName.ManagedHeap => 1_099_511_627_776,
                MetricName.SessionDuration => 7_776_000_000,
                _ => 86_400_000,
            });

    /// <summary>The page a view model is, if it's one of the pages measured; otherwise none.</summary>
    public static MetricArea? PageArea(string viewModel) => viewModel switch
    {
        "DashboardViewModel" => MetricArea.Dashboard,
        "AlertsViewModel" => MetricArea.Alerts,
        "DevicesViewModel" => MetricArea.Devices,
        "HealthViewModel" => MetricArea.Health,
        "DeviceDetailViewModel" => MetricArea.DeviceView,
        "LogsViewModel" => MetricArea.Logs,
        "GraylogViewModel" => MetricArea.Graylog,
        "MapViewModel" => MetricArea.Map,
        "NetworkMapViewModel" => MetricArea.NetworkMap,
        "NeighboursViewModel" => MetricArea.Neighbours,
        _ => null,
    };

    /// <summary>The feature a page is (#219), counted each time it's opened rather than returned to.</summary>
    public static MetricArea? FeatureArea(string viewModel) => viewModel switch
    {
        "MapViewModel" => MetricArea.Map,
        "NetworkMapViewModel" => MetricArea.NetworkMap,
        "NeighboursViewModel" => MetricArea.Neighbours,
        "LogsViewModel" => MetricArea.Logs,
        "GraylogViewModel" => MetricArea.Graylog,
        "CustomiseDashboardViewModel" => MetricArea.DashboardEditor,
        _ => null,
    };
}

/// <summary>
/// Features used outside a page opening (#219): a card added, a CSV export.
/// The app head counts them when usage is shared; the fixed area is all that's
/// passed - never which card, what was exported or where.
/// </summary>
public static class FeatureUse
{
    public static event EventHandler<MetricArea>? Used;

    internal static void Count(MetricArea area) => Used?.Invoke(null, area);
}
