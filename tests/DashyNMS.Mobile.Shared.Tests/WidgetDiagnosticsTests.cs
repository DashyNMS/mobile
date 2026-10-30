using DashyNMS.Mobile.Widgets;

namespace DashyNMS.Mobile.Tests;

public sealed class WidgetDiagnosticsTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 18, 0, 0, TimeSpan.Zero);

    private static string SignedIn() => (WidgetSnapshot.SignedOut with
    {
        SignedIn = true,
        Critical = 2,
        Warning = 1,
        CheckedAt = Now.AddMinutes(-3).ToUnixTimeSeconds(),
    }).ToJson();

    [Fact]
    public void Says_what_was_saved_and_what_the_widget_made_of_it()
    {
        var note = $$"""{"readAt":{{Now.AddMinutes(-1).ToUnixTimeSeconds()}},"outcome":"ok","signedIn":true}""";

        var lines = WidgetDiagnostics.Describe(true, SignedIn(), Now.AddMinutes(-3), null, note, Now);

        Assert.Equal(2, lines.Count);
        Assert.Contains("signed in · 3 alerts · checked", lines[0]);
        Assert.Contains("(3m ago)", lines[0]);
        Assert.StartsWith("Widgets: widget read it", lines[1]);
        Assert.EndsWith("· signed in", lines[1]);
    }

    [Fact]
    public void A_widget_that_couldnt_read_it_says_why()
    {
        var note = $$"""{"readAt":{{Now.ToUnixTimeSeconds()}},"outcome":"unreadable","detail":"alerts.Index 0: missing faultCount"}""";

        var lines = WidgetDiagnostics.Describe(true, SignedIn(), Now, "IOException: disk full", note, Now);

        Assert.Equal("Widgets: last save failed - IOException: disk full", lines[1]);
        Assert.EndsWith("- alerts.Index 0: missing faultCount", lines[2]);
    }

    [Fact]
    public void Nothing_saved_no_folder_or_an_old_version_are_each_named()
    {
        Assert.Equal(["Widgets: no App Group folder - this build can't share data with the widgets"],
            WidgetDiagnostics.Describe(false, null, null, null, null, Now));

        var none = WidgetDiagnostics.Describe(true, null, null, null, null, Now);
        Assert.Equal(["Widgets: no snapshot saved yet", "Widgets: the widget hasn't read the snapshot yet"], none);

        var old = WidgetDiagnostics.Describe(true, """{"version":1,"signedIn":true}""", Now, null, null, Now);
        Assert.Contains("version 1 (the widgets read version 2)", old[0]);
    }

    [Fact]
    public void An_unreadable_snapshot_is_as_good_as_none()
    {
        Assert.False(WidgetSnapshot.FromJson("""{"version":2,"signedIn":true,"alerts":{}}""").SignedIn);
        Assert.False(WidgetSnapshot.FromJson("not json").SignedIn);
    }
}
