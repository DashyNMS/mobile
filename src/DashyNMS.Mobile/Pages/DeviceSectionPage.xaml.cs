using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.DeviceSection"/>, with the device id, section and name.</summary>
public partial class DeviceSectionPage : ContentPage, IQueryAttributable
{
	private readonly DeviceSectionViewModel _viewModel;

	public DeviceSectionPage(DeviceSectionViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.DeviceIdParameter, out var id) && id is int deviceId
			&& query.TryGetValue(Routes.SectionParameter, out var value) && value is DeviceSection section)
		{
			query.TryGetValue(Routes.DeviceNameParameter, out var name);
			_ = _viewModel.LoadAsync(deviceId, section, name as string);
		}
	}

	/// <summary>The app's own header, as every page (#142).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
	}
}
