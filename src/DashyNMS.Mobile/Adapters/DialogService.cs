using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Adapters;

public sealed class DialogService : IDialogService
{
    private static Page Page => Shell.Current;

    public Task AlertAsync(string title, string message) =>
        MainThread.InvokeOnMainThreadAsync(() => Page.DisplayAlertAsync(title, message, "OK"));

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) =>
        MainThread.InvokeOnMainThreadAsync(() => Page.DisplayAlertAsync(title, message, accept, cancel));

    /// <summary>
    /// MAUI's alert has no destructive button, so each platform shows its own
    /// (#146): iOS's destructive action style, and on Android the confirm
    /// button in the critical red. Cancel is the safe default either way -
    /// iOS bolds it, and dismissing the dialog counts as Cancel.
    /// </summary>
    public Task<bool> ConfirmDestructiveAsync(string title, string message, string action) =>
        MainThread.InvokeOnMainThreadAsync(() =>
        {
#if IOS
            if (Microsoft.Maui.ApplicationModel.Platform.GetCurrentUIViewController() is { } presenter)
            {
                var answered = new TaskCompletionSource<bool>();
                var alert = UIKit.UIAlertController.Create(title, message, UIKit.UIAlertControllerStyle.Alert);
                alert.AddAction(UIKit.UIAlertAction.Create("Cancel", UIKit.UIAlertActionStyle.Cancel, _ => answered.TrySetResult(false)));
                alert.AddAction(UIKit.UIAlertAction.Create(action, UIKit.UIAlertActionStyle.Destructive, _ => answered.TrySetResult(true)));
                presenter.PresentViewController(alert, true, null);
                return answered.Task;
            }
#elif ANDROID
            if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is { } activity)
            {
                var answered = new TaskCompletionSource<bool>();
                var dialog = new AndroidX.AppCompat.App.AlertDialog.Builder(activity)
                    .SetTitle(title)
                    .SetMessage(message)
                    .SetNegativeButton("Cancel", (_, _) => answered.TrySetResult(false))
                    .SetPositiveButton(action, (_, _) => answered.TrySetResult(true))
                    .Create();

                // Back, or a tap outside: Cancel. A button's own answer comes first.
                dialog.DismissEvent += (_, _) => answered.TrySetResult(false);
                dialog.Show();

                var red = Application.Current?.RequestedTheme == AppTheme.Dark ? "#FF7B72" : "#B42318";
                dialog.GetButton((int)Android.Content.DialogButtonType.Positive)?.SetTextColor(Android.Graphics.Color.ParseColor(red));
                return answered.Task;
            }
#endif
            return Page.DisplayAlertAsync(title, message, action, "Cancel");
        });

    public Task<string?> ChooseAsync(string title, IReadOnlyList<string> options) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var choice = await Page.DisplayActionSheetAsync(title, "Cancel", null, options.ToArray());
            return options.Contains(choice) ? choice : null;
        });

    public Task<string?> ChooseAsync(string title, IReadOnlyList<string> options, string destructive) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var choice = await Page.DisplayActionSheetAsync(title, "Cancel", destructive, options.ToArray());
            return choice == destructive || options.Contains(choice) ? choice : null;
        });

    public Task<string?> PromptAsync(string title, string message, string accept, string placeholder) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
            (string?)await Page.DisplayPromptAsync(title, message, accept, "Cancel", placeholder));
}
