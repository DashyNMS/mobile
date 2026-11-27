using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
#if IOS
using CoreGraphics;
#endif

namespace DashyNMS.Mobile.Pages;

public partial class SignInPage : ContentPage
{
	private readonly SignInViewModel _viewModel;
	private Window? _resumeWindow;
	private bool _wideForm;
#if IOS
	private LiveKeyboardBounds? _keyboard;
#endif

	public SignInPage(SignInViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		FormLayout.SizeChanged += (_, _) => PlaceForm();

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
		FormLayout.SizeChanged += (_, _) => Dispatcher.Dispatch(PlaceKeyboard);
#endif
	}

	/// <summary>Use the safe content width to reclaim height without recreating focused fields on a fold.</summary>
	private void PlaceForm()
	{
		var wide = FormLayout.Width >= ScreenLayout.SplitFrom && FormLayout.Width > FormLayout.Height;
		if (_wideForm == wide) return;
		_wideForm = wide;

		var secondColumnWidth = wide ? GridLength.Star : new GridLength(0);
		AddressFields.ColumnDefinitions[1].Width = secondColumnWidth;
		AddressFields.ColumnSpacing = wide ? 16 : 0;
		AddressFields.RowSpacing = wide ? 0 : 14;
		Grid.SetRow(BackupField, wide ? 0 : 1);
		Grid.SetColumn(BackupField, wide ? 1 : 0);

		SignInOptions.ColumnDefinitions[1].Width = secondColumnWidth;
		SignInOptions.ColumnSpacing = wide ? 24 : 0;
		OptionsDivider.IsVisible = !wide;
		Grid.SetRow(CertificateOption, wide ? 0 : 2);
		Grid.SetColumn(CertificateOption, wide ? 1 : 0);
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
		if (_keyboard is null)
		{
			// This logged-out page owns no tab glass. Keep native safe areas and
			// reserve only keyboard overlap beyond the native bottom inset.
			DashyNMS.Mobile.Controls.ManualKeyboardScroll.Acquire(this);
			_keyboard = new LiveKeyboardBounds(PlaceKeyboard, () => Dispatcher.Dispatch(ScrollFocusedField));
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
		_keyboard?.Dispose();
		_keyboard = null;
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
		if (_keyboard is null || FormLayout.Handler?.PlatformView is not UIKit.UIView { Window: { } window } view) return;

		// During a fold the notification may still describe the old display.
		// The form's live guide settles in the new coordinates independently.
		var guide = view.KeyboardLayoutGuide.LayoutFrame;
		var docked = guide.Width >= view.Bounds.Width * 0.8
			&& guide.Bottom >= view.Bounds.Bottom - 1
			&& guide.Height > window.SafeAreaInsets.Bottom + 40;
		var overlap = docked ? Math.Max(0, view.Bounds.Bottom - guide.Top) : _keyboard.OverlapIn(view);
		var padding = new Thickness(0, 0, 0, overlap);
		if (FormLayout.Padding != padding)
		{
			FormLayout.Padding = padding;
			if (overlap > 0) Dispatcher.Dispatch(ScrollFocusedField);
		}
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
