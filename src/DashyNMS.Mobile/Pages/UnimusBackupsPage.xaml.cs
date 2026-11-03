using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Unimus;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.UnimusBackups"/>, from a device's Config backups card (#21).</summary>
public partial class UnimusBackupsPage : ContentPage, IQueryAttributable, IActivePage
{
	private readonly UnimusBackupsViewModel _viewModel;
	private readonly PushedListLayout _layout;

	public UnimusBackupsPage(UnimusBackupsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_layout = new PushedListLayout(this, Header, Body, Rows, RowFoot, CompareButton);
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

    /// <summary>Restores retained history controls and refreshes changed connection settings.</summary>
    public void Activate()
    {
        _layout.Activate();
        _ = _viewModel.EnsureLoadedAsync();
    }

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_layout.Hide();
	}
}
