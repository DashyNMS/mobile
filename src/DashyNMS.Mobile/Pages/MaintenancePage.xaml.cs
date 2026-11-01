using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.Maintenance"/>, with the device id and name - or several devices (#85).</summary>
public partial class MaintenancePage : ContentPage, IQueryAttributable
{
	private readonly MaintenanceViewModel _viewModel;

	public MaintenancePage(MaintenanceViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
#if IOS
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
		Header.BackInGlass = TabBarButtons.IsAvailable;
		FormScroll.HandlerChanged += (_, _) => StopInsetAdjustment();
		FormScroll.Loaded += (_, _) => StopInsetAdjustment();
#endif
		CoveredEdges.Watch(this, PlaceControls);
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		// Several ticked in Devices (#85).
		if (query.TryGetValue(Routes.DevicesParameter, out var several) && several is IReadOnlyList<(int Id, string Name)> devices)
		{
			_viewModel.Initialize(devices);
			return;
		}

		if (query.TryGetValue(Routes.DeviceIdParameter, out var id) && id is int deviceId)
		{
			query.TryGetValue(Routes.DeviceNameParameter, out var name);
			_viewModel.Initialize(deviceId, name as string ?? $"device {deviceId}");
		}
	}

	/// <summary>The app's own header, as every page (#142).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
#if IOS
		if (TabBarButtons.IsAvailable)
		{
			TabBarButtons.Show(this, null, string.Empty,
			[
				new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => _ = Shell.Current.GoToAsync("..")),
			]);
		}
		TabBarButtons.Moved += OnTabBarButtonsMoved;
#endif
		PlaceControls();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
#if IOS
		TabBarButtons.Moved -= OnTabBarButtonsMoved;
		TabBarButtons.Hide(this);
#endif
	}

#if IOS
	private void StopInsetAdjustment()
	{
		if (FormScroll.Handler?.PlatformView is UIKit.UIScrollView scroll)
		{
			scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
		}
	}

	private void OnTabBarButtonsMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);
#endif

	/// <summary>Form controls stay inside the clear edges; the action buttons scroll clear of the glass.</summary>
	private void PlaceControls()
	{
		if (Width <= 0) return;
		var clear = CoveredEdges.ForScrolling(this);
		var left = 16 + clear.Left;
		var right = 16 + clear.Right;
		Header.Margin = new Thickness(left, 0, right, 0);
		ContentColumn.Padding = new Thickness(left, 20, right, 24 + clear.Bottom);
		var wide = Width - left - right >= 800;
		var columns = wide ? 2 : 1;
		if (FormCards.ColumnDefinitions.Count != columns)
		{
			FormCards.ColumnDefinitions.Clear();
			for (var i = 0; i < columns; i++) FormCards.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
		}
		View[] sections = [WhenSection, DurationSection, BehaviorSection, DetailsSection];
		for (var i = 0; i < sections.Length; i++)
		{
			Grid.SetRow(sections[i], wide ? i / 2 : i);
			Grid.SetColumn(sections[i], wide ? i % 2 : 0);
		}
	}
}
