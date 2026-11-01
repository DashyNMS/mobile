using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DashyNMS.Mobile.Views;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Shown via <see cref="Routes.DeviceDetail"/>, with the device id as a
/// query attribute. The device itself is <see cref="DeviceDetailView"/>,
/// which the Devices list also shows beside itself on a larger screen (#88).
/// </summary>
/// <remarks>
/// On iOS the cards scroll on down under the tab bar to the foot of the
/// screen, as on the Dashboard, and never sideways under a side tab bar's
/// glass. On iOS 26 and later Back and the pin are glass by the tab bar, so
/// the top bar goes and the device has the whole screen; before that, and
/// on Android, they stay on the top bar.
/// </remarks>
public sealed class DeviceDetailPage : ContentPage, IQueryAttributable
{
	private readonly DeviceDetailView _view;
	private readonly DeviceDetailViewModel _viewModel;
	private bool _showing;

	/// <summary>Whether the glass's pin was last shown (pinning on, device loaded), and filled.</summary>
	private (bool Shown, bool Pinned)? _glassPin;

	public DeviceDetailPage(DeviceDetailViewModel viewModel)
	{
		BindingContext = _viewModel = viewModel;
		SetBinding(TitleProperty, new Binding(nameof(DeviceDetailViewModel.Title)));
#if IOS
		// Keep clear of the status bar only; the rest is the column's padding (PlaceControls).
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
#endif
		Content = _view = new DeviceDetailView(viewModel);
		CoveredEdges.Watch(this, PlaceControls);
#if IOS
		if (TabBarButtons.IsAvailable)
		{
			_view.UseGlass();
		}

		// The pin shows once the device has loaded, filled while it's pinned.
		_viewModel.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(DeviceDetailViewModel.IsPinned))
			{
				Dispatcher.Dispatch(UpdateGlass);
			}
		};
		_viewModel.TogglePinCommand.CanExecuteChanged += (_, _) => Dispatcher.Dispatch(UpdateGlass);
#endif
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_view.Bar.Apply(this);
		_showing = true;
#if IOS
		_glassPin = null;
		UpdateGlass();
		TabBarButtons.Moved += OnTabBarButtonsMoved;
#endif
		PlaceControls();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_showing = false;
#if IOS
		TabBarButtons.Moved -= OnTabBarButtonsMoved;
		TabBarButtons.Hide(this);
#endif
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query) => _view.Load(query);

	private void PlaceControls() => _view.PlaceContent(CoveredEdges.ForScrolling(this));

#if IOS
	private void OnTabBarButtonsMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);

	/// <summary>Back, and the pin once there's a device to pin, by the tab bar - again only when they change.</summary>
	private void UpdateGlass()
	{
		var pin = (Shown: _viewModel.TogglePinCommand.CanExecute(null), Pinned: _viewModel.IsPinned);
		if (!_showing || _glassPin == pin)
		{
			return;
		}

		_glassPin = pin;
		List<GlassAction> actions =
		[
			new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => _ = Shell.Current.GoToAsync("..")),
		];
		if (pin.Shown)
		{
			actions.Add(GlassActions.Pin(pin.Pinned, () => _viewModel.TogglePinCommand.Execute(null)));
		}

		// Nothing to search on a device's own page: the actions alone.
		TabBarButtons.Show(this, null, string.Empty, actions);
	}
#endif
}
