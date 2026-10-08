using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class AlertCountThresholdTests
{
    private static readonly Alert[] Open =
    [
        Fakes.Alert(1, 1, "critical"),
        Fakes.Alert(2, 1, "warning"),
        Fakes.Alert(3, 2, "ok"),
        Fakes.Alert(4, 2, "critical", acknowledged: true),
        Fakes.Alert(5, 3, "warning", acknowledged: true),
        Fakes.Alert(6, 3, "something-new"), // a severity LibreNMS may add
    ];

    [Theory]
    [InlineData(AlertSeverity.Ok, false, 4)]       // everything active, as before #96
    [InlineData(AlertSeverity.Warning, false, 2)]
    [InlineData(AlertSeverity.Critical, false, 1)]
    [InlineData(AlertSeverity.Ok, true, 6)]
    [InlineData(AlertSeverity.Warning, true, 4)]
    [InlineData(AlertSeverity.Critical, true, 2)]
    public void The_badge_counts_from_the_threshold_with_or_without_acknowledged(AlertSeverity minimum, bool acknowledged, int expected)
    {
        var settings = new AppSettings { ShowAlertTabBadge = true, AlertTabBadgeIncludesAcknowledged = acknowledged };

        Assert.Equal(expected, AlertBadge.Count(Open, settings, minimum));
    }

    [Fact]
    public void The_badge_turned_off_counts_nothing_whatever_the_threshold() =>
        Assert.Equal(0, AlertBadge.Count(Open, new AppSettings { ShowAlertTabBadge = false }, AlertSeverity.Critical));

    [Fact]
    public void The_tab_dot_goes_by_the_same_threshold()
    {
        var threshold = new AlertCountThreshold(Fakes.Settings(), new InMemoryPreferences()) { Minimum = AlertSeverity.Critical };
        var dot = new AlertTabDot(threshold);
        var settings = new AppSettings { ShowAlertTabBadge = true, AlertTabBadgeIncludesAcknowledged = false };

        dot.Update([Fakes.Alert(2, 1, "warning")], settings);
        Assert.Equal(AlertTabSeverity.None, dot.Severity); // warnings don't count at Critical only

        dot.Update(Open, settings);
        Assert.Equal(AlertTabSeverity.Critical, dot.Severity);
        Assert.Equal("1 critical", dot.Description);
    }

    [Fact]
    public void Defaults_to_everything_and_keeps_the_choice_in_desktops_settings()
    {
        var appSettings = new AppSettings();
        var store = Fakes.Settings(appSettings);
        var threshold = new AlertCountThreshold(store, new InMemoryPreferences());

        Assert.Equal(AlertSeverity.Ok, threshold.Minimum);

        threshold.Minimum = AlertSeverity.Warning;

        Assert.Equal(AlertSeverity.Warning, appSettings.Notifications.CountFrom); // shared with desktop (#168)
        store.Received(1).Save();
        Assert.Equal(AlertSeverity.Warning, new AlertCountThreshold(store, new InMemoryPreferences()).Minimum);
    }

    [Fact]
    public void A_choice_saved_on_the_phone_before_moves_into_the_settings_once()
    {
        var appSettings = new AppSettings();
        var preferences = new InMemoryPreferences();
        preferences.Set(AlertCountThreshold.OldKey, nameof(AlertSeverity.Critical));

        var threshold = new AlertCountThreshold(Fakes.Settings(appSettings), preferences);

        Assert.Equal(AlertSeverity.Critical, threshold.Minimum);
        Assert.Equal(AlertSeverity.Critical, appSettings.Notifications.CountFrom);
        Assert.Null(preferences.Get(AlertCountThreshold.OldKey));
    }

    [Fact]
    public void An_unreadable_old_choice_is_dropped()
    {
        var appSettings = new AppSettings();
        var preferences = new InMemoryPreferences();
        preferences.Set(AlertCountThreshold.OldKey, "Nonsense");

        Assert.Equal(AlertSeverity.Ok, new AlertCountThreshold(Fakes.Settings(appSettings), preferences).Minimum);
        Assert.Null(preferences.Get(AlertCountThreshold.OldKey));
    }

    [Theory]
    [InlineData(AlertSeverity.Ok, "Every alert")]
    [InlineData(AlertSeverity.Warning, "Critical and warning")]
    [InlineData(AlertSeverity.Critical, "Critical only")]
    public void Each_choice_reads_as_on_desktop(AlertSeverity minimum, string label) =>
        Assert.Equal(label, AlertCountThreshold.Describe(minimum));
}
