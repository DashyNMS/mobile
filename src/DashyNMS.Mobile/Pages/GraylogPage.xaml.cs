using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Shown via <see cref="Routes.Graylog"/>: from Logs or the tab bar for every
/// device, or from a device's Graylog section with its id and name - the
/// same page, with the Device chip fixed to it (#117).
/// </summary>
public partial class GraylogPage : ContentPage, IQueryAttributable, IDetailHost, ISearchHost, IActivePage
{
	private readonly GraylogViewModel _viewModel;
	private readonly TabAlignedListLayout _listLayout;
	private readonly ButtonSearch _search;
	private bool _showing;
#if IOS
	private GraylogMessageViewModel? _observedDetail;
	private (GraylogMessageViewModel? Detail, bool Search, bool Back, string BackText, bool Filter, bool Device, string FilterText, string DeviceText)? _glassState;
#endif

	public GraylogPage(GraylogViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_listLayout = new TabAlignedListLayout(this, Split, ListColumn, ListCard, List, ListFoot, clear =>
		{
			if (Split.Detail is GraylogMessageView detail) detail.PlaceContent(clear);
		});
		PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading messages…", "Refreshing messages…");
		_search = new ButtonSearch(SearchSlot, SearchField, Search, List);
		SearchButton.Command = new Command(OpenSearch);
		Split.SplitChanged += (_, _) => UpdateGlass();
#if IOS
		Header.BackInGlass = TabBarButtons.IsAvailable;
		if (TabBarButtons.IsAvailable) SearchButton.IsVisible = false;
#endif
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		var deviceId = query.TryGetValue(Routes.DeviceIdParameter, out var id) && id is int value ? value : (int?)null;
		query.TryGetValue(Routes.DeviceNameParameter, out var name);
		_viewModel.Initialise(deviceId, name as string);

		// A Graylog card's See all: its stream and time range.
		if (query.TryGetValue(Routes.GraylogRangeParameter, out var range) && range is int seconds)
		{
			query.TryGetValue(Routes.GraylogStreamParameter, out var stream);
			_viewModel.ShowFeed(stream as string, seconds);
		}
	}

	/// <summary>A message tapped with room beside the list (#88) opens there.</summary>
	public bool TryShowDetail(string route, IDictionary<string, object>? parameters)
	{
		if (!Split.IsSplit || route != Routes.GraylogMessage || Handler?.MauiContext?.Services is not { } services)
		{
			return false;
		}

		var view = new GraylogMessageView(services.GetRequiredService<GraylogMessageViewModel>());
		view.Bar.ShowsBack = false;
		view.ViewModel.IsBesideList = true;
		view.Load(parameters);
#if IOS
		ObserveDetail(null);
#endif
		Split.Detail = view;
		UpdateGlass();
#if IOS
		PlaceFooter();
#endif
		return true;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Activate();
		_viewModel.AutoRefresh.SetShowing(true);
	}

	/// <summary>Reclaims controls and reloads only an initial or changed connection.</summary>
	public void Activate()
	{
		_showing = true;
		_ = _viewModel.EnsureLoadedAsync();
		Header.Apply(this);
#if IOS
		_glassState = null;
		_viewModel.PropertyChanged -= OnHostChanged;
		_viewModel.PropertyChanged += OnHostChanged;
		TabBarButtons.Moved -= OnGlassMoved;
		TabBarButtons.Moved += OnGlassMoved;
		UpdateGlass();
		PlaceFooter();
#endif
	}

