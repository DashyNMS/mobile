using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Logs;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Shown via <see cref="Routes.Logs"/>: from More or the dashboard for every
/// device, or from a device's Event log section with its id and name - the
/// same page, with the Device chip fixed to it (#118, #125).
/// </summary>
public partial class LogsPage : ContentPage, IQueryAttributable, IDetailHost, ISearchHost, IActivePage
{
	private readonly LogsViewModel _viewModel;
	private readonly TabAlignedListLayout _listLayout;
	private readonly ButtonSearch _search;
	private bool _active;

	public LogsPage(LogsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_listLayout = new TabAlignedListLayout(this, Split, ListColumn, ListCard, List, ListFoot, clear =>
		{
			if (Split.Detail is LogEntryView detail) detail.PlaceContent(clear);
		}, PlaceGutters);
		PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading the log…", "Refreshing the log…");
		_search = new ButtonSearch(SearchSlot, SearchField, Search, List);
		SearchButton.Command = new Command(OpenSearch);
#if IOS
		Header.BackInGlass = TabBarButtons.IsAvailable;
		SearchButton.IsVisible = !TabBarButtons.IsAvailable;
#endif
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		var deviceId = query.TryGetValue(Routes.DeviceIdParameter, out var id) && id is int value ? value : (int?)null;
		query.TryGetValue(Routes.DeviceNameParameter, out var name);
		_viewModel.Initialise(deviceId, name as string);

		// An Event log card's See all: its type.
		if (query.TryGetValue(Routes.LogTypeParameter, out var type))
		{
			_viewModel.ShowEventType(type as string);
		}
	}

	/// <summary>An entry tapped with room beside the list (#88) opens there.</summary>
	public bool TryShowDetail(string route, IDictionary<string, object>? parameters)
	{
		if (!Split.IsSplit || route != Routes.LogEntry || Handler?.MauiContext?.Services is not { } services)
		{
			return false;
		}

		var view = new LogEntryView(services.GetRequiredService<LogEntryViewModel>());
		view.Bar.ShowsBack = false;
		view.ViewModel.IsBesideList = true;
		view.Load(parameters);
		Split.Detail = view;
#if IOS
		PlaceFooter();
#endif
		return true;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Activate();
		_ = _viewModel.EnsureLoadedAsync();
	}

	/// <summary>Restores retained controls without fetching or resetting the source.</summary>
	public void Activate()
	{
		_active = true;
		Header.Apply(this);
#if IOS
		if (TabBarButtons.IsAvailable)
		{
			TabBarButtons.Show(this, OpenSearch, _viewModel.SearchPlaceholder,
			[
				new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => _ = Shell.Current.GoToAsync(".."), IsBack: true),
			]);
		}
		TabBarButtons.Moved -= OnGlassMoved;
		TabBarButtons.Moved += OnGlassMoved;
		PlaceFooter();
#endif
		_viewModel.AutoRefresh.SetShowing(true);
	}

	/// <summary>Auto-refresh only ticks while the page is showing (#214).</summary>
	protected override void OnDisappearing()
	{
		_active = false;
		base.OnDisappearing();
		CloseSearch();
#if IOS
		TabBarButtons.Moved -= OnGlassMoved;
		TabBarButtons.Hide(this);
#endif
		_viewModel.AutoRefresh.SetShowing(false);
	}

	public void OpenSearch() { if (_active) _search.Open(); }
	private void CloseSearch() => _search.Close();
	private void OnSearchCompleted(object? sender, EventArgs e) => CloseSearch();

    /// <summary>Keeps the filter clip under the camera while resting controls own safe gutters.</summary>
    private void PlaceGutters(Thickness gutters)
    {
        var left = gutters.Left;
        var right = gutters.Right;
        Header.Margin = new Thickness(left, 0, right, 0);
        LogSwitch.Margin = new Thickness(left, 0, right, 0);
        SearchField.Margin = new Thickness(left, 4, right, 8);
        FilterStrip.Padding = new Thickness(left, 0, right, 0);
        Summary.Margin = new Thickness(left + 4, 6, right, 4);
        ErrorCard.Margin = new Thickness(left, 0, right, 8);
        Busy.Margin = new Thickness(left, 0, right, 0);
        ListCard.Margin = new Thickness(left, 0, right, ListCard.Margin.Bottom);
    }

#if IOS
	private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceFooter);
	private void PlaceFooter()
	{
		if (!_active || Shell.Current?.CurrentPage != this) return;
		_listLayout.Place();
	}
#endif
}
