namespace DashyNMS.Mobile.Services;

/// <summary>
/// When the app asks before acting (#146), the same as desktop's
/// <c>Infrastructure/Confirmations</c> (desktop #239), so both apps ask the same way:
/// <list type="bullet">
/// <item>Anything nothing in DashyNMS can undo - deleting a neighbourhood or
/// a dashboard card, resetting a map layout, forgetting a stored password,
/// forgetting everything on sign-out - always asks, through
/// <see cref="IDialogService.ConfirmDestructiveAsync"/>: a red button, the
/// thing named in quotes, and the message ending <see cref="CannotBeUndone"/>.</item>
/// <item>A server action applied to a selection asks only when it covers more
/// than <see cref="BulkThreshold"/> items.</item>
/// <item>A single, reversible action (acknowledge or unacknowledge one alert,
/// rediscover one device, notify again) just happens.</item>
/// </list>
/// The confirm button always names the action, never a bare "OK".
/// </summary>
/// <remarks>
/// Desktop's copy lives in its app rather than Core, so this mirrors its two
/// values; if it moves to Core, use that instead.
/// </remarks>
public static class Confirmations
{
    /// <summary>A selection larger than this asks before a bulk server action runs.</summary>
    public const int BulkThreshold = 5;

    /// <summary>The standard closing line for a destructive confirmation.</summary>
    public const string CannotBeUndone = "This can't be undone.";

    /// <summary>Whether a bulk server action on <paramref name="count"/> items asks first.</summary>
    public static bool AsksForBulk(int count) => count > BulkThreshold;
}
