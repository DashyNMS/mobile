using System.Net;
using System.Text;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Unimus;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute.ExceptionExtensions;

namespace DashyNMS.Mobile.Tests;

public sealed class UnimusSetupTests
{
    private readonly AppSettings _appSettings = new();
    private readonly IUnimusApi _api = Substitute.For<IUnimusApi>();
    private readonly SecretCache _secrets = Fakes.Secrets();
    private readonly SecureUnimusTokenProtector _tokens;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly UnimusSetup _setup;

    public UnimusSetupTests()
    {
        _tokens = new SecureUnimusTokenProtector(_secrets);
        _setup = new UnimusSetup(_api, _tokens, Fakes.Settings(_appSettings), _secrets, _time, NullLogger<UnimusSetup>.Instance);
    }

    [Fact]
    public void The_token_is_kept_in_the_keychain_under_its_own_key()
    {
        _tokens.Save("secret");

        Assert.Equal("secret", _secrets.Get(SecureUnimusTokenProtector.Key));
        Assert.Null(_secrets.Get(SecureGraylogPasswordProtector.Key));
        Assert.Contains(SecureUnimusTokenProtector.Key, ServiceCollectionExtensions.SecretKeys);
    }

    [Fact]
    public async Task Switched_on_with_a_token_connects_on_first_use()
    {
        _appSettings.Unimus = new UnimusSettings { Enabled = true, Url = "unimus.example.com/api/v2" };
        var storage = Substitute.For<ISecureStorage>();
        storage.GetAsync(SecureUnimusTokenProtector.Key).Returns("secret");
        var secrets = new SecretCache(storage, NullLogger<SecretCache>.Instance);
        var setup = new UnimusSetup(_api, new SecureUnimusTokenProtector(secrets), Fakes.Settings(_appSettings), secrets, _time, NullLogger<UnimusSetup>.Instance);

        await setup.EnsureConfiguredAsync();

        _api.Received(1).Configure(Arg.Is<UnimusConnection>(c =>
            c.WebRoot == new Uri("https://unimus.example.com/") && c.ApiToken == "secret"));
    }

    [Fact]
    public async Task Without_a_token_it_stays_disconnected()
    {
        _appSettings.Unimus = new UnimusSettings { Enabled = true, Url = "unimus.example.com" };

        await _setup.EnsureConfiguredAsync();

        _api.DidNotReceive().Configure(Arg.Any<UnimusConnection>());
    }

    [Fact]
    public async Task Devices_match_by_name_then_address_from_one_cached_list()
    {
        _appSettings.Unimus = new UnimusSettings { Enabled = true, Url = "unimus.example.com", MyDomain = "example.net" };
        _api.IsConfigured.Returns(true);
        _api.ListAllDevicesAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new UnimusDevice { Id = 7, Address = "192.0.2.10", Description = "edge-rtr-01.example.net" },
            new UnimusDevice { Id = 8, Address = "192.0.2.20" },
        ]);

        var byName = await _setup.FindAsync(Fakes.Device(1, "edge-rtr-01"));
        var byAddress = await _setup.FindAsync(Fakes.Device(2, "access-sw-02", ip: "192.0.2.20"));
        var missing = await _setup.FindAsync(Fakes.Device(3, "lab-sw-03", ip: "198.51.100.1"));

        Assert.Equal(7, byName?.Id);
        Assert.Equal(8, byAddress?.Id);
        Assert.Null(missing);
        await _api.Received(1).ListAllDevicesAsync(Arg.Any<CancellationToken>());

        // Kept for a while, then fetched again so newly added devices are found.
        _time.Advance(UnimusSetup.DeviceListLifetime);
        await _setup.FindAsync(Fakes.Device(1, "edge-rtr-01"));
        await _api.Received(2).ListAllDevicesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Forgetting_the_token_disconnects()
    {
        _tokens.Save("secret");

        _setup.ForgetToken();

        Assert.False(_tokens.HasStoredToken);
        _api.Received(1).Clear();
    }
}

public sealed class UnimusSettingsTests
{
    private readonly AppSettings _appSettings = new();
    private readonly ISettingsStore _settings;
    private readonly IUnimusApi _api = Substitute.For<IUnimusApi>();
    private readonly IUnimusConnectionTester _tester = Substitute.For<IUnimusConnectionTester>();
    private readonly SecureUnimusTokenProtector _tokens = new(Fakes.Secrets());
    private readonly RecordingNavigation _navigation = new();
    private readonly UnimusSettingsViewModel _vm;

