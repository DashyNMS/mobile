using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Tests;

public sealed class IntelligenceSettingsTests
{
    private readonly IIntelligenceModel _model = Substitute.For<IIntelligenceModel>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly ILauncherService _launcher = Substitute.For<ILauncherService>();
    private readonly AppSettings _values = new();
    private readonly InMemoryPreferences _preferences = new();
    private readonly ICheckedSettingsStore _settings;
    private readonly IntelligencePreferences _policy;
    private readonly IntelligenceSettingsViewModel _vm;

    public IntelligenceSettingsTests()
    {
        _settings = Substitute.For<ICheckedSettingsStore>();
        _settings.Current.Returns(_values);
        _settings.TrySave().Returns(true);
        _model.Availability.Returns(IntelligenceAvailability.Available);
        _policy = new IntelligencePreferences(_settings, _preferences);
        _vm = new IntelligenceSettingsViewModel(_model, _policy, _dialogs, _launcher);
    }

    [Theory]
    [InlineData(IntelligenceAvailability.Available, true, true, "Ready")]
    [InlineData(IntelligenceAvailability.Unsupported, false, false, "iOS 26")]
    [InlineData(IntelligenceAvailability.DeviceNotEligible, true, false, "doesn't support")]
    [InlineData(IntelligenceAvailability.NotEnabled, true, false, "turned off")]
    [InlineData(IntelligenceAvailability.ModelNotReady, true, false, "still downloading")]
    [InlineData(IntelligenceAvailability.Unavailable, true, false, "unavailable")]
    public void Availability_explains_each_state_and_hides_unsupported_platforms(IntelligenceAvailability state, bool visible, bool available, string reason)
    {
        _model.Availability.Returns(state);
        _vm.Refresh();
        Assert.Equal(visible, _vm.IsVisible);
        Assert.Equal(available, _vm.IsAvailable);
        Assert.Contains(reason, _vm.AvailabilityText);
        Assert.Equal(available, _vm.ToggleCommand.CanExecute(null));
        Assert.False(_vm.CanUseIntelligence);
    }

    [Fact]
    public void The_mobile_gate_is_off_even_with_Cores_enabled_default()
    {
        Assert.True(_values.IntelligenceEnabled);
        Assert.False(_policy.Enabled);
        Assert.False(_vm.Enabled);
    }

    [Fact]
    public async Task Cancelling_the_first_notice_keeps_Intelligence_off()
    {
        await _vm.ToggleCommand.ExecuteAsync(null);
        Assert.False(_policy.Enabled);
        Assert.False(_policy.NoticeAccepted);
        _settings.DidNotReceive().TrySave();
    }

    [Fact]
    public async Task First_enable_shows_the_notice_once_and_preserves_opt_in_across_restarts()
    {
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        await _vm.ToggleCommand.ExecuteAsync(null);
        Assert.True(_vm.CanUseIntelligence);
        Assert.True(new IntelligencePreferences(_settings, _preferences).Enabled);
        await _vm.ToggleCommand.ExecuteAsync(null);
        Assert.False(_policy.Enabled);
        await _vm.ToggleCommand.ExecuteAsync(null);
        Assert.True(_policy.Enabled);
        await _dialogs.Received(1).ConfirmAsync("Intelligence on this device", IntelligenceSettingsViewModel.Notice, "Turn on", "Cancel");
    }

    [Fact]
    public async Task Availability_is_rechecked_after_the_notice_and_after_resume()
    {
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(_ =>
        {
            _model.Availability.Returns(IntelligenceAvailability.NotEnabled);
            return Task.FromResult(true);
        });
        await _vm.ToggleCommand.ExecuteAsync(null);
        Assert.False(_vm.CanUseIntelligence);
        Assert.False(_policy.NoticeAccepted);
        Assert.True(_vm.CanOpenSettings);
        _model.Availability.Returns(IntelligenceAvailability.Available);
        _vm.Refresh();
        Assert.True(_vm.IsAvailable);
        Assert.True(_vm.ToggleCommand.CanExecute(null));
    }

    [Fact]
    public async Task Enabled_preferences_never_override_unavailable_models_but_can_be_turned_off()
    {
        _policy.SetEnabled(true);
        _model.Availability.Returns(IntelligenceAvailability.ModelNotReady);
        _vm.Refresh();
        Assert.False(_vm.CanUseIntelligence);
        Assert.True(_vm.ToggleCommand.CanExecute(null));
        await _vm.ToggleCommand.ExecuteAsync(null);
        Assert.False(_policy.Enabled);
    }

    [Fact]
    public void Detail_and_chat_choices_use_Cores_keys_and_normalise_legacy_values()
    {
        _values.IntelligenceReplyStyle = "evidence-based";
        Assert.Equal(2, _vm.DetailIndex);
        _values.IntelligenceReplyStyle = "full";
        Assert.Equal(2, _vm.DetailIndex);
        _vm.DetailIndex = 0;
        _vm.ColourStatusIndicators = false;
        _vm.SingleChatMode = true;
        Assert.Equal("brief", _values.IntelligenceReplyStyle);
        Assert.False(_values.IntelligenceColourStatusIndicators);
        Assert.True(_values.IntelligenceSingleChatMode);
        _settings.Received(3).TrySave();
    }

