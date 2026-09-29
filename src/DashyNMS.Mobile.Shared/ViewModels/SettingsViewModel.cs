using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Widgets;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.ViewModels;

/// <summary>A Settings section with its own page (#67); Health thresholds and Graylog have theirs already.</summary>
public enum SettingsSection
{
    Server,
    Appearance,
    Devices,
    AlertChecks,
    Notifications,
    LockScreen,
}

/// <summary>Which server we're on, timestamps, notifications, and signing out.</summary>
/// <remarks>
/// Notification options are desktop's own <see cref="NotificationSettings"/>,
/// so they mean the same thing on both. Desktop's per-severity "stay on
/// screen" and sound options aren't offered: phones decide those per app
/// (Android per notification channel), in the system settings.
/// </remarks>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly ISessionService _session;
    private readonly ISettingsStore _settings;
    private readonly IDialogService _dialogs;
    private readonly INavigationService _navigation;
    private readonly IAlertNotifier _notifier;
    private readonly AlertWatchCoordinator _coordinator;
    private readonly IAppBadge _badge;
    private readonly IAppearance _appearance;
    private readonly IHomeWidgets _widgets;
    private readonly DeviceBookmarks _bookmarks;
    private readonly IShareService? _share;
    private readonly Graylog.GraylogSetup? _graylog;
    private readonly INotificationPrivacy _privacy;
    private readonly ILauncherService? _launcher;

    [ObservableProperty]
    private bool _serverTimestampsAreUtc;

    public SettingsViewModel(
        ISessionService session,
        ISettingsStore settings,
        IDialogService dialogs,
        INavigationService navigation,
        IAlertNotifier notifier,
        AlertWatchCoordinator coordinator,
        IAppBadge badge,
        IAppearance appearance,
        IHomeWidgets widgets,
        DeviceBookmarks? bookmarks = null,
        IShareService? share = null,
        Graylog.GraylogSetup? graylog = null,
        INotificationPrivacy? privacy = null,
        ILauncherService? launcher = null)
    {
        _privacy = privacy ?? new SystemNotificationPrivacy();
        _launcher = launcher;
        _bookmarks = bookmarks ?? new DeviceBookmarks(settings, TimeProvider.System);
        _share = share;
        _graylog = graylog;
        _session = session;
        _settings = settings;
        _dialogs = dialogs;
        _navigation = navigation;
        _notifier = notifier;
        _coordinator = coordinator;
        _badge = badge;
        _appearance = appearance;
        _widgets = widgets;
        _serverTimestampsAreUtc = settings.Current.ServerTimestampsAreUtc;
    }

    public string ServerUrl => _session.Connection?.WebRoot.ToString() ?? "Not signed in";

    /// <summary>Signed in over plain http - the Server card says so, as a browser would (#3).</summary>
    public bool IsServerInsecure => _session.Connection?.WebRoot.Scheme == Uri.UriSchemeHttp;

    public string ServerVersion => _session.ServerInfo?.LocalVersion ?? "unknown";

    public string AppVersion { get; } =
        typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "unknown";

    /// <summary>The About card's links - privacy policy, help, the website (see <see cref="AppLinks"/>) - in the browser.</summary>
    [RelayCommand]
    private Task OpenLinkAsync(Uri? link) =>
        link is null || _launcher is null ? Task.CompletedTask : _launcher.OpenAsync(link);

    /// <summary>"00:00" to "23:00", for the quiet-hours pickers (index = hour).</summary>
    public IReadOnlyList<string> Hours { get; } =
        Enumerable.Range(0, 24).Select(h => h.ToString("00", CultureInfo.InvariantCulture) + ":00").ToList();

    private NotificationSettings Notifications => _settings.Current.Notifications;

    /// <summary>Desktop's name choices, for the Device names picker.</summary>
    public IReadOnlyList<string> DeviceNameStyles { get; } =
        Enum.GetValues<DeviceNameStyle>().Select(s => s.ToDisplayString()).ToList();

    /// <summary>The choices for how many recently viewed devices to keep, up to desktop's ceiling.</summary>
    internal static readonly int[] RecentlyViewedCounts = [3, 5, 10, 15, 20, AppSettings.MaxRecentlyViewedDeviceCount];

    public IReadOnlyList<string> RecentlyViewedCountLabels { get; } =
        RecentlyViewedCounts.Select(n => n.ToString(CultureInfo.CurrentCulture)).ToList();

    /// <summary>Desktop's recently viewed switch: the strip on the Devices tab and the dashboard card.</summary>
    public bool ShowRecentlyViewed
    {
        get => _bookmarks.ShowRecentlyViewed;
        set
        {
            if (_bookmarks.ShowRecentlyViewed != value)
            {
                _bookmarks.ShowRecentlyViewed = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>How many recently viewed devices to keep (#35); a count desktop allows but the list doesn't shows as the next one up.</summary>
    public int RecentlyViewedCountIndex
    {
        get
        {
            var index = Array.FindIndex(RecentlyViewedCounts, n => n >= _bookmarks.RecentlyViewedCount);
            return index < 0 ? RecentlyViewedCounts.Length - 1 : index;
        }

        set
        {
            if (value < 0 || value >= RecentlyViewedCounts.Length || RecentlyViewedCounts[value] == _bookmarks.RecentlyViewedCount)
            {
                return;
            }

            _bookmarks.RecentlyViewedCount = RecentlyViewedCounts[value];
            OnPropertyChanged();
        }
    }

    /// <summary>Which name devices go by - hostname, sysName or display name - as on desktop.</summary>
    public int DeviceNameStyleIndex
    {
        get => Array.IndexOf(Enum.GetValues<DeviceNameStyle>(), _settings.Current.DeviceNameStyle);
        set
        {
            var styles = Enum.GetValues<DeviceNameStyle>();
            if (value < 0 || value >= styles.Length || styles[value] == _settings.Current.DeviceNameStyle)
            {
                return;
            }

            _settings.Current.DeviceNameStyle = styles[value];
            _settings.Save();
            OnPropertyChanged();
        }
    }

    /// <summary>The in-app check's choices: 30 seconds (the fastest the app allows) to 15 minutes.</summary>
    internal static readonly int[] PollIntervals = [30, 60, 120, 300, 600, 900];

    public IReadOnlyList<string> PollIntervalLabels { get; } =
        PollIntervals.Select(s => s < 60 ? $"{s} seconds" : s == 60 ? "1 minute" : $"{s / 60} minutes").ToList();

    /// <summary>
    /// Desktop's poll interval: how often alerts are checked while the app is
    /// open. Background checks run when the phone allows, whatever this is.
    /// </summary>
    public int PollIntervalIndex
    {
        get
        {
            var current = _settings.Current.PollIntervalSeconds;
            var index = Array.FindIndex(PollIntervals, s => s >= current);
            return index < 0 ? PollIntervals.Length - 1 : index;
        }

        set
        {
            if (value < 0 || value >= PollIntervals.Length || PollIntervals[value] == _settings.Current.PollIntervalSeconds)
            {
                return;
            }

            _settings.Current.PollIntervalSeconds = PollIntervals[value];
            _settings.Save();
            OnPropertyChanged();
            _coordinator.SettingsChanged();
        }
    }

    public IReadOnlyList<string> AppearanceLabels { get; } = ["Same as the phone", "Light", "Dark"];

    public int AppearanceIndex
    {
        get => (int)_appearance.Current;
        set
        {
            if (value < 0 || value >= AppearanceLabels.Count || value == (int)_appearance.Current)
            {
                return;
            }

            _appearance.Set((AppearanceChoice)value);
            OnPropertyChanged();
        }
    }

    [RelayCommand]
    private Task OpenThresholdsAsync() => _navigation.GoToAsync(Routes.Thresholds);

    /// <summary>"On · graylog.example.com", or "Off" - the Graylog row's second line.</summary>
    public string GraylogStatusText => _settings.Current.Graylog is { Enabled: true, Server: { Length: > 0 } server }
        ? "On · " + server.Trim()
        : "Off";

    [RelayCommand]
    private Task OpenGraylogSettingsAsync() => _navigation.GoToAsync(Routes.GraylogSettings);

    /// <summary>
    /// Only where the phone lets an app set the number (iPhone). Android
    /// launchers show a dot or count from the app's notifications themselves.
    /// </summary>
    public bool CanShowAppBadge => _badge.IsSupported;

    /// <summary>Desktop's Alerts tab badge setting, shown as the count on the app icon.</summary>
    public bool ShowAppBadge
    {
        get => _settings.Current.ShowAlertTabBadge;
        set => SetNotification(_settings.Current.ShowAlertTabBadge, value, v => _settings.Current.ShowAlertTabBadge = v, requestPermission: value);
    }

    public bool BadgeIncludesAcknowledged
    {
        get => _settings.Current.AlertTabBadgeIncludesAcknowledged;
        set => SetNotification(_settings.Current.AlertTabBadgeIncludesAcknowledged, value, v => _settings.Current.AlertTabBadgeIncludesAcknowledged = v);
    }

    /// <summary>Only where there are lock-screen widgets (iPhone).</summary>
    public bool HasLockScreenWidgets => _widgets.HasLockScreenWidgets;

    public bool HideLockScreenDetails
    {
        get => _widgets.HideLockScreenDetails;
        set
        {
            if (value != _widgets.HideLockScreenDetails)
            {
                _widgets.HideLockScreenDetails = value;
                OnPropertyChanged();
            }
        }
    }

    public bool NotificationsEnabled
    {
        get => Notifications.Enabled;
        set => SetNotification(Notifications.Enabled, value, v => Notifications.Enabled = v, requestPermission: value);
    }

    /// <summary>Android: the lock screen shows how serious, not which device or rule (#8).</summary>
    public bool CanHideNotificationDetails => _privacy.CanHideLockScreenDetails;

    public bool HideNotificationDetails
    {
        get => _privacy.HideLockScreenDetails;
        set
        {
            if (_privacy.HideLockScreenDetails != value)
            {
                _privacy.HideLockScreenDetails = value;
                OnPropertyChanged();
            }
        }
    }

    public bool NotifyCritical
    {
        get => Notifications.Critical.Enabled;
        set => SetNotification(Notifications.Critical.Enabled, value, v => Notifications.Critical.Enabled = v);
    }

    public bool NotifyWarning
    {
        get => Notifications.Warning.Enabled;
        set => SetNotification(Notifications.Warning.Enabled, value, v => Notifications.Warning.Enabled = v);
    }

    public bool NotifyOnRecovery
    {
        get => Notifications.NotifyOnRecovery;
        set => SetNotification(Notifications.NotifyOnRecovery, value, v => Notifications.NotifyOnRecovery = v);
    }

    public bool NotifyOnAcknowledge
    {
        get => Notifications.NotifyOnAcknowledge;
        set => SetNotification(Notifications.NotifyOnAcknowledge, value, v => Notifications.NotifyOnAcknowledge = v);
    }

    public bool QuietHoursEnabled
    {
        get => Notifications.QuietHoursEnabled;
        set => SetNotification(Notifications.QuietHoursEnabled, value, v => Notifications.QuietHoursEnabled = v);
    }

    public int QuietHoursStart
    {
        get => Notifications.QuietHoursStartHour;
        set => SetNotification(Notifications.QuietHoursStartHour, Math.Clamp(value, 0, 23), v => Notifications.QuietHoursStartHour = v);
    }

    public int QuietHoursEnd
    {
        get => Notifications.QuietHoursEndHour;
        set => SetNotification(Notifications.QuietHoursEndHour, Math.Clamp(value, 0, 23), v => Notifications.QuietHoursEndHour = v);
    }

    public bool QuietHoursAllowCritical
    {
        get => Notifications.QuietHoursAllowCritical;
        set => SetNotification(Notifications.QuietHoursAllowCritical, value, v => Notifications.QuietHoursAllowCritical = v);
    }

    partial void OnServerTimestampsAreUtcChanged(bool value)
    {
        _settings.Current.ServerTimestampsAreUtc = value;
        _settings.Save();
    }

    /// <summary>A section page's title.</summary>
    public static string Title(SettingsSection section) => section switch
    {
        SettingsSection.Server => "Server",
        SettingsSection.Appearance => "Appearance",
        SettingsSection.Devices => "Devices",
        SettingsSection.AlertChecks => "Alert checks",
        SettingsSection.Notifications => "Notifications",
        SettingsSection.LockScreen => "Lock screen",
        _ => "Settings",
    };

    [RelayCommand]
    private Task OpenSectionAsync(SettingsSection section) =>
        _navigation.GoToAsync(Routes.SettingsSection, new Dictionary<string, object> { [Routes.SettingsSectionParameter] = section });

    /// <summary>The server card's name line: just the host, the address in full is on its page.</summary>
    public string ServerHost => _session.Connection?.WebRoot.Host ?? "Not signed in";

    /// <summary>"LibreNMS 25.9.0 · https", with plain http called out (#3).</summary>
    public string ServerSummary => "LibreNMS " + ServerVersion + (IsServerInsecure ? " · not secure (http)" : " · https");

    public string AppearanceSummary => AppearanceLabels[Math.Clamp(AppearanceIndex, 0, AppearanceLabels.Count - 1)];

    /// <summary>"Hostname · 10 recently viewed".</summary>
    public string DevicesSummary => DeviceNameStyles[Math.Max(0, DeviceNameStyleIndex)] + " · " + (ShowRecentlyViewed
        ? _bookmarks.RecentlyViewedCount.ToString(CultureInfo.CurrentCulture) + " recently viewed"
        : "recently viewed off");

    /// <summary>"Every 1 minute while open · icon badge on".</summary>
    public string AlertChecksSummary => "Every " + PollIntervalLabels[PollIntervalIndex] + " while open"
        + (CanShowAppBadge ? (ShowAppBadge ? " · icon badge on" : " · icon badge off") : string.Empty);

    /// <summary>"Critical and warnings · quiet 22:00-07:00", or "Off".</summary>
    public string NotificationsSummary
    {
        get
        {
            if (!NotificationsEnabled)
            {
                return "Off";
            }

            var which = (NotifyCritical, NotifyWarning) switch
            {
                (true, true) => "Critical and warnings",
                (true, false) => "Critical only",
                (false, true) => "Warnings only",
                _ => "No severities",
            };
            return QuietHoursEnabled ? which + " · quiet " + Hours[QuietHoursStart] + "–" + Hours[QuietHoursEnd] : which;
        }
    }

    public string LockScreenSummary => HideLockScreenDetails ? "Alert details hidden when locked" : "Alert details shown when locked";

    /// <summary>Called when the page shows, since the session may have changed since it was built.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(ServerHost));
        OnPropertyChanged(nameof(ServerSummary));
        OnPropertyChanged(nameof(AppearanceSummary));
        OnPropertyChanged(nameof(DevicesSummary));
        OnPropertyChanged(nameof(AlertChecksSummary));
        OnPropertyChanged(nameof(NotificationsSummary));
        OnPropertyChanged(nameof(LockScreenSummary));
        OnPropertyChanged(nameof(ServerUrl));
        OnPropertyChanged(nameof(ServerVersion));
        OnPropertyChanged(nameof(IsServerInsecure));
        OnPropertyChanged(nameof(GraylogStatusText));
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        var choice = await _dialogs.ChooseAsync(
            "Sign out? Alert notifications stop until you sign in again.",
            [SignOutChoice, ForgetEverythingChoice]);
        if (choice is null)
        {
            return;
        }

        // The token, and what would show which devices were being watched:
        // pins, recently viewed, exported lists (#10). The alert watch state
        // and widgets are cleared by AlertWatchCoordinator on the session
        // change. The server address stays to fill in the sign-in form,
        // unless everything's to go.
        _session.SignOut(forgetToken: true);
        _bookmarks.Clear();
        _share?.ClearExports();

        if (choice == ForgetEverythingChoice)
        {
            ForgetServers();
        }

        await _navigation.GoToAsync(Routes.SignIn);
    }

    internal const string SignOutChoice = "Sign out";

    internal const string ForgetEverythingChoice = "Sign out and forget everything";

    /// <summary>The LibreNMS addresses and the Graylog connection, password included.</summary>
    private void ForgetServers()
    {
        var current = _settings.Current;
        current.ServerUrl = null;
        current.BackupServerAddress = null;
        current.AllowUntrustedCertificate = false;
        current.Graylog = new GraylogSettings();
        _settings.Save();
        _graylog?.ForgetPassword();
    }

    private void SetNotification<T>(T current, T value, Action<T> apply, bool requestPermission = false, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        if (EqualityComparer<T>.Default.Equals(current, value))
        {
            return;
        }

        apply(value);
        _settings.Save();
        OnPropertyChanged(property);
        _coordinator.SettingsChanged();

        if (requestPermission)
        {
            _ = RequestPermissionAsync();
        }
    }

    private async Task RequestPermissionAsync()
    {
        if (!await _notifier.RequestPermissionAsync())
        {
            await _dialogs.AlertAsync(
                "Notifications are off",
                "DashyNMS will keep checking, but your phone won't show its notifications until you allow them for DashyNMS in the system settings.");
        }
    }
}