    public UnimusSettingsTests()
    {
        _settings = Fakes.Settings(_appSettings);
        var setup = new UnimusSetup(_api, _tokens, _settings, Fakes.Secrets(), TimeProvider.System, NullLogger<UnimusSetup>.Instance);
        _vm = new UnimusSettingsViewModel(_settings, setup, _tokens, _tester, Substitute.For<IDialogService>(), _navigation);
    }

    private void FillIn()
    {
        _vm.Enabled = true;
        _vm.Url = "unimus.example.com";
        _vm.TokenInput = "secret";
    }

    [Fact]
    public async Task Saving_keeps_the_settings_and_token_and_connects()
    {
        FillIn();
        _vm.MyDomain = " .example.net ";

        await _vm.SaveCommand.ExecuteAsync(null);

        Assert.True(_appSettings.Unimus.Enabled);
        Assert.Equal("example.net", _appSettings.Unimus.MyDomain);
        Assert.Equal("secret", _tokens.Load());
        _settings.Received(1).Save();
        _api.Received(1).Configure(Arg.Is<UnimusConnection>(c => c.WebRoot == new Uri("https://unimus.example.com/")));
        Assert.Equal(Routes.Back, _navigation.Visits.Single().Route);
    }

    [Fact]
    public async Task Switched_on_without_a_token_isnt_saved()
    {
        _vm.Enabled = true;
        _vm.Url = "unimus.example.com";

        await _vm.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Enter an API token from Unimus.", _vm.ErrorMessage);
        Assert.False(_appSettings.Unimus.Enabled);
        _settings.DidNotReceive().Save();
    }

    [Fact]
    public async Task Test_tries_the_form_without_touching_the_live_client()
    {
        FillIn();

        await _vm.TestCommand.ExecuteAsync(null);

        Assert.True(_vm.TestSucceeded);
        Assert.Equal("Connected to Unimus.", _vm.TestResultText);
        _api.DidNotReceive().Configure(Arg.Any<UnimusConnection>());
    }

    [Fact]
    public async Task Test_shows_Unimuss_reason_for_a_failure()
    {
        FillIn();
        _tester.TestAsync(Arg.Any<UnimusConnection>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnimusApiException("Unimus rejected the API token.", HttpStatusCode.Unauthorized));

        await _vm.TestCommand.ExecuteAsync(null);

        Assert.False(_vm.TestSucceeded);
        Assert.Equal("Unimus rejected the API token. (HTTP 401)", _vm.TestResultText);
    }
}

public sealed class UnimusBackupsTests
{
    private readonly AppSettings _appSettings = new() { Unimus = new UnimusSettings { Enabled = true, Url = "unimus.example.com" } };
    private readonly ILibreNmsClient _client = Fakes.Client();
    private readonly IUnimusApi _api = Substitute.For<IUnimusApi>();
    private readonly IShareService _share = Substitute.For<IShareService>();
    private readonly RecordingNavigation _navigation = new();
    private readonly UnimusSetup _setup;

    public UnimusBackupsTests()
    {
        _api.IsConfigured.Returns(true);
        _setup = new UnimusSetup(_api, Substitute.For<DesktopNMS.Core.Security.IUnimusTokenProtector>(), Fakes.Settings(_appSettings), Fakes.Secrets(), TimeProvider.System, NullLogger<UnimusSetup>.Instance);
        _client.Devices.GetAsync("1", Arg.Any<CancellationToken>()).Returns(Fakes.Device(1, "edge-rtr-01", ip: "192.0.2.10"));
        _api.ListAllDevicesAsync(Arg.Any<CancellationToken>()).Returns([new UnimusDevice { Id = 7, Address = "192.0.2.10" }]);
    }

    private static UnimusBackup Backup(int id, int day, string? text = null) => new()
    {
        Id = id,
        ValidSince = new DateTimeOffset(2026, 9, day, 18, 1, 0, TimeSpan.Zero).ToUnixTimeSeconds(),
        Bytes = text is null ? null : Convert.ToBase64String(Encoding.UTF8.GetBytes(text)),
    };

    private async Task<UnimusBackupsViewModel> LoadAsync()
    {
        var vm = new UnimusBackupsViewModel(_client, _setup, _api, _navigation);
        vm.Initialise(1, "edge-rtr-01");
        await vm.EnsureLoadedAsync();
        return vm;
    }

