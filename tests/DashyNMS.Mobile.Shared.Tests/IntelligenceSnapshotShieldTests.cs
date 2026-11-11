using DashyNMS.Mobile.Intelligence;
using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Tests;

/// <summary>
/// When single-chat mode hides the chat from the app switcher (#230). The cover
/// and the secure window themselves are native and need an installed build.
/// </summary>
public sealed class IntelligenceSnapshotShieldTests : IDisposable
{
    private readonly AppSettings _values = new();
    private readonly IntelligencePreferences _preferences;
    private readonly IntelligenceSnapshotShield _shield;
    private bool _chatOnScreen = true;
    private bool _throws;

    public IntelligenceSnapshotShieldTests()
    {
        var settings = Substitute.For<ICheckedSettingsStore>();
        settings.Current.Returns(_values);
        settings.TrySave().Returns(true);
        _preferences = new IntelligencePreferences(settings, new InMemoryPreferences());
        _shield = new IntelligenceSnapshotShield(_preferences, () => _throws ? throw new InvalidOperationException("synthetic") : _chatOnScreen);
    }

    public void Dispose() => _shield.Dispose();

    [Fact]
    public void Nothing_is_hidden_outside_single_chat_mode()
    {
        Assert.False(_shield.ShouldHide);
        Assert.False(_shield.Leaving());
        Assert.False(_shield.IsCovered);
        Assert.False(_shield.Returned());
    }

    [Fact]
    public void Other_pages_are_not_hidden_in_single_chat_mode()
    {
        _preferences.SetSingleChatMode(true);
        _chatOnScreen = false;
        Assert.False(_shield.Leaving());
        Assert.False(_shield.IsSecure);
    }

    [Fact]
    public void The_cover_goes_up_once_on_the_way_out_and_comes_down_on_return()
    {
        _preferences.SetSingleChatMode(true);
        Assert.True(_shield.Leaving());
        Assert.True(_shield.IsCovered);
        // Resigning active, then entering the background: drawn once.
        Assert.False(_shield.Leaving());

        Assert.True(_shield.Returned());
        Assert.False(_shield.IsCovered);
        Assert.False(_shield.Returned());
    }

    [Fact]
    public void A_cover_comes_down_even_if_the_mode_or_page_changed_while_away()
    {
        _preferences.SetSingleChatMode(true);
        Assert.True(_shield.Leaving());
        _chatOnScreen = false;
        _preferences.SetSingleChatMode(false);
        Assert.True(_shield.Returned());
        Assert.False(_shield.IsCovered);
    }

    [Fact]
    public void The_secure_window_follows_the_mode_and_what_is_on_screen()
    {
        var changes = 0;
        _shield.SecureChanged += (_, _) => changes++;

        _preferences.SetSingleChatMode(true);
        Assert.True(_shield.IsSecure);
        Assert.Equal(1, changes);

        // Navigating away from the chat clears it; back to it sets it again.
        _chatOnScreen = false;
        _shield.Refresh();
        Assert.False(_shield.IsSecure);
        _chatOnScreen = true;
        _shield.Refresh();
        _shield.Refresh();
        Assert.True(_shield.IsSecure);
        Assert.Equal(3, changes);

        _preferences.SetSingleChatMode(false);
        Assert.False(_shield.IsSecure);
        Assert.Equal(4, changes);
    }

    [Fact]
    public void Not_knowing_what_is_on_screen_hides_the_chat()
    {
        _preferences.SetSingleChatMode(true);
        _throws = true;
        Assert.True(_shield.ShouldHide);
        Assert.True(_shield.Leaving());
    }

    [Fact]
    public void A_disposed_shield_stops_following_the_setting()
    {
        _shield.Dispose();
        _preferences.SetSingleChatMode(true);
        Assert.False(_shield.IsSecure);
        Assert.False(_shield.Leaving());
    }
}
