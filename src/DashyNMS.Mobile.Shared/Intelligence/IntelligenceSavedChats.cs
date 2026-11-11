using System.Globalization;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Intelligence;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>
/// The phone's chat in Core's saved-chat shape (#229), and back. Each turn is a
/// user message and an answer message, as desktop saves them, so the files hold
/// what desktop's would: draft, detail, model, answer state, lookup activity,
/// typed references and images.
/// </summary>
/// <remarks>
/// Files are authenticated (AES-GCM, #222), yet a restored reference still has
/// to pass the checks a live one does: a valid typed object whose key matches
/// it, and an exact filter only for a group or location. Anything else is
/// dropped, so its marker stays plain text and nothing can open it. Chips are
/// checked again when tapped (<see cref="IntelligenceAnswerActions.TryRoute"/>),
/// and destination pages handle a target that has since gone.
/// </remarks>
public static class IntelligenceSavedChats
{
    /// <summary>The phone's model, recorded as the chat's model. There is only one in 2.0.0.</summary>
    public const string OnDeviceModel = "on-device";

    /// <summary>What a chat is called until its first question names it.</summary>
    public const string NewChatTitle = "New chat";

    /// <summary>
    /// The status card holding a turn's exact group and location names, which
    /// Devices filters by; Core's references keep only the cleaned display name.
    /// </summary>
    internal const string FilterCardTitle = "Exact filters";

    /// <summary>Marks an answer whose earlier turns were left out of the prompt.</summary>
    internal const string HistoryOmittedActivity = "history-omitted";

    internal const string ClosedMidAnswer = "Interrupted when the app closed. Retry when you're ready.";

    private const int MaxFilterLength = 512;

    /// <summary>
    /// Chats belong to the LibreNMS server they were asked about, as desktop
    /// scopes them (Core hashes the scope, so no address reaches a folder name).
    /// The prefix leaves room for 2.1.0's ChatGPT accounts (#577), which add the
    /// account as desktop does, without mixing with on-device chats.
    /// </summary>
    public static string Scope(LibreNmsConnection connection) =>
        OnDeviceModel + "\n" + connection.WebRoot.AbsoluteUri.TrimEnd('/');

    /// <summary>A title from the first question: one line, at most 80 characters, as desktop names chats.</summary>
    public static string Title(string value)
    {
        var title = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (title.Length == 0) return NewChatTitle;
        return title.Length > 80 ? title[..77] + "…" : title;
    }

    /// <summary>The turns as saved messages: fresh lists and objects, so the writer can serialise them on another thread.</summary>
    public static List<SavedIntelligenceMessage> Messages(IEnumerable<IntelligenceTurn> turns)
    {
        var messages = new List<SavedIntelligenceMessage>();
        foreach (var turn in turns)
        {
            messages.Add(new SavedIntelligenceMessage { IsUser = true, CreatedAt = turn.AskedAt, Text = turn.Question });
            var filters = turn.Evidence
                .Where(e => e.Filter is { Length: > 0 } && e.Reference.Object is { Kind: "group" or "location" })
                .Select(e => new IntelligenceStatusRow(e.Filter!, "", "", "") { Object = e.Reference.Object })
                .ToList();
            messages.Add(new SavedIntelligenceMessage
            {
                CreatedAt = turn.AskedAt,
                Text = turn.Answer,
                ReplyStyle = turn.Detail,
                SubmittedQuestion = turn.Question,
                // Saved mid-answer: reopened as Interrupted, as desktop does.
                IsIncomplete = turn.State == IntelligenceTurnState.Running,
                Outcome = turn.State switch
                {
                    IntelligenceTurnState.Running => IntelligenceReplyOutcome.Generating,
                    IntelligenceTurnState.Completed => IntelligenceReplyOutcome.Completed,
                    IntelligenceTurnState.Stopped => IntelligenceReplyOutcome.Stopped,
                    IntelligenceTurnState.Interrupted => IntelligenceReplyOutcome.Interrupted,
                    _ => IntelligenceReplyOutcome.Failed,
                },
                FailureText = turn.Message.Length > 0 ? turn.Message : null,
                Activity = turn.HistoryOmitted ? HistoryOmittedActivity : null,
                PriorReferences = turn.Evidence.Select(e => e.Reference).ToList(),
                StatusCards = filters.Count > 0 ? [new IntelligenceStatusCard(FilterCardTitle, "", turn.AskedAt, filters)] : [],
                ToolObservations = turn.Lookups.Select(lookup => new IntelligenceToolObservation(
                    lookup.Name, turn.AskedAt,
                    lookup.State is IntelligenceLookupState.Completed or IntelligenceLookupState.Failed ? lookup.Result : "",
                    lookup.State == IntelligenceLookupState.Failed)).ToList(),
                Images = turn.Images.ToList(),
            });
        }

        return messages;
    }

