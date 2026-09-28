using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>Which server we're on, the timestamp option, and signing out.</summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISessionService _session;
    private readonly ISettingsStore _settings;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private bool _serverTimestampsAreUtc;

    public SettingsViewModel(ISessionService session, ISettingsStore settings, IDialogService dialogs, INavigationService navigation)
    {
        _session = session;
        _settings = settings;
        _dialogs = dialogs;
        _navigation = navigation;
        _serverTimestampsAreUtc = settings.Current.ServerTimestampsAreUtc;
    }

    public string ServerUrl => _session.Connection?.WebRoot.ToString() ?? "Not signed in";

    public string ServerVersion => _session.ServerInfo?.LocalVersion ?? "unknown";

    public string AppVersion { get; } =
        typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "unknown";

    partial void OnServerTimestampsAreUtcChanged(bool value)
    {
        _settings.Current.ServerTimestampsAreUtc = value;
        _settings.Save();
    }

    /// <summary>Called when the page shows, since the session may have changed since it was built.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(ServerUrl));
        OnPropertyChanged(nameof(ServerVersion));
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        var confirmed = await _dialogs.ConfirmAsync(
            "Sign out",
            "Sign out and forget the saved API token on this device?",
            "Sign out",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        _session.SignOut(forgetToken: true);
        await _navigation.GoToAsync(Routes.SignIn);
    }
}
