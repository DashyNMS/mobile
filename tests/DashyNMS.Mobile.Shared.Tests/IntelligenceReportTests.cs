using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Intelligence;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// Report this answer (#574): the email carries only the chosen question, answer
/// and reason, the person sees and can redact it first, nothing is sent
/// automatically, and a chat that changes meanwhile can't be reported.
/// All names and addresses are synthetic.
/// </summary>
public sealed class IntelligenceReportTests
{
    private const string LookupText = "LOOKUP-ONLY edge-router-a [[device:12]]: Down, 192.0.2.10";
    private static readonly DateTimeOffset Observed = new(2026, 11, 11, 9, 30, 0, TimeSpan.Zero);

    private readonly ISessionService _session = Fakes.Session();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IEmailComposer _email = Substitute.For<IEmailComposer>();
    private readonly Model _model = new();
    private readonly IntelligenceConversationViewModel _conversation;
    private readonly IntelligenceAnswerReporter _reporter;

    public IntelligenceReportTests()
    {
        var store = Substitute.For<ICheckedSettingsStore>();
        store.Current.Returns(new AppSettings());
        store.TrySave().Returns(true);
        var preferences = new InMemoryPreferences();
        preferences.Set(IntelligencePreferences.NoticeKey, "accepted");
        _session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.net/"), "test-token"));
        _email.IsAvailable.Returns(true);
        _conversation = new IntelligenceConversationViewModel(_model, new IntelligencePreferences(store, preferences),
            new Lookups(), _session, action => action());
        _reporter = new IntelligenceAnswerReporter(_conversation, _dialogs, _email);
    }

    private void Choose(string? reason) =>
        _dialogs.ChooseAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns(reason);

    private async Task<IntelligenceTurn> AnswerAsync(string question, string answer)
    {
        _model.Next = answer;
        Assert.True(await _conversation.AskAsync(question));
        return _conversation.Turns[^1];
    }

    private void ChangeConnection()
    {
        _session.Connection.Returns(new LibreNmsConnection(new Uri("https://other.example.net/"), "other-token"));
        _session.StateChanged += Raise.EventWith(EventArgs.Empty);
    }

    [Fact]
    public async Task The_email_holds_only_the_reason_and_the_chosen_question_and_answer()
    {
        await AnswerAsync("Earlier question about core-sw1", "Earlier answer naming core-sw1.");
        var turn = await AnswerAsync("Is [[device:12]] down?", "**[[device:12]]** is down.\n\n| Device | State |\n|---|---|\n| [[device:12]] | Down |");
        Choose("Wrong or misleading");

        var report = await _reporter.ChooseReasonAsync(turn);

        Assert.NotNull(report);
        Assert.Equal(
            "Reason: Wrong or misleading\n\nQuestion:\nIs [[device:12]] down?\n\nAnswer:\n"
            + "**[[device:12]]** is down.\n\n| Device | State |\n|---|---|\n| [[device:12]] | Down |\n",
            report.Body);
        Assert.Equal(AppLinks.SupportEmail, report.To);
        Assert.Equal("DashyNMS Intelligence answer report: Wrong or misleading", report.Subject);
        // Lookup evidence, the other turn and the connection never reach the draft.
        Assert.NotEmpty(turn.Evidence);
        foreach (var excluded in new[] { "LOOKUP-ONLY", "192.0.2.10", "Earlier", "core-sw1", "nms.example.net", "test-token" })
            Assert.DoesNotContain(excluded, report.ReportText);
    }

    [Fact]
    public async Task Nothing_is_sent_until_the_person_opens_the_previewed_draft()
    {
        var turn = await AnswerAsync("How is the network?", "All good.");
        Choose("Offensive");

        var report = await _reporter.ChooseReasonAsync(turn);
        await _email.DidNotReceiveWithAnyArgs().ComposeAsync(default!);

        Assert.Equal(IntelligenceReportOutcome.Opened, await report!.ComposeAsync());
        await _email.Received(1).ComposeAsync(new EmailDraft("dashynms@pckp.net",
            "DashyNMS Intelligence answer report: Offensive",
            "Reason: Offensive\n\nQuestion:\nHow is the network?\n\nAnswer:\nAll good.\n"));
        Assert.True(report.IsOpened);
        Assert.Contains("cannot tell whether you sent or cancelled", report.Status);
    }

