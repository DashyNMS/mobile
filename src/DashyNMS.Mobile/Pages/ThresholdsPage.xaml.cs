using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;

namespace DashyNMS.Mobile.Pages;

/// <summary>Shown via <see cref="Routes.Thresholds"/>, from Settings.</summary>
public partial class ThresholdsPage : ContentPage
{
    private readonly ThresholdsViewModel _viewModel;

    public ThresholdsPage(ThresholdsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _ = new SetupPageLayout(this, Setup, Header, FormScroll);
    }

    /// <summary>Keeps validation reachable even when Save is below the final field.</summary>
    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (_viewModel.SaveCommand.IsRunning) return;
        await _viewModel.SaveCommand.ExecuteAsync(null);
        if (_viewModel.ErrorMessage is not { Length: > 0 } error) return;

        await FormScroll.ScrollToAsync(ValidationError, ScrollToPosition.Start, false);
        SemanticScreenReader.Default.Announce(error);
    }
}
