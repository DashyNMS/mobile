using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Tests;

public sealed class NaturalOrderTests
{
    [Fact]
    public void Numbers_go_by_value_not_by_character()
    {
        string[] names = ["Hall 10", "Hall 2", "Hall 1", "Hall 02b", "Hall 2a"];

        Assert.Equal(["Hall 1", "Hall 2", "Hall 2a", "Hall 02b", "Hall 10"], names.Order(NaturalOrder.Instance));
    }

    [Fact]
    public void Case_and_spaces_at_either_end_are_ignored()
    {
        string[] names = ["switches", " Access", "Core ", "BACKBONE"];

        Assert.Equal([" Access", "BACKBONE", "Core ", "switches"], names.Order(NaturalOrder.Instance));
    }

    [Fact]
    public void A_name_comes_before_longer_ones_it_starts_and_ties_are_always_the_same_way_round()
    {
        Assert.True(NaturalOrder.Instance.Compare("Rack", "Rack 3") < 0);
        Assert.True(NaturalOrder.Instance.Compare("rack", "Rack") != 0);
        Assert.Equal(-NaturalOrder.Instance.Compare("rack", "Rack"), NaturalOrder.Instance.Compare("Rack", "rack"));
        Assert.Equal(0, NaturalOrder.Instance.Compare("Rack", "Rack"));
        Assert.True(NaturalOrder.Instance.Compare(null, "Rack") < 0);
    }
}
