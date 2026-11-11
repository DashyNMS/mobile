using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Intelligence;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>One saved chat in the list.</summary>
public sealed class IntelligenceChatItem(SavedIntelligenceChat data) : ObservableObject
{
    /// <summary>The chat as last captured; only the UI thread changes it, and the writer gets copies.</summary>
    internal SavedIntelligenceChat Data { get; } = data;

    public Guid Id => Data.Id;
    public string Title => string.IsNullOrWhiteSpace(Data.Title) ? IntelligenceSavedChats.NewChatTitle : Data.Title;
    public bool IsPinned => Data.IsPinned;
    public DateTimeOffset UpdatedAt => Data.UpdatedAt;

    /// <summary>Pin or Unpin, for the swipe item, menu and VoiceOver action.</summary>
    public string PinText => IsPinned ? "Unpin" : "Pin";

    private string _when = "";
    /// <summary>When it was last used, in the phone's time.</summary>
    public string When { get => _when; private set => SetProperty(ref _when, value); }

    private bool _isOpen;
    /// <summary>The chat showing in the conversation.</summary>
    public bool IsOpen { get => _isOpen; internal set { if (SetProperty(ref _isOpen, value)) OnPropertyChanged(nameof(Description)); } }

    /// <summary>What a screen reader reads for the row.</summary>
    public string Description => Title + (IsPinned ? ". Pinned" : "") + (IsOpen ? ". Open now" : "") + (When.Length > 0 ? ". " + When : "");

    /// <summary>The newest save of this chat the writer put on disk, by the list's save number.</summary>
    internal long WrittenSave { get; set; }

    /// <summary>The newest save of this chat that failed or was refused.</summary>
    internal long FailedSave { get; set; }

    /// <summary>
    /// The save that first carried what the chat now holds - its text, draft or
    /// detail - by the list's save number; 0 for a chat as it was read from disk.
    /// The chat is on disk once a save at least this new is written; saves that
    /// only move the reopen marker, pin or rename don't raise it.
    /// </summary>
    internal long RequiredSave { get; set; }

    /// <summary>The save queued last, so suspension can wait for its outcome.</summary>
    internal (long Number, long Generation, Task<ChatWriteResult> Saving)? LastSave { get; set; }

    /// <summary>Its delete is with the writer: nothing else may change it until that finishes.</summary>
    internal bool IsDeleting { get; set; }

    /// <summary>
    /// A save failed and none since reached the disk. <see cref="Data"/> already
    /// holds the failed capture, so it can't serve as the "already saved" baseline:
    /// the next save is written even if nothing changed.
    /// </summary>
    internal bool NeedsRetry => FailedSave > WrittenSave;

    internal void Refresh(DateTimeOffset now, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(UpdatedAt, zone);
        var today = TimeZoneInfo.ConvertTime(now, zone);
        When = local.Date == today.Date ? local.ToString("t", CultureInfo.CurrentCulture)
            : local.Year == today.Year ? local.ToString("d MMM", CultureInfo.CurrentCulture)
            : local.ToString("d MMM yyyy", CultureInfo.CurrentCulture);
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(IsPinned));
        OnPropertyChanged(nameof(PinText));
        OnPropertyChanged(nameof(Description));
    }
}

/// <summary>A heading of the list - Pinned, Today, Yesterday and so on, from Core - and its chats.</summary>
public sealed class IntelligenceChatGroup(string label, IEnumerable<IntelligenceChatItem> chats) : List<IntelligenceChatItem>(chats)
{
    public string Label { get; } = label;
}

/// <summary>
/// Saved chats (#229): the list for the signed-in LibreNMS server, grouped by
/// Core's <see cref="IntelligenceChatActivityGroups"/>, with pin, rename and
/// delete, and the conversation's autosave - captured on the UI thread, written
/// off it by <see cref="IntelligenceChatWriter"/>. Construct and call from the UI
/// context; timer and storage callbacks are posted back to it.
/// </summary>
/// <remarks>
/// <para>
/// A saved chat is bound to the conversation's <see cref="IntelligenceConversationViewModel.ChatRevision"/>
/// it was shown in. Every transcript change that isn't an edit - a new chat, a
/// reopened one, a connection change or sign-out - moves the revision on first,
/// so the binding breaks and an emptied or foreign transcript is never written
/// into that chat. Captures also need the transcript to be current for the
/// connection and the scope the list was loaded for.
/// </para>
/// <para>
/// Changes to the draft or detail are saved two seconds after they settle; while
/// an answer streams, at most every two seconds, so a forced quit loses little
/// and the partial answer reopens as Interrupted. A new question and a finished
/// answer are queued straight away. Suspension queues the latest and waits for
/// the writer (<see cref="SaveNowAsync"/>).
/// </para>
/// <para>
/// Settings › Intelligence › Single-chat mode already exists; its full privacy
/// mode is #230. Here it is honoured as far as saving goes: while it is on,
/// nothing is read or written, the list is hidden, waiting saves are cancelled,
/// and the chat that was showing is unbound so its later text can't reach the
/// file. A transcript that had text in single-chat mode stays unsaved after
/// leaving it, until a new chat starts. Saved chats are never deleted by it.
/// </para>
/// <para>
/// The writer reports what became of each save and delete. A chat leaves the list
/// only once its file is gone; a failed or dropped delete keeps it there to try
/// again. A failed save keeps the chat marked for retry, so suspension and the next
/// change write it again.
/// </para>
/// <para>
/// Leaving the chat on screen - New chat or opening another - goes through one
/// decision (<see cref="NewChatAsync"/>, <see cref="OpenAsync"/>), whichever
/// button, swipe, menu or VoiceOver action asked. The latest is queued and waited
/// for, at most <see cref="LeaveWait"/> so the screen never hangs on a slow disk.
/// Only a chat whose latest text is confirmed written counts as kept; a save that
/// failed, or is still waiting when the wait ends, is not, and nor is a chat that
/// isn't being saved at all. Those ask first: try saving again, discard the chat,
/// or cancel and keep it on screen.
/// </para>
/// </remarks>
public sealed class IntelligenceChatListViewModel : ObservableObject, IDisposable
{
    /// <summary>How long after the last change a chat is saved, and how often while streaming.</summary>
    public static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(2);

