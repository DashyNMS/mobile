using DashyNMS.Mobile.Intelligence;
using System.ComponentModel;
using DesktopNMS.Core.Intelligence;

namespace DashyNMS.Mobile.Pages;

public sealed partial class IntelligencePage
{
    private string? _spotlightStamp;
    private void RefreshSpotlight()
    {
        var stamp = _conversation.ChatRevision + ":" + _conversation.CanAsk + ":" + _conversation.HasPreviousObjectChat + ":" + _conversation.SpotlightInstructions + ":" + string.Join("|", _conversation.Spotlight.Select(s => s.Name));
        if (stamp == _spotlightStamp) return;
        _spotlightStamp = stamp;
        _spotlightControls.Children.Clear();
        var choices = new HorizontalStackLayout { Spacing = 4 };
        var add = new Button { Text = "Spotlight", MinimumHeightRequest = 48, IsEnabled = _conversation.CanAsk, Style = ResourceStyle("SecondaryButton") };
        SemanticProperties.SetDescription(add, "Choose devices, a group or a location for Spotlight");
        add.Clicked += async (_, _) => await PickSpotlightAsync();
        choices.Children.Add(add);
        foreach (var item in _conversation.Spotlight)
        {
            var remove = new Button { Text = item.Name + " ×", MinimumHeightRequest = 48, IsEnabled = _conversation.CanAsk, Style = ResourceStyle("SecondaryButton") };
            SemanticProperties.SetDescription(remove, "Remove " + item.Name + " from Spotlight");
            remove.Clicked += (_, _) => _conversation.RemoveSpotlight(item);
            choices.Children.Add(remove);
        }
        _spotlightControls.Children.Add(new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal, HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            SafeAreaEdges = SafeAreaEdges.None, Content = choices,
        });
        if (_conversation.HasPreviousObjectChat)
        {
            var previous = new Button { Text = "Switch to previous chat", MinimumHeightRequest = 48, IsEnabled = _conversation.CanAsk };
            previous.Clicked += (_, _) => _conversation.SwitchPreviousObjectChat();
            _spotlightControls.Children.Add(previous);
        }
        if (_conversation.IsRunning) _spotlightControls.Children.Add(new Label { Text = "Spotlight is locked while an answer runs.", Style = ResourceStyle("Caption") });
    }

    private async Task PickSpotlightAsync()
    {
        var revision = _conversation.ChatRevision;
        var scope = _conversation.CurrentScope;
        if (scope is null) return;
        var kind = await DisplayActionSheetAsync("Keep this chat to", "Cancel", null, "Devices", "Group", "Location");
        var key = kind switch { "Devices" => "device", "Group" => "group", "Location" => "location", _ => null };
        if (key is null || revision != _conversation.ChatRevision || _conversation.CurrentScope != scope) return;
        try
        {
            var choices = await _spotlightSource.ChoicesAsync(key);
            if (revision != _conversation.ChatRevision || _conversation.CurrentScope != scope) return;
            var picker = new IntelligenceSpotlightPickerPage(choices, _conversation, revision, async (choice, token) =>
                await _conversation.SetSpotlightAsync(choice, revision, _spotlightSource.ResolveAsync, token: token));
            await Navigation.PushModalAsync(picker);
        }
        catch { await DisplayAlertAsync("Spotlight", "The choices could not be read. Try again.", "OK"); }
    }

    private async Task SpotlightReferenceAsync(IntelligenceTurn turn, IntelligenceInlineReference reference, bool only)
    {
        if (_conversation.CurrentScope is not { } scope || reference.Object is not { } identity) return;
        await _conversation.SetSpotlightAsync(new(scope, identity, reference.Name), _conversation.ChatRevision,
            _spotlightSource.ResolveAsync, only, turn);
    }
}

/// <summary>A finite searchable native list; selection applies immediately and stays open for devices.</summary>
internal sealed class IntelligenceSpotlightPickerPage : ContentPage, IPrivateChatSurface
{
    private readonly IntelligenceConversationViewModel _conversation;
    private readonly long _revision;
    private bool _valid = true;
    private bool _selecting;
    private readonly CancellationTokenSource _lifetime = new();
    private SearchBar? _search;
    private Label? _status;
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.None };
    public IntelligenceSpotlightPickerPage(IReadOnlyList<SpotlightChoice> choices, IntelligenceConversationViewModel conversation, long revision, Func<SpotlightChoice, CancellationToken, Task<bool>> select)
    {
        _conversation = conversation;
        _revision = revision;
        Title = "Spotlight";
        SafeAreaEdges = SafeAreaEdges.All;
        var done = new Button { Text = "Done", MinimumHeightRequest = 48, HorizontalOptions = LayoutOptions.Start };
        done.Clicked += async (_, _) => await Navigation.PopModalAsync();
        var search = new SearchBar { Placeholder = "Search choices" };
        var status = new Label { Text = "Selections apply straight away. Use Done to return to the chat.", LineBreakMode = LineBreakMode.WordWrap };
        _search = search; _status = status;
        search.TextChanged += (_, e) => _list.ItemsSource = !_valid ? [] : choices.Where(c => c.Name.Contains(e.NewTextValue ?? "", StringComparison.CurrentCultureIgnoreCase)).ToArray();
        _list.ItemsSource = choices;
        _list.EmptyView = "No matching choices.";
        _list.ItemTemplate = new DataTemplate(() =>
        {
            var button = new Button { MinimumHeightRequest = 48, LineBreakMode = LineBreakMode.WordWrap };
            button.SetBinding(Button.TextProperty, nameof(SpotlightChoice.Name));
            button.Clicked += async (_, _) =>
            {
                if (!_valid || _selecting || button.BindingContext is not SpotlightChoice choice) return;
                _selecting = true;
                _list.IsEnabled = false;
                try
                {
                    var added = await select(choice, _lifetime.Token);
                    if (!_lifetime.IsCancellationRequested)
                    {
                        status.Text = added ? "Added to Spotlight." : conversation.Notice;
                        SemanticScreenReader.Default.Announce(status.Text);
                    }
                }
                finally { _selecting = false; _list.IsEnabled = _valid; }
            };
            return button;
        });
        var grid = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star)], Padding = 16, RowSpacing = 8 };
        grid.Add(done, 0, 0); grid.Add(search, 0, 1); grid.Add(status, 0, 2); grid.Add(_list, 0, 3);
        Content = grid;
    }
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _conversation.PropertyChanged += ConversationChanged;
        InvalidateIfNeeded();
    }
    protected override void OnDisappearing()
    {
        _lifetime.Cancel();
        _valid = false;
        _conversation.PropertyChanged -= ConversationChanged;
        base.OnDisappearing();
    }
    private void ConversationChanged(object? sender, PropertyChangedEventArgs e) => InvalidateIfNeeded();
    private void InvalidateIfNeeded()
    {
        if (_conversation.ChatRevision == _revision && _conversation.IsTranscriptCurrent) return;
        _valid = false;
        _list.ItemsSource = null;
        if (_search is not null) { _search.Text = ""; _search.IsEnabled = false; }
        if (_status is not null) _status.Text = "The chat or connection changed. Close this picker and try again.";
    }
}
