using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Map;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>Finite, searchable location chooser returning to the actual calling map.</summary>
public partial class MapLocationPickerPage : ContentPage, IQueryAttributable, ISearchHost, IActivePage
{
    private readonly MapLocationPickerViewModel _viewModel;
    private readonly SetupPageLayout _placement;

    public MapLocationPickerPage(MapLocationPickerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        Setup.OverlayPrimaryAction = true;
        _placement = new SetupPageLayout(this, Setup, Header, DeviceList) { FullHeight = true };
    }

    public void Activate() => _placement.Activate();

    public void OpenSearch() => SearchEntry.Focus();

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(Routes.MapOwnerParameter, out var owner) && owner is MapViewModel map)
            _viewModel.Load(map);
    }

    protected override void OnDisappearing()
    {
        KeyboardDismiss.Close(SearchEntry);
        base.OnDisappearing();
    }
}
