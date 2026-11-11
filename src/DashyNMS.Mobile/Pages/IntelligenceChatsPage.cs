using System.ComponentModel;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Saved chats (#229): a modal sheet over the chat, grouped Pinned, Today,
/// Yesterday and so on by Core. Like the report preview it stacks a 44pt Done
/// above a wrapping title and owns its own safe area, as a modal with no glass
/// owner. Each row opens its chat when tapped; swiping right pins, swiping left
/// reveals Rename and Delete, a long press opens the same actions as a menu
/// (TalkBack's double-tap-and-hold reaches it too), and VoiceOver offers them
/// as custom actions. Delete always confirms (#146).
/// </summary>
public sealed class IntelligenceChatsPage : ContentPage
{
    private readonly IntelligenceChatListViewModel _chats;
    private readonly IntelligenceConversationViewModel _conversation;
    private readonly IDialogService _dialogs;
    private readonly Action _newChat;
    private readonly Label _status = new() { LineBreakMode = LineBreakMode.WordWrap };
    private readonly CollectionView _list = new() { IsGrouped = true, SelectionMode = SelectionMode.None };
    private readonly Button _new = new() { Text = "New chat", MinimumHeightRequest = 44 };
    private readonly Button _close = new() { Text = "Done", MinimumHeightRequest = 44, MinimumWidthRequest = 88, HorizontalOptions = LayoutOptions.Start };
    private bool _busy;
    private bool _visible;
    private readonly List<ChatRow> _rows = [];

    public IntelligenceChatsPage(IntelligenceChatListViewModel chats, IntelligenceConversationViewModel conversation,
        IDialogService dialogs, Action newChat)
    {
        _chats = chats;
        _conversation = conversation;
        _dialogs = dialogs;
        _newChat = newChat;
        Title = "Chats";
        SafeAreaEdges = SafeAreaEdges.All;
        this.SetAppThemeColor(BackgroundColorProperty, ThemeColour("BackgroundLight"), ThemeColour("BackgroundDark"));

        _close.Style = ResourceStyle("SecondaryButton");
        _new.Style = ResourceStyle("SecondaryButton");
        SemanticProperties.SetDescription(_close, "Close saved chats");
        SemanticProperties.SetDescription(_new, "Start a new chat");
        _close.Clicked += async (_, _) => await CloseAsync();
        _new.Clicked += async (_, _) =>
        {
            await CloseAsync();
            _newChat();
        };

        var title = new Label { Text = "Chats", Style = ResourceStyle("PageTitle"), LineBreakMode = LineBreakMode.WordWrap };
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);
        var scope = new Label
        {
            Text = "Saved on this phone for the LibreNMS server you're signed in to.",
            Style = ResourceStyle("Caption"), LineBreakMode = LineBreakMode.WordWrap,
        };

        _list.ItemsSource = chats.Groups;
        _list.GroupHeaderTemplate = new DataTemplate(() =>
        {
            var label = new Label { Style = ResourceStyle("SectionHeader"), Padding = new Thickness(16, 16, 16, 4) };
            label.SetBinding(Label.TextProperty, nameof(IntelligenceChatGroup.Label));
            SemanticProperties.SetHeadingLevel(label, SemanticHeadingLevel.Level2);
            return label;
        });
        _list.ItemTemplate = new DataTemplate(() =>
        {
            var row = new ChatRow(this);
            _rows.Add(row);
            return row;
        });
        _list.EmptyView = new Label
        {
            Text = "No saved chats yet. Questions you ask are saved here.",
            Style = ResourceStyle("Caption"), Margin = new Thickness(16), LineBreakMode = LineBreakMode.WordWrap,
        };

        var top = new VerticalStackLayout { Spacing = 8, Padding = new Thickness(16, 0, 16, 8) };
        top.Children.Add(title);
        top.Children.Add(scope);
        top.Children.Add(_status);
        top.Children.Add(_new);

        var grid = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star)] };
        _close.Margin = new Thickness(16, 8);
        grid.Add(_close, 0, 0);
        grid.Add(top, 0, 1);
        grid.Add(_list, 0, 2);
        Content = grid;
        Update();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _visible = true;
        foreach (var row in _rows) row.Attach();
        _chats.PropertyChanged -= OnChanged;
        _chats.PropertyChanged += OnChanged;
        _conversation.PropertyChanged -= OnChanged;
        _conversation.PropertyChanged += OnChanged;
        _ = _chats.LoadAsync();
        Update();
    }

    protected override void OnDisappearing()
    {
        _visible = false;
        foreach (var row in _rows) row.Detach();
        _chats.PropertyChanged -= OnChanged;
        _conversation.PropertyChanged -= OnChanged;
        base.OnDisappearing();
    }

    private void OnChanged(object? sender, PropertyChangedEventArgs e) => Update();

    private void Update()
    {
        var status = _chats.Status.Length > 0 ? _chats.Status
            : _conversation.IsRunning ? "Saved chats can be opened or changed once the answer finishes."
            : !_chats.IsListAvailable ? "Saved chats aren't available yet."
            : "";
        _status.Text = status;
        _status.IsVisible = status.Length > 0;
        _status.Style = _chats.IsReadOnly || !_chats.IsListAvailable && !_chats.IsSingleChatMode && _chats.Status.Length > 0
            ? ResourceStyle("Error") : ResourceStyle("Caption");
        // Hidden in single-chat mode, and when the list can't be read.
        _list.IsVisible = _chats.IsListAvailable;
        _new.IsEnabled = !_conversation.IsRunning && _conversation.IsTranscriptCurrent;
    }

    private async Task CloseAsync()
    {
        if (Navigation.ModalStack.Contains(this)) await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync();
        return true;
    }

    /// <summary>
    /// Opens through the list's shared leave decision, which may ask about the
    /// chat on screen first. Cancelling there says nothing more: the chat stays as it was.
    /// </summary>
    private async Task OpenAsync(IntelligenceChatItem item)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            if (await _chats.OpenAsync(item)) await CloseAsync();
            else if (_conversation.IsRunning) SemanticScreenReader.Default.Announce("Wait for the answer to finish");
            else if (!_chats.CanManage || !_chats.Chats.Contains(item)) SemanticScreenReader.Default.Announce("That chat can't be opened right now");
        }
        finally { _busy = false; }
    }

    private void TogglePin(IntelligenceChatItem item)
    {
        var pinning = !item.IsPinned;
        if (_chats.TogglePin(item)) SemanticScreenReader.Default.Announce(pinning ? "Pinned" : "Unpinned");
    }

    private async Task RenameAsync(IntelligenceChatItem item)
    {
        if (_busy) return;
        _busy = true;
        try { if (await _chats.RenameAsync(item)) SemanticScreenReader.Default.Announce("Renamed"); }
        finally { _busy = false; }
    }

    private async Task DeleteAsync(IntelligenceChatItem item)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            // Announced only once the file is gone; a failure says so in the status line.
            var before = _chats.Status;
            if (await _chats.DeleteAsync(item)) SemanticScreenReader.Default.Announce("Chat deleted");
            else if (_chats.Status.Length > 0 && _chats.Status != before) SemanticScreenReader.Default.Announce(_chats.Status);
        }
        finally { _busy = false; }
    }

    /// <summary>The long-press menu: what the row's swipes and VoiceOver actions do.</summary>
    private async Task ShowMenuAsync(IntelligenceChatItem item)
    {
        if (_busy || !_chats.Chats.Contains(item)) return;
        var choice = await _dialogs.ChooseAsync(item.Title, ["Open", item.PinText, "Rename"], "Delete");
        switch (choice)
        {
            case "Open": await OpenAsync(item); break;
            case "Pin" or "Unpin": TogglePin(item); break;
            case "Rename": await RenameAsync(item); break;
            case "Delete": await DeleteAsync(item); break;
        }
    }

    /// <summary>One chat: title and when, its swipe actions, menu and VoiceOver actions.</summary>
    private sealed class ChatRow : ContentView
    {
        private readonly IntelligenceChatsPage _page;
        private readonly Label _title = new() { FontFamily = "BodySemibold", LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 2 };
        private readonly Label _when = new();
        private readonly Grid _row = new()
        {
            ColumnDefinitions = [new(GridLength.Star)], RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto)],
            Padding = new Thickness(16, 10), MinimumHeightRequest = 56,
        };
        private readonly SwipeItem _pin = new();
        private readonly AccessibilityActions _pinAction;
        private readonly AccessibilityActions _unpinAction;
        private readonly AccessibilityActions _renameAction;
        private readonly AccessibilityActions _deleteAction;
        private IntelligenceChatItem? _item;

        public ChatRow(IntelligenceChatsPage page)
        {
            _page = page;
            _when.Style = ResourceStyle("Caption");
            _row.Add(_title, 0, 0);
            _row.Add(_when, 0, 1);
            _row.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => { if (_item is { } item) await _page.OpenAsync(item); }) });
            LongPress.SetCommand(_row, new Command(async () => { if (_item is { } item) await _page.ShowMenuAsync(item); }));

            _pin.BackgroundColor = ThemeColour("ButtonPrimary");
            _pin.Invoked += (_, _) => { if (_item is { } item) _page.TogglePin(item); };
            var rename = new SwipeItem { Text = "Rename", BackgroundColor = ThemeColour("InactiveColor") };
            rename.Invoked += async (_, _) => { if (_item is { } item) await _page.RenameAsync(item); };
            var delete = new SwipeItem { Text = "Delete", BackgroundColor = ThemeColour("CriticalTextLight") };
            delete.Invoked += async (_, _) => { if (_item is { } item) await _page.DeleteAsync(item); };

            // Revealing rather than acting, as Alerts' swipes do, so a stray
            // sideways scroll can't change anything; Delete still confirms.
            Content = new SwipeView
            {
                Threshold = 96,
                LeftItems = new SwipeItems([_pin]) { Mode = SwipeMode.Reveal, SwipeBehaviorOnInvoked = SwipeBehaviorOnInvoked.Close },
                RightItems = new SwipeItems([rename, delete]) { Mode = SwipeMode.Reveal, SwipeBehaviorOnInvoked = SwipeBehaviorOnInvoked.Close },
                Content = _row,
            };

            _pinAction = new AccessibilityActions(_row, "Pin", () => Act(item => _page.TogglePin(item)));
            _unpinAction = new AccessibilityActions(_row, "Unpin", () => Act(item => _page.TogglePin(item)));
            _renameAction = new AccessibilityActions(_row, "Rename", () => Act(item => _ = _page.RenameAsync(item)));
            _deleteAction = new AccessibilityActions(_row, "Delete", () => Act(item => _ = _page.DeleteAsync(item)));
            _row.HandlerChanged += (_, _) => RefreshActions();
            _row.SizeChanged += (_, _) => RefreshActions();
        }

        private bool Act(Action<IntelligenceChatItem> action)
        {
            if (_item is not { } item || !_page._chats.CanManage) return false;
            action(item);
            return true;
        }

        protected override void OnBindingContextChanged()
        {
            base.OnBindingContextChanged();
            Detach();
            _item = BindingContext as IntelligenceChatItem;
            if (_page._visible) Attach();
            Show();
        }

        internal void Attach()
        {
            if (_item is null) return;
            _item.PropertyChanged -= OnItemChanged;
            _item.PropertyChanged += OnItemChanged;
            Show();
        }

        internal void Detach()
        {
            if (_item is not null) _item.PropertyChanged -= OnItemChanged;
        }

        private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => Show();

        private void Show()
        {
            if (_item is not { } item) return;
            _title.Text = item.Title;
            _when.Text = (item.IsPinned ? "Pinned · " : "") + (item.IsOpen ? "Open now · " : "") + item.When;
            _pin.Text = item.PinText;
            SemanticProperties.SetDescription(_row, item.Description);
            SemanticProperties.SetHint(_row, "Opens this chat. Actions: " + item.PinText + ", Rename, Delete");
            if (item.IsOpen) _row.BackgroundColor = ThemeColour("PrimaryTint");
            else _row.ClearValue(BackgroundColorProperty);
            RefreshActions();
        }

        private void RefreshActions()
        {
            var pinned = _item?.IsPinned == true;
            var available = _item is not null;
            _pinAction.Refresh(available && !pinned);
            _unpinAction.Refresh(available && pinned);
            _renameAction.Refresh(available);
            _deleteAction.Refresh(available);
        }
    }

    private static Color ThemeColour(string key) => (Color)Application.Current!.Resources[key];

    private static Style ResourceStyle(string key) => (Style)Application.Current!.Resources[key];
}
