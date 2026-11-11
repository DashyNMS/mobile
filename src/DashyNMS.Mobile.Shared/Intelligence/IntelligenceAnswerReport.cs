using System.ComponentModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>Why someone reports an answer (#574): the choices Apple and Google ask AI apps to offer.</summary>
public enum IntelligenceReportReason { Offensive, WrongOrMisleading, Other }

/// <summary>What happened when the person chose to open the report in their email app.</summary>
public enum IntelligenceReportOutcome { Opened, Unavailable, Failed, TooLong, Expired }

/// <summary>
/// The report's exact wording. The body holds the reason, the question and the
/// answer, and nothing else: no lookup results, other turns, server details,
/// app or device identifiers, or attachments. What the preview shows is built by
/// these same functions, so the preview and the email can't drift apart.
/// </summary>
public static class IntelligenceReportFormat
{
    /// <summary>
    /// Bounds what a draft can carry. Answers are a few hundred tokens, but a
    /// question can be long; past this the person is asked to remove text rather
    /// than having it cut off unseen.
    /// </summary>
    public const int MaxBodyLength = 8000;

    public static IReadOnlyList<string> ReasonLabels { get; } = ["Offensive", "Wrong or misleading", "Other"];

    public static string Label(IntelligenceReportReason reason) => reason switch
    {
        IntelligenceReportReason.Offensive => "Offensive",
        IntelligenceReportReason.WrongOrMisleading => "Wrong or misleading",
        _ => "Other",
    };

    public static bool TryParse(string? label, out IntelligenceReportReason reason)
    {
        var index = label is null ? -1 : ReasonLabels.ToList().IndexOf(label);
        reason = index < 0 ? IntelligenceReportReason.Other : (IntelligenceReportReason)index;
        return index >= 0;
    }

    public static string Subject(IntelligenceReportReason reason) => "DashyNMS Intelligence answer report: " + Label(reason);

    public static string Body(IntelligenceReportReason reason, string? question, string? answer) =>
        "Reason: " + Label(reason) + "\n\nQuestion:\n" + Clean(question) + "\n\nAnswer:\n" + Clean(answer) + "\n";

    /// <summary>
    /// Keeps the text as written, Markdown and markers included, but normalises
    /// line endings and drops control characters other than new lines and tabs.
    /// A stray control character could otherwise break a mailto fallback or hide
    /// text from the preview that the email would still carry.
    /// </summary>
    public static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var normalised = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var builder = new StringBuilder(normalised.Length);
        foreach (var c in normalised)
            if (c is '\n' or '\t' || !char.IsControl(c)) builder.Append(c);
        return builder.ToString();
    }
}

/// <summary>
/// The answer someone chose to report, captured when they chose it. It stays
/// reportable only while the same turn, with the same text, still belongs to the
/// current chat and connection.
/// </summary>
internal sealed class IntelligenceReportSelection(IntelligenceTurn turn, long revision)
{
    public IntelligenceTurn Turn { get; } = turn;
    public long Revision { get; } = revision;
    public string Question { get; } = turn.Question;
    public string Answer { get; } = turn.Answer;

    public bool IsCurrent(IntelligenceConversationViewModel conversation) =>
        conversation.OwnsTurn(Turn) && conversation.ChatRevision == Revision
        && Turn.State != IntelligenceTurnState.Running
        && string.Equals(Turn.Question, Question, StringComparison.Ordinal)
        && string.Equals(Turn.Answer, Answer, StringComparison.Ordinal);
}

/// <summary>
/// Starts "Report this answer" (#574) from the long-press menu or the VoiceOver
/// action. It asks for a reason, then rechecks ownership: a sign-out, connection
/// change or new chat while the reason sheet is open discards the report rather
/// than previewing an answer from a chat that has gone.
/// </summary>
public sealed class IntelligenceAnswerReporter(
    IntelligenceConversationViewModel conversation, IDialogService dialogs, IEmailComposer email)
{
    /// <summary>A finished, stopped or failed answer with text, from the current chat.</summary>
    public bool CanReport(IntelligenceTurn turn) =>
        conversation.OwnsTurn(turn) && turn.State != IntelligenceTurnState.Running && !string.IsNullOrWhiteSpace(turn.Answer);

    /// <summary>Null when cancelled or when the chat changed while choosing.</summary>
    public async Task<IntelligenceReportViewModel?> ChooseReasonAsync(IntelligenceTurn turn)
    {
        if (!CanReport(turn)) return null;
        var selection = new IntelligenceReportSelection(turn, conversation.ChatRevision);
        var choice = await dialogs.ChooseAsync("Why are you reporting this answer?", IntelligenceReportFormat.ReasonLabels);
        if (!IntelligenceReportFormat.TryParse(choice, out var reason)) return null;
        if (!selection.IsCurrent(conversation))
        {
            await dialogs.AlertAsync("This answer can't be reported", "The chat changed while you were choosing a reason. Nothing was sent.");
            return null;
        }
        return new IntelligenceReportViewModel(conversation, selection, reason, email);
    }
}

/// <summary>
/// The report preview. It shows the destination, subject, reason, question and
/// answer exactly as the email will carry them. The question and answer can be
/// edited to remove names and addresses first. Nothing leaves the app until the
/// person opens the draft in their email app and sends it there.
/// </summary>
public sealed class IntelligenceReportViewModel : ObservableObject
{
    public const string ExpiredMessage = "This chat is no longer available, so the report preview was discarded.";

    private readonly IntelligenceConversationViewModel _conversation;
    private readonly IntelligenceReportSelection _selection;
    private readonly IEmailComposer _email;
    private bool _attached;

