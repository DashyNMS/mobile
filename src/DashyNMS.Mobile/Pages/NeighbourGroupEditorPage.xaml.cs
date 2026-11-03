using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;
using DashyNMS.Mobile.Controls;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.NeighbourGroupEditor"/> - with <see cref="Routes.GroupIdParameter"/> to change a group, without for a new one (#98).</summary>
public sealed partial class NeighbourGroupEditorPage : ContentPage, IQueryAttributable
{
	private readonly NeighbourGroupEditorViewModel _viewModel;
	private bool _loaded;
    private readonly SetupPageLayout _layout;
    private Shell? _shell;
    private Window? _window;

	public NeighbourGroupEditorPage(NeighbourGroupEditorViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_layout = new SetupPageLayout(this, Setup, Header, FormScroll);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
	}

    private void OnLoaded(object? sender, EventArgs e)
    {
        OnUnloaded(sender, e);
        _shell = Shell.Current;
        _window = Window;
        if (_shell is not null) _shell.Navigated += OnNavigated;
        if (_window is not null) _window.Resumed += OnResumed;
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (_shell is not null) _shell.Navigated -= OnNavigated;
        if (_window is not null) _window.Resumed -= OnResumed;
        _shell = null;
        _window = null;
    }

    private void OnNavigated(object? sender, ShellNavigatedEventArgs e) => RestoreActive();
    private void OnResumed(object? sender, EventArgs e) => RestoreActive();
    private void RestoreActive() => Dispatcher.Dispatch(() =>
    {
        if (Shell.Current?.CurrentPage == this) _layout.Activate();
    });

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
