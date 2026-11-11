using System.ComponentModel;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>A pushed, automatic-save setup page from Settings. Back returns to the caller.</summary>
public partial class IntelligenceSettingsPage : ContentPage, IActivePage
{
    private readonly IntelligenceSettingsViewModel _viewModel;
    private readonly SetupPageLayout _layout;

    public IntelligenceSettingsPage(IntelligenceSettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _layout = new SetupPageLayout(this, Setup, Header, FormScroll);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.PropertyChanged -= OnChanged;
        _viewModel.PropertyChanged += OnChanged;
        _viewModel.Refresh();
    }

    protected override void OnDisappearing()
    {
        _viewModel.PropertyChanged -= OnChanged;
        base.OnDisappearing();
    }

    private async void OnChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_viewModel.SavedChatsStatus))
        {
            // Said aloud, as the result sits below the button rather than in a dialog.
            var status = _viewModel.SavedChatsStatus;
            if (status.Length > 0) SemanticScreenReader.Default.Announce(status);
            return;
        }

        if (e.PropertyName != nameof(_viewModel.ErrorMessage) || !_viewModel.HasError) return;
        var message = _viewModel.ErrorMessage;
        await FormScroll.ScrollToAsync(0, 0, false);
        if (!string.IsNullOrEmpty(message) && _viewModel.ErrorMessage == message)
            SemanticScreenReader.Default.Announce(message);
    }

    /// <summary>Recheck model availability when returning from the system Settings app.</summary>
    public void Activate()
    {
        _viewModel.Refresh();
        _layout.Activate();
    }
}
