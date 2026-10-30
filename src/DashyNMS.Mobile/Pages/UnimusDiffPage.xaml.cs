using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Unimus;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.UnimusDiff"/>, from Compare on a device's Config backups (#21).</summary>
public partial class UnimusDiffPage : ContentPage, IQueryAttributable
{
	private readonly UnimusDiffViewModel _viewModel;

	public UnimusDiffPage(UnimusDiffViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue(Routes.UnimusParameter, out var value) && value is UnimusDiffRequest request)
		{
			_ = _viewModel.LoadAsync(request);
		}
	}

	private void OnOnlyChangedClicked(object? sender, EventArgs e) => _viewModel.OnlyChanged = !_viewModel.OnlyChanged;

	private void OnIgnoreEmptyLinesClicked(object? sender, EventArgs e) => _viewModel.IgnoreEmptyLines = !_viewModel.IgnoreEmptyLines;

	/// <summary>The app's own header, as every page (#142).</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		Header.Apply(this);
	}
}
