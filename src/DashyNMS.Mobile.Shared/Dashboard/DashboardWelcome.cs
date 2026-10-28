using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DashyNMS.Mobile.Alerts;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Widgets;
using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Dashboard;

/// <summary>One line of the welcome card's checklist: ticked once done, with a way to do it until then.</summary>
public sealed class WelcomeStep
{
    public WelcomeStep(string text, string tip, bool isDone, string? actionText = null, IAsyncRelayCommand? action = null)
    {
        Text = text;
        Tip = tip;
        IsDone = isDone;
        ActionText = actionText;
        Action = action;
    }

    public string Text { get; }

    /// <summary>Why it's worth doing - hidden once it's done.</summary>
    public string Tip { get; }

    public bool IsDone { get; }

    public bool ShowTip => !IsDone && Tip.Length > 0;

    /// <summary>"Set up", "Devices" - hidden once done.</summary>
    public string? ActionText { get; }

    public IAsyncRelayCommand? Action { get; }

    public bool ShowAction => !IsDone && Action is not null;
}

/// <summary>
/// The empty dashboard's welcome card (#140), as desktop's (its #233): what
/// you're connected to, desktop's starter dashboard or the card picker, and
/// a short checklist that ticks itself off - the phone's own steps, since
/// some of desktop's (Unimus) aren't on the phone. It shows until a card is
/// added, and again if every card is removed - or never again, with "Don't
/// show again", which desktop's own setting remembers.
/// </summary>
public sealed partial class DashboardWelcome : ObservableObject
{
    private const string NotificationsAllowedKey = "welcome.notifications";

    private readonly ISettingsStore _settings;
    private readonly INavigationService _navigation;
    private readonly IAppPreferences _preferences;
    private readonly IAlertNotifier? _notifier;
    private readonly IHomeWidgets? _widgets;
    private readonly IDialogService? _dialogs;
    private int? _devices;
    private int? _activeAlerts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepsText))]
    private IReadOnlyList<WelcomeStep> _steps = [];

    public DashboardWelcome(
        ISettingsStore settings,
        INavigationService navigation,
        IAppPreferences preferences,
        IAlertNotifier? notifier = null,
        IHomeWidgets? widgets = null,
        IDialogService? dialogs = null)
    {
        _settings = settings;
        _navigation = navigation;
        _preferences = preferences;
        _notifier = notifier;
        _widgets = widgets;
        _dialogs = dialogs;
        RebuildSteps();
    }

    /// <summary>"Connected to nms.example.net · 150 devices · 14 active alerts", filling in as the lists arrive.</summary>
    public string ServerSummary
    {
        get
        {
            var host = Uri.TryCreate(_settings.Current.ServerUrl, UriKind.Absolute, out var uri) ? uri.Host : "your LibreNMS server";
            var parts = new List<string> { "Connected to " + host };
            if (_devices is { } devices)
            {
                parts.Add(Count(devices, "device", "devices"));
            }

            if (_activeAlerts is { } alerts)
            {
                parts.Add(Count(alerts, "active alert", "active alerts"));
            }

            return string.Join(" · ", parts);
        }
    }

    /// <summary>"Starter: Alerts, Devices, Needs attention, Top interfaces and Recently viewed".</summary>
    public string StarterText => "Starter: " + DashboardLayout.StarterContents;

    /// <summary>"2 of 5".</summary>
    public string StepsText => $"{Steps.Count(s => s.IsDone)} of {Steps.Count}";

    /// <summary>The dashboard's counts, once it has them.</summary>
    internal void ShowCounts(int devices, int activeAlerts)
    {
        _devices = devices;
        _activeAlerts = activeAlerts;
        OnPropertyChanged(nameof(ServerSummary));
    }

    /// <summary>Ticks off what has been done since - pins, Graylog, a widget.</summary>
    public void RebuildSteps()
    {
        var settings = _settings.Current;
        var steps = new List<WelcomeStep>
        {
            new("Sign in to LibreNMS", string.Empty, isDone: true),
        };

        if (_notifier is not null)
        {
            steps.Add(new("Allow notifications", "Hear about new alerts when the app is closed",
                _preferences.Get(NotificationsAllowedKey) == "1", "Allow", AllowNotificationsCommand));
        }

        steps.Add(new("Pin the devices you watch most", "They stay at the top of Devices",
            settings.PinnedDevices.Count > 0, "Devices", OpenDevicesCommand));
        steps.Add(new("Connect Graylog for device logs", "Syslog for each device, beside LibreNMS's",
            settings.Graylog.Enabled && !string.IsNullOrWhiteSpace(settings.Graylog.Server), "Set up", OpenGraylogCommand));

        if (_widgets is not null)
        {
            steps.Add(new("Add a home screen widget", "Alerts at a glance, without opening the app",
                _widgets.IsInUse, "How", ExplainWidgetsCommand));
        }

        Steps = steps;
    }

    [RelayCommand]
    private async Task AllowNotificationsAsync()
    {
        if (_notifier is not null && await _notifier.RequestPermissionAsync())
        {
            _preferences.Set(NotificationsAllowedKey, "1");
            RebuildSteps();
        }
    }

    [RelayCommand]
    private Task OpenDevicesAsync() => _navigation.GoToAsync(Routes.Devices);

    [RelayCommand]
    private Task OpenGraylogAsync() => _navigation.GoToAsync(Routes.GraylogSettings);

    /// <summary>The app can't add a widget itself; it says how.</summary>
    [RelayCommand]
    private Task ExplainWidgetsAsync() => _dialogs?.AlertAsync(
        "Home screen widgets",
        "Touch and hold an empty part of your home screen, then add a DashyNMS widget: tap + on an iPhone or iPad, or choose Widgets on Android.")
        ?? Task.CompletedTask;

    private static string Count(int count, string one, string many) =>
        count.ToString("N0", CultureInfo.CurrentCulture) + " " + (count == 1 ? one : many);
}
