namespace DashyNMS.Mobile.Services;

/// <summary>Modal prompts, so view models don't depend on MAUI's pages.</summary>
public interface IDialogService
{
    Task AlertAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    /// <summary>
    /// Asks before something that can't be undone (#146): <paramref name="action"/>
    /// on a red button, beside Cancel - see <see cref="Confirmations"/>. A plain
    /// alert can't colour its button, so the platforms show their own.
    /// </summary>
    Task<bool> ConfirmDestructiveAsync(string title, string message, string action);

    /// <summary>Asks which of <paramref name="options"/>; null when cancelled.</summary>
    Task<string?> ChooseAsync(string title, IReadOnlyList<string> options);

    /// <summary>
    /// As <see cref="ChooseAsync(string, IReadOnlyList{string})"/>, with
    /// <paramref name="destructive"/> - one that can't be undone - shown red
    /// after the others. Null when cancelled.
    /// </summary>
    Task<string?> ChooseAsync(string title, IReadOnlyList<string> options, string destructive);

    /// <summary>Asks for a line of text. Returns null when cancelled; an empty string when accepted blank.</summary>
    Task<string?> PromptAsync(string title, string message, string accept, string placeholder);
}
