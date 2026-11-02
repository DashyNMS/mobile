using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Shown via <see cref="Routes.AddCard"/>, from Edit dashboard or the welcome
/// card (#140) - with <see cref="Routes.PositionParameter"/> from a card's
/// "Add a card below" (#145).
/// </summary>
public partial class AddCardPage : ContentPage, IQueryAttributable
{
	private readonly AddCardViewModel _viewModel;

	public AddCardPage(AddCardViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		DashboardBackNavigation.Attach(this, TopBar, List);
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query) =>
		_viewModel.Position = query.TryGetValue(Routes.PositionParameter, out var value) && value is int position ? position : null;

	/// <summary>What's on the dashboard may have changed since it was made.</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		TopBar.Apply(this);
		_viewModel.Load();
	}
}
