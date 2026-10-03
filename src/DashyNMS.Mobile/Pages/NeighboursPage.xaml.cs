using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;
using DashyNMS.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.Neighbours"/>, from the Devices tab.</summary>
public partial class NeighboursPage : ContentPage, IDetailHost
{
	private readonly NeighboursViewModel _viewModel;

	public NeighboursPage(NeighboursViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading neighbours…", "Refreshing neighbours…");
		SearchReveal.Attach(List, SearchSlot, Search);
		viewModel.RefreshCommand.Execute(null);
	}

	/// <summary>A link tapped with room beside the list (#88) opens there.</summary>
	public bool TryShowDetail(string route, IDictionary<string, object>? parameters)
	{
		if (!Split.IsSplit || route != Routes.NeighbourLink || Handler?.MauiContext?.Services is not { } services)
		{
			return false;
		}

		var view = new NeighbourLinkView(services.GetRequiredService<NeighbourLinkViewModel>());
		view.Bar.ShowsBack = false;
		view.Load(parameters);
		Split.Detail = view;
		return true;
	}

	/// <summary>Back from the groups: they may have changed (#98).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
		_viewModel.GroupsChanged();
	}
}
