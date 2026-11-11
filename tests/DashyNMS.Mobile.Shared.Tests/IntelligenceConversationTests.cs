using System.Collections.Concurrent;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Tests;

public sealed class IntelligenceConversationTests
{
    private delegate Task<IntelligenceAnswer> Script(string prompt, IReadOnlyList<IntelligenceLookup> tools, Action<string> text, CancellationToken token);
    private sealed class Model : IIntelligenceModel
    {
        public IntelligenceAvailability Availability { get; set; } = IntelligenceAvailability.Available;
        public int? ContextSize { get; set; } = 4096;
        public Queue<Script> Scripts { get; } = new();
        public List<string> Prompts { get; } = [];
        public List<string> Instructions { get; } = [];
        public int Disposed;
        public Task<int?> CountTokensAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);
        public IIntelligenceSession StartSession(string instructions, IReadOnlyList<IntelligenceLookup> lookups)
        {
            Instructions.Add(instructions);
            var script = Scripts.Count > 0 ? Scripts.Dequeue() : (_, _, _, _) => Task.FromResult(new IntelligenceAnswer("Answer", null));
            return new Session(this, lookups, script);
        }
        private sealed class Session(Model owner, IReadOnlyList<IntelligenceLookup> tools, Script script) : IIntelligenceSession
        {
            public Task<IntelligenceAnswer> AskAsync(string question, Action<string> onText, CancellationToken cancellationToken = default)
            {
                owner.Prompts.Add(question);
                return script(question, tools, onText, cancellationToken);
            }
            public void Dispose() => owner.Disposed++;
        }
    }
    private sealed class Lookups : IIntelligenceLookups
    {
        public int Calls;
        public bool Fail;
        public IReadOnlyList<IntelligenceLookup> Create() => [new("network_overview", "Overview", (_, _) =>
        {
            Calls++;
            return Fail ? Task.FromException<string>(new IOException("private URL and token")) : Task.FromResult("Devices: 10 total.");
        })];
    }
    private readonly Model _model = new();
    private readonly Lookups _lookups = new();
    private readonly ISessionService _session = Substitute.For<ISessionService>();
    private readonly IntelligencePreferences _preferences;

    public IntelligenceConversationTests()
    {
        var store = Substitute.For<ICheckedSettingsStore>();
        store.Current.Returns(new AppSettings());
        store.TrySave().Returns(true);
        var preferences = new InMemoryPreferences();
        preferences.Set(IntelligencePreferences.NoticeKey, "accepted");
        _preferences = new IntelligencePreferences(store, preferences);
        _session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.net/"), "test-token"));
    }
    private IntelligenceConversationViewModel Open(Action<Action>? dispatch = null) => new(_model, _preferences, _lookups, _session, dispatch);

    [Fact]
    public async Task Streaming_and_lookup_evidence_finish_on_the_answer()
    {
        _model.Scripts.Enqueue(async (_, tools, text, token) =>
        {
            Assert.Equal("Devices: 10 total.", await tools[0].Run("", token));
            text("Ten");
            text("Ten devices.");
            return new IntelligenceAnswer("Ten devices.", null);
        });
        using var vm = Open();
        Assert.True(await vm.AskAsync("How is the network?"));
        var turn = Assert.Single(vm.Turns);
        Assert.Equal("Ten devices.", turn.Answer);
        Assert.Equal(IntelligenceTurnState.Completed, turn.State);
        Assert.Equal(IntelligenceLookupState.Completed, Assert.Single(turn.Lookups).State);
        Assert.Equal(1, _model.Disposed);
        Assert.False(vm.IsRunning);
    }

    [Fact]
    public async Task Stop_is_prompt_even_if_a_model_ignores_cancellation_and_late_text_is_ignored()
    {
        var held = new TaskCompletionSource<IntelligenceAnswer>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action<string>? late = null;
        _model.Scripts.Enqueue((_, _, text, _) => { late = text; text("Partial"); return held.Task; });
        using var vm = Open();
        var asking = vm.AskAsync("Question");
        Assert.False(await vm.AskAsync("Overlapping question"));
        Assert.Single(vm.Turns);
        Assert.False(vm.NewChat());
        Assert.True(vm.Stop());
        await asking.WaitAsync(TimeSpan.FromSeconds(5));
        var stopped = vm.Turns[0];
        Assert.Equal(IntelligenceTurnState.Stopped, stopped.State);
        Assert.Equal("Partial", stopped.Answer);
        Assert.True(await vm.RetryAsync(stopped));
        late!("Stale text");
        held.SetResult(new IntelligenceAnswer("Stale answer", null));
        Assert.Equal("Partial", stopped.Answer);
        Assert.Equal("Answer", vm.Turns[1].Answer);
        Assert.Equal("Question", vm.EditQuestion(stopped));
    }

    [Fact]
    public async Task A_stop_racing_with_a_success_is_never_completed()
    {
        using var vm = Open();
        _model.Scripts.Enqueue((_, _, text, _) =>
        {
            text("Partial");
            vm.Stop();
            return Task.FromResult(new IntelligenceAnswer("Final", null));
        });
        await vm.AskAsync("Question");
        Assert.Equal(IntelligenceTurnState.Stopped, vm.Turns[0].State);
        Assert.Equal("Partial", vm.Turns[0].Answer);
    }

    [Fact]
    public async Task Stop_accepted_during_final_text_notification_still_wins()
    {
        using var vm = Open();
        vm.Turns.CollectionChanged += (_, e) =>
        {
            var turn = (IntelligenceTurn)e.NewItems![0]!;
            turn.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(turn.Answer) && turn.Answer == "Answer")
                    Assert.True(vm.Stop());
            };
        };
        await vm.AskAsync("Question");
        Assert.Equal(IntelligenceTurnState.Stopped, vm.Turns[0].State);
        Assert.False(vm.IsRunning);
    }

    [Theory]
    [InlineData(IntelligenceFailureKind.Failed)]
    [InlineData(IntelligenceFailureKind.ContextLimit)]
    public async Task Stop_before_a_queued_failure_update_preserves_the_displayed_partial(IntelligenceFailureKind kind)
    {
        IntelligenceConversationViewModel? vm = null;
        var stopBeforeUpdate = false;
        vm = Open(action =>
        {
            if (stopBeforeUpdate)
            {
                stopBeforeUpdate = false;
                Assert.True(vm!.Stop());
            }
            action();
        });
        using (vm)
        {
            await vm.AskAsync("Seed question");
            _model.Scripts.Enqueue((_, _, text, _) =>
            {
                text("Displayed partial");
                stopBeforeUpdate = true;
                return Task.FromException<IntelligenceAnswer>(new IntelligenceException("private", "Late failure partial", kind));
            });
            await vm.AskAsync("Current question");
            Assert.Equal("Displayed partial", vm.Turns[1].Answer);
            Assert.Equal(IntelligenceTurnState.Stopped, vm.Turns[1].State);
            Assert.Equal(2, _model.Prompts.Count);
        }
    }

    [Theory]
    [InlineData(IntelligenceFailureKind.Failed, "failed")]
    [InlineData(IntelligenceFailureKind.Refused, "rephrasing")]
    [InlineData(IntelligenceFailureKind.ContextLimit, "context")]
    [InlineData(IntelligenceFailureKind.Unavailable, "unavailable")]
    public async Task Failures_keep_partial_text_and_use_fixed_messages(IntelligenceFailureKind kind, string message)
    {
        _model.Scripts.Enqueue((_, _, _, _) => Task.FromException<IntelligenceAnswer>(new IntelligenceException("private error token", "Partial", kind)));
        using var vm = Open();
        await vm.AskAsync("Question");
        Assert.Equal(IntelligenceTurnState.Failed, vm.Turns[0].State);
        Assert.Equal("Partial", vm.Turns[0].Answer);
        Assert.Contains(message, vm.Turns[0].Message);
        Assert.DoesNotContain("private", vm.Turns[0].Message);
    }

    [Theory]
    [InlineData(4096, true)]
    [InlineData(8192, false)]
    public async Task History_uses_runtime_budget_while_old_turns_stay_visible(int contextSize, bool omitted)
    {
        _model.ContextSize = contextSize;
        _model.Scripts.Enqueue((_, _, _, _) => Task.FromResult(new IntelligenceAnswer(new string('x', 7000), null)));
        using var vm = Open();
        await vm.AskAsync("Seed question");
        await vm.AskAsync("Next question");
        Assert.Equal(2, vm.Turns.Count);
        Assert.Equal(7000, vm.Turns[0].Answer.Length);
        Assert.Equal(omitted, vm.Turns[1].HistoryOmitted);
        Assert.Equal(!omitted, _model.Prompts[1].Contains("Seed question", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Context_overflow_retries_once_without_history_in_a_fresh_session()
    {
        using var vm = Open();
        await vm.AskAsync("Seed question");
        _model.Scripts.Enqueue((_, _, _, _) => Task.FromException<IntelligenceAnswer>(new IntelligenceException("private", kind: IntelligenceFailureKind.ContextLimit)));
        await vm.AskAsync("Current question");
        Assert.Equal(3, _model.Prompts.Count);
        Assert.Contains("Seed question", _model.Prompts[1]);
        Assert.Equal("Question:\nCurrent question", _model.Prompts[2]);
        Assert.True(vm.Turns[1].HistoryOmitted);
        Assert.Equal(IntelligenceTurnState.Completed, vm.Turns[1].State);
    }

    [Fact]
    public async Task A_second_context_failure_ends_the_attempt_instead_of_retrying_indefinitely()
    {
        using var vm = Open();
        await vm.AskAsync("Seed question");
        for (var i = 0; i < 2; i++)
            _model.Scripts.Enqueue((_, _, _, _) => Task.FromException<IntelligenceAnswer>(new IntelligenceException("private", "Partial", IntelligenceFailureKind.ContextLimit)));
        await vm.AskAsync("Current question");
        Assert.Equal(3, _model.Prompts.Count);
        Assert.Equal(IntelligenceTurnState.Failed, vm.Turns[1].State);
        Assert.Equal("Partial", vm.Turns[1].Answer);
        Assert.Contains("Shorten", vm.Turns[1].Message);
    }

    [Theory]
    [InlineData(false, IntelligenceAvailability.Available, true)]
    [InlineData(true, IntelligenceAvailability.ModelNotReady, true)]
    [InlineData(true, IntelligenceAvailability.Available, false)]
    public async Task Disabled_unavailable_and_signed_out_states_do_not_start_a_model(bool enabled, IntelligenceAvailability availability, bool signedIn)
    {
        _preferences.SetEnabled(enabled);
        _model.Availability = availability;
        if (!signedIn) _session.Connection.Returns((LibreNmsConnection?)null);
        using var vm = Open();
        Assert.False(vm.CanAsk);
        Assert.False(await vm.AskAsync("Question"));
        Assert.Empty(vm.Turns);
        Assert.Empty(_model.Prompts);
        Assert.Equal(0, _lookups.Calls);
        Assert.NotEmpty(vm.Notice);
    }

    [Fact]
    public async Task A_turn_keeps_its_detail_for_retry_without_changing_the_default()
    {
        using var vm = Open();
        await vm.AskAsync("Question", "brief");
        Assert.Equal("balanced", _preferences.Detail);
        Assert.Contains("150 tokens", _model.Instructions[^1]);
        _preferences.SetDetail("detailed");
        await vm.RetryAsync(vm.Turns[0]);
        Assert.Contains("150 tokens", _model.Instructions[^1]);
        await vm.AskAsync("Question with default detail");
        Assert.Contains("400 tokens", _model.Instructions[^1]);
    }

    [Fact]
    public async Task An_oversized_question_does_not_start_a_model()
    {
        using var vm = Open();
        await vm.AskAsync(new string('x', 20000));
        Assert.Empty(_model.Prompts);
        Assert.Equal(IntelligenceTurnState.Failed, vm.Turns[0].State);
        Assert.Contains("Shorten", vm.Turns[0].Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_lookups_and_lookup_limits_are_honest(bool failing)
    {
        _lookups.Fail = failing;
        _model.Scripts.Enqueue(async (_, tools, _, token) =>
        {
            for (var i = 0; i < 3; i++)
            {
                var result = await tools[0].Run("", token);
                Assert.DoesNotContain("private", result);
            }
            return new IntelligenceAnswer("Some data is missing.", null);
        });
        using var vm = Open();
        await vm.AskAsync("Question");
        Assert.Equal(2, _lookups.Calls);
        Assert.Equal(2, vm.Turns[0].Lookups.Count);
        Assert.Equal(IntelligenceTurnState.Completed, vm.Turns[0].State);
        Assert.Contains("lookups failed", vm.Turns[0].Message);
    }

    [Fact]
    public async Task A_connection_switch_permanently_interrupts_a_run_and_requires_a_new_chat()
    {
        var held = new TaskCompletionSource<IntelligenceAnswer>();
        _model.Scripts.Enqueue((_, _, text, _) => { text("Partial"); return held.Task; });
        using var vm = Open();
        var asking = vm.AskAsync("Old network question");
        var old = _session.Connection;
        _session.Connection.Returns(new LibreNmsConnection(new Uri("https://new.example.net/"), "new-token"));
        _session.StateChanged += Raise.EventWith(EventArgs.Empty);
        _session.Connection.Returns(old); // Even an old-reference restoration cannot revive it.
        await asking.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(IntelligenceTurnState.Interrupted, vm.Turns[0].State);
        Assert.Equal("Partial", vm.Turns[0].Answer);
        Assert.False(await vm.AskAsync("Follow-up"));
        Assert.True(vm.NewChat());
        Assert.True(await vm.AskAsync("New network question"));
        Assert.DoesNotContain("Old network question", _model.Prompts[^1]);
        held.SetResult(new IntelligenceAnswer("Late", null));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Opting_out_or_background_interruption_retains_the_partial_answer(bool optingOut)
    {
        var held = new TaskCompletionSource<IntelligenceAnswer>();
        _model.Scripts.Enqueue((_, _, text, _) => { text("Partial"); return held.Task; });
        using var vm = Open();
        var asking = vm.AskAsync("Question");
        if (optingOut) _preferences.SetEnabled(false);
        await vm.InterruptAsync().WaitAsync(TimeSpan.FromSeconds(5));
        await asking;
        Assert.Equal(IntelligenceTurnState.Interrupted, vm.Turns[0].State);
        Assert.Equal("Partial", vm.Turns[0].Answer);
        Assert.Equal(!optingOut, vm.CanAsk);
        held.SetResult(new IntelligenceAnswer("Late", null));
    }

    [Fact]
    public async Task Native_callbacks_and_terminal_updates_use_the_supplied_UI_dispatcher()
    {
        using var queue = new BlockingCollection<Action>();
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => { foreach (var action in queue.GetConsumingEnumerable()) action(); });
        thread.IsBackground = true;
        thread.Start();
        queue.Add(async () =>
        {
            try
            {
                var owner = Environment.CurrentManagedThreadId;
                using var vm = Open(action => queue.Add(action));
                var changedOn = new List<int>();
                vm.Turns.CollectionChanged += (_, e) =>
                    ((IntelligenceTurn)e.NewItems![0]!).PropertyChanged += (_, _) => changedOn.Add(Environment.CurrentManagedThreadId);
                _model.Scripts.Enqueue(async (_, _, text, _) =>
                {
                    await Task.Run(() => text("Streamed"));
                    return new IntelligenceAnswer("Finished", null);
                });
                await vm.AskAsync("Question");
                Assert.NotEmpty(changedOn);
                Assert.All(changedOn, id => Assert.Equal(owner, id));
                finished.SetResult();
            }
            catch (Exception ex) { finished.SetException(ex); }
        });
        try { await finished.Task.WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { queue.CompleteAdding(); thread.Join(TimeSpan.FromSeconds(2)); }
    }

    [Theory]
    [InlineData("contextSizeExceeded(details)", IntelligenceFailureKind.ContextLimit)]
    [InlineData("guardrailViolation", IntelligenceFailureKind.Refused)]
    [InlineData("unsupported", IntelligenceFailureKind.Unavailable)]
    [InlineData("unknown future SDK error", IntelligenceFailureKind.Failed)]
    public void Native_reason_mapping_never_requires_displaying_the_error(string reason, IntelligenceFailureKind kind) =>
        Assert.Equal(kind, IntelligenceFailures.FromNativeReason(reason));
}
