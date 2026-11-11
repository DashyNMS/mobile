namespace DashyNMS.Mobile.Intelligence;

/// <summary>
/// Keeps a single-chat conversation (#230) out of the app switcher: when the
/// mode is on and the chat - or one of its sheets, the saved-chat list, a report
/// preview or an enlarged graph - is on screen, the platform hides it before the
/// system takes its snapshot. Only the decision lives here, so it can be tested;
/// the head draws the cover (iOS) or sets the window secure (Android).
/// </summary>
/// <remarks>
/// <para>
/// iOS snapshots the window once the app has resigned active and entered the
/// background, so the cover goes up on the first of those (<see cref="Leaving"/>)
/// and comes down when the app is active again (<see cref="Returned"/>). The
/// shield records the cover as up only once the head reports it installed: a
/// failed or impossible install (no window or scene yet, or an exception)
/// leaves it down, so entering the background tries again rather than being
/// skipped as already covered. Once up, it always comes down, even if the
/// mode was turned off or the chat left the screen meanwhile, and leaving
/// twice draws it once.
/// </para>
/// <para>
/// Android takes its recents thumbnail when the activity pauses, too late to
/// draw anything, so its window is marked secure for as long as the chat is on
/// screen in this mode (<see cref="IsSecure"/>), and the head re-evaluates on
/// navigation, modal changes and setting changes (<see cref="Refresh"/>).
/// </para>
/// <para>
/// Other pages aren't hidden: they show the network, not the private
/// conversation, and hiding them would cost the app switcher for no privacy
/// gain. If the head can't tell what is on screen - no window, no page, a
/// page it doesn't recognise, or an exception - the chat is assumed to be
/// there (<see cref="PrivateScreen"/>), so a failure hides too much rather
/// than too little.
/// </para>
/// </remarks>
public sealed class IntelligenceSnapshotShield : IDisposable
{
    private readonly IntelligencePreferences _preferences;
    private readonly Func<bool> _chatOnScreen;
    private bool _covered;
    private bool _secure;
    private bool _disposed;

    /// <param name="chatOnScreen">Whether a page showing the conversation is visible; called on the UI thread.</param>
    public IntelligenceSnapshotShield(IntelligencePreferences preferences, Func<bool> chatOnScreen)
    {
        _preferences = preferences;
        _chatOnScreen = chatOnScreen;
        _preferences.Changed += PreferencesChanged;
    }

    /// <summary>The iOS cover is up: the head reported it installed and the app hasn't returned since.</summary>
    public bool IsCovered => _covered;

    /// <summary>Android's window should be secure now.</summary>
    public bool IsSecure => _secure;

    /// <summary>Raised when <see cref="IsSecure"/> changes.</summary>
    public event EventHandler? SecureChanged;

    /// <summary>Whether the chat would show in a snapshot taken now.</summary>
    public bool ShouldHide
    {
        get
        {
            if (_disposed || !_preferences.SingleChatMode) return false;
            try { return _chatOnScreen(); }
            catch (Exception) { return true; }
        }
    }

    /// <summary>
    /// The app is resigning active or going to the background. When the chat
    /// needs hiding and no cover is up, runs <paramref name="cover"/>, which
    /// installs it and says whether it did. True when a cover went up now;
    /// false when one already was, none was needed, or installing it failed or
    /// threw - in which case the next call tries again.
    /// </summary>
    public bool Leaving(Func<bool> cover)
    {
        if (_covered || !ShouldHide) return false;
        try { _covered = cover(); }
        catch (Exception) { _covered = false; }
        return _covered;
    }

    /// <summary>The app is active again. True when a cover is up and should come down.</summary>
    public bool Returned()
    {
        Refresh();
        if (!_covered) return false;
        _covered = false;
        return true;
    }

    /// <summary>Something on screen or the setting changed: works out <see cref="IsSecure"/> again.</summary>
    public void Refresh()
    {
        var secure = ShouldHide;
        if (secure == _secure) return;
        _secure = secure;
        SecureChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _preferences.Changed -= PreferencesChanged;
    }

    private void PreferencesChanged(object? sender, EventArgs e) => Refresh();
}
