using System.Net;
using System.Text.Json;
using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DashyNMS.Mobile.Widgets;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Json;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Tests;

public sealed class ResourceVisibilityTests
{
    private const string DeniedSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><foreignObject><div>No Authorization</div></foreignObject></svg>";
    private const string ValidSvg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1000\" height=\"400\"><path d=\"M 1 1 L 2 2\"/></svg>";

    [Fact]
    public void Missing_device_flags_and_uptime_are_unknown_and_not_counted_as_up_down_or_ok()
    {
        var device = JsonSerializer.Deserialize<Device>("""{"device_id":7,"hostname":"example.net"}""", LibreNmsJson.Options)!;
        var row = new DeviceItem(device);
        Assert.Equal(DeviceState.Unknown, row.State);
        Assert.Equal("Unknown", row.StateText);
        Assert.Equal("Unknown", row.UptimeText);
        var snapshot = WidgetSnapshot.Build([], DateTimeOffset.UtcNow, new(), [device]);
        Assert.Equal((0, 0, 0, 0, 1), (snapshot.Devices!.Up, snapshot.Devices.Down, snapshot.Devices.Disabled, snapshot.Devices.Ok, snapshot.Devices.Unknown));
    }

    [Fact]
    public async Task A_resource_specific_bulk_refusal_does_not_skip_the_next_device()
    {
        var result = await BulkRun.RunAsync(new[] { 7, 8 }, id => $"Device {id}", id => id == 7
            ? Task.FromException(new LibreNmsApiException("Denied", HttpStatusCode.Forbidden, "Insufficient permissions to update this device"))
            : Task.CompletedTask);
        Assert.Equal([8], result.Succeeded);
        Assert.Single(result.Failures);
        Assert.Equal(0, result.NotTried);
        Assert.Null(result.Refusal);
    }

