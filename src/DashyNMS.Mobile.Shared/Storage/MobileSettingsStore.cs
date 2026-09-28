using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Storage;

/// <summary>
/// Desktop's <see cref="SettingsStore"/>, with saving that works without a
/// <see cref="ISettingsStore.Changed"/> subscriber.
/// </summary>
/// <remarks>
/// Desktop's <c>Save()</c> is <c>Changed?.Invoke(this, Write())</c>: the
/// null-conditional skips the whole call, the write included, when nothing
/// listens to <c>Changed</c>. Desktop always has a listener; mobile has none,
/// so nothing was ever saved. Here every save writes through
/// <c>SaveQuietly()</c> and raises <c>Changed</c> separately. Harmless once
/// desktop's own is fixed.
/// </remarks>
public sealed class MobileSettingsStore : ISettingsStore
{
    private readonly ISettingsStore _inner;

    public MobileSettingsStore(ISettingsStore inner) => _inner = inner;

    public AppSettings Current => _inner.Current;

    public event EventHandler<AppSettings>? Changed;

    public AppSettings Load() => _inner.Load();

    public void Save()
    {
        _inner.SaveQuietly();
        Changed?.Invoke(this, _inner.Current);
    }

    public void SaveQuietly() => _inner.SaveQuietly();

    public void Replace(AppSettings settings)
    {
        // Desktop's Replace() saves through the same Save(), so write again here.
        _inner.Replace(settings);
        _inner.SaveQuietly();
        Changed?.Invoke(this, _inner.Current);
    }
}
