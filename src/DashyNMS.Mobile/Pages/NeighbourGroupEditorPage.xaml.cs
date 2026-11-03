using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;
using DashyNMS.Mobile.Controls;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.NeighbourGroupEditor"/> - with <see cref="Routes.GroupIdParameter"/> to change a group, without for a new one (#98).</summary>
public sealed partial class NeighbourGroupEditorPage : ContentPage, IQueryAttributable, IActivePage
{
	private readonly NeighbourGroupEditorViewModel _viewModel;
	private bool _loaded;
	private readonly SetupPageLayout _layout;

	public NeighbourGroupEditorPage(NeighbourGroupEditorViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_layout = new SetupPageLayout(this, Setup, Header, FormScroll);
	}

	/// <summary>Restores retained controls through the shell's single activation owner.</summary>
	public void Activate() => _layout.Activate();

	/// <summary>Makes a failed Save reachable from the final rule and announces its reason.</summary>
	private async void OnSaveClicked(object? sender, EventArgs e)
	{
		if (_viewModel.SaveCommand.IsRunning) return;
		await _viewModel.SaveCommand.ExecuteAsync(null);
		if (_viewModel.ErrorMessage is not { Length: > 0 } error) return;

		await FormScroll.ScrollToAsync(ValidationError, ScrollToPosition.Start, false);
		SemanticScreenReader.Default.Announce(error);
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		_loaded = true;
		_viewModel.Load(query.TryGetValue(Routes.GroupIdParameter, out var id) ? id as string : null);
	}

	/// <summary>A new group arrives with no query at all, so nothing else loads it.</summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (!_loaded)
		{
			_loaded = true;
			_viewModel.Load(null);
		}

		// Each rule's count as it's typed (#121) - fetched here if Neighbours hasn't.
		_ = _viewModel.LoadNeighboursAsync();
	}
}
