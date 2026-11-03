using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

public partial class HealthPage : ContentPage, ISearchHost
{
    private readonly HealthViewModel _viewModel;
    private IDisposable? _edges;
    private readonly ButtonSearch _search;

    public HealthPage(HealthViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading sensors…", "Refreshing sensors…");
        _search = new ButtonSearch(SearchSlot, SearchField, Search, List);
        SearchButton.Command = new Command(OpenSearch);
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
        Header.BackInGlass = TabBarButtons.IsAvailable;
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
        List<GlassAction> actions = [];
        if (PageHeader.IsPushed(this))
            actions.Add(new("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
                () => _ = Shell.Current.GoToAsync(".."), IsBack: true));
        actions.Add(new("arrow.clockwise", "Refresh sensors", () => _viewModel.RefreshCommand.Execute(null)));
        TabBarButtons.Show(this, OpenSearch, "Search sensors", actions);
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Moved += OnGlassMoved;
#endif
        PlaceControls();
        _ = _viewModel.RefreshIfStaleAsync();
    }

    public void OpenSearch() => _search.Open();

    private void OnSearchCompleted(object? sender, EventArgs e) => _search.Close();

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _search.Close();
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
}
