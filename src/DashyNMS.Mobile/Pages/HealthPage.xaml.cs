using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.ViewModels;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

public partial class HealthPage : ContentPage, ISearchHost, IActivePage
{
    public static readonly BindableProperty ShowsHealthColumnsProperty = BindableProperty.Create(
        nameof(ShowsHealthColumns), typeof(bool), typeof(HealthPage), false);

    public bool ShowsHealthColumns
    {
        get => (bool)GetValue(ShowsHealthColumnsProperty);
        private set => SetValue(ShowsHealthColumnsProperty, value);
    }

    private readonly HealthViewModel _viewModel;
    private IDisposable? _edges;
    private bool _active;
    private readonly ButtonSearch _search;

    /// <summary>The glass Refresh button's refresh: shows the busy card, as a pull does.</summary>
    private readonly Action _refresh;
#if IOS
    private PageActionPlace? _actionPlace;
    private Foundation.NSObject? _textSizeObserver;
    private int _firstVisibleSensor;
#endif

    public HealthPage(HealthViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _refresh = PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading sensors…", "Refreshing sensors…");
        _search = new ButtonSearch(SearchSlot, SearchField, Search, List);
        SearchButton.Command = new Command(OpenSearch);
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
        Header.BackInGlass = TabBarButtons.IsAvailable;
        SearchButton.IsVisible = !TabBarButtons.IsAvailable;
        List.HandlerChanged += (_, _) => StopInsets();
        List.Loaded += (_, _) => StopInsets();
        List.SizeChanged += (_, _) => UpdateReadingColumns();
        List.Scrolled += (_, args) => _firstVisibleSensor = args.FirstVisibleItemIndex;
#endif
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Activate();
        _ = _viewModel.RefreshIfStaleAsync();
    }

    /// <summary>Reclaims retained-page controls without reloading readings or resetting filters.</summary>
    public void Activate()
    {
        _active = true;
        Header.Apply(this);
        _edges ??= CoveredEdges.Watch(this, PlaceControls);
#if IOS
        _actionPlace = null;
        SyncActions();
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Moved += OnGlassMoved;
        _textSizeObserver ??= UIKit.UIApplication.Notifications.ObserveContentSizeCategoryChanged((_, _) =>
            Dispatcher.Dispatch(UpdateReadingColumns));
        UpdateReadingColumns();
#endif
        PlaceControls();
    }

#if IOS
    private void SyncActions()
    {
        if (!_active || !TabBarButtons.IsAvailable) return;
        var place = Header.ResolveActionPlacement(this, 1);
        if (_actionPlace == place) return;
        _actionPlace = place;
        List<GlassAction> bar = [];
        if (PageHeader.IsPushed(this))
            bar.Add(new("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
                () => _ = Shell.Current.GoToAsync(".."), IsBack: true));
        var refresh = new GlassAction("arrow.clockwise", "Refresh sensors", _refresh);
        if (place == PageActionPlace.HeaderGlass)
        {
            Header.SetHeaderGlass([refresh]);
        }
        else
        {
            Header.SetHeaderGlass([]);
            bar.Add(refresh);
        }
        TabBarButtons.Show(this, OpenSearch, "Search sensors", bar);
    }
#endif

    public void OpenSearch() => _search.Open();

    private void OnSearchCompleted(object? sender, EventArgs e) => _search.Close();

    private async void OnSensorClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: SectionRow row } && _viewModel.OpenSensorCommand.CanExecute(row))
            await _viewModel.OpenSensorCommand.ExecuteAsync(row);
    }

    protected override void OnDisappearing()
    {
        _active = false;
        base.OnDisappearing();
        _search.Close();
        _edges?.Dispose();
        _edges = null;
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Hide(this);
        _textSizeObserver?.Dispose();
        _textSizeObserver = null;
#endif
    }

    private void PlaceControls()
    {
        // Native safe-area and glass callbacks may already be queued on departure.
        if (!_active || !ActivePage.IsCurrent(this)) return;

        var clear = CoveredEdges.ForScrolling(this);
        Body.Padding = new Thickness(16 + clear.Left, 0, 16 + clear.Right, 0);
#if IOS
        var sideBar = false;
        var obstruction = 12 + clear.Bottom;
        if (Handler?.PlatformView is UIKit.UIView page && page.Bounds.Height > 0)
        {
            var nativePill = TabBarButtons.PillIn(page);
            sideBar = nativePill is { } p && p.Height > p.Width;
            OccupiedArea? pill = nativePill is { } bar ? new(bar.Left, bar.Top, bar.Right, bar.Bottom) : null;
            var glass = TabBarButtons.Occupied(page)
                .Select(r => new OccupiedArea(r.Left, r.Top, r.Right, r.Bottom)).ToArray();
            obstruction = SetupActionPlacement.BottomGap(page.Bounds.Height,
                CoveredEdges.Of(this).Bottom + (pill is null ? TabBarButtons.CoveredBottom : 0), pill, glass);
        }
        ListCard.Margin = Thickness.Zero;
        ListFoot.HeightRequest = SectionListPlacement.Resolve(obstruction).Footer;
        BottomFade.SetCoveredHeight(!sideBar ? clear.Bottom : 0);
        SyncActions();
#else
        ListCard.Margin = new Thickness(0, 0, 0, 12 + clear.Bottom);
        ListFoot.HeightRequest = 0;
#endif
    }

#if IOS
    private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);

    /// <summary>Reflows the same cells at a measured list width that accommodates the current text size.</summary>
    private void UpdateReadingColumns()
    {
        if (!_active || List.Width <= 0) return;

        using var font = UIKit.UIFont.PreferredBody;
        // Keep space for a device name, a value with its unit, and a legible limit.
        const double minimumWidth = 600;
        var show = List.Width >= minimumWidth * Math.Max(1, (font?.PointSize ?? 17) / 17d);
        if (show == ShowsHealthColumns) return;

        var index = _firstVisibleSensor;
        var anchor = index > 0 && index < _viewModel.Sensors.Count ? _viewModel.Sensors[index] : null;
        ShowsHealthColumns = show;
        if (anchor is null) return;

        // The item source stays in place. Restore the first visible row after cells remeasure.
        Dispatcher.Dispatch(() =>
        {
            if (_active && show == ShowsHealthColumns && index < _viewModel.Sensors.Count
                && ReferenceEquals(_viewModel.Sensors[index], anchor))
                List.ScrollTo(index, position: ScrollToPosition.Start, animate: false);
        });
    }

    private void StopInsets()
    {
        if (List.Handler?.PlatformView is UIKit.UIView view && FindScroll(view) is { } scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
    }

    private static UIKit.UIScrollView? FindScroll(UIKit.UIView view) =>
        view as UIKit.UIScrollView ?? view.Subviews.Select(FindScroll).FirstOrDefault(found => found is not null);
#endif
}
