using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.WhatsNew"/>: once after an update, or from Settings › About (#159).</summary>
public partial class WhatsNewPage : ContentPage
{
	public WhatsNewPage(WhatsNewViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

	/// <summary>The app's own header, as every page (#142).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
	}
}
