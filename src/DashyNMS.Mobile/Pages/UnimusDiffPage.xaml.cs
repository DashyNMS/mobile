using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Unimus;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.UnimusDiff"/>, from Compare on a device's Config backups (#21).</summary>
public partial class UnimusDiffPage : ContentPage, IQueryAttributable, IActivePage
{
	private readonly UnimusDiffViewModel _viewModel;
	private readonly PushedListLayout _layout;

	public UnimusDiffPage(UnimusDiffViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_layout = new PushedListLayout(this, Header, Body, Rows, RowFoot);
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
    public void Activate() => _layout.Activate();

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_layout.Hide();
	}
}
