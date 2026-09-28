using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Security;

/// <summary>
/// A synchronous view over <see cref="ISecureStorage"/>, for Core's secret
/// interfaces (<c>ITokenProtector</c> and friends), whose methods are
/// synchronous because DPAPI is.
/// </summary>
/// <remarks>
/// Secrets are read once at start-up (<see cref="LoadAsync"/>) and served from
/// memory after that. Writes update memory straight away and reach the
/// platform store in the background, one at a time and in order, so a quick
/// save-then-clear can never land the other way round. Blocking on the
/// platform's async API from the UI thread instead risks a deadlock on iOS.
/// </remarks>
public sealed class SecretCache
{
    private readonly ISecureStorage _storage;
    private readonly ILogger<SecretCache> _logger;
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    private Task _writes = Task.CompletedTask;
    private Task? _loaded;

    public SecretCache(ISecureStorage storage, ILogger<SecretCache> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    /// <summary>
    /// <see cref="LoadAsync"/> the first time; the same task after that, so
    /// every caller that needs secrets can await it without reading twice.
    /// </summary>
    public Task EnsureLoadedAsync(IEnumerable<string> keys)
    {
        lock (_gate)
        {
            return _loaded ??= LoadAsync(keys);
        }
    }

    /// <summary>Reads <paramref name="keys"/> from the platform store, replacing what's in memory for them.</summary>
    public async Task LoadAsync(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            string? value;
            try
            {
                value = await _storage.GetAsync(key).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Typically a restored backup on a new device: the keystore
                // entry that encrypted it doesn't exist here. Start clean.
                _logger.LogWarning(ex, "Could not read secret {Key}; it will be discarded", key);
                TryRemove(key);
                value = null;
            }

            lock (_gate)
            {
                if (string.IsNullOrEmpty(value))
                {
                    _values.Remove(key);
                }
                else
                {
                    _values[key] = value;
                }
            }
        }
    }

    public string? Get(string key)
    {
        lock (_gate)
        {
            return _values.TryGetValue(key, out var value) ? value : null;
        }
    }

    public bool Contains(string key) => Get(key) is not null;

    public void Set(string key, string value)
    {
        lock (_gate)
        {
            _values[key] = value;
            Enqueue(() => _storage.SetAsync(key, value), key);
        }
    }

    public void Remove(string key)
    {
        lock (_gate)
        {
            _values.Remove(key);
            Enqueue(() =>
            {
                _storage.Remove(key);
                return Task.CompletedTask;
            }, key);
        }
    }

    /// <summary>Completes once every write so far has reached the platform store.</summary>
    public Task FlushAsync()
    {
        lock (_gate)
        {
            return _writes;
        }
    }

    // Callers hold _gate, so writes are chained in the order they were made.
    private void Enqueue(Func<Task> write, string key)
    {
        _writes = _writes.ContinueWith(
            async _ =>
            {
                try
                {
                    await write().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Could not update secret {Key}", key);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default).Unwrap();
    }

    private void TryRemove(string key)
    {
        try
        {
            _storage.Remove(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not remove secret {Key}", key);
        }
    }
}
