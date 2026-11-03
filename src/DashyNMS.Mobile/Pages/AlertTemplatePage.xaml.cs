using DashyNMS.Mobile.Rules;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Controls;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.AlertTemplate"/>: one alert template, read-only (#22).</summary>
public partial class AlertTemplatePage : ContentPage, IQueryAttributable
{
	private readonly AlertTemplateViewModel _viewModel;
    private IDisposable? _edges;
    private Window? _resumeWindow;

	public AlertTemplatePage(AlertTemplateViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
#if IOS
        Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.Page.SetUseSafeArea(this, false);
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
        Scroller.HandlerChanged += (_, _) => StopInsets();
        Scroller.Loaded += (_, _) => StopInsets();
#endif
		TopBar.SizeChanged += (_, _) => PlaceControls();
		Controls.PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading the template…", "Refreshing the template…");
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.TemplateIdParameter, out var id) && id is int templateId)
		{
			_viewModel.TemplateId = templateId;
			_viewModel.RefreshCommand.Execute(null);
		}
	}

	/// <summary>"‹ Device down" - back to the rule it was opened from.</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		TopBar.Apply(this);
        _edges ??= CoveredEdges.Watch(this, PlaceControls);
        if (!ReferenceEquals(_resumeWindow, Window))
        {
            if (_resumeWindow is not null) _resumeWindow.Resumed -= OnResumed;
            _resumeWindow = Window;
        }
        if (_resumeWindow is not null)
        {
            _resumeWindow.Resumed -= OnResumed;
            _resumeWindow.Resumed += OnResumed;
        }
#if IOS
        TopBar.IsVisible = !TabBarButtons.IsAvailable;
        TabBarButtons.Show(this, null, string.Empty,
            [new("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
                () => _ = Shell.Current.GoToAsync(".."), IsBack: true)]);
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Moved += OnGlassMoved;
#endif
        PlaceControls();
	}

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _edges?.Dispose();
        _edges = null;
        if (_resumeWindow is not null) _resumeWindow.Resumed -= OnResumed;
        _resumeWindow = null;
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Hide(this);
#endif
    }

    private void PlaceControls()
    {
#if IOS
        StopInsets();
        Root.SafeAreaEdges = ReadingColumn.SafeAreaEdges = ContentColumn.SafeAreaEdges =
            PullRefresh.SafeAreaEdges = Scroller.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);
#endif
        var edges = CoveredEdges.ForScrolling(this);
        ContentColumn.Padding = new Thickness(16 + edges.Left, 0, 16 + edges.Right, 24 + edges.Bottom);
        Fade.Margin = new Thickness(16 + edges.Left, TopBar.IsVisible ? Math.Max(0, TopBar.Height) + 12 : 0, 16 + edges.Right, 0);
    }

    private void OnResumed(object? sender, EventArgs e)
    {
#if IOS
        TabBarButtons.Show(this, null, string.Empty,
            [new("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
                () => _ = Shell.Current.GoToAsync(".."), IsBack: true)]);
#endif
        Dispatcher.Dispatch(PlaceControls);
    }
#if IOS
    private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);

    private void StopInsets()
    {
        if (Scroller.Handler?.PlatformView is UIKit.UIScrollView scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
    }
#endif
}