    [Fact]
    public async Task Lists_the_matched_devices_backups_newest_first()
    {
        _api.GetBackupsAsync(7, 0, UnimusBackupsViewModel.PageSize, Arg.Any<CancellationToken>())
            .Returns(new UnimusBackupPage { Backups = [Backup(1, 3), Backup(3, 18), Backup(2, 10)] });

        var vm = await LoadAsync();

        Assert.Equal([3, 2, 1], vm.Backups.Select(b => b.Backup.Id));
        Assert.Equal("Current", vm.Backups[0].StatusText);
        Assert.Equal("Previous", vm.Backups[1].StatusText);
        Assert.True(vm.CanCompare);
    }

    [Fact]
    public async Task A_device_Unimus_doesnt_have_says_how_its_matched()
    {
        _client.Devices.GetAsync("1", Arg.Any<CancellationToken>()).Returns(Fakes.Device(1, "lab-sw-03", ip: "198.51.100.1"));

        var vm = await LoadAsync();

        Assert.True(vm.IsEmpty);
        Assert.StartsWith("Unimus doesn't have this device.", vm.EmptyText);
        await _api.DidNotReceiveWithAnyArgs().GetBackupsAsync(default, default, default, default);
    }

    [Fact]
    public async Task A_Unimus_error_is_shown()
    {
        _api.GetBackupsAsync(7, Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnimusApiException("Unimus rejected the API token.", HttpStatusCode.Unauthorized));

        var vm = await LoadAsync();

        Assert.Equal("Unimus rejected the API token. (HTTP 401)", vm.ErrorMessage);
        Assert.False(vm.IsEmpty);
    }

    [Fact]
    public async Task Tapping_a_backup_opens_it_and_Compare_diffs_the_two_chosen_older_first()
    {
        _api.GetBackupsAsync(7, 0, UnimusBackupsViewModel.PageSize, Arg.Any<CancellationToken>())
            .Returns(new UnimusBackupPage { Backups = [Backup(1, 3), Backup(2, 10), Backup(3, 18)] });
        var vm = await LoadAsync();

        await vm.OpenCommand.ExecuteAsync(vm.Backups[0]);
        var opened = Assert.IsType<UnimusBackupRequest>(_navigation.Visits.Single().Parameters![Routes.UnimusParameter]);
        Assert.Equal((7, 3), (opened.UnimusDeviceId, opened.Backup.Id));

        vm.ToggleCompareCommand.Execute(null);
        await vm.OpenCommand.ExecuteAsync(vm.Backups[0]);
        await vm.OpenCommand.ExecuteAsync(vm.Backups[1]);
        Assert.True(vm.CompareSelectedCommand.CanExecute(null));

        // A third choice replaces the first.
        await vm.OpenCommand.ExecuteAsync(vm.Backups[2]);
        Assert.Equal([2, 1], vm.Backups.Where(b => b.IsSelected).Select(b => b.Backup.Id));

        await vm.CompareSelectedCommand.ExecuteAsync(null);

        var (route, parameters) = _navigation.Visits.Last();
        Assert.Equal(Routes.UnimusDiff, route);
        var diff = Assert.IsType<UnimusDiffRequest>(parameters![Routes.UnimusParameter]);
        Assert.Equal((1, 2), (diff.Original.Id, diff.Revised.Id));
        Assert.False(vm.IsComparing);
    }

    [Fact]
    public async Task A_backup_is_searchable_and_shared_as_a_file()
    {
        var vm = new UnimusBackupViewModel(_api, _share) { SearchDelay = TimeSpan.Zero };
        await vm.LoadAsync(new UnimusBackupRequest("edge-rtr-01", 7, Backup(3, 18, "hostname edge-rtr-01\r\ninterface ge-0/0/1\n description uplink\ninterface ge-0/0/2")));

        Assert.Equal("4 lines", vm.CountText);

        vm.SearchText = "interface";
        Assert.Equal(["2", "4"], vm.Lines.Select(l => l.NumberText));
        Assert.Equal("2 of 4 lines", vm.CountText);

        await vm.ShareCommand.ExecuteAsync(null);
        await _share.Received(1).ShareTextFileAsync(
            Arg.Is<string>(n => n.StartsWith("edge-rtr-01_2026-09-", StringComparison.Ordinal) && n.EndsWith(".cfg", StringComparison.Ordinal)),
            Arg.Is<string>(c => c.StartsWith("hostname edge-rtr-01", StringComparison.Ordinal)),
            "text/plain",
            Arg.Any<string>());
    }