    /// <summary>
    /// The turns a saved chat holds. An answer saved mid-stream, or one with no
    /// answer message at all, reopens as Interrupted with its partial text.
    /// </summary>
    public static List<IntelligenceTurn> Turns(SavedIntelligenceChat chat)
    {
        var turns = new List<IntelligenceTurn>();
        var messages = chat.Messages ?? [];
        for (var i = 0; i < messages.Count; i++)
        {
            var question = messages[i];
            if (question is null || !question.IsUser) continue;
            var answer = i + 1 < messages.Count && messages[i + 1] is { IsUser: false } next ? next : null;
            if (answer is not null) i++;
            turns.Add(Turn(question, answer));
        }

        return turns;
    }

    private static IntelligenceTurn Turn(SavedIntelligenceMessage question, SavedIntelligenceMessage? answer)
    {
        var detail = IntelligenceDetail.Normalize(answer?.ReplyStyle);
        if (detail == IntelligenceDetail.Full) detail = IntelligenceDetail.Detailed;
        var turn = new IntelligenceTurn(question.Text ?? "", detail)
        {
            AskedAt = question.CreatedAt ?? answer?.CreatedAt ?? DateTimeOffset.UnixEpoch,
        };
        var closedMidAnswer = answer is null || answer.IsIncomplete || answer.Outcome is null or IntelligenceReplyOutcome.Generating;
        turn.Answer = answer?.Text ?? "";
        turn.State = closedMidAnswer ? IntelligenceTurnState.Interrupted : answer!.Outcome switch
        {
            IntelligenceReplyOutcome.Completed => IntelligenceTurnState.Completed,
            IntelligenceReplyOutcome.Stopped => IntelligenceTurnState.Stopped,
            IntelligenceReplyOutcome.Interrupted => IntelligenceTurnState.Interrupted,
            _ => IntelligenceTurnState.Failed,
        };
        turn.Message = closedMidAnswer ? ClosedMidAnswer : answer!.FailureText ?? (turn.State == IntelligenceTurnState.Completed ? "Completed" : "");
        if (answer is null) return turn;

        turn.HistoryOmitted = answer.Activity == HistoryOmittedActivity;
        foreach (var observation in answer.ToolObservations ?? [])
        {
            if (observation is null) continue;
            turn.Lookups.Add(new IntelligenceLookupProgress(observation.Tool ?? "")
            {
                Result = observation.Summary ?? "",
                State = observation.IsError ? IntelligenceLookupState.Failed
                    : string.IsNullOrEmpty(observation.Summary) ? IntelligenceLookupState.Stopped : IntelligenceLookupState.Completed,
            });
        }

        turn.AddEvidence(Evidence(answer));
        turn.Images = answer.Images ?? [];
        return turn;
    }

    /// <summary>Only references that pass the live checks, each with its exact filter where it has one.</summary>
    internal static IEnumerable<IntelligenceEvidence> Evidence(SavedIntelligenceMessage answer)
    {
        var filters = (answer.StatusCards ?? [])
            .Where(card => card is not null && card.Title == FilterCardTitle)
            .SelectMany(card => card.Rows ?? [])
            .Where(row => row?.Object is { IsValid: true, Kind: "group" or "location" }
                && !string.IsNullOrWhiteSpace(row.Name) && row.Name.Length <= MaxFilterLength)
            .GroupBy(row => row.Object!)
            .ToDictionary(group => group.Key, group => group.Last().Name);
        foreach (var reference in answer.PriorReferences ?? [])
        {
            if (reference?.Object is not { IsValid: true } target || reference.Key != Key(target)) continue;
            yield return new IntelligenceEvidence(reference,
                target.Kind is "group" or "location" && filters.TryGetValue(target, out var filter) ? filter : null);
        }
    }

    /// <summary>The marker key a lookup gives this object; faults share the alert marker.</summary>
    private static string Key(IntelligenceObjectReference target) =>
        (target.Kind == "fault" ? "alert" : target.Kind) + ":" + target.Id.ToString(CultureInfo.InvariantCulture);
}
