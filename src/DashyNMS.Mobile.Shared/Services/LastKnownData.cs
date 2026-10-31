using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// The last good copy of the device list, the open alerts and the sensors,
/// kept on the phone (#130) - so with no signal or the VPN down the app opens
/// to what it last saw, marked "Offline: last updated 07:42", rather than to
/// errors and empty lists. It's used again as soon as the server answers.
/// </summary>
/// <remarks>
/// <para>Mirrors DashyNMS/desktop#69's last-known-good cache, which hasn't
/// moved to Core yet; when it does, this should give way to it.</para>
/// <para>Only what LibreNMS returned for those lists, filed under the server
/// it came from, so another server never shows this one's devices. Never a
/// token or password. Signing out wipes it, and "forget everything" takes the
/// whole data folder.</para>
/// </remarks>
public sealed partial class LastKnownData : ObservableObject
{
    private const string Devices = "devices";
    private const string Alerts = "alerts";
    private const string Sensors = "sensors";

    private readonly string _folder;
    private readonly ISettingsStore _settings;
    private readonly ILogger<LastKnownData> _logger;
    private readonly HashSet<string> _offline = [];
    private readonly object _gate = new();

    /// <summary>When what's shown was fetched, while any of it is the saved copy.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShowingSaved), nameof(OfflineText))]
    private DateTimeOffset? _shownSince;

    public LastKnownData(string dataFolder, ISettingsStore settings, ILogger<LastKnownData> logger)
    {
        _folder = Path.Combine(dataFolder, "last-known");
        _settings = settings;
        _logger = logger;
    }

    /// <summary>Some of what's on screen is the saved copy, because the server couldn't be reached.</summary>
    public bool IsShowingSaved => ShownSince is not null;

    /// <summary>"Offline: last updated 07:42", or with the day when it wasn't today.</summary>
    public string? OfflineText => ShownSince is { } at
        ? "Offline: last updated " + at.ToLocalTime().ToString(at.ToLocalTime().Date == DateTime.Today ? "HH:mm" : "d MMM, HH:mm", CultureInfo.CurrentCulture)
        : null;

    /// <summary>The device list from <paramref name="fetch"/>, saved; or, the server out of reach, the saved one.</summary>
    public Task<IReadOnlyList<Device>> DevicesAsync(Func<Task<IReadOnlyList<Device>>> fetch) => OrSavedAsync(Devices, fetch, d => d, d => d);

    /// <summary>
    /// The open alerts, as <see cref="DevicesAsync"/> - each with the
    /// endpoint it came from, which isn't in LibreNMS's JSON but is half of
    /// what identifies a LibreNMS 26.10 fault.
    /// </summary>
    public Task<IReadOnlyList<Alert>> AlertsAsync(Func<Task<IReadOnlyList<Alert>>> fetch) => OrSavedAsync(
        Alerts, fetch, a => new SavedAlert(a.Source, a), s => { s.Alert.Source = s.Source; return s.Alert; });

    /// <summary>Every sensor, as <see cref="DevicesAsync"/>.</summary>
    public Task<IReadOnlyList<Sensor>> SensorsAsync(Func<Task<IReadOnlyList<Sensor>>> fetch) => OrSavedAsync(Sensors, fetch, s => s, s => s);

    /// <summary>Whether there's anything saved for the server in settings - enough to open the app offline.</summary>
    public bool HasSaved() => Read<Device>(Devices) is not null || Read<SavedAlert>(Alerts) is not null;

    /// <summary>Signing out: nothing of this server's stays on the phone.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _offline.Clear();
            try
            {
                if (Directory.Exists(_folder))
                {
                    Directory.Delete(_folder, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning("Could not delete the saved lists: {Reason}", ex.Message);
            }
        }

        ShownSince = null;
    }

    /// <summary>
    /// The server not answering - no connection, a timeout, a server error -
    /// rather than refusing: a rejected token or a denied list is shown as
    /// it is, never papered over with the saved copy.
    /// </summary>
    internal static bool IsUnreachable(Exception ex) => ex switch
    {
        LibreNmsApiException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound } => false,
        LibreNmsApiException => true,
        HttpRequestException or TimeoutException or IOException or SocketException => true,
        // Core's monitors report a failed poll by its message.
        InvalidOperationException => true,
        _ => false,
    };

    private async Task<IReadOnlyList<T>> OrSavedAsync<T, TSaved>(string kind, Func<Task<IReadOnlyList<T>>> fetch, Func<T, TSaved> save, Func<TSaved, T> restore)
    {
        IReadOnlyList<T> items;
        try
        {
            items = await fetch();
        }
        catch (Exception ex) when (IsUnreachable(ex) && Read<TSaved>(kind) is { } saved)
        {
            _logger.LogInformation("Showing the saved {Kind} from {SavedAt:HH:mm}: {Reason}", kind, saved.SavedAt.ToLocalTime(), ex.Message);
            lock (_gate)
            {
                _offline.Add(kind);
            }

            ShownSince = ShownSince is { } earlier && earlier < saved.SavedAt ? earlier : saved.SavedAt;
            return saved.Items.Select(restore).ToList();
        }

        Write(kind, items.Select(save).ToList());
        bool online;
        lock (_gate)
        {
            _offline.Remove(kind);
            online = _offline.Count == 0;
        }

        if (online)
        {
            ShownSince = null;
        }

        return items;
    }

    private string? Server => _settings.Current.ServerUrl?.Trim().TrimEnd('/');

    private string PathFor(string kind) => Path.Combine(_folder, kind + ".json");

    private Saved<T>? Read<T>(string kind)
    {
        try
        {
            var path = PathFor(kind);
            if (!File.Exists(path))
            {
                return null;
            }

            var saved = JsonSerializer.Deserialize<Saved<T>>(File.ReadAllText(path));
            return saved is not null && saved.Server is not null && string.Equals(saved.Server, Server, StringComparison.OrdinalIgnoreCase) ? saved : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private void Write<T>(string kind, IReadOnlyList<T> items)
    {
        if (Server is not { } server)
        {
            return;
        }

        var saved = new Saved<T>(server, DateTimeOffset.UtcNow, items);
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(_folder);
                var path = PathFor(kind);
                File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(saved));
                File.Move(path + ".tmp", path, overwrite: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // A copy that can't be saved just means none offline; the fetch itself worked.
            _logger.LogWarning("Could not save the {Kind} for offline use: {Reason}", kind, ex.Message);
        }
    }

    private sealed record Saved<T>(string Server, DateTimeOffset SavedAt, IReadOnlyList<T> Items);

    private sealed record SavedAlert(AlertSource Source, Alert Alert);
}
