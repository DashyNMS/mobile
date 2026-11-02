using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using NSubstitute.ExceptionExtensions;

namespace DashyNMS.Mobile.Tests;

public sealed class SensorDraftTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Leaving_without_save_discards_edits_and_does_not_create_a_card(bool existing)
    {
        var app = new AppSettings();
        var settings = Fakes.Settings(app);
        var card = existing ? DashboardLayout.Add(app, DashboardLayout.Sensors) : null;
        var client = Fakes.Client(devices: [Fakes.Device(1, "switch-1.example.net")]);
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
            [new Sensor { SensorId = 10, DeviceId = 1, Description = "Sensor 1" }]);
        var vm = new SensorPickerViewModel(client, settings, new RecordingNavigation()) { WidgetId = card?.Id };
        await vm.LoadCommand.ExecuteAsync(null);
        vm.CardTitle = "Unsaved title";
        vm.SearchText = "Sensor";
        vm.ToggleCommand.Execute(Assert.Single(vm.Sensors));

        // Back leaves the draft behind. A fresh page must read only persisted settings.
        var reopened = new SensorPickerViewModel(client, settings, new RecordingNavigation()) { WidgetId = card?.Id };
        await reopened.LoadCommand.ExecuteAsync(null);
        Assert.Equal(string.Empty, reopened.CardTitle);
        Assert.Equal(0, reopened.PickedCount);
        Assert.Equal(existing ? 1 : 0, app.DashboardWidgets.Count);
        settings.DidNotReceive().Save();
    }

    [Fact]
    public async Task Empty_selection_is_saved_once_and_returns_to_the_caller()
    {
        var app = new AppSettings();
        var settings = Fakes.Settings(app);
        var navigation = new RecordingNavigation();
        var client = Fakes.Client();
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        var vm = new SensorPickerViewModel(client, settings, navigation);
        Assert.False(vm.SaveCommand.CanExecute(null));
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.True(vm.SaveCommand.CanExecute(null));
        vm.CardTitle = "  Empty card  ";
        await vm.SaveCommand.ExecuteAsync(null);
        var card = Assert.Single(app.DashboardWidgets);
        Assert.Equal("Empty card", card.Title);
        Assert.Empty(card.Sensors);
        settings.Received(1).Save();
        Assert.Equal(Routes.Back, Assert.Single(navigation.Visits).Route);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_save_restores_settings_and_keeps_the_draft_for_retry(bool existing)
    {
        var app = new AppSettings();
        var settings = Fakes.Settings(app);
        var card = existing ? DashboardLayout.Add(app, DashboardLayout.Sensors) : null;
        var client = Fakes.Client();
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        var navigation = new RecordingNavigation();
        var vm = new SensorPickerViewModel(client, settings, navigation) { WidgetId = card?.Id };
        await vm.LoadCommand.ExecuteAsync(null);
        vm.CardTitle = "Draft title";
        var fail = true;
        settings.When(s => s.Save()).Do(_ => { if (fail) throw new IOException("Could not save settings"); });
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.True(vm.HasError);
        Assert.Empty(navigation.Visits);
        Assert.Equal(existing ? 1 : 0, app.DashboardWidgets.Count);
        if (card is not null) Assert.Equal("Sensors", card.Title);
        Assert.Equal("Draft title", vm.CardTitle);
        fail = false;
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.False(vm.HasError);
        Assert.Equal("Draft title", Assert.Single(app.DashboardWidgets).Title);
        Assert.Single(navigation.Visits);
    }

    [Fact]
    public async Task Load_failure_blocks_save_and_retry_preserves_the_typed_title()
    {
        var settings = Fakes.Settings();
        var client = Fakes.Client();
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new HttpRequestException());
        var vm = new SensorPickerViewModel(client, settings, new RecordingNavigation());
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.True(vm.HasError);
        Assert.False(vm.SaveCommand.CanExecute(null));
        vm.CardTitle = "Keep this draft";
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.False(vm.HasError);
        Assert.True(vm.SaveCommand.CanExecute(null));
        Assert.Equal("Keep this draft", vm.CardTitle);
        Assert.Empty(settings.Current.DashboardWidgets);
        settings.DidNotReceive().Save();
    }

    [Fact]
    public async Task Natural_search_order_and_retry_keep_selection_including_unavailable_saved_sensors()
    {
        var app = new AppSettings();
        var card = DashboardLayout.Add(app, DashboardLayout.Sensors);
        card.Sensors.Add(new PinnedSensor { SensorId = 99, DeviceId = 99, Description = "Unavailable sensor" });
        var settings = Fakes.Settings(app);
        var client = Fakes.Client(devices: [Fakes.Device(1, "switch-2.example.net"), Fakes.Device(2, "switch-10.example.net")]);
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Sensor { SensorId = 30, DeviceId = 2, Description = "Sensor 1" },
            new Sensor { SensorId = 20, DeviceId = 1, Description = "Sensor 10" },
            new Sensor { SensorId = 10, DeviceId = 1, Description = "Sensor 2" },
        ]);
        var vm = new SensorPickerViewModel(client, settings, new RecordingNavigation()) { WidgetId = card.Id };
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SearchText = "Sensor";
        Assert.Equal([10, 20, 30], vm.Sensors.Select(s => s.Sensor.SensorId));
        vm.ToggleCommand.Execute(vm.Sensors[0]);
        Assert.Equal(2, vm.PickedCount);
        Assert.Contains("selected", vm.Sensors[0].SelectionDescription);
        await vm.LoadCommand.ExecuteAsync(null);
        Assert.True(vm.Sensors[0].IsPicked);
        await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal([99, 10], card.Sensors.Select(s => s.SensorId));
        settings.Received(1).Save();
    }

    [Fact]
    public async Task Toggling_search_results_keeps_the_list_and_unticking_drops_only_that_selected_row()
    {
        var app = new AppSettings();
        var settings = Fakes.Settings(app);
        var client = Fakes.Client(devices: [Fakes.Device(1, "switch-1.example.net")]);
        client.Sensors.ListAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new Sensor { SensorId = 10, DeviceId = 1, Description = "Sensor 1" },
            new Sensor { SensorId = 20, DeviceId = 1, Description = "Sensor 2" },
        ]);
        var vm = new SensorPickerViewModel(client, settings, new RecordingNavigation());
        await vm.LoadCommand.ExecuteAsync(null);
        vm.SearchText = "Sensor";
        var shown = vm.Sensors.ToList();
        var resets = 0;
        vm.Sensors.CollectionChanged += (_, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Reset) resets++;
        };

        vm.ToggleCommand.Execute(shown[0]);
        vm.ToggleCommand.Execute(shown[1]);
        Assert.Equal(shown, vm.Sensors); // the same rows, so the list keeps its scroll position
        Assert.Equal(0, resets);
        Assert.Equal("2 selected", vm.SelectedText);
        Assert.EndsWith(", selected", shown[0].SelectionDescription);

        vm.SearchText = string.Empty;
        Assert.Equal("Selected for this card. Search to add more.", vm.HintText);
        resets = 0;
        vm.ToggleCommand.Execute(shown[0]);
        Assert.Equal([20], vm.Sensors.Select(s => s.Sensor.SensorId));
        Assert.Equal(0, resets);
        Assert.EndsWith(", not selected", shown[0].SelectionDescription);
        settings.DidNotReceive().Save();
    }
}
