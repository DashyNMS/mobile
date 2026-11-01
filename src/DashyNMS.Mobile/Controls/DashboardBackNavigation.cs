namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Keeps dashboard editing and its pickers on the same glass Back control,
/// while leaving each editor's explicit Save action and save rules intact.
/// </summary>
internal static class DashboardBackNavigation
{
    public static void Attach(ContentPage page, BackBar bar)
    {
#if IOS
        if (!TabBarButtons.IsAvailable)
        {
            return;
        }

        bar.ShowsBack = false;
        // Graph and Top card editors still need their explicit Save button.
        bar.IsVisible = bar.Actions is not null;
        page.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
        var padding = page.Padding;
        var showing = false;

        void PlaceControls()
        {
            if (!showing)
            {
                return;
            }

            var edges = CoveredEdges.ForScrolling(page);
            page.Padding = new Thickness(padding.Left + edges.Left, padding.Top,
                padding.Right + edges.Right, padding.Bottom + edges.Bottom);
        }

        void OnMoved(object? sender, EventArgs e) => page.Dispatcher.Dispatch(PlaceControls);

        CoveredEdges.Watch(page, PlaceControls);
        page.Appearing += (_, _) =>
        {
            showing = true;
            TabBarButtons.Show(page, null, string.Empty,
            [
                new GlassAction("chevron.backward", "Back to " + BackBar.PreviousTitle(page),
                    () => _ = Shell.Current.GoToAsync(".."), IsBack: true),
            ]);
            TabBarButtons.Moved += OnMoved;
            PlaceControls();
        };
        page.Disappearing += (_, _) =>
        {
            showing = false;
            TabBarButtons.Moved -= OnMoved;
            TabBarButtons.Hide(page);
        };
#endif
    }
}
