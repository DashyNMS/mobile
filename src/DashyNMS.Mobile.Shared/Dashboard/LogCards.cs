using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Dashboard;

/// <summary>
/// What the Event log and Graylog cards (DashyNMS/desktop#202, #203) share
/// with desktop's widgets: how many entries, and Graylog's time ranges. The
/// options live on the widget, so either app shows the same entries.
/// </summary>
public static class LogCards
{
    /// <summary>Desktop's count choices; another saved value shows as its default.</summary>
    public static IReadOnlyList<int> CountChoices { get; } = [10, 25, 50];

    public const int DefaultCount = 10;

    /// <summary>Desktop's Graylog widget's ranges, in seconds.</summary>
    public static IReadOnlyList<(int Seconds, string Label)> RangeChoices { get; } =
    [
        (300, "5 min"),
        (900, "15 min"),
        (3600, "60 min"),
    ];

    public const int DefaultRangeSeconds = 900;

    public const string AllTypes = "All types";

    public const string AllStreams = "All streams";

    public const string NotSetUp = "Set up Graylog in Settings first.";

    public static int CountOf(DashboardWidget widget) =>
        CountChoices.Contains(widget.LogCount) ? widget.LogCount : DefaultCount;

    public static int RangeOf(DashboardWidget widget) =>
        RangeChoices.Any(r => r.Seconds == widget.GraylogRangeSeconds) ? widget.GraylogRangeSeconds : DefaultRangeSeconds;

    public static string RangeLabel(DashboardWidget widget) => RangeChoices.First(r => r.Seconds == RangeOf(widget)).Label;

    /// <summary>Nothing to show: desktop's words, saying whether the filters are why.</summary>
    public static string EventsEmptyText(DashboardWidget widget) =>
        string.IsNullOrWhiteSpace(widget.LogType) && string.IsNullOrWhiteSpace(widget.LogSearch) ? "No events yet." : "No events match.";

    public static string GraylogEmptyText(DashboardWidget widget) => $"No messages in the last {RangeLabel(widget)}.";
}