    [Fact]
    public void A_denied_section_remains_visible_and_is_not_presented_as_empty()
    {
        var card = new DeviceSectionCard(DeviceSectionInfo.For(DeviceSection.Fdb));
        card.Failed(new LibreNmsApiException("Denied", HttpStatusCode.Forbidden));
        Assert.True(card.IsVisible);
        Assert.Contains("isn't allowed", card.SummaryText);
        Assert.Equal(DesktopNMS.Core.Api.SourceOutcome.Denied, VisibleTotals.Classify(null, new LibreNmsApiException("Denied", HttpStatusCode.Forbidden)));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, SourceOutcome.Denied)]
    [InlineData(HttpStatusCode.ServiceUnavailable, SourceOutcome.Failed)]
    public async Task Section_denials_and_failures_do_not_show_an_empty_state(HttpStatusCode status, SourceOutcome expected)
    {
        var client = Fakes.Client();
        client.Fdb.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns<IReadOnlyList<FdbEntry>>(_ => throw new LibreNmsApiException("Cannot read", status));
        client.Ports.ListForDeviceAsync(7, Arg.Any<CancellationToken>()).Returns(Array.Empty<Port>());
        var vm = new DeviceSectionViewModel(new DeviceSectionLoader(client, Fakes.Settings()), new RecordingNavigation());
        await vm.LoadAsync(7, DeviceSection.Fdb);
        Assert.True(vm.HasError);
        Assert.Equal(expected, vm.FailureOutcome);
        Assert.False(vm.IsEmpty);
        Assert.DoesNotContain("No fdb", vm.EmptyText);
    }

    [Fact]
    public void Graph_error_images_are_denied_before_they_become_web_pages()
    {
        var error = Assert.Throws<LibreNmsApiException>(() => GraphHtml.Build(DeniedSvg, false));
        Assert.True(error.IsPermissionDenied);
    }

    [Fact]
    public async Task A_denied_graph_does_not_replace_another_devices_graph_or_poison_its_cache()
    {
        var client = Fakes.Client();
        foreach (var id in new[] { 7, 8 })
        {
            client.Graphs.ListAsync(id, Arg.Any<CancellationToken>()).Returns([new GraphType { Name = "device_mystery", Description = "Mystery" }]);
            client.Graphs.ListHealthAsync(id, Arg.Any<CancellationToken>()).Returns(Array.Empty<GraphType>());
            client.Graphs.ListWirelessAsync(id, Arg.Any<CancellationToken>()).Returns(Array.Empty<GraphType>());
        }
        client.Graphs.GetSvgAsync(7, Arg.Any<string>(), Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), true).Returns(ValidSvg);
        client.Graphs.GetSvgAsync(8, Arg.Any<string>(), Arg.Any<GraphTimeRange>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>(), true).Returns(DeniedSvg);
        var vm = new DeviceGraphsViewModel(client, Fakes.Settings());
        await vm.LoadAsync(7);
        Assert.NotNull(vm.GraphPage);
        await vm.LoadAsync(8);
        Assert.Null(vm.GraphPage);
        Assert.Equal(SourceOutcome.Denied, vm.FailureOutcome);
        await vm.LoadAsync(7);
        Assert.NotNull(vm.GraphPage);
        Assert.False(vm.HasError);
    }

    [Fact]
    public void Maintenance_refusal_is_scoped_to_the_selected_device()
    {
        var client = Fakes.Client();
        var permissions = new ApiPermissions();
        permissions.NoteServerVersion(new Version(26, 10, 0));
        permissions.Learn(HttpMethod.Post, "devices/7/maintenance", new LibreNmsApiException("Denied", HttpStatusCode.Forbidden, "Insufficient permissions to update this device"));
        client.Permissions.Returns(permissions);
        var vm = new MaintenanceViewModel(client, Substitute.For<IDialogService>(), new RecordingNavigation(), TimeProvider.System);
        vm.Initialize(7, "Device 7");
        Assert.False(vm.MaySchedule);
        vm.Initialize(8, "Device 8");
        Assert.True(vm.MaySchedule);
        Assert.Null(vm.RefusedText);
    }

    [Fact]
    public async Task A_denied_alert_list_has_no_zero_total_or_false_empty_message_and_can_recover()
    {
        var client = Fakes.Client();
        client.Alerts.ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>()).Returns<IReadOnlyList<Alert>>(_ => throw new LibreNmsApiException("Denied", HttpStatusCode.Forbidden));
        var vm = new AlertsViewModel(client, Fakes.Settings(), Substitute.For<IDialogService>(), new RecordingNavigation(), Substitute.For<ISelfActionTracker>(), Substitute.For<IShareService>(), new RecordingBadge(), TimeProvider.System);
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.Equal(SourceOutcome.Denied, vm.FailureOutcome);
        Assert.False(vm.IsEmpty);
        Assert.Contains("isn't allowed", vm.CountText);
        Assert.Contains("isn't allowed", vm.EmptyText);
        client.Alerts.ListAsync(Arg.Any<AlertQuery?>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<Alert>());
        await vm.RefreshCommand.ExecuteAsync(null);
        Assert.True(vm.IsEmpty);
        Assert.Null(vm.FailureOutcome);
        Assert.Contains("visible to this account", vm.CountText);
    }
    [Fact]
    public async Task A_write_refusal_does_not_claim_the_loaded_alert_list_is_denied()
    {
        var client = Fakes.Client(alerts: [Fakes.Alert(1, 7, "critical")]);
        client.Alerts.AcknowledgeAsync(Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new LibreNmsApiException("Denied", HttpStatusCode.Forbidden)));
        var dialogs = Substitute.For<IDialogService>();
        dialogs.PromptAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(string.Empty);
        var vm = new AlertsViewModel(client, Fakes.Settings(), dialogs, new RecordingNavigation(), Substitute.For<ISelfActionTracker>(), Substitute.For<IShareService>(), new RecordingBadge(), TimeProvider.System);
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.AcknowledgeCommand.ExecuteAsync(vm.Alerts[0]);
        Assert.True(vm.HasError);
        Assert.Null(vm.FailureOutcome);
        Assert.Equal("1 alert visible to this account", vm.CountText);
    }

}
