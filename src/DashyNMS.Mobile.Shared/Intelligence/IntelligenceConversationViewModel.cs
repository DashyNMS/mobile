using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Intelligence;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Intelligence;

public enum IntelligenceTurnState { Running, Completed, Stopped, Interrupted, Failed }
public enum IntelligenceLookupState { Running, Completed, Failed, Stopped }

/// <summary>A retained answer attempt. Retry appends a new attempt so a partial answer is never lost.</summary>
public sealed class IntelligenceTurn(string question, string detail) : ObservableObject
{
    public string Question { get; } = question;
    public string Detail { get; } = detail;

    /// <summary>When it was asked; a saved chat (#229) keeps it, so a reopened chat shows its own times.</summary>
    public DateTimeOffset AskedAt { get; internal init; } = DateTimeOffset.UtcNow;

    private DateTimeOffset? _answeredAt;
    /// <summary>
    /// When this session's answer settled, for "Answered in 6s". A reopened saved
    /// chat has none, rather than a duration measured from its reopening.
    /// </summary>
    public DateTimeOffset? AnsweredAt { get => _answeredAt; internal set => SetProperty(ref _answeredAt, value); }
    private string _answer = "";
    /// <summary>
    /// Always carries verified markers in their double-bracket form, so the answer renders,
    /// copies and saves the same way. A model sometimes writes <c>[alert:12]</c> for
    /// <c>[[alert:12]]</c>; see <see cref="IntelligenceMarkdown.RepairMarkers"/>.
    /// </summary>
    public string Answer { get => _answer; internal set => SetProperty(ref _answer, IntelligenceMarkdown.RepairMarkers(value, _references)); }
    private string _message = "";
    public string Message { get => _message; internal set => SetProperty(ref _message, value); }
    private IntelligenceTurnState _state = IntelligenceTurnState.Running;
    public IntelligenceTurnState State { get => _state; internal set => SetProperty(ref _state, value); }
    private bool _historyOmitted;
    public bool HistoryOmitted { get => _historyOmitted; internal set => SetProperty(ref _historyOmitted, value); }
    public ObservableCollection<IntelligenceLookupProgress> Lookups { get; } = [];

    private IReadOnlyList<IntelligenceEvidence> _evidence = [];
    /// <summary>
    /// Typed objects the lookups showed the model in this chat, newest observation
    /// per key, with the exact filter values their chips open (#228). Answer chips
    /// resolve only against these.
    /// </summary>
    public IReadOnlyList<IntelligenceEvidence> Evidence
    {
        get => _evidence;
        private set
        {
            if (!SetProperty(ref _evidence, value)) return;
            _references = value.Select(e => e.Reference).ToArray();
            OnPropertyChanged(nameof(References));
            // A reopened chat restores its answer before its evidence.
            Answer = _answer;
        }
    }

    private IReadOnlyList<IntelligenceInlineReference> _references = [];
    /// <summary>What the renderer resolves markers against: the display side of <see cref="Evidence"/>.</summary>
    public IReadOnlyList<IntelligenceInlineReference> References => _references;

    private IReadOnlyList<IntelligenceImage> _images = [];
    /// <summary>Validated images returned with the answer; never fetched by the app.</summary>
    public IReadOnlyList<IntelligenceImage> Images { get => _images; internal set => SetProperty(ref _images, IntelligenceAnswerImages.Accept(value)); }

    /// <summary>
    /// A later observation of the same object replaces the earlier one, filter value
    /// included, keeping first-seen order. A different object behind the same marker key
    /// (fault 12 after legacy alert 12, both <c>[[alert:12]]</c>) removes the key for the rest
    /// of the answer: neither may claim the other's chip. Both objects are kept in
    /// <see cref="Withdrawn"/>, so a later turn or a reopened saved chat meets the same
    /// conflict again rather than restoring the chip from an earlier turn.
    /// </summary>
    internal void AddEvidence(IEnumerable<IntelligenceEvidence> evidence)
    {
        var merged = _evidence.ToList();
        foreach (var item in evidence)
        {
            var key = item.Reference.Key;
            if (_conflicting.Contains(key))
            {
                Withdraw(item);
                continue;
            }
            var index = merged.FindIndex(e => e.Reference.Key == key);
            if (index < 0) merged.Add(item);
            else if (SameObject(merged[index], item)) merged[index] = item;
            else
            {
                Withdraw(merged[index]);
                Withdraw(item);
                merged.RemoveAt(index);
                _conflicting.Add(key);
            }
        }
        Evidence = merged;
    }

