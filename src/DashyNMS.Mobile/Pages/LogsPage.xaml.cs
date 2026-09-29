using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Logs;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.Logs"/>, from More or the Alerts page.</summary>
public partial class LogsPage : ContentPage
{
	private readonly LogsViewModel _viewModel;

	public LogsPage(LogsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		SearchReveal.Attach(List, Search);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
		_ = _viewModel.EnsureLoadedAsync();
	}
}
