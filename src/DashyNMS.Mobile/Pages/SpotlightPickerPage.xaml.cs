using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.SpotlightPicker"/>, from a log card's Spotlight (#270).</summary>
public partial class SpotlightPickerPage : ContentPage, IQueryAttributable, IActivePage
{
	private readonly SpotlightPickerViewModel _viewModel;
	private readonly SetupPageLayout _placement;

	public SpotlightPickerPage(SpotlightPickerViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		// The shared setup layout owns the header, insets, glass Back and fixed Save (#398).
		_placement = new SetupPageLayout(this, Setup, Header, List);
	}

	public void Activate() => _placement.Activate();

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.WidgetIdParameter, out var id) && id is string widgetId && widgetId.Length > 0)
		{
			_ = _viewModel.LoadAsync(widgetId);
		}
	}

	/// <summary>A device's check box ticks as its row does; the row's tap handles every other case.</summary>
	private void OnChoiceCheckedChanged(object? sender, CheckedChangedEventArgs e)
	{
		if (sender is CheckBox { BindingContext: SpotlightChoice choice } && choice.IsSelected != e.Value)
		{
			_viewModel.PickCommand.Execute(choice);
		}
	}

	private void OnSearchCompleted(object? sender, EventArgs e) => KeyboardDismiss.Close(Search);
}