    private readonly HashSet<string> _conflicting = new(StringComparer.Ordinal);
    private readonly List<IntelligenceEvidence> _withdrawn = [];

    /// <summary>Evidence withdrawn because another object claimed its marker key; never rendered as a chip.</summary>
    internal IReadOnlyList<IntelligenceEvidence> Withdrawn => _withdrawn;

    /// <summary>What a later turn or a saved chat carries forward: the chips and the conflicts that withdrew others.</summary>
    internal IEnumerable<IntelligenceEvidence> Carried => _evidence.Concat(_withdrawn);

    private void Withdraw(IntelligenceEvidence item)
    {
        if (!_withdrawn.Any(w => w.Reference.Key == item.Reference.Key && SameObject(w, item))) _withdrawn.Add(item);
    }

    private static bool SameObject(IntelligenceEvidence a, IntelligenceEvidence b)
        => a.Reference.Object?.Kind == b.Reference.Object?.Kind && a.Reference.Object?.Id == b.Reference.Object?.Id;
}

/// <summary>Applies Core's image limits so an oversized or non-image attachment is never decoded.</summary>
public static class IntelligenceAnswerImages
{
    public const int MaxImages = 8;

    public static IReadOnlyList<IntelligenceImage> Accept(IEnumerable<IntelligenceImage>? images) => (images ?? [])
        .Where(i => i is not null && i.Data is { Length: > 0 and <= IntelligenceImage.MaxBytes }
            && i.MimeType is "image/png" or "image/jpeg")
        .Take(MaxImages).ToArray();
}

/// <summary>Lookup evidence stays in memory for answer links and is never logged.</summary>
public sealed class IntelligenceLookupProgress(string name) : ObservableObject
{
    public string Name { get; } = name;
    private string _result = "";
    public string Result { get => _result; internal set => SetProperty(ref _result, value); }
    private IntelligenceLookupState _state;
    public IntelligenceLookupState State { get => _state; internal set => SetProperty(ref _state, value); }
}

/// <summary>
/// UI-owned, in-memory conversation over a model. Construct and call from the UI
/// context; native streaming and lookup callbacks are posted back to that context.
/// Each question uses a fresh native session with bounded explicit history.
/// </summary>
public sealed class IntelligenceConversationViewModel : ObservableObject, IDisposable
{
    private readonly IIntelligenceModel _model;
    private readonly IntelligencePreferences _preferences;
    private readonly IIntelligenceLookups _lookups;
    private readonly ISessionService _session;
    private readonly SynchronizationContext? _ui = SynchronizationContext.Current;
    private readonly Action<Action>? _dispatch;
    private long _chatGeneration;
    private Run? _active;
    private long _networkGeneration;
    private bool _disposed;
    private string _notice = "";
    public string Notice { get => _notice; private set => SetProperty(ref _notice, value); }
    public ObservableCollection<IntelligenceTurn> Turns { get; } = [];

    private string _draft = "";
    /// <summary>
    /// The composer's unsent text. Kept here, not only in the page, so a saved
    /// chat (#229) reopens with it and a connection change clears it with the turns.
    /// </summary>
    public string Draft { get => _draft; set => SetProperty(ref _draft, value ?? ""); }

