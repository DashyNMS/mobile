using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DashyNMS.Mobile.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile.Pages;

/// <summary>The Devices tab; Groups &amp; locations comes back here with <see cref="Routes.GroupParameter"/> or <see cref="Routes.LocationParameter"/>.</summary>
public partial class DevicesPage : ContentPage, IQueryAttributable, IDetailHost
{
	private readonly DevicesViewModel _viewModel;
	private readonly ShortcutReturn _shortcut = new();

	/// <summary>The device showing beside the list, if any - highlighted while the pane is there.</summary>
	private int? _shown;

	public DevicesPage(DevicesViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		SearchReveal.Attach(List, SearchSlot, Search);
		Split.SplitChanged += (_, _) => _viewModel.Select(Split.IsSplit ? _shown : null);
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		var group = query.TryGetValue(Routes.GroupParameter, out var g) ? g as string : null;
		var location = query.TryGetValue(Routes.LocationParameter, out var l) ? l as string : null;
		if (group is not null || location is not null)
		{
			_shortcut.Arrived();
			_viewModel.ShowOnly(group, location);
		}
		else if (query.TryGetValue(Routes.StateParameter, out var s) && s is DesktopNMS.Core.Models.DeviceState state)
		{
			_shortcut.Arrived();
			_viewModel.ShowOnlyState(state);
		}

		// Applied once: coming back to the tab later shouldn't reapply it.
		query.Clear();
	}

	protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
	{
		base.OnNavigatedFrom(args);
		_shortcut.Left(this, args);
	}

	protected override void OnNavigatedTo(NavigatedToEventArgs args)
	{
		base.OnNavigatedTo(args);
		if (_shortcut.OpenedAfresh())
		{
			_viewModel.ShowSavedFilter();
		}
	}

	/// <summary>
	/// A device tapped with room beside the list (#88) opens there, as does
	/// one it links to (a neighbour, say); its sections and graphs still open
	/// as pages of their own.
	/// </summary>
	public bool TryShowDetail(string route, IDictionary<string, object>? parameters)
	{
		if (!Split.IsSplit || route != Routes.DeviceDetail || Handler?.MauiContext?.Services is not { } services)
		{
			return false;
		}

		var view = new DeviceDetailView(services.GetRequiredService<DeviceDetailViewModel>());
		view.Bar.ShowsBack = false;
		view.Load(parameters);
		Split.Detail = view;
		_shown = parameters?.TryGetValue(Routes.DeviceIdParameter, out var id) == true ? id as int? : null;
		_viewModel.Select(_shown);
		return true;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);

		// Every time, like the other tabs: after a sign-out and sign-in the
		// page is reused, and a once-only load would keep the old server's list.
		_viewModel.RefreshCommand.Execute(null);
	}
}
