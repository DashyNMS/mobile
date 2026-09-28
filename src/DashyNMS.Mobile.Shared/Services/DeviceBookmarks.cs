using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// Pinned and recently viewed devices, kept in desktop's own settings
/// (<see cref="AppSettings.PinnedDevices"/>, <see cref="AppSettings.RecentlyViewedDevices"/>)
/// so they mean the same as on desktop.
/// </summary>
public sealed class DeviceBookmarks
{
    private readonly ISettingsStore _settings;
    private readonly TimeProvider _time;

    public DeviceBookmarks(ISettingsStore settings, TimeProvider time)
    {
        _settings = settings;
        _time = time;
    }

    /// <summary>Raised after pins or the recently viewed list change.</summary>
    public event EventHandler? Changed;

    private AppSettings Current => _settings.Current;

    /// <summary>Desktop's switch for the whole feature; off means nothing counts as pinned.</summary>
    public bool PinningEnabled => Current.EnablePinnedDevices;

    public IReadOnlySet<int> PinnedIds =>
        PinningEnabled ? Current.PinnedDevices.Select(p => p.DeviceId).ToHashSet() : new HashSet<int>();

    /// <summary>Newest first, capped at desktop's <see cref="AppSettings.RecentlyViewedDeviceCount"/>.</summary>
    public IReadOnlyList<RecentlyViewedDevice> RecentlyViewed =>
        Current.ShowRecentlyViewedDevices ? Current.RecentlyViewedDevices : [];

    public bool IsPinned(int deviceId) => PinningEnabled && Current.PinnedDevices.Any(p => p.DeviceId == deviceId);

    public void SetPinned(int deviceId, string displayName, bool pinned)
    {
        var list = Current.PinnedDevices;
        var existing = list.FindIndex(p => p.DeviceId == deviceId);

        if (pinned && existing < 0)
        {
            list.Add(new PinnedDevice { DeviceId = deviceId, DisplayName = displayName, PinnedAt = _time.GetUtcNow() });
        }
        else if (!pinned && existing >= 0)
        {
            list.RemoveAt(existing);
        }
        else
        {
            return;
        }

        _settings.Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Desktop's switch for the recently viewed strip.</summary>
    public bool ShowRecentlyViewed
    {
        get => Current.ShowRecentlyViewedDevices;
        set
        {
            if (Current.ShowRecentlyViewedDevices == value)
            {
                return;
            }

            Current.ShowRecentlyViewedDevices = value;
            _settings.Save();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// How many recently viewed devices are remembered and shown (#35) -
    /// desktop's setting, 1 to <see cref="AppSettings.MaxRecentlyViewedDeviceCount"/>.
    /// Lowering it drops the oldest straight away rather than at the next view.
    /// </summary>
    public int RecentlyViewedCount
    {
        get => Math.Clamp(Current.RecentlyViewedDeviceCount, 1, AppSettings.MaxRecentlyViewedDeviceCount);
        set
        {
            var count = Math.Clamp(value, 1, AppSettings.MaxRecentlyViewedDeviceCount);
            if (Current.RecentlyViewedDeviceCount == count)
            {
                return;
            }

            Current.RecentlyViewedDeviceCount = count;
            var list = Current.RecentlyViewedDevices;
            if (list.Count > count)
            {
                list.RemoveRange(count, list.Count - count);
            }

            _settings.Save();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Forgets every pinned and recently viewed device - on sign-out, so a
    /// signed-out phone doesn't list which devices were being watched (#10).
    /// </summary>
    public void Clear()
    {
        if (Current.PinnedDevices.Count == 0 && Current.RecentlyViewedDevices.Count == 0)
        {
            return;
        }

        Current.PinnedDevices.Clear();
        Current.RecentlyViewedDevices.Clear();
        _settings.Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Moves the device to the front of the recently viewed list.</summary>
    public void RecordViewed(int deviceId, string displayName)
    {
        var list = Current.RecentlyViewedDevices;
        list.RemoveAll(d => d.DeviceId == deviceId);
        list.Insert(0, new RecentlyViewedDevice { DeviceId = deviceId, DisplayName = displayName, ViewedAt = _time.GetUtcNow() });

        var cap = RecentlyViewedCount;
        if (list.Count > cap)
        {
            list.RemoveRange(cap, list.Count - cap);
        }

        _settings.Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