    internal const string SingleChatStatus = "Single-chat mode is on: this chat is kept only while the app is open, and saved chats are hidden.";
    internal const string UnsavedStatus = "This chat isn't saved because it was used in single-chat mode. Start a new chat to save again.";
    internal const string ReadFailedStatus = "Saved chats couldn't be read. They've been left as they are, and this chat isn't being saved.";
    internal const string SaveFailedStatus = "This chat couldn't be saved. It stays on screen, and saving is tried again with the next change and when you leave the app.";
    internal const string UnavailableStatus = "Saved chats are unavailable, so this chat isn't being saved.";
    internal const string DeleteFailedStatus = "That chat couldn't be deleted, so it's still saved. Try again.";

    /// <summary>How long leaving a chat waits for its latest save before asking; short, as the screen waits with it.</summary>
    public static readonly TimeSpan LeaveWait = TimeSpan.FromSeconds(2);

    internal const string TryAgainChoice = "Try again";
    internal const string DiscardChoice = "Discard chat";
    internal const string LeaveChoice = "Leave anyway";
    internal const string SaveFailedQuestion = "The latest changes couldn't be saved. Try again or leave anyway? An earlier version may remain in saved chats, and a pending save may still finish. Latest changes could be lost when the app closes.";
    internal const string SavePendingQuestion = "This chat hasn't finished saving. Try again or leave anyway? An earlier version may remain in saved chats, and the pending save may still finish. Latest changes could be lost when the app closes.";
    internal const string NotSavedMessage = "This chat isn't being saved, so its questions and answers will be cleared. " + Confirmations.CannotBeUndone;

    private readonly IntelligenceConversationViewModel _conversation;
    private readonly IntelligenceChatStorage _storage;
    private readonly IntelligencePreferences _preferences;
    private readonly ISessionService _session;
    private readonly IDialogService _dialogs;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;
    private readonly IntelligenceChatWriter _writer;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private readonly Action<Action>? _dispatch;
    private readonly List<IntelligenceChatItem> _chats = [];
    private readonly List<IntelligenceTurn> _watched = [];

    /// <summary>Moves on at each connection or single-chat change; loads and writes from an older one are dropped.</summary>
    private long _scopeGeneration;
    private long _loadVersion;
    private long _loadedGeneration = -1;
    private string? _loadedScope;
    private IIntelligenceChatStore? _store;

    private IntelligenceChatItem? _active;
    private long _activeRevision = -1;
    private string _activity = "";

    /// <summary>A transcript revision used in single-chat mode: never saved.</summary>
    private long? _unsavedRevision;
    private bool _singleChatMode;
    private bool _changing;
    private bool _disposed;
    private bool _wanted;
    private bool _leaving;

    private ITimer? _timer;
    private long _timerVersion;
    private long _saveNumber;
    private long _contentVersion;

    public IntelligenceChatListViewModel(IntelligenceConversationViewModel conversation, IntelligenceChatStorage storage,
        IntelligencePreferences preferences, ISessionService session, IDialogService dialogs, TimeProvider time,
        ILogger<IntelligenceChatListViewModel> logger, Action<Action>? dispatch = null)
    {
        _conversation = conversation;
        _storage = storage;
        _preferences = preferences;
        _session = session;
        _dialogs = dialogs;
        _time = time;
        _logger = logger;
        _dispatch = dispatch;
        _writer = new IntelligenceChatWriter(logger);
        _writer.Failed += WriteFailed;
        _singleChatMode = preferences.SingleChatMode;
        if (_singleChatMode) _unsavedRevision = conversation.ChatRevision;
        _status = _singleChatMode ? SingleChatStatus : "";

        // After the conversation's own handlers, which it subscribed when it was made.
        session.StateChanged += ConnectionChanged;
        preferences.Changed += PreferencesChanged;
        storage.StateChanged += StorageChanged;
        conversation.PropertyChanged += ConversationChanged;
        conversation.Turns.CollectionChanged += TurnsChanged;
        Watch();
    }