    [Fact]
    public async Task Redacted_text_is_what_the_draft_carries_and_the_chat_keeps_the_original()
    {
        var turn = await AnswerAsync("Why is edge-router-a down?", "edge-router-a at 192.0.2.10 lost power.");
        Choose("Other");
        var report = (await _reporter.ChooseReasonAsync(turn))!;

        report.Question = "Why is [device] down?";
        report.Answer = "[device] at [address] lost power.";
        await report.ComposeAsync();

        await _email.Received(1).ComposeAsync(Arg.Is<EmailDraft>(d =>
            d.Body == "Reason: Other\n\nQuestion:\nWhy is [device] down?\n\nAnswer:\n[device] at [address] lost power.\n"));
        Assert.Equal("edge-router-a at 192.0.2.10 lost power.", turn.Answer);
    }

    [Fact]
    public async Task Cancelling_the_reason_opens_nothing()
    {
        var turn = await AnswerAsync("Question", "Answer");
        Choose(null);
        Assert.Null(await _reporter.ChooseReasonAsync(turn));
        Choose("Not a reason");
        Assert.Null(await _reporter.ChooseReasonAsync(turn));
    }

    [Fact]
    public async Task A_connection_change_while_choosing_a_reason_discards_the_report()
    {
        var turn = await AnswerAsync("Question", "Answer");
        _dialogs.ChooseAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns(_ =>
        {
            ChangeConnection();
            return Task.FromResult<string?>("Offensive");
        });

        Assert.Null(await _reporter.ChooseReasonAsync(turn));
        await _dialogs.Received(1).AlertAsync("This answer can't be reported", Arg.Is<string>(m => m.Contains("Nothing was sent")));
        await _email.DidNotReceiveWithAnyArgs().ComposeAsync(default!);
    }

    [Fact]
    public async Task A_new_chat_while_choosing_a_reason_discards_the_report()
    {
        var turn = await AnswerAsync("Question", "Answer");
        _dialogs.ChooseAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>()).Returns(_ =>
        {
            Assert.True(_conversation.NewChat());
            return Task.FromResult<string?>("Offensive");
        });

