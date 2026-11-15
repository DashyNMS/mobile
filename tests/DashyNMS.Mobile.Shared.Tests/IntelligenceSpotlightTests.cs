using DashyNMS.Agent;
using DashyNMS.Agent.Devices;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Intelligence;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Tests;

public sealed class IntelligenceSpotlightTests
{
    private sealed class Model : IIntelligenceModel
    {
        public IntelligenceAvailability Availability => IntelligenceAvailability.Available;
        public int? ContextSize => 4096;
        public bool Hold { get; set; }
        public async Task<int?> CountTokensAsync(string text, CancellationToken cancellationToken = default)
        {
            if (Hold) await Task.Delay(Timeout.Infinite, cancellationToken);
            return null;
        }
        public IIntelligenceSession StartSession(string instructions, IReadOnlyList<IntelligenceLookup> lookups) => throw new NotSupportedException();
    }
    private sealed class Lookups : IIntelligenceLookups
    {
        public IReadOnlyList<IntelligenceLookup> Create() => [];
    }
    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly SessionScope _scope = new(1, new LibreNmsConnection(new Uri("https://nms.example.net"), "test-token"));
    private readonly IntelligencePreferences _preferences;
    private readonly Model _model = new();
    public IntelligenceSpotlightTests()
    {
        var settings = Substitute.For<ICheckedSettingsStore>();
        settings.Current.Returns(new AppSettings());
        settings.TrySave().Returns(true);
        _preferences = new(settings, new InMemoryPreferences());
        _preferences.SetEnabled(true);
        _session.CurrentScope.Returns(_scope);
        _session.Connection.Returns(_scope.Connection);
    }
    private IntelligenceConversationViewModel Create() => new(_model, _preferences, new Lookups(), _session);
    private SpotlightChoice Choice(int id = 12) => new(_scope, new("device", id), "Example");
    private static Task<ObjectResolutionOutcome> Found(SessionScope scope, IntelligenceObjectReference identity, CancellationToken token)
        => Task.FromResult(new ObjectResolutionOutcome(new(identity, "Example"), null));

    [Fact]
    public void Private_drafts_survive_disabling_single_chat_without_becoming_persistable()
    {
        using var vm = Create();
        _preferences.SetSingleChatMode(true);
        vm.Draft = "First private draft";
        Assert.True(vm.NewObjectChatRetainingDraft());
        vm.Draft = "Second private draft";
        _preferences.SetSingleChatMode(false);
        Assert.True(vm.HasPreviousObjectChat);
        Assert.True(vm.SwitchPreviousObjectChat());
        Assert.Equal("First private draft", vm.Draft);
        Assert.True(vm.IsMemoryOnlyChat);
        Assert.True(vm.SwitchPreviousObjectChat());
        Assert.Equal("Second private draft", vm.Draft);
        Assert.True(vm.IsMemoryOnlyChat);
        Assert.True(vm.NewChat());
        Assert.False(vm.IsMemoryOnlyChat);
    }

    [Fact]
    public async Task Duplicate_and_deleted_objects_keep_existing_scope()
    {
        using var vm = Create();
        Assert.True(await vm.SetSpotlightAsync(Choice(), vm.ChatRevision, Found));
        Assert.False(await vm.SetSpotlightAsync(Choice(), vm.ChatRevision, Found));
        Assert.False(await vm.SetSpotlightAsync(Choice(13), vm.ChatRevision,
            (_, _, _) => Task.FromResult(new ObjectResolutionOutcome(null, HostFailure.NotFound))));
        Assert.Equal(12, Assert.Single(vm.Spotlight).Object!.Id);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Other_server_or_account_is_refused_before_resolution(bool account)
    {
        using var vm = Create();
        var other = new SessionScope(2, new LibreNmsConnection(new Uri(account ? "https://nms.example.net" : "https://other.example.net"), "other-token"));
        Assert.False(await vm.SetSpotlightAsync(Choice() with { Scope = other }, vm.ChatRevision,
            (_, _, _) => throw new InvalidOperationException("Must not resolve")));
        Assert.Empty(vm.Spotlight);
    }

    [Fact]
    public async Task A_full_spotlight_keeps_its_scope_and_only_replaces_it()
    {
        using var vm = Create();
        for (var id = 1; id <= IntelligenceConversationViewModel.MaxSpotlight; id++)
            Assert.True(await vm.SetSpotlightAsync(Choice(id), vm.ChatRevision, Found));
        Assert.False(await vm.SetSpotlightAsync(Choice(99), vm.ChatRevision, Found));
        Assert.Equal(IntelligenceConversationViewModel.MaxSpotlight, vm.Spotlight.Count);
        Assert.True(await vm.SetSpotlightAsync(Choice(99), vm.ChatRevision, Found, only: true));
        Assert.Equal(99, Assert.Single(vm.Spotlight).Object!.Id);
    }

    [Fact]
    public async Task A_new_chat_discards_an_inflight_selection()
    {
        using var vm = Create();
        var pending = new TaskCompletionSource<ObjectResolutionOutcome>();
        var call = vm.SetSpotlightAsync(Choice(), vm.ChatRevision, (_, _, _) => pending.Task);
        vm.NewChat();
        pending.SetResult(new(new(Choice().Identity, "Example"), null));
        Assert.False(await call);
        Assert.Empty(vm.Spotlight);
    }

    [Fact]
    public async Task Running_answers_lock_changes()
    {
        using var vm = Create();
        _model.Hold = true;
        var answer = vm.AskAsync("How is the network doing?");
        Assert.True(vm.IsRunning);
        Assert.False(await vm.SetSpotlightAsync(Choice(), vm.ChatRevision, Found));
        vm.Stop();
        await answer;
        Assert.Empty(vm.Spotlight);
    }

    [Fact]
    public async Task Restored_chats_keep_identity_and_unavailability_discards_pending_changes()
    {
        using var vm = Create();
        Assert.True(await vm.SetSpotlightAsync(Choice(), vm.ChatRevision, Found));
        var saved = vm.Spotlight.ToArray();
        vm.NewChat();
        Assert.True(vm.Restore([], "draft", null, saved));
        Assert.Equal(Choice().Identity, Assert.Single(vm.Spotlight).Object);
        Assert.Contains("device 12", vm.SpotlightInstructions);
        // The availability gate is checked again after an asynchronous resolver returns.
        var pending = new TaskCompletionSource<ObjectResolutionOutcome>();
        var call = vm.SetSpotlightAsync(Choice(13), vm.ChatRevision, (_, _, _) => pending.Task);
        _preferences.SetEnabled(false);
        pending.SetResult(new(new(Choice(13).Identity, "Example"), null));
        Assert.False(await call);
        Assert.Single(vm.Spotlight);
    }
}
