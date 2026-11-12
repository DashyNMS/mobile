using DashyNMS.Mobile.Intelligence;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed class CompactNetworkLookupsTests
{
    private static ISessionService Session()
    {
        var session = Substitute.For<ISessionService>();
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.net/"), "test-token"));
        return session;
    }

    [Fact]
    public async Task Signed_out_lookups_ask_for_sign_in_without_calling_LibreNMS()
    {
        var client = Fakes.Client();
        var session = Substitute.For<ISessionService>();
        var lookups = new CompactNetworkLookups(client, Fakes.Settings(), session, deviceTransport: Fakes.DeviceTransport(client)).Create();
        foreach (var lookup in lookups)
            Assert.Equal("Sign in to LibreNMS to use network lookups.", await lookup.Run("1", default));
        await client.Devices.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
        await client.Alerts.DidNotReceive().ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>());
        await client.Sensors.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Each_lookup_returns_identified_data_within_the_budget()
    {
        var devices = Enumerable.Range(1, 5000).Select(i => Fakes.Device(i, $"switch-{i}.example.net")).ToList();
        var alerts = Enumerable.Range(1, 3000).Select(i => Fakes.Alert(i, 1, "critical")).ToList();
        var client = Fakes.Client(devices, alerts);
        client.Devices.GetAsync("1", Arg.Any<CancellationToken>()).Returns(devices[0]);
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(Enumerable.Range(1, 2000)
            .Select(i => new Sensor { SensorId = i, DeviceId = 1, Description = new string('x', 500), Current = 20, SensorClass = "temperature" }).ToList());
        var lookups = new CompactNetworkLookups(client, Fakes.Settings(), Session(), deviceTransport: Fakes.DeviceTransport(client)).Create();

        Assert.Equal(5, lookups.Count);
        foreach (var lookup in lookups)
        {
            var query = lookup.Name == "find_device" ? "switch" : lookup.Name == "network_overview" ? "" : "1";
            var text = await lookup.Run(query, default);
            Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
            Assert.NotEqual(CompactNetworkLookups.Failed, text);
            Assert.Contains(lookup.Name == "device_sensors" ? "[[sensor:1]]" : lookup.Name is "active_alerts" or "network_overview" ? "[[alert:1]]" : "[[device:1]]", text);
        }
    }

    [Fact]
    public async Task Overview_keeps_warning_details_ahead_of_ok_rows_when_space_is_limited()
    {
        var alerts = Enumerable.Range(1, 100).Select(i => Fakes.Alert(i, 1, "ok"))
            .Append(Fakes.Alert(101, 1, "warning")).ToList();
        var lookup = new CompactNetworkLookups(Fakes.Client(alerts: alerts), Fakes.Settings(), Session())
            .Create().Single(l => l.Name == "network_overview");
        var text = await lookup.Run("", default);
        Assert.Contains("(alert:101)", text);
        Assert.True(text.IndexOf("(alert:101)", StringComparison.Ordinal) < text.IndexOf("(alert:1)", StringComparison.Ordinal));
        Assert.Contains("more results omitted", text);
    }

    [Fact]
    public async Task Unacknowledged_warning_precedes_acknowledged_critical()
    {
        var client = Fakes.Client(alerts: [Fakes.Alert(1, 1, "critical", acknowledged: true), Fakes.Alert(2, 1, "warning")]);
        var lookups = new CompactNetworkLookups(client, Fakes.Settings(), Session(), deviceTransport: Fakes.DeviceTransport(client)).Create();
        foreach (var lookup in lookups.Where(l => l.Name is "network_overview" or "active_alerts"))
        {
            var text = await lookup.Run("", default);
            Assert.True(text.IndexOf("alert:2", StringComparison.Ordinal) < text.IndexOf("alert:1", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData(AlertSource.Alert, "alert:7")]
    [InlineData(AlertSource.Fault, "fault:7")]
    public async Task Alert_results_keep_the_source_qualified_identity(AlertSource source, string key)
    {
        var alert = Fakes.Alert(7, 1, "critical");
        alert.Source = source;
        var lookups = new CompactNetworkLookups(Fakes.Client(alerts: [alert]), Fakes.Settings(), Session()).Create();
        foreach (var lookup in lookups.Where(l => l.Name is "network_overview" or "active_alerts"))
        {
            var text = await lookup.Run("", default);
            // The marker links any source; the qualified key keeps alert 7 and fault 7 distinct.
            Assert.True(text.Contains($"[[alert:7]] ({key}) on host1", StringComparison.Ordinal), text);
            Assert.Equal(alert.Identity, DashyNMS.Mobile.Alerts.AlertTargets.ParseKey(key));
        }
    }

    [Fact]
    public async Task Failures_do_not_expose_exception_details()
    {
        var client = Fakes.Client();
        client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<Device>>(new HttpRequestException("private-server secret-token")));
        var lookup = new CompactNetworkLookups(client, Fakes.Settings(), Session(), deviceTransport: Fakes.DeviceTransport(client)).Create().Single(l => l.Name == "find_device");
        Assert.Equal(CompactNetworkLookups.Failed, await lookup.Run("switch", default));
    }

    [Fact]
    public async Task A_timeout_bounds_a_transport_which_ignores_cancellation()
    {
        var time = new FakeTimeProvider();
        var client = Fakes.Client();
        client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<IReadOnlyList<Device>>().Task);
        var lookup = new CompactNetworkLookups(client, Fakes.Settings(), Session(), time, deviceTransport: Fakes.DeviceTransport(client)).Create().Single(l => l.Name == "find_device");
        var pending = lookup.Run("switch", default);
        time.Advance(CompactNetworkLookups.Timeout);
        Assert.Equal(CompactNetworkLookups.Failed, await pending);
    }

    [Fact]
    public async Task Cancellation_is_not_reported_as_a_lookup_failure()
    {
        var client = Fakes.Client();
        client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(new TaskCompletionSource<IReadOnlyList<Device>>().Task);
        var lookup = new CompactNetworkLookups(client, Fakes.Settings(), Session(), deviceTransport: Fakes.DeviceTransport(client)).Create().Single(l => l.Name == "find_device");
        using var stopping = new CancellationTokenSource();
        var pending = lookup.Run("switch", stopping.Token);
        stopping.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task A_connection_switch_discards_old_data_and_requires_a_fresh_lookup_set()
    {
        var client = Fakes.Client();
        var response = new TaskCompletionSource<IReadOnlyList<Device>>();
        client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(response.Task);
        var session = Session();
        var provider = new CompactNetworkLookups(client, Fakes.Settings(), session, deviceTransport: Fakes.DeviceTransport(client));
        var lookup = provider.Create().Single(l => l.Name == "find_device");
        var pending = lookup.Run("switch", default);
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://new.example.net/"), "new-test-token"));
        response.SetResult([Fakes.Device(1, "old-switch")]);
        Assert.Equal(CompactNetworkLookups.Changed, await pending);
        Assert.Equal(CompactNetworkLookups.Changed, await lookup.Run("switch", default));
        client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(new[] { Fakes.Device(2, "new-switch") });
        var fresh = provider.Create().Single(l => l.Name == "find_device");
        Assert.Contains("new-switch", await fresh.Run("switch", default));
    }

    [Theory]
    [InlineData("device_summary")]
    [InlineData("device_sensors")]
    [InlineData("active_alerts")]
    public async Task Invalid_device_ids_do_not_make_requests(string name)
    {
        var client = Fakes.Client();
        var lookup = new CompactNetworkLookups(client, Fakes.Settings(), Session(), deviceTransport: Fakes.DeviceTransport(client)).Create().Single(l => l.Name == name);
        Assert.Contains("numeric device id", await lookup.Run("-1", default));
        await client.Devices.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
        await client.Alerts.DidNotReceive().ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>());
        await client.Sensors.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Alerts_are_named_from_one_device_list_request_and_report_their_evidence()
    {
        var devices = new[] { Fakes.Device(708, "192.0.2.8", sysName: "core-sw1") };
        var fault = Fakes.Alert(3, 708, "critical");
        fault.Source = AlertSource.Fault;
        var client = Fakes.Client(devices, [fault, Fakes.Alert(4, 708, "warning")]);
        var evidence = new List<IntelligenceEvidence>();
        var lookup = new CompactNetworkLookups(client, Fakes.Settings(), Session(), deviceTransport: Fakes.DeviceTransport(client)).Create(evidence.AddRange).Single(l => l.Name == "active_alerts");

        var text = await lookup.Run("", default);

        Assert.Contains("Rule 3 [[alert:3]] (fault:3) on core-sw1 [[device:708]]", text);
        Assert.DoesNotContain("device 708", text, StringComparison.OrdinalIgnoreCase);
        await client.Devices.Received(1).ListAsync(Arg.Any<CancellationToken>());
        var alert = Assert.Single(evidence, r => r.Reference.Key == "alert:3").Reference;
        Assert.Equal(("Rule 3", "Critical", "critical"), (alert.Name, alert.Status, alert.Tone));
        Assert.Equal(new DesktopNMS.Core.Intelligence.IntelligenceObjectReference("fault", 3, 708, 30), alert.Object);
        var device = Assert.Single(evidence, r => r.Reference.Key == "device:708").Reference;
        Assert.Equal(("core-sw1", "Up"), (device.Name, device.Status));
        Assert.Equal(new DesktopNMS.Core.Intelligence.IntelligenceObjectReference("device", 708), device.Object);
    }

    [Fact]
    public async Task Missing_names_say_so_and_unreadable_device_lists_fall_back_to_the_alert_hostname()
    {
        var unnamed = Fakes.Alert(5, 9, "warning");
        unnamed.Hostname = null;
        var client = Fakes.Client(alerts: [unnamed, Fakes.Alert(6, 10, "warning")]);
        client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<IReadOnlyList<Device>>(new HttpRequestException("private")));
        var evidence = new List<IntelligenceEvidence>();
        var lookup = new CompactNetworkLookups(client, Fakes.Settings(), Session(), deviceTransport: Fakes.DeviceTransport(client)).Create(evidence.AddRange).Single(l => l.Name == "active_alerts");

        var text = await lookup.Run("", default);

        Assert.Contains("(alert:5) on Device 9 (name unavailable)", text);
        Assert.Contains("(alert:6) on host10", text);
        // Without a device row there is no verified device to link.
        Assert.DoesNotContain("[[device:", text);
        Assert.DoesNotContain(evidence, r => r.Reference.Key.StartsWith("device:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Evidence_covers_only_rows_the_model_was_shown()
    {
        var devices = Enumerable.Range(1, 400).Select(i => Fakes.Device(i, $"switch-{i}.example.net", up: false)).ToList();
        var evidence = new List<IntelligenceEvidence>();
        var lookup = new CompactNetworkLookups(Fakes.Client(devices), Fakes.Settings(), Session()).Create(evidence.AddRange).Single(l => l.Name == "network_overview");

        var text = await lookup.Run("", default);

        Assert.Contains("more results omitted", text);
        Assert.NotEmpty(evidence);
        Assert.All(evidence, r => Assert.Contains("[[" + r.Reference.Key + "]]", text));
        Assert.DoesNotContain(evidence, r => r.Reference.Key == "device:400");
    }

    [Fact]
    public async Task A_reply_from_a_previous_connection_reports_no_evidence()
    {
        var client = Fakes.Client();
        var response = new TaskCompletionSource<IReadOnlyList<Device>>();
        client.Devices.ListAsync(Arg.Any<CancellationToken>()).Returns(response.Task);
        var session = Session();
        var evidence = new List<IntelligenceEvidence>();
        var lookup = new CompactNetworkLookups(client, Fakes.Settings(), session, deviceTransport: Fakes.DeviceTransport(client)).Create(evidence.AddRange).Single(l => l.Name == "find_device");
        var pending = lookup.Run("switch", default);
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://new.example.net/"), "new-test-token"));
        response.SetResult([Fakes.Device(1, "old-switch")]);
        Assert.Equal(CompactNetworkLookups.Changed, await pending);
        Assert.Empty(evidence);
    }

    [Fact]
    public async Task Sensors_and_locations_carry_navigable_references_without_a_health_claim()
    {
        var device = Fakes.Device(12, "edge.example.net", sysName: "edge-router-a", location: "Example lab");
        device.LocationId = 4;
        var client = Fakes.Client([device]);
        client.Devices.GetAsync("12", Arg.Any<CancellationToken>()).Returns(device);
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(new[] { new Sensor { SensorId = 31, DeviceId = 12, Description = "Inlet temperature", Current = 41, SensorClass = "temperature", Unit = "C" } });
        var evidence = new List<IntelligenceEvidence>();
        var lookups = new CompactNetworkLookups(client, Fakes.Settings(), Session(), deviceTransport: Fakes.DeviceTransport(client)).Create(evidence.AddRange);

        var sensors = await lookups.Single(l => l.Name == "device_sensors").Run("12", default);
        var summary = await lookups.Single(l => l.Name == "device_summary").Run("12", default);

        Assert.StartsWith("Sensors on edge-router-a [[device:12]]: 1.", sensors);
        Assert.Contains("Inlet temperature [[sensor:31]]: temperature; reading 41; unit C", sensors);
        Assert.Contains("location: Example lab [[location:4]]", summary);
        var sensor = Assert.Single(evidence, r => r.Reference.Key == "sensor:31").Reference;
        Assert.Equal("", sensor.Status);
        Assert.Equal(new DesktopNMS.Core.Intelligence.IntelligenceObjectReference("sensor", 31, 12), sensor.Object);
        Assert.Equal(new DesktopNMS.Core.Intelligence.IntelligenceObjectReference("location", 4), Assert.Single(evidence, r => r.Reference.Key == "location:4").Reference.Object);
    }

    [Fact]
    public async Task A_long_or_sanitised_location_shows_a_safe_label_but_keeps_its_exact_filter_value()
    {
        // Synthetic: longer than the 80-character label limit, with a control character and marker brackets.
        var original = "Example campus\tbuilding [[device:1]] " + new string('x', 90);
        var device = Fakes.Device(12, "edge.example.net", sysName: "edge-router-a", location: original);
        device.LocationId = 4;
        var client = Fakes.Client([device]);
        client.Devices.GetAsync("12", Arg.Any<CancellationToken>()).Returns(device);
        var evidence = new List<IntelligenceEvidence>();
        var summary = await new CompactNetworkLookups(client, Fakes.Settings(), Session(), deviceTransport: Fakes.DeviceTransport(client)).Create(evidence.AddRange)
            .Single(l => l.Name == "device_summary").Run("12", default);

        var location = Assert.Single(evidence, r => r.Reference.Key == "location:4");
        Assert.Equal(original, location.Filter);
        Assert.Equal(CompactNetworkLookups.Clean(original), location.Reference.Name);
        Assert.True(location.Reference.Name.Length <= 80);
        Assert.DoesNotContain("[[device:1]]", location.Reference.Name);
        Assert.DoesNotContain("\t", location.Reference.Name);
        Assert.DoesNotContain("[[device:1]]", summary);
        Assert.DoesNotContain(original, summary);
        // Only locations need a filter value; other kinds route by id.
        Assert.All(evidence.Where(r => r.Reference.Key != "location:4"), r => Assert.Null(r.Filter));
    }

    [Fact]
    public async Task Network_labels_cannot_open_or_close_a_marker()
    {
        var client = Fakes.Client([Fakes.Device(2, "spoof [[device:1]] ]]")]);
        var text = await new CompactNetworkLookups(client, Fakes.Settings(), Session(), deviceTransport: Fakes.DeviceTransport(client)).Create().Single(l => l.Name == "find_device").Run("spoof", default);
        Assert.DoesNotContain("[[device:1]]", text);
        Assert.Contains("[[device:2]]", text);
    }
}
