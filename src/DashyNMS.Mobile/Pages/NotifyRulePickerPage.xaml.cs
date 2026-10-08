using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.AddNotifyRule"/>, from Settings › Notifications (#167).</summary>
public partial class NotifyRulePickerPage : ContentPage
{
	private readonly NotifyRulePickerViewModel _viewModel;

	public NotifyRulePickerPage(NotifyRulePickerViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>The app's own header, as every page (#142); the rules, read afresh.</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
		_viewModel.LoadCommand.Execute(null);
	}
}
