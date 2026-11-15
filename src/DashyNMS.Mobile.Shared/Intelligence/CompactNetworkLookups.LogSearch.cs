using DashyNMS.Agent.Graylog;
using DashyNMS.Agent.Summaries;

namespace DashyNMS.Mobile.Intelligence;

public sealed partial class CompactNetworkLookups
{
    private static Reply LogSearchRows(SearchLogsResult found, DateTimeOffset at, TimeProvider time, Func<DeviceBrief, string> name)
    {
        var data = found.Data;
        var search = data.Search;
        var heading = $"Graylog matches {Count(data.Count)}. Window {IntelligenceTime.At(search.Since, time)} to {IntelligenceTime.At(search.Until, time)}. "
            + (found.NewestRead ? "Newest samples." : "Oldest samples; newest exceed Graylog paging. Narrow the window for newer messages.");
        var rows = new List<Row>
        {
            new($"Search text {(search.Text is { } text ? Clean(text) : "any")}; level {(search.Level is { } level ? Clean(level) : "any")}; stream {(search.Stream is { } stream ? Clean(stream) : "any")}."),
        };
        if (search.Device is { } device) rows.Add(FleetDevice(device, name, "Graylog device filter", at));
        if (data.WhyNone is { } why) rows.Add(new("No-match context: " + Clean(why)));
        if (data.TopFrom is { } source) rows.Add(new("Top values from " + Clean(source) + "."));
        rows.AddRange((data.Lines ?? []).Select(l => new Row("Log " + (l.Time is { } instant ? IntelligenceTime.AtAndAgo(instant, at, time) : "time unknown")
            + $"; source {Clean(l.Source)}; level {Clean(l.Level)}; facility {Clean(l.Facility)}; application {Clean(l.Application)}; {CleanText(l.Text)}")));
        if (data.Top is { } top)
            foreach (var (label, values) in new[] { ("source", top.Sources), ("level", top.Levels), ("facility", top.Facilities), ("application", top.Applications) })
                rows.AddRange((values ?? []).Select(v => new Row($"Top {label} {Clean(v.Value)}: {Id(v.Count)}.")));
        rows.AddRange((data.OverTime ?? []).Select(b => new Row($"From {IntelligenceTime.At(b.From, time)}: {Count(b.Count)} messages.")));
        return Rows(heading, rows, alsoOmitted: found.LinesOmitted);
    }
}
