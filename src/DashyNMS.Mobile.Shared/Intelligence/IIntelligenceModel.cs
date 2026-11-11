namespace DashyNMS.Mobile.Intelligence;

/// <summary>Whether the phone's own language model can answer, and if not, why.</summary>
public enum IntelligenceAvailability
{
    /// <summary>Ready to answer.</summary>
    Available,

    /// <summary>No model on this platform or OS version: Android, or iOS before 26.</summary>
    Unsupported,

    /// <summary>The device can't run Apple Intelligence.</summary>
    DeviceNotEligible,

    /// <summary>Apple Intelligence is turned off in the phone's Settings.</summary>
    NotEnabled,

    /// <summary>The model is still downloading or preparing.</summary>
    ModelNotReady,

    /// <summary>Unavailable for a reason this build doesn't know.</summary>
    Unavailable,
}

/// <summary>
/// A read-only lookup the model may call while answering. It returns short
/// text, not JSON: the on-device model's context is a few thousand tokens for
/// instructions, lookups, question and answer together (#570).
/// </summary>
/// <param name="Name">What the model calls it; letters and underscores.</param>
/// <param name="Purpose">When and how to use it, in a sentence. This is spent from the context on every question.</param>
/// <param name="Run">Runs the lookup for the model's query, which is empty when it wants everything.</param>
public sealed record IntelligenceLookup(string Name, string Purpose, Func<string, CancellationToken, Task<string>> Run);

/// <summary>
/// A language model that answers on the phone. Pages and view models see only
/// this, so the model behind it can change (Apple's on-device model first,
/// #236) without new pages.
/// </summary>
public interface IIntelligenceModel
{
    IntelligenceAvailability Availability { get; }

    /// <summary>The model's whole context in tokens, or null where it won't say.</summary>
    int? ContextSize { get; }

    /// <summary>How many tokens <paramref name="text"/> costs as instructions, or null where the model can't count.</summary>
    Task<int?> CountTokensAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>A conversation with its own instructions and lookups. Dispose it to free the model.</summary>
    IIntelligenceSession StartSession(string instructions, IReadOnlyList<IntelligenceLookup> lookups);
}

/// <summary>One conversation with the model: one question at a time.</summary>
public interface IIntelligenceSession : IDisposable
{
    /// <summary>
    /// Asks, reporting the answer so far (all of it, not the newest words)
    /// through <paramref name="onText"/> as it's written, and returns the
    /// finished answer. Cancelling stops the model.
    /// </summary>
    /// <exception cref="IntelligenceException">The model refused, ran out of context or failed.</exception>
    Task<IntelligenceAnswer> AskAsync(string question, Action<string> onText, CancellationToken cancellationToken = default);
}

/// <param name="Text">The finished answer.</param>
/// <param name="Usage">The model's own account of the tokens this answer used, where it gives one.</param>
public sealed record IntelligenceAnswer(string Text, string? Usage);

/// <summary>Stable failure categories for UI wording; native descriptions stay diagnostic-only.</summary>
public enum IntelligenceFailureKind { Failed, Refused, ContextLimit, Unavailable }

/// <summary>Maps the Swift bridge's diagnostic case names without displaying native error text.</summary>
public static class IntelligenceFailures
{
    public static IntelligenceFailureKind FromNativeReason(string? reason) => reason switch
    {
        { } s when s.StartsWith("contextSizeExceeded", StringComparison.Ordinal) || s.StartsWith("exceededContextWindowSize", StringComparison.Ordinal) => IntelligenceFailureKind.ContextLimit,
        { } s when s.StartsWith("guardrailViolation", StringComparison.Ordinal) || s.StartsWith("refusal", StringComparison.Ordinal) => IntelligenceFailureKind.Refused,
        "unsupported" or "unavailable" => IntelligenceFailureKind.Unavailable,
        _ => IntelligenceFailureKind.Failed,
    };
}

/// <summary>The model couldn't finish an answer. The message is the model's own, for diagnostics, not for a page.</summary>
public sealed class IntelligenceException : Exception
{
    public IntelligenceException(string message, string? partialAnswer = null, IntelligenceFailureKind kind = IntelligenceFailureKind.Failed)
        : base(message)
    {
        PartialAnswer = partialAnswer;
        Kind = kind;
    }

    /// <summary>What had been written before it stopped.</summary>
    public string? PartialAnswer { get; }

    public IntelligenceFailureKind Kind { get; }
}

/// <summary>No model: the default, on Android, and in tests.</summary>
public sealed class NoIntelligenceModel : IIntelligenceModel
{
    public IntelligenceAvailability Availability => IntelligenceAvailability.Unsupported;

    public int? ContextSize => null;

    public Task<int?> CountTokensAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);

    public IIntelligenceSession StartSession(string instructions, IReadOnlyList<IntelligenceLookup> lookups) =>
        throw new IntelligenceException("There's no language model on this phone.");
}
