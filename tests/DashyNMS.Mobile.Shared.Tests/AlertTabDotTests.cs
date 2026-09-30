using DashyNMS.Mobile.Alerts;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class AlertTabDotTests
{
    private static readonly Alert[] Open =
    [
        Fakes.Alert(1, 1, "warning"),
        Fakes.Alert(2, 1, "critical", acknowledged: true),
        Fakes.Alert(3, 2, "ok"),
    ];

    [Fact]
    public void Orange_for_a_warning_with_acknowledged_alerts_left_out()
    {
        var dot = new AlertTabDot();

        dot.Update(Open, new AppSettings { ShowAlertTabBadge = true, AlertTabBadgeIncludesAcknowledged = false });

        Assert.Equal(AlertTabSeverity.Warning, dot.Severity); // the acknowledged critical doesn't count (#90)
        Assert.Equal("1 warning", dot.Description);
    }

    [Fact]
    public void Red_once_acknowledged_ones_count()
    {
        var dot = new AlertTabDot();

        dot.Update(Open, new AppSettings { ShowAlertTabBadge = true, AlertTabBadgeIncludesAcknowledged = true });

        Assert.Equal(AlertTabSeverity.Critical, dot.Severity);
        Assert.Equal("1 critical, 1 warning", dot.Description);
    }

    [Fact]
    public void None_with_the_tab_badge_off_nothing_open_or_signed_out_and_changes_are_announced()
    {
        var dot = new AlertTabDot();
        var changes = 0;
        dot.Changed += (_, _) => changes++;

        dot.Update(Open, new AppSettings { ShowAlertTabBadge = false });
        Assert.Equal(AlertTabSeverity.None, dot.Severity);
        Assert.Equal(0, changes); // nothing changed

        dot.Update(Open, new AppSettings { ShowAlertTabBadge = true });
        Assert.Equal(1, changes);

        dot.Update([Fakes.Alert(3, 2, "ok")], new AppSettings { ShowAlertTabBadge = true });
        Assert.Equal(AlertTabSeverity.None, dot.Severity); // OK severity doesn't count

        dot.Update(Open, new AppSettings { ShowAlertTabBadge = true });
        dot.Clear();
        Assert.Equal(AlertTabSeverity.None, dot.Severity);
        Assert.Equal(string.Empty, dot.Description);
    }
}