    internal IntelligenceReportViewModel(IntelligenceConversationViewModel conversation, IntelligenceReportSelection selection,
        IntelligenceReportReason reason, IEmailComposer email)
    {
        _conversation = conversation;
        _selection = selection;
        _email = email;
        Reason = reason;
        _question = IntelligenceReportFormat.Clean(selection.Question);
        _answer = IntelligenceReportFormat.Clean(selection.Answer);
    }

    public string To => AppLinks.SupportEmail;
    public IntelligenceReportReason Reason { get; }
    public string ReasonText => IntelligenceReportFormat.Label(Reason);
    public string Subject => IntelligenceReportFormat.Subject(Reason);

    private string _question;
    /// <summary>Starts as the chosen question exactly; the person may remove identifying text.</summary>
    public string Question { get => _question; set { if (!IsExpired && SetProperty(ref _question, value ?? "")) BodyChanged(); } }

    private string _answer;
    /// <summary>Starts as the chosen answer exactly, Markdown and markers included.</summary>
    public string Answer { get => _answer; set { if (!IsExpired && SetProperty(ref _answer, value ?? "")) BodyChanged(); } }

    /// <summary>Exactly the email body handed to the composer.</summary>
    public string Body => IntelligenceReportFormat.Body(Reason, Question, Answer);

    /// <summary>Everything the draft contains, for copying when no email app can take it.</summary>
    public string ReportText => "To: " + To + "\nSubject: " + Subject + "\n\n" + Body;

    public bool IsTooLong => Body.Length > IntelligenceReportFormat.MaxBodyLength;

    private bool _expired;
    public bool IsExpired { get => _expired; private set => SetProperty(ref _expired, value); }

    private bool _busy;
    public bool IsBusy { get => _busy; private set { if (SetProperty(ref _busy, value)) OnPropertyChanged(nameof(CanCompose)); } }

    private bool _opened;
    /// <summary>The composer was invoked; dismissal does not tell us whether the person sent or cancelled.</summary>
    public bool IsOpened { get => _opened; private set => SetProperty(ref _opened, value); }

    private bool _canCopy;
    /// <summary>Offered only when no email app could take the draft.</summary>
    public bool CanCopy { get => _canCopy; private set => SetProperty(ref _canCopy, value); }

    private string _status = "";
    public string Status { get => _status; private set => SetProperty(ref _status, value); }

    public bool CanCompose => !IsExpired && !IsBusy && !IsTooLong;

    private void BodyChanged()
    {
        OnPropertyChanged(nameof(Body));
        OnPropertyChanged(nameof(ReportText));
        OnPropertyChanged(nameof(IsTooLong));
        OnPropertyChanged(nameof(CanCompose));
        Status = IsTooLong ? TooLongMessage : "";
    }

    private static string TooLongMessage =>
        $"This report is too long for an email. Remove some text to bring it under {IntelligenceReportFormat.MaxBodyLength:N0} characters.";

    /// <summary>Watches the chat while the preview is showing; pair with <see cref="Detach"/>.</summary>
    public void Attach()
    {
        if (!_attached) { _conversation.PropertyChanged += OnConversationChanged; _attached = true; }
        CheckOwnership();
    }

    public void Detach()
    {
        if (_attached) { _conversation.PropertyChanged -= OnConversationChanged; _attached = false; }
    }

    private void OnConversationChanged(object? sender, PropertyChangedEventArgs e) => CheckOwnership();

    /// <summary>
    /// Clears the preview as soon as the chosen answer stops belonging to the
    /// current chat, so a report can't carry one network's answer after sign-out,
    /// a connection change or a new chat. Returns whether it is still current.
    /// </summary>
    public bool CheckOwnership()
    {
        if (IsExpired) return false;
        if (_selection.IsCurrent(_conversation)) return true;
        _question = "";
        _answer = "";
        IsExpired = true;
        CanCopy = false;
        OnPropertyChanged(nameof(Question));
        OnPropertyChanged(nameof(Answer));
        BodyChanged();
        Status = ExpiredMessage;
        return false;
    }

    /// <summary>The draft's full text for the clipboard, or null once the chat has changed.</summary>
    public string? CopyText() => CheckOwnership() && CanCopy ? ReportText : null;

    /// <summary>Opens the email app with this draft. It never sends; the person does that in their email app.</summary>
    public async Task<IntelligenceReportOutcome> ComposeAsync()
    {
        if (IsBusy) return IntelligenceReportOutcome.Failed;
        if (!CheckOwnership()) return IntelligenceReportOutcome.Expired;
        if (IsTooLong) { Status = TooLongMessage; return IntelligenceReportOutcome.TooLong; }
        if (!_email.IsAvailable)
        {
            CanCopy = true;
            Status = $"No email app is set up on this device. Copy the report and send it to {To} from another app.";
            return IntelligenceReportOutcome.Unavailable;
        }
        IsBusy = true;
        try
        {
            await _email.ComposeAsync(new EmailDraft(To, Subject, Body));
            if (!CheckOwnership()) return IntelligenceReportOutcome.Expired;
            IsOpened = true;
            CanCopy = false;
            Status = "The email composer was opened. This app cannot tell whether you sent or cancelled the report. You can open it again.";
            return IntelligenceReportOutcome.Opened;
        }
        catch (Exception)
        {
            if (!CheckOwnership()) return IntelligenceReportOutcome.Expired;
            CanCopy = true;
            Status = $"Your email app couldn't open the report. Copy it and send it to {To} from another app.";
            return IntelligenceReportOutcome.Failed;
        }
        finally { IsBusy = false; }
    }
}
