using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Pull to refresh without the platform's spinner. The dashboard's pull
/// sets its logo beating (#100); a list's pull shows its <see cref="BusyCard"/>
/// over the list instead (#111) - the same card it loads with the first time,
/// as the network map does (#109) - so the app shows loading one way.
/// </summary>
/// <remarks>
/// Only a pull, or the first load, shows the card: the refreshes a page does
/// on its own (coming back to the tab, a timer) stay quiet, so it doesn't
/// flash up with every tab switch. The list stays visible beneath it until
/// the new one arrives.
/// </remarks>
internal static class PullToRefresh
{
    /// <summary>
    /// <paramref name="refresh"/>'s spinner put away, and <paramref name="card"/>
    /// shown while <paramref name="viewModel"/> loads for the first time
    /// (<paramref name="loading"/>) or after a pull (<paramref name="refreshing"/>).
    /// </summary>
    public static void ShowBusyCard(RefreshView refresh, BusyCard card, ViewModelBase viewModel, string loading, string refreshing)
    {
        var pulled = false;

        void Update()
        {
            card.Text = pulled && !viewModel.IsLoadingFirstTime ? refreshing : loading;
            card.IsVisible = viewModel.IsLoadingFirstTime || (pulled && viewModel.IsBusy);
        }

        HideSpinner(refresh, () =>
        {
            pulled = true;
            Update();

            // The command may have had nothing to do (a refresh already
            // running finishes it): nothing then to wait for.
            refresh.Dispatcher.Dispatch(() =>
            {
                if (!viewModel.IsBusy)
                {
                    pulled = false;
                    Update();
                }
            });
        });

        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ViewModelBase.IsBusy) or nameof(ViewModelBase.IsLoadingFirstTime))
            {
                if (!viewModel.IsBusy)
                {
                    pulled = false;
                }

                Update();
            }
        };

        Update();
    }

    /// <summary>
    /// A pull still runs the view's command, but its spinner is never seen:
    /// put away at once, and drawn in no colour - on Android, whose spinner
    /// sits on a disc with a shadow, parked above the top edge instead, so the
    /// pull measures the same but the disc never comes into view.
    /// </summary>
    public static void HideSpinner(RefreshView refresh, Action? pulled = null)
    {
        refresh.Refreshing += (_, _) =>
        {
            pulled?.Invoke();
            refresh.Dispatcher.Dispatch(() => refresh.IsRefreshing = false);
        };

        refresh.RefreshColor = Colors.Transparent;
        refresh.HandlerChanged += (_, _) =>
        {
#if ANDROID
            if (refresh.Handler?.PlatformView is AndroidX.SwipeRefreshLayout.Widget.SwipeRefreshLayout layout)
            {
                layout.SetProgressViewOffset(false, -2000, -1000);
            }
#endif
        };
    }
}
