using DashyNMS.Mobile.Logs;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.Logs"/>, from the Alerts tab.</summary>
public partial class LogsPage : ContentPage
{
	private readonly LogsViewModel _viewModel;
	private readonly ToolbarItem _graylog;

	public LogsPage(LogsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_graylog = new ToolbarItem { Text = "Graylog", Command = viewModel.OpenGraylogCommand };
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_ = _viewModel.EnsureLoadedAsync();
		_ = ShowGraylogAsync();
	}

	/// <summary>Toolbar items can't be hidden by binding, so Graylog's is added only once it's set up.</summary>
	private async Task ShowGraylogAsync()
	{
		var show = await _viewModel.HasGraylogAsync();
		if (show && !ToolbarItems.Contains(_graylog))
		{
			ToolbarItems.Add(_graylog);
		}
		else if (!show)
		{
			ToolbarItems.Remove(_graylog);
		}
	}
}
