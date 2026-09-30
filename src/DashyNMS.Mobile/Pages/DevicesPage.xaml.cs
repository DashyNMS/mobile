using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>The Devices tab; Groups &amp; locations comes back here with <see cref="Routes.GroupParameter"/> or <see cref="Routes.LocationParameter"/>.</summary>
public partial class DevicesPage : ContentPage, IQueryAttributable
{
	private readonly DevicesViewModel _viewModel;
	private readonly ShortcutReturn _shortcut = new();

	public DevicesPage(DevicesViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		SearchReveal.Attach(List, SearchSlot, Search);
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

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);

		// Every time, like the other tabs: after a sign-out and sign-in the
		// page is reused, and a once-only load would keep the old server's list.
		_viewModel.RefreshCommand.Execute(null);
	}
}