    /// <summary>The writer, for tests of ordering and coalescing.</summary>
    internal IntelligenceChatWriter Writer => _writer;

    /// <summary>Wraps the store each load opens, for tests that make reads and writes wait or fail.</summary>
    internal Func<IIntelligenceChatStore, IIntelligenceChatStore>? WrapStore { get; set; }

    /// <summary>The list, pinned first then most recently used.</summary>
    public IReadOnlyList<IntelligenceChatItem> Chats => _chats;

    public ObservableCollection<IntelligenceChatGroup> Groups { get; } = [];

    /// <summary>The chat showing in the conversation, once it has been saved or reopened.</summary>
    public IntelligenceChatItem? Active => _active is not null && _activeRevision == _conversation.ChatRevision ? _active : null;

    private string _status;
    /// <summary>What the list can't do right now, or how many chats couldn't be read; empty when all is well.</summary>
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public bool IsSingleChatMode => _singleChatMode;

    private bool _isReadOnly;
    /// <summary>Saved chats couldn't be read, so nothing is written over them.</summary>
    public bool IsReadOnly { get => _isReadOnly; private set => SetProperty(ref _isReadOnly, value); }

    /// <summary>Chats are being kept: loaded for this server, writable and not in single-chat mode.</summary>
    /// <remarks>Reads the setting itself too, so no save slips in before its change is handled.</remarks>
    public bool CanPersist => !_disposed && !_singleChatMode && !_preferences.SingleChatMode && !IsReadOnly && _store is not null && _loadedScope is not null
        && _loadedGeneration == Interlocked.Read(ref _scopeGeneration);

    /// <summary>Opening, pinning, renaming and deleting wait for an answer to finish.</summary>
    public bool CanManage => CanPersist && !_conversation.IsRunning && _conversation.IsTranscriptCurrent;

    /// <summary>Whether the list has anything to show or say.</summary>
    public bool IsListAvailable => CanPersist;

    /// <summary>
    /// Whether leaving the chat on screen keeps it: it is empty, or its latest
    /// text is confirmed on disk. A save waiting, superseded or failed doesn't count.
    /// </summary>
    public bool KeepsCurrentChat => CanPersist && _unsavedRevision != _conversation.ChatRevision
        && (!HasContent() || Active is { } item && IsWritten(item));

    /// <summary>A New chat or open is deciding what becomes of the chat on screen.</summary>
    public bool IsLeaving => _leaving;

    /// <summary>
    /// Reads this server's chats, once per connection, and reopens the chat that
    /// was open into an empty transcript. Reading waits for writes already queued,
    /// and runs off the UI thread.
    /// </summary>
    public async Task LoadAsync()
    {
        _wanted = true;
        if (_disposed) return;
        var generation = Interlocked.Read(ref _scopeGeneration);
        if (_singleChatMode)
        {
            Status = SingleChatStatus;
            return;
        }

        if (_session.Connection is not { } connection) return;
        var scope = IntelligenceSavedChats.Scope(connection);
        if (_loadedGeneration == generation && _store is not null) return;
        var version = ++_loadVersion;
        bool Stale() => _disposed || version != _loadVersion || generation != Interlocked.Read(ref _scopeGeneration);

        IIntelligenceChatStore? store;
        try { store = await _storage.OpenAsync(); }
        catch (Exception) { store = null; }
        if (Stale()) return;
        if (store is not null && WrapStore is { } wrap) store = wrap(store);
        if (store is null)
        {
            Status = _storage.UnavailableReason ?? UnavailableStatus;
            Changed();
            return;
        }

        SavedChatCollection saved;
        try
        {
            await _writer.FlushAsync();
            saved = await Task.Run(() => store.Load(scope));
        }
        catch (Exception)
        {
            if (Stale()) return;
            // Never write an empty list over chats that are still there.
            _logger.LogWarning("Saved chats could not be read");
            IsReadOnly = true;
            Status = ReadFailedStatus;
            Changed();
            return;
        }

        if (Stale()) return;
        _store = store;
        _loadedScope = scope;
        _loadedGeneration = generation;
        IsReadOnly = false;
        _chats.Clear();
        _chats.AddRange(saved.Chats.Select(chat => new IntelligenceChatItem(chat)));
        var unreadable = saved.Unreadable?.Count ?? 0;
        if (unreadable > 0) _logger.LogWarning("{Count} saved chats could not be read and were skipped", unreadable);
        Status = unreadable switch
        {
            0 => _unsavedRevision == _conversation.ChatRevision && HasContent() ? UnsavedStatus : "",
            1 => "One saved chat couldn't be read and was set aside. Your other chats are here.",
            _ => $"{unreadable} saved chats couldn't be read and were set aside. Your other chats are here.",
        };

        if (Active is null && !HasContent() && !_conversation.IsRunning && _conversation.IsTranscriptCurrent
            && _chats.FirstOrDefault(chat => chat.Id == saved.ActiveId) is { } reopen)
        {
            Show(reopen);
        }
        else
        {
            // A draft typed or an answer finished while loading couldn't be saved
            // then; Capture still refuses a transcript used in single-chat mode.
            SaveActive();
        }

        Regroup();
        Changed();
    }

