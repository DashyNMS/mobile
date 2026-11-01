using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Unimus;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.UnimusBackup"/>, from a device's Config backups (#21).</summary>
public partial class UnimusBackupPage : ContentPage, IQueryAttributable
{
	private readonly UnimusBackupViewModel _viewModel;
	private readonly PushedListLayout _layout;

	public UnimusBackupPage(UnimusBackupViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_layout = new PushedListLayout(this, Header, Body, Rows, RowFoot);
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.UnimusParameter, out var value) && value is UnimusBackupRequest request)
		{
			_ = _viewModel.LoadAsync(request);
		}
	}

	/// <summary>The app's own header, as every page (#142).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		_layout.Show();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_layout.Hide();
	}
}
