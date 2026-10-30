using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>
/// The sheet asking whether to share error reports and usage (#216), as
/// desktop's (DashyNMS/desktop#62, #498): both unticked, Allow selected only
/// once one is ticked, and Don't share. Leaving it any other way records
/// nothing, so it's asked again at the next launch.
/// </summary>
public sealed partial class DiagnosticsSharingViewModel : ObservableObject
{
    private readonly DiagnosticsConsent _consent;
    private readonly INavigationService _navigation;
    private readonly ILauncherService? _launcher;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AllowSelectedCommand))]
    private bool _shareErrors;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AllowSelectedCommand))]
    private bool _shareUsage;

    public DiagnosticsSharingViewModel(DiagnosticsConsent consent, INavigationService navigation, ILauncherService? launcher = null)
    {
        _consent = consent;
        _navigation = navigation;
        _launcher = launcher;
    }

    private bool CanAllowSelected => ShareErrors || ShareUsage;

    [RelayCommand(CanExecute = nameof(CanAllowSelected))]
    private Task AllowSelectedAsync()
    {
        _consent.Choose(ShareErrors, ShareUsage);
        return _navigation.GoToAsync(Routes.Back);
    }

    [RelayCommand]
    private Task DeclineAsync()
    {
        _consent.Choose(errors: false, usage: false);
        return _navigation.GoToAsync(Routes.Back);
    }

    [RelayCommand]
    private Task OpenPrivacyPolicyAsync() => _launcher?.OpenAsync(AppLinks.PrivacyPolicy) ?? Task.CompletedTask;
}
