using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;

namespace DashyNMS.Mobile.Pages;

/// <summary>Saved definitions; editor return reloads settings without changing their schema.</summary>
public partial class NeighbourGroupsPage : ContentPage, IActivePage
{
    private readonly NeighbourGroupsViewModel _viewModel;
    private IDisposable? _edges;
    private bool _active;

    public NeighbourGroupsPage(NeighbourGroupsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        PullToRefresh.HideSpinner(PullRefresh);
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
        Header.BackInGlass = TabBarButtons.IsAvailable;
        AddButton.IsVisible = !TabBarButtons.IsAvailable;
        List.HandlerChanged += (_, _) => StopInsets();
        List.Loaded += (_, _) => StopInsets();
#endif
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Activate();
        _viewModel.Refresh();
        _ = _viewModel.LoadCountsAsync();
    }

    public void Activate()
    {
        _active = true;
        Header.Apply(this);
        _edges ??= CoveredEdges.Watch(this, PlaceControls);
#if IOS
        List<GlassAction> actions = [];
        if (PageHeader.IsPushed(this))
            actions.Add(new("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
                () => _ = Shell.Current.GoToAsync(".."), IsBack: true));
        actions.Add(new("plus", "Add a neighbourhood", () => _viewModel.AddCommand.Execute(null)));
        TabBarButtons.Show(this, null, string.Empty, actions);
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Moved += OnGlassMoved;
#endif
        PlaceControls();
    }

    protected override void OnDisappearing()
    {
        _active = false;
        base.OnDisappearing();
        _edges?.Dispose();
        _edges = null;
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Hide(this);
#endif
    }

    private void PlaceControls()
    {
        // Native safe-area and glass callbacks can already be queued on departure.
        if (!_active || Shell.Current?.CurrentPage != this) return;

        var clear = CoveredEdges.ForScrolling(this);
        Body.Padding = new Thickness(16 + clear.Left, 0, 16 + clear.Right, 0);
        ListFoot.HeightRequest = 12 + clear.Bottom;
    }

    private async void OnMore(object? sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: NeighbourGroupRow row }) return;
        var index = _viewModel.Groups.ToList().FindIndex(item => item.Group.Id == row.Group.Id);
        if (index < 0) return;
        var options = new List<string>();
        if (index > 0) options.Add("Move earlier");
        if (index < _viewModel.Groups.Count - 1) options.Add("Move later");
        options.Add("Delete neighbourhood");
        var choice = await DisplayActionSheetAsync("Actions for " + row.Name, "Cancel", null, options.ToArray());
        if (choice == "Move earlier") _viewModel.MoveUpCommand.Execute(row);
        else if (choice == "Move later") _viewModel.MoveDownCommand.Execute(row);
        else if (choice == "Delete neighbourhood") await _viewModel.DeleteCommand.ExecuteAsync(row);
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