    private string? _selectedDetail;
    /// <summary>The chat's explicit selection; null preserves following the Settings default across restart.</summary>
    public string? SelectedDetailOverride => _selectedDetail;
    /// <summary>The detail chosen for the next answer, or null for the saved default; a saved chat keeps it.</summary>
    public string SelectedDetail
    {
        get => _selectedDetail ?? _preferences.Detail;
        set => SetProperty(ref _selectedDetail, NormaliseDetail(value));
    }
    public bool IsRunning => Volatile.Read(ref _active) is not null;
    /// <summary>False immediately at a connection transition, before queued UI cleanup runs.</summary>
    public bool IsTranscriptCurrent => _chatGeneration == Volatile.Read(ref _networkGeneration);
    public long ChatRevision { get; private set; }
    public bool OwnsTurn(IntelligenceTurn turn) => IsTranscriptCurrent && Turns.Contains(turn);
    public bool CanAsk => !_disposed && !IsRunning && _preferences.Enabled
        && _model.Availability == IntelligenceAvailability.Available && _session.Connection is not null
        && IsTranscriptCurrent;
    internal static readonly string[][] StarterSets =
    [
        ["How is the network doing?", "Which open alerts need attention?", "Which devices are down?"],
        ["Summarise the active alerts.", "How many devices are up and down?", "Which alerts are unacknowledged?"],
        ["Give me a brief network overview.", "Which devices need attention?", "What are the most severe active alerts?"],
    ];
    private int _starterSet = Random.Shared.Next(StarterSets.Length);
    public IReadOnlyList<string> StarterQuestions => Array.AsReadOnly(StarterSets[_starterSet]);

    private void RefreshStarters()
    {
        _starterSet = (_starterSet + 1) % StarterSets.Length;
        OnPropertyChanged(nameof(StarterQuestions));
    }

    /// <summary>A stale suggestion must never replace a draft or submit into another chat.</summary>
    public Task<bool> AskStarterAsync(string question, long revision) =>
        revision == ChatRevision && Turns.Count == 0 && string.IsNullOrWhiteSpace(Draft)
        && StarterQuestions.Contains(question) && CanAsk
            ? AskAsync(question) : Task.FromResult(false);

    private sealed class Run(IntelligenceTurn turn, SessionScope scope, long generation)
    {
        public IntelligenceTurn Turn { get; } = turn;

        /// <summary>
        /// The connection the run was authorised for, by session generation: a certificate
        /// trusted or a timeout changed mid-answer keeps it, a sign-in or sign-out ends it.
        /// </summary>
        public SessionScope Scope { get; } = scope;
        public long Generation { get; } = generation;
        public CancellationTokenSource Stop { get; } = new();
        public int Ending = (int)IntelligenceTurnState.Stopped;
        public int Attempt;
        public int Lookups;
        public int LookupFailures;
        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public IntelligenceConversationViewModel(IIntelligenceModel model, IntelligencePreferences preferences,
        IIntelligenceLookups lookups, ISessionService session, Action<Action>? dispatch = null)
    {
        _model = model;
        _preferences = preferences;
        _lookups = lookups;
        _session = session;
        _dispatch = dispatch;
        preferences.Changed += PreferencesChanged;
        session.StateChanged += ConnectionChanged;
    }

    private void PreferencesChanged(object? sender, EventArgs e)
    {
        if (!_preferences.Enabled) Interrupt();
        Notify();
    }

    private void ConnectionChanged(object? sender, EventArgs e)
    {
        // A -> B -> A must not revive a run authorised for the earlier connection.
        var generation = Interlocked.Increment(ref _networkGeneration);
        Interrupt();
        OnUi(() =>
        {
            if (generation != Volatile.Read(ref _networkGeneration)) return;
            // Retention across page departure is useful, but another authenticated
            // connection must never inherit the previous connection's network data.
            // The revision moves first: a saved chat bound to the old one (#229)
            // must never see the cleared transcript as an edit to save.
            ChatRevision++;
            RefreshStarters();
            Turns.Clear();
            Draft = "";
            _selectedDetail = null;
            OnPropertyChanged(nameof(SelectedDetail));
            _chatGeneration = generation;
            OnPropertyChanged(nameof(ChatRevision));
            Notice = _session.Connection is null ? "Signed out. The previous chat was cleared."
                : "The connection changed. The previous chat was cleared.";
        });
        Notify();
    }

