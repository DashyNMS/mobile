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
