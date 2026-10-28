using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;
using DashyNMS.Mobile.Views;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Shown via <see cref="Routes.NeighbourLink"/>, with the link (#121). The link
/// itself is <see cref="NeighbourLinkView"/>, which the Neighbours list also
/// shows beside itself on a larger screen (#88).
/// </summary>
public sealed class NeighbourLinkPage : ContentPage, IQueryAttributable
{
	private readonly NeighbourLinkView _view;

	public NeighbourLinkPage(NeighbourLinkViewModel viewModel)
	{
		BindingContext = viewModel;
		Title = "Link";
		On<iOS>().SetUseSafeArea(true);
		Content = _view = new NeighbourLinkView(viewModel);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_view.Bar.Apply(this);
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query) => _view.Load(query);
}