    /// <summary>
    /// Shows a saved chat in place of the one on screen, through the same
    /// decision as <see cref="NewChatAsync"/>: the one on screen is kept only
    /// once its latest text is on disk, and otherwise discarded only when the user says so.
    /// </summary>
    public Task<bool> OpenAsync(IntelligenceChatItem item)
    {
        if (!CanOpen(item)) return Task.FromResult(false);
        if (ReferenceEquals(item, Active)) return Task.FromResult(true);
        return LeaveAsync(item);
    }

    /// <summary>Starts an empty chat once the one on screen is kept, or the user agrees to discard it.</summary>
    public Task<bool> NewChatAsync() => CanStartNew() ? LeaveAsync(null) : Task.FromResult(false);

    private bool CanOpen(IntelligenceChatItem item) => CanManage && _chats.Contains(item) && !item.IsDeleting;

    private bool CanStartNew() => !_disposed && !_conversation.IsRunning && _conversation.IsTranscriptCurrent;

    /// <summary>
    /// Saves the chat on screen and waits for it, then asks when it isn't
    /// confirmed on disk. Each answer is checked against the scope generation,
    /// the transcript revision and the target, so a sign-out, server switch,
    /// single-chat change or another chat appearing while a question is up
    /// cancels instead of acting on what was decided for something else.
    /// </summary>
    private async Task<bool> LeaveAsync(IntelligenceChatItem? target)
    {
        if (_leaving) return false;
        _leaving = true;
        Changed();
        try
        {
            var generation = Interlocked.Read(ref _scopeGeneration);
            var revision = _conversation.ChatRevision;
            var contentVersion = _contentVersion;
            bool Current() => !_disposed && generation == Interlocked.Read(ref _scopeGeneration)
                && revision == _conversation.ChatRevision && contentVersion == _contentVersion && (target is null ? CanStartNew() : CanOpen(target));

            while (true)
            {
                var state = await SaveForLeavingAsync();
                if (!Current()) return false;
                if (state == LeaveState.Kept) break;

                var title = target is null ? "Start a new chat?" : $"Open “{target.Title}”?";
                if (state == LeaveState.NotSaved)
                {
                    if (!await _dialogs.ConfirmDestructiveAsync(title, NotSavedMessage, DiscardChoice) || !Current()) return false;
                    break;
                }

                var choice = await _dialogs.ChooseAsync(state == LeaveState.Failed ? SaveFailedQuestion : SavePendingQuestion,
                    [TryAgainChoice], LeaveChoice);
                if (!Current()) return false;
                if (choice == LeaveChoice) break;
                if (choice != TryAgainChoice) return false;
            }

            return target is null ? StartNew() : ShowOther(target);
        }
        finally
        {
            _leaving = false;
            Changed();
        }
    }

    private enum LeaveState { Kept, NotSaved, Failed, Pending }

    /// <summary>
    /// Queues the latest of the chat on screen and waits, at most <see cref="LeaveWait"/>,
    /// for the save that holds it. Acknowledged here rather than through the posted
    /// outcome, so a save that has just finished counts, and one still waiting never does.
    /// </summary>
    private async Task<LeaveState> SaveForLeavingAsync()
    {
        SaveActive();
        if (!HasContent()) return LeaveState.Kept;
        if (!CanPersist || _conversation.ChatRevision == _unsavedRevision || Active is not { } item) return LeaveState.NotSaved;

        var deadline = _time.GetUtcNow() + LeaveWait;
        while (!IsWritten(item) && item.LastSave is { } last && last.Number >= item.RequiredSave)
        {
            var remaining = deadline - _time.GetUtcNow();
            if (remaining <= TimeSpan.Zero) break;
            ChatWriteResult result;
            try { result = await last.Saving.WaitAsync(remaining, _time); }
            catch (TimeoutException) { break; }
            Acknowledge(item, last.Number, last.Generation, result);
            // Superseded: a newer save took its place and holds the same text; wait for that one.
            if (result != ChatWriteResult.Superseded || item.LastSave?.Number == last.Number) break;
        }

        if (!ReferenceEquals(item, Active) || !CanPersist) return LeaveState.NotSaved;
        if (IsWritten(item)) return LeaveState.Kept;
        return item.FailedSave >= item.RequiredSave && item.NeedsRetry ? LeaveState.Failed : LeaveState.Pending;
    }

