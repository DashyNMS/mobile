using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.GraylogDevice"/>, with the Graylog list whose Device chip it sets (#117).</summary>
public partial class GraylogDevicePickerPage : ContentPage, IQueryAttributable
{
	private readonly GraylogDevicePickerViewModel _viewModel;

	public GraylogDevicePickerPage(GraylogDevicePickerViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.GraylogListParameter, out var list) && list is GraylogViewModel graylog)
		{
			_ = _viewModel.LoadAsync(graylog);
		}
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		TopBar.Apply(this);
	}
}
