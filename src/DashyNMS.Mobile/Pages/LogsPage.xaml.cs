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
public partial class LogsPage : ContentPage, IQueryAttributable, IDetailHost, ISearchHost
{
	private readonly LogsViewModel _viewModel;
	private readonly TabAlignedListLayout _listLayout;
	private const uint SearchSlide = 220;
	private bool _searchOpen;

	public LogsPage(LogsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_listLayout = new TabAlignedListLayout(this, Split, ListColumn, ListCard, List, ListFoot, clear =>
		{
			if (Split.Detail is LogEntryView detail) detail.PlaceContent(clear);
		});
		PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading the log…", "Refreshing the log…");
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
		List.Scrolled += (_, _) =>
		{
			if (Search.IsFocused) CloseSearch();
		};
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
		Header.Apply(this);
#if IOS
		if (TabBarButtons.IsAvailable)
		{
			TabBarButtons.Show(this, OpenSearch, _viewModel.SearchPlaceholder,
			[
				new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => _ = Shell.Current.GoToAsync(".."), IsBack: true),
			]);
		}
		TabBarButtons.Moved += OnGlassMoved;
		PlaceFooter();
#endif
		_ = _viewModel.EnsureLoadedAsync();
		_viewModel.AutoRefresh.SetShowing(true);
	}

	/// <summary>Auto-refresh only ticks while the page is showing (#214).</summary>
	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		CloseSearch();
#if IOS
		TabBarButtons.Moved -= OnGlassMoved;
		TabBarButtons.Hide(this);
#endif
		_viewModel.AutoRefresh.SetShowing(false);
	}

	public void OpenSearch()
	{
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

	private void OnSearchCompleted(object? sender, EventArgs e) => CloseSearch();

#if IOS
	private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceFooter);
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
