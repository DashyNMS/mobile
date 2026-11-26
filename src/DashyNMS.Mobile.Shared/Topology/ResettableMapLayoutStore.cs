using DesktopNMS.Core.Topology;
using Microsoft.Extensions.Logging;

namespace DashyNMS.Mobile.Topology;

/// <summary>
/// Core's <see cref="MapLayoutStore"/>, which can also forget every layout
/// (#139). Core's keeps what it read in memory and only forgets one scope at a
/// time, so deleting its file alone would leave the old layouts to be written
/// back at the next drag. Forgetting deletes the file and starts a fresh store.
/// </summary>
public sealed class ResettableMapLayoutStore : IMapLayoutStore
{
    private readonly string _path;
    private readonly ILogger<MapLayoutStore> _logger;
    private readonly object _sync = new();
    private MapLayoutStore _inner;

    public ResettableMapLayoutStore(string path, ILogger<MapLayoutStore> logger)
    {
        _path = path;
        _logger = logger;
        _inner = new MapLayoutStore(path, logger);
    }

    private MapLayoutStore Inner
    {
        get
        {
            lock (_sync)
            {
                return _inner;
            }
        }
    }

    /// <summary>While the demo runs, devices it drags are kept in memory only and forgotten with it (#160).</summary>
    public Demo.DemoState? Demo
    {
        get => _demo;
        init
        {
            _demo = value;
            if (value is not null)
            {
                value.Changed += (_, _) => { if (!value.IsActive) ForgetDemo(); };
            }
        }
    }

    private readonly Demo.DemoState? _demo;

    private readonly Dictionary<string, IReadOnlyDictionary<int, MapPoint>> _demoLayouts = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<int, MapPoint> Get(string scopeKey)
    {
        if (Demo?.IsActive == true)
        {
            lock (_demoLayouts)
            {
                return _demoLayouts.TryGetValue(scopeKey, out var positions) ? positions : new Dictionary<int, MapPoint>();
            }
        }

        return Inner.Get(scopeKey);
    }

    public void Save(string scopeKey, IReadOnlyDictionary<int, MapPoint> positions)
    {
        if (Demo?.IsActive == true)
        {
            lock (_demoLayouts)
            {
                _demoLayouts[scopeKey] = positions.ToDictionary();
            }

            return;
        }

        Inner.Save(scopeKey, positions);
    }

    public void Clear(string scopeKey)
    {
        if (Demo?.IsActive == true)
        {
            lock (_demoLayouts)
            {
                _demoLayouts.Remove(scopeKey);
            }

            return;
        }

        Inner.Clear(scopeKey);
    }

    /// <summary>The demo ended: what it dragged goes with it.</summary>
    public void ForgetDemo()
    {
        lock (_demoLayouts)
        {
            _demoLayouts.Clear();
        }
    }

    /// <summary>Every map's layout gone, on disk and in memory. False if the file couldn't be deleted.</summary>
    public bool ForgetAll()
    {
        lock (_sync)
        {
            _inner = new MapLayoutStore(_path, _logger);
            try
            {
                File.Delete(_path);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not delete {Path}", _path);
                return false;
            }
        }
    }
}
