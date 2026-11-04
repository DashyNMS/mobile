using DashyNMS.Mobile.ViewModels;
#if IOS
using CoreGraphics;
#endif

namespace DashyNMS.Mobile.Pages;

public partial class SignInPage : ContentPage
{
	private readonly SignInViewModel _viewModel;
	private Window? _resumeWindow;
#if IOS
	private readonly List<Foundation.NSObject> _keyboardObservers = [];
	private CoreGraphics.CGRect? _keyboardScreen;
#endif

	public SignInPage(SignInViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;

		// Copied text to offer as the token (#161): looked for on focusing the
		// token box, as well as on showing and coming back to the app.
		TokenEntry.Focused += (_, _) => _viewModel.CheckClipboard();
#if IOS
		SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container, SafeAreaRegions.Container,
			SafeAreaRegions.Container, SafeAreaRegions.Container);
		TokenEntry.Focused += (_, _) => Dispatcher.Dispatch(ScrollFocusedField);
		FormLayout.SafeAreaEdges = SafeAreaEdges.None;
		FormScroll.SafeAreaEdges = SafeAreaEdges.None;
		FormScroll.HandlerChanged += (_, _) => StopScrollInsets();
		FormLayout.SizeChanged += (_, _) => PlaceKeyboard();
#endif
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.AppearingCommand.Execute(null);
		_viewModel.CheckClipboard();
		_viewModel.PropertyChanged -= OnViewModelChanged;
		_viewModel.PropertyChanged += OnViewModelChanged;

		if (_resumeWindow is not null) _resumeWindow.Resumed -= OnResumed;
		_resumeWindow = Window;
		if (_resumeWindow is { } window)
		{
			window.Resumed += OnResumed;
		}
#if IOS
		if (_keyboardObservers.Count == 0)
		{
			// This logged-out page owns no tab glass. Keep native safe areas and
			// reserve only keyboard overlap beyond the native bottom inset.
			DashyNMS.Mobile.Controls.ManualKeyboardScroll.Acquire(this);
			_keyboardObservers.Add(UIKit.UIKeyboard.Notifications.ObserveWillChangeFrame((_, args) =>
			{
				_keyboardScreen = args.FrameEnd;
				PlaceKeyboard();
				Dispatcher.Dispatch(ScrollFocusedField);
			}));
			_keyboardObservers.Add(UIKit.UIKeyboard.Notifications.ObserveWillHide((_, _) =>
			{
				_keyboardScreen = null;
				FormLayout.Padding = 0;
			}));
		}
		StopScrollInsets();
#endif
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.PropertyChanged -= OnViewModelChanged;
		if (_resumeWindow is { } window)
		{
			window.Resumed -= OnResumed;
		}
		_resumeWindow = null;
#if IOS
		foreach (var observer in _keyboardObservers) observer.Dispose();
		_keyboardObservers.Clear();
		_keyboardScreen = null;
		FormLayout.Padding = 0;
		DashyNMS.Mobile.Controls.ManualKeyboardScroll.Release(this);
#endif
	}

	/// <summary>Back from the browser, where a token was likely just copied.</summary>
	private void OnResumed(object? sender, EventArgs e) => _viewModel.CheckClipboard();
	private void OnFieldFocused(object? sender, FocusEventArgs e)
	{
#if IOS
		Dispatcher.Dispatch(ScrollFocusedField);
#endif
	}

	private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
	{
		if (e.PropertyName is not (nameof(SignInViewModel.ErrorMessage) or nameof(SignInViewModel.IsBusy))) return;
		if (_viewModel.IsBusy || string.IsNullOrEmpty(_viewModel.ErrorMessage)) return;
		var message = _viewModel.ErrorMessage;
		Dispatcher.Dispatch(async () =>
		{
			try
			{
				if (_resumeWindow is null || _viewModel.ErrorMessage != message) return;
				await FormScroll.ScrollToAsync(SignInError, ScrollToPosition.MakeVisible, false);
				if (_resumeWindow is not null && _viewModel.ErrorMessage == message)
					SemanticScreenReader.Default.Announce(message);
			}
			catch (Exception)
			{
				// Native scroll handlers can disappear while sign-in replaces this page.
				// Feedback must not interrupt authentication or expose the error in logs.
			}
		});
	}

#if IOS
	private void StopScrollInsets()
	{
		if (FormScroll.Handler?.PlatformView is UIKit.UIScrollView scroll)
			scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
	}

	private void PlaceKeyboard()
	{
		if (_keyboardScreen is not { } screen || FormLayout.Handler?.PlatformView is not UIKit.UIView view
			|| view.Window is not { } nativeWindow) return;
		var frame = view.ConvertRectFromView(nativeWindow.ConvertRectFromCoordinateSpace(screen, nativeWindow.Screen.CoordinateSpace), nativeWindow);
		var overlap = frame.Width >= view.Bounds.Width * .8 && frame.Bottom >= view.Bounds.Height
			? Math.Max(0, view.Bounds.Height - frame.Top) : 0;
		FormLayout.Padding = new Thickness(0, 0, 0, overlap);
	}

	private void ScrollFocusedField()
	{
		if (FormScroll.Handler?.PlatformView is not UIKit.UIScrollView scroll) return;
		scroll.LayoutIfNeeded();
		var field = FindFocus(scroll);
		if (field is not null) scroll.ScrollRectToVisible(field.ConvertRectToView(field.Bounds, scroll).Inset(-8, -8), true);
	}

	private static UIKit.UIView? FindFocus(UIKit.UIView view) =>
		view.IsFirstResponder ? view : view.Subviews.Select(FindFocus).FirstOrDefault(found => found is not null);
#endif
}
