using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Security;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute.ExceptionExtensions;

namespace DashyNMS.Mobile.Tests;

public sealed class GraylogSetupTests
{
    private readonly AppSettings _appSettings = new();
    private readonly IGraylogApi _api = Substitute.For<IGraylogApi>();
    private readonly SecretCache _secrets = Fakes.Secrets();
    private readonly SecureGraylogPasswordProtector _passwords;
    private readonly GraylogSetup _setup;

    public GraylogSetupTests()
    {
        _passwords = new SecureGraylogPasswordProtector(_secrets);
        _setup = new GraylogSetup(_api, _passwords, Fakes.Settings(_appSettings), _secrets, NullLogger<GraylogSetup>.Instance);
    }

    [Fact]
    public void The_password_is_kept_in_the_keychain_under_its_own_key()
    {
        _passwords.Save("secret");

        Assert.True(_passwords.HasStoredPassword);
        Assert.Equal("secret", _secrets.Get(SecureGraylogPasswordProtector.Key));
        Assert.Null(_secrets.Get(SecureTokenProtector.Key));
        Assert.Contains(SecureGraylogPasswordProtector.Key, ServiceCollectionExtensions.SecretKeys);

        _passwords.Clear();
        Assert.False(_passwords.HasStoredPassword);
    }

    [Fact]
    public async Task Switched_on_with_a_password_connects_on_first_use()
    {
        // Saved on an earlier run: in the keychain before the app starts.
        _appSettings.Graylog = new GraylogSettings { Enabled = true, Server = "graylog.example.com", Username = "admin" };
        var storage = Substitute.For<ISecureStorage>();
        storage.GetAsync(SecureGraylogPasswordProtector.Key).Returns("secret");
        var secrets = new SecretCache(storage, NullLogger<SecretCache>.Instance);
        var setup = new GraylogSetup(_api, new SecureGraylogPasswordProtector(secrets), Fakes.Settings(_appSettings), secrets, NullLogger<GraylogSetup>.Instance);

        await setup.EnsureConfiguredAsync();

        _api.Received(1).Configure(Arg.Is<GraylogConnection>(c =>
            c.Root == new Uri("https://graylog.example.com/") && c.Username == "admin" && c.Password == "secret"));
    }

    [Fact]
    public async Task Switched_off_or_missing_its_password_stays_disconnected()
    {
        // Switched off, with the password in the keychain - so only the switch keeps it disconnected.
        _appSettings.Graylog = new GraylogSettings { Enabled = false, Server = "graylog.example.com", Username = "admin" };
        var storage = Substitute.For<ISecureStorage>();
        storage.GetAsync(SecureGraylogPasswordProtector.Key).Returns("secret");
        var secrets = new SecretCache(storage, NullLogger<SecretCache>.Instance);
        await new GraylogSetup(_api, new SecureGraylogPasswordProtector(secrets), Fakes.Settings(_appSettings), secrets, NullLogger<GraylogSetup>.Instance)
            .EnsureConfiguredAsync();

        var withoutPassword = new GraylogSetup(_api, new SecureGraylogPasswordProtector(Fakes.Secrets()), Fakes.Settings(new AppSettings
        {
            Graylog = new GraylogSettings { Enabled = true, Server = "graylog.example.com", Username = "admin" },
        }), Fakes.Secrets(), NullLogger<GraylogSetup>.Instance);
        await withoutPassword.EnsureConfiguredAsync();

        _api.DidNotReceive().Configure(Arg.Any<GraylogConnection>());
    }

    [Fact]
    public void Forgetting_the_password_disconnects()
    {
        _passwords.Save("secret");

        _setup.ForgetPassword();

        Assert.False(_passwords.HasStoredPassword);
        _api.Received(1).Clear();
    }
}

public sealed class GraylogSettingsTests
{
    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly IGraylogApi _api = Substitute.For<IGraylogApi>();
    private readonly IGraylogConnectionTester _tester = Substitute.For<IGraylogConnectionTester>();
    private readonly SecureGraylogPasswordProtector _passwords = new(Fakes.Secrets());
    private readonly RecordingNavigation _navigation = new();
    private readonly GraylogSettingsViewModel _vm;