    /// <summary>
    /// The chat's latest text is on disk: a save at least as new as <see cref="IntelligenceChatItem.RequiredSave"/>
    /// was written. Also reads the save's own outcome, as its acknowledgement is posted and may not have run yet.
    /// </summary>
    private bool IsWritten(IntelligenceChatItem item) =>
        item.WrittenSave >= item.RequiredSave
        || item.LastSave is { } last && last.Number >= item.RequiredSave && last.Generation == Interlocked.Read(ref _scopeGeneration)
            && last.Saving is { IsCompletedSuccessfully: true, Result: ChatWriteResult.Written };

    private bool ShowOther(IntelligenceChatItem item)
    {
        var shown = Show(item);
        Regroup();
        Changed();
        return shown;
    }

    private bool StartNew()
    {
        if (!Replace(() => _conversation.NewChat())) return false;
        Unbind();
        if (_singleChatMode) _unsavedRevision = _conversation.ChatRevision;
        else
        {
            _unsavedRevision = null;
            if (Status == UnsavedStatus) Status = "";
        }

        Regroup();
        Changed();
        return true;
    }

    public bool TogglePin(IntelligenceChatItem item)
    {
        if (!CanManage || !_chats.Contains(item) || item.IsDeleting) return false;
        item.Data.IsPinned = !item.Data.IsPinned;
        Persist(item);
        Regroup();
        return true;
    }

    /// <summary>Asks for a name, then renames.</summary>
    public async Task<bool> RenameAsync(IntelligenceChatItem item)
    {
        if (!CanManage || !_chats.Contains(item)) return false;
        var generation = Interlocked.Read(ref _scopeGeneration);
        var name = await _dialogs.PromptAsync("Rename chat", "Choose a name for this chat.", "Rename", item.Title);
        return name is not null && generation == Interlocked.Read(ref _scopeGeneration) && Rename(item, name);
    }

    public bool Rename(IntelligenceChatItem item, string name)
    {
        if (!CanManage || !_chats.Contains(item) || item.IsDeleting || string.IsNullOrWhiteSpace(name)) return false;
        item.Data.Title = IntelligenceSavedChats.Title(name);
        item.Data.IsRenamed = true;
        Persist(item);
        Regroup();
        return true;
    }

    /// <summary>
    /// Deletes after the usual confirmation (#146), and reports success only once
    /// the file is gone. Deleting the open chat starts a new one straight away.
    /// </summary>
    /// <remarks>
    /// The open chat is cleared before the delete is written, so nothing typed
    /// meanwhile can be saved into it. If the delete fails, or a sign-out, server
    /// switch or single-chat mode drops it, the chat stays in the list (or comes
    /// back with it) to reopen or delete again; nothing says it was deleted.
    /// </remarks>
    public async Task<bool> DeleteAsync(IntelligenceChatItem item)
    {
        if (!CanManage || !_chats.Contains(item) || item.IsDeleting) return false;
        var generation = Interlocked.Read(ref _scopeGeneration);
        if (!await _dialogs.ConfirmDestructiveAsync("Delete chat?",
            $"Delete “{item.Title}” and its messages from this phone? {Confirmations.CannotBeUndone}", "Delete")) return false;
        if (generation != Interlocked.Read(ref _scopeGeneration) || !CanManage || !_chats.Contains(item) || item.IsDeleting) return false;

        item.IsDeleting = true;
        // No tag: the outcome is reported here, not as a failed save.
        var deleting = _writer.Delete(_store!, _loadedScope!, item.Id, tag: -1);
        if (ReferenceEquals(item, Active) || ReferenceEquals(item, _active))
        {
            Replace(() => _conversation.NewChat());
            Unbind();
            Regroup();
            Changed();
        }

        ChatWriteResult result;
        try { result = await deleting; }
        finally { item.IsDeleting = false; }

        // Signed out, another server or single-chat mode: that list is gone, and
        // a dropped delete left the file for the list that's loaded next. CanPersist
        // reads the setting itself, so this holds before the mode change is handled.
        if (_disposed || generation != Interlocked.Read(ref _scopeGeneration) || !CanPersist) return false;
        if (result != ChatWriteResult.Written)
        {
            Status = result == ChatWriteResult.Unavailable ? _storage.UnavailableReason ?? UnavailableStatus : DeleteFailedStatus;
            return false;
        }

        _chats.Remove(item);
        if (Status == DeleteFailedStatus) Status = "";
        Regroup();
        Changed();
        return true;
    }

