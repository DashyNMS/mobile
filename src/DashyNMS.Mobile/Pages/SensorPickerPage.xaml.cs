using DashyNMS.Mobile.Controls;
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
		_ = new SetupPageLayout(this, Setup, Header, List);
	}

	private void OnSensorCheckedChanged(object? sender, CheckedChangedEventArgs e)
	{
		if (sender is CheckBox { BindingContext: PickableSensor sensor } && sensor.IsPicked != e.Value)
		{
			_viewModel.ToggleCommand.Execute(sensor);
		}
	}

	private void OnSearchCompleted(object? sender, EventArgs e) => KeyboardDismiss.Close(Search);

	/// <summary>Loads once it knows which card - before then it would set up a new one.</summary>
	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.WidgetIdParameter, out var id) && id is string widgetId && widgetId.Length > 0)
		{
			_viewModel.WidgetId = widgetId;
		}

		_viewModel.LoadCommand.Execute(null);
	}
}
