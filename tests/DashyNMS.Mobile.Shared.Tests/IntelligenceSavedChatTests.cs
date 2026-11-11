using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Intelligence;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// Saved chats to reopen, rename, pin and delete (#229), over the real encrypted
/// storage (#222). Each "app start" makes a new storage, conversation and list
/// over the same folder and keychain. Every name, address and line here is synthetic.
/// </summary>
public sealed class IntelligenceSavedChatTests : IDisposable
{
    private static readonly LibreNmsConnection ServerA = new(new Uri("https://nms-a.example.net/"), "token-a");
    private static readonly LibreNmsConnection ServerB = new(new Uri("https://nms-b.example.net/"), "token-b");

    // A Wednesday, so this week, last week and earlier this month are distinct.
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private static readonly IntelligenceInlineReference Device =
        new("device:7", "sw-zz-01", "up", "up", Now, "Switch") { Object = new IntelligenceObjectReference("device", 7) };

    private static readonly IntelligenceInlineReference Group =
        new("group:3", "Core switches", "", "", Now, "") { Object = new IntelligenceObjectReference("group", 3) };

    private const string GroupFilter = "Core  switches / exact";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"dashynms-saved-chats-{Guid.NewGuid():N}");
    private readonly ChatKeyStorage _keys = new();
    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly AppSettings _settings = new();
    private readonly IntelligencePreferences _preferences;
    private readonly Model _model = new();
    private readonly List<IDisposable> _open = [];
    private readonly List<App> _apps = [];

    public IntelligenceSavedChatTests()
    {
        _time.SetLocalTimeZone(TimeZoneInfo.Utc);
        var store = Substitute.For<ICheckedSettingsStore>();
        store.Current.Returns(_settings);
        store.TrySave().Returns(true);
        var preferences = new InMemoryPreferences();
        preferences.Set(IntelligencePreferences.NoticeKey, "accepted");
        _preferences = new IntelligencePreferences(store, preferences);
        _session.Connection.Returns(ServerA);
        _dialogs.ConfirmDestructiveAsync(default!, default!, default!).ReturnsForAnyArgs(true);
    }

    public void Dispose()
    {
        // Writes still going would race the folder's removal.
        foreach (var app in _apps) app.Chats.Writer.FlushAsync().Wait(TimeSpan.FromSeconds(10));
        foreach (var item in _open) item.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        File.Delete(_root + ".cleanup-pending");
    }

    /// <summary>One app start: a fresh storage, conversation and chat list over the same disk and keychain.</summary>
    private sealed record App(IntelligenceChatStorage Storage, IntelligenceConversationViewModel Conversation, IntelligenceChatListViewModel Chats)
    {
        public async Task SettleAsync() => await Chats.Writer.FlushAsync();
    }

    private App Start(Action<Action>? dispatch = null)
    {
        var storage = new IntelligenceChatStorage(_root, _keys, new NoBackup(), NullLogger<IntelligenceChatStorage>.Instance);
        var conversation = new IntelligenceConversationViewModel(_model, _preferences, new Lookups(), _session, action => action());
        var chats = new IntelligenceChatListViewModel(conversation, storage, _preferences, _session, _dialogs, _time,
            NullLogger<IntelligenceChatListViewModel>.Instance, dispatch ?? (action => action()));
        _open.Add(chats);
        _open.Add(conversation);
        var app = new App(storage, conversation, chats);
        _apps.Add(app);
        return app;
    }

    private async Task<SavedChatCollection> ReadAsync(LibreNmsConnection server)
    {
        var storage = new IntelligenceChatStorage(_root, _keys, new NoBackup(), NullLogger<IntelligenceChatStorage>.Instance);
        var store = await storage.OpenAsync();
        Assert.NotNull(store);
        return store.Load(IntelligenceSavedChats.Scope(server));
    }

    private void SwitchTo(LibreNmsConnection? server)
    {
        _session.Connection.Returns(server);
        _session.StateChanged += Raise.EventWith(EventArgs.Empty);
    }

    private string[] ChatFiles(string pattern = "*.chat") =>
        Directory.Exists(_root) ? Directory.GetFiles(_root, pattern, SearchOption.AllDirectories) : [];

    [Fact]
    public async Task A_chat_reopens_after_a_restart_with_its_draft_detail_model_evidence_filters_and_images()
    {
        _model.Scripts.Enqueue(async (_, tools, text, token) =>
        {
            await tools[0].Run("", token);
            text("Partial");
            return new IntelligenceAnswer("[[device:7]] is up. Group [[group:3]].", null)
            {
                Images = [new IntelligenceImage(Png, "image/png")],
            };
        });
        var first = Start();
        await first.Chats.LoadAsync();
        first.Conversation.SelectedDetail = IntelligenceDetail.Brief;
        Assert.True(await first.Conversation.AskAsync("Which switches are up?"));
        first.Conversation.Draft = "draft-sentinel follow-up";
        _time.Advance(IntelligenceChatListViewModel.SaveDelay);
        await first.SettleAsync();

        var restarted = Start();
        await restarted.Chats.LoadAsync();
        var chat = Assert.Single(restarted.Chats.Chats);
        Assert.Same(chat, restarted.Chats.Active);
        Assert.Equal("Which switches are up?", chat.Title);
        Assert.Equal(IntelligenceSavedChats.OnDeviceModel, chat.Data.Model);
        Assert.Equal("draft-sentinel follow-up", restarted.Conversation.Draft);
        Assert.Equal(IntelligenceDetail.Brief, restarted.Conversation.SelectedDetail);

        var turn = Assert.Single(restarted.Conversation.Turns);
        Assert.Equal(IntelligenceTurnState.Completed, turn.State);
        Assert.Equal(IntelligenceDetail.Brief, turn.Detail);
        Assert.Equal("[[device:7]] is up. Group [[group:3]].", turn.Answer);
        Assert.Equal(IntelligenceLookupState.Completed, Assert.Single(turn.Lookups).State);
        Assert.Equal(Png, Assert.Single(turn.Images).Data);
        Assert.Equal(new[] { new IntelligenceEvidence(Device), new IntelligenceEvidence(Group, GroupFilter) }, turn.Evidence);

        // A reopened chip opens what the live one did: the exact group name, not the display text.
        Assert.True(restarted.Conversation.OwnsTurn(turn));
        Assert.True(IntelligenceAnswerActions.TryRoute(Group, turn.Evidence, out var route, out var parameters));
        Assert.Equal(Routes.Devices, route);
        Assert.Equal(GroupFilter, parameters[Routes.GroupParameter]);
        Assert.True(IntelligenceAnswerActions.TryRoute(Device, turn.Evidence, out _, out var device));
        Assert.Equal(7, device[Routes.DeviceIdParameter]);
    }

    [Fact]
    public void Forged_or_stale_references_in_a_file_open_nothing()
    {
        var chat = new SavedIntelligenceChat
        {
            Messages =
            [
                new() { IsUser = true, Text = "Question", CreatedAt = Now },
                new()
                {
                    Text = "Answer", Outcome = IntelligenceReplyOutcome.Completed,
                    PriorReferences =
                    [
                        Device,
                        // Its key names another object than it stands for.
                        Device with { Key = "device:8" },
                        // No typed object at all, and an invalid one.
                        new("device:9", "sw-zz-09", "", "", Now, ""),
                        new("sensor:4", "Temp", "", "", Now, "") { Object = new IntelligenceObjectReference("sensor", 4) },
                        Group,
                    ],
                    StatusCards =
                    [
                        new(IntelligenceSavedChats.FilterCardTitle, "", Now,
                        [
                            // A filter for a device is ignored; the group's own is kept.
                            new IntelligenceStatusRow("forged-device-filter", "", "", "") { Object = Device.Object },
                            new IntelligenceStatusRow(GroupFilter, "", "", "") { Object = Group.Object },
                        ]),
                        new("Some other card", "", Now, [new IntelligenceStatusRow("other", "", "", "") { Object = Group.Object }]),
                    ],
                },
            ],
        };

        var turn = Assert.Single(IntelligenceSavedChats.Turns(chat));
        Assert.Equal(new[] { new IntelligenceEvidence(Device), new IntelligenceEvidence(Group, GroupFilter) }, turn.Evidence);
        Assert.False(IntelligenceAnswerActions.TryRoute(Device with { Key = "device:8" }, turn.Evidence, out _, out _));
    }

    [Fact]
    public void Images_from_a_file_still_pass_the_size_and_type_checks()
    {
        var chat = new SavedIntelligenceChat
        {
            Messages =
            [
                new() { IsUser = true, Text = "Question" },
                new()
                {
                    Text = "Answer", Outcome = IntelligenceReplyOutcome.Completed,
                    Images = [new IntelligenceImage(Png, "image/png"), new IntelligenceImage(Png, "image/svg+xml"), new IntelligenceImage([], "image/png")],
                },
            ],
        };

        Assert.Equal(Png, Assert.Single(Assert.Single(IntelligenceSavedChats.Turns(chat)).Images).Data);
    }

    [Fact]
    public async Task Chats_are_kept_per_server_and_another_server_never_shows_them()
    {
        Assert.NotEqual(IntelligenceSavedChats.Scope(ServerA), IntelligenceSavedChats.Scope(ServerB));
        Assert.Equal(IntelligenceSavedChats.Scope(ServerA),
            IntelligenceSavedChats.Scope(new LibreNmsConnection(new Uri("https://nms-a.example.net"), "other-token")));

        var app = Start();
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Question on server A"));
        await app.SettleAsync();

        SwitchTo(ServerB);
        Assert.Empty(app.Conversation.Turns);
        await app.Chats.LoadAsync();
        Assert.Empty(app.Chats.Chats);
        Assert.Empty(app.Conversation.Turns);
        Assert.True(await app.Conversation.AskAsync("Question on server B"));
        await app.SettleAsync();
        Assert.Equal("Question on server B", Assert.Single(app.Chats.Chats).Title);

        SwitchTo(ServerA);
        await app.Chats.LoadAsync();
        Assert.Equal("Question on server A", Assert.Single(app.Chats.Chats).Title);
        Assert.Equal("Question on server A", Assert.Single(app.Conversation.Turns).Question);

        Assert.Equal("Question on server A", Assert.Single((await ReadAsync(ServerA)).Chats).Title);
        Assert.Equal("Question on server B", Assert.Single((await ReadAsync(ServerB)).Chats).Title);
    }

    [Fact]
    public async Task Signing_out_drops_waiting_saves_and_the_next_session_sees_no_earlier_chat()
    {
        var app = Start();
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Question before sign-out"));
        await app.SettleAsync();

        // A draft change waiting on the debounce when the user signs out.
        app.Conversation.Draft = "draft-sentinel unsent";
        SwitchTo(null);
        var wipe = await app.Storage.WipeAsync();
        Assert.Equal(ChatWipeResult.Removed, wipe);
        _time.Advance(IntelligenceChatListViewModel.SaveDelay * 3);
        await app.Chats.SaveNowAsync();
        await app.SettleAsync();
        Assert.Empty(ChatFiles());
        Assert.Empty(app.Chats.Chats);

        SwitchTo(ServerA);
        await app.Chats.LoadAsync();
        Assert.Empty(app.Chats.Chats);
        Assert.Empty(app.Conversation.Turns);
        Assert.Equal("", app.Conversation.Draft);
        Assert.Empty((await ReadAsync(ServerA)).Chats);
    }

    [Fact]
    public async Task A_save_queued_before_a_wipe_is_refused_by_the_wiped_store()
    {
        var storage = new IntelligenceChatStorage(_root, _keys, new NoBackup(), NullLogger<IntelligenceChatStorage>.Instance);
        var stale = await storage.OpenAsync();
        Assert.NotNull(stale);
        var writer = new IntelligenceChatWriter(NullLogger.Instance);
        var failures = new List<bool>();
        writer.Failed += (_, unavailable) => { lock (failures) failures.Add(unavailable); };

        Assert.Equal(ChatWipeResult.Removed, await storage.WipeAsync());
        var saving = writer.Save(stale, IntelligenceSavedChats.Scope(ServerA), new SavedIntelligenceChat { Title = "late" }, makeActive: true);
        await writer.FlushAsync();

        Assert.Equal(ChatWriteResult.Unavailable, await saving);
        Assert.Empty(ChatFiles());
        Assert.Equal(new[] { true }, failures);
    }

    [Fact]
    public async Task Invalidating_drops_every_waiting_write_behind_one_in_progress()
    {
        var store = new GateStore();
        var writer = new IntelligenceChatWriter(NullLogger.Instance);
        var first = new SavedIntelligenceChat { Title = "first" };
        var started = writer.Save(store, "scope", first, makeActive: true);
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = writer.Save(store, "scope", new SavedIntelligenceChat { Title = "second" }, makeActive: true);
        var deleting = writer.Delete(store, "scope", first.Id);
        writer.Invalidate();
        Assert.Equal(0, writer.Pending);

        // Dropped operations say so at once; none claims to have reached the disk.
        Assert.Equal(ChatWriteResult.Cancelled, await second);
        Assert.Equal(ChatWriteResult.Cancelled, await deleting);
        store.Release.SetResult();
        await writer.FlushAsync();
        Assert.Equal(ChatWriteResult.Written, await started);
        Assert.Equal(new[] { "save first" }, store.Calls);

        // The dropped delete no longer refuses saves: the chat's file is still there.
        Assert.Equal(ChatWriteResult.Written, await writer.Save(store, "scope", new SavedIntelligenceChat { Id = first.Id, Title = "again" }, makeActive: true));
    }

    [Fact]
    public async Task Saves_of_one_chat_coalesce_so_only_the_latest_waits()
    {
        var store = new GateStore();
        var writer = new IntelligenceChatWriter(NullLogger.Instance);
        var chat = new SavedIntelligenceChat { Title = "v0" };
        var other = new SavedIntelligenceChat { Title = "other" };
        _ = writer.Save(store, "scope", chat, makeActive: true);
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var replaced = writer.Save(store, "scope", new SavedIntelligenceChat { Id = chat.Id, Title = "v1" }, makeActive: true);
        for (var i = 2; i <= 50; i++)
        {
            _ = writer.Save(store, "scope", new SavedIntelligenceChat { Id = chat.Id, Title = "v" + i }, makeActive: true);
        }

        Assert.Equal(ChatWriteResult.Superseded, await replaced);

        _ = writer.Save(store, "scope", other, makeActive: false);
        _ = writer.Save(store, "scope", new SavedIntelligenceChat { Id = chat.Id, Title = "v51" }, makeActive: true);

        // One waiting save per chat, however many arrived.
        Assert.Equal(2, writer.Pending);
        store.Release.SetResult();
        await writer.FlushAsync();
        Assert.Equal(new[] { "save v0", "save other", "save v51" }, store.Calls);
    }

    [Fact]
    public async Task A_delete_drops_waiting_saves_and_refuses_later_ones()
    {
        var store = new GateStore();
        var writer = new IntelligenceChatWriter(NullLogger.Instance);
        var blocker = new SavedIntelligenceChat { Title = "blocker" };
        var chat = new SavedIntelligenceChat { Title = "doomed" };
        _ = writer.Save(store, "scope", blocker, makeActive: true);
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var doomed = writer.Save(store, "scope", chat, makeActive: true);
        var deleting = writer.Delete(store, "scope", chat.Id);
        Assert.Equal(ChatWriteResult.Superseded, await doomed);
        Assert.Equal(ChatWriteResult.Cancelled, await writer.Save(store, "scope", new SavedIntelligenceChat { Id = chat.Id, Title = "late" }, makeActive: true));
        // The same chat in another server's scope is a different chat.
        var elsewhere = writer.Save(store, "other-scope", new SavedIntelligenceChat { Id = chat.Id, Title = "elsewhere" }, makeActive: true);
        Assert.False(deleting.IsCompleted);
        store.Release.SetResult();
        await writer.FlushAsync();
        Assert.Equal(ChatWriteResult.Written, await deleting);
        Assert.Equal(ChatWriteResult.Written, await elsewhere);
        Assert.Equal(new[] { "save blocker", "delete " + chat.Id, "save elsewhere" }, store.Calls);

        // A completed delete still refuses a stale snapshot.
        Assert.Equal(ChatWriteResult.Cancelled, await writer.Save(store, "scope", new SavedIntelligenceChat { Id = chat.Id, Title = "stale" }, makeActive: true));
    }

    [Fact]
    public async Task Chats_are_grouped_by_activity_with_pinned_first_and_newest_first_within_each_group()
    {
        var storage = new IntelligenceChatStorage(_root, _keys, new NoBackup(), NullLogger<IntelligenceChatStorage>.Instance);
        var store = await storage.OpenAsync();
        Assert.NotNull(store);
        var scope = IntelligenceSavedChats.Scope(ServerA);
        void Save(string title, DateTimeOffset at, bool pinned = false) =>
            store.Save(scope, new SavedIntelligenceChat { Title = title, UpdatedAt = at, IsPinned = pinned }, makeActive: false);
        Save("pinned old", new DateTimeOffset(2024, 1, 2, 9, 0, 0, TimeSpan.Zero), pinned: true);
        Save("today early", Now.AddHours(-3));
        Save("today late", Now.AddHours(-1));
        Save("yesterday", Now.AddDays(-1));
        Save("monday", new DateTimeOffset(2026, 11, 9, 8, 0, 0, TimeSpan.Zero));
        Save("last week", new DateTimeOffset(2026, 11, 3, 8, 0, 0, TimeSpan.Zero));
        Save("first of month", new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero));
        Save("august", new DateTimeOffset(2026, 8, 15, 8, 0, 0, TimeSpan.Zero));
        Save("last year", new DateTimeOffset(2025, 5, 1, 8, 0, 0, TimeSpan.Zero));

        var app = Start();
        await app.Chats.LoadAsync();
        Assert.Equal(
            new[] { "Pinned", "Today", "Yesterday", "This week", "Last week", "Earlier this month", new DateTime(2026, 8, 1).ToString("MMMM"), "2025" },
            app.Chats.Groups.Select(group => group.Label));
        Assert.Equal(new[] { "today late", "today early" }, app.Chats.Groups[1].Select(chat => chat.Title));
        Assert.Equal("pinned old", app.Chats.Chats[0].Title);
        Assert.Equal("last year", app.Chats.Chats[^1].Title);
    }

    [Fact]
    public async Task Rename_pin_and_confirmed_delete_are_saved_and_a_declined_delete_keeps_the_chat()
    {
        var app = Start();
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("First question"));
        Assert.True(await app.Chats.NewChatAsync());
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await app.Conversation.AskAsync("Second question"));
        await app.SettleAsync();
        Assert.Equal(new[] { "Second question", "First question" }, app.Chats.Chats.Select(chat => chat.Title));

        var first = app.Chats.Chats.Single(chat => chat.Title == "First question");
        Assert.True(app.Chats.Rename(first, "  Renamed \n chat  "));
        Assert.False(app.Chats.Rename(first, "   "));
        Assert.True(app.Chats.TogglePin(first));
        Assert.Equal("Pinned", app.Chats.Groups[0].Label);
        Assert.Same(first, app.Chats.Chats[0]);
        await app.SettleAsync();

        var restarted = Start();
        await restarted.Chats.LoadAsync();
        var renamed = restarted.Chats.Chats[0];
        Assert.Equal("Renamed chat", renamed.Title);
        Assert.True(renamed.IsPinned);
        Assert.True(renamed.Data.IsRenamed);
        // Renaming and pinning aren't activity: the open chat is still the second.
        Assert.Equal("Second question", Assert.Single(restarted.Conversation.Turns).Question);

        _dialogs.ConfirmDestructiveAsync(default!, default!, default!).ReturnsForAnyArgs(false);
        Assert.False(await restarted.Chats.DeleteAsync(renamed));
        Assert.Equal(2, restarted.Chats.Chats.Count);

        _dialogs.ConfirmDestructiveAsync(default!, default!, default!).ReturnsForAnyArgs(true);
        Assert.True(await restarted.Chats.DeleteAsync(renamed));
        await restarted.SettleAsync();
        Assert.Equal("Second question", Assert.Single((await ReadAsync(ServerA)).Chats).Title);
        _ = _dialogs.Received().ConfirmDestructiveAsync("Delete chat?", Arg.Is<string>(m => m.Contains("Renamed chat") && m.Contains(Confirmations.CannotBeUndone)), "Delete");
    }

    [Fact]
    public async Task Deleting_the_open_chat_clears_it_and_no_later_save_brings_it_back()
    {
        var app = Start();
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Question to delete"));
        await app.SettleAsync();
        var open = Assert.Single(app.Chats.Chats);

        // A draft save waiting on the debounce, then deleted.
        app.Conversation.Draft = "draft-sentinel";
        Assert.True(await app.Chats.DeleteAsync(open));
        Assert.Empty(app.Conversation.Turns);
        Assert.Equal("", app.Conversation.Draft);
        _time.Advance(IntelligenceChatListViewModel.SaveDelay * 3);
        await app.Chats.SaveNowAsync();
        await app.SettleAsync();

        Assert.Empty(app.Chats.Chats);
        Assert.Empty((await ReadAsync(ServerA)).Chats);
        Assert.Empty(ChatFiles());
    }

    [Fact]
    public async Task A_partial_answer_is_saved_while_streaming_and_reopens_as_interrupted()
    {
        var held = new TaskCompletionSource<IntelligenceAnswer>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<string>? stream = null;
        _model.Scripts.Enqueue((_, _, text, _) => { stream = text; text("Partial"); return held.Task; });
        var app = Start();
        await app.Chats.LoadAsync();
        var asking = app.Conversation.AskAsync("Question cut off");

        // The question was kept straight away; streaming waits for the next save.
        await app.SettleAsync();
        var saved = Assert.Single((await ReadAsync(ServerA)).Chats);
        Assert.Equal("Question cut off", saved.Messages[0].Text);
        Assert.Equal("", saved.Messages[1].Text);
        Assert.Equal(IntelligenceReplyOutcome.Generating, saved.Messages[1].Outcome);

        stream!("Partial answer so far");
        _time.Advance(IntelligenceChatListViewModel.SaveDelay - TimeSpan.FromMilliseconds(1));
        await app.SettleAsync();
        Assert.Equal("", Assert.Single((await ReadAsync(ServerA)).Chats).Messages[1].Text);
        _time.Advance(TimeSpan.FromMilliseconds(1));
        await app.SettleAsync();
        Assert.Equal("Partial answer so far", Assert.Single((await ReadAsync(ServerA)).Chats).Messages[1].Text);

        // The app is ended here, mid-answer: no completion, no suspension save.
        var restarted = Start();
        await restarted.Chats.LoadAsync();
        var turn = Assert.Single(restarted.Conversation.Turns);
        Assert.Equal("Question cut off", turn.Question);
        Assert.Equal("Partial answer so far", turn.Answer);
        Assert.Equal(IntelligenceTurnState.Interrupted, turn.State);
        Assert.Equal(IntelligenceSavedChats.ClosedMidAnswer, turn.Message);
        Assert.True(restarted.Conversation.CanAsk);

        app.Conversation.Stop();
        await asking.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Suspension_interrupts_then_saves_the_partial_answer()
    {
        var held = new TaskCompletionSource<IntelligenceAnswer>(TaskCreationOptions.RunContinuationsAsynchronously);
        _model.Scripts.Enqueue((_, _, text, _) => { text("Partial before suspension"); return held.Task; });
        var app = Start();
        await app.Chats.LoadAsync();
        var asking = app.Conversation.AskAsync("Question at suspension");
        await app.Conversation.InterruptAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await app.Chats.SaveNowAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await asking.WaitAsync(TimeSpan.FromSeconds(5));

        var answer = Assert.Single((await ReadAsync(ServerA)).Chats).Messages[1];
        Assert.Equal("Partial before suspension", answer.Text);
        Assert.Equal(IntelligenceReplyOutcome.Interrupted, answer.Outcome);
        Assert.False(answer.IsIncomplete);
    }

    [Fact]
    public async Task A_damaged_file_is_set_aside_counted_and_the_healthy_chats_load()
    {
        var app = Start();
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Healthy chat"));
        Assert.True(await app.Chats.NewChatAsync());
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await app.Conversation.AskAsync("Chat to damage"));
        Assert.True(await app.Chats.NewChatAsync());
        await app.SettleAsync();
        var damaged = app.Chats.Chats.Single(chat => chat.Title == "Chat to damage");
        var file = ChatFiles(damaged.Id.ToString("N") + ".chat").Single();
        var bytes = File.ReadAllBytes(file);
        bytes[^1] ^= 0xFF;
        File.WriteAllBytes(file, bytes);

        var restarted = Start();
        await restarted.Chats.LoadAsync();
        Assert.Equal("Healthy chat", Assert.Single(restarted.Chats.Chats).Title);
        Assert.Equal("One saved chat couldn't be read and was set aside. Your other chats are here.", restarted.Chats.Status);
        Assert.Single(ChatFiles("*.chat.bad"));
        Assert.True(restarted.Chats.CanPersist);
    }

    [Fact]
    public async Task An_unreadable_key_store_says_so_and_writes_nothing()
    {
        _keys.FailReads = true;
        var app = Start();
        await app.Chats.LoadAsync();
        Assert.False(app.Chats.CanPersist);
        Assert.False(app.Chats.IsListAvailable);
        Assert.Equal(app.Storage.UnavailableReason, app.Chats.Status);
        Assert.Contains("secure storage", app.Chats.Status);

        Assert.True(await app.Conversation.AskAsync("Question while locked"));
        app.Conversation.Draft = "draft-sentinel";
        _time.Advance(IntelligenceChatListViewModel.SaveDelay * 2);
        await app.Chats.SaveNowAsync();
        Assert.Empty(ChatFiles());
        Assert.Empty(app.Chats.Chats);
        Assert.False(app.Chats.KeepsCurrentChat);
    }

    [Fact]
    public async Task Enabling_single_chat_mode_discards_saves_already_waiting_on_the_writer()
    {
        var app = Start();
        await app.Chats.LoadAsync();
        var store = new GateStore();
        _ = app.Chats.Writer.Save(store, "scope", new SavedIntelligenceChat { Title = "already started" }, true);
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            _ = app.Chats.Writer.Save(store, "scope", new SavedIntelligenceChat { Title = "waiting snapshot" }, true);
            Assert.Equal(1, app.Chats.Writer.Pending);
            _preferences.SetSingleChatMode(true);
            Assert.Equal(0, app.Chats.Writer.Pending);
        }
        finally { store.Release.TrySetResult(); }
        await app.SettleAsync();
        Assert.Equal(new[] { "save already started" }, store.Calls);
        Assert.False(app.Chats.CanPersist);
    }

    [Fact]
    public async Task Single_chat_mode_writes_nothing_hides_the_list_and_keeps_old_chats()
    {
        var app = Start();
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Saved before single-chat mode"));
        await app.SettleAsync();

        _preferences.SetSingleChatMode(true);
        Assert.True(app.Chats.IsSingleChatMode);
        Assert.False(app.Chats.IsListAvailable);
        Assert.Empty(app.Chats.Chats);
        Assert.Equal(IntelligenceChatListViewModel.SingleChatStatus, app.Chats.Status);

        // Private text added to the chat that was open, by every route that saves.
        _model.Scripts.Enqueue((_, _, text, _) => { text("private-sentinel partial"); return Task.FromResult(new IntelligenceAnswer("private-sentinel answer", null)); });
        Assert.True(await app.Conversation.AskAsync("private-sentinel question"));
        app.Conversation.Draft = "private-sentinel draft";
        _time.Advance(IntelligenceChatListViewModel.SaveDelay * 3);
        await app.Chats.SaveNowAsync();
        await app.SettleAsync();

        var saved = Assert.Single((await ReadAsync(ServerA)).Chats);
        Assert.Equal(2, saved.Messages.Count);
        Assert.DoesNotContain(saved.Messages, message => message.Text.Contains("private-sentinel"));
        Assert.DoesNotContain("private-sentinel", saved.Draft);

        // Leaving the mode shows the old chat again, but the private transcript stays unsaved.
        _preferences.SetSingleChatMode(false);
        await app.Chats.LoadAsync();
        Assert.Single(app.Chats.Chats);
        Assert.Equal(IntelligenceChatListViewModel.UnsavedStatus, app.Chats.Status);
        Assert.False(app.Chats.KeepsCurrentChat);
        app.Conversation.Draft = "private-sentinel more";
        _time.Advance(IntelligenceChatListViewModel.SaveDelay * 2);
        await app.Chats.SaveNowAsync();
        Assert.Single((await ReadAsync(ServerA)).Chats);

        // A new chat saves again.
        Assert.True(await app.Chats.NewChatAsync());
        Assert.True(await app.Conversation.AskAsync("Saved after single-chat mode"));
        await app.SettleAsync();
        Assert.Equal(2, (await ReadAsync(ServerA)).Chats.Count);
        Assert.Equal("", app.Chats.Status);
    }

    [Fact]
    public async Task Starting_in_single_chat_mode_reads_and_writes_nothing()
    {
        _settings.IntelligenceSingleChatMode = true;
        var app = Start();
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("private-sentinel question"));
        await app.Chats.SaveNowAsync();
        Assert.Equal(IntelligenceChatStorageState.Closed, app.Storage.State);
        Assert.Empty(ChatFiles());
    }

    [Fact]
    public async Task Single_chat_mode_hands_the_store_nothing_across_streaming_lookups_images_drafts_detail_and_suspension()
    {
        // UI work is posted and run only when the test says, so the setting's
        // clean-up can be held back while saves that were already due run first.
        var posted = new Queue<Action>();
        void RunPosted() { while (posted.TryDequeue(out var action)) action(); }
        var app = Start(posted.Enqueue);
        var faults = Faulty(app);
        await app.Chats.LoadAsync();
        RunPosted();
        Assert.True(await app.Conversation.AskAsync("Saved before single-chat mode"));
        await app.SettleAsync();
        RunPosted();
        var before = faults.Writes;
        Assert.True(before > 0);

        // A draft change is waiting on the debounce when the mode goes on, and
        // the list hasn't handled the change yet.
        app.Conversation.Draft = "private-sentinel draft typed just before";
        _preferences.SetSingleChatMode(true);
        Assert.False(app.Chats.CanPersist);
        Assert.Equal(0, app.Chats.Writer.Pending);
        _time.Advance(IntelligenceChatListViewModel.SaveDelay * 2);
        RunPosted();

        // A streamed answer with a lookup and an image, a detail change and a
        // draft, the debounce and suspension, before and after the change is handled.
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _model.Scripts.Enqueue(async (_, tools, text, token) =>
        {
            await tools[0].Run("", token);
            text("private-sentinel partial");
            await release.Task;
            return new IntelligenceAnswer("private-sentinel answer", null) { Images = [new IntelligenceImage(Png, "image/png")] };
        });
        var asking = app.Conversation.AskAsync("private-sentinel question");
        _time.Advance(IntelligenceChatListViewModel.SaveDelay * 2);
        RunPosted();
        app.Conversation.SelectedDetail = IntelligenceDetail.Detailed;
        app.Conversation.Draft = "private-sentinel draft";
        _time.Advance(IntelligenceChatListViewModel.SaveDelay * 2);
        await app.Chats.SaveNowAsync();
        release.SetResult();
        Assert.True(await asking);
        RunPosted();
        _time.Advance(IntelligenceChatListViewModel.SaveDelay * 2);
        RunPosted();
        await app.Chats.SaveNowAsync();
        await app.SettleAsync();
        RunPosted();

        Assert.Equal(before, faults.Writes);
        var saved = Assert.Single((await ReadAsync(ServerA)).Chats);
        Assert.Equal("Saved before single-chat mode", saved.Title);
        Assert.DoesNotContain(saved.Messages, message => message.Text.Contains("private-sentinel"));
        Assert.DoesNotContain("private-sentinel", saved.Draft ?? "");
        Assert.All(saved.Messages, message => Assert.Empty(message.Images ?? []));

        // Leaving the mode: the old chat is listed again, still without the private text.
        _preferences.SetSingleChatMode(false);
        RunPosted();
        await app.Chats.LoadAsync();
        RunPosted();
        app.Conversation.Draft = "private-sentinel after leaving";
        _time.Advance(IntelligenceChatListViewModel.SaveDelay * 2);
        RunPosted();
        await app.Chats.SaveNowAsync();
        RunPosted();
        var after = Assert.Single((await ReadAsync(ServerA)).Chats);
        Assert.Equal(saved.Id, after.Id);
        Assert.DoesNotContain(after.Messages, message => message.Text.Contains("private-sentinel"));
        Assert.DoesNotContain("private-sentinel", after.Draft ?? "");
        Assert.Single(app.Chats.Chats);
    }

    [Fact]
    public async Task Single_chat_mode_keeps_saved_chats_until_delete_saved_chats_removes_them_all()
    {
        var app = Start();
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Chat on server A"));
        await app.SettleAsync();
        SwitchTo(ServerB);
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Chat on server B"));
        await app.SettleAsync();

        // Turning the mode on deletes nothing.
        _preferences.SetSingleChatMode(true);
        Assert.Single((await ReadAsync(ServerA)).Chats);
        Assert.Single((await ReadAsync(ServerB)).Chats);
        _model.Scripts.Enqueue((_, _, _, _) => Task.FromResult(new IntelligenceAnswer("private-sentinel answer", null)));
        Assert.True(await app.Conversation.AskAsync("private-sentinel question"));

        Assert.Equal(ChatWipeResult.Removed, await app.Chats.DeleteSavedChatsAsync());
        Assert.Empty(ChatFiles());
        Assert.False(_keys.Values.ContainsKey(IntelligenceChatStorage.KeyName));
        Assert.False(File.Exists(_root + ".cleanup-pending"));
        Assert.Equal(IntelligenceChatListViewModel.SingleChatStatus, app.Chats.Status);

        // The private chat is still on screen, and still written nowhere.
        Assert.Equal("private-sentinel question", app.Conversation.Turns[^1].Question);
        app.Conversation.Draft = "private-sentinel draft";
        _time.Advance(IntelligenceChatListViewModel.SaveDelay * 2);
        await app.Chats.SaveNowAsync();
        Assert.Empty(ChatFiles());
        Assert.Equal(IntelligenceChatStorageState.Closed, app.Storage.State);

        // Leaving the mode lists nothing, keeps the private chat unsaved, and a new chat saves under a new key.
        _preferences.SetSingleChatMode(false);
        await app.Chats.LoadAsync();
        Assert.Empty(app.Chats.Chats);
        Assert.Equal(IntelligenceChatListViewModel.UnsavedStatus, app.Chats.Status);
        await app.Chats.SaveNowAsync();
        Assert.Empty((await ReadAsync(ServerB)).Chats);
        Assert.True(await app.Chats.NewChatAsync());
        Assert.True(await app.Conversation.AskAsync("Saved after deleting"));
        await app.SettleAsync();
        Assert.Equal("Saved after deleting", Assert.Single((await ReadAsync(ServerB)).Chats).Title);
        Assert.Empty((await ReadAsync(ServerA)).Chats);
    }

    [Fact]
    public async Task Delete_saved_chats_drops_waiting_and_running_saves_and_never_resaves_the_open_chat()
    {
        var app = Start();
        var faults = Faulty(app);
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Saved history"));
        await app.SettleAsync();

        // One save under way and held, another waiting behind it.
        var running = faults.HoldNext();
        app.Conversation.Draft = "draft-sentinel running";
        _time.Advance(IntelligenceChatListViewModel.SaveDelay);
        await running.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<ChatWipeResult> deleting;
        try
        {
            app.Conversation.Draft = "draft-sentinel waiting";
            _time.Advance(IntelligenceChatListViewModel.SaveDelay);
            Assert.Equal(1, app.Chats.Writer.Pending);

            // The held save keeps the store it was queued with; the list's
            // reload after deleting opens its own, unwrapped.
            app.Chats.WrapStore = null;
            deleting = app.Chats.DeleteSavedChatsAsync();
            Assert.Equal(0, app.Chats.Writer.Pending);
            Assert.Empty(app.Chats.Chats);
            Assert.Null(app.Chats.Active);
        }
        finally { running.Release.TrySetResult(); }

        Assert.Equal(ChatWipeResult.Removed, await deleting.WaitAsync(TimeSpan.FromSeconds(10)));
        await app.SettleAsync();
        Assert.Empty(ChatFiles());

        // The chat on screen stays, but none of it becomes a saved chat again.
        Assert.Equal("Saved history", Assert.Single(app.Conversation.Turns).Question);
        Assert.Equal("draft-sentinel waiting", app.Conversation.Draft);
        await app.Chats.LoadAsync();
        Assert.Equal(IntelligenceChatListViewModel.DeletedUnsavedStatus, app.Chats.Status);
        Assert.False(app.Chats.KeepsCurrentChat);
        Assert.True(await app.Conversation.AskAsync("Follow-up after deleting"));
        app.Conversation.Draft = "draft-sentinel after";
        _time.Advance(IntelligenceChatListViewModel.SaveDelay * 2);
        await app.Chats.SaveNowAsync();
        Assert.Empty((await ReadAsync(ServerA)).Chats);

        // A new chat is saved again, and only it.
        Assert.True(await app.Chats.NewChatAsync());
        Assert.True(await app.Conversation.AskAsync("New after deleting"));
        await app.SettleAsync();
        var only = Assert.Single((await ReadAsync(ServerA)).Chats);
        Assert.Equal("New after deleting", only.Title);
        Assert.DoesNotContain(only.Messages, message => message.Text.Contains("Saved history"));
        Assert.Equal("", app.Chats.Status);
    }

    [Fact]
    public async Task A_delete_saved_chats_that_cannot_finish_says_so_and_trying_again_finishes_it()
    {
        var app = Start();
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Saved history"));
        await app.SettleAsync();

        _keys.FailRemove = true;
        Assert.Equal(ChatWipeResult.Retained, await app.Chats.DeleteSavedChatsAsync());
        Assert.True(File.Exists(_root + ".cleanup-pending"));
        Assert.Equal(IntelligenceChatStorageState.Unavailable, app.Storage.State);
        Assert.Equal(app.Storage.UnavailableReason, app.Chats.Status);
        Assert.Contains("stay locked", app.Chats.Status);
        Assert.False(app.Chats.CanPersist);
        // Nothing is written while it is locked.
        app.Conversation.Draft = "draft-sentinel while locked";
        _time.Advance(IntelligenceChatListViewModel.SaveDelay * 2);
        await app.Chats.SaveNowAsync();
        Assert.Empty(ChatFiles());

        _keys.FailRemove = false;
        Assert.Equal(ChatWipeResult.Removed, await app.Chats.DeleteSavedChatsAsync());
        Assert.False(File.Exists(_root + ".cleanup-pending"));
        Assert.False(_keys.Values.ContainsKey(IntelligenceChatStorage.KeyName));
        await app.Chats.LoadAsync();
        Assert.True(app.Chats.CanPersist);
        Assert.Empty(app.Chats.Chats);
        Assert.Equal(IntelligenceChatListViewModel.DeletedUnsavedStatus, app.Chats.Status);
    }

    [Fact]
    public async Task Draft_typing_is_debounced_and_unchanged_chats_are_not_rewritten()
    {
        var app = Start();
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Question"));
        await app.SettleAsync();

        for (var i = 0; i < 10; i++)
        {
            app.Conversation.Draft = "draft " + i;
            _time.Advance(IntelligenceChatListViewModel.SaveDelay / 2);
        }

        await app.SettleAsync();
        Assert.Equal("", Assert.Single((await ReadAsync(ServerA)).Chats).Draft);
        _time.Advance(IntelligenceChatListViewModel.SaveDelay);
        await app.SettleAsync();
        Assert.Equal("draft 9", Assert.Single((await ReadAsync(ServerA)).Chats).Draft);

        // Nothing changed: nothing queued.
        await app.Chats.SaveNowAsync();
        Assert.Equal(0, app.Chats.Writer.Pending);
    }

    [Fact]
    public async Task Changes_made_while_chats_load_are_saved_once_loading_finishes()
    {
        var app = Start();
        var faults = Faulty(app);
        var reading = faults.HoldNext();
        var loading = app.Chats.LoadAsync();
        await reading.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(app.Chats.CanPersist);

        // Asked, answered and a draft typed while the list was still being read.
        Assert.True(await app.Conversation.AskAsync("Question while loading"));
        app.Conversation.Draft = "draft-sentinel while loading";
        reading.Release.SetResult();
        await loading.WaitAsync(TimeSpan.FromSeconds(5));
        await app.SettleAsync();

        var saved = Assert.Single((await ReadAsync(ServerA)).Chats);
        Assert.Equal("Question while loading", saved.Messages[0].Text);
        Assert.Equal("Answer", saved.Messages[1].Text);
        Assert.Equal(IntelligenceReplyOutcome.Completed, saved.Messages[1].Outcome);
        Assert.Equal("draft-sentinel while loading", saved.Draft);
        Assert.Same(Assert.Single(app.Chats.Chats), app.Chats.Active);
        Assert.True(app.Chats.KeepsCurrentChat);
    }

    [Fact]
    public async Task Suspension_waits_for_inflight_loading_before_capturing_the_new_chat()
    {
        var app = Start();
        var faults = Faulty(app);
        var reading = faults.HoldNext();
        var loading = app.Chats.LoadAsync();
        await reading.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(await app.Conversation.AskAsync("Question before load finishes"));
        app.Conversation.Draft = "draft at suspension";
        var saving = app.Chats.SaveNowAsync();
        Assert.False(saving.IsCompleted);
        Assert.Empty(ChatFiles());
        reading.Release.SetResult();
        await saving.WaitAsync(TimeSpan.FromSeconds(5));
        await loading;
        var saved = Assert.Single((await ReadAsync(ServerA)).Chats);
        Assert.Equal("Question before load finishes", saved.Messages[0].Text);
        Assert.Equal("draft at suspension", saved.Draft);
    }

    [Fact]
    public async Task A_delete_that_fails_keeps_the_chat_listed_never_reports_success_and_can_be_retried()
    {
        var app = Start();
        var faults = Faulty(app);
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Chat to keep"));
        Assert.True(await app.Chats.NewChatAsync());
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await app.Conversation.AskAsync("Chat to delete"));
        await app.SettleAsync();
        var doomed = app.Chats.Chats.Single(chat => chat.Title == "Chat to delete");
        Assert.Same(doomed, app.Chats.Active);

        faults.FailDeletes = true;
        Assert.False(await app.Chats.DeleteAsync(doomed));
        Assert.Contains(doomed, app.Chats.Chats);
        Assert.Equal(IntelligenceChatListViewModel.DeleteFailedStatus, app.Chats.Status);
        Assert.Equal(2, (await ReadAsync(ServerA)).Chats.Count);
        // The open chat was cleared first, so nothing typed meanwhile went into it.
        Assert.Empty(app.Conversation.Turns);

        // Still a saved chat: it reopens, and a pin is written rather than refused as deleted.
        Assert.True(app.Chats.TogglePin(doomed));
        await app.SettleAsync();
        Assert.True((await ReadAsync(ServerA)).Chats.Single(chat => chat.Id == doomed.Id).IsPinned);
        Assert.True(await app.Chats.OpenAsync(doomed));
        Assert.Equal("Chat to delete", Assert.Single(app.Conversation.Turns).Question);

        faults.FailDeletes = false;
        Assert.True(await app.Chats.DeleteAsync(doomed));
        Assert.DoesNotContain(doomed, app.Chats.Chats);
        Assert.Equal("", app.Chats.Status);
        await app.SettleAsync();
        Assert.Equal("Chat to keep", Assert.Single((await ReadAsync(ServerA)).Chats).Title);
    }

    [Fact]
    public async Task A_queued_delete_dropped_by_single_chat_mode_never_reports_success_and_can_be_retried()
    {
        var app = Start();
        var faults = Faulty(app);
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Chat to keep"));
        Assert.True(await app.Chats.NewChatAsync());
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await app.Conversation.AskAsync("Chat to delete"));
        Assert.True(await app.Chats.NewChatAsync());
        await app.SettleAsync();
        var keep = app.Chats.Chats.Single(chat => chat.Title == "Chat to keep");
        var doomed = app.Chats.Chats.Single(chat => chat.Title == "Chat to delete");

        // The writer is busy, so the confirmed delete waits behind it.
        var busy = faults.HoldNext();
        Assert.True(app.Chats.TogglePin(keep));
        await busy.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<bool> deleting;
        try
        {
            deleting = app.Chats.DeleteAsync(doomed);
            Assert.False(deleting.IsCompleted);
            Assert.Contains(doomed, app.Chats.Chats);
            _preferences.SetSingleChatMode(true);
            Assert.False(await deleting.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally { busy.Release.TrySetResult(); }

        await app.SettleAsync();
        Assert.Equal(2, (await ReadAsync(ServerA)).Chats.Count);

        // Back from single-chat mode the chat is listed again, and deleting it works.
        _preferences.SetSingleChatMode(false);
        await app.Chats.LoadAsync();
        var back = app.Chats.Chats.Single(chat => chat.Id == doomed.Id);
        Assert.True(await app.Chats.DeleteAsync(back));
        Assert.Equal("Chat to keep", Assert.Single((await ReadAsync(ServerA)).Chats).Title);
    }

    [Fact]
    public async Task A_failed_save_is_retried_unchanged_and_a_new_chat_asks_first_until_it_is_written()
    {
        var app = Start();
        var faults = Faulty(app);
        await app.Chats.LoadAsync();

        faults.FailSaves = true;
        Assert.True(await app.Conversation.AskAsync("Question not yet saved"));
        await app.Chats.SaveNowAsync();
        Assert.Empty((await ReadAsync(ServerA)).Chats);
        Assert.Equal(IntelligenceChatListViewModel.SaveFailedStatus, app.Chats.Status);
        // Not on disk, so starting a new chat must ask before clearing it.
        Assert.False(app.Chats.KeepsCurrentChat);

        // Still failing: still not treated as saved.
        await app.Chats.SaveNowAsync();
        Assert.False(app.Chats.KeepsCurrentChat);

        // Nothing changed on screen, but suspension writes it again.
        faults.FailSaves = false;
        await app.Chats.SaveNowAsync();
        var saved = Assert.Single((await ReadAsync(ServerA)).Chats);
        Assert.Equal("Question not yet saved", saved.Messages[0].Text);
        Assert.Equal("Answer", saved.Messages[1].Text);
        Assert.True(app.Chats.KeepsCurrentChat);
        Assert.Equal("", app.Chats.Status);

        // And a failure on the next edit is retried by that edit's own save once the disk recovers.
        faults.FailSaves = true;
        app.Conversation.Draft = "draft-sentinel retry";
        _time.Advance(IntelligenceChatListViewModel.SaveDelay);
        await app.SettleAsync();
        await app.Chats.SaveNowAsync();
        Assert.False(app.Chats.KeepsCurrentChat);
        faults.FailSaves = false;
        await app.Chats.SaveNowAsync();
        Assert.Equal("draft-sentinel retry", Assert.Single((await ReadAsync(ServerA)).Chats).Draft);
        Assert.True(app.Chats.KeepsCurrentChat);
    }

    [Fact]
    public async Task Text_from_a_reconnect_in_single_chat_mode_stays_unsaved_after_leaving_it()
    {
        var app = Start();
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Saved before single-chat mode"));
        await app.SettleAsync();

        _preferences.SetSingleChatMode(true);
        // Reconnecting clears the transcript and moves its revision on.
        SwitchTo(ServerA);
        Assert.Empty(app.Conversation.Turns);
        Assert.True(await app.Conversation.AskAsync("private-sentinel question"));

        _preferences.SetSingleChatMode(false);
        await app.Chats.LoadAsync();
        Assert.Equal(IntelligenceChatListViewModel.UnsavedStatus, app.Chats.Status);
        Assert.False(app.Chats.KeepsCurrentChat);
        app.Conversation.Draft = "private-sentinel draft";
        _time.Advance(IntelligenceChatListViewModel.SaveDelay * 2);
        await app.Chats.SaveNowAsync();

        var saved = Assert.Single((await ReadAsync(ServerA)).Chats);
        Assert.Equal("Saved before single-chat mode", saved.Title);
        Assert.DoesNotContain(saved.Messages, message => message.Text.Contains("private-sentinel"));
        Assert.DoesNotContain("private-sentinel", saved.Draft ?? "");
        Assert.Single(app.Chats.Chats);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Editing_during_a_save_wait_cancels_navigation_before_new_content_can_be_cleared(bool detail)
    {
        var app = Start();
        var faults = Faulty(app);
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Question on screen"));
        await app.SettleAsync();
        var held = faults.HoldNext();
        app.Conversation.Draft = "snapshot draft";
        var leaving = app.Chats.NewChatAsync();
        await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (detail) app.Conversation.SelectedDetail = "detailed";
        else app.Conversation.Draft = "new edit during save";
        held.Release.SetResult();

        Assert.False(await leaving.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal("Question on screen", Assert.Single(app.Conversation.Turns).Question);
        Assert.Equal(detail ? "snapshot draft" : "new edit during save", app.Conversation.Draft);
        _ = _dialogs.DidNotReceiveWithAnyArgs().ChooseAsync(default!, default!, default!);
    }

    [Fact]
    public async Task Leaving_a_pending_save_explains_that_saved_history_can_still_remain()
    {
        var app = Start();
        var faults = Faulty(app);
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Saved question"));
        await app.SettleAsync();
        var held = faults.HoldNext();
        app.Conversation.Draft = "pending draft";
        _dialogs.ChooseAsync(default!, default!, default!).ReturnsForAnyArgs(IntelligenceChatListViewModel.LeaveChoice);
        var leaving = app.Chats.NewChatAsync();
        await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        _time.Advance(IntelligenceChatListViewModel.LeaveWait);
        Assert.True(await leaving.WaitAsync(TimeSpan.FromSeconds(5)));
        _ = _dialogs.Received(1).ChooseAsync(Arg.Is<string>(message => message.Contains("pending save may still finish")
            && message.Contains("earlier version may remain")), Arg.Any<IReadOnlyList<string>>(), IntelligenceChatListViewModel.LeaveChoice);
        held.Release.SetResult();
        await app.SettleAsync();
        Assert.Equal("pending draft", Assert.Single((await ReadAsync(ServerA)).Chats).Draft);
    }

    [Fact]
    public async Task A_new_chat_while_the_latest_save_is_held_past_the_wait_asks_and_a_later_failure_is_never_discarded_silently()
    {
        var app = Start();
        var faults = Faulty(app);
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Question on screen"));
        await app.SettleAsync();

        // The next write - the draft's - is held past the wait, then fails.
        var held = faults.HoldNext();
        faults.FailSaves = true;
        app.Conversation.Draft = "draft-sentinel unsaved";
        _dialogs.ChooseAsync(default!, default!, default!).ReturnsForAnyArgs(Task.FromResult<string?>(null));
        var leaving = app.Chats.NewChatAsync();
        await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(leaving.IsCompleted);
        Assert.False(app.Chats.KeepsCurrentChat);
        _time.Advance(IntelligenceChatListViewModel.LeaveWait);

        // Still saving when the wait ends: asked, never counted as kept, and Cancel keeps everything.
        Assert.False(await leaving.WaitAsync(TimeSpan.FromSeconds(5)));
        _ = _dialogs.Received(1).ChooseAsync(IntelligenceChatListViewModel.SavePendingQuestion,
            Arg.Is<IReadOnlyList<string>>(options => options.SequenceEqual(new[] { IntelligenceChatListViewModel.TryAgainChoice })),
            IntelligenceChatListViewModel.LeaveChoice);
        Assert.Equal("Question on screen", Assert.Single(app.Conversation.Turns).Question);
        Assert.Equal("draft-sentinel unsaved", app.Conversation.Draft);
        Assert.NotNull(app.Chats.Active);

        held.Release.SetResult();
        await app.SettleAsync();
        Assert.Equal("", Assert.Single((await ReadAsync(ServerA)).Chats).Draft);
        Assert.False(app.Chats.KeepsCurrentChat);

        // The held save failed: the next New chat asks as a failure, Try again fails again and
        // asks again, and only an explicit discard clears it.
        _dialogs.ClearReceivedCalls();
        _dialogs.ChooseAsync(default!, default!, default!).ReturnsForAnyArgs(
            Task.FromResult<string?>(IntelligenceChatListViewModel.TryAgainChoice),
            Task.FromResult<string?>(IntelligenceChatListViewModel.LeaveChoice));
        Assert.True(await app.Chats.NewChatAsync());
        _ = _dialogs.Received(2).ChooseAsync(IntelligenceChatListViewModel.SaveFailedQuestion,
            Arg.Any<IReadOnlyList<string>>(), IntelligenceChatListViewModel.LeaveChoice);
        Assert.Empty(app.Conversation.Turns);
        Assert.Equal("", app.Conversation.Draft);
    }

    [Fact]
    public async Task Opening_another_chat_while_the_latest_save_fails_asks_and_opens_only_once_it_is_written()
    {
        var app = Start();
        var faults = Faulty(app);
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Chat to open"));
        Assert.True(await app.Chats.NewChatAsync());
        _time.Advance(TimeSpan.FromMinutes(1));
        Assert.True(await app.Conversation.AskAsync("Chat on screen"));
        await app.SettleAsync();
        var target = app.Chats.Chats.Single(chat => chat.Title == "Chat to open");
        var onScreen = app.Chats.Chats.Single(chat => chat.Title == "Chat on screen");

        // Saved chats move between each other without a question.
        Assert.True(await app.Chats.OpenAsync(target));
        Assert.True(await app.Chats.OpenAsync(onScreen));
        Assert.Equal("Chat on screen", Assert.Single(app.Conversation.Turns).Question);
        _ = _dialogs.DidNotReceiveWithAnyArgs().ChooseAsync(default!, default!, default!);
        _ = _dialogs.DidNotReceiveWithAnyArgs().ConfirmDestructiveAsync(default!, default!, default!);

        faults.FailSaves = true;
        app.Conversation.Draft = "draft-sentinel unsaved";
        _dialogs.ChooseAsync(default!, default!, default!).ReturnsForAnyArgs(Task.FromResult<string?>(null));
        Assert.False(await app.Chats.OpenAsync(target));
        _ = _dialogs.Received(1).ChooseAsync(IntelligenceChatListViewModel.SaveFailedQuestion,
            Arg.Any<IReadOnlyList<string>>(), IntelligenceChatListViewModel.LeaveChoice);
        Assert.False(app.Chats.KeepsCurrentChat);
        Assert.Same(onScreen, app.Chats.Active);
        Assert.Equal("Chat on screen", Assert.Single(app.Conversation.Turns).Question);
        Assert.Equal("draft-sentinel unsaved", app.Conversation.Draft);

        // The disk recovers: the next attempt writes it first, then opens the other chat without asking.
        faults.FailSaves = false;
        _dialogs.ClearReceivedCalls();
        Assert.True(await app.Chats.OpenAsync(target));
        _ = _dialogs.DidNotReceiveWithAnyArgs().ChooseAsync(default!, default!, default!);
        Assert.Same(target, app.Chats.Active);
        Assert.Equal("Chat to open", Assert.Single(app.Conversation.Turns).Question);
        await app.SettleAsync();
        Assert.Equal("draft-sentinel unsaved", (await ReadAsync(ServerA)).Chats.Single(chat => chat.Id == onScreen.Id).Draft);
    }

    [Fact]
    public async Task Opening_a_saved_chat_after_single_chat_mode_asks_first_and_declining_keeps_the_private_chat()
    {
        var app = Start();
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Saved before single-chat mode"));
        await app.SettleAsync();

        _preferences.SetSingleChatMode(true);
        Assert.True(await app.Chats.NewChatAsync());
        _model.Scripts.Enqueue((_, _, _, _) => Task.FromResult(new IntelligenceAnswer("private-sentinel answer", null)));
        Assert.True(await app.Conversation.AskAsync("private-sentinel question"));
        app.Conversation.Draft = "private-sentinel draft";
        _preferences.SetSingleChatMode(false);
        await app.Chats.LoadAsync();
        var saved = Assert.Single(app.Chats.Chats);

        void AssertPrivateChatKept()
        {
            var turn = Assert.Single(app.Conversation.Turns);
            Assert.Equal("private-sentinel question", turn.Question);
            Assert.Equal("private-sentinel answer", turn.Answer);
            Assert.Equal("private-sentinel draft", app.Conversation.Draft);
            Assert.Null(app.Chats.Active);
        }

        // Opening history and New chat both ask; declining keeps the private chat on screen.
        _dialogs.ClearReceivedCalls();
        _dialogs.ConfirmDestructiveAsync(default!, default!, default!).ReturnsForAnyArgs(false);
        Assert.False(await app.Chats.OpenAsync(saved));
        _ = _dialogs.Received(1).ConfirmDestructiveAsync("Open “Saved before single-chat mode”?",
            IntelligenceChatListViewModel.NotSavedMessage, IntelligenceChatListViewModel.DiscardChoice);
        AssertPrivateChatKept();
        Assert.False(await app.Chats.NewChatAsync());
        _ = _dialogs.Received(1).ConfirmDestructiveAsync("Start a new chat?",
            IntelligenceChatListViewModel.NotSavedMessage, IntelligenceChatListViewModel.DiscardChoice);
        AssertPrivateChatKept();

        // An answer given after the scope moved on is not acted on.
        var answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _dialogs.ConfirmDestructiveAsync(default!, default!, default!).ReturnsForAnyArgs(answer.Task);
        var opening = app.Chats.OpenAsync(saved);
        Assert.False(await app.Chats.NewChatAsync());
        _preferences.SetSingleChatMode(true);
        answer.SetResult(true);
        Assert.False(await opening.WaitAsync(TimeSpan.FromSeconds(5)));
        AssertPrivateChatKept();

        // Confirming discards it and opens the saved chat, whose file never sees the private text.
        _preferences.SetSingleChatMode(false);
        await app.Chats.LoadAsync();
        saved = Assert.Single(app.Chats.Chats);
        _dialogs.ConfirmDestructiveAsync(default!, default!, default!).ReturnsForAnyArgs(true);
        Assert.True(await app.Chats.OpenAsync(saved));
        Assert.Same(saved, app.Chats.Active);
        Assert.Equal("Saved before single-chat mode", Assert.Single(app.Conversation.Turns).Question);
        await app.SettleAsync();
        var file = Assert.Single((await ReadAsync(ServerA)).Chats);
        Assert.DoesNotContain(file.Messages, message => message.Text.Contains("private-sentinel"));
        Assert.DoesNotContain("private-sentinel", file.Draft ?? "");
    }

    [Fact]
    public async Task A_chat_without_its_own_detail_follows_the_settings_default_and_asks_with_it()
    {
        var app = Start();
        _preferences.SetDetail(IntelligenceDetail.Brief);
        Assert.Equal(IntelligenceDetail.Brief, app.Conversation.SelectedDetail);

        // Changed in Settings while the chat had no pick of its own: the next answer uses the new default.
        _preferences.SetDetail(IntelligenceDetail.Detailed);
        Assert.Equal(IntelligenceDetail.Detailed, app.Conversation.SelectedDetail);
        Assert.True(await app.Conversation.AskAsync("Question at the default"));
        Assert.Equal(IntelligenceDetail.Detailed, Assert.Single(app.Conversation.Turns).Detail);

        // The chat's own pick stays, whatever the default does.
        app.Conversation.SelectedDetail = IntelligenceDetail.Brief;
        _preferences.SetDetail(IntelligenceDetail.Balanced);
        Assert.Equal(IntelligenceDetail.Brief, app.Conversation.SelectedDetail);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restart_preserves_whether_detail_is_explicit_or_follows_settings(bool explicitPick)
    {
        _preferences.SetDetail(IntelligenceDetail.Brief);
        var app = Start();
        await app.Chats.LoadAsync();
        if (explicitPick) app.Conversation.SelectedDetail = IntelligenceDetail.Brief;
        Assert.True(await app.Conversation.AskAsync("Saved detail"));
        await app.Chats.SaveNowAsync();
        var saved = Assert.Single((await ReadAsync(ServerA)).Chats);
        Assert.Equal(explicitPick ? IntelligenceDetail.Brief : null, saved.ReplyStyle);

        _preferences.SetDetail(IntelligenceDetail.Detailed);
        var restarted = Start();
        await restarted.Chats.LoadAsync();
        Assert.Equal(explicitPick ? IntelligenceDetail.Brief : IntelligenceDetail.Detailed, restarted.Conversation.SelectedDetail);
    }

    [Fact]
    public async Task Suspension_retries_the_active_failure_before_its_UI_acknowledgement_runs()
    {
        var holdUi = false;
        var callbacks = new System.Collections.Concurrent.ConcurrentQueue<Action>();
        var app = Start(action => { if (holdUi) callbacks.Enqueue(action); else action(); });
        var faults = Faulty(app);
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Saved question"));
        await app.Chats.SaveNowAsync();
        faults.FailSaves = true;
        var held = faults.HoldNext();
        app.Conversation.Draft = "draft waiting for acknowledgement";
        // Let the timer execute and queue the write before holding UI work.
        _time.Advance(IntelligenceChatListViewModel.SaveDelay);
        await held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        holdUi = true;
        held.Release.SetResult();
        await app.SettleAsync();
        Assert.Equal(ChatWriteResult.Failed, await app.Chats.Active!.LastSave!.Value.Saving);
        Assert.False(app.Chats.Active.NeedsRetry);
        Assert.False(callbacks.IsEmpty);
        Assert.Equal("", Assert.Single((await ReadAsync(ServerA)).Chats).Draft);

        faults.FailSaves = false;
        await app.Chats.SaveNowAsync();
        Assert.Equal("draft waiting for acknowledgement", Assert.Single((await ReadAsync(ServerA)).Chats).Draft);
        Assert.True(app.Chats.KeepsCurrentChat);
        // Delayed older acknowledgements cannot undo the confirmed newer write.
        while (callbacks.TryDequeue(out var callback)) callback();
        Assert.True(app.Chats.KeepsCurrentChat);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Suspension_retries_failed_inactive_pin_and_rename_without_changing_the_active_chat(bool pin)
    {
        var app = Start();
        var faults = Faulty(app);
        await app.Chats.LoadAsync();
        Assert.True(await app.Conversation.AskAsync("Inactive chat"));
        Assert.True(await app.Chats.NewChatAsync());
        Assert.True(await app.Conversation.AskAsync("Active chat"));
        await app.SettleAsync();
        var inactive = app.Chats.Chats.Single(chat => chat.Title == "Inactive chat");
        var activeId = app.Chats.Active!.Id;
        faults.FailSaves = true;
        Assert.True(pin ? app.Chats.TogglePin(inactive) : app.Chats.Rename(inactive, "Renamed inactive"));
        await app.Chats.SaveNowAsync();
        var before = (await ReadAsync(ServerA)).Chats.Single(chat => chat.Id == inactive.Id);
        Assert.False(before.IsPinned);
        Assert.Equal("Inactive chat", before.Title);

        faults.FailSaves = false;
        await app.Chats.SaveNowAsync();
        var collection = await ReadAsync(ServerA);
        var saved = collection.Chats.Single(chat => chat.Id == inactive.Id);
        Assert.Equal(pin, saved.IsPinned);
        Assert.Equal(pin ? "Inactive chat" : "Renamed inactive", saved.Title);
        Assert.Equal(activeId, collection.ActiveId);
    }

    private static FaultyStore Faulty(App app)
    {
        var faults = new FaultyStore();
        app.Chats.WrapStore = inner =>
        {
            faults.Inner = inner;
            return faults;
        };
        return faults;
    }

    private sealed class NoBackup : IBackupExclusion
    {
        public void Exclude(string directory) { }
    }

    /// <summary>A store whose first write waits to be released, recording every call in order.</summary>
    private sealed class GateStore : IIntelligenceChatStore
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<string> Calls { get; } = [];
        private int _first;

        public SavedChatCollection Load(string scope) => new(null, []);

        public void Save(string scope, SavedIntelligenceChat chat, bool makeActive = true)
        {
            Hold();
            lock (Calls) Calls.Add("save " + chat.Title);
        }

        public void Delete(string scope, Guid id)
        {
            Hold();
            lock (Calls) Calls.Add("delete " + id);
        }

        private void Hold()
        {
            if (Interlocked.Exchange(ref _first, 1) != 0) return;
            Entered.SetResult();
            Release.Task.Wait(TimeSpan.FromSeconds(10));
        }
    }

    private sealed class Gate
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>The real encrypted store underneath; holds the next call on cue, and fails saves or deletes on cue.</summary>
    private sealed class FaultyStore : IIntelligenceChatStore
    {
        private Gate? _hold;
        private int _writes;
        private volatile bool _failSaves;
        private volatile bool _failDeletes;

        public IIntelligenceChatStore Inner { get; set; } = null!;
        public bool FailSaves { get => _failSaves; set => _failSaves = value; }
        public bool FailDeletes { get => _failDeletes; set => _failDeletes = value; }

        /// <summary>Every save and delete the writer handed to the store, failed or not.</summary>
        public int Writes => Volatile.Read(ref _writes);

        /// <summary>The next load, save or delete waits until the gate is released.</summary>
        public Gate HoldNext()
        {
            var gate = new Gate();
            Volatile.Write(ref _hold, gate);
            return gate;
        }

        public SavedChatCollection Load(string scope)
        {
            Wait();
            return Inner.Load(scope);
        }

        public void Save(string scope, SavedIntelligenceChat chat, bool makeActive = true)
        {
            Interlocked.Increment(ref _writes);
            Wait();
            if (FailSaves) throw new IOException("Synthetic save failure.");
            Inner.Save(scope, chat, makeActive);
        }

        public void Delete(string scope, Guid id)
        {
            Interlocked.Increment(ref _writes);
            Wait();
            if (FailDeletes) throw new IOException("Synthetic delete failure.");
            Inner.Delete(scope, id);
        }

        private void Wait()
        {
            if (Interlocked.Exchange(ref _hold, null) is not { } gate) return;
            gate.Entered.SetResult();
            gate.Release.Task.Wait(TimeSpan.FromSeconds(10));
        }
    }

    private delegate Task<IntelligenceAnswer> Script(string prompt, IReadOnlyList<IntelligenceLookup> tools, Action<string> text, CancellationToken token);

    private sealed class Model : IIntelligenceModel
    {
        public IntelligenceAvailability Availability => IntelligenceAvailability.Available;
        public int? ContextSize => 4096;
        public Queue<Script> Scripts { get; } = new();
        public Task<int?> CountTokensAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);

        public IIntelligenceSession StartSession(string instructions, IReadOnlyList<IntelligenceLookup> lookups) =>
            new Session(lookups, Scripts.Count > 0 ? Scripts.Dequeue() : (_, _, _, _) => Task.FromResult(new IntelligenceAnswer("Answer", null)));

        private sealed class Session(IReadOnlyList<IntelligenceLookup> tools, Script script) : IIntelligenceSession
        {
            public Task<IntelligenceAnswer> AskAsync(string question, Action<string> onText, CancellationToken cancellationToken = default) =>
                script(question, tools, onText, cancellationToken);

            public void Dispose() { }
        }
    }

    /// <summary>One lookup, which reports a device and a group with its exact LibreNMS name.</summary>
    private sealed class Lookups : IIntelligenceLookups
    {
        public IReadOnlyList<IntelligenceLookup> Create() => Create(_ => { });

        public IReadOnlyList<IntelligenceLookup> Create(Action<IReadOnlyList<IntelligenceEvidence>> evidence) =>
        [
            new("network_overview", "Overview", (_, _) =>
            {
                evidence([new IntelligenceEvidence(Device), new IntelligenceEvidence(Group, GroupFilter)]);
                return Task.FromResult("Devices: 10 total.");
            }),
        ];
    }
}