    [Fact]
    public async Task A_failed_save_keeps_the_mobile_gate_off_and_does_not_expose_exception_text()
    {
        _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _settings.TrySave().Returns(false);
        await _vm.ToggleCommand.ExecuteAsync(null);
        Assert.False(_policy.Enabled);
        Assert.False(_policy.NoticeAccepted);
        Assert.Equal("The Intelligence setting couldn't be saved. Try again.", _vm.ErrorMessage);
        _vm.DetailIndex = 0;
        Assert.Equal(1, _vm.DetailIndex);
    }

    [Fact]
    public void A_notice_write_failure_never_commits_enablement_or_notifies()
    {
        _values.IntelligenceEnabled = false;
        var preferences = Substitute.For<IAppPreferences>();
        preferences.When(p => p.Set(IntelligencePreferences.NoticeKey, "accepted"))
            .Do(_ => throw new IOException("preferences write failed"));
        var policy = new IntelligencePreferences(_settings, preferences);
        var notifications = 0;
        policy.Changed += (_, _) => notifications++;
        Assert.Throws<IOException>(() => policy.SetEnabled(true));
        Assert.False(_values.IntelligenceEnabled);
        Assert.False(policy.Enabled);
        Assert.Equal(0, notifications);
        _settings.DidNotReceive().TrySave();
    }

    [Fact]
    public void A_failed_Core_save_restores_the_notice_and_does_not_notify()
    {
        _values.IntelligenceEnabled = false;
        _settings.TrySave().Returns(false);
        var notifications = 0;
        _policy.Changed += (_, _) => notifications++;
        Assert.Throws<IOException>(() => _policy.SetEnabled(true));
        Assert.False(_values.IntelligenceEnabled);
        Assert.Null(_preferences.Get(IntelligencePreferences.NoticeKey));
        Assert.False(_policy.Enabled);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void An_accepted_notice_is_not_rewritten_when_re_enabling()
    {
        _values.IntelligenceEnabled = false;
        var preferences = Substitute.For<IAppPreferences>();
        preferences.Get(IntelligencePreferences.NoticeKey).Returns("accepted");
        var policy = new IntelligencePreferences(_settings, preferences);
        policy.SetEnabled(true);
        Assert.True(policy.Enabled);
        preferences.DidNotReceive().Set(Arg.Any<string>(), Arg.Any<string?>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_real_write_failure_preserves_enablement_after_reload(bool enabling)
    {
        var folder = Path.Combine(Path.GetTempPath(), "mobile-intelligence-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "settings.json");
            var store = new SettingsStore(Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsStore>.Instance, path);
            store.Current.IntelligenceEnabled = !enabling;
            Assert.True(store.TrySave());
            if (!enabling) _preferences.Set(IntelligencePreferences.NoticeKey, "accepted");
            var policy = new IntelligencePreferences(store, _preferences);
            var vm = new IntelligenceSettingsViewModel(_model, policy, _dialogs, _launcher);
            _dialogs.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
            // Forces a real write failure on every platform without relying on permissions.
            Directory.CreateDirectory(path + ".tmp");
            var notifications = 0;
            policy.Changed += (_, _) => notifications++;
            await vm.ToggleCommand.ExecuteAsync(null);
            Assert.Equal(!enabling, policy.Enabled);
            Assert.Equal(!enabling, store.Current.IntelligenceEnabled);
            var reloaded = new SettingsStore(Microsoft.Extensions.Logging.Abstractions.NullLogger<SettingsStore>.Instance, path);
            Assert.Equal(!enabling, new IntelligencePreferences(reloaded, _preferences).Enabled);
            Assert.Equal(enabling ? null : "accepted", _preferences.Get(IntelligencePreferences.NoticeKey));
            Assert.Equal(0, notifications);
            Assert.Equal("The Intelligence setting couldn't be saved. Try again.", vm.ErrorMessage);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void An_unchecked_store_cannot_report_a_saved_change()
    {
        var store = Fakes.Settings(_values);
        var policy = new IntelligencePreferences(store, _preferences);
        Assert.Throws<IOException>(() => policy.SetDetail("brief"));
        Assert.Equal("balanced", _values.IntelligenceReplyStyle);
        store.DidNotReceive().Save();
    }

    [Fact]
    public async Task Opening_Settings_uses_a_public_URL_and_has_a_safe_fallback()
    {
        _model.Availability.Returns(IntelligenceAvailability.NotEnabled);
        await _vm.OpenSettingsCommand.ExecuteAsync(null);
        await _launcher.Received(1).TryOpenAppAsync(new Uri("app-settings:"));
        Assert.Contains("Apple Intelligence & Siri", _vm.ErrorMessage);
    }
}
