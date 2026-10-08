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
    private readonly AlertCountThreshold _countThreshold;
    private readonly IgnoredAlerts _ignored;
    private readonly DiagnosticsLog? _diagnostics;
    private readonly DiagnosticsReport? _report;
    private readonly ILocalDataWipe? _wipe;

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
        ILauncherService? launcher = null,
        AlertCountThreshold? countThreshold = null,
        IgnoredAlerts? ignored = null,
        DiagnosticsLog? diagnostics = null,
        ILocalDataWipe? wipe = null,
        BackupAddressStatus? backup = null,
        DiagnosticsReport? report = null)
    {
        _wipe = wipe;
        Backup = backup;
        _report = report;
        _countThreshold = countThreshold ?? new AlertCountThreshold(settings, new InMemoryPreferences());
        _ignored = ignored ?? new IgnoredAlerts(new InMemoryPreferences());
        _diagnostics = diagnostics;
        _ignored.Changed += (_, _) => ShowIgnored();
        ShowIgnored();
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

    /// <summary>
    /// Devices wobble like jelly when dragged on the network map (#106) -
    /// desktop's own setting (jigglePhysicsOnMaps), so it's the same in both
    /// apps. Just for fun; off by default.
    /// </summary>
    public bool JigglePhysicsOnMaps
    {
        get => _settings.Current.JigglePhysicsOnMaps;
        set
        {
            if (value == _settings.Current.JigglePhysicsOnMaps)
            {
                return;
            }

            _settings.Current.JigglePhysicsOnMaps = value;
            _settings.Save();
            OnPropertyChanged();
            OnPropertyChanged(nameof(AppearanceSummary));
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

    /// <summary>About › Open-source licences (#164).</summary>
    [RelayCommand]
    private Task OpenLicencesAsync() => _navigation.GoToAsync(Routes.Licences);

    /// <summary>About's trademark line - see <see cref="DesktopNMS.Core.Licences.OpenSourceNotices.Trademarks"/>.</summary>
    public string Trademarks => DesktopNMS.Core.Licences.OpenSourceNotices.Trademarks;

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

    /// <summary>The badge threshold's choices (#96), in Core's words as desktop's (#168): "Every alert", "Critical and warning", "Critical only".</summary>
    public IReadOnlyList<string> BadgeThresholdLabels { get; } = AlertCountThreshold.Choices.Select(AlertCountThreshold.Describe).ToList();

    /// <summary>
    /// Which alerts the app icon's count and the Alerts tab's dot notice:
    /// a phone preference. Takes effect at the next alert check, as the
    /// acknowledged switch beside it does.
    /// </summary>
    public int BadgeThresholdIndex
    {
        get => Math.Max(0, AlertCountThreshold.Choices.ToList().IndexOf(_countThreshold.Minimum));
        set
        {
            if (value < 0 || value >= AlertCountThreshold.Choices.Count || value == BadgeThresholdIndex)
            {
                return;
            }

            _countThreshold.Minimum = AlertCountThreshold.Choices[value];
            OnPropertyChanged();
            OnPropertyChanged(nameof(AlertChecksSummary));
            _coordinator.SettingsChanged();
        }
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

    public string AppearanceSummary => AppearanceLabels[Math.Clamp(AppearanceIndex, 0, AppearanceLabels.Count - 1)]
        + (JigglePhysicsOnMaps ? " · jiggle physics" : string.Empty);

    /// <summary>"Hostname · 10 recently viewed".</summary>
    public string DevicesSummary => DeviceNameStyles[Math.Max(0, DeviceNameStyleIndex)] + " · " + (ShowRecentlyViewed
        ? _bookmarks.RecentlyViewedCount.ToString(CultureInfo.CurrentCulture) + " recently viewed"
        : "recently viewed off");

    /// <summary>"Every 1 minute while open · icon badge on", with ", critical only" when it counts less (#96).</summary>
    public string AlertChecksSummary => "Every " + PollIntervalLabels[PollIntervalIndex] + " while open"
        + (CanShowAppBadge ? (ShowAppBadge ? " · icon badge on" + (BadgeThresholdIndex > 0 ? ", " + BadgeThresholdLabels[BadgeThresholdIndex].ToLower(CultureInfo.CurrentCulture) : string.Empty) : " · icon badge off") : string.Empty);

    /// <summary>The backup address, when the app has moved to it (#114) - the Server card says so, and taps through to switch back.</summary>
    public BackupAddressStatus? Backup { get; }

    /// <summary>
    /// "Hide names" (#126): the shared diagnostics with the server's and
    /// devices' names and IP addresses as placeholders. Remembered.
    /// </summary>
    public bool HideNamesInDiagnostics
    {
        get => _report?.HideNames ?? false;
        set
        {
            if (_report is not null && _report.HideNames != value)
            {
                _report.HideNames = value;
                OnPropertyChanged();
            }
        }
    }

    public bool CanHideNamesInDiagnostics => _report is not null && CanShareDiagnostics;

    /// <summary>The diagnostics log can be shared (#107) - the app keeps one, and the phone can share a file.</summary>
    public bool CanShareDiagnostics => _diagnostics is not null && _share is not null;

    /// <summary>
    /// Shares the diagnostics log as a text file, after saying what's in it -
    /// the server's address and device names can be, a token never is. It
    /// goes wherever the user sends it; the app sends nothing itself.
    /// </summary>
    [RelayCommand]
    private async Task ShareDiagnosticsAsync()
    {
        if (_diagnostics is null || _share is null)
        {
            return;
        }

        if (!await _dialogs.ConfirmAsync("Share diagnostics", DiagnosticsMessage, "Share", "Cancel"))
        {
            return;
        }

        _diagnostics.Note("App", "Diagnostics shared" + (HideNamesInDiagnostics ? " with names hidden" : string.Empty));
        var text = _report is null ? _diagnostics.Text : await _report.BuildAsync();
        var name = $"dashynms-diagnostics-{DateTime.Now.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}.txt";
        await _share.ShareTextFileAsync(name, text, "text/plain", "DashyNMS diagnostics");
    }

    /// <summary>What's in the file, said before it goes (#126) - and whether names are in it.</summary>
    internal string DiagnosticsMessage =>
        "A log of what the app did recently: the app and phone versions and the settings that matter, pages opened, "
        + "requests to LibreNMS and Graylog, alert checks, notifications, slow pages and any errors. "
        + "Never your API token or passwords. "
        + (HideNamesInDiagnostics
            ? "Server and device names and IP addresses are replaced with placeholders."
            : "It names your server and devices - turn on Hide names to leave them out.");

    /// <summary>Alert rules that don't notify, everywhere or on a device (#102) - chosen from an alert or a rule's page.</summary>
    public BulkObservableCollection<IgnoredAlert> IgnoredAlerts { get; } = new();

    public bool HasIgnoredAlerts => IgnoredAlerts.Count > 0;

    /// <summary>Notifications again from one of them.</summary>
    [RelayCommand]
    private void NotifyAgain(IgnoredAlert? entry)
    {
        if (entry is not null)
        {
            _ignored.NotifyAgain(entry);
        }
    }

    private void ShowIgnored()
    {
        IgnoredAlerts.ReplaceAll(_ignored.All);
        OnPropertyChanged(nameof(HasIgnoredAlerts));
    }

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
        // Forgetting everything can't be undone (#146), so it's red, and asks
        // again saying what goes (#139).
        var choice = await _dialogs.ChooseAsync(
            "Sign out? Alert notifications stop until you sign in again.",
            [SignOutChoice],
            ForgetEverythingChoice);
        if (choice is null
            || (choice == ForgetEverythingChoice && !await _dialogs.ConfirmDestructiveAsync("Forget everything", ForgetEverythingMessage, "Forget everything")))
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
            ForgetEverything();
        }

        await _navigation.GoToAsync(Routes.SignIn);
    }

    internal const string SignOutChoice = "Sign out";

    internal const string ForgetEverythingChoice = "Sign out and forget everything";

    /// <summary>What goes, in plain words, as desktop's sign-out says it (desktop #231).</summary>
    internal const string ForgetEverythingMessage =
        "This removes everything DashyNMS has saved on this phone: your sign-in and servers, settings, dashboard, "
        + "map layouts, ignored alerts and the Graylog connection. " + Confirmations.CannotBeUndone
        + "\n\nThe diagnostics log is kept, for bug reports.";

    /// <summary>
    /// As a fresh install (#139): the servers and Graylog password forgotten,
    /// then every file and stored preference wiped, and the settings back to
    /// their defaults (#112) - in memory too, as the app keeps running.
    /// Before #139 the settings, map layouts, ignored alerts, tab pins and
    /// appearance stayed for whoever signed in next.
    /// </summary>
    private void ForgetEverything()
    {
        ForgetServers();
        if (_wipe is not null)
        {
            foreach (var path in _wipe.Wipe())
            {
                _diagnostics?.Note("Sign out", "Could not delete " + path);
            }
        }

        _settings.Replace(new AppSettings());
        _appearance.Set(AppearanceChoice.System);
        _diagnostics?.Note("Sign out", "Forgot everything on this phone");
    }

    /// <summary>The LibreNMS addresses and the Graylog connection, password included.</summary>
    private void ForgetServers()
    {
        var current = _settings.Current;
        current.ServerUrl = null;
        current.BackupServerAddress = null;
        current.AllowUntrustedCertificate = false;
        current.TrustedCertificates.Clear();
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
