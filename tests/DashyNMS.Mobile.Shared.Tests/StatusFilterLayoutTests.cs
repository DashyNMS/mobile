using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed class StatusFilterLayoutTests
{
    [Theory]
    [InlineData(637, 951, 1.0, StatusFilterMode.SingleRow)]   // Duo inner portrait
    [InlineData(574, 466, 1.0, StatusFilterMode.SingleRow)]   // Duo outer landscape
    [InlineData(370, 874, 1.0, StatusFilterMode.TwoRows)]     // iPhone 18 Pro portrait
    [InlineData(362, 678, 1.0, StatusFilterMode.TwoRows)]     // Duo outer portrait
    [InlineData(308, 1180, 1.0, StatusFilterMode.TwoRows)]    // iPad portrait split list pane
    [InlineData(368, 820, 1.0, StatusFilterMode.TwoRows)]     // iPad landscape split list pane
    [InlineData(308, 669, 1.0, StatusFilterMode.TwoRows)]     // Duo inner landscape split list pane
    [InlineData(370, 874, 1.65, StatusFilterMode.CompactPicker)] // AX1 accessibility text on iPhone
    [InlineData(637, 951, 2.35, StatusFilterMode.CompactPicker)] // AX3 accessibility text on Duo inner
    [InlineData(308, 1180, 1.65, StatusFilterMode.CompactPicker)] // AX1 accessibility text on iPad split
    [InlineData(308, 450, 1.0, StatusFilterMode.CompactPicker)]  // Short narrow pane
    [InlineData(220, 800, 1.0, StatusFilterMode.CompactPicker)]  // Very narrow pane
    public void Resolve_picks_mode_from_pane_geometry_and_text_scale(
        double width,
        double height,
        double fontScale,
        StatusFilterMode expected)
    {
        Assert.Equal(expected, StatusFilterLayout.Resolve(width, height, fontScale));
    }

    [Fact]
    public void Resolve_falls_back_to_two_rows_before_first_measure()
    {
        Assert.Equal(StatusFilterMode.TwoRows, StatusFilterLayout.Resolve(0, 0, 1.0));
        Assert.Equal(StatusFilterMode.TwoRows, StatusFilterLayout.Resolve(double.NaN, double.NaN, 1.0));
    }

    [Fact]
    public void SummarizeSelection_describes_all_none_single_and_multiple()
    {
        Assert.Equal("All", StatusFilterLayout.SummarizeSelection([("Critical", true), ("Warning", true), ("OK", true), ("Acknowledged", true)]));
        Assert.Equal("None", StatusFilterLayout.SummarizeSelection([("Critical", false), ("Warning", false), ("OK", false), ("Acknowledged", false)]));
        Assert.Equal("Disabled only", StatusFilterLayout.SummarizeSelection([("Up", false), ("Down", false), ("Maintenance", false), ("Disabled", true)]));
        Assert.Equal("Critical, Warning", StatusFilterLayout.SummarizeSelection([("Critical", true), ("Warning", true), ("OK", false), ("Acknowledged", false)]));
        Assert.Equal("Acknowledged only", StatusFilterLayout.SummarizeSelection([("Critical", false), ("Acknowledged", true)], "Acknowledged only"));
    }

    [Fact]
    public void ChipDescription_includes_spoken_included_or_excluded_state()
    {
        Assert.Equal("OK 0, included", StatusFilterLayout.ChipDescription("OK 0", true));
        Assert.Equal("Acknowledged 2, excluded", StatusFilterLayout.ChipDescription("Acknowledged 2", false));
        Assert.Equal("Included", StatusFilterLayout.ChipStateText(true));
        Assert.Equal("Excluded", StatusFilterLayout.ChipStateText(false));
    }

    [Fact]
    public async Task Alerts_acknowledged_shortcut_keeps_status_separate_from_search_and_clears_to_defaults()
    {
        var client = Fakes.Client(alerts:
        [
            Fakes.Alert(1, 1, "critical"),
            Fakes.Alert(2, 1, "warning"),
            Fakes.Alert(3, 2, "critical", acknowledged: true),
        ]);
        var vm = new AlertsViewModel(
            client,
            Fakes.Settings(new AppSettings()),
            Substitute.For<IDialogService>(),
            new RecordingNavigation(),
            Substitute.For<ISelfActionTracker>(),
            Substitute.For<IShareService>(),
            new RecordingBadge(),
            new FakeTimeProvider());
        await vm.RefreshCommand.ExecuteAsync(null);

        vm.ShowOnly("acknowledged");

        Assert.Equal([3], vm.Alerts.Select(a => a.Id));
        Assert.Equal(string.Empty, vm.SearchText);
        Assert.Equal("Acknowledged only", vm.StatusSummaryText);
        Assert.True(vm.HasStatusFilter);
        Assert.True(vm.HasActiveFilters);
        Assert.False(vm.ShowCritical);
        Assert.False(vm.ShowWarning);
        Assert.False(vm.ShowOk);
        Assert.True(vm.ShowAcknowledged);

        vm.ClearFiltersCommand.Execute(null);

        Assert.Equal([1, 2, 3], vm.Alerts.Select(a => a.Id));
        Assert.Equal("All", vm.StatusSummaryText);
        Assert.False(vm.HasStatusFilter);
        Assert.False(vm.HasActiveFilters);
    }

    [Fact]
    public async Task Devices_status_summary_tracks_toggles_shortcuts_and_clear()
    {
        var settings = Fakes.Settings(new AppSettings());
        var client = Fakes.Client(devices:
        [
            Fakes.Device(1, "core-sw"),
            Fakes.Device(2, "edge-fw", up: false),
            Fakes.Device(3, "lab-sw", disabled: true),
        ]);
        var vm = new DevicesViewModel(client, new RecordingNavigation(), settings, new DeviceBookmarks(settings, TimeProvider.System), TimeProvider.System);
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.Extras;

        Assert.Equal("All", vm.StatusSummaryText);
        Assert.False(vm.HasStatusFilter);

        vm.ToggleDisabledCommand.Execute(null);
        Assert.Equal("Up, Down, Maintenance", vm.StatusSummaryText);
        Assert.True(vm.HasStatusFilter);

        vm.ShowOnlyState(DeviceState.Disabled);
        Assert.Equal("Disabled only", vm.StatusSummaryText);
        Assert.Equal([3], vm.Devices.Select(d => d.DeviceId));

        vm.ClearFiltersCommand.Execute(null);
        Assert.Equal("All", vm.StatusSummaryText);
        Assert.False(vm.HasStatusFilter);
        Assert.Equal(3, vm.Devices.Count);
    }
}
