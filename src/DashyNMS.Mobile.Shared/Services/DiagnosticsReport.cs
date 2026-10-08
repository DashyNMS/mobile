using System.Globalization;
using DashyNMS.Mobile.Alerts;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// The diagnostics file as shared (#126): a header saying what it came from -
/// the app and phone, the server's LibreNMS version and which address is in
/// use (#114), the settings that most change what the app does - then the
/// log, with names hidden if asked. Values only: never a token or password.
/// </summary>
public sealed class DiagnosticsReport
{
    private const string HideNamesKey = "diagnostics.hideNames";

    private readonly DiagnosticsLog _log;
    private readonly ISessionService _session;
    private readonly ISettingsStore _settings;
    private readonly ServerFailover _failover;
    private readonly TabPins _pins;
    private readonly AlertCountThreshold _threshold;
    private readonly NotifyRules _notifyRules;
    private readonly IAppPreferences _preferences;
    private readonly ILibreNmsClient? _client;
    private readonly TimeProvider _time;

    public DiagnosticsReport(
        DiagnosticsLog log,
        ISessionService session,
        ISettingsStore settings,
        ServerFailover failover,
        TabPins pins,
        AlertCountThreshold threshold,
        NotifyRules notifyRules,
        IAppPreferences preferences,
        ILibreNmsClient? client = null,
        TimeProvider? time = null)
    {
        _log = log;
        _session = session;
        _settings = settings;
        _failover = failover;
        _pins = pins;
        _threshold = threshold;
        _notifyRules = notifyRules;
        _preferences = preferences;
        _client = client;
        _time = time ?? TimeProvider.System;
    }

    public DiagnosticsLog Log => _log;

    /// <summary>"Hide names" (#126): the server's and devices' names and IP addresses as placeholders. Remembered.</summary>
    public bool HideNames
    {
        get => _preferences.Get(HideNamesKey) == "1";
        set => _preferences.Set(HideNamesKey, value ? "1" : "0");
    }

    /// <summary>The header's lines.</summary>
    public IReadOnlyList<string> Header()
    {
        var settings = _settings.Current;
        var pinned = _pins.Pinned.Select(AppPages.TabTitle).ToList();
        var address = !_failover.IsOnBackup
            ? "main" + (_failover.HasBackup ? " (a backup address is set)" : string.Empty)
            : $"backup since {_failover.SwitchedAt?.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture) ?? "?"} - the main address stopped answering";

        return
        [
            "DashyNMS diagnostics",
            $"Shared: {_time.GetLocalNow().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}{(HideNames ? " · names hidden" : string.Empty)}",
            $"App: DashyNMS mobile {_log.AppVersion ?? "unknown"}",
            $"Phone: {_log.Device ?? "unknown"}",
            $"Server: LibreNMS {_session.ServerInfo?.LocalVersion ?? "unknown"}{(_session.Connection is { } connection ? $" · {connection.WebRoot.Scheme}" : " · not signed in")}",
            $"Address in use: {address}",
            "Settings: "
                + $"device names {settings.DeviceNameStyle.ToDisplayString()}"
                + $" · server times {(settings.ServerTimestampsAreUtc ? "UTC" : "local")}"
                + $" · checks every {settings.PollIntervalSeconds.ToString(CultureInfo.InvariantCulture)} s"
                + $" · notifications {(settings.Notifications.Enabled ? "on" : "off")}"
                + $" · badge counts {AlertCountThreshold.Describe(_threshold.Minimum).ToLower(CultureInfo.InvariantCulture)}"
                + $" · tabs {(pinned.Count == 0 ? "none pinned" : string.Join(", ", pinned))}"
                + $" · notify {NotifyModeText()}"
                + $" · {settings.DashboardWidgets.Count.ToString(CultureInfo.InvariantCulture)} dashboard cards",
            new string('-', 40),
        ];
    }

    /// <summary>The file to share - names hidden if <see cref="HideNames"/> is on, using every device name LibreNMS has.</summary>
    public async Task<string> BuildAsync(CancellationToken cancellationToken = default)
    {
        var header = Header();
        if (!HideNames)
        {
            return _log.Report(header);
        }

        return _log.Report(header, await RedactionAsync(cancellationToken));
    }

    /// <summary>"every alert", "every alert except 3 rules", "only 2 rules" (#167) - counts, never the rules' names.</summary>
    private string NotifyModeText()
    {
        var count = _notifyRules.Listed.Count;
        var rules = count == 1 ? "1 rule" : $"{count.ToString(CultureInfo.InvariantCulture)} rules";
        return _notifyRules.Mode switch
        {
            DesktopNMS.Core.Alerting.NotificationRuleMode.AllExcept => $"every alert except {rules}",
            DesktopNMS.Core.Alerting.NotificationRuleMode.Only => $"only {rules}",
            _ => "every alert",
        };
    }

    private async Task<DiagnosticsRedaction> RedactionAsync(CancellationToken cancellationToken)
    {
        var settings = _settings.Current;
        var hosts = new List<string?>
        {
            _session.Connection?.WebRoot.Host,
            Uri.TryCreate(settings.ServerUrl, UriKind.Absolute, out var server) ? server.Host : null,
            Uri.TryCreate(_failover.BackupAddress ?? settings.BackupServerAddress, UriKind.Absolute, out var backup) ? backup.Host : null,
            settings.Graylog.Server,
        };

        var devices = settings.PinnedDevices.Select(p => p.DisplayName)
            .Concat(settings.RecentlyViewedDevices.Select(r => r.DisplayName))
            .ToList();

        // Every device's names, best effort: the log names them as the user sees them.
        if (_client is not null && _session.IsConnected)
        {
            try
            {
                foreach (var device in await _client.Devices.ListAsync(cancellationToken))
                {
                    devices.Add(device.Hostname);
                    devices.Add(device.SysName);
                    devices.Add(device.Display);
                }
            }
            catch (LibreNmsApiException)
            {
                // The names already known are hidden; IP addresses always are.
            }
        }

        return new DiagnosticsRedaction(hosts, devices);
    }
}
