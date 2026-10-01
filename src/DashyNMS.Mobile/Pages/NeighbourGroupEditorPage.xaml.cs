using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.NeighbourGroupEditor"/> - with <see cref="Routes.GroupIdParameter"/> to change a group, without for a new one (#98).</summary>
public partial class NeighbourGroupEditorPage : ContentPage, IQueryAttributable
{
	private readonly NeighbourGroupEditorViewModel _viewModel;
	private bool _loaded;

	public NeighbourGroupEditorPage(NeighbourGroupEditorViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
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
		TopBar.Apply(this);
		if (!_loaded)
		{
			_loaded = true;
			_viewModel.Load(null);
		}
	}
}
