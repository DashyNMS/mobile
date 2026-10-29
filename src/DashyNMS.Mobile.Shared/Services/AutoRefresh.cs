using CommunityToolkit.Mvvm.ComponentModel;
using DesktopNMS.Core.Services;

namespace DashyNMS.Mobile.Services;

/// <summary>
/// A list page's Auto-refresh switch and interval, as desktop's logs have
/// (DashyNMS/desktop#407). It ticks only while the page is showing and the
/// app is in the foreground, and never starts a refresh while one is still
/// on its way.
/// </summary>
/// <remarks>
/// Off by default, and remembered on this phone only: refreshing on a timer
/// costs data and battery, which a desktop doesn't have to weigh.
/// </remarks>
public sealed partial class AutoRefresh : ObservableObject, IDisposable
{
    /// <summary>Desktop's interval choices.</summary>
    public static IReadOnlyList<(int Seconds, string Label)> Intervals { get; } =
    [
        (10, "Every 10 seconds"),
        (30, "Every 30 seconds"),
        (60, "Every minute"),
        (300, "Every 5 minutes"),
    ];

    public const int DefaultSeconds = 30;

    private readonly Func<Task> _refresh;
    private readonly IAppPreferences _preferences;
    private readonly string _key;
    private readonly TimeProvider _time;
    private readonly IAppActivity? _activity;
    private readonly object _sync = new();
    private ITimer? _timer;
    private SynchronizationContext? _context;
    private bool _showing;
    private bool _inFlight;

    public AutoRefresh(Func<Task> refresh, IAppPreferences preferences, string key, TimeProvider? time = null, IAppActivity? activity = null)
    {
        _refresh = refresh;
        _preferences = preferences;
        _key = key;
        _time = time ?? TimeProvider.System;
        _activity = activity;
        _isOn = preferences.Get(key + ".autorefresh") == "1";
        _seconds = int.TryParse(preferences.Get(key + ".autorefreshseconds"), out var saved) && Intervals.Any(i => i.Seconds == saved)
            ? saved
            : DefaultSeconds;
    }

    [ObservableProperty]
    private bool _isOn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IntervalText))]
    private int _seconds;

    /// <summary>"Every 30 seconds".</summary>
    public string IntervalText => Intervals.First(i => i.Seconds == Seconds).Label;

    /// <summary>Ticking now: switched on, the page showing, and the app in the foreground.</summary>
    public bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _timer is not null;
            }
        }
    }

    partial void OnIsOnChanged(bool value)
    {
        _preferences.Set(_key + ".autorefresh", value ? "1" : null);
        Update();
    }

    partial void OnSecondsChanged(int value)
    {
        _preferences.Set(_key + ".autorefreshseconds", value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Update(restart: true);
    }

    /// <summary>Off in tests: a tick refreshes at once rather than posting to the caller's context.</summary>
    internal bool PostsToCaller { get; set; } = true;

    /// <summary>The page appeared or went; called on the UI thread, whose context the refreshes run on.</summary>
    /// <remarks>
    /// The app's foreground is only watched while the page shows: pages come
    /// and go, and a handler left on the app-wide service would keep each alive.
    /// </remarks>
    public void SetShowing(bool showing)
    {
        if (showing && PostsToCaller)
        {
            _context = SynchronizationContext.Current;
        }

        if (_activity is not null && showing != _showing)
        {
            if (showing)
            {
                _activity.Changed += OnActivityChanged;
            }
            else
            {
                _activity.Changed -= OnActivityChanged;
            }
        }

        _showing = showing;
        Update();
    }

    private void OnActivityChanged(object? sender, EventArgs e) => Update();

    private void Update(bool restart = false)
    {
        var run = IsOn && _showing && _activity?.IsInBackground != true;
        lock (_sync)
        {
            if (!run || restart)
            {
                _timer?.Dispose();
                _timer = null;
            }

            if (run && _timer is null)
            {
                var interval = TimeSpan.FromSeconds(Seconds);
                _timer = _time.CreateTimer(_ => OnTick(), null, interval, interval);
            }
        }
    }

    private void OnTick()
    {
        if (_context is { } context)
        {
            context.Post(_ => _ = TickAsync(), null);
        }
        else
        {
            _ = TickAsync();
        }
    }

    /// <summary>One refresh, unless the last is still going or the page has gone since the tick.</summary>
    internal async Task TickAsync()
    {
        if (_inFlight || !IsRunning)
        {
            return;
        }

        _inFlight = true;
        try
        {
            await _refresh();
        }
        finally
        {
            _inFlight = false;
        }
    }

    public void Dispose()
    {
        if (_activity is not null && _showing)
        {
            _activity.Changed -= OnActivityChanged;
        }

        lock (_sync)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }
}
