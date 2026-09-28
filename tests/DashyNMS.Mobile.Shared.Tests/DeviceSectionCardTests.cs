using DashyNMS.Mobile.DeviceSections;

namespace DashyNMS.Mobile.Tests;

/// <summary>Device View sections as cards with a quick view (#43), hidden when empty as on desktop (#44).</summary>
public sealed class DeviceSectionCardTests
{
    private static DeviceSectionCard Card(DeviceSection section) => new(DeviceSectionInfo.For(section));

    private static SectionGroup Group(params SectionRow[] rows) => new("Group", rows);

    [Fact]
    public void Summarises_the_count_and_what_needs_attention_with_the_worst_rows_first()
    {
        var card = Card(DeviceSection.Ports);

        card.Show([Group(
            new SectionRow("Gi0/1") { Value = "Up", Status = RowStatus.Ok },
            new SectionRow("Gi0/2") { Value = "Down", Status = RowStatus.Critical },
            new SectionRow("Gi0/3") { Value = "Up", Status = RowStatus.Ok },
            new SectionRow("Gi0/4") { Value = "Errors", Status = RowStatus.Warning },
            new SectionRow("Gi0/5") { Value = "Up", Status = RowStatus.Ok })]);

        Assert.Equal("5 ports · 1 critical · 1 warning", card.SummaryText);
        Assert.Equal(["Gi0/2", "Gi0/4", "Gi0/1"], card.Highlights.Select(r => r.Title));
        Assert.Equal(RowStatus.Critical, card.Status);
        Assert.True(card.NeedsAttention);
        Assert.True(card.ShowSummary);
    }

    [Fact]
    public void One_of_something_is_singular()
    {
        var card = Card(DeviceSection.Arp);

        card.Show([Group(new SectionRow("10.0.0.1"))]);

        Assert.Equal("1 ARP entry", card.SummaryText);
        Assert.False(card.NeedsAttention);
    }

    [Theory]
    [InlineData(DeviceSection.Wireless)]
    [InlineData(DeviceSection.Vlans)]
    [InlineData(DeviceSection.Routing)]
    public void An_empty_section_desktop_hides_goes_once_loaded_but_shows_while_loading(DeviceSection section)
    {
        var card = Card(section);
        card.IsLoading = true;
        Assert.True(card.IsVisible);

        card.Show([]);
        card.IsLoading = false;

        Assert.False(card.IsVisible);
    }

    [Theory]
    [InlineData(DeviceSection.Availability)]
    [InlineData(DeviceSection.Sensors)]
    [InlineData(DeviceSection.EventLog)]
    public void Sections_desktop_always_shows_stay_when_empty(DeviceSection section)
    {
        var card = Card(section);

        card.Show([]);

        Assert.True(card.IsVisible);
        Assert.StartsWith("No ", card.SummaryText);
    }

    [Fact]
    public void A_section_that_fails_to_load_stays_and_says_so()
    {
        var card = Card(DeviceSection.Wireless);

        card.Failed();

        Assert.True(card.IsVisible);
        Assert.Contains("Couldn't load", card.SummaryText);
    }

    [Fact]
    public void Graphs_and_Graylog_have_no_quick_view_and_show_their_description()
    {
        var graphs = Card(DeviceSection.Graphs);
        var graylog = new DeviceSectionCard(DeviceSectionInfo.Graylog);

        Assert.False(graphs.HasQuickView);
        Assert.False(graylog.HasQuickView);
        Assert.False(graphs.ShowSummary);
        Assert.True(graphs.IsVisible);
    }
}
