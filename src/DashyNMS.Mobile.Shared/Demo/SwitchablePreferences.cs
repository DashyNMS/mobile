using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Demo;

/// <summary>
/// The phone's own preferences (tab pins, filters, notices), with the demo's
/// changes kept in memory over them while it runs (#160): the demo starts from
/// how the app is set up, and nothing chosen in it is kept.
/// </summary>
public sealed class SwitchablePreferences(IAppPreferences real, DemoState demo) : IAppPreferences
{
    private readonly Dictionary<string, string?> _overlay = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    public string? Get(string key)
    {
        lock (_sync)
        {
            if (demo.IsActive && _overlay.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return real.Get(key);
    }

    public void Set(string key, string? value)
    {
        if (demo.IsActive)
        {
            lock (_sync)
            {
                _overlay[key] = value;
            }

            return;
        }

        real.Set(key, value);
    }

    /// <summary>Forgets what the demo chose.</summary>
    public void ClearDemo()
    {
        lock (_sync)
        {
            _overlay.Clear();
        }
    }
}
