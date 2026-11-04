using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Unimus;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.UnimusBackup"/>, from a device's Config backups (#21).</summary>
public partial class UnimusBackupPage : ContentPage, IQueryAttributable, ISearchHost, IActivePage
{
	private readonly UnimusBackupViewModel _viewModel;
	private readonly PushedListLayout _layout;
    private readonly ButtonSearch _search;
    private bool _active;
#if IOS
    private bool? _shareAvailable;
#endif

	public UnimusBackupPage(UnimusBackupViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_layout = new PushedListLayout(this, Header, Body, Rows, RowFoot);
        _search = new ButtonSearch(SearchSlot, SearchBox, SearchEntry, Rows);
        SearchButton.Command = new Command(OpenSearch);
#if IOS
        HeaderActions.IsVisible = !TabBarButtons.IsAvailable;
#endif
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.UnimusParameter, out var value) && value is UnimusBackupRequest request)
		{
			_ = _viewModel.LoadAsync(request);
		}
	}

    public void OpenSearch() => _search.Open();
    private void OnSearchCompleted(object? sender, EventArgs e) => _search.Close();

	/// <summary>The app's own header, as every page (#142).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		Activate();
	}

    /// <summary>Restores controls when Shell returns to retained backup contents.</summary>
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

    // Native taps can change async-command state while UIKit is delivering the
    // action. Rebuild on the next UI turn, only for the still-active owner.
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
            ? [new("square.and.arrow.up", "Share complete backup", () =>
            {
                if (_active && _viewModel.ShareCommand.CanExecute(null))
                    _viewModel.ShareCommand.Execute(null);
            })]
            : [];
        _layout.SetActions(actions, OpenSearch, "Search this backup");
#endif
    }

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
        _active = false;
        _viewModel.ShareCommand.CanExecuteChanged -= OnShareCanExecuteChanged;
		_layout.Hide();
        _search.Close();
	}
}