    /// <summary>
    /// The app is going to the background: queue the latest and wait for the
    /// writer. The host bounds the wait (with its iOS background reservation).
    /// </summary>
    public async Task SaveNowAsync()
    {
        // Posted acknowledgements may still be waiting. Observe completed saves
        // before deciding which inactive edits need a suspension retry.
        foreach (var chat in _chats.ToArray())
            if (chat.LastSave is { } previous && previous.Saving.IsCompletedSuccessfully)
                Acknowledge(chat, previous.Number, previous.Generation, previous.Saving.Result);
        SaveActive();
        if (CanPersist)
            foreach (var chat in _chats.Where(chat => !ReferenceEquals(chat, Active) && chat.NeedsRetry && !chat.IsDeleting).ToArray())
                Queue(chat, Snapshot(chat.Data), makeActive: false);
        await _writer.FlushAsync();
        foreach (var chat in _chats.ToArray())
            if (chat.LastSave is { } last) Acknowledge(chat, last.Number, last.Generation, await last.Saving);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelTimer();
        Unwatch();
        _session.StateChanged -= ConnectionChanged;
        _preferences.Changed -= PreferencesChanged;
        _storage.StateChanged -= StorageChanged;
        _conversation.PropertyChanged -= ConversationChanged;
        _conversation.Turns.CollectionChanged -= TurnsChanged;
        _writer.Failed -= WriteFailed;
    }

    private bool HasContent() => _conversation.Turns.Count > 0 || !string.IsNullOrWhiteSpace(_conversation.Draft);

    private bool Show(IntelligenceChatItem item)
    {
        var turns = IntelligenceSavedChats.Turns(item.Data);
        if (!Replace(() => _conversation.Restore(turns, item.Data.Draft ?? "", item.Data.ReplyStyle))) return false;
        Bind(item);
        _unsavedRevision = null;
        if (Status == UnsavedStatus) Status = "";
        // Recorded as the chat to reopen; a reopened chat's activity time is unchanged.
        SaveActive(force: true);
        return true;
    }

    /// <summary>Changes the transcript without the change counting as an edit to the bound chat.</summary>
    private bool Replace(Func<bool> change)
    {
        CancelTimer();
        _changing = true;
        try { return change(); }
        finally { _changing = false; }
    }

    private void Bind(IntelligenceChatItem item)
    {
        if (_active is not null) _active.IsOpen = false;
        _active = item;
        _activeRevision = _conversation.ChatRevision;
        _activity = Activity();
        item.IsOpen = true;
    }

    private void Unbind()
    {
        if (_active is not null) _active.IsOpen = false;
        _active = null;
        _activeRevision = _conversation.ChatRevision;
        _activity = "";
    }

    /// <summary>Changes only when the conversation moves on, so drafts, renames and reopening don't reorder the list.</summary>
    private string Activity() => _conversation.Turns.Count + ":" + (_conversation.Turns.Count > 0 ? (int)_conversation.Turns[^1].State : -1);

    private void Persist(IntelligenceChatItem item)
    {
        if (ReferenceEquals(item, Active))
        {
            SaveActive(force: true);
            return;
        }

        item.Refresh(_time.GetUtcNow(), _time.LocalTimeZone);
        Queue(item, Snapshot(item.Data), makeActive: false);
    }

    /// <summary>Captures the open chat and queues it, when it may be kept.</summary>
    private void SaveActive(bool force = false)
    {
        CancelTimer();
        if (Capture(force, out var content) is { } snapshot && Active is { } item)
        {
            var number = Queue(item, snapshot, makeActive: true);
            if (content) item.RequiredSave = number;
        }
    }

    /// <summary>Queues a save, numbered so its outcome is recorded against the right capture.</summary>
    private long Queue(IntelligenceChatItem item, SavedIntelligenceChat snapshot, bool makeActive)
    {
        var number = ++_saveNumber;
        var generation = _loadedGeneration;
        var saving = _writer.Save(_store!, _loadedScope!, snapshot, makeActive, generation);
        item.LastSave = (number, generation, saving);
        _ = TrackAsync(item, number, generation, saving);
        return number;
    }

    private async Task TrackAsync(IntelligenceChatItem item, long number, long generation, Task<ChatWriteResult> saving)
    {
        var result = await saving;
        OnUi(() => Acknowledge(item, number, generation, result));
    }

    /// <summary>
    /// Records a save's outcome. Written means the disk holds this capture or a
    /// later one, since the writer keeps order; a failure or refusal leaves the
    /// chat to be written again. Superseded says nothing: the save that replaced
    /// it reports instead. Idempotent, as suspension acknowledges too.
    /// </summary>
    private void Acknowledge(IntelligenceChatItem item, long number, long generation, ChatWriteResult result)
    {
        if (_disposed || result == ChatWriteResult.Superseded || generation != Interlocked.Read(ref _scopeGeneration)) return;
        var retry = item.NeedsRetry;
        if (result == ChatWriteResult.Written) item.WrittenSave = Math.Max(item.WrittenSave, number);
        else item.FailedSave = Math.Max(item.FailedSave, number);

        if (result == ChatWriteResult.Written && Status == SaveFailedStatus && !_chats.Any(chat => chat.NeedsRetry)) Status = "";
        if (retry != item.NeedsRetry) Changed();
    }

