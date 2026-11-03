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

	public UnimusBackupPage(UnimusBackupViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_layout = new PushedListLayout(this, Header, Body, Rows, RowFoot);
        _search = new ButtonSearch(SearchSlot, SearchBox, SearchEntry, Rows);
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.UnimusParameter, out var value) && value is UnimusBackupRequest request)
		{
			_ = _viewModel.LoadAsync(request);
		}
	}

    public void OpenSearch() => _search.Open();
    private void OnSearchClicked(object? sender, EventArgs e) => OpenSearch();
    private void OnSearchCompleted(object? sender, EventArgs e) => _search.Close();

	/// <summary>The app's own header, as every page (#142).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		Activate();
	}

    /// <summary>Restores controls when Shell returns to retained backup contents.</summary>
    public void Activate() => _layout.Activate();

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_layout.Hide();
        _search.Close();
	}
}
