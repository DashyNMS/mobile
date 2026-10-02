using DashyNMS.Mobile.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

/// <summary>The log Settings shares (#107).</summary>
public sealed class DiagnosticsLogTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"dashynms-diagnostics-{Guid.NewGuid():N}.log");
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 2, 9, 30, 0, TimeSpan.Zero));

    public DiagnosticsLogTests() => _time.SetLocalTimeZone(TimeZoneInfo.Utc);

    public void Dispose() => File.Delete(_path);

    [Fact]
    public void Notes_and_the_apps_own_log_messages_go_in_with_the_time()
    {
        var log = new DiagnosticsLog(_path, _time, "1.1.0 (150)");
        log.Note("Page", "Alerts appeared");
        log.CreateLogger("DashyNMS.Mobile.Alerts.AlertWatcher").LogInformation("Alert check: {Changes} change(s)", 2);

        Assert.Equal(
        [
            "2026-10-02 09:30:00.000 App: Started - DashyNMS mobile 1.1.0 (150)",
            "2026-10-02 09:30:00.000 Page: Alerts appeared",
            "2026-10-02 09:30:00.000 AlertWatcher: Alert check: 2 change(s)",
        ], log.Lines);
    }

    [Fact]
    public void Other_libraries_only_warnings_and_errors_with_the_exception()
    {
        var log = new DiagnosticsLog(null, _time);
        var maui = log.CreateLogger("Microsoft.Maui.Controls.Shell");
        maui.LogInformation("Navigating");
        maui.LogWarning("Layout cycle");
        log.CreateLogger("DesktopNMS.Core.Api.LibreNmsClient").LogError(new HttpRequestException("timed out"), "Request failed");

        Assert.Equal(
        [
            "2026-10-02 09:30:00.000 Shell [warning]: Layout cycle",
            "2026-10-02 09:30:00.000 LibreNmsClient [error]: Request failed - HttpRequestException: timed out",
        ], log.Lines.Skip(1));
    }

    [Fact]
    public void Kept_across_a_restart_since_restarting_is_the_cure()
    {
        new DiagnosticsLog(_path, _time).Note("Page", "Dashboard looks blank");

        var after = new DiagnosticsLog(_path, _time);

        Assert.Contains(after.Lines, l => l.EndsWith("Page: Dashboard looks blank", StringComparison.Ordinal));
        Assert.Equal(3, after.Lines.Count); // two starts and the note
        Assert.EndsWith("\n", after.Text);
    }

    [Fact]
    public void Only_the_newest_lines_are_kept_in_memory_and_the_file()
    {
        var log = new DiagnosticsLog(_path, _time);
        for (var i = 0; i < DiagnosticsLog.MaxLines * 2 + 10; i++)
        {
            log.Note("Test", $"line {i}\nwith a second line");
        }

        Assert.Equal(DiagnosticsLog.MaxLines, log.Lines.Count);
        Assert.EndsWith($"line {DiagnosticsLog.MaxLines * 2 + 9} | with a second line", log.Lines[^1]);
        Assert.True(File.ReadAllLines(_path).Length <= DiagnosticsLog.MaxLines * 2);
        Assert.Equal(DiagnosticsLog.MaxLines, new DiagnosticsLog(_path, _time).Lines.Count);
    }
}
