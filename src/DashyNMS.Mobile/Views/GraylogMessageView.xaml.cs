using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Views;

/// <summary>
/// One Graylog message, as its own page (<see cref="Pages.GraylogMessagePage"/>)
/// or beside the Graylog list on a larger screen (#88, #117).
/// </summary>
public partial class GraylogMessageView : ContentView
{
	public GraylogMessageView(GraylogMessageViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = ViewModel = viewModel;
	}

	public GraylogMessageViewModel ViewModel { get; }

	/// <summary>Extra clearance for glass above the page's normal bottom safe area.</summary>
	public void SetGlassClearance(double bottom) => ContentColumn.Padding = new Thickness(16, 16, 16, 24 + bottom);

	/// <summary>"‹ Graylog"; beside the list, its back button is hidden.</summary>
	public Controls.BackBar Bar => TopBar;

	/// <summary>Shows the message <see cref="Routes.GraylogMessage"/>'s parameters carry, from the list they name.</summary>
	public void Load(IDictionary<string, object>? parameters)
	{
		if (parameters?.TryGetValue(Routes.GraylogMessageParameter, out var item) == true && item is GraylogMessageItem message)
		{
			var list = parameters.TryGetValue(Routes.GraylogListParameter, out var owner) ? owner as GraylogViewModel : null;
			ViewModel.Load(message, list);
		}
	}
}