    public GraylogSettingsTests()
    {
        _settings = Fakes.Settings(_appSettings);
        var setup = new GraylogSetup(_api, _passwords, _settings, Fakes.Secrets(), NullLogger<GraylogSetup>.Instance);
        _vm = new GraylogSettingsViewModel(_settings, setup, _passwords, _tester, Substitute.For<IDialogService>(), _navigation);
    }

    private void FillIn()
    {
        _vm.Enabled = true;
        _vm.Server = "graylog.example.com";
        _vm.Username = "admin";
        _vm.PasswordInput = "secret";
    }

    [Fact]
    public async Task Saving_keeps_the_settings_and_password_and_connects()
    {
        FillIn();
        _vm.PortText = "9000";

        await _vm.SaveCommand.ExecuteAsync(null);

        Assert.True(_appSettings.Graylog.Enabled);
        Assert.Equal(9000, _appSettings.Graylog.Port);
        Assert.Equal("secret", _passwords.Load());
        _settings.Received(1).Save();
        _api.Received(1).Configure(Arg.Is<GraylogConnection>(c => c.Root == new Uri("https://graylog.example.com:9000/")));
        Assert.Equal(Routes.Back, _navigation.Visits.Single().Route);
    }

    [Fact]
    public void Nothing_changes_until_Save()
    {
        FillIn();

        Assert.False(_appSettings.Graylog.Enabled);
        Assert.Null(_passwords.Load());
        _api.DidNotReceive().Configure(Arg.Any<GraylogConnection>());
    }

    [Fact]
    public async Task Switched_on_but_incomplete_isnt_saved()
    {
        _vm.Enabled = true;
        _vm.Server = "graylog.example.com";

        await _vm.SaveCommand.ExecuteAsync(null);

        Assert.True(_vm.HasError);
        Assert.False(_appSettings.Graylog.Enabled);
        _settings.DidNotReceive().Save();
        Assert.Empty(_navigation.Visits);
    }

    [Fact]
    public async Task A_port_that_isnt_a_number_is_caught()
    {
        FillIn();
        _vm.PortText = "90o0";

        await _vm.SaveCommand.ExecuteAsync(null);

        Assert.Contains("port", _vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        _settings.DidNotReceive().Save();
    }

    [Fact]
    public async Task Test_tries_the_form_and_says_how_many_streams()
    {
        FillIn();
        _tester.TestAsync(Arg.Any<GraylogConnection>(), Arg.Any<CancellationToken>()).Returns(3);

        await _vm.TestCommand.ExecuteAsync(null);

        Assert.True(_vm.TestSucceeded);
        Assert.Equal("Connected to Graylog - 3 streams available.", _vm.TestResultText);
        _api.DidNotReceive().Configure(Arg.Any<GraylogConnection>());
    }

    [Fact]
    public async Task Test_shows_Graylogs_reason_for_a_failure()
    {
        FillIn();
        _tester.TestAsync(Arg.Any<GraylogConnection>(), Arg.Any<CancellationToken>())
            .Returns<int>(_ => throw new GraylogApiException("Graylog rejected the username or password.", HttpStatusCode.Unauthorized));

        await _vm.TestCommand.ExecuteAsync(null);

        Assert.False(_vm.TestSucceeded);
        Assert.Equal("Graylog rejected the username or password. (HTTP 401)", _vm.TestResultText);
    }

    [Fact]
    public async Task The_match_field_can_be_cleared_while_typing_and_saves_as_source()
    {
        FillIn();

        _vm.QueryField = "";
        Assert.Equal("", _vm.QueryField);

        await _vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal(GraylogSettings.DefaultQueryField, _appSettings.Graylog.QueryField);
    }

    [Fact]
    public void The_base_uri_only_shows_for_Other()
    {
        Assert.False(_vm.IsBaseUriVisible);

        _vm.VersionIndex = 2;

        Assert.True(_vm.IsBaseUriVisible);
    }
}

public sealed class GraylogViewModelTests
{
    private readonly AppSettings _appSettings = new();
    private readonly IGraylogApi _api = Substitute.For<IGraylogApi>();
    private readonly RecordingNavigation _navigation = new();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly ILibreNmsClient _client;

