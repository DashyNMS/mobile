using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.PickSensors"/>, with the card it sets up: a Sensors card's sensors and title (#87).</summary>
public partial class SensorPickerPage : ContentPage, IQueryAttributable
{
	private readonly SensorPickerViewModel _viewModel;

	public SensorPickerPage(SensorPickerViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>Loads once it knows which card - before then it would set up a new one.</summary>
	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.WidgetIdParameter, out var id) && id is string widgetId && widgetId.Length > 0)
		{
			_viewModel.WidgetId = widgetId;
		}

		_viewModel.LoadCommand.Execute(null);
	}

	/// <summary>The app's own top bar, as the other pushed pages (#140).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		TopBar.Apply(this);
	}
}
