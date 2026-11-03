using DashyNMS.Mobile.Dashboard;

namespace DashyNMS.Mobile.Tests;

public sealed class CatalogueColumnsTests
{
    [Theory]
    [InlineData(159, 1, 1)]
    [InlineData(329, 1, 1)]
    [InlineData(330, 1, 2)]
    [InlineData(500, 1, 3)]
    [InlineData(670, 1, 4)]
    [InlineData(1200, 1, 4)]
    [InlineData(670, 2, 2)]
    [InlineData(330, 1.5, 1)]
    [InlineData(490, 1.5, 2)]
    [InlineData(0, 1, 1)]
    [InlineData(double.NaN, 1, 1)]
    [InlineData(double.PositiveInfinity, 1, 1)]
    public void Choices_fit_scaled_minimum_width_and_gaps(double width, double scale, int expected)
        => Assert.Equal(expected, CatalogueColumns.Count(width, scale));
}
