using DashyNMS.Mobile.Logs;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.Logs"/>, from the Alerts tab.</summary>
public partial class LogsPage : ContentPage
{
	private readonly LogsViewModel _viewModel;

	public LogsPage(LogsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_ = _viewModel.EnsureLoadedAsync();
	}
}
