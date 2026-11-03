using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

public partial class HealthPage : ContentPage, ISearchHost
{
    private readonly HealthViewModel _viewModel;
    private bool _searchOpen;
    private IDisposable? _edges;

    public HealthPage(HealthViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading sensors…", "Refreshing sensors…");
        SearchSlot.HeightRequest = 0;
        SearchField.IsVisible = false;
        SearchButton.Command = new Command(OpenSearch);
        Search.Unfocused += (_, _) => CloseSearch();
        List.Scrolled += (_, _) => { if (Search.IsFocused) KeyboardDismiss.Close(Search); };
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
        SearchButton.IsVisible = !TabBarButtons.IsAvailable;
        List.HandlerChanged += (_, _) => StopInsets();
        List.Loaded += (_, _) => StopInsets();
#endif
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Header.Apply(this);
        _edges ??= CoveredEdges.Watch(this, PlaceControls);
#if IOS
        TabBarButtons.Show(this, OpenSearch, "Search sensors",
            [new("arrow.clockwise", "Refresh sensors", () => _viewModel.RefreshCommand.Execute(null))]);
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Moved += OnGlassMoved;
#endif
        PlaceControls();
        _ = _viewModel.RefreshIfStaleAsync();
    }

    public void OpenSearch()
    {
        if (_searchOpen) { Search.Focus(); return; }
        _searchOpen = true;
        SlideSearch(true);
    }

    private void CloseSearch()
    {
        _searchOpen = false;
        SlideSearch(false);
    }

    private void OnSearchCompleted(object? sender, EventArgs e) => CloseSearch();

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        KeyboardDismiss.Close(Search);
        _edges?.Dispose();
        _edges = null;
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Hide(this);
#endif
    }

    private void PlaceControls()
    {
        var clear = CoveredEdges.ForScrolling(this);
        Body.Padding = new Thickness(16 + clear.Left, 0, 16 + clear.Right, 0);
        var foot = 12 + clear.Bottom;
        var spacer = 0.0;
#if IOS
        if (Handler?.PlatformView is UIKit.UIView page && TabBarButtons.PillIn(page) is { } pill
            && (pill.Height > pill.Width || pill.Top > page.Bounds.Height / 2))
        {
            foot = Math.Max(0, page.Bounds.Height - pill.Bottom);
            if (pill.Height <= pill.Width && clear.Bottom > foot)
                spacer = clear.Bottom - foot + 8;
        }
#endif
        ListCard.Margin = new Thickness(0, 0, 0, foot);
        ListFoot.HeightRequest = spacer;
    }

#if IOS
    private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);

    private void StopInsets()
    {
        if (List.Handler?.PlatformView is UIKit.UIView view && FindScroll(view) is { } scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
    }

    private static UIKit.UIScrollView? FindScroll(UIKit.UIView view) =>
        view as UIKit.UIScrollView ?? view.Subviews.Select(FindScroll).FirstOrDefault(found => found is not null);
#endif
    private void SlideSearch(bool open)
    {
        const string name = "SearchSlide";
        var full = SearchField.HeightRequest + SearchField.Margin.VerticalThickness;
        SearchSlot.AbortAnimation(name);
        if (open)
        {
            // Out of sight before it shows, or it flashes in place first.
            if (!SearchField.IsVisible && !ReducedMotion.IsOn)
            {
                SearchField.TranslationY = -full;
                SearchField.Opacity = 0;
            }

            SearchField.IsVisible = true;
            Dispatcher.Dispatch(() => Search.Focus());
        }
        else
        {
            KeyboardDismiss.Close(Search);
        }

        // Done: hidden once out of sight, unless opened again meanwhile.
        void Finish()
        {
            var stillOpen = _searchOpen;
            SearchField.IsVisible = stillOpen;
            SearchSlot.HeightRequest = stillOpen ? full : 0;
            SearchField.TranslationY = 0;
            SearchField.Opacity = 1;
        }

        if (ReducedMotion.IsOn || (!open && !SearchField.IsVisible))
        {
            Finish();
            return;
        }

        // From wherever it is now, so a quick open-close doesn't jump.
        var fromHeight = Math.Max(0, SearchSlot.HeightRequest);
        var fromY = SearchField.TranslationY;
        var fromOpacity = SearchField.Opacity;
        var toHeight = open ? full : 0;
        var toY = open ? 0 : -full;
        var toOpacity = open ? 1.0 : 0;
        SearchSlot.Animate(
            name,
            v =>
            {
                SearchSlot.HeightRequest = fromHeight + ((toHeight - fromHeight) * v);
                SearchField.TranslationY = fromY + ((toY - fromY) * v);
                SearchField.Opacity = fromOpacity + ((toOpacity - fromOpacity) * v);
            },
            length: 220,
            easing: open ? Easing.CubicOut : Easing.CubicIn,
            finished: (_, cancelled) =>
            {
                if (!cancelled)
                {
                    Finish();
                }
            });
    }

}
