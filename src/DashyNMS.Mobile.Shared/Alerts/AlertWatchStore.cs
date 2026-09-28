using System.Text.Json;
using DesktopNMS.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Alerts;

/// <summary>The alert states seen at the last check, and which server they came from.</summary>
public sealed record AlertWatchState(string Server, IReadOnlyDictionary<int, int> States);

/// <summary>
/// Remembers the last check between app launches - including background
/// wakes, which are often a fresh process - so each check only reports what
/// changed since the one before.
/// </summary>
/// <remarks>
/// Desktop's <see cref="INotificationStateStore"/> can't tell "never checked"
/// from "checked, nothing open", and doesn't record the server. Mobile needs
/// both: the first check for a server sets the baseline quietly rather than
/// announcing everything already outstanding.
/// </remarks>
public interface IAlertWatchStore
{
    /// <summary>Null when there has been no check since sign-in.</summary>
    AlertWatchState? Load();

    void Save(AlertWatchState state);

    void Clear();
}

public sealed class AlertWatchStore : IAlertWatchStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly string _path;
    private readonly ILogger<AlertWatchStore> _logger;
    private readonly object _gate = new();

    public AlertWatchStore(ILogger<AlertWatchStore> logger)
        : this(Path.Combine(AppPaths.DataDirectory, "alert-watch.json"), logger)
    {
    }

    internal AlertWatchStore(string path, ILogger<AlertWatchStore> logger)
    {
        _path = path;
        _logger = logger;
    }

    public AlertWatchState? Load()
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(_path))
                {
                    return null;
                }

                var persisted = JsonSerializer.Deserialize<Persisted>(File.ReadAllText(_path), SerializerOptions);
                if (persisted?.Server is null || persisted.Alerts is null)
                {
                    return null;
                }

                var states = new Dictionary<int, int>(persisted.Alerts.Count);
                foreach (var pair in persisted.Alerts)
                {
                    if (int.TryParse(pair.Key, out var id))
                    {
                        states[id] = pair.Value;
                    }
                }

                return new AlertWatchState(persisted.Server, states);
            }
            catch (Exception ex)
            {
                // A fresh baseline costs one quiet check; better than failing every check.
                _logger.LogWarning(ex, "Could not read {Path}; starting a new baseline", _path);
                return null;
            }
        }
    }

    public void Save(AlertWatchState state)
    {
        lock (_gate)
        {
            try
            {
                var persisted = new Persisted
                {
                    Server = state.Server,
                    SavedAtUtc = DateTime.UtcNow,
                    Alerts = state.States.ToDictionary(
                        p => p.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        p => p.Value),
                };

                var temp = _path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(persisted, SerializerOptions));
                File.Move(temp, _path, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not write {Path}", _path);
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            try
            {
                File.Delete(_path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not delete {Path}", _path);
            }
        }
    }

    private sealed class Persisted
    {
        public string? Server { get; set; }

        public DateTime SavedAtUtc { get; set; }

        /// <summary>Alert id (a string, as JSON keys must be) to alerts.state.</summary>
        public Dictionary<string, int>? Alerts { get; set; }
    }
}
