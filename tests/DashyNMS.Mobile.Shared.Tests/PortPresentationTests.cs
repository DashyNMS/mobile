using DashyNMS.Mobile.DeviceSections;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class PortPresentationTests
{
    [Theory]
    [InlineData("up", "up", false, RowStatus.Ok, "Up")]
    [InlineData("up", "down", true, RowStatus.Ok, "Up")]
    [InlineData("down", "up", false, RowStatus.Warning, "Down")]
    [InlineData("down", "down", false, RowStatus.Inactive, "Disabled")]
    [InlineData("down", "up", true, RowStatus.Inactive, "Disabled")]
    [InlineData(null, null, false, RowStatus.None, "Unknown")]
    public void Display_palette_preserves_source_classification(string? oper, string? admin, bool disabled, RowStatus visual, string label)
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfOperStatus = oper, IfAdminStatus = admin,
            Disabled = disabled, IfInOctetsRate = 0, IfOutOctetsRate = 0, IfInErrorsRate = 0, IfOutErrorsRate = 0 });
        Assert.Equal(visual, row.PortVisualStatus);
        Assert.Equal(label, row.PortVisualLabel);
        Assert.Contains(row.PortDisplayFacts, f => f.Name == "Monitoring disabled" && f.Value == (disabled ? "Yes" : "No"));
        if (oper == "up") Assert.Equal("Up", row.Port!.StatusName);
        if (label == "Disabled") Assert.Equal("Shut down", row.Port!.StatusName);
    }

    [Fact]
    public void Errors_override_up_with_partial_missing_readings_and_actual_graph_identity()
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfDescr = "Synthetic interface", IfName = "Gi0/7",
            IfOperStatus = "up", IfInErrorsRate = 2, IfOutErrorsRate = null, IfInOctetsRate = 10 });
        Assert.Equal(RowStatus.Critical, row.PortVisualStatus);
        Assert.Equal(RowStatus.Warning, row.Status);
        Assert.Equal("Errors · link up", row.PortVisualLabel);
        Assert.Equal("Gi0/7", row.LinkPortIfName);
        Assert.Contains(row.PortDisplayFacts, f => f.Name == "Traffic in" && f.Value == "80 b/s");
        Assert.Contains(row.PortDisplayFacts, f => f.Name == "Errors out" && f.Value == "Unavailable");
        Assert.False(row.Matches("Administrative state"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.MaxValue)]
    public void Incomplete_or_overflowing_traffic_never_claims_green(double? rate)
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfOperStatus = "up", IfInOctetsRate = rate,
            IfOutOctetsRate = 0, IfInErrorsRate = 0, IfOutErrorsRate = 0 });
        Assert.Equal(RowStatus.None, row.PortVisualStatus);
        Assert.Contains(row.PortDisplayFacts, f => f.Name == "Traffic in" && f.Value == "Unavailable");
        Assert.Contains(row.PortDisplayFacts, f => f.Name == "Errors in" && f.Value == "0 errors/s");
        Assert.Contains("ifName not reported", row.PortActionDescription);
    }

    [Fact]
    public void Small_positive_errors_do_not_round_to_a_reported_zero()
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfOperStatus = "up", IfInErrorsRate = 0.001 });
        Assert.Equal(RowStatus.Critical, row.PortVisualStatus);
        Assert.DoesNotContain(row.PortDisplayFacts, f => f.Name == "Errors in" && f.Value == "0 errors/s");
    }

    [Theory]
    [InlineData(308, 308, false, 2, 308)]
    [InlineData(636, 308, true, 4, 624)]
    [InlineData(1000, 308, true, 6, 940)]
    [InlineData(200, 308, true, 2, 200)]
    [InlineData(200, 0, true, 2, 200)]
    public void Portrait_tracks_and_landscape_fitting_stay_within_live_pane(double width, double portrait, bool landscape, int count, double stage)
    {
        var fit = PortColumns.Fit(width, portrait, landscape, 8);
        Assert.Equal(count, fit.Count);
        Assert.Equal(stage, fit.Width);
        Assert.True(fit.Width <= width);
    }
}
