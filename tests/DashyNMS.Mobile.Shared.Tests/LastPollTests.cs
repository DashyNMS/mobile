using System.Text.Json;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class LastPollTests
{
    private static Device With(string json)
    {
        var device = Fakes.Device(1, "core-sw-01");
        device.AdditionalData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        return device;
    }

    [Fact]
    public void Reads_the_poll_time_and_its_length_as_LibreNMS_sends_them()
    {
        var device = With("""{ "last_polled": "2026-10-01 09:41:07", "last_polled_timetaken": 4.234 }""");

        Assert.Equal(new DateTime(2026, 10, 1, 9, 41, 7), LastPoll.At(device));
        Assert.Equal(4.234, LastPoll.Seconds(device));
        Assert.Equal("took 4.2s", LastPoll.TookText(device));
    }

    [Fact]
    public void Takes_the_length_as_a_string_too()
    {
        var device = With("""{ "last_polled_timetaken": "61.8" }""");

        Assert.Equal(61.8, LastPoll.Seconds(device));
        Assert.Equal("took 62s", LastPoll.TookText(device));
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "last_polled": null, "last_polled_timetaken": null }""")]
    [InlineData("""{ "last_polled": "", "last_polled_timetaken": "" }""")]
    [InlineData("""{ "last_polled": "yesterday-ish", "last_polled_timetaken": "quick" }""")]
    [InlineData("""{ "last_polled": 1727775667, "last_polled_timetaken": -1 }""")]
    public void Missing_or_unreadable_values_are_nothing(string json)
    {
        var device = With(json);

        Assert.Null(LastPoll.At(device));
        Assert.Null(LastPoll.Seconds(device));
        Assert.Null(LastPoll.TookText(device));
    }

    [Fact]
    public void No_extra_fields_at_all_is_nothing()
    {
        var device = Fakes.Device(1, "core-sw-01");
        device.AdditionalData = null;

        Assert.Null(LastPoll.At(device));
        Assert.Null(LastPoll.Seconds(device));
    }
}
