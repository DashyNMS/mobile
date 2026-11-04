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
	private const uint SearchSlide = 220;
	private bool _searchOpen;
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
		SearchButton.Command = new Command(() =>
		{
			if (_searchOpen)
			{
				_viewModel.SearchText = string.Empty;
				CloseSearch();
			}
			else OpenSearch();
		});
		Search.Unfocused += (_, _) => CloseSearch();
		Split.SplitChanged += (_, _) => UpdateGlass();
		List.Scrolled += (_, _) =>
		{
			if (Search.IsFocused) CloseSearch();
		};
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
		_ = _viewModel.EnsureLoadedAsync();
		_viewModel.AutoRefresh.SetShowing(true);
	}

	/// <summary>Reclaims retained host controls without fetching or resetting list state.</summary>
	public void Activate()
	{
		_showing = true;
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
		if (_searchOpen) { Search.Focus(); return; }
		_searchOpen = true;
		SlideSearch(true);
	}

	private void CloseSearch()
	{
		if (!_searchOpen) return;
		_searchOpen = false;
		SlideSearch(false);
	}

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
			length: SearchSlide,
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
