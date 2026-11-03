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
    [NotifyPropertyChangedFor(nameof(CanChoose))]
    [NotifyCanExecuteChangedFor(nameof(AllowSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeclineCommand))]
    private bool _isCompleting;

    /// <summary>Both completion commands share a latch so competing taps cannot pop twice.</summary>
    public bool CanChoose => !IsCompleting;

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

    private bool CanAllowSelected => CanChoose && (ShareErrors || ShareUsage);

    [RelayCommand(CanExecute = nameof(CanAllowSelected))]
    private Task AllowSelectedAsync() => CanAllowSelected
        ? CompleteAsync(ShareErrors, ShareUsage) : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanChoose))]
    private Task DeclineAsync() => CompleteAsync(false, false);

    private async Task CompleteAsync(bool errors, bool usage)
    {
        if (!CanChoose) return;
        IsCompleting = true;
        try
        {
            _consent.Choose(errors, usage);
            await _navigation.GoToAsync(Routes.Back);
        }
        catch
        {
            IsCompleting = false;
            throw;
        }
    }

    [RelayCommand]
    private Task OpenPrivacyPolicyAsync() => _launcher?.OpenAsync(AppLinks.PrivacyPolicy) ?? Task.CompletedTask;
}
