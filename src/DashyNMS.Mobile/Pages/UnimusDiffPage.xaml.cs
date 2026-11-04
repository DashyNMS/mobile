using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Unimus;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.UnimusDiff"/>, from Compare on a device's Config backups (#21).</summary>
public partial class UnimusDiffPage : ContentPage, IQueryAttributable, IActivePage
{
	private readonly UnimusDiffViewModel _viewModel;
	private readonly PushedListLayout _layout;
    private bool _active;
#if IOS
    private bool? _shareAvailable;
#endif

	public UnimusDiffPage(UnimusDiffViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_layout = new PushedListLayout(this, Header, Body, Rows, RowFoot);
#if IOS
        ShareButton.IsVisible = !TabBarButtons.IsAvailable;
#endif
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.UnimusParameter, out var value) && value is UnimusDiffRequest request)
		{
			_ = _viewModel.LoadAsync(request);
		}
	}

	/// <summary>The app's own header, as every page (#142).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		Activate();
	}

    /// <summary>Restores diff controls through the shared retained-stack activation.</summary>
    public void Activate()
    {
        if (!_active)
        {
            _active = true;
            _viewModel.ShareCommand.CanExecuteChanged += OnShareCanExecuteChanged;
        }
        UpdateActions();
        _layout.Activate();
    }

    // Defer native action updates until UIKit has finished delivering the tap.
    private void OnShareCanExecuteChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(UpdateActions);

    private void UpdateActions()
    {
        if (!_active) return;
#if IOS
        if (!TabBarButtons.IsAvailable) return;
        var available = _viewModel.ShareCommand.CanExecute(null);
        if (_shareAvailable == available) return;
        _shareAvailable = available;
        IReadOnlyList<GlassAction> actions = available
            ? [new("square.and.arrow.up", "Share complete diff", () =>
            {
                if (_active && _viewModel.ShareCommand.CanExecute(null))
                    _viewModel.ShareCommand.Execute(null);
            })]
            : [];
        _layout.SetActions(actions);
#endif
    }

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
        _active = false;
        _viewModel.ShareCommand.CanExecuteChanged -= OnShareCanExecuteChanged;
		_layout.Hide();
	}
}
