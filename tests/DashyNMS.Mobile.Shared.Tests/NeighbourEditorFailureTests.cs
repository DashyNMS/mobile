using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.Topology;
using DesktopNMS.Core.Configuration;

namespace DashyNMS.Mobile.Tests;

public sealed class NeighbourEditorFailureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_save_restores_settings_and_retry_keeps_draft_and_identity(bool existing)
    {
        var settings = new AppSettings();
        var original = new NeighbourViewDefinition { Name = "Lab" };
        if (existing) settings.NeighbourViews.Add(original);
        var store = Fakes.Settings(settings);
        var fail = true;
        store.When(s => s.Save()).Do(_ => { if (fail) throw new IOException("Synthetic failure"); });
        var navigation = new RecordingNavigation();
        var editor = new NeighbourGroupEditorViewModel(store, navigation,
            Substitute.For<IDialogService>(), new NeighbourDirectory(Fakes.Client(), store));
        editor.Load(existing ? original.Id : null);
        editor.Name = "Lab renamed";
        editor.Rules[0].Value = "ap-";

        await editor.SaveCommand.ExecuteAsync(null);

        Assert.True(editor.HasError);
        Assert.Empty(navigation.Visits);
        Assert.Equal("Lab renamed", editor.Name);
        if (existing) Assert.Same(original, Assert.Single(settings.NeighbourViews));
        else Assert.Empty(settings.NeighbourViews);

        fail = false;
        await editor.SaveCommand.ExecuteAsync(null);
        var saved = Assert.Single(settings.NeighbourViews);
        Assert.Equal("Lab renamed", saved.Name);
        Assert.Equal("ap-", Assert.Single(saved.Rules).Value);
        if (existing) Assert.Equal(original.Id, saved.Id);
        Assert.Equal(Routes.Back, Assert.Single(navigation.Visits).Route);
    }
}
