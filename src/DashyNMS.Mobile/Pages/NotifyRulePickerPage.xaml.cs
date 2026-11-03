using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.AddNotifyRule"/>, from Settings › Notifications (#167).</summary>
public partial class NotifyRulePickerPage : ContentPage, ISearchHost
{
	private readonly NotifyRulePickerViewModel _viewModel;

	public NotifyRulePickerPage(NotifyRulePickerViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_ = new SetupPageLayout(this, Setup, Header, List);
	}

	public void OpenSearch() => Search.Focus();

	protected override void OnDisappearing()
	{
		KeyboardDismiss.Close(Search);
		base.OnDisappearing();
	}

	/// <summary>The app's own header, as every page (#142); the rules, read afresh.</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}