    /// <param name="content">Whether the capture carries text, a draft or detail the disk may not have yet.</param>
    private SavedIntelligenceChat? Capture(bool force, out bool content)
    {
        content = false;
        if (_changing || !CanPersist || _conversation.ChatRevision == _unsavedRevision || !_conversation.IsTranscriptCurrent
            || _session.Connection is not { } connection || IntelligenceSavedChats.Scope(connection) != _loadedScope)
        {
            return null;
        }

        var item = Active;
        var created = item is null;
        if (item is null)
        {
            // Bound to another revision: that transcript is gone, so is any edit to it.
            if (_active is not null && _activeRevision != _conversation.ChatRevision) Unbind();
            if (!HasContent()) return null;
            item = new IntelligenceChatItem(new SavedIntelligenceChat { Model = IntelligenceSavedChats.OnDeviceModel });
            _chats.Insert(0, item);
            Bind(item);
            _activity = "";
            Regroup();
            Changed();
        }

        var chat = item.Data;
        var messages = IntelligenceSavedChats.Messages(_conversation.Turns);
        var activity = Activity();
        var unchanged = !created && activity == _activity && chat.Draft == _conversation.Draft
            && chat.ReplyStyle == _conversation.SelectedDetailOverride && SameMessages(chat.Messages, messages);
        if (!force && !item.NeedsRetry && unchanged) return null;
        // A retry of unchanged text leaves the save that first carried it as the one to confirm.
        content = !unchanged;

        chat.Draft = _conversation.Draft;
        chat.ReplyStyle = _conversation.SelectedDetailOverride;
        chat.Model ??= IntelligenceSavedChats.OnDeviceModel;
        chat.Messages = messages;
        if (!chat.IsRenamed && _conversation.Turns.Count > 0) chat.Title = IntelligenceSavedChats.Title(_conversation.Turns[0].Question);
        if (activity != _activity)
        {
            _activity = activity;
            chat.UpdatedAt = _time.GetUtcNow();
            Regroup();
        }
        else item.Refresh(_time.GetUtcNow(), _time.LocalTimeZone);

        return Snapshot(chat);
    }

    /// <summary>Unchanged text and states need no write; a streaming answer always differs.</summary>
    private static bool SameMessages(List<SavedIntelligenceMessage>? saved, List<SavedIntelligenceMessage> now) =>
        saved is not null && saved.Count == now.Count && saved.Zip(now).All(pair => pair.First is { } before
            && before.Text == pair.Second.Text && before.Outcome == pair.Second.Outcome
            && before.IsIncomplete == pair.Second.IsIncomplete && before.FailureText == pair.Second.FailureText
            && Count(before.ToolObservations) == pair.Second.ToolObservations.Count
            && Count(before.PriorReferences) == pair.Second.PriorReferences.Count
            && Count(before.Images) == pair.Second.Images.Count);

    private static int Count<T>(List<T>? list) => list?.Count ?? 0;

    /// <summary>
    /// A copy for the writer to serialise while the UI thread carries on. Each
    /// capture builds fresh messages, so only the chat's own fields and lists need copying.
    /// </summary>
    private static SavedIntelligenceChat Snapshot(SavedIntelligenceChat chat) => new()
    {
        Id = chat.Id,
        Title = chat.Title,
        IsRenamed = chat.IsRenamed,
        IsPinned = chat.IsPinned,
        UpdatedAt = chat.UpdatedAt,
        Model = chat.Model,
        ReplyStyle = chat.ReplyStyle,
        Outfit = chat.Outfit,
        Draft = chat.Draft,
        History = (JsonArray)(chat.History ?? new JsonArray()).DeepClone(),
        Messages = (chat.Messages ?? []).ToList(),
        // Kept as the file had them (desktop's Spotlight and looked-at context);
        // the phone has no Spotlight in chats yet (#231).
        Spotlight = (chat.Spotlight ?? []).ToList(),
        LookedAt = (chat.LookedAt ?? []).ToList(),
    };

    private void Regroup()
    {
        var now = _time.GetUtcNow();
        var zone = _time.LocalTimeZone;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        _chats.Sort((a, b) => a.IsPinned != b.IsPinned ? b.IsPinned.CompareTo(a.IsPinned) : b.UpdatedAt.CompareTo(a.UpdatedAt));
        foreach (var chat in _chats) chat.Refresh(now, zone);
        Groups.Clear();
        foreach (var group in _chats
            .Select(chat => (Chat: chat, Group: IntelligenceChatActivityGroups.ForDate(
                DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(chat.UpdatedAt, zone).DateTime), today, chat.IsPinned)))
            .GroupBy(entry => entry.Group)
            .OrderBy(group => group.Key.Order))
        {
            Groups.Add(new IntelligenceChatGroup(group.Key.Label, group.Select(entry => entry.Chat)));
        }

