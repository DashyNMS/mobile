using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Diagnostics;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>
/// Settings › Diagnostics › Intelligence check (#570): asks the phone's own
/// model one question with the network overview lookup, and measures what
/// the rest of Intelligence (#236) has to budget for - tokens, time to the
/// first words and to the whole answer, and memory.
/// </summary>
/// <remarks>
/// Temporary: #571 replaces it with Settings › Intelligence. It's the only
/// way to measure on a real iPhone, since the model isn't in CI's simulator.
/// </remarks>
public sealed partial class IntelligenceCheckViewModel : ObservableObject
{
    public const string DefaultQuestion = "How is the network doing?";

    /// <summary>
    /// Written for the on-device model: short, since every word is spent
    /// from the context on each question, and it follows few rules well.
    /// </summary>
    internal const string Instructions =
        "You answer questions about a computer network monitored by LibreNMS. "
        + "Before answering, call " + NetworkOverviewLookup.Name + " to get the current state. "
        + "Use only what it returns. If it does not say, say you do not know. "
        + "Answer in a few short sentences, naming the devices and alerts that need attention.";

    private readonly IIntelligenceModel _model;
    private readonly NetworkOverviewLookup _overview;
    private readonly IShareService? _share;
    private readonly Func<MemoryReading>? _memory;
    private readonly TimeProvider _time;
    private CancellationTokenSource? _running;

