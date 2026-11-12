using System.ComponentModel;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Saved chats (#229): a modal sheet over the chat, grouped Pinned, Today,
/// Yesterday and so on by Core. The modal owns glass by the tab bar, with
/// New chat beside the title on large portrait screens as in experiment #594.
/// Each row opens its chat when tapped; swiping right pins, swiping left
/// reveals Rename and Delete, a long press opens the same actions as a menu
/// (TalkBack's double-tap-and-hold reaches it too), and VoiceOver offers them
/// as custom actions. Delete always confirms (#146).
/// </summary>
public sealed class IntelligenceChatsPage : ContentPage, IPrivateChatSurface, IActivePage
{
    private readonly IntelligenceChatListViewModel _chats;
    private readonly IntelligenceConversationViewModel _conversation;
    private readonly IDialogService _dialogs;
    private readonly Action _newChat;
    private readonly Label _status = new() { LineBreakMode = LineBreakMode.WordWrap };
    private readonly CollectionView _list = new() { IsGrouped = true, SelectionMode = SelectionMode.None };
    private readonly Button _new = new() { Text = "New chat", MinimumHeightRequest = 44 };
    private readonly Button _close = new() { Text = "Back", MinimumHeightRequest = 44, MinimumWidthRequest = 88, HorizontalOptions = LayoutOptions.Start };
#if IOS
    private readonly HeaderGlassGroup _headerGlass = new();
    private ChatActionPlace? _actionPlace;
    private bool? _canStart;
#endif
    private readonly Grid _titleLine = new() { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 12, SafeAreaEdges = SafeAreaEdges.None, MinimumHeightRequest = 44 };
    private readonly VerticalStackLayout _top = new() { Spacing = 8, SafeAreaEdges = SafeAreaEdges.None };
    private readonly BoxView _footer = new() { Color = Colors.Transparent, InputTransparent = true };
    private IDisposable? _edges;
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
#if IOS
        // Keep the presenting tab controller attached so its pill can be measured
        // through rotation. The opaque Chats page still covers the whole screen.
        SetValue(Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.Page.ModalPresentationStyleProperty,
            Microsoft.Maui.Controls.PlatformConfiguration.iOSSpecific.UIModalPresentationStyle.OverFullScreen);
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None);
#else
        SafeAreaEdges = SafeAreaEdges.All;
#endif
        this.SetAppThemeColor(BackgroundColorProperty, ThemeColour("BackgroundLight"), ThemeColour("BackgroundDark"));

        _close.Style = ResourceStyle("SecondaryButton");
        _new.Style = ResourceStyle("SecondaryButton");
        SemanticProperties.SetDescription(_close, "Back to Intelligence");
        SemanticProperties.SetDescription(_new, "Start a new chat");
        _close.Clicked += async (_, _) => await CloseAsync();
        _new.Clicked += (_, _) => StartNewChat();

        var title = new Label { Text = "Chats", Style = ResourceStyle("PageTitle"), LineBreakMode = LineBreakMode.WordWrap, VerticalOptions = LayoutOptions.Center };
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

        _titleLine.Add(title, 0, 0);
        var headerActions = new HorizontalStackLayout { Spacing = 8 };
        headerActions.Children.Add(_close);
        headerActions.Children.Add(_new);
#if IOS
        headerActions.Children.Add(_headerGlass);
#endif
        _titleLine.Add(headerActions, 1, 0);
        _top.Children.Add(_titleLine);
        _top.Children.Add(scope);
        _top.Children.Add(_status);
        _titleLine.Padding = new Thickness(0, TopBar.Offset, 0, 0);
        _top.Padding = new Thickness(16, 0, 16, 8);
        TopBar.Fit(_titleLine);

        _list.Header = new BoxView { Color = Colors.Transparent, HeightRequest = 20, InputTransparent = true };
        _list.Footer = _footer;
        var viewport = new Grid { SafeAreaEdges = SafeAreaEdges.None };
        viewport.Children.Add(_list);
        var fade = new BoxView { HeightRequest = 20, VerticalOptions = LayoutOptions.Start, InputTransparent = true };
        var start = new GradientStop { Offset = 0 };
        start.SetAppThemeColor(GradientStop.ColorProperty, ThemeColour("BackgroundLight"), ThemeColour("BackgroundDark"));
        var end = new GradientStop { Offset = 1 };
        end.SetAppThemeColor(GradientStop.ColorProperty, ThemeColour("BackgroundLightClear"), ThemeColour("BackgroundDarkClear"));
        fade.Background = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1), GradientStops = [start, end] };
        viewport.Children.Add(fade);
        Content = new SetupLayout { Header = _top, Body = viewport };
        _list.HandlerChanged += OnGeometryChanged;
        _list.Loaded += OnGeometryChanged;
        Update();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Activate();
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
        _edges?.Dispose();
        _edges = null;
#if IOS
        TabBarButtons.Moved -= OnGeometryChanged;
        TabBarButtons.Hide(this);
        _actionPlace = null;