        OnPropertyChanged(nameof(Chats));
        OnPropertyChanged(nameof(Active));
    }

    private void Changed()
    {
        OnPropertyChanged(nameof(CanPersist));
        OnPropertyChanged(nameof(CanManage));
        OnPropertyChanged(nameof(IsListAvailable));
        OnPropertyChanged(nameof(IsSingleChatMode));
        OnPropertyChanged(nameof(KeepsCurrentChat));
        OnPropertyChanged(nameof(IsLeaving));
        OnPropertyChanged(nameof(Active));
    }

    private void Schedule(bool restart)
    {
        if (!CanPersist || _changing) return;
        if (_timer is not null && !restart) return;
        _timer?.Dispose();
        var version = ++_timerVersion;
        _timer = _time.CreateTimer(_ => OnUi(() =>
        {
            if (version != _timerVersion) return;
            _timer?.Dispose();
            _timer = null;
            SaveActive();
        }), null, SaveDelay, Timeout.InfiniteTimeSpan);
    }

    private void CancelTimer()
    {
        _timerVersion++;
        _timer?.Dispose();
        _timer = null;
    }

    private void ConversationChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(IntelligenceConversationViewModel.Draft) or nameof(IntelligenceConversationViewModel.SelectedDetail):
                _contentVersion++;
                Schedule(restart: true);
                break;
            case nameof(IntelligenceConversationViewModel.IsRunning) or nameof(IntelligenceConversationViewModel.IsTranscriptCurrent)
                or nameof(IntelligenceConversationViewModel.ChatRevision):
                Changed();
                break;
        }
    }

    private void TurnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _contentVersion++;
        Watch();
        // A new question is kept straight away, so a forced quit still has it.
        if (e.Action == NotifyCollectionChangedAction.Add && !_changing) SaveActive();
    }

    private void TurnChanged(object? sender, PropertyChangedEventArgs e)
    {
        _contentVersion++;
        if (_changing) return;
        if (e.PropertyName == nameof(IntelligenceTurn.State) && sender is IntelligenceTurn { State: not IntelligenceTurnState.Running })
        {
            // Finished, stopped or failed: kept now rather than after the delay.
            SaveActive();
            return;
        }

        // Streaming: no restart, so a save follows at most every SaveDelay.
        Schedule(restart: false);
    }

    private void Watch()
    {
        Unwatch();
        foreach (var turn in _conversation.Turns)
        {
            turn.PropertyChanged += TurnChanged;
            _watched.Add(turn);
        }
    }

    private void Unwatch()
    {
        foreach (var turn in _watched) turn.PropertyChanged -= TurnChanged;
        _watched.Clear();
    }

    private void ConnectionChanged(object? sender, EventArgs e)
    {
        Interlocked.Increment(ref _scopeGeneration);
        // Signed out: nothing waiting may be written. The storage's wipe refuses
        // anything already under way; a server switch lets the previous server's
        // own saves finish, in that server's scope.
        if (_session.Connection is null) _writer.Invalidate();
        OnUi(() =>
        {
            CancelTimer();
            Unbind();
            _store = null;
            _loadedScope = null;
            _chats.Clear();
            IsReadOnly = false;
            Status = _singleChatMode ? SingleChatStatus : "";
            Regroup();
            Changed();
            if (_wanted && _session.Connection is not null) _ = LoadAsync();
        });
    }

    private void PreferencesChanged(object? sender, EventArgs e)
    {
        // Invalidate synchronously, even when UI cleanup must be dispatched.
        // Waiting snapshots must not escape after privacy mode is enabled.
        if (_preferences.SingleChatMode) _writer.Invalidate();
        OnUi(() =>
    {
        var single = _preferences.SingleChatMode;
        if (single == _singleChatMode) return;
        _singleChatMode = single;
        Interlocked.Increment(ref _scopeGeneration);
        CancelTimer();
        if (single)
        {
            // From here the transcript is private: unbound, so none of it reaches the file it came from.
            _unsavedRevision = _conversation.ChatRevision;
            Unbind();
            _store = null;
            _loadedScope = null;
            _chats.Clear();
            Status = SingleChatStatus;
        }
        else
        {
            // The current revision, not the one marked on entering: a reconnect
            // in single-chat mode moved the revision on, and the text since is private too.
            _unsavedRevision = HasContent() ? _conversation.ChatRevision : null;
            Status = "";
        }

        Regroup();
        Changed();
        if (!single && _wanted) _ = LoadAsync();
    });
    }

    private void StorageChanged(object? sender, EventArgs e) => OnUi(() =>
    {
        if (_storage.State != IntelligenceChatStorageState.Unavailable || _singleChatMode) return;
        _store = null;
        Status = _storage.UnavailableReason ?? UnavailableStatus;
        Changed();
    });

    private void WriteFailed(long tag, bool unavailable) => OnUi(() =>
    {
        if (tag != Interlocked.Read(ref _scopeGeneration) || _singleChatMode) return;
        Status = unavailable ? _storage.UnavailableReason ?? UnavailableStatus : SaveFailedStatus;
    });

    private void OnUi(Action action)
    {
        if (_dispatch is not null) _dispatch(action);
        else if (_ui is null || SynchronizationContext.Current == _ui) action();
        else _ui.Post(_ => action(), null);
    }
}