        Assert.Null(await _reporter.ChooseReasonAsync(turn));
    }

    [Fact]
    public async Task A_connection_change_while_previewing_clears_the_preview_and_sends_nothing()
    {
        var turn = await AnswerAsync("Question about edge-router-a", "edge-router-a is down.");
        Choose("Offensive");
        var report = (await _reporter.ChooseReasonAsync(turn))!;
        report.Attach();

        ChangeConnection();

        Assert.True(report.IsExpired);
        Assert.Equal("", report.Question);
        Assert.Equal("", report.Answer);
        Assert.DoesNotContain("edge-router-a", report.ReportText);
        Assert.Equal(IntelligenceReportViewModel.ExpiredMessage, report.Status);
        Assert.False(report.CanCompose);
        report.Answer = "typed after expiry";
        Assert.Equal("", report.Answer);
        Assert.Equal(IntelligenceReportOutcome.Expired, await report.ComposeAsync());
        Assert.Null(report.CopyText());
        await _email.DidNotReceiveWithAnyArgs().ComposeAsync(default!);
        report.Detach();
    }

    [Fact]
    public async Task A_new_chat_while_previewing_expires_it_before_opening_email()
    {
        var turn = await AnswerAsync("Question", "Answer");
        Choose("Other");
        var report = (await _reporter.ChooseReasonAsync(turn))!;
        report.Attach();

        Assert.True(_conversation.NewChat());

        Assert.True(report.IsExpired);
        Assert.Equal(IntelligenceReportOutcome.Expired, await report.ComposeAsync());
        await _email.DidNotReceiveWithAnyArgs().ComposeAsync(default!);
    }

    [Fact]
    public async Task An_unwatched_preview_still_rechecks_ownership_before_opening_email()
    {
        var turn = await AnswerAsync("Question", "Answer");
        Choose("Other");
        var report = (await _reporter.ChooseReasonAsync(turn))!;

        ChangeConnection();

        Assert.Equal(IntelligenceReportOutcome.Expired, await report.ComposeAsync());
        Assert.Equal("", report.Answer);
        await _email.DidNotReceiveWithAnyArgs().ComposeAsync(default!);
    }

    [Fact]
    public async Task Turns_from_another_chat_running_answers_and_empty_answers_cannot_be_reported()
    {
        var other = new IntelligenceTurn("Someone else's question", "balanced");
        Assert.False(_reporter.CanReport(other));
        Assert.Null(await _reporter.ChooseReasonAsync(other));

        _model.Hold = new TaskCompletionSource<IntelligenceAnswer>(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = _conversation.AskAsync("Still answering?");
        var turn = Assert.Single(_conversation.Turns);
        Assert.Equal(IntelligenceTurnState.Running, turn.State);
        Assert.False(_reporter.CanReport(turn));
        _model.Hold.SetResult(new IntelligenceAnswer("", null));
        await running;
        Assert.False(_reporter.CanReport(turn));
        await _dialogs.DidNotReceiveWithAnyArgs().ChooseAsync(default!, default(IReadOnlyList<string>)!);
    }

    [Fact]
    public async Task A_stopped_partial_answer_can_be_reported_as_shown()
    {
        _model.Hold = new TaskCompletionSource<IntelligenceAnswer>(TaskCreationOptions.RunContinuationsAsynchronously);
        _model.Partial = "Partial text";
        var running = _conversation.AskAsync("Long question");
        Assert.True(_conversation.Stop());
        await running;
        var turn = Assert.Single(_conversation.Turns);
        Assert.Equal(IntelligenceTurnState.Stopped, turn.State);
        Choose("Wrong or misleading");

        var report = await _reporter.ChooseReasonAsync(turn);

        Assert.Equal("Reason: Wrong or misleading\n\nQuestion:\nLong question\n\nAnswer:\nPartial text\n", report!.Body);
    }

    [Fact]
    public async Task Without_an_email_app_the_report_says_so_and_offers_its_exact_text()
    {
        var turn = await AnswerAsync("Question", "Answer");
        Choose("Other");
        var report = (await _reporter.ChooseReasonAsync(turn))!;
        _email.IsAvailable.Returns(false);

        Assert.Equal(IntelligenceReportOutcome.Unavailable, await report.ComposeAsync());

        Assert.Contains("No email app", report.Status);
        Assert.Contains(AppLinks.SupportEmail, report.Status);
        Assert.True(report.CanCopy);
        Assert.Equal("To: dashynms@pckp.net\nSubject: DashyNMS Intelligence answer report: Other\n\n"
            + "Reason: Other\n\nQuestion:\nQuestion\n\nAnswer:\nAnswer\n", report.CopyText());
        await _email.DidNotReceiveWithAnyArgs().ComposeAsync(default!);
    }

    [Fact]
    public async Task A_composer_failure_is_reported_truthfully()
    {
        var turn = await AnswerAsync("Question", "Answer");
        Choose("Other");
        var report = (await _reporter.ChooseReasonAsync(turn))!;
        _email.ComposeAsync(Arg.Any<EmailDraft>()).Returns(Task.FromException(new InvalidOperationException("native detail")));

        Assert.Equal(IntelligenceReportOutcome.Failed, await report.ComposeAsync());

        Assert.False(report.IsOpened);
        Assert.True(report.CanCopy);
        Assert.Contains("couldn't open the report", report.Status);
        Assert.DoesNotContain("native detail", report.Status);
        Assert.False(report.IsBusy);
    }

    [Fact]
    public async Task Dismissing_the_composer_allows_the_same_report_to_be_opened_again()
    {
        var turn = await AnswerAsync("Question", "Answer");
        Choose("Other");
        var report = (await _reporter.ChooseReasonAsync(turn))!;

        // MAUI returns normally for both Send and Cancel on iOS.
        Assert.Equal(IntelligenceReportOutcome.Opened, await report.ComposeAsync());
        Assert.True(report.CanCompose);
        Assert.Contains("cannot tell whether you sent or cancelled", report.Status);
        Assert.Equal(IntelligenceReportOutcome.Opened, await report.ComposeAsync());
        await _email.Received(2).ComposeAsync(Arg.Any<EmailDraft>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_chat_change_during_email_handoff_keeps_the_preview_expired(bool fails)
    {
        var turn = await AnswerAsync("Question", "Answer");
        Choose("Other");
        var report = (await _reporter.ChooseReasonAsync(turn))!;
        var dismissal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _email.ComposeAsync(Arg.Any<EmailDraft>()).Returns(dismissal.Task);
        var opening = report.ComposeAsync();
        Assert.True(report.IsBusy);
        ChangeConnection();
        if (fails) dismissal.SetException(new InvalidOperationException("native detail"));
        else dismissal.SetResult();

        Assert.Equal(IntelligenceReportOutcome.Expired, await opening);
        Assert.Equal(IntelligenceReportViewModel.ExpiredMessage, report.Status);
        Assert.Equal("", report.Answer);
        Assert.False(report.CanCompose);
        Assert.False(report.CanCopy);
        Assert.False(report.IsOpened);
    }

    [Fact]
    public async Task An_oversized_report_must_be_shortened_rather_than_cut_off()
    {
        var turn = await AnswerAsync("Question", "Answer");
        Choose("Other");
        var report = (await _reporter.ChooseReasonAsync(turn))!;

        report.Answer = new string('a', IntelligenceReportFormat.MaxBodyLength);

        Assert.True(report.IsTooLong);
        Assert.False(report.CanCompose);
        Assert.Contains("too long", report.Status);
        Assert.Equal(IntelligenceReportOutcome.TooLong, await report.ComposeAsync());
        await _email.DidNotReceiveWithAnyArgs().ComposeAsync(default!);

        report.Answer = "Short";
        Assert.True(report.CanCompose);
        Assert.Equal("", report.Status);
    }

    [Fact]
    public void Control_characters_and_line_endings_are_normalised_without_changing_the_text()
    {
        Assert.Equal("Line one\nLine two\n\tindented `code` [[device:12]] caf\u00e9",
            IntelligenceReportFormat.Clean("Line one\r\nLine two\r\tindented `code`\u0000\u0007 [[device:12]] caf\u00e9\u001b"));
        Assert.Equal("", IntelligenceReportFormat.Clean(null));
    }

    [Fact]
    public void Mailto_components_cannot_inject_recipients_or_parameters()
    {
        var draft = new EmailDraft("support@example.net", "Question &bcc=other@example.net",
            "Line one\nUnicode: café? # &body=extra %20");
        var uri = draft.ToMailtoUri().OriginalString;
        Assert.StartsWith("mailto:support@example.net?subject=", uri);
        var fields = uri[(uri.IndexOf('?') + 1)..].Split('&');
        Assert.Equal(2, fields.Length);
        Assert.Equal(draft.Subject, Uri.UnescapeDataString(fields[0]["subject=".Length..]));
        Assert.Equal(draft.Body, Uri.UnescapeDataString(fields[1]["body=".Length..]));
    }

    [Theory]
    [InlineData("Offensive", IntelligenceReportReason.Offensive)]
    [InlineData("Wrong or misleading", IntelligenceReportReason.WrongOrMisleading)]
    [InlineData("Other", IntelligenceReportReason.Other)]
    public void Each_offered_reason_round_trips(string label, IntelligenceReportReason expected)
    {
        Assert.True(IntelligenceReportFormat.TryParse(label, out var reason));
        Assert.Equal(expected, reason);
        Assert.Equal(label, IntelligenceReportFormat.Label(reason));
    }

    private sealed class Lookups : IIntelligenceLookups
    {
        private static readonly IntelligenceEvidence Device = new(
            new("device:12", "edge-router-a", "Down", "down", Observed, "Device status at lookup time") { Object = new("device", 12) }, null);

        public IReadOnlyList<IntelligenceLookup> Create() => Create(_ => { });

        public IReadOnlyList<IntelligenceLookup> Create(Action<IReadOnlyList<IntelligenceEvidence>> evidence) =>
            [new("network_overview", "Overview", (_, _) => { evidence([Device]); return Task.FromResult(LookupText); })];
    }

    private sealed class Model : IIntelligenceModel
    {
        public string Next { get; set; } = "";
        public string? Partial { get; set; }
        public TaskCompletionSource<IntelligenceAnswer>? Hold { get; set; }
        public IntelligenceAvailability Availability => IntelligenceAvailability.Available;
        public int? ContextSize => 4096;
        public Task<int?> CountTokensAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);
        public IIntelligenceSession StartSession(string instructions, IReadOnlyList<IntelligenceLookup> lookups) => new Session(this, lookups);

        private sealed class Session(Model owner, IReadOnlyList<IntelligenceLookup> lookups) : IIntelligenceSession
        {
            public async Task<IntelligenceAnswer> AskAsync(string question, Action<string> onText, CancellationToken cancellationToken = default)
            {
                await lookups[0].Run("", cancellationToken);
                if (owner.Partial is not null) onText(owner.Partial);
                if (owner.Hold is not null) return await owner.Hold.Task.WaitAsync(cancellationToken);
                return new IntelligenceAnswer(owner.Next, null);
            }

            public void Dispose() { }
        }
    }
}