    public GraylogViewModelTests()
    {
        _client = Fakes.Client(devices: [Fakes.Device(1, "core-sw-01", ip: "10.0.0.1"), Fakes.Device(2, "edge-rtr", ip: "10.0.0.2")]);
        _api.IsConfigured.Returns(true);
        _api.GetStreamsAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<GraylogStream>());
        _api.SearchAsync(default!, default, default, default, default, default, default).ReturnsForAnyArgs(Result(total: 2, "10.0.0.2", "10.9.9.9"));
    }

    private GraylogViewModel NewViewModel()
    {
        var setup = new GraylogSetup(_api, Substitute.For<IGraylogPasswordProtector>(), Fakes.Settings(_appSettings), Fakes.Secrets(), NullLogger<GraylogSetup>.Instance);
        return new GraylogViewModel(setup, _api, _client, Fakes.Settings(_appSettings), _navigation, _dialogs)
        {
            ResolveHostname = (_, _) => Task.FromResult<string?>(null),
        };
    }

    private static GraylogSearchResult Result(long total, params string[] sources) => new()
    {
        TotalResults = total,
        Messages = sources.Select((source, i) => new GraylogMessageEnvelope
        {
            Message = new GraylogMessage(new Dictionary<string, JsonElement>
            {
                ["_id"] = JsonSerializer.SerializeToElement($"m{i}"),
                ["source"] = JsonSerializer.SerializeToElement(source),
                ["message"] = JsonSerializer.SerializeToElement($"line one\nline two {i}"),
                ["level"] = JsonSerializer.SerializeToElement(3),
                ["timestamp"] = JsonSerializer.SerializeToElement("2026-01-01T12:00:00.000Z"),
            }),
        }).ToList(),
    };

    [Fact]
    public async Task Every_devices_messages_newest_first_with_known_sources_named()
    {
        var vm = NewViewModel();
        vm.Initialise(null, null);

        await vm.EnsureLoadedAsync();

        await _api.Received(1).SearchAsync("*", 0, GraylogViewModel.PageSize, 0, "timestamp:desc", null, Arg.Any<CancellationToken>());
        Assert.Equal(2, vm.Messages.Count);
        Assert.Equal("edge-rtr", vm.Messages[0].SourceText);
        Assert.Equal(2, vm.Messages[0].DeviceId);
        Assert.Equal("10.9.9.9", vm.Messages[1].SourceText);
        Assert.Null(vm.Messages[1].DeviceId);
        Assert.Equal(RowStatus.Critical, vm.Messages[0].Status);
        Assert.Equal("line one line two 0", vm.Messages[0].MessageText);
        Assert.False(vm.CanLoadMore);
    }

    [Fact]
    public async Task A_device_searches_its_own_addresses_from_its_default_level()
    {
        _appSettings.Graylog.DeviceLogLevel = 4;
        var vm = NewViewModel();
        vm.Initialise(1, "core-sw-01");

        await vm.EnsureLoadedAsync();

        await _api.Received(1).SearchAsync(
            "source: (\"core-sw-01\" OR \"10.0.0.1\") AND level: <=4",
            0, GraylogViewModel.PageSize, 0, "timestamp:desc", null, Arg.Any<CancellationToken>());
        Assert.Equal("Graylog · core-sw-01", vm.Title);
    }

