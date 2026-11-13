namespace DashyNMS.Mobile.Intelligence;

/// <summary>
/// The time an answer takes, in words: an on-device model can take a while,
/// and a counting "Answering… 4s" shows it is still working rather than stuck.
/// </summary>
public static class IntelligenceElapsed
{
    /// <summary>"4s", "1m 05s"; whole seconds, never negative.</summary>
    public static string Format(TimeSpan elapsed)
    {
        var seconds = (long)Math.Max(0, Math.Floor(elapsed.TotalSeconds));
        return seconds < 60 ? $"{seconds}s" : $"{seconds / 60}m {seconds % 60:00}s";
    }

    /// <summary>"Answering… 4s" while running.</summary>
    public static string Running(DateTimeOffset askedAt, DateTimeOffset now) => "Answering… " + Format(now - askedAt);

    /// <summary>"Answered in 6s" for a completed answer from this session, or null when the time isn't known.</summary>
    public static string? Answered(IntelligenceTurn turn) =>
        turn is { State: IntelligenceTurnState.Completed, AnsweredAt: { } answered } ? "Answered in " + Format(answered - turn.AskedAt) : null;
}
