using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Help;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Views;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// One page of the user guide (#668), pushed from Help, from a link in
/// another page or from the part of the app it explains. Back returns there.
/// </summary>
public sealed class HelpArticlePage : ContentPage, IQueryAttributable, IActivePage
{
    private readonly PageHeader _header = new() { Title = "Help", Margin = new Thickness(16, 0) };
    private readonly HelpArticleView _view;
    private IDisposable? _edges;
    private bool _showing;

    public HelpArticlePage(HelpArticleViewModel viewModel)
    {
        BindingContext = viewModel;
        Title = "Help";
        _view = new HelpArticleView(viewModel, showsTitle: false);
        var root = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)] };
        root.Add(_header, 0, 0);
        root.Add(_view, 0, 1);
        Content = root;
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
        root.SafeAreaEdges = SafeAreaEdges.None;
        _header.BackInGlass = HelpGlass.Available;
#else
        SafeAreaEdges = SafeAreaEdges.All;
#endif
        viewModel.Shown += (_, shown) => Title = _header.Title = shown.Page.Title;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(Routes.HelpTopicParameter, out var value) && value is HelpTopic topic)
        {
            _view.ViewModel.Open(topic);
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Activate();
        _view.ViewModel.Appearing();
    }

    /// <summary>Restores retained page ownership through AppShell's activation hook.</summary>
    public void Activate()
    {
        _showing = true;
        _edges ??= CoveredEdges.Watch(this, Place);
#if IOS
        _header.BackInGlass = HelpGlass.Available;
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Moved += OnGlassMoved;
        if (HelpGlass.Available)
        {
            TabBarButtons.Show(this, null, string.Empty,
            [
                new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => { if (_showing) _ = Shell.Current.GoToAsync(".."); }, IsBack: true),
            ]);
        }
#endif
        _header.Apply(this);
        Place();
    }

    protected override void OnDisappearing()
    {
        _showing = false;
        _edges?.Dispose();
        _edges = null;
        _view.ViewModel.Disappearing();
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Hide(this);
#endif
        base.OnDisappearing();
    }

    private void Place()
    {
        if (!_showing || !ActivePage.IsCurrent(this)) return;
        var clear = CoveredEdges.ForScrolling(this);
        // Over the readable column, which is centred once the page is wider than it.
        var gutter = 16 + Math.Max(0, (Width - ScreenLayout.ReadableWidth) / 2);
        _header.Margin = new Thickness(gutter + clear.Left, 0, 16 + clear.Right, 0);
        var (footer, fade) = HelpGlass.Foot(this, clear);
        _view.PlaceContent(new Thickness(clear.Left, 0, clear.Right, 0), footer, fade);
    }

#if IOS
    private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(Place);
#endif
}

/// <summary>What both help pages measure alike. Before sign-in there's no tab bar, so no glass.</summary>
internal static class HelpGlass
{
#if IOS
    public static bool Available => TabBarButtons.IsAvailable && Shell.Current?.CurrentItem is TabBar;
#endif

    /// <summary>The scrolling footer that clears the foot, and how much of it the fade covers.</summary>
    public static (double Footer, double Fade) Foot(ContentPage page, Thickness clear)
    {
#if IOS
        if (page.Handler?.PlatformView is UIKit.UIView view && view.Bounds.Height > 0)
        {
            var nativePill = TabBarButtons.PillIn(view);
            var sideBar = nativePill is { } p && p.Height > p.Width;
            OccupiedArea? pill = nativePill is { } bar ? new(bar.Left, bar.Top, bar.Right, bar.Bottom) : null;
            var glass = TabBarButtons.Occupied(view).Select(r => new OccupiedArea(r.Left, r.Top, r.Right, r.Bottom)).ToArray();
            var obstruction = SetupActionPlacement.BottomGap(view.Bounds.Height,
                CoveredEdges.Of(page).Bottom + (pill is null ? TabBarButtons.CoveredBottom : 0), pill, glass);
            return (SectionListPlacement.Resolve(obstruction).Footer, nativePill is null || sideBar ? 0 : clear.Bottom);
        }
#endif
        return (24 + clear.Bottom, 0);
    }
}
