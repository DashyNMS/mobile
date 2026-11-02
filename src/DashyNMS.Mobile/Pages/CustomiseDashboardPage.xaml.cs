using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.CustomiseDashboard"/>, from the Dashboard's pencil.</summary>
public partial class CustomiseDashboardPage : ContentPage
{
	private readonly CustomiseDashboardViewModel _viewModel;

	public CustomiseDashboardPage(CustomiseDashboardViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
#if IOS
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
        if (TabBarButtons.IsAvailable) TopBar.IsVisible = false;
#endif
        Controls.TopBar.Fit(Heading);
        CoveredEdges.Watch(this, PlaceControls);
        List.SizeChanged += (_, _) => PlaceColumns();
	}

	/// <summary>Back from a card's set-up or the card picker: the cards may have changed (#87, #140).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		TopBar.Apply(this);
		_viewModel.Attach();
#if IOS
        TabBarButtons.Show(this, null, string.Empty,
        [
            new GlassAction("chevron.backward", "Back to " + BackBar.PreviousTitle(this),
                () => _ = Shell.Current.GoToAsync(".."), IsBack: true),
        ]);
        TabBarButtons.Moved += OnGlassMoved;
#endif
        PlaceControls();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.Detach();
#if IOS
        TabBarButtons.Moved -= OnGlassMoved;
        TabBarButtons.Hide(this);
#endif
	}

    /// <summary>Pad the list's footer so its final action scrolls clear without shortening the viewport.</summary>
    private void PlaceControls()
    {
        var edges = CoveredEdges.ForScrolling(this);
        Heading.Margin = TopBar.Margin = new Thickness(16 + edges.Left, 0, 16 + edges.Right, 0);
        List.Margin = new Thickness(16 + edges.Left, 0, 16 + edges.Right, 0);
        Footer.Padding = new Thickness(0, 12, 0, 16 + edges.Bottom);
        double foot = 16 + edges.Bottom;
#if IOS
        if (Handler?.PlatformView is UIKit.UIView page && TabBarButtons.PillIn(page) is { } pill && pill.Height > pill.Width)
            foot = Math.Max(0, page.Bounds.Height - pill.Bottom);
#endif
        Toast.Margin = new Thickness(12 + edges.Left, 0, 12 + edges.Right, foot);
        PlaceColumns();
    }

    private void PlaceColumns()
    {
        if (List.Width <= 0) return;
        var columns = DashboardColumns.Count(List.Width);
        if (CardLayout.Span != columns) CardLayout.Span = columns;
    }

#if IOS
    private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);
#endif

	private void OnReorderCompleted(object? sender, EventArgs e) => _viewModel.SaveOrderCommand.Execute(null);
}