#endif
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
        var canStart = !_conversation.IsRunning && _conversation.IsTranscriptCurrent;
        _new.IsEnabled = canStart;
        ShowActions();
    }

    /// <summary>Restores modal glass after resume without loading the list again.</summary>
    public void Activate()
    {
        if (!_visible)
        {
            _visible = true;
            _edges = CoveredEdges.Watch(this, PlaceControls);
#if IOS
            TabBarButtons.Moved -= OnGeometryChanged;
            TabBarButtons.Moved += OnGeometryChanged;
#endif
        }
#if IOS
        _actionPlace = null;
#endif
        ShowActions();
        Dispatcher.Dispatch(PlaceControls);
    }

    private void OnGeometryChanged(object? sender, EventArgs e) => Dispatcher.Dispatch(PlaceControls);

    private void ShowActions()
    {
        if (!_visible) return;
#if IOS
        var place = ChatActionPlacement.Resolve(TabBarButtons.IsAvailable, Width, Height);
        var canStart = !_conversation.IsRunning && _conversation.IsTranscriptCurrent;
        if (_actionPlace == place && _canStart == canStart) return;
        _actionPlace = place;
        _canStart = canStart;
        _close.IsVisible = _new.IsVisible = place == ChatActionPlace.HeaderIcons;
        _headerGlass.IsVisible = place == ChatActionPlace.HeaderGlass;
        GlassAction back = new("chevron.backward", "Back to Intelligence", () => _ = CloseAsync(), IsBack: true);
        GlassAction create = new("square.and.pencil", "New chat", StartNewChat, IsEnabled: canStart);
        _headerGlass.Actions = place == ChatActionPlace.HeaderGlass ? [create] : [];
        if (TabBarButtons.IsAvailable)
            TabBarButtons.Show(this, null, string.Empty,
                place == ChatActionPlace.HeaderGlass ? [back] : [back, create], this);
#endif
    }

    private void PlaceControls()
    {
        if (!_visible) return;
        ShowActions();
#if IOS
        if (TabDots.FindTabs() is { } tabs) TabBarButtons.Relayout(tabs);
#endif
        var clear = CoveredEdges.ForScrolling(this);
        _top.Padding = new Thickness(16 + clear.Left, 0, 16 + clear.Right, 8);
        _list.Margin = new Thickness(clear.Left, 0, clear.Right, 0);
        var footer = 24 + clear.Bottom;
#if IOS
        if (_list.Handler?.PlatformView is UIKit.UIView view)
        {
            if (FindScroll(view) is { } scroll)
                scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
            var pill = TabBarButtons.PillIn(view);
            var glass = TabBarButtons.Occupied(view)
                .Select(rect => new OccupiedArea(rect.Left, rect.Top, rect.Right, rect.Bottom)).ToArray();
            var gap = SetupActionPlacement.BottomGap(view.Bounds.Height, clear.Bottom,
                pill is { } bar ? new OccupiedArea(bar.Left, bar.Top, bar.Right, bar.Bottom) : null, glass);
            footer = 24 + gap;
        }
#endif
        if (_footer.HeightRequest != footer) _footer.HeightRequest = footer;
    }

#if IOS
    private static UIKit.UIScrollView? FindScroll(UIKit.UIView view) =>
        view as UIKit.UIScrollView ?? view.Subviews.Select(FindScroll).FirstOrDefault(found => found is not null);
#endif

    private async void StartNewChat()
    {
        if (_busy || _conversation.IsRunning || !_conversation.IsTranscriptCurrent) return;
        _busy = true;
        try
        {
            await CloseAsync();
            _newChat();
        }
        finally { _busy = false; }
    }

    private async Task CloseAsync()
    {
        if (!Navigation.ModalStack.Contains(this)) return;
        await Navigation.PopModalAsync();
        // OverFullScreen keeps the chat attached, so it may not get Appearing.
        if ((Navigation.ModalStack.LastOrDefault() ?? Shell.Current?.CurrentPage) is IActivePage page)
            page.Activate();
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
        private readonly SwipeView _swipe;
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
            _swipe = new SwipeView
            {
                // A short deliberate swipe settles open; it never invokes an action.
                Threshold = 48,
                LeftItems = new SwipeItems([_pin]) { Mode = SwipeMode.Reveal, SwipeBehaviorOnInvoked = SwipeBehaviorOnInvoked.Close },
                RightItems = new SwipeItems([rename, delete]) { Mode = SwipeMode.Reveal, SwipeBehaviorOnInvoked = SwipeBehaviorOnInvoked.Close },
                Content = _row,
            };

            var separator = new BoxView { HeightRequest = 1, InputTransparent = true, Margin = new Thickness(16, 0) };
            separator.SetAppThemeColor(BoxView.ColorProperty, ThemeColour("StrokeLight"), ThemeColour("StrokeDark"));
            var content = new Grid { RowDefinitions = [new(GridLength.Star), new(GridLength.Auto)] };
            content.Add(_swipe, 0, 0);
            content.Add(separator, 0, 1);
            Content = content;

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
            // A recycled row must not expose actions for the previous chat.
            _swipe.Close(false);
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
            if (item.IsOpen) _row.SetAppThemeColor(BackgroundColorProperty, ThemeColour("PrimaryTint"), ThemeColour("PrimaryTint"));
            else _row.SetAppThemeColor(BackgroundColorProperty, ThemeColour("BackgroundLight"), ThemeColour("BackgroundDark"));
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
