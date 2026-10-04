using System.Globalization;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// What the shared diagnostics follow beyond log messages (#126): settings
/// as they change ("Device names: SysName → Hostname"), and pages slow to
/// load the first time, with the requests that took longest - #123 would
/// have shown up straight away. The platform's own events (errors, memory,
/// connectivity, theme) are the app head's.
/// </summary>
public sealed class AppDiagnostics
{
    /// <summary>A first load slower than this is noted.</summary>
    public static readonly TimeSpan SlowLoad = TimeSpan.FromSeconds(2);

    private readonly DiagnosticsLog _log;
    private readonly ISettingsStore _settings;
    private readonly HttpRequestLog? _requests;
    private readonly TimeProvider _time;
    private IReadOnlyDictionary<string, string> _seen = new Dictionary<string, string>();

    public AppDiagnostics(DiagnosticsLog log, ISettingsStore settings, HttpRequestLog? requests = null, TimeProvider? time = null)
    {
        _log = log;
        _settings = settings;
        _requests = requests;
        _time = time ?? TimeProvider.System;
    }

    public void Start()
    {
        _seen = Snapshot(_settings.Current);
        _settings.Changed += (_, settings) => SettingsChanged(settings);
        ViewModelBase.FirstLoadTook += (sender, took) => LoadTook(sender?.GetType().Name ?? "Page", took);
    }

    /// <summary>Which of the settings that matter changed, and from what to what.</summary>
    internal void SettingsChanged(AppSettings settings)
    {
        var now = Snapshot(settings);
        var changes = now
            .Where(kv => !_seen.TryGetValue(kv.Key, out var was) || was != kv.Value)
            .Select(kv => $"{kv.Key}: {(_seen.TryGetValue(kv.Key, out var was) ? was : "?")} → {kv.Value}")
            .ToList();
        _seen = now;

        if (changes.Count > 0)
        {
            _log.Note("Settings", string.Join("; ", changes));
        }
    }

    /// <summary>A page's first load: noted if slow, with the slowest requests in that time.</summary>
    internal void LoadTook(string viewModel, TimeSpan took)
    {
        if (took < SlowLoad)
        {
            return;
        }

        var page = viewModel.EndsWith("ViewModel", StringComparison.Ordinal) ? viewModel[..^"ViewModel".Length] : viewModel;
        var slowest = _requests?.Slowest(_time.GetUtcNow() - took) ?? [];
        _log.NoteImportant("Slow load", string.Create(CultureInfo.InvariantCulture, $"{page} took {took.TotalSeconds:0.0} s")
            + (slowest.Count == 0 ? string.Empty : "; slowest: " + string.Join(", ", slowest.Select(r => r.Summary))));
    }

    /// <summary>
    /// The settings worth following, as text - values only, never a token or
    /// password; the backup address only as set or not.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> Snapshot(AppSettings s) => new Dictionary<string, string>
    {
        ["Device names"] = s.DeviceNameStyle.ToDisplayString(),
        ["Server times"] = s.ServerTimestampsAreUtc ? "UTC" : "local",
        ["Checks while open"] = s.PollIntervalSeconds.ToString(CultureInfo.InvariantCulture) + " s",
        ["Timeout"] = s.TimeoutSeconds.ToString(CultureInfo.InvariantCulture) + " s",
        ["Notifications"] = OnOff(s.Notifications.Enabled),
        ["Notify on recovery"] = OnOff(s.Notifications.NotifyOnRecovery),
        ["Recovered alerts"] = OnOff(s.IncludeRecoveredAlerts),
        ["Untrusted certificates"] = s.AllowUntrustedCertificate ? "allowed" : "refused",
        ["Backup address"] = string.IsNullOrWhiteSpace(s.BackupServerAddress) ? "none" : "set",
        ["Pinned devices"] = OnOff(s.EnablePinnedDevices),
        ["Recently viewed"] = OnOff(s.ShowRecentlyViewedDevices),
        ["Graylog"] = OnOff(s.Graylog.Enabled),
        ["Theme"] = s.Theme.ToString(),
        ["Dashboard cards"] = s.DashboardWidgets.Count.ToString(CultureInfo.InvariantCulture),
        ["Welcome card"] = s.WelcomeDismissed ? "turned off" : "on",
    };

    private static string OnOff(bool on) => on ? "on" : "off";
}
