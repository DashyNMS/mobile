using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

public partial class SettingsPage : ContentPage, IActivePage
{
	private IDisposable? _edges;
	private bool _visible;
#if IOS
	private bool _goingBack;
#endif
	private readonly SettingsViewModel _viewModel;
	private readonly SecretTaps _secretTaps = new(TimeProvider.System);

	public SettingsPage(SettingsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		Header.TitleTapped += OnSecretTap;
#if IOS
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
		Scroller.SafeAreaEdges = SafeAreaEdges.None;
		Root.SafeAreaEdges = SafeAreaEdges.None;
#else
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container, SafeAreaRegions.Container,
			SafeAreaRegions.Container, SafeAreaRegions.Container);
#endif
#if IOS
		Scroller.HandlerChanged += OnScrollerChanged;
		Scroller.Loaded += OnScrollerChanged;
#endif
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_visible = true;
		Activate();
	}

	/// <summary>Restore ownership when Shell selects a retained More stack.</summary>
	public void Activate()
	{
		_visible = true;
		_viewModel.Refresh();
		_edges ??= CoveredEdges.Watch(this, PlaceControls);
#if IOS
		Header.BackInGlass = TabBarButtons.IsAvailable;
		TabBarButtons.Moved -= OnGlassMoved;
		TabBarButtons.Moved += OnGlassMoved;
		TabBarButtons.Show(this, null, string.Empty, PageHeader.IsPushed(this)
			? [new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), GoBack, IsBack: true)] : []);
		StopInsets();
#endif
		Header.Apply(this);
		PlaceControls();
	}

	private void PlaceControls()
	{
		if (!_visible) return;
		var clear = CoveredEdges.ForScrolling(this);
		Body.Padding = new Thickness(16 + clear.Left, 20, 16 + clear.Right, 0);
		Header.Margin = new Thickness(16 + clear.Left, 0, 16 + clear.Right, 0);
		Fade.Margin = new Thickness(16 + clear.Left, 0, 16 + clear.Right, 0);
		Foot.HeightRequest = 16 + clear.Bottom;
	}

#if IOS
	private async void GoBack()
	{
		if (_goingBack || !_visible) return;
		_goingBack = true;
		try { await Shell.Current.GoToAsync(".."); }
		catch (Exception) { await DisplayAlertAsync("Could not go back", "Please try Back again.", "OK"); }
		finally { _goingBack = false; }
	}

	private void OnGlassMoved(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);
	private void OnScrollerChanged(object? sender, EventArgs e) => StopInsets();
	private void StopInsets()
	{
		if (Scroller.Handler?.PlatformView is UIKit.UIScrollView scroll)
			scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
	}
#endif

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_visible = false;
		_edges?.Dispose();
		_edges = null;
#if IOS
		TabBarButtons.Moved -= OnGlassMoved;
		TabBarButtons.Hide(this);
#endif
		Penguins.Dismiss();
	}

	/// <summary>
	/// Three quick taps on the title, or on the version line when the title
	/// is the navigation bar's: smile and wave (#99) - desktop's three
	/// Shift+clicks on its About logo. A light tick on the third, on iPhone.
	/// </summary>
	private void OnSecretTap(object? sender, EventArgs e)
	{
		try
		{
			if (!_secretTaps.Tap())
			{
				return;
			}

#if IOS
			// Android's haptics need the vibrate permission, which an easter egg doesn't justify.
			HapticFeedback.Default.Perform(HapticFeedbackType.Click);
#endif
			Penguins.Play();
		}
		catch (Exception)
		{
			// Cosmetic only - never let it affect the page.
		}
	}
}
