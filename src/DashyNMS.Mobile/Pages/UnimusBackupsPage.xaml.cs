using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Unimus;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.UnimusBackups"/>, from a device's Config backups card (#21).</summary>
public partial class UnimusBackupsPage : ContentPage, IQueryAttributable, IActivePage
{
	private readonly UnimusBackupsViewModel _viewModel;
	private readonly PushedListLayout _layout;
    private bool _active;
#if IOS
    private (bool Comparing, bool Toggle, bool Submit, int Count)? _glassState;
#endif

	public UnimusBackupsPage(UnimusBackupsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_layout = new PushedListLayout(this, Header, Body, Rows, RowFoot, CompareButton);
#if IOS
        if (TabBarButtons.IsAvailable)
        {
            CompareToggle.RemoveBinding(IsVisibleProperty);
            CompareToggle.IsVisible = false;
            CompareButton.RemoveBinding(IsVisibleProperty);
            CompareButton.IsVisible = false;
        }
#endif
		PullToRefresh.ShowBusyCard(PullRefresh, Busy, _viewModel, "Loading backups…", "Refreshing backups…");
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.DeviceIdParameter, out var id) && id is int deviceId)
		{
			query.TryGetValue(Routes.DeviceNameParameter, out var name);
			_viewModel.Initialise(deviceId, name as string);
		}
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		Activate();
	}

    /// <summary>Restores controls and checks connection revision without reloading unchanged history.</summary>
    public void Activate()
    {
        if (!_active)
        {
            _active = true;
            _viewModel.PropertyChanged += OnStateChanged;
            _viewModel.ToggleCompareCommand.CanExecuteChanged += OnCommandChanged;
            _viewModel.CompareSelectedCommand.CanExecuteChanged += OnCommandChanged;
        }
        UpdateActions();
        _layout.Activate();
        _ = _viewModel.EnsureLoadedAsync();
    }

    private void OnStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Dispatcher.Dispatch(UpdateActions);
    private void OnCommandChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(UpdateActions);

    private void UpdateActions()
    {
        if (!_active) return;
#if IOS
        if (!TabBarButtons.IsAvailable) return;
        var state = (_viewModel.IsComparing, _viewModel.ToggleCompareCommand.CanExecute(null),
            _viewModel.CompareSelectedCommand.CanExecute(null), _viewModel.Backups.Count(item => item.IsSelected));
        if (_glassState == state) return;
        _glassState = state;
        var actions = new List<GlassAction>();
        if (state.Item2)
            actions.Add(new(state.Item1 ? "xmark" : "square.on.square", state.Item1 ? "Cancel comparison" : "Choose backups to compare",
                () => { if (_active && _viewModel.ToggleCompareCommand.CanExecute(null)) _viewModel.ToggleCompareCommand.Execute(null); }));
        if (state.Item3)
            actions.Add(new("arrow.left.arrow.right", $"Compare {state.Item4} selected backups",
                () => { if (_active && _viewModel.CompareSelectedCommand.CanExecute(null)) _viewModel.CompareSelectedCommand.Execute(null); }));
        _layout.SetActions(actions);
#endif
    }

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
        _active = false;
        _viewModel.PropertyChanged -= OnStateChanged;
        _viewModel.ToggleCompareCommand.CanExecuteChanged -= OnCommandChanged;
        _viewModel.CompareSelectedCommand.CanExecuteChanged -= OnCommandChanged;
		_layout.Hide();
	}
}
