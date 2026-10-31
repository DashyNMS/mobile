using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Adapters;

/// <summary>
/// <see cref="IClipboardText"/> over MAUI's clipboard (#161). Its HasText asks
/// iOS's UIPasteboard.hasStrings, which doesn't show the paste banner; the
/// text is read only when the user taps to use it.
/// </summary>
public sealed class MauiClipboard : IClipboardText
{
    public bool HasText => Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.HasText;

    public Task<string?> GetTextAsync() => Microsoft.Maui.ApplicationModel.DataTransfer.Clipboard.Default.GetTextAsync();
}
