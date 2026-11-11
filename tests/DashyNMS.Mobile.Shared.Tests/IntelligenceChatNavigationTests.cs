using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Tests;

public sealed class IntelligenceChatNavigationTests
{
    [Theory]
    [InlineData(IntelligenceAvailability.Available, true, true)]
    [InlineData(IntelligenceAvailability.Available, false, false)]
    [InlineData(IntelligenceAvailability.NotEnabled, true, false)]
    [InlineData(IntelligenceAvailability.DeviceNotEligible, true, false)]
    [InlineData(IntelligenceAvailability.ModelNotReady, true, false)]
    [InlineData(IntelligenceAvailability.Unsupported, true, false)]
    [InlineData(IntelligenceAvailability.Unavailable, true, false)]
    public void Chat_catalogue_and_saved_pin_share_the_mobile_gate(IntelligenceAvailability availability, bool enabled, bool visible)
    {
        var saved = new InMemoryPreferences();
        saved.Set(TabPins.Key, "Intelligence");
        saved.Set(IntelligencePreferences.NoticeKey, "accepted");
        var store = Substitute.For<ICheckedSettingsStore>();
        store.Current.Returns(new AppSettings { IntelligenceEnabled = enabled });
        var model = Substitute.For<IIntelligenceModel>();
        model.Availability.Returns(availability);
        var pins = new TabPins(saved, new IntelligencePreferences(store, saved), model);
        var more = new MoreViewModel(pins, new RecordingNavigation(), Substitute.For<IDialogService>());
        Assert.Equal(visible, pins.IsPinned(AppPage.Intelligence));
        Assert.Equal(visible, more.Groups.SelectMany(g => g.Items).Any(i => i.Page == AppPage.Intelligence));
        Assert.Equal("Intelligence", saved.Get(TabPins.Key));
    }

    [Fact]
    public async Task Availability_changes_restore_the_pin_and_choose_the_correct_entry_route()
    {
        var saved = new InMemoryPreferences();
        saved.Set(TabPins.Key, "Intelligence");
        saved.Set(IntelligencePreferences.NoticeKey, "accepted");
        var store = Substitute.For<ICheckedSettingsStore>();
        store.Current.Returns(new AppSettings { IntelligenceEnabled = true });
        store.TrySave().Returns(true);
        var model = Substitute.For<IIntelligenceModel>();
        model.Availability.Returns(IntelligenceAvailability.Available);
        var preferences = new IntelligencePreferences(store, saved);
        var pins = new TabPins(saved, preferences, model);
        var navigation = new RecordingNavigation();
        var more = new MoreViewModel(pins, navigation, Substitute.For<IDialogService>());
        var item = more.Groups.SelectMany(g => g.Items).Single(i => i.Page == AppPage.Intelligence);
        await more.OpenCommand.ExecuteAsync(item);
        Assert.Equal("//main/tabintelligencepage", navigation.Visits.Last().Route);
        var notifications = 0;
        pins.Changed += (_, _) => notifications++;
        model.Availability.Returns(IntelligenceAvailability.ModelNotReady);
        more.Update();
        Assert.Empty(pins.Pinned);
        await more.OpenCommand.ExecuteAsync(item); // A stale catalogue row cannot open a hidden page.
        Assert.Single(navigation.Visits);
        model.Availability.Returns(IntelligenceAvailability.Available);
        more.Update();
        Assert.True(pins.IsPinned(AppPage.Intelligence));
        Assert.Equal(2, notifications);
        pins.Toggle(AppPage.Intelligence);
        more.Update();
        await more.OpenCommand.ExecuteAsync(item);
        Assert.Equal("intelligencepage", navigation.Visits.Last().Route);
        preferences.SetEnabled(false);
        more.Update();
        Assert.DoesNotContain(more.Groups.SelectMany(g => g.Items), i => i.Page == AppPage.Intelligence);
    }

    [Theory]
    [InlineData(0, 400, 300, true)]
    [InlineData(560, 400, 1000, true)]
    [InlineData(535, 400, 1000, false)]
    [InlineData(0, 0, 1000, false)]
    public void Stream_following_keeps_a_reader_who_scrolls_away_in_place(double offset, double viewport, double content, bool follows) =>
        Assert.Equal(follows, ChatScrollFollow.IsNearEnd(offset, viewport, content));
}
