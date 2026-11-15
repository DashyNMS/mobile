using DashyNMS.Mobile.Intelligence;

namespace DashyNMS.Mobile.Tests;

public sealed class IntelligenceRateTests
{
    [Theory]
    [InlineData(0.001, "0.001")]
    [InlineData(0.0, "0")]
    [InlineData(null, "unknown")]
    public void Rates_keep_small_measurements_distinct_from_zero(double? value, string expected)
        => Assert.Equal(expected, CompactNetworkLookups.Number(value));
}
