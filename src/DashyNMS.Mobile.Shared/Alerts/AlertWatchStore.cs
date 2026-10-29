using System.Text.Json;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Alerting;
using CoreWatchState = DesktopNMS.Core.Alerting.AlertWatchState;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Alerts;

/// <summary>The alert states seen at the last check, and which server they came from.</summary>
public sealed record AlertWatchState(string Server, CoreWatchState Snapshot);

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
                if (persisted?.Server is null) return null;
                if (persisted.Version != CoreWatchState.CurrentVersion || persisted.Entries is null || persisted.Source is null || !Enum.IsDefined(persisted.Source.Value))
                    return new AlertWatchState(persisted.Server, CoreWatchState.Outdated);

                var entries = new Dictionary<AlertKey, AlertWatchEntry>();
                foreach (var (text, entry) in persisted.Entries)
                {
                    if (entry is null || !text.Contains(':') || !AlertKey.TryParse(text, out var key) || key.Source != persisted.Source)
                        return new AlertWatchState(persisted.Server, CoreWatchState.Outdated);
                    entries[key] = entry;
                }
                return new AlertWatchState(persisted.Server, new CoreWatchState(persisted.Source, entries));
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
                    Version = CoreWatchState.CurrentVersion,
                    Source = state.Snapshot.Source,
                    Entries = state.Snapshot.Entries.ToDictionary(p => p.Key.ToString(), p => p.Value),
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

        public int Version { get; set; }
        public AlertSource? Source { get; set; }
        public Dictionary<string, AlertWatchEntry>? Entries { get; set; }
    }
}
