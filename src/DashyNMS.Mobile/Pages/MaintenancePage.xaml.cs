using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.Maintenance"/>, with the device id and name - or several devices (#85).</summary>
public partial class MaintenancePage : ContentPage, IQueryAttributable, IActivePage
{
	private readonly MaintenanceViewModel _viewModel;
	private readonly SetupPageLayout _layout;

	public MaintenancePage(MaintenanceViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_layout = new SetupPageLayout(this, Setup, Header, FormScroll);
		Setup.SizeChanged += (_, _) => PlaceControls();
		Setup.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Padding)) PlaceControls(); };
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		// Several ticked in Devices (#85).
		if (query.TryGetValue(Routes.DevicesParameter, out var several) && several is IReadOnlyList<(int Id, string Name)> devices)
		{
			_viewModel.Initialize(devices);
			return;
		}

		if (query.TryGetValue(Routes.DeviceIdParameter, out var id) && id is int deviceId)
		{
			query.TryGetValue(Routes.DeviceNameParameter, out var name);
			_viewModel.Initialize(deviceId, name as string ?? $"device {deviceId}");
		}
	}

	/// <summary>Reclaims shared placement and Back on retained-tab return and resume.</summary>
	public void Activate() => _layout.Activate();

	/// <summary>Reveals validation and server failures even after submitting from the first field.</summary>
	private async void OnScheduleClicked(object? sender, EventArgs e)
	{
		if (_viewModel.SaveCommand.IsRunning) return;
		await _viewModel.SaveCommand.ExecuteAsync(null);
		if (_viewModel.ErrorMessage is not { Length: > 0 } error) return;
		await FormScroll.ScrollToAsync(ValidationError, ScrollToPosition.Start, false);
		if (_viewModel.ErrorMessage == error) SemanticScreenReader.Default.Announce(error);
	}

	/// <summary>Retains the existing section breakpoint within the shared measured gutters.</summary>
	private void PlaceControls()
	{
		var wide = Setup.Width - Setup.Padding.HorizontalThickness >= 800;
		var columns = wide ? 2 : 1;
		if (FormCards.ColumnDefinitions.Count != columns)
		{
			FormCards.ColumnDefinitions.Clear();
			for (var i = 0; i < columns; i++) FormCards.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
		}
		View[] sections = [WhenSection, DurationSection, BehaviorSection, DetailsSection];
		for (var i = 0; i < sections.Length; i++)
		{
			Grid.SetRow(sections[i], wide ? i / 2 : i);
			Grid.SetColumn(sections[i], wide ? i % 2 : 0);
		}
	}
}