	/// <summary>Auto-refresh only ticks while the page is showing (#214).</summary>
	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_showing = false;
		CloseSearch();
#if IOS
		ObserveDetail(null);
		_viewModel.PropertyChanged -= OnHostChanged;
		TabBarButtons.Moved -= OnGlassMoved;
		TabBarButtons.Hide(this);
#endif
		_viewModel.AutoRefresh.SetShowing(false);
	}
	public void OpenSearch()
	{
		if (!_viewModel.IsConfigured) return;
		_search.Open();
	}

	/// <summary>Constrain native choices to the measured strip so long labels wrap.</summary>
	private void OnFilterStripSizeChanged(object? sender, EventArgs e)
	{
		var width = FilterStrip.Width;
		if (!double.IsFinite(width) || width <= 0) return;
		foreach (var child in FilterStrip.Children.OfType<View>())
		{
			var available = width - child.Margin.HorizontalThickness;
			if (double.IsFinite(available) && available > 0 && child.MaximumWidthRequest != available)
				child.MaximumWidthRequest = available;
		}
	}

	private void CloseSearch() => _search.Close();

	private void OnSearchCompleted(object? sender, EventArgs e)
	{
		_viewModel.SearchCommand.Execute(null);
		CloseSearch();
	}

#if IOS
	private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(() => { if (IsActive) PlaceFooter(); });
	private void OnHostChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => QueueGlass();
	private void OnDetailChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => QueueGlass();
	private void OnDetailCommandChanged(object? sender, EventArgs e) => QueueGlass();
	private void QueueGlass() => Dispatcher.Dispatch(() => { if (IsActive) UpdateGlass(); });

	/// <summary>Only this host observes its current detail; replaced/hidden hosts release subscriptions.</summary>
	private void ObserveDetail(GraylogMessageViewModel? detail)
	{
		if (ReferenceEquals(_observedDetail, detail)) return;
		if (_observedDetail is { } old)
		{
			old.PropertyChanged -= OnDetailChanged;
			old.OpenDeviceCommand.CanExecuteChanged -= OnDetailCommandChanged;
			old.ShowOnlyCommand.CanExecuteChanged -= OnDetailCommandChanged;
		}
		_observedDetail = detail;
		if (detail is null) return;
		detail.PropertyChanged += OnDetailChanged;
		detail.OpenDeviceCommand.CanExecuteChanged += OnDetailCommandChanged;
		detail.ShowOnlyCommand.CanExecuteChanged += OnDetailCommandChanged;
	}
#endif

	private bool IsActive => _showing && Shell.Current?.CurrentPage == this;

	/// <summary>Detail actions join the existing list owner, never claim child Back or search.</summary>
	private void UpdateGlass()
	{
#if IOS
		if (!IsActive || !TabBarButtons.IsAvailable) return;
		var view = Split.IsSplit ? Split.Detail as GraylogMessageView : null;
		var detail = view?.ViewModel;
		ObserveDetail(detail);
		view?.HideInlineActions();
		var state = (detail, _viewModel.IsConfigured, PageHeader.IsPushed(this), BackBar.PreviousTitle(this),
			detail?.ShowOnlyCommand.CanExecute(null) == true, detail?.OpenDeviceCommand.CanExecute(null) == true,
			detail?.ShowOnlyText ?? string.Empty, detail?.DeviceActionDescription ?? string.Empty);
		if (_glassState == state) return;
		_glassState = state;
		List<GlassAction> actions = [];
		if (state.Item3)
			actions.Add(new("chevron.backward", "Back to " + state.Item4, () => { if (IsActive) _ = Shell.Current.GoToAsync(".."); }, IsBack: true));
		if (state.Item5)
			actions.Add(new("line.3.horizontal.decrease", state.Item7, () =>
			{
				if (IsCurrentDetail(detail) && detail!.ShowOnlyCommand.CanExecute(null)) detail.ShowOnlyCommand.Execute(null);
			}));
		if (state.Item6)
			actions.Add(new("desktopcomputer", state.Item8, () =>
			{
				if (IsCurrentDetail(detail) && detail!.OpenDeviceCommand.CanExecute(null)) detail.OpenDeviceCommand.Execute(null);
			}));
		TabBarButtons.Show(this, state.Item2 ? () => { if (IsActive) OpenSearch(); } : null, "Search messages", actions);
#endif
	}

#if IOS
	private bool IsCurrentDetail(GraylogMessageViewModel? detail) => IsActive && Split.IsSplit
		&& detail is not null && ReferenceEquals((Split.Detail as GraylogMessageView)?.ViewModel, detail);

	private void PlaceFooter()
	{
		_listLayout.Place();
	}
#endif

}
