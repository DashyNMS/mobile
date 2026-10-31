namespace DashyNMS.Mobile.Services;

/// <summary>
/// The phone's clipboard, for offering a copied API token at sign-in (#161).
/// </summary>
/// <remarks>
/// <see cref="HasText"/> only asks whether there's text: on iOS that doesn't
/// show the "pasted from" banner, and on Android 13 and later it doesn't
/// show the clipboard toast. The text itself is read only when the user taps
/// to use it.
/// </remarks>
public interface IClipboardText
{
    bool HasText { get; }

    Task<string?> GetTextAsync();
}
