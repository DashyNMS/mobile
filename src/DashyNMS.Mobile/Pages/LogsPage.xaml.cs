using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Logs;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Shown via <see cref="Routes.Logs"/>: from More or the Alerts page for every
/// device, or from a device's Event log section with its id and name - the
/// same page, with the Device chip fixed to it (#118, #125).
/// </summary>
public partial class LogsPage : ContentPage, IQueryAttributable, IDetailHost
{
	private readonly LogsViewModel _viewModel;

	public LogsPage(LogsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading the log…", "Refreshing the log…");
		SearchReveal.Attach(List, SearchSlot, Search);
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		var deviceId = query.TryGetValue(Routes.DeviceIdParameter, out var id) && id is int value ? value : (int?)null;
		query.TryGetValue(Routes.DeviceNameParameter, out var name);
		_viewModel.Initialise(deviceId, name as string);
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
		return true;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
		_ = _viewModel.EnsureLoadedAsync();
	}
}