    [Fact]
    public async Task Search_text_and_level_go_into_the_query()
    {
        var vm = NewViewModel();
        vm.Initialise(null, null);
        await vm.EnsureLoadedAsync();

        vm.SearchText = "link \"down\"";
        await vm.SearchCommand.ExecuteAsync(null);

        await _api.Received(1).SearchAsync("message:\"link \\\"down\\\"\"", 0, GraylogViewModel.PageSize, 0, "timestamp:desc", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Load_more_asks_for_the_next_page_and_appends_it()
    {
        _api.SearchAsync(default!, default, default, default, default, default, default).ReturnsForAnyArgs(Result(total: 120, "10.0.0.1", "10.0.0.2"));
        var vm = NewViewModel();
        vm.Initialise(null, null);
        await vm.EnsureLoadedAsync();
        Assert.True(vm.CanLoadMore);

        await vm.LoadMoreCommand.ExecuteAsync(null);

        await _api.Received(1).SearchAsync("*", 0, GraylogViewModel.PageSize, 2, "timestamp:desc", null, Arg.Any<CancellationToken>());
        Assert.Equal(4, vm.Messages.Count);
        Assert.Equal("4 of 120 messages", vm.SummaryText);
    }

    [Fact]
    public async Task A_message_opens_on_its_own_page_with_every_field()
    {
        var vm = NewViewModel();
        vm.Initialise(null, null);
        await vm.EnsureLoadedAsync();
        var message = vm.Messages[0];

        await vm.OpenMessageCommand.ExecuteAsync(message);

        var visit = _navigation.Visits.Single();
        Assert.Equal(Routes.GraylogMessage, visit.Route);
        Assert.Same(message, visit.Parameters![Routes.GraylogMessageParameter]);
        Assert.Same(vm, visit.Parameters[Routes.GraylogListParameter]);
        Assert.Contains(message.Fields, f => f.Key == "source" && f.Value == "10.0.0.2");
        Assert.Equal("line one\nline two 0", message.FullText);
        Assert.Equal("(3) Error", message.MetaText);
    }

    [Fact]
    public async Task A_messages_page_leads_to_its_device()
    {
        var vm = NewViewModel();
        vm.Initialise(null, null);
        await vm.EnsureLoadedAsync();
        var page = new GraylogMessageViewModel(_navigation);

        page.Load(vm.Messages[0], vm);
        await page.OpenDeviceCommand.ExecuteAsync(null);

        Assert.True(page.HasDevice);
        var visit = _navigation.Visits.Single();
        Assert.Equal(Routes.DeviceDetail, visit.Route);
        Assert.Equal(2, visit.Parameters![Routes.DeviceIdParameter]);
    }

    [Fact]
    public async Task Show_only_sets_the_Device_chip_to_a_known_device_and_searches_its_addresses()
    {
        var vm = NewViewModel();
        vm.Initialise(null, null);
        await vm.EnsureLoadedAsync();
        var page = new GraylogMessageViewModel(_navigation);
        page.Load(vm.Messages[0], vm);

        Assert.True(page.CanShowOnly);
        Assert.Equal("Show only edge-rtr's messages", page.ShowOnlyText);
        await page.ShowOnlyCommand.ExecuteAsync(null);

        Assert.Equal(GraylogDeviceFilter.ForDevice(2, "edge-rtr"), vm.Device);
        Assert.Equal("edge-rtr", vm.DeviceChipText);
        Assert.False(vm.ShowsSource);
        Assert.True(vm.CanClearDevice);
        Assert.True(vm.HasActiveFilters);
        Assert.False(page.CanShowOnly);
        await _api.Received(1).SearchAsync("source: (\"edge-rtr\" OR \"10.0.0.2\")", 0, GraylogViewModel.PageSize, 0, "timestamp:desc", null, Arg.Any<CancellationToken>());
        Assert.Equal(Routes.Back, _navigation.Visits.Single().Route);
    }

    [Fact]
    public async Task Show_only_a_sender_LibreNMS_doesnt_know_searches_its_one_address()
    {
        var vm = NewViewModel();
        vm.Initialise(null, null);
        await vm.EnsureLoadedAsync();
        var page = new GraylogMessageViewModel(_navigation) { IsBesideList = true };
        page.Load(vm.Messages[1], vm);

        Assert.False(page.HasDevice);
        await page.ShowOnlyCommand.ExecuteAsync(null);

        Assert.Equal(GraylogDeviceFilter.ForAddress("10.9.9.9"), vm.Device);
        await _api.Received(1).SearchAsync("source: (\"10.9.9.9\")", 0, GraylogViewModel.PageSize, 0, "timestamp:desc", null, Arg.Any<CancellationToken>());

        // Beside the list, there's no page to go back from.
        Assert.Empty(_navigation.Visits);
    }

    [Fact]
    public async Task Clearing_the_device_chip_goes_back_to_every_devices_messages()
    {
        var vm = NewViewModel();
        vm.Initialise(null, null);
        await vm.EnsureLoadedAsync();
        vm.ShowDevice(GraylogDeviceFilter.ForDevice(1, "core-sw-01"));

        vm.ClearDeviceCommand.Execute(null);

        Assert.Null(vm.Device);
        Assert.Equal("Device", vm.DeviceChipText);
        Assert.True(vm.ShowsSource);
        await _api.Received(2).SearchAsync("*", 0, GraylogViewModel.PageSize, 0, "timestamp:desc", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_devices_own_Graylog_page_has_its_Device_chip_fixed()
    {
        _appSettings.Graylog.DeviceLogLevel = 4;
        var vm = NewViewModel();
        vm.Initialise(1, "core-sw-01");
        await vm.EnsureLoadedAsync();

        Assert.True(vm.IsDeviceFixed);
        Assert.Equal("core-sw-01", vm.DeviceChipText);
        Assert.False(vm.CanClearDevice);
        Assert.False(vm.IsLevelSet);
        Assert.False(vm.HasActiveFilters);
        Assert.False(vm.CanShowOnly(vm.Messages[0]));

        vm.ShowDevice(GraylogDeviceFilter.ForDevice(2, "edge-rtr"));
        await vm.ChooseDeviceCommand.ExecuteAsync(null);
        vm.LevelIndex = 0;
        vm.ClearFiltersCommand.Execute(null);

        Assert.Equal(1, vm.Device!.DeviceId);
        Assert.Equal(5, vm.LevelIndex);
        Assert.Empty(_navigation.Visits);
    }

    [Fact]
    public async Task The_level_chip_chooses_a_level_and_everything_worse()
    {
        _dialogs.ChooseAsync("Level", Arg.Any<IReadOnlyList<string>>()).Returns("(4) Warning and worse");
        var vm = NewViewModel();
        vm.Initialise(null, null);
        await vm.EnsureLoadedAsync();
        Assert.Equal("Any level", vm.LevelChipText);

        await vm.ChooseLevelCommand.ExecuteAsync(null);

        Assert.Equal(5, vm.LevelIndex);
        Assert.Equal("Warning and worse", vm.LevelChipText);
        Assert.True(vm.IsLevelSet);
        Assert.Equal("(0) Emergency", vm.LevelLabels[1]);
        await _api.Received(1).SearchAsync("* AND level: <=4", 0, GraylogViewModel.PageSize, 0, "timestamp:desc", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_range_chip_chooses_LibreNMSs_ranges_and_Clear_puts_every_chip_back()
    {
        _dialogs.ChooseAsync("Time range", Arg.Any<IReadOnlyList<string>>()).Returns("Last 1 day");
        var vm = NewViewModel();
        vm.Initialise(null, null);
        await vm.EnsureLoadedAsync();
        Assert.Equal("All time", vm.RangeChipText);

        await vm.ChooseRangeCommand.ExecuteAsync(null);
        vm.ShowDevice(GraylogDeviceFilter.ForAddress("10.9.9.9"));
        Assert.Equal("Last 1 day", vm.RangeChipText);
        Assert.True(vm.HasActiveFilters);
        _api.ClearReceivedCalls();

        vm.ClearFiltersCommand.Execute(null);

        Assert.False(vm.HasActiveFilters);
        Assert.Null(vm.Device);
        Assert.Equal(0, vm.RangeIndex);
        await _api.Received(1).SearchAsync("*", 0, GraylogViewModel.PageSize, 0, "timestamp:desc", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Not_set_up_says_so_rather_than_searching()
    {
        _api.IsConfigured.Returns(false);
        var vm = NewViewModel();
        vm.Initialise(null, null);

        await vm.EnsureLoadedAsync();

        Assert.True(vm.IsNotConfigured);
        await _api.DidNotReceiveWithAnyArgs().SearchAsync(default!, default, default, default, default, default, default);
    }

    [Fact]
    public async Task A_Graylog_error_is_shown()
    {
        _api.SearchAsync(default!, default, default, default, default, default, default)
            .ThrowsAsyncForAnyArgs(new GraylogApiException("Graylog's API wasn't found at this address - check the server address, port and version.", HttpStatusCode.NotFound));
        var vm = NewViewModel();
        vm.Initialise(null, null);

        await vm.EnsureLoadedAsync();

        Assert.Equal("Graylog's API wasn't found at this address - check the server address, port and version. (HTTP 404)", vm.ErrorMessage);
    }

    [Fact]
    public async Task Device_detail_lists_Graylog_only_once_its_set_up()
    {
        var settings = Fakes.Settings(_appSettings);
        _client.Devices.GetAsync("1", Arg.Any<CancellationToken>()).Returns(Fakes.Device(1, "core-sw-01"));
        var setup = new GraylogSetup(_api, Substitute.For<IGraylogPasswordProtector>(), settings, Fakes.Secrets(), NullLogger<GraylogSetup>.Instance);
        var navigation = new RecordingNavigation();
        var vm = new DeviceDetailViewModel(_client, settings, Substitute.For<ILauncherService>(), new DeviceBookmarks(settings, TimeProvider.System), Substitute.For<IDialogService>(), navigation, setup);

        await vm.LoadAsync(1);
        await vm.OpenSectionCommand.ExecuteAsync(DeviceSectionInfo.Graylog);

        Assert.Contains(DeviceSectionInfo.Graylog, vm.Sections);
        Assert.Equal(Routes.Graylog, navigation.Visits.Single().Route);

        // Section cards are built per refresh, so switching Graylog off shows on the next one.
        _api.IsConfigured.Returns(false);
        await vm.LoadAsync(1);
        Assert.DoesNotContain(DeviceSectionInfo.Graylog, vm.Sections);
    }
}

/// <summary>
/// The transport concern from #1, checked against Core's real GraylogApi:
/// Graylog's login is an Authorization header, which .NET drops on any
/// redirect it follows - unlike the LibreNMS token's X-Auth-Token.
/// </summary>
public sealed class GraylogTransportTests
{
    [Fact]
    public async Task The_login_isnt_sent_on_to_where_a_redirect_points()
    {
        using var elsewhere = new HttpListener();
        var elsewherePort = FreePort();
        elsewhere.Prefixes.Add($"http://localhost:{elsewherePort}/");
        elsewhere.Start();

        using var graylog = new HttpListener();
        var graylogPort = FreePort();
        graylog.Prefixes.Add($"http://localhost:{graylogPort}/");
        graylog.Start();

        string? forwarded = "not reached";
        var redirecting = Task.Run(async () =>
        {
            var context = await graylog.GetContextAsync();
            Assert.NotNull(context.Request.Headers["Authorization"]);
            context.Response.StatusCode = 302;
            context.Response.RedirectLocation = $"http://localhost:{elsewherePort}/api/streams";
            context.Response.Close();
        });
        var receiving = Task.Run(async () =>
        {
            var context = await elsewhere.GetContextAsync();
            forwarded = context.Request.Headers["Authorization"];
            var body = "{\"total\":0,\"streams\":[]}"u8.ToArray();
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(body);
            context.Response.Close();
        });

        using var api = new GraylogApi(NullLogger<GraylogApi>.Instance);
        api.Configure(new GraylogConnection(new Uri($"http://localhost:{graylogPort}/"), GraylogSettings.Version21, null, "admin", "secret"));

        await api.GetStreamsAsync();
        await Task.WhenAll(redirecting, receiving).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Null(forwarded);
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}

public sealed class GraylogDevicePickerTests
{
    private readonly AppSettings _appSettings = new();
    private readonly IGraylogApi _api = Substitute.For<IGraylogApi>();
    private readonly RecordingNavigation _navigation = new();
    private readonly ILibreNmsClient _client = Fakes.Client(devices:
    [
        Fakes.Device(1, "core-sw-01", ip: "10.0.0.1"),
        Fakes.Device(2, "edge-rtr", ip: "10.0.0.2"),
        Fakes.Device(3, "ap-floor3", ip: "10.0.0.3"),
    ]);

    public GraylogDevicePickerTests()
    {
        _api.IsConfigured.Returns(true);
        _api.GetStreamsAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<GraylogStream>());
        _api.SearchAsync(default!, default, default, default, default, default, default).ReturnsForAnyArgs(new GraylogSearchResult
        {
            TotalResults = 3,
            Messages = new[] { "10.0.0.2", "10.9.9.9", "10.0.0.2" }.Select(source => new GraylogMessageEnvelope
            {
                Message = new GraylogMessage(new Dictionary<string, JsonElement> { ["source"] = JsonSerializer.SerializeToElement(source) }),
            }).ToList(),
        });
    }

    private async Task<(GraylogViewModel List, GraylogDevicePickerViewModel Picker)> OpenAsync()
    {
        var settings = Fakes.Settings(_appSettings);
        var setup = new GraylogSetup(_api, Substitute.For<IGraylogPasswordProtector>(), settings, Fakes.Secrets(), NullLogger<GraylogSetup>.Instance);
        var list = new GraylogViewModel(setup, _api, _client, settings, new RecordingNavigation(), Substitute.For<IDialogService>())
        {
            ResolveHostname = (_, _) => Task.FromResult<string?>(null),
        };
        list.Initialise(null, null);
        await list.EnsureLoadedAsync();

        var picker = new GraylogDevicePickerViewModel(_client, settings, new DeviceBookmarks(settings, TimeProvider.System), _navigation)
        {
            SearchDelay = TimeSpan.Zero,
        };
        await picker.LoadAsync(list);
        return (list, picker);
    }

    [Fact]
    public async Task Suggests_the_senders_on_screen_busiest_first_then_pinned_and_recent_devices()
    {
        _appSettings.PinnedDevices.Add(new PinnedDevice { DeviceId = 1, DisplayName = "core-sw-01" });
        _appSettings.RecentlyViewedDevices.Add(new RecentlyViewedDevice { DeviceId = 3, DisplayName = "ap-floor3" });
        _appSettings.RecentlyViewedDevices.Add(new RecentlyViewedDevice { DeviceId = 1, DisplayName = "core-sw-01" });

        var (_, picker) = await OpenAsync();

        Assert.Equal(["In these messages", "Pinned", "Recently viewed"], picker.Groups.Select(g => g.Name));
        var senders = picker.Groups[0];
        Assert.Equal(["edge-rtr", "10.9.9.9"], senders.Select(c => c.Name));
        Assert.Equal(["2", "1"], senders.Select(c => c.CountText));
        Assert.Equal("Not in LibreNMS", senders[1].Details);
        Assert.Equal(["core-sw-01"], picker.Groups[1].Select(c => c.Name));

        // Pinned already lists core-sw-01; recently viewed doesn't repeat it.
        Assert.Equal(["ap-floor3"], picker.Groups[2].Select(c => c.Name));
        Assert.False(picker.CanChooseAny);
    }

    [Fact]
    public async Task Searching_finds_any_device_and_offers_an_address_no_device_has()
    {
        var (_, picker) = await OpenAsync();

        picker.SearchText = "core";
        Assert.Equal(["core-sw-01"], picker.Groups.Single().Select(c => c.Name));

        picker.SearchText = "10.5.5.5";
        var unknown = picker.Groups.Single();
        Assert.Equal("Not in LibreNMS", unknown.Name);
        Assert.Equal(GraylogDeviceFilter.ForAddress("10.5.5.5"), unknown.Single().Filter);

        // An address a device has is that device, not a second row for it.
        picker.SearchText = "10.0.0.1";
        Assert.Equal(["Devices"], picker.Groups.Select(g => g.Name));

        picker.SearchText = "nothing like it";
        Assert.True(picker.IsEmpty);
    }

    [Fact]
    public async Task Choosing_sets_the_lists_Device_chip_and_goes_back()
    {
        var (list, picker) = await OpenAsync();

        await picker.ChooseCommand.ExecuteAsync(picker.Groups[0][0]);

        Assert.Equal(GraylogDeviceFilter.ForDevice(2, "edge-rtr"), list.Device);
        Assert.Equal(Routes.Back, _navigation.Visits.Single().Route);

        await picker.LoadAsync(list);
        Assert.True(picker.CanChooseAny);
        await picker.ChooseAnyCommand.ExecuteAsync(null);
        Assert.Null(list.Device);
    }
}
