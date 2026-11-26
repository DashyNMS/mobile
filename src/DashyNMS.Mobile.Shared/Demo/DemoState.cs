namespace DashyNMS.Mobile.Demo;

/// <summary>
/// Whether the app is showing the demo (#160). The stores and checks that
/// must keep demo data off the phone ask this, so it has no dependencies of
/// its own; <see cref="DemoSession"/> is what enters and leaves the demo.
/// </summary>
public sealed class DemoState
{
    private volatile bool _isActive;

    public bool IsActive => _isActive;

    /// <summary>The demo started or ended. Raised on the thread that entered or left it.</summary>
    public event EventHandler? Changed;

    internal void Set(bool active)
    {
        if (_isActive == active)
        {
            return;
        }

        _isActive = active;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
