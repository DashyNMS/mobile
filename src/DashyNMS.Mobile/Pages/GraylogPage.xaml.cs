using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Shown via <see cref="Routes.Graylog"/>: from Logs for every device, or
/// from a device's Graylog section with its id and name.
/// </summary>
public partial class GraylogPage : ContentPage, IQueryAttributable
{
	private readonly GraylogViewModel _viewModel;

	public GraylogPage(GraylogViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		var deviceId = query.TryGetValue(Routes.DeviceIdParameter, out var id) && id is int value ? value : (int?)null;
		query.TryGetValue(Routes.DeviceNameParameter, out var name);
		_viewModel.Initialise(deviceId, name as string);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_ = _viewModel.EnsureLoadedAsync();
	}
}
