namespace DashyNMS.Mobile.Services;

/// <summary>Modal prompts, so view models don't depend on MAUI's pages.</summary>
public interface IDialogService
{
    Task AlertAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message, string accept, string cancel);

    /// <summary>Asks which of <paramref name="options"/>; null when cancelled.</summary>
    Task<string?> ChooseAsync(string title, IReadOnlyList<string> options);

    /// <summary>Asks for a line of text. Returns null when cancelled; an empty string when accepted blank.</summary>
    Task<string?> PromptAsync(string title, string message, string accept, string placeholder);
}
