using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.GraylogDevice"/>, with the list whose Device chip it sets - Graylog's (#117) or Logs' (#118).</summary>
public partial class GraylogDevicePickerPage : ContentPage, IQueryAttributable, ISearchHost, IActivePage
{
	private readonly GraylogDevicePickerViewModel _viewModel;
	private readonly SetupPageLayout _placement;

	public GraylogDevicePickerPage(GraylogDevicePickerViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		Setup.OverlayPrimaryAction = true;
		_placement = new SetupPageLayout(this, Setup, Header, DeviceList) { FullHeight = true };
	}

	public void Activate() => _placement.Activate();

	public void OpenSearch() => SearchEntry.Focus();

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.GraylogListParameter, out var list) && list is IDeviceChipList chips)
		{
			_ = _viewModel.LoadAsync(chips);
		}
	}

}
