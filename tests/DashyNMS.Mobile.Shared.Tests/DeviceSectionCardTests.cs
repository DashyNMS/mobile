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
        var card = Card(DeviceSection.Wireless);

        card.Show([Group(
            new SectionRow("AP 1") { Value = "Up", Status = RowStatus.Ok },
            new SectionRow("AP 2") { Value = "Down", Status = RowStatus.Critical },
            new SectionRow("AP 3") { Value = "Up", Status = RowStatus.Ok },
            new SectionRow("AP 4") { Value = "Noisy", Status = RowStatus.Warning },
            new SectionRow("AP 5") { Value = "Up", Status = RowStatus.Ok })]);

        Assert.Equal("5 wireless readings · 1 critical · 1 warning", card.SummaryText);
        Assert.Equal(["AP 2", "AP 4", "AP 1"], card.Highlights.Select(r => r.Title));
        Assert.Equal(RowStatus.Critical, card.Status);
        Assert.True(card.NeedsAttention);
        Assert.True(card.ShowSummary);
    }

    [Fact]
    public void Ports_show_desktops_busiest_five_and_how_many_are_up()
    {
        var card = Card(DeviceSection.Ports);

        card.Show([
            new SectionGroup("Down", [new SectionRow("Gi0/9") { Status = RowStatus.Critical, SortValue = 0 }]),
            new SectionGroup("Up", Enumerable.Range(1, 7).Select(i => new SectionRow($"Gi0/{i}") { SortValue = i * 100 })),
        ]);

        Assert.Equal("Busiest ports", card.Title);
        Assert.Equal("7 of 8 ports up · 1 down", card.SummaryText);
        Assert.Equal(["Gi0/7", "Gi0/6", "Gi0/5", "Gi0/4", "Gi0/3"], card.Highlights.Select(r => r.Title));
    }

    [Fact]
    public void Availability_shows_every_window_in_order_and_the_outages()
    {
        var card = Card(DeviceSection.Availability);

        card.Show([
            new SectionGroup("Availability",
            [
                new SectionRow("Last 24 hours") { Value = "100%", SortValue = 86400 },
                new SectionRow("Last 30 days") { Value = "99.5%", Status = RowStatus.Warning, SortValue = DeviceSectionCard.ThirtyDays },
            ]),
            new SectionGroup("Outages", [new SectionRow("Down 1 Sep 10:00")]),
        ]);

        Assert.Equal(["Last 24 hours", "Last 30 days"], card.Highlights.Select(r => r.Title));
        Assert.Equal("1 recent outage", card.SummaryText);
        Assert.Equal("99.5%", DeviceSectionCard.ThirtyDayWindow(card.Groups)!.Value);
    }

    [Fact]
    public void Resources_show_the_first_cpu_and_memory_and_the_fullest_disk()
    {
        var card = Card(DeviceSection.Resources);

        card.Show([
            new SectionGroup("Processors", [new SectionRow("CPU 0") { Bar = 0.2 }, new SectionRow("CPU 1") { Bar = 0.3 }]),
            new SectionGroup("Memory", [new SectionRow("RAM") { Bar = 0.5 }]),
            new SectionGroup("Storage", [new SectionRow("/boot") { Bar = 0.9 }, new SectionRow("/") { Bar = 0.4 }]),
        ]);

        Assert.Equal(["CPU 0", "RAM", "/boot"], card.Highlights.Select(r => r.Title));
    }

    [Fact]
    public void Neighbours_are_connected_to_with_how_many_LibreNMS_knows()
    {
        var card = Card(DeviceSection.Neighbours);

        card.Show([Group(
            new SectionRow("core-sw") { LinkDeviceId = 1 },
            new SectionRow("phone-42"),
            new SectionRow("edge-rtr") { LinkDeviceId = 2 })]);

        Assert.Equal("Connected to", card.Title);
        Assert.Equal("3 neighbours · 2 in LibreNMS", card.SummaryText);
    }

    [Fact]
    public async Task The_device_page_lays_out_desktops_overview_and_fills_its_tiles()
    {
        var client = Fakes.Client(devices: [Fakes.Device(7, "core-sw", ip: "192.0.2.1")]);
        client.Devices.GetAsync("7", Arg.Any<CancellationToken>()).Returns(Fakes.Device(7, "core-sw", ip: "192.0.2.1"));
        client.Devices.GetAvailabilityAsync(7, Arg.Any<CancellationToken>()).Returns(
        [
            new DesktopNMS.Core.Models.AvailabilityWindow { DurationSeconds = 86400, Percent = 100 },
            new DesktopNMS.Core.Models.AvailabilityWindow { DurationSeconds = 2592000, Percent = 99.5 },
        ]);
        client.Devices.GetOutagesAsync(7, Arg.Any<CancellationToken>()).Returns(Array.Empty<DesktopNMS.Core.Models.DeviceOutage>());
        client.Ports.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns(
        [
            new DesktopNMS.Core.Models.Port { PortId = 1, IfName = "Gi0/1", IfOperStatus = "up", IfInOctetsRate = 1000 },
            new DesktopNMS.Core.Models.Port { PortId = 2, IfName = "Gi0/2", IfOperStatus = "down" },
        ]);
        var settings = Fakes.Settings();
        var vm = new DashyNMS.Mobile.ViewModels.DeviceDetailViewModel(
            client, settings, Substitute.For<DashyNMS.Mobile.Services.ILauncherService>(),
            new DashyNMS.Mobile.Services.DeviceBookmarks(settings, TimeProvider.System),
            Substitute.For<DashyNMS.Mobile.Services.IDialogService>(), new RecordingNavigation(),
            sections: new DeviceSectionLoader(client, settings));

        await vm.LoadAsync(7);
        await vm.SectionsLoaded;

        // Desktop's overview order, before the rest.
        var order = vm.Sections.Select(s => s.Section).ToList();
        Assert.True(order.IndexOf(DeviceSection.Availability) < order.IndexOf(DeviceSection.Ports));
        Assert.Equal(DeviceSection.Availability, order[0]);

        Assert.Equal("99.5%", vm.AvailabilityTileText);
        Assert.Equal(RowStatus.Warning, vm.AvailabilityTileStatus);
        Assert.Equal("1 / 2", vm.PortsTileText);
        Assert.Equal(RowStatus.Warning, vm.PortsTileStatus);
        Assert.Equal(RowStatus.Ok, vm.AlertsTileStatus);
        Assert.Contains(vm.Properties, p => p is { Key: "IP address", Value: "192.0.2.1" });
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