    [Fact]
    public async Task A_backup_listed_without_its_text_is_fetched()
    {
        _api.GetBackupContentAsync(7, 3, 0, UnimusBackupsViewModel.PageSize, Arg.Any<CancellationToken>()).Returns("hostname edge-rtr-01");
        var vm = new UnimusBackupViewModel(_api, _share);

        await vm.LoadAsync(new UnimusBackupRequest("edge-rtr-01", 7, Backup(3, 18)));

        Assert.Equal("hostname edge-rtr-01", vm.Lines.Single().Text);
    }

    [Fact]
    public async Task A_binary_backup_says_only_Unimus_can_show_it()
    {
        var backup = Backup(3, 18);
        backup.Type = "BINARY";
        var vm = new UnimusBackupViewModel(_api, _share);

        await vm.LoadAsync(new UnimusBackupRequest("edge-rtr-01", 7, backup));

        Assert.True(vm.IsEmpty);
        Assert.Equal("This backup is stored as binary. Only Unimus can show it.", vm.EmptyText);
        Assert.False(vm.ShareCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_diff_colours_changes_folds_the_rest_and_shares_a_unified_diff()
    {
        var common = Enumerable.Range(1, 10).Select(i => new UnimusDiffLine { Number = i, Text = $"line {i}" }).ToList();
        _api.GetDiffAsync(1, 2, Arg.Any<CancellationToken>()).Returns(new UnimusBackupDiff
        {
            LineGroups =
            [
                new UnimusDiffLineGroup { Type = "COMMON", OriginalLines = common, RevisedLines = common },
                new UnimusDiffLineGroup
                {
                    Type = "CHANGED",
                    OriginalLines = [new UnimusDiffLine { Number = 11, Text = " description old" }],
                    RevisedLines = [new UnimusDiffLine { Number = 11, Text = " description new" }],
                },
            ],
        });
        var vm = new UnimusDiffViewModel(_api, _share);

        await vm.LoadAsync(new UnimusDiffRequest("edge-rtr-01", Backup(1, 3), Backup(2, 10)));

        Assert.Equal("+1 −1", vm.SummaryText);
        var hidden = vm.Lines[0];
        Assert.True(hidden.IsHidden);
        Assert.Equal("… 7 unchanged lines, tap to show", hidden.Text);
        Assert.Equal(["-", "+"], vm.Lines.Where(l => l.IsAdded || l.IsRemoved).Select(l => l.Prefix));

        vm.ExpandCommand.Execute(hidden);
        Assert.Equal(12, vm.Lines.Count);

        vm.OnlyChanged = false;
        Assert.DoesNotContain(vm.Lines, l => l.IsHidden);

        await vm.ShareCommand.ExecuteAsync(null);
        await _share.Received(1).ShareTextFileAsync(
            Arg.Is<string>(n => n.EndsWith(".diff", StringComparison.Ordinal)),
            Arg.Is<string>(t => t.Contains("- description old", StringComparison.Ordinal) && t.Contains("+ description new", StringComparison.Ordinal)),
            "text/x-diff",
            Arg.Any<string>());
    }

    [Fact]
    public async Task Two_identical_backups_say_so()
    {
        _api.GetDiffAsync(1, 2, Arg.Any<CancellationToken>()).Returns(new UnimusBackupDiff());
        var vm = new UnimusDiffViewModel(_api, _share);

        await vm.LoadAsync(new UnimusDiffRequest("edge-rtr-01", Backup(1, 3), Backup(2, 10)));

        Assert.True(vm.IsEmpty);
        Assert.Equal("No differences", vm.SummaryText);
        Assert.False(vm.ShareCommand.CanExecute(null));
    }

    [Fact]
    public async Task Device_detail_lists_Config_backups_only_once_Unimus_is_set_up()
    {
        var settings = Fakes.Settings(_appSettings);
        var vm = new DeviceDetailViewModel(_client, settings, Substitute.For<ILauncherService>(), new DeviceBookmarks(settings, TimeProvider.System),
            Substitute.For<IDialogService>(), _navigation, unimus: _setup);

        await vm.LoadAsync(1);
        await vm.OpenSectionCommand.ExecuteAsync(DeviceSectionInfo.Unimus);

        Assert.Contains(DeviceSectionInfo.Unimus, vm.Sections);
        Assert.Equal(Routes.UnimusBackups, _navigation.Visits.Single().Route);

        _api.IsConfigured.Returns(false);
        await vm.LoadAsync(1);
        Assert.DoesNotContain(DeviceSectionInfo.Unimus, vm.Sections);
    }
}
