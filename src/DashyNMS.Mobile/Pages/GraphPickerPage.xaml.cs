using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.PickGraph"/>, with the card it sets up: a Graph card's graph and title (#87).</summary>
public partial class GraphPickerPage : ContentPage, IQueryAttributable
{
	private readonly GraphPickerViewModel _viewModel;

	public GraphPickerPage(GraphPickerViewModel viewModel)
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
}
