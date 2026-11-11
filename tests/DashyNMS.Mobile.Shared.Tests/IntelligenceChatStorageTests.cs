using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Security;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Intelligence;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Tests;

/// <summary>Saved chats, encrypted on the phone (#222). Every name, address and line of text here is synthetic.</summary>
public sealed class IntelligenceChatStorageTests : IDisposable
{
    private const string Scope = "https://nms.example.net|scope-sentinel";
    private const string Title = "title-sentinel chat";
    private const string Question = "question-sentinel: which switch is down?";
    private const string Answer = "answer-sentinel: sw-zz-01 is down.";
    private const string Draft = "draft-sentinel text";
    private const string Evidence = "evidence-sentinel lookup";
    private const string SpotlightName = "spotlight-sentinel";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"dashynms-chats-{Guid.NewGuid():N}");
    private readonly ChatKeyStorage _keys = new();
    private readonly RecordingExclusion _backup = new();
    private readonly DiagnosticsLog _log = new(null);

    /// <summary>One file system across every instance, as across an app restart.</summary>
    private readonly FailingDisk _disk = new();

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        File.Delete(NotePath);
    }

    private IntelligenceChatStorage NewStorage(IBackupExclusion? backup = null, IChatCleanupFiles? disk = null) =>
        new(_root, _keys, backup ?? _backup, new Forward<IntelligenceChatStorage>(_log.CreateLogger(typeof(IntelligenceChatStorage).FullName!)), disk ?? _disk);

    private string NotePath => NewStorage().CleanupNotePath;

    private static SavedIntelligenceChat Chat() => new()
    {
        Title = Title,
        Draft = Draft,
        Model = "on-device",
        History = new JsonArray(JsonValue.Create(Question)),
        Messages =
        [
            new() { IsUser = true, Text = Question },
            new() { Text = Answer, SubmittedQuestion = Question, Outcome = IntelligenceReplyOutcome.Completed },
        ],
        LookedAt = [Evidence],
        Spotlight = [new SavedChatSpotlight(1, 42, SpotlightName)],
    };

    private string[] ChatFiles(string pattern = "*.chat") =>
        Directory.Exists(_root) ? Directory.GetFiles(_root, pattern, SearchOption.AllDirectories) : [];

    private string FileOf(SavedIntelligenceChat chat) => ChatFiles(chat.Id.ToString("N") + ".chat").Single();

    private static bool Contains(byte[] data, string text) => data.AsSpan().IndexOf(Encoding.UTF8.GetBytes(text)) >= 0;

    [Fact]
    public async Task A_saved_chat_reads_back_after_a_restart_and_its_file_holds_nothing_readable()
    {
        var chat = Chat();
        var storage = NewStorage();
        Assert.Equal(IntelligenceChatStorageState.Closed, storage.State);
        var store = await storage.OpenAsync();
        Assert.NotNull(store);
        Assert.Equal(IntelligenceChatStorageState.Ready, storage.State);
        Assert.Null(storage.UnavailableReason);
        store.Save(Scope, chat);

        // A new instance over the same folder and keychain: an app restart.
        var loaded = (await NewStorage().OpenAsync())!.Load(Scope);

        var back = Assert.Single(loaded.Chats);
        Assert.Equal(chat.Id, loaded.ActiveId);
        Assert.Equal(Title, back.Title);
        Assert.Equal(Draft, back.Draft);
        Assert.Equal(new[] { Question, Answer }, back.Messages.Select(m => m.Text));
        Assert.Equal(Evidence, Assert.Single(back.LookedAt));
        Assert.Equal(SpotlightName, Assert.Single(back.Spotlight).Name);
        Assert.Empty(loaded.Unreadable ?? []);

        // Encrypted on disk: the chat and the "which one was open" file both,
        // under a folder named by a hash rather than the server.
        var files = Directory.GetFiles(_root, "*", SearchOption.AllDirectories);
        Assert.Equal(2, files.Length);
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file);
            Assert.Equal((byte)1, bytes[0]);
            foreach (var text in new[] { Title, Question, Answer, Draft, Evidence, SpotlightName, chat.Id.ToString() })
            {
                Assert.False(Contains(bytes, text), $"{Path.GetFileName(file)} holds \"{text}\" in plain");
            }

            Assert.DoesNotContain("example", file, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(32, Convert.FromBase64String(_keys.Values[IntelligenceChatStorage.KeyName]).Length);
    }

    [Fact]
    public async Task A_damaged_or_cut_short_file_is_set_aside_while_the_others_load()
    {
        var store = (await NewStorage().OpenAsync())!;
        var healthy = Chat();
        var tampered = Chat();
        var truncated = Chat();
        store.Save(Scope, healthy);
        store.Save(Scope, tampered, makeActive: false);
        store.Save(Scope, truncated, makeActive: false);

        // One flipped byte fails the GCM tag; five bytes isn't a chat at all.
        var bytes = File.ReadAllBytes(FileOf(tampered));
        bytes[^1] ^= 0x5a;
        File.WriteAllBytes(FileOf(tampered), bytes);
        File.WriteAllBytes(FileOf(truncated), [1, 2, 3, 4, 5]);

        var loaded = (await NewStorage().OpenAsync())!.Load(Scope);

        Assert.Equal(healthy.Id, Assert.Single(loaded.Chats).Id);
        Assert.Equal(healthy.Id, loaded.ActiveId);
        Assert.Equal(
            new[] { tampered, truncated }.Select(c => c.Id.ToString("N") + ".chat").Order(),
            (loaded.Unreadable ?? []).Order());
        Assert.Equal(2, ChatFiles("*.chat.bad").Length);

        // Set aside for good, not retried on every load.
        Assert.Empty((await NewStorage().OpenAsync())!.Load(Scope).Unreadable ?? []);
    }

    [Fact]
    public async Task A_key_that_cannot_be_read_keeps_nothing_and_leaves_saved_chats_alone()
    {
        var chat = Chat();
        (await NewStorage().OpenAsync())!.Save(Scope, chat);
        var key = _keys.Values[IntelligenceChatStorage.KeyName];
        var before = File.ReadAllBytes(FileOf(chat));
        var writes = _keys.Writes;

        _keys.FailReads = true;
        var storage = NewStorage();
        var changes = 0;
        storage.StateChanged += (_, _) => changes++;

        Assert.Null(await storage.OpenAsync());
        Assert.Null(await storage.OpenAsync());

        Assert.Equal(IntelligenceChatStorageState.Unavailable, storage.State);
        Assert.Contains("secure storage", storage.UnavailableReason);
        Assert.Equal(1, changes);

        // Not "there's no key": nothing made over it, nothing removed, nothing set aside.
        Assert.Equal(writes, _keys.Writes);
        Assert.Equal(key, _keys.Values[IntelligenceChatStorage.KeyName]);
        Assert.Equal(before, File.ReadAllBytes(FileOf(chat)));
        Assert.Empty(ChatFiles("*.bad"));

        // Back once the keychain answers again.
        _keys.FailReads = false;
        Assert.Equal(chat.Id, Assert.Single((await storage.OpenAsync())!.Load(Scope).Chats).Id);
        Assert.Equal(IntelligenceChatStorageState.Ready, storage.State);
        Assert.Null(storage.UnavailableReason);
    }

    [Fact]
    public async Task A_key_that_cannot_be_stored_means_no_files_at_all()
    {
        _keys.FailWrites = true;
        var storage = NewStorage();

        Assert.Null(await storage.OpenAsync());

        Assert.Equal(IntelligenceChatStorageState.Unavailable, storage.State);
        Assert.Contains("couldn't be stored", storage.UnavailableReason);
        Assert.Empty(_keys.Values);
        Assert.Empty(ChatFiles("*"));
    }

    [Fact]
    public async Task A_missing_key_gets_a_new_one_and_the_old_chats_are_set_aside_unread()
    {
        var chat = Chat();
        (await NewStorage().OpenAsync())!.Save(Scope, chat);
        var oldKey = _keys.Values[IntelligenceChatStorage.KeyName];

        // A restored phone, or a cleared keystore: the key is simply gone.
        _keys.Values.Clear();
        var loaded = (await NewStorage().OpenAsync())!.Load(Scope);

        Assert.Empty(loaded.Chats);
        Assert.Equal(chat.Id.ToString("N") + ".chat", Assert.Single(loaded.Unreadable ?? []));
        Assert.Single(ChatFiles("*.chat.bad"));
        Assert.NotEqual(oldKey, _keys.Values[IntelligenceChatStorage.KeyName]);
    }

    [Fact]
    public async Task A_damaged_key_is_left_in_place_and_nothing_is_saved()
    {
        _keys.Values[IntelligenceChatStorage.KeyName] = "not-a-key";
        var storage = NewStorage();

        Assert.Null(await storage.OpenAsync());

        Assert.Equal(IntelligenceChatStorageState.Unavailable, storage.State);
        Assert.Contains("damaged", storage.UnavailableReason);
        Assert.Equal("not-a-key", _keys.Values[IntelligenceChatStorage.KeyName]);
        Assert.Equal(0, _keys.Writes);
    }

    [Fact]
    public async Task A_wipe_removes_the_folder_and_key_and_refuses_a_late_save()
    {
        var storage = NewStorage();
        var store = (await storage.OpenAsync())!;
        store.Save(Scope, Chat());
        var oldKey = _keys.Values[IntelligenceChatStorage.KeyName];

        Assert.Equal(ChatWipeResult.Removed, await storage.WipeAsync());

        Assert.False(Directory.Exists(_root));
        Assert.False(_keys.Values.ContainsKey(IntelligenceChatStorage.KeyName));
        Assert.Equal(IntelligenceChatStorageState.Closed, storage.State);

        // An answer finishing after sign-out: refused, and the folder stays gone.
        Assert.Throws<ChatStorageUnavailableException>(() => store.Save(Scope, Chat()));
        Assert.Throws<ChatStorageUnavailableException>(() => store.Load(Scope));
        Assert.Throws<ChatStorageUnavailableException>(() => store.Delete(Scope, Guid.NewGuid()));
        Assert.False(Directory.Exists(_root));

        // Signing in again starts afresh under a new key, and the old store stays refused.
        var next = (await storage.OpenAsync())!;
        Assert.Empty(next.Load(Scope).Chats);
        Assert.NotEqual(oldKey, _keys.Values[IntelligenceChatStorage.KeyName]);
        Assert.Throws<ChatStorageUnavailableException>(() => store.Save(Scope, Chat()));
        Assert.Empty(next.Load(Scope).Chats);
    }

    [Fact]
    public async Task Saves_racing_a_wipe_never_bring_the_folder_back()
    {
        var storage = NewStorage();
        var store = (await storage.OpenAsync())!;

        var savers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            var saved = 0;
            while (true)
            {
                try
                {
                    store.Save(Scope, Chat());
                    saved++;
                }
                catch (ChatStorageUnavailableException)
                {
                    return saved;
                }
            }
        })).ToArray();

        Assert.True(SpinWait.SpinUntil(() => ChatFiles().Length > 0, TimeSpan.FromSeconds(10)));
        Assert.Equal(ChatWipeResult.Removed, await storage.WipeAsync());
        var counts = await Task.WhenAll(savers);

        Assert.True(counts.Sum() > 0);
        Assert.False(Directory.Exists(_root));
        Assert.False(_keys.Values.ContainsKey(IntelligenceChatStorage.KeyName));
    }

    [Fact]
    public async Task A_key_being_created_as_a_wipe_starts_does_not_outlive_it()
    {
        var keychain = new TaskCompletionSource();
        _keys.BeforeRead = () => keychain.Task;
        var storage = NewStorage();

        var opening = storage.OpenAsync();
        var wiping = storage.WipeAsync();
        _keys.BeforeRead = null;
        keychain.SetResult();

        Assert.Null(await opening);
        Assert.Equal(ChatWipeResult.Removed, await wiping);
        Assert.Equal(1, _keys.Writes); // the key was made, then removed by the wipe
        Assert.False(_keys.Values.ContainsKey(IntelligenceChatStorage.KeyName));
        Assert.False(Directory.Exists(_root));
        Assert.Equal(IntelligenceChatStorageState.Closed, storage.State);
    }

    [Fact]
    public async Task Opens_queued_before_or_started_during_a_wipe_cannot_survive_it()
    {
        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _keys.BeforeRead = () => blocked.Task;
        var storage = NewStorage();
        var first = storage.OpenAsync(); // holds the key turn
        var queued = storage.OpenAsync(); // queued ahead of wipe
        var wiping = storage.WipeAsync();
        var during = storage.OpenAsync();
        _keys.BeforeRead = null;
        blocked.SetResult();

        Assert.Null(await first);
        Assert.Null(await queued);
        Assert.Null(await during);
        Assert.Equal(ChatWipeResult.Removed, await wiping);
        Assert.Empty(_keys.Values);
        Assert.False(Directory.Exists(_root));
        Assert.NotNull(await storage.OpenAsync()); // only a fresh post-wipe open
    }

    [Fact]
    public async Task A_silent_key_removal_failure_is_reported_and_blocks_reopening_until_retry()
    {
        var storage = NewStorage();
        var store = (await storage.OpenAsync())!;
        store.Save(Scope, Chat());
        _keys.FailRemove = true;

        Assert.Equal(ChatWipeResult.Retained, await storage.WipeAsync());
        Assert.Single(_keys.Values);
        Assert.Equal(IntelligenceChatStorageState.Unavailable, storage.State);
        Assert.Null(await storage.OpenAsync());
        Assert.Throws<ChatStorageUnavailableException>(() => store.Save(Scope, Chat()));
        _keys.FailRemove = false;
        Assert.Equal(ChatWipeResult.Removed, await storage.WipeAsync());
        Assert.Empty(_keys.Values);
        Assert.NotNull(await storage.OpenAsync());
    }

    [Fact]
    public async Task Both_removals_failing_keep_the_old_chats_locked_after_a_restart_until_cleanup_succeeds()
    {
        var chat = Chat();
        var storage = NewStorage();
        (await storage.OpenAsync())!.Save(Scope, chat);
        var oldKey = _keys.Values[IntelligenceChatStorage.KeyName];
        var before = File.ReadAllBytes(FileOf(chat));
        _keys.FailRemove = true;
        _disk.FailDeleteDirectory = true;

        Assert.Equal(ChatWipeResult.Retained, await storage.WipeAsync());
        Assert.Equal(oldKey, _keys.Values[IntelligenceChatStorage.KeyName]);
        Assert.Equal(before, File.ReadAllBytes(FileOf(chat)));
        Assert.True(File.Exists(NotePath));
        Assert.Contains("even after the app restarts", storage.UnavailableReason);

        // A restart with both still refused: the same keychain and files, a new instance.
        var restarted = NewStorage();
        Assert.Null(await restarted.OpenAsync());
        Assert.Null(await restarted.OpenAsync());
        Assert.Equal(IntelligenceChatStorageState.Unavailable, restarted.State);
        Assert.Contains("earlier sign-out", restarted.UnavailableReason);

        // The old key was never used to open them: nothing read, nothing set
        // aside, no key made, and the note stays.
        Assert.Equal(oldKey, _keys.Values[IntelligenceChatStorage.KeyName]);
        Assert.Equal(before, File.ReadAllBytes(FileOf(chat)));
        Assert.Empty(ChatFiles("*.bad"));
        Assert.Equal(1, _keys.Writes);
        Assert.True(File.Exists(NotePath));

        // Once removal works, the next open finishes it and starts afresh.
        _keys.FailRemove = false;
        _disk.FailDeleteDirectory = false;
        var store = (await restarted.OpenAsync())!;
        Assert.Equal(IntelligenceChatStorageState.Ready, restarted.State);
        Assert.False(File.Exists(NotePath));
        Assert.Empty(ChatFiles(chat.Id.ToString("N") + ".chat*"));
        Assert.NotEqual(oldKey, _keys.Values[IntelligenceChatStorage.KeyName]);
        var loaded = store.Load(Scope);
        Assert.Empty(loaded.Chats);
        Assert.Empty(loaded.Unreadable ?? []);

        var next = Chat();
        store.Save(Scope, next);
        Assert.Equal(next.Id, Assert.Single((await NewStorage().OpenAsync())!.Load(Scope).Chats).Id);
    }

    [Fact]
    public async Task A_wipe_cut_off_part_way_is_finished_by_the_next_open_after_a_restart()
    {
        var chat = Chat();
        var storage = NewStorage();
        (await storage.OpenAsync())!.Save(Scope, chat);
        var oldKey = _keys.Values[IntelligenceChatStorage.KeyName];

        // The app is killed mid-wipe: the key's removal is refused, and the
        // check that follows never answers. Nothing has been removed yet.
        _keys.FailRemove = true;
        _keys.BeforeRead = () => new TaskCompletionSource().Task;
        _ = storage.WipeAsync();
        Assert.True(File.Exists(NotePath));
        Assert.Equal(oldKey, _keys.Values[IntelligenceChatStorage.KeyName]);
        Assert.Single(ChatFiles());

        _keys.FailRemove = false;
        _keys.BeforeRead = null;
        var store = (await NewStorage().OpenAsync())!;

        Assert.False(File.Exists(NotePath));
        Assert.NotEqual(oldKey, _keys.Values[IntelligenceChatStorage.KeyName]);
        var loaded = store.Load(Scope);
        Assert.Empty(loaded.Chats);
        Assert.Empty(loaded.Unreadable ?? []);
        Assert.Contains("Finished removing saved chats from an earlier sign-out", _log.Text);
    }

    [Fact]
    public async Task A_note_left_with_the_key_already_gone_removes_the_old_files_before_a_new_key_is_made()
    {
        var chat = Chat();
        var storage = NewStorage();
        (await storage.OpenAsync())!.Save(Scope, chat);
        _disk.FailDeleteDirectory = true;

        Assert.Equal(ChatWipeResult.Retained, await storage.WipeAsync());
        Assert.Empty(_keys.Values);
        Assert.Single(ChatFiles());

        _disk.FailDeleteDirectory = false;
        var store = (await NewStorage().OpenAsync())!;

        // Removed, not set aside as unreadable under the new key.
        Assert.Empty(ChatFiles(chat.Id.ToString("N") + ".chat*"));
        var loaded = store.Load(Scope);
        Assert.Empty(loaded.Chats);
        Assert.Empty(loaded.Unreadable ?? []);
        Assert.Single(_keys.Values);
        Assert.False(File.Exists(NotePath));
    }

    [Fact]
    public async Task A_note_that_cannot_be_saved_is_reported_as_lasting_only_until_a_restart()
    {
        var storage = NewStorage();
        (await storage.OpenAsync())!.Save(Scope, Chat());
        _disk.FailWriteNote = true;
        _keys.FailRemove = true;
        _disk.FailDeleteDirectory = true;

        Assert.Equal(ChatWipeResult.RetainedUntilRestart, await storage.WipeAsync());
        Assert.False(File.Exists(NotePath));
        Assert.Contains("after a restart they could be readable again", storage.UnavailableReason);

        // Still locked for the rest of this run, and still said to be temporary.
        Assert.Null(await storage.OpenAsync());
        Assert.Equal(IntelligenceChatStorageState.Unavailable, storage.State);
        Assert.Contains("after a restart they could be readable again", storage.UnavailableReason);

        // An open writes the note once it can, so the lock now outlasts a restart too.
        _disk.FailWriteNote = false;
        Assert.Null(await storage.OpenAsync());
        Assert.True(File.Exists(NotePath));
        Assert.Contains("earlier sign-out", storage.UnavailableReason);
        Assert.Null(await NewStorage().OpenAsync());
        Assert.Single(_keys.Values);
    }

    [Fact]
    public async Task The_note_is_written_first_and_removed_only_after_the_key_and_folder_are_gone()
    {
        var storage = NewStorage();
        (await storage.OpenAsync())!.Save(Scope, Chat());
        _keys.OnRemove = () => _disk.Steps.Add("key");
        _disk.Steps.Clear();

        Assert.Equal(ChatWipeResult.Removed, await storage.WipeAsync());

        Assert.Equal(new[] { "write note", "key", "delete folder", "delete note" }, _disk.Steps);
        Assert.False(File.Exists(NotePath));
    }

    [Fact]
    public async Task A_note_that_cannot_be_removed_keeps_saved_chats_closed_until_it_can()
    {
        var storage = NewStorage();
        (await storage.OpenAsync())!.Save(Scope, Chat());
        _disk.FailDeleteFile = true;

        // The chats and key are gone, which is what the result reports.
        Assert.Equal(ChatWipeResult.Removed, await storage.WipeAsync());
        Assert.False(Directory.Exists(_root));
        Assert.Empty(_keys.Values);
        Assert.True(File.Exists(NotePath));

        Assert.Null(await NewStorage().OpenAsync());
        Assert.Empty(_keys.Values);

        _disk.FailDeleteFile = false;
        Assert.NotNull(await NewStorage().OpenAsync());
        Assert.False(File.Exists(NotePath));
    }

    [Fact]
    public async Task Not_knowing_whether_a_wipe_finished_opens_nothing_and_deletes_nothing()
    {
        var chat = Chat();
        (await NewStorage().OpenAsync())!.Save(Scope, chat);
        var key = _keys.Values[IntelligenceChatStorage.KeyName];
        var before = File.ReadAllBytes(FileOf(chat));
        _disk.FailExists = true;
        var storage = NewStorage();

        Assert.Null(await storage.OpenAsync());

        Assert.Equal(IntelligenceChatStorageState.Unavailable, storage.State);
        Assert.Contains("couldn't confirm", storage.UnavailableReason);
        Assert.Equal(key, _keys.Values[IntelligenceChatStorage.KeyName]);
        Assert.Equal(before, File.ReadAllBytes(FileOf(chat)));
    }

    [Fact]
    public async Task An_already_absent_key_counts_as_a_successful_wipe()
    {
        Assert.Equal(ChatWipeResult.Removed, await NewStorage().WipeAsync());
    }

    [Fact]
    public async Task Two_screens_opening_at_once_share_one_key()
    {
        var keychain = new TaskCompletionSource();
        _keys.BeforeRead = () => keychain.Task;
        var storage = NewStorage();

        var first = storage.OpenAsync();
        var second = storage.OpenAsync();
        _keys.BeforeRead = null;
        keychain.SetResult();
        var a = await first;
        var b = await second;

        Assert.Equal(1, _keys.Writes);
        var chat = Chat();
        a!.Save(Scope, chat);
        Assert.Equal(chat.Id, Assert.Single(b!.Load(Scope).Chats).Id);
    }

    [Fact]
    public async Task A_key_stored_elsewhere_at_the_same_moment_is_the_one_used()
    {
        var other = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _keys.ReplaceOnSet = other;
        var chat = Chat();

        (await NewStorage().OpenAsync())!.Save(Scope, chat);
        _keys.ReplaceOnSet = null;

        Assert.Equal(other, _keys.Values[IntelligenceChatStorage.KeyName]);
        Assert.Equal(chat.Id, Assert.Single((await NewStorage().OpenAsync())!.Load(Scope).Chats).Id);
    }

    [Fact]
    public async Task A_write_cut_short_is_cleared_and_never_loaded()
    {
        var chat = Chat();
        (await NewStorage().OpenAsync())!.Save(Scope, chat);
        var partial = Path.Combine(Path.GetDirectoryName(FileOf(chat))!, Guid.NewGuid().ToString("N") + ".chat.tmp");
        File.WriteAllBytes(partial, [1, 9, 9]);

        var loaded = (await NewStorage().OpenAsync())!.Load(Scope);

        Assert.False(File.Exists(partial));
        Assert.Equal(chat.Id, Assert.Single(loaded.Chats).Id);
        Assert.Empty(loaded.Unreadable ?? []);
    }

    [Fact]
    public async Task The_folder_is_kept_out_of_backups_before_anything_is_saved_and_again_after_a_wipe()
    {
        var storage = NewStorage();

        await storage.OpenAsync();
        Assert.Equal(new[] { _root }, _backup.Calls.Select(c => c.Directory));

        await storage.WipeAsync();
        await storage.OpenAsync();
        Assert.Equal(new[] { _root, _root }, _backup.Calls.Select(c => c.Directory));
        Assert.All(_backup.Calls, c => Assert.True(c.Existed));
    }

    [Fact]
    public async Task A_folder_that_cannot_be_kept_out_of_backups_keeps_nothing()
    {
        var storage = NewStorage(new RecordingExclusion { Failure = new IOException("exclusion-sentinel /private/var/mobile") });

        Assert.Null(await storage.OpenAsync());

        Assert.Equal(IntelligenceChatStorageState.Unavailable, storage.State);
        Assert.Contains("backups", storage.UnavailableReason);
        Assert.Empty(ChatFiles("*"));
    }

    [Fact]
    public void The_platforms_keep_saved_chats_out_of_backups()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "DashyNMS.Mobile.slnx")))
        {
            repo = repo.Parent;
        }

        Assert.NotNull(repo);
        var root = repo.FullName;
        string Source(params string[] parts) => File.ReadAllText(Path.Combine([root, "src", "DashyNMS.Mobile", .. parts]));

        // Android: the whole app is out of backups.
        Assert.Contains("android:allowBackup=\"false\"", Source("Platforms", "Android", "AndroidManifest.xml"));

        // iOS: the chats folder is marked, through the exclusion the head registers.
        Assert.Contains("NSUrl.IsExcludedFromBackupKey", Source("Platforms", "iOS", "IosBackupExclusion.cs"));
        var program = Source("MauiProgram.cs");
        Assert.Matches(@"#if IOS\s+builder\.Services\.AddSingleton<IBackupExclusion, IosBackupExclusion>\(\);", program);
        Assert.Contains("Path.Combine(FileSystem.AppDataDirectory, \"chats\")", program);
        Assert.Contains("sp.GetRequiredService<IBackupExclusion>()", program);
    }

    [Fact]
    public async Task Nothing_from_a_chat_its_scope_or_its_key_reaches_the_diagnostics_log()
    {
        // Every path that logs: create, save and load, damage, an unreadable
        // keychain, a backup failure and a wipe.
        var storage = NewStorage();
        var store = (await storage.OpenAsync())!;
        var chat = Chat();
        store.Save(Scope, chat);
        File.WriteAllBytes(FileOf(chat), [1, 2, 3]);
        store.Save(Scope, Chat());
        store.Load(Scope);
        var key = _keys.Values[IntelligenceChatStorage.KeyName];

        _keys.FailReads = true;
        await NewStorage().OpenAsync();
        _keys.FailReads = false;
        await NewStorage(new RecordingExclusion { Failure = new IOException("exclusion-sentinel") }).OpenAsync();

        // A wipe that can't finish, and an open that can't finish it either.
        var failing = new FailingDisk { FailDeleteDirectory = true, FailWriteNote = true };
        var stuck = NewStorage(disk: failing);
        await stuck.WipeAsync();
        await stuck.OpenAsync();
        failing.FailExists = true;
        await NewStorage(disk: failing).OpenAsync();

        await storage.WipeAsync();

        var text = _log.Text;

        // Logged at all - so the absences below mean something.
        Assert.Contains("Created the saved-chats key", text);
        Assert.Contains("Saved chats are unavailable (KeyUnreadable)", text);
        Assert.Contains("Saved chats are unavailable (FolderUnavailable)", text);
        Assert.Contains("Removed saved chats and their key", text);
        Assert.Contains("the cleanup note could not be saved", text);
        Assert.Contains("Saved chats are unavailable (CleanupUnguarded)", text);
        Assert.Contains("Saved chats are unavailable (CleanupUnknown)", text);

        foreach (var secret in new[] { Title, Question, Answer, Draft, Evidence, SpotlightName, Scope, "nms.example.net", key, ChatKeyStorage.ReadFailure, "exclusion-sentinel", FailingDisk.Failure, _root, chat.Id.ToString("N") })
        {
            Assert.DoesNotContain(secret, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private sealed class RecordingExclusion : IBackupExclusion
    {
        public List<(string Directory, bool Existed)> Calls { get; } = new();

        public Exception? Failure { get; init; }

        public void Exclude(string directory)
        {
            Calls.Add((directory, Directory.Exists(directory)));
            if (Failure is not null)
            {
                throw Failure;
            }
        }
    }

    /// <summary>The real file system, which can refuse any one cleanup step on cue and records the ones it does.</summary>
    internal sealed class FailingDisk : IChatCleanupFiles
    {
        public const string Failure = "disk-refused-sentinel";

        private readonly ChatCleanupFiles _real = new();

        public bool FailExists { get; set; }

        public bool FailWriteNote { get; set; }

        public bool FailDeleteFile { get; set; }

        public bool FailDeleteDirectory { get; set; }

        public List<string> Steps { get; } = new();

        public bool Exists(string path) => FailExists ? throw new UnauthorizedAccessException(Failure + path) : _real.Exists(path);

        public void WriteNote(string path) => Step("write note", FailWriteNote, () => _real.WriteNote(path), path);

        public void DeleteFile(string path) => Step("delete note", FailDeleteFile, () => _real.DeleteFile(path), path);

        public void DeleteDirectory(string path) => Step("delete folder", FailDeleteDirectory, () => _real.DeleteDirectory(path), path);

        private void Step(string name, bool fail, Action work, string path)
        {
            if (fail)
            {
                throw new IOException(Failure + path);
            }

            work();
            Steps.Add(name);
        }
    }

    /// <summary>The typed logger the storage takes, over the diagnostics log's own.</summary>
    private sealed class Forward<T>(ILogger inner) : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => inner.BeginScope(state);

        public bool IsEnabled(LogLevel logLevel) => inner.IsEnabled(logLevel);

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            inner.Log(logLevel, eventId, state, exception, formatter);
    }
}

/// <summary>A keychain in memory, which can refuse, stall or be beaten to a write.</summary>
internal sealed class ChatKeyStorage : ISecureStorage
{
    public const string ReadFailure = "keystore-locked-sentinel";

    private int _writes;

    public ConcurrentDictionary<string, string> Values { get; } = new();

    public bool FailReads { get; set; }

    public bool FailWrites { get; set; }

    public bool FailRemove { get; set; }

    /// <summary>Called on every removal, refused or not, to record the order of a wipe's steps.</summary>
    public Action? OnRemove { get; set; }

    /// <summary>Awaited before each read, to hold one open.</summary>
    public Func<Task>? BeforeRead { get; set; }

    /// <summary>Stored instead of what was set: another writer's key landing at the same moment.</summary>
    public string? ReplaceOnSet { get; set; }

    public int Writes => Volatile.Read(ref _writes);

    public async Task<string?> GetAsync(string key)
    {
        if (BeforeRead is { } before)
        {
            await before();
        }

        if (FailReads)
        {
            throw new InvalidOperationException(ReadFailure);
        }

        return Values.TryGetValue(key, out var value) ? value : null;
    }

    public async Task SetAsync(string key, string value)
    {
        await Task.Yield();
        Interlocked.Increment(ref _writes);
        if (FailWrites)
        {
            throw new IOException("keystore full");
        }

        Values[key] = ReplaceOnSet ?? value;
    }

    public bool Remove(string key)
    {
        OnRemove?.Invoke();
        return !FailRemove && Values.TryRemove(key, out _);
    }
}