    private void OnUi(Action action)
    {
        if (_dispatch is not null) _dispatch(action);
        else if (_ui is null || SynchronizationContext.Current == _ui) action();
        else _ui.Post(_ => action(), null);
    }

    private Task OnUiAsync(Action action)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        OnUi(() =>
        {
            try { action(); done.SetResult(); }
            catch (Exception ex) { done.SetException(ex); }
        });
        return done.Task;
    }

    private void Notify() => OnUi(() =>
    {
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(CanAsk));
        OnPropertyChanged(nameof(IsTranscriptCurrent));
    });

    private void Post(Run run, int attempt, Action action) => OnUi(() =>
    {
        if (ReferenceEquals(Volatile.Read(ref _active), run) && Volatile.Read(ref run.Attempt) == attempt && !run.Stop.IsCancellationRequested)
            action();
    });

    private void RequireCurrent(Run run)
    {
        if (!_preferences.Enabled || run.Generation != Volatile.Read(ref _networkGeneration)
            || !run.Scope.IsCurrent(_session))
        {
            run.Ending = (int)IntelligenceTurnState.Interrupted;
            run.Stop.Cancel();
        }
        run.Stop.Token.ThrowIfCancellationRequested();
        if (_model.Availability != IntelligenceAvailability.Available)
            throw new IntelligenceException("Model unavailable", kind: IntelligenceFailureKind.Unavailable);
    }

    /// <summary>Refuses overlap rather than queuing a question behind an answer.</summary>
    public async Task<bool> AskAsync(string question, string? detail = null)
    {
        question = question.Trim();
        if (IsRunning) { Notice = "An answer is already running."; return false; }
        if (!CanAsk || question.Length == 0)
        {
            Notice = !_preferences.Enabled ? "Turn on Intelligence in Settings to ask a question."
                : _model.Availability != IntelligenceAvailability.Available ? IntelligenceAvailabilityText.Describe(_model.Availability)
                : _session.Connection is null ? "Sign in to LibreNMS to ask about the network."
                : !IsTranscriptCurrent ? "The connection changed. Wait for the previous chat to clear." : "Enter a question.";
            return false;
        }
        if (SessionScope.Capture(_session) is not { } scope) return false;
        var generation = Volatile.Read(ref _networkGeneration);
        if (_chatGeneration != generation) { Notice = "The connection changed. Wait for the previous chat to clear."; return false; }
        var chosenDetail = NormaliseDetail(detail ?? SelectedDetail);
        var turn = new IntelligenceTurn(question, chosenDetail);
        // Earlier answers in this chat (the same connection) may mention objects
        // their lookups returned; the history the model sees can repeat those markers.
        // A key an earlier turn withdrew stays withdrawn: its conflicting objects come too.
        turn.AddEvidence(Turns.SelectMany(t => t.Carried));
        var run = new Run(turn, scope, generation);
        if (Interlocked.CompareExchange(ref _active, run, null) is not null) { run.Stop.Dispose(); return false; }
        Turns.Add(turn);
        Notice = "";
        Notify();
        try
        {
            // The question's share of the context, from the model's own token counts (#637): what's
            // left once the instructions, the answer, two full results and the cheapest tool are counted.
            var context = _model.ContextSize ?? 4096;
            var output = OutputTokens(chosenDetail);
            var instructionTokens = await TokensAsync(IntelligenceInstructions.Build(chosenDetail, null, IntelligenceToolChoice.MinimumLookups, context), run.Stop.Token);
            var resultTokens = await ResultTokensAsync(run.Stop.Token);
            var toolReserve = await CheapestToolAsync(run.Stop.Token);
            var promptTokens = (int)(context * IntelligenceBudget.Usable) - instructionTokens - output
                - IntelligenceToolChoice.MinimumLookups * resultTokens - toolReserve;
            var budget = Characters(Math.Max(0, promptTokens));
            var earlier = Turns.Where(t => !ReferenceEquals(t, turn)).Select(t => t.Question).ToList();
            var prompt = Prompt(question, budget, out var omitted, out var hasHistory);
            turn.HistoryOmitted = omitted;
            // The character budget is an estimate; where the model counts, its own count of the question
            // decides, so a long question that fits by its real cost isn't turned away.
            if (prompt.Length > budget
                && (await _model.CountTokensAsync(prompt, run.Stop.Token) is not { } counted || counted > Math.Max(0, promptTokens)))
            {
                turn.State = IntelligenceTurnState.Failed;
                turn.Message = "This question is too long. Shorten it or narrow its scope.";
                return true;
            }
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                RequireCurrent(run);
                Volatile.Write(ref run.Attempt, attempt);
                // The retry after a context limit is a new session: its lookups start again.
                Volatile.Write(ref run.Lookups, 0);
                var attemptId = attempt;
                using var attemptStop = CancellationTokenSource.CreateLinkedTokenSource(run.Stop.Token);
                var token = attemptStop.Token;
                // Owned by this attempt: however it ends (an answer, the context retry, a
                // failure, Stop or a switch) the set is disposed with it, closing the device
                // search's private transport rather than leaving it to the next switch.
                using var lookupSet = _lookups.Open(evidence => Post(run, attemptId, () => turn.AddEvidence(evidence)));
                // This question's tools and lookup limit, from what was asked and what the context holds (#637).
                var costs = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var lookup in lookupSet.Lookups) costs[lookup.Name] = await ToolTokensAsync(lookup, token);
                // Choose, build the instructions (they name the limit and the tools left out) and count them
                // as built. Two results must fit beside them: if not, earlier turns give way and the choice is
                // made again, as on a context limit; with none left, the question itself is too long.
                IntelligenceChoice choice;
                string instructions;
                int room;
                var baseTokens = instructionTokens;
                while (true)
                {
                    var promptCost = await TokensAsync(prompt, token);
                    choice = IntelligenceToolChoice.Choose(question, earlier, null, lookupSet.Lookups,
                        new IntelligenceBudget(context, baseTokens, promptCost, output, resultTokens, costs));
                    instructions = IntelligenceInstructions.Build(turn.Detail, null, choice.MaxLookups, context, choice.NotOffered);
                    var built = await TokensAsync(instructions, token);
                    room = (int)(context * IntelligenceBudget.Usable) - built - promptCost - output - choice.Tools.Sum(t => costs[t.Name]);
                    var missing = IntelligenceToolChoice.MinimumLookups * resultTokens - room;
                    if (missing <= 0)
                    {
                        // The limit stated must be one the room holds.
                        if (built > baseTokens && room / Math.Max(1, resultTokens) < choice.MaxLookups)
                        {
                            baseTokens = built;
                            continue;
                        }
                        break;
                    }
                    if (!hasHistory) break;
                    prompt = Prompt(question, Math.Max(0, prompt.Length - Characters(missing)), out omitted, out hasHistory);
                    turn.HistoryOmitted = true;
                }
                if (room < IntelligenceToolChoice.MinimumLookups * resultTokens)
                {
                    await OnUiAsync(() =>
                    {
                        turn.State = IntelligenceTurnState.Failed;
                        turn.Message = "This question is too long. Shorten it or narrow its scope.";
                    });
                    break;
                }
                var maxLookups = Math.Min(choice.MaxLookups, Math.Max(IntelligenceToolChoice.MinimumLookups, room / Math.Max(1, resultTokens)));
                if (maxLookups != choice.MaxLookups)
                    instructions = IntelligenceInstructions.Build(turn.Detail, null, maxLookups, context, choice.NotOffered);
                var tools = choice.Tools.Select(lookup => lookup with
                {
                    Run = async (query, callerToken) =>
                    {
                        using var call = CancellationTokenSource.CreateLinkedTokenSource(token, callerToken);
                        RequireCurrent(run);
                        call.Token.ThrowIfCancellationRequested();
                        if (Interlocked.Increment(ref run.Lookups) > maxLookups)
                        {
                            Interlocked.Increment(ref run.LookupFailures);
                            return "The lookup limit was reached. Say if needed data is missing.";
                        }
                        var progress = new IntelligenceLookupProgress(lookup.Name);
                        Post(run, attemptId, () => turn.Lookups.Add(progress));
                        try
                        {
                            var result = await lookup.Run(query, call.Token).WaitAsync(call.Token).ConfigureAwait(false);
                            RequireCurrent(run);
                            var failed = CompactNetworkLookups.IsFailure(result);
                            if (failed) Interlocked.Increment(ref run.LookupFailures);
                            Post(run, attemptId, () => { progress.Result = result; progress.State = failed ? IntelligenceLookupState.Failed : IntelligenceLookupState.Completed; });
                            return result;
                        }
                        catch (OperationCanceledException) when (call.Token.IsCancellationRequested) { throw; }
                        catch
                        {
                            Interlocked.Increment(ref run.LookupFailures);
                            Post(run, attemptId, () => progress.State = IntelligenceLookupState.Failed);
                            return CompactNetworkLookups.Failed;
                        }
                    },
                }).ToArray();
                using var modelSession = _model.StartSession(instructions, tools);
                try
                {
                    var answer = await modelSession.AskAsync(prompt,
                        text => Post(run, attemptId, () => turn.Answer = text), token).WaitAsync(token);
                    RequireCurrent(run); // A Stop racing with a successful completion always wins.
                    // Core formats any cited output here, so the turn shows, copies and
                    // vets exactly the Markdown desktop would; plain answers pass through.
                    var markdown = answer.ToMarkdown();
                    await OnUiAsync(() =>
                    {
                        RequireCurrent(run);
                        turn.Answer = markdown;
                        turn.Images = answer.Images;
                        turn.State = IntelligenceTurnState.Completed;
                        turn.Message = run.LookupFailures == 0 ? "Completed" : "Completed, but some lookups failed.";
                    });
                    break;
                }
                catch (IntelligenceException ex) when (ex.Kind == IntelligenceFailureKind.ContextLimit && hasHistory && attempt == 1)
                {
                    await OnUiAsync(() =>
                    {
                        if (run.Stop.IsCancellationRequested) return;
                        if (ex.PartialAnswer is not null) turn.Answer = ex.PartialAnswer;
                        turn.HistoryOmitted = true;
                    });
                    attemptStop.Cancel();
                    prompt = "Question:\n" + question;
                    hasHistory = false;
                }
                finally { attemptStop.Cancel(); }
            }
        }
        catch (OperationCanceledException)
        {
            await OnUiAsync(() =>
            {
                turn.State = (IntelligenceTurnState)run.Ending;
                turn.Message = turn.State == IntelligenceTurnState.Stopped ? "Stopped. Retry or edit the question to continue." : "Interrupted. Retry when you're ready.";
            });
        }
        catch (IntelligenceException ex)
        {
            await OnUiAsync(() =>
            {
                if (run.Stop.IsCancellationRequested) return;
                if (ex.PartialAnswer is not null) turn.Answer = ex.PartialAnswer;
                turn.State = IntelligenceTurnState.Failed;
                turn.Message = ex.Kind switch
                {
                    IntelligenceFailureKind.Refused => "The model couldn't answer this question. Try rephrasing it.",
                    IntelligenceFailureKind.ContextLimit => "This question exceeded the model's context. Shorten it or narrow its scope.",
                    IntelligenceFailureKind.Unavailable => "The on-device model is unavailable. Try again later.",
                    _ => "The answer failed. Retry or edit the question.",
                };
            });
        }
        catch
        {
            await OnUiAsync(() =>
            {
                turn.State = IntelligenceTurnState.Failed;
                turn.Message = "The answer failed. Retry or edit the question.";
            });
        }
        finally
        {
            await OnUiAsync(() =>
            {
                // Completion and releasing the UI's Stop action happen together.
                // A Stop accepted during final-text notification still wins.
                if (run.Stop.IsCancellationRequested)
                {
                    turn.State = (IntelligenceTurnState)run.Ending;
                    turn.Message = turn.State == IntelligenceTurnState.Stopped ? "Stopped. Retry or edit the question to continue." : "Interrupted. Retry when you're ready.";
                }
                foreach (var progress in turn.Lookups.Where(p => p.State == IntelligenceLookupState.Running))
                    progress.State = IntelligenceLookupState.Stopped;
                turn.AnsweredAt ??= DateTimeOffset.UtcNow;
                Interlocked.CompareExchange(ref _active, null, run);
                Notify();
            });
            run.Stop.Dispose();
            run.Finished.TrySetResult();
        }
        return true;
    }

    /// <summary>
    /// Tokens for <paramref name="text"/> where the model can't count (before iOS 26.4), at two and a
    /// half characters a token: lookup text full of markers, ids and addresses packs about that densely,
    /// so the estimate errs towards offering less rather than overflowing the context.
    /// </summary>
    internal static int Estimate(string text) => (text.Length * 2 + 4) / 5;

    /// <summary>The characters a number of tokens holds at the same rate.</summary>
    internal static int Characters(int tokens) => tokens * 5 / 2;

    /// <summary>Room kept for the answer, by detail level: the lengths the detail levels ask for, with some over.</summary>
    internal static int OutputTokens(string detail) => detail switch
    {
        IntelligenceDetail.Brief => 150,
        IntelligenceDetail.Detailed => 400,
        _ => 300,
    };

    /// <summary>
    /// A full-size lookup reply in the shape lookups write, for measuring what one result costs. The
    /// phone's replies stop at <see cref="CompactNetworkLookups.MaxCharacters"/>.
    /// </summary>
    internal static readonly string SampleResult = string.Concat(Enumerable.Repeat(
        "Critical; active; Device down [[alert:9200]] on core-sw1 [[device:12]] (Down); open 2 days 3 hours; for Inlet\n", 9))[..CompactNetworkLookups.MaxCharacters];

    /// <summary>
    /// Measured costs by model context and text: counting is a native call, and a tool's definition is
    /// the same on every question. Keyed by context so on-device and Private Cloud Compute counts stay apart.
    /// </summary>
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(int, string), int> Measured = new();

    /// <summary>The text a tool definition puts in the context: its name, description and argument schema.</summary>
    internal static string Definition(string name, string description, IReadOnlyList<DashyNMS.Agent.Catalogue.ToolParameter> parameters)
        => name + "\n" + description + "\n" + IntelligenceToolArguments.Schema(parameters);

    /// <summary>The model's own count, or a conservative estimate where it can't count.</summary>
    private async Task<int> TokensAsync(string text, CancellationToken token)
        => await _model.CountTokensAsync(text, token).ConfigureAwait(false) ?? Estimate(text);

    private async Task<int> MeasuredAsync(string text, CancellationToken token)
    {
        var key = (_model.ContextSize ?? 0, text);
        if (Measured.TryGetValue(key, out var known)) return known;
        var counted = await _model.CountTokensAsync(text, token).ConfigureAwait(false);
        if (counted is not { } tokens) return Estimate(text);
        Measured[key] = tokens;
        return tokens;
    }

    private Task<int> ToolTokensAsync(IntelligenceLookup lookup, CancellationToken token) => MeasuredAsync(Definition(lookup.Name, lookup.Purpose, lookup.Parameters), token);

    private Task<int> ResultTokensAsync(CancellationToken token) => MeasuredAsync(SampleResult, token);

    /// <summary>The cheapest tool the phone has, which every question offers at least.</summary>
    private async Task<int> CheapestToolAsync(CancellationToken token)
    {
        var cheapest = int.MaxValue;
        foreach (var name in CompactNetworkLookups.ReviewedTools)
        {
            var (tool, parameters) = CompactNetworkLookups.Definition(name);
            cheapest = Math.Min(cheapest, await MeasuredAsync(Definition(tool.Name, tool.Description, parameters), token));
        }
        return cheapest;
    }

    private string Prompt(string question, int budget, out bool omitted, out bool hasHistory)
    {
        var prompt = "Question:\n" + question;
        var completed = Turns.Where(t => t.State == IntelligenceTurnState.Completed).ToArray();
        var included = 0;
        foreach (var turn in completed.Reverse())
        {
            var candidate = "Earlier question: " + turn.Question + "\nEarlier answer: " + turn.Answer + "\n\n" + prompt;
            if (candidate.Length > budget) break;
            prompt = candidate;
            included++;
        }
        omitted = included < completed.Length;
        hasHistory = included > 0;
        return prompt;
    }

    public Task<bool> RetryAsync(IntelligenceTurn turn) => OwnsTurn(turn) ? AskAsync(turn.Question, turn.Detail) : Task.FromResult(false);
    public string EditQuestion(IntelligenceTurn turn) => OwnsTurn(turn) ? turn.Question : "";

    /// <summary>Any owned, settled question can be edited, without replacing an existing draft.</summary>
    public bool CanEditQuestion(IntelligenceTurn turn) =>
        CanAsk && OwnsTurn(turn) && turn.State != IntelligenceTurnState.Running
        && !string.IsNullOrWhiteSpace(turn.Question) && string.IsNullOrWhiteSpace(Draft);

    /// <summary>Check and install the draft together, including after a retained page returns.</summary>
    public bool TryEditQuestion(IntelligenceTurn turn)
    {
        if (!CanEditQuestion(turn)) return false;
        Draft = turn.Question;
        return true;
    }
    public bool NewChat() => Replace([], "", null);

    /// <summary>
    /// Shows a saved chat (#229) in place of the current transcript, as a new
    /// revision. Refused while an answer runs or a connection change is clearing
    /// the transcript, so a reopened chat can't land under another connection.
    /// </summary>
    public bool Restore(IReadOnlyList<IntelligenceTurn> turns, string draft, string? detail) =>
        Replace(turns, draft, detail is null ? null : NormaliseDetail(detail));

    private bool Replace(IReadOnlyList<IntelligenceTurn> turns, string draft, string? detail)
    {
        if (IsRunning || !IsTranscriptCurrent) return false;
        // The revision moves before the turns change, so anything bound to the
        // previous transcript sees a different chat, never an emptied one.
        ChatRevision++;
        RefreshStarters();
        Turns.Clear();
        foreach (var turn in turns) Turns.Add(turn);
        Notice = "";
        Draft = draft;
        _selectedDetail = detail;
        OnPropertyChanged(nameof(SelectedDetail));
        OnPropertyChanged(nameof(ChatRevision));
        Notify();
        return true;
    }

    /// <summary>Full is desktop's; the phone's model answers at most Detailed.</summary>
    private static string NormaliseDetail(string? detail) => IntelligenceInstructions.Detail(detail);

    /// <summary>The host can request a short background task and await interruption before suspension.</summary>
    public Task InterruptAsync()
    {
        var run = Volatile.Read(ref _active);
        Interrupt();
        return run?.Finished.Task ?? Task.CompletedTask;
    }

    public bool Stop() => Cancel(IntelligenceTurnState.Stopped);
    public bool Interrupt() => Cancel(IntelligenceTurnState.Interrupted);
    private bool Cancel(IntelligenceTurnState ending)
    {
        var run = Volatile.Read(ref _active);
        if (run is null) return false;
        run.Ending = (int)ending;
        try { run.Stop.Cancel(); } catch (ObjectDisposedException) { return false; }
        return true;
    }

    public void Dispose()
    {
        _disposed = true;
        Interrupt();
        _preferences.Changed -= PreferencesChanged;
        _session.StateChanged -= ConnectionChanged;
    }
}
