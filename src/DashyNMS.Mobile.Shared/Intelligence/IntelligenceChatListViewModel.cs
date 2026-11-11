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
/// </remarks>
public sealed class IntelligenceChatListViewModel : ObservableObject, IDisposable
{
    /// <summary>How long after the last change a chat is saved, and how often while streaming.</summary>
    public static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(2);

    internal const string SingleChatStatus = "Single-chat mode is on: this chat is kept only while the app is open, and saved chats are hidden.";
    internal const string UnsavedStatus = "This chat isn't saved because it was used in single-chat mode. Start a new chat to save again.";
    internal const string ReadFailedStatus = "Saved chats couldn't be read. They've been left as they are, and this chat isn't being saved.";
    internal const string SaveFailedStatus = "This chat couldn't be saved. It stays on screen, and saving is tried again with the next change.";
    internal const string UnavailableStatus = "Saved chats are unavailable, so this chat isn't being saved.";

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

    private ITimer? _timer;
    private long _timerVersion;

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

    /// <summary>Whether starting a new chat keeps the current one.</summary>
    public bool KeepsCurrentChat => CanPersist && _unsavedRevision != _conversation.ChatRevision;

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

        Regroup();
        Changed();
    }

    /// <summary>Shows a saved chat, saving the one that was showing first.</summary>
    public bool Open(IntelligenceChatItem item)
    {
        if (!CanManage || !_chats.Contains(item)) return false;
        if (ReferenceEquals(item, Active)) return true;
        SaveActive();
        var shown = Show(item);
        Regroup();
        return shown;
    }

    /// <summary>Starts an empty chat; the one showing stays saved.</summary>
    public bool NewChat()
    {
        if (_conversation.IsRunning || !_conversation.IsTranscriptCurrent) return false;
        SaveActive();
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
        if (!CanManage || !_chats.Contains(item)) return false;
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
        if (!CanManage || !_chats.Contains(item) || string.IsNullOrWhiteSpace(name)) return false;
        item.Data.Title = IntelligenceSavedChats.Title(name);
        item.Data.IsRenamed = true;
        Persist(item);
        Regroup();
        return true;
    }

    /// <summary>Deletes after the usual confirmation (#146). Deleting the open chat starts a new one.</summary>
    public async Task<bool> DeleteAsync(IntelligenceChatItem item)
    {
        if (!CanManage || !_chats.Contains(item)) return false;
        var generation = Interlocked.Read(ref _scopeGeneration);
        if (!await _dialogs.ConfirmDestructiveAsync("Delete chat?",
            $"Delete “{item.Title}” and its messages from this phone? {Confirmations.CannotBeUndone}", "Delete")) return false;
        if (generation != Interlocked.Read(ref _scopeGeneration) || !CanManage || !_chats.Contains(item)) return false;

        var open = ReferenceEquals(item, Active);
        if (open) CancelTimer();
        _writer.Delete(_store!, _loadedScope!, item.Id, generation);
        _chats.Remove(item);
        if (open || ReferenceEquals(item, _active))
        {
            // Cleared without saving: the chat it showed is gone.
            Replace(() => _conversation.NewChat());
            Unbind();
        }

        Regroup();
        Changed();
        return true;
    }

    /// <summary>
    /// The app is going to the background: queue the latest and wait for the
    /// writer. The host bounds the wait (with its iOS background reservation).
    /// </summary>
    public Task SaveNowAsync()
    {
        SaveActive();
        return _writer.FlushAsync();
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
        _writer.Save(_store!, _loadedScope!, Snapshot(item.Data), makeActive: false, _loadedGeneration);
    }

    /// <summary>Captures the open chat and queues it, when it may be kept.</summary>
    private void SaveActive(bool force = false)
    {
        CancelTimer();
        if (Capture(force) is { } snapshot)
        {
            _writer.Save(_store!, _loadedScope!, snapshot, makeActive: true, _loadedGeneration);
        }
    }

    private SavedIntelligenceChat? Capture(bool force)
    {
        if (_changing || !CanPersist || _conversation.ChatRevision == _unsavedRevision || !_conversation.IsTranscriptCurrent
            || _session.Connection is not { } connection || IntelligenceSavedChats.Scope(connection) != _loadedScope)
        {
            return null;
        }

        var item = Active;
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
        if (!force && activity == _activity && chat.Draft == _conversation.Draft && chat.ReplyStyle == _conversation.SelectedDetail
            && SameMessages(chat.Messages, messages))
        {
            return null;
        }

        chat.Draft = _conversation.Draft;
        chat.ReplyStyle = _conversation.SelectedDetail;
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
        Watch();
        // A new question is kept straight away, so a forced quit still has it.
        if (e.Action == NotifyCollectionChangedAction.Add && !_changing) SaveActive();
    }

    private void TurnChanged(object? sender, PropertyChangedEventArgs e)
    {
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

    private void PreferencesChanged(object? sender, EventArgs e) => OnUi(() =>
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
            if (!HasContent()) _unsavedRevision = null;
            Status = "";
        }

        Regroup();
        Changed();
        if (!single && _wanted) _ = LoadAsync();
    });

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
