using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Adapters;

public sealed class DialogService : IDialogService
{
    private static Page Page => Shell.Current;

    public Task AlertAsync(string title, string message) =>
        MainThread.InvokeOnMainThreadAsync(() => Page.DisplayAlertAsync(title, message, "OK"));

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        MainThread.InvokeOnMainThreadAsync(() => Page.DisplayAlertAsync(title, message, accept, cancel));

    public Task<string?> ChooseAsync(string title, IReadOnlyList<string> options) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var choice = await Page.DisplayActionSheetAsync(title, "Cancel", null, options.ToArray());
            return options.Contains(choice) ? choice : null;
        });

    public Task<string?> PromptAsync(string title, string message, string accept, string placeholder) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
            (string?)await Page.DisplayPromptAsync(title, message, accept, "Cancel", placeholder));
}
