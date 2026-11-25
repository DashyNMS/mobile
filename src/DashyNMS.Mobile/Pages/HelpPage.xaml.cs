using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Help;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Help (#668), shown via <see cref="Routes.Help"/>: the user guide's contents
/// and search, with the page beside the list where there's room.
/// </summary>
public partial class HelpPage : ContentPage, IDetailHost, ISearchHost, IActivePage
{
    private readonly HelpViewModel _viewModel;
    private readonly ButtonSearch _search;
    private readonly Stack<HelpTopic> _earlier = new();
    private HelpArticleView? _detail;
    private IDisposable? _edges;
    private bool _active;
#if IOS
    private LiveKeyboardBounds? _keyboard;
#endif

    public HelpPage(HelpViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _search = new ButtonSearch(SearchSlot, SearchField, Search, List);
        SearchButton.Command = new Command(OpenSearch);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(HelpViewModel.ResultsText) && _active && viewModel.IsSearching)
            {
                SemanticScreenReader.Default.Announce(viewModel.ResultsText);
            }
        };
        Body.SizeChanged += (_, _) => PlaceControls();
        SearchSlot.SizeChanged += (_, _) => PlaceControls();
        Split.SplitChanged += (_, _) =>
        {
            ShowHomeBeside();
            _viewModel.CurrentPageId = Split.IsSplit ? _detail?.ViewModel.Page?.Id : null;
            ShowGlass();
            PlaceControls();
        };
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
        Header.BackInGlass = TabBarButtons.IsAvailable;
        SearchButton.IsVisible = !TabBarButtons.IsAvailable;
        if (TabBarButtons.IsAvailable) Header.Actions = null;
        List.HandlerChanged += (_, _) => StopInsets();
        List.Loaded += (_, _) => StopInsets();
#endif
    }

    /// <summary>A page chosen from the list, or linked from the one beside it, opens in the pane.</summary>
    public bool TryShowDetail(string route, IDictionary<string, object>? parameters)
    {
        if (route != Routes.HelpArticle || parameters?.TryGetValue(Routes.HelpTopicParameter, out var value) != true || value is not HelpTopic topic)
        {
            return false;
        }

        if (!Split.IsSplit)
        {
            return false;
        }

        if (_detail?.ViewModel.Topic is { } current && current != topic)
        {
            _earlier.Push(current);
        }

        ShowBeside(topic);
        return true;
    }

    private bool ShowBeside(HelpTopic topic)
    {
        if (Handler?.MauiContext?.Services is not { } services)
        {
            return false;
        }

        _detail?.ViewModel.Disappearing();
        var view = new HelpArticleView(services.GetRequiredService<HelpArticleViewModel>(), showsTitle: true);
        view.ViewModel.Open(topic);
        view.ViewModel.Appearing();
        Split.Detail = _detail = view;
        _viewModel.CurrentPageId = view.ViewModel.Page?.Id;
        ShowGlass();
        PlaceControls();
        return true;
    }

    /// <summary>The front page to begin with, so the pane never opens empty.</summary>
    private void ShowHomeBeside()
    {
        if (!Split.IsSplit)
        {
            return;
        }

        // A page read on its own since: the pane follows it.
        if (Handler?.MauiContext?.Services.GetService<HelpHistory>()?.LastRead is { } read && read != _detail?.ViewModel.Topic && _detail is not null)
        {
            if (_detail.ViewModel.Topic is { } current)
            {
                _earlier.Push(current);
            }

            ShowBeside(read);
        }
        else if (_detail is null && _viewModel.Home is { } home)
        {
            ShowBeside(home);
        }
    }

    /// <summary>Back through the pages read in the pane, which the page's own Back leaves.</summary>
    private void ShowEarlier()
    {
        if (_active && _earlier.TryPop(out var topic))
        {
            ShowBeside(topic);
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Activate();
        _viewModel.Appearing();
        _detail?.ViewModel.Appearing();
        ShowHomeBeside();
    }

    /// <summary>Restores controls without changing the query or the retained page.</summary>
    public void Activate()
    {
        _active = true;
        Header.Apply(this);
        _edges ??= CoveredEdges.Watch(this, PlaceControls);
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Moved += OnGlassMoved;
        ManualKeyboardScroll.Acquire(this);
        _keyboard ??= new LiveKeyboardBounds(PlaceControls);
#endif
        ShowGlass();
        PlaceControls();
    }

    private void ShowGlass()
    {
#if IOS
        if (!_active || !ActivePage.IsCurrent(this) || !TabBarButtons.IsAvailable) return;
        List<GlassAction> actions =
        [
            new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => { if (_active) _ = Shell.Current.GoToAsync(".."); }, IsBack: true),
        ];
        if (Split.IsSplit && _earlier.Count > 0)
        {
            actions.Add(new("arrow.uturn.backward", "Previous help page", ShowEarlier));
        }

        TabBarButtons.Show(this, OpenSearch, "Search help", actions);
#endif
    }

    public void OpenSearch()
    {
        if (_active) _search.Open();
    }

    private void OnSearchCompleted(object? sender, EventArgs e) => _search.Close();

    protected override void OnDisappearing()
    {
        _active = false;
        _search.Close();
        _edges?.Dispose();
        _edges = null;
        _viewModel.Disappearing();
        _detail?.ViewModel.Disappearing();
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Hide(this);
        ManualKeyboardScroll.Release(this);
        _keyboard?.Dispose();
        _keyboard = null;
#endif
        base.OnDisappearing();
    }

    private void PlaceControls()
    {
        if (!_active || !ActivePage.IsCurrent(this)) return;
        var clear = CoveredEdges.ForScrolling(this);
        var left = 16 + clear.Left;
        var right = 16 + Math.Max(0, clear.Right - Math.Max(0, Width - Body.Width));
        Header.Margin = new Thickness(left, 0, right, 0);
        SearchField.Margin = new Thickness(left, 2, right, 0);
        var (footer, fade) = HelpGlass.Foot(this, clear);
#if IOS
        // A docked keyboard covers the foot itself, so the list stops above it instead.
        var keyboard = Body.Handler?.PlatformView is UIKit.UIView host
            ? SectionListPlacement.KeyboardViewportMargin(host.Bounds.Height, _keyboard?.TopIn(host)) : 0;
        ListCard.Margin = new Thickness(left, 0, right, keyboard);
        ListFoot.HeightRequest = keyboard > 0 ? SectionListPlacement.FooterGap : footer;
        BottomFade.SetCoveredHeight(keyboard > 0 ? 0 : fade);
#else
        ListCard.Margin = new Thickness(left, 0, right, 12 + clear.Bottom);
#endif
        if (Split.IsSplit)
        {
            _detail?.PlaceContent(new Thickness(0, 0, clear.Right, 0), footer, fade);
        }
    }

#if IOS
    private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);

    private void StopInsets()
    {
        static UIKit.UIScrollView? Find(UIKit.UIView view) =>
            view as UIKit.UIScrollView ?? view.Subviews.Select(Find).FirstOrDefault(found => found is not null);
        if (List.Handler?.PlatformView is UIKit.UIView view && Find(view) is { } scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
    }
#endif
}
