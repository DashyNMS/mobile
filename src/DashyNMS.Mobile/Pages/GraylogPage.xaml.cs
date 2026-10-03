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
public partial class GraylogPage : ContentPage, IQueryAttributable, IDetailHost
{
	private readonly GraylogViewModel _viewModel;

	public GraylogPage(GraylogViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading messages…", "Refreshing messages…");
		SearchReveal.Attach(List, SearchSlot, Search);
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		var deviceId = query.TryGetValue(Routes.DeviceIdParameter, out var id) && id is int value ? value : (int?)null;
		query.TryGetValue(Routes.DeviceNameParameter, out var name);
		_viewModel.Initialise(deviceId, name as string);
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
		Split.Detail = view;
		return true;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
		_ = _viewModel.EnsureLoadedAsync();
	}
}
