using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Demo;

/// <summary>
/// The app's settings: the real file, or the demo's own while the demo runs
/// (#160). Everything that reads or saves settings goes through this, so the
/// demo's pins, dashboard and server address never reach the real file, and
/// leaving it puts the real settings back exactly as they were.
/// </summary>
public sealed class SwitchableSettingsStore : ICheckedSettingsStore
{
    private readonly ICheckedSettingsStore _real;
    private ICheckedSettingsStore _current;

    public SwitchableSettingsStore(ICheckedSettingsStore real)
    {
        _real = real;
        _current = real;
        real.Changed += OnInnerChanged;
    }

    public AppSettings Current => _current.Current;

    public event EventHandler<AppSettings>? Changed;

    /// <summary>Whether the demo's settings are in use.</summary>
    public bool IsUsingDemo => !ReferenceEquals(_current, _real);

    /// <summary>Uses <paramref name="demo"/> until <see cref="UseReal"/>, telling every listener the settings changed.</summary>
    public void UseDemo(ICheckedSettingsStore demo)
    {
        if (ReferenceEquals(_current, demo))
        {
            return;
        }

        _current.Changed -= OnInnerChanged;
        _current = demo;
        demo.Changed += OnInnerChanged;
        Changed?.Invoke(this, demo.Current);
    }

    public void UseReal()
    {
        if (!IsUsingDemo)
        {
            return;
        }

        _current.Changed -= OnInnerChanged;
        _current = _real;
        _real.Changed += OnInnerChanged;
        Changed?.Invoke(this, _real.Current);
    }

    public AppSettings Load() => _current.Load();

    public void Save() => _current.Save();

    public void SaveQuietly() => _current.SaveQuietly();

    public bool TrySave() => _current.TrySave();

    public void Replace(AppSettings settings) => _current.Replace(settings);

    private void OnInnerChanged(object? sender, AppSettings settings)
    {
        if (ReferenceEquals(sender, _current))
        {
            Changed?.Invoke(this, settings);
        }
    }
}
