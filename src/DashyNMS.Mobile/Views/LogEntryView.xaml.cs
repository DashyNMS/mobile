using DashyNMS.Mobile.Logs;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Views;

/// <summary>
/// One event log or alert log entry, as its own page (<see cref="Pages.LogEntryPage"/>)
/// or beside the Logs list on a larger screen (#88, #118).
/// </summary>
public partial class LogEntryView : ContentView
{
	public LogEntryView(LogEntryViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = ViewModel = viewModel;
	}

	public LogEntryViewModel ViewModel { get; }

	/// <summary>The embedded pane keeps its own right and bottom edges clear of the tab glass.</summary>
	public void PlaceContent(Thickness clear) => ContentColumn.Padding = new Thickness(16, 16, 16 + clear.Right, 24 + clear.Bottom);

	/// <summary>Extra clearance for glass above the standalone page's normal bottom safe area.</summary>
	public void SetGlassClearance(double bottom) => ContentColumn.Padding = new Thickness(16, 16, 16, 24 + bottom);

	/// <summary>"‹ Logs"; beside the list, its back button is hidden.</summary>
	public Controls.BackBar Bar => TopBar;

	/// <summary>Shows the entry <see cref="Routes.LogEntry"/>'s parameters carry, from the list they name.</summary>
	public void Load(IDictionary<string, object>? parameters)
	{
		if (parameters?.TryGetValue(Routes.LogEntryParameter, out var item) == true && item is LogEntryItem entry)
		{
			var list = parameters.TryGetValue(Routes.LogsListParameter, out var owner) ? owner as LogsViewModel : null;
			ViewModel.Load(entry, list);
		}
	}
}
