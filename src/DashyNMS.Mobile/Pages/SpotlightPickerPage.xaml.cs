using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.SpotlightPicker"/>, from a log card's Spotlight (#270).</summary>
public partial class SpotlightPickerPage : ContentPage, IQueryAttributable
{
	private readonly SpotlightPickerViewModel _viewModel;

	public SpotlightPickerPage(SpotlightPickerViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.WidgetIdParameter, out var id) && id is string widgetId)
		{
			_ = _viewModel.LoadAsync(widgetId);
		}
	}

	/// <summary>The app's own header, as every page (#142).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
	}
	private void OnSetupSearchCompleted(object? sender, EventArgs e)
	{
		if (sender is Entry entry) DashyNMS.Mobile.Controls.KeyboardDismiss.Close(entry);
	}

}