    public IntelligenceCheckViewModel(
        IIntelligenceModel model,
        NetworkOverviewLookup overview,
        IShareService? share = null,
        Func<MemoryReading>? memory = null,
        TimeProvider? time = null)
    {
        _model = model;
        _overview = overview;
        _share = share;
        _memory = memory;
        _time = time ?? TimeProvider.System;
    }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    private string _question = DefaultQuestion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnswer))]
    private string _answer = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReport))]
    [NotifyCanExecuteChangedFor(nameof(ShareCommand))]
    private string _report = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    private string _status = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotRunning))]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isRunning;

    public bool IsNotRunning => !IsRunning;

    public bool HasAnswer => Answer.Length > 0;

    public bool HasReport => Report.Length > 0;

    public bool HasStatus => Status.Length > 0;

    public bool IsAvailable => _model.Availability == IntelligenceAvailability.Available;

    public bool CanShare => _share is not null;

    /// <summary>Why the model can or can't answer, in plain words.</summary>
    public string AvailabilityText => Describe(_model.Availability);

    internal static string Describe(IntelligenceAvailability availability) => availability switch
    {
        IntelligenceAvailability.Available => "The on-device model is ready.",
        IntelligenceAvailability.DeviceNotEligible => "This device doesn't support Apple Intelligence.",
        IntelligenceAvailability.NotEnabled => "Apple Intelligence is turned off. Turn it on in the Settings app, under Apple Intelligence & Siri.",
        IntelligenceAvailability.ModelNotReady => "The on-device model is still downloading or getting ready. Try again later.",
        IntelligenceAvailability.Unsupported => "There's no on-device model on this phone. It needs iOS 26 or later.",
        _ => "The on-device model isn't available.",
    };

    private bool CanRun() => !IsRunning && IsAvailable && !string.IsNullOrWhiteSpace(Question);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        using var running = _running = new CancellationTokenSource();
        var cancellationToken = running.Token;
        var question = Question.Trim();
        var report = new StringBuilder();
        var outcome = "Completed";
        string? lookupText = null;
        IsRunning = true;
        Answer = string.Empty;
        Report = string.Empty;
        Status = "Asking LibreNMS…";

        // The model reports from its own thread; the page is bound on this one.
        // Once the answer has ended, a late report mustn't replace it.
        var context = SynchronizationContext.Current;
        var streaming = true;
        void Show(string text)
        {
            if (context is null)
                Answer = text;
            else
                context.Post(_ => { if (streaming) Answer = text; }, null);
        }

        try
        {
            report.AppendLine("DashyNMS Intelligence check");
            report.Append("Model: ").AppendLine(_model.Availability.ToString());
            report.Append("Context size: ").AppendLine(Tokens(_model.ContextSize));

            // The lookup on its own first, so its size and LibreNMS's time are
            // known even if the model never calls it.
            var started = _time.GetTimestamp();
            lookupText = await _overview.RunAsync(cancellationToken);
            var lookupTook = _time.GetElapsedTime(started);

            var lookup = _overview.AsLookup();
            await CountAsync(report, "Instructions", Instructions, cancellationToken);
            await CountAsync(report, "Lookup definition", lookup.Name + ": " + lookup.Purpose, cancellationToken);
            await CountAsync(report, "Lookup result", lookupText, cancellationToken);
            await CountAsync(report, "Question", question, cancellationToken);
            report.Append("Lookup took: ").AppendLine(Seconds(lookupTook));

            var before = _memory?.Invoke();
            var lookupsRun = 0;
            var counted = lookup with
            {
                Run = async (query, token) =>
                {
                    Interlocked.Increment(ref lookupsRun);
                    return await lookup.Run(query, token);
                },
            };

            Status = "Asking the model…";
            TimeSpan? firstWords = null;
            started = _time.GetTimestamp();
            using var session = _model.StartSession(Instructions, [counted]);
            IntelligenceAnswer? answer = null;
            try
            {
                answer = await session.AskAsync(
                    question,
                    text =>
                    {
                        if (text.Length > 0)
                            firstWords ??= _time.GetElapsedTime(started);
                        Show(text);
                    },
                    cancellationToken);
                streaming = false;
                Answer = answer.Text;
            }
            catch (OperationCanceledException)
            {
                streaming = false;
                outcome = "Stopped";
            }
            catch (IntelligenceException ex)
            {
                streaming = false;
                outcome = "Failed: " + ex.Message;
                if (!string.IsNullOrEmpty(ex.PartialAnswer))
                    Answer = ex.PartialAnswer;
            }

            var took = _time.GetElapsedTime(started);
            var after = _memory?.Invoke();
            var finalAnswer = answer?.Text ?? Answer;

            report.Append("Lookups run by the model: ").AppendLine(lookupsRun.ToString(CultureInfo.InvariantCulture));
            report.Append("First words after: ").AppendLine(firstWords is { } first ? Seconds(first) : "none");
            report.Append("Finished after: ").AppendLine(Seconds(took));
            await CountAsync(report, "Answer", finalAnswer, CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(answer?.Usage))
                report.Append("Model's own usage: ").AppendLine(answer.Usage);
            if (before is not null && after is not null)
            {
                report.Append("App memory: ").Append(Megabytes(before.FootprintBytes)).Append(" before, ")
                    .Append(Megabytes(after.FootprintBytes)).AppendLine(" after");
            }
        }
        catch (OperationCanceledException)
        {
            outcome = "Stopped";
        }
        catch (Exception ex)
        {
            outcome = "Failed: " + ViewModels.ViewModelBase.Describe(ex);
        }
        finally
        {
            report.Append("Outcome: ").AppendLine(outcome);
            if (lookupText is not null)
                report.AppendLine().AppendLine("Lookup result:").AppendLine(lookupText);
            if (Answer.Length > 0)
                report.AppendLine().AppendLine("Answer:").AppendLine(Answer);

            Report = report.ToString().TrimEnd();
            Status = outcome;
            _running = null;
            IsRunning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Stop() => _running?.Cancel();

    private bool CanShareReport() => HasReport && CanShare;

    /// <summary>The numbers and the answer as a text file, for the write-up in docs/DEVELOPING.md.</summary>
    [RelayCommand(CanExecute = nameof(CanShareReport))]
    private Task ShareAsync() =>
        _share!.ShareTextFileAsync("dashynms-intelligence-check.txt", Report, "text/plain", "Intelligence check");

    private async Task CountAsync(StringBuilder report, string what, string text, CancellationToken cancellationToken)
    {
        var tokens = await _model.CountTokensAsync(text, cancellationToken);
        report.Append(what).Append(": ").Append(Tokens(tokens))
            .Append(" (").Append(text.Length.ToString(CultureInfo.InvariantCulture)).AppendLine(" characters)");
    }

    private static string Tokens(int? tokens) =>
        tokens is { } count ? count.ToString(CultureInfo.InvariantCulture) + " tokens" : "unknown";

    private static string Seconds(TimeSpan time) => time.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture) + " s";

    private static string Megabytes(long? bytes) =>
        bytes is { } count ? (count / 1048576d).ToString("0.0", CultureInfo.InvariantCulture) + " MB" : "unknown";
}
