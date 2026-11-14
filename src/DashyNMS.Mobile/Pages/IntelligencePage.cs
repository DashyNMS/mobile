using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// The chat from More or a pinned tab, kept as a saved chat for the signed-in
/// server (#229) unless saving is off or unavailable; the list button opens the
/// saved chats. A finite transcript viewport and a measured composer share the
/// setup layout's keyboard and glass clearance, with the composer overlaid on the transcript.
/// Streaming labels are coalesced to ten updates a second rather than relaid out per token.
/// </summary>
public sealed class IntelligencePage : ContentPage, IActivePage, IPrivateChatSurface
{
    private readonly IntelligenceConversationViewModel _conversation;
    private readonly IntelligenceChatListViewModel _chats;
    private readonly IntelligencePreferences _preferences;
    private readonly IIntelligenceModel _model;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly IntelligenceAnswerActions _actions;
    private readonly IntelligenceAnswerReporter _reporter;
    private readonly SetupPageLayout _placement;
    private readonly PageHeader _header = new() { Title = "Intelligence" };
    private readonly ScrollView _scroll = new() { SafeAreaEdges = SafeAreaEdges.None };
    private readonly VerticalStackLayout _transcript = new() { Spacing = 16, Padding = new Thickness(0, 20, 0, 16) };
    private readonly VerticalStackLayout _starters = new() { Spacing = 8 };
    private readonly Label _notice = new();
    private readonly IconButton _jump = new() { Icon = "M12 4V20 M5 13 12 20 19 13", Kind = IconButtonKind.Filled, HorizontalOptions = LayoutOptions.End, IsVisible = false };
    private readonly Editor _question = new() { Placeholder = "Ask about your network…", MinimumHeightRequest = 48, MaximumHeightRequest = 140, AutoSize = EditorAutoSizeOption.TextChanges, BackgroundColor = Colors.Transparent };
    private readonly Picker _detail = new() { Title = "Answer detail", ItemsSource = new[] { "Brief", "Balanced", "Detailed" } };
    private readonly Button _ask = new() { Text = "", MinimumHeightRequest = 48, MinimumWidthRequest = 48, WidthRequest = 48, HeightRequest = 48, CornerRadius = 24, Padding = 0 };
    // Drawn over the native button, which keeps its semantics and disabled colours.
    private readonly Microsoft.Maui.Controls.Shapes.Path _askIcon = Glyph(SendIcon, 24);
    private readonly BoxView _fade = new() { InputTransparent = true, VerticalOptions = LayoutOptions.End, HeightRequest = 0 };
    private readonly Border _composer = new() { Padding = new Thickness(12, 8), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = 18 }, HorizontalOptions = LayoutOptions.Center };
    private readonly IconButton _new = new() { Icon = "M12 4V20 M4 12H20", Kind = IconButtonKind.Plain };
    private readonly IconButton _list = new() { Icon = "M9 6H20 M9 12H20 M9 18H20 M4 6H5 M4 12H5 M4 18H5", Kind = IconButtonKind.Plain };
    private readonly IDispatcherTimer _timer;
#if IOS
    // Saved chats and New chat beside the title on a big portrait screen (#594).
    private readonly HeaderGlassGroup _headerGlass = new() { IsVisible = false };
    private ChatActionPlace _place = ChatActionPlace.TabBarGlass;
#endif
    private readonly List<TurnCard> _cards = [];
    private long _chatRevision;
    private long _starterRevision = -1;
    private readonly VerticalStackLayout _overlay = new() { Spacing = 8, VerticalOptions = LayoutOptions.End, HorizontalOptions = LayoutOptions.Center };
    private bool _showing;
    private bool _following = true;
    private bool _scrollPending;
    private bool _scrolling;
    private bool _newChatPending;
    private bool _reporting;
    private bool _listOpening;
    private bool _showingDetail;
    private ChatComposerLane _lane;
    private double _columnLeft;
    private double _columnRightGutter;

    public IntelligencePage(IntelligenceConversationViewModel conversation, IntelligencePreferences preferences,
        IIntelligenceModel model, INavigationService navigation, IDialogService dialogs, IntelligenceAnswerActions actions,
        IntelligenceAnswerReporter reporter, IntelligenceChatListViewModel chats)
    {
        _conversation = conversation;
        _chats = chats;
        _chatRevision = conversation.ChatRevision;
        _preferences = preferences;
        _model = model;
        _navigation = navigation;
        _dialogs = dialogs;
        _actions = actions;
        _reporter = reporter;
        Title = "Intelligence";
        Shell.SetNavBarIsVisible(this, false);
        SemanticProperties.SetDescription(_question, "Question about your network");
        SemanticProperties.SetDescription(_detail, "Detail for the next answer");
        ShowDetail();
        _question.Text = conversation.Draft;
        _detail.SelectedIndexChanged += (_, _) =>
        {
            // The conversation owns the choice, so a saved chat keeps it (#229).
            // Only the user's own pick is recorded: showing the conversation's
            // detail must leave a chat that follows the Settings default following it.
            if (!_showingDetail && _detail.SelectedIndex >= 0) _conversation.SelectedDetail = DetailValue(_detail.SelectedIndex);
        };
        _notice.Style = ResourceStyle("Caption");
        _notice.HorizontalOptions = LayoutOptions.Center;
        SemanticProperties.SetDescription(_new, "New chat");
        SemanticProperties.SetDescription(_jump, "Jump to latest");
        SemanticProperties.SetDescription(_list, "Saved chats");
        _new.Command = new Command(RequestNewChat, () => !_conversation.IsRunning && !_newChatPending);
        _list.Command = new Command(OpenChats);
        _header.Actions = new HorizontalStackLayout { Children = { _list, _new } };
#if IOS
        ((HorizontalStackLayout)_header.Actions).Children.Add(_headerGlass);
        // Rotating, folding or resizing the window can move the actions between
        // the header and the tab bar; rebuild the glass only when they move.
        SizeChanged += (_, _) =>
        {
            if (_showing && ChatActionPlacement.Resolve(TabBarButtons.IsAvailable, Width, Height) != _place) ShowActions();
        };
#endif
        _scroll.Content = _transcript;
        _scroll.Scrolled += (_, e) =>
        {
            if (_scrolling) return;
            _following = ChatScrollFollow.IsNearEnd(e.ScrollY, _scroll.Height, _scroll.ContentSize.Height);
            _jump.IsVisible = !_following && _conversation.Turns.Count > 0;
        };
        _transcript.SizeChanged += (_, _) => { if (_following) _scrollPending = true; };
        _scroll.SizeChanged += (_, _) => { PlaceConversation(); if (_following) _scrollPending = true; };
        _jump.Command = new Command(() => { _following = true; _scrollPending = true; Flush(); });
        _ask.Clicked += async (_, _) =>
        {
            if (_conversation.IsRunning) _conversation.Stop();
            else await AskAsync();
        };
        _question.TextChanged += (_, e) =>
        {
            // The draft lives in the conversation: saved with the chat, cleared with it.
            if (_conversation.IsTranscriptCurrent) _conversation.Draft = e.NewTextValue ?? "";
            UpdateControls();
        };
        _transcript.HorizontalOptions = LayoutOptions.Center;
        _composer.SetAppThemeColor(BackgroundColorProperty, ThemeColour("SurfaceLight"), ThemeColour("SurfaceAltDark"));
        var composerStroke = new SolidColorBrush();
        composerStroke.SetAppThemeColor(SolidColorBrush.ColorProperty, ThemeColour("StrokeLight"), ThemeColour("StrokeDark"));
        _composer.Stroke = composerStroke;
        _detail.BackgroundColor = Colors.Transparent;
        _detail.MinimumHeightRequest = 44;
        _detail.MaximumWidthRequest = 150;
        _detail.FontSize = 14;
        _detail.Margin = new Thickness(0, 0, 18, 0);
        var composer = new Grid { SafeAreaEdges = SafeAreaEdges.None, RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto)], RowSpacing = 4 };
        composer.Add(_question, 0, 0);
        var choices = new Grid { SafeAreaEdges = SafeAreaEdges.None, ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 8 };
        choices.Add(DetailChip(), 0, 0);
        var askHost = new Grid { SafeAreaEdges = SafeAreaEdges.None, VerticalOptions = LayoutOptions.Center };
        askHost.Children.Add(_ask);
        askHost.Children.Add(_askIcon);
        _askIcon.Stroke = _ask.TextColor ?? Colors.White;
        _ask.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(Button.TextColor)) _askIcon.Stroke = _ask.TextColor ?? Colors.White; };
        choices.Add(askHost, 1, 0);
        composer.Add(choices, 0, 1);
        _composer.Content = composer;
        _composer.Margin = new Thickness(0, 8, 0, 0);
        var body = new Grid { SafeAreaEdges = SafeAreaEdges.None, RowDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)] };
        body.Add(_notice, 0, 0);
        // The page owns the backdrop so it also paints behind safe-area gutters,
        // header, composer and glass. Content keeps its existing inset ownership.
        // Opaque endpoints make the result independent of native host backgrounds.
        var glow = new RadialGradientBrush { Center = new Point(.35, .35), Radius = .85 };
        var glowStart = new GradientStop { Offset = 0 };
        glowStart.SetAppThemeColor(GradientStop.ColorProperty, Color.FromArgb("#E3EBF9"), Color.FromArgb("#162236"));
        var glowEnd = new GradientStop { Offset = 1 };
        glowEnd.SetAppThemeColor(GradientStop.ColorProperty, Color.FromArgb("#F4F6F9"), Color.FromArgb("#11141A"));
        glow.GradientStops = [glowStart, glowEnd];
        Background = glow;
        body.Add(_scroll, 0, 1);
        // Messages passing behind the composer and tab bar fade out rather than
        // running into the composer's edge. Decorative; it never takes touches.
        var fadeTop = new GradientStop { Offset = 0 };
        fadeTop.SetAppThemeColor(GradientStop.ColorProperty, Color.FromArgb("#00F4F6F9"), Color.FromArgb("#0011141A"));
        var fadeBottom = new GradientStop { Offset = .45f };
        fadeBottom.SetAppThemeColor(GradientStop.ColorProperty, Color.FromArgb("#EBF4F6F9"), Color.FromArgb("#EB11141A"));
        _fade.Background = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1), GradientStops = [fadeTop, fadeBottom] };
        body.Add(_fade, 0, 1);
        // Keep the arrow slot measured when hidden, so following does not change the footer.
        var jumpSlot = new Grid { HeightRequest = 44, SafeAreaEdges = SafeAreaEdges.None };
        jumpSlot.Children.Add(_jump);
        _overlay.Children.Add(jumpSlot);
        _overlay.Children.Add(_composer);
        body.Add(_overlay, 0, 1);
        // The placement helper measures the keyboard/glass foot once; the page
        // applies it to the overlay, so the transcript runs to the bottom of the
        // screen and messages scroll behind the composer and the tab bar.
        _overlay.SizeChanged += (_, _) => ReserveFoot();
        var setup = new SetupLayout { Header = _header, Body = body };
        Content = setup;
        _placement = new SetupPageLayout(this, setup, _header, _scroll) { ShowActions = ShowActions, PlaceFoot = PlaceFoot };
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(100);
        _timer.Tick += (_, _) => Flush();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Activate();
    }

    /// <summary>Reclaims glass and availability on retained-tab return and resume, without clearing text.</summary>
    public void Activate()
    {
        if (!_showing)
        {
            _showing = true;
            _conversation.PropertyChanged += OnConversationChanged;
            _conversation.Turns.CollectionChanged += OnTurnsChanged;
            _chats.PropertyChanged += OnChatsChanged;
            if (_cards.Count != _conversation.Turns.Count || _cards.Where((card, index) => !ReferenceEquals(card.Turn, _conversation.Turns[index])).Any()
                || _transcript.Children.Count == 0) Rebuild();
            else
            {
                RefreshStarters();
                foreach (var card in _cards) card.Resume();
            }
            _timer.Start();
        }
        _placement.Activate();
        SynchroniseComposer();
        UpdateControls();
        ShowActions();
        PlaceConversation();
        // This server's saved chats, read off the UI thread; reopens the last one into an empty chat.
        _ = _chats.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        _showing = false;
        _timer.Stop();
        _conversation.PropertyChanged -= OnConversationChanged;
        _conversation.Turns.CollectionChanged -= OnTurnsChanged;
        _chats.PropertyChanged -= OnChatsChanged;
        foreach (var card in _cards) card.Dispose();
        base.OnDisappearing();
    }

    private void OnConversationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_showing) return;
        // Replacement publishes this only after draft and detail are installed.
        if (e.PropertyName == nameof(IntelligenceConversationViewModel.ChatRevision)) { SynchroniseComposer(); RefreshStarters(); }
        UpdateControls();
        if (e.PropertyName == nameof(_conversation.IsRunning)) ShowActions();
    }

    private void OnChatsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_showing) UpdateControls();
    }

    private void OnTurnsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
        {
            _starters.IsVisible = false;
            foreach (IntelligenceTurn turn in e.NewItems) AddTurn(turn);
        }
        else Rebuild();
        if (_following) _scrollPending = true;
    }

    private void Rebuild()
    {
        foreach (var card in _cards) card.Dispose();
        _cards.Clear();
        _transcript.Children.Clear();
        RefreshStarters();
        _transcript.Children.Add(_starters);
        foreach (var turn in _conversation.Turns) AddTurn(turn);
        _scrollPending = _following;
    }

    private void RefreshStarters()
    {
        if (_starterRevision == _conversation.ChatRevision) return;
        _starterRevision = _conversation.ChatRevision;
        _starters.Children.Clear();
        var heading = new Label { Text = "What would you like to know?", Style = ResourceStyle("SectionHeader") };
        // This heading sits on the accent wash rather than a neutral surface.
        heading.SetAppThemeColor(Label.TextColorProperty, ThemeColour("TextSecondaryLight"), ThemeColour("TextSecondaryDark"));
        _starters.Children.Add(heading);
        foreach (var starter in _conversation.StarterQuestions)
        {
            var revision = _conversation.ChatRevision;
            var button = new ChatActionButton { Text = starter, MinimumHeightRequest = 44, CornerRadius = 22, BorderWidth = 1 };
            button.SetAppThemeColor(Button.BorderColorProperty, ThemeColour("PrimaryTextLight"), ThemeColour("PrimaryTextDark"));
            button.Clicked += async (_, _) =>
            {
                if (!_showing || Shell.Current?.CurrentPage != this || !string.IsNullOrWhiteSpace(_question.Text)) return;
                _following = true;
                await _conversation.AskStarterAsync(starter, revision);
                Flush();
            };
            _starters.Children.Add(button);
        }
        _starters.IsVisible = _conversation.Turns.Count == 0;
    }

    private void AddTurn(IntelligenceTurn turn)
    {
        var card = new TurnCard(turn, this);
        _cards.Add(card);
        _transcript.Children.Add(card);
        if (_scroll.Width > 0) card.Place(Math.Min(860, _scroll.Width));
    }

    /// <summary>Use the measured, already guttered viewport for one readable column on any display.</summary>
    private void PlaceConversation()
    {
        if (!_showing || Shell.Current?.CurrentPage != this || _scroll.Width <= 0) return;
        var width = Math.Min(860, _scroll.Width);
        _transcript.WidthRequest = width;
        _notice.WidthRequest = width;
        // Beside a bottom tab bar the composer keeps to its free lane, level with the bar's base.
        if (_lane is { LaneLeft: { } left, LaneRight: { } right })
        {
            var laneWidth = Math.Min(860, right - left);
            _overlay.HorizontalOptions = LayoutOptions.Start;
            _overlay.Margin = new Thickness(Math.Max(0, left - _columnLeft + (right - left - laneWidth) / 2), 0, 0, _lane.Gap);
            _composer.WidthRequest = _overlay.WidthRequest = laneWidth;
        }
        else
        {
            _overlay.HorizontalOptions = LayoutOptions.Center;
            _overlay.Margin = new Thickness(0, 0, 0, _lane.Gap);
            _composer.WidthRequest = _overlay.WidthRequest = width;
        }
        foreach (var card in _cards) card.Place(width);
        ReserveFoot();
    }

    /// <summary>Takes the measured foot from the shared placement helper and fits the composer to it.</summary>
    private void PlaceFoot(SetupFoot foot)
    {
        var lane = ChatComposerPlacement.Resolve(foot.Width, foot.Height, foot.SafeBottom, foot.ColumnLeft, foot.ColumnRight,
            foot.Pill, foot.Glass, foot.KeyboardTop);
        var rightGutter = foot.Width - foot.ColumnRight;
        if (lane == _lane && foot.ColumnLeft == _columnLeft && rightGutter == _columnRightGutter) return;
        _lane = lane;
        _columnLeft = foot.ColumnLeft;
        _columnRightGutter = rightGutter;
        // The fade spans the whole screen, through the layout's side gutters.
        _fade.Margin = new Thickness(-_columnLeft, 0, -_columnRightGutter, 0);
        PlaceConversation();
    }

    /// <summary>
    /// Leaves room under the last message for the composer and whatever covers
    /// the page's foot (tab bar, glass or keyboard), whichever reaches higher.
    /// The hidden Jump to latest slot keeps this stable while following.
    /// </summary>
    private void ReserveFoot()
    {
        var reserve = Math.Max(Math.Max(0, _overlay.Height) + _lane.Gap, _lane.Clearance) + 16;
        // A docked keyboard in a short landscape window can cover more than the
        // viewport; keep the last message's end inside it rather than above it.
        if (_scroll.Height > 0) reserve = Math.Min(reserve, Math.Max(0, _scroll.Height - 64));
        // From a little above the composer (not the Jump to latest slot) to the screen's base.
        var fade = Math.Max(Math.Max(0, _composer.Height) + _lane.Gap, _lane.Clearance) + 24;
        if (_fade.HeightRequest != fade) _fade.HeightRequest = fade;
        var padding = new Thickness(0, 20, 0, reserve);
        if (_transcript.Padding == padding) return;
        _transcript.Padding = padding;
        if (_following) _scrollPending = true;
    }

    private void SynchroniseComposer()
    {
        if (_chatRevision != _conversation.ChatRevision)
        {
            _chatRevision = _conversation.ChatRevision;
            // A new, reopened or cleared chat brings its own draft and detail.
            _question.Text = _conversation.Draft;
            _following = true;
            _scrollPending = true;
        }

        // Every time, not only for another chat: the Settings default may have
        // changed while this page was away, and a chat without its own pick follows it.
        ShowDetail();
    }

    /// <summary>Shows the conversation's detail without recording it as the chat's own choice.</summary>
    private void ShowDetail()
    {
        _showingDetail = true;
        try { _detail.SelectedIndex = DetailIndex(_conversation.SelectedDetail); }
        finally { _showingDetail = false; }
    }

    private void UpdateControls()
    {
        _transcript.IsVisible = _question.IsVisible = _detail.IsVisible = _conversation.IsTranscriptCurrent;
        _askIcon.Data = IconData(_conversation.IsRunning ? StopIcon : SendIcon);
        SemanticProperties.SetDescription(_ask, _conversation.IsRunning ? "Stop answer" : "Send question");
        _ask.IsEnabled = _conversation.IsRunning || (_conversation.CanAsk && !string.IsNullOrWhiteSpace(_question.Text));
        _question.IsEnabled = !_conversation.IsRunning;
        _detail.IsEnabled = !_conversation.IsRunning;
        _starters.IsEnabled = _conversation.CanAsk;
        _starters.IsVisible = _conversation.Turns.Count == 0 && string.IsNullOrWhiteSpace(_question.Text);
        var notices = string.Join("\n", new[] { _chats.Status, _conversation.Notice }.Where(text => !string.IsNullOrEmpty(text)));
        _notice.Text = notices.Length > 0 ? notices
            : !_preferences.Enabled ? "Turn on Intelligence in Settings to ask a question."
            : _model.Availability != IntelligenceAvailability.Available ? IntelligenceAvailabilityText.Describe(_model.Availability)
            : "Answers can be wrong. Lookups may use cached data; check readings and their age in LibreNMS.";
        (_new.Command as Command)?.ChangeCanExecute();
        foreach (var card in _cards) card.UpdateActions();
    }

    private async Task AskAsync()
    {
        var question = _question.Text ?? "";
        var count = _conversation.Turns.Count;
        _following = true;
        // Null: the conversation's own detail, which the picker shows - the chat's pick or the current default.
        var task = _conversation.AskAsync(question);
        if (_conversation.Turns.Count > count) _question.Text = "";
        await task;
        Flush();
    }

    private void Flush()
    {
        if (!_showing || Shell.Current?.CurrentPage != this) return;
        foreach (var card in _cards) { card.Render(); card.Tick(); }
        if (_scrollPending && _following && !_scrolling)
        {
            _scrollPending = false;
            _ = ScrollToEndAsync();
        }
    }

    private async Task ScrollToEndAsync()
    {
        _scrolling = true;
        try
        {
            await _scroll.ScrollToAsync(0, Math.Max(0, _scroll.ContentSize.Height - _scroll.Height), false);
            _jump.IsVisible = false;
        }
        finally { _scrolling = false; }
    }

    /// <summary>
    /// New chat goes through the list's shared leave decision
    /// (<see cref="IntelligenceChatListViewModel.NewChatAsync"/>), as opening a
    /// saved chat does: a chat whose latest text is confirmed saved is left at once,
    /// after a wait of at most two seconds; otherwise the user chooses to try
    /// again, discard it or cancel. Stop remains a separate action.
    /// </summary>
    public async void RequestNewChat()
    {
        if (_conversation.IsRunning || _newChatPending) return;
        _newChatPending = true;
        UpdateControls();
        ShowActions();
        try
        {
            if (await _chats.NewChatAsync())
            {
                _question.Text = "";
                _following = true;
                _scrollPending = true;
            }
        }
        finally { _newChatPending = false; UpdateControls(); ShowActions(); }
    }

    /// <summary>The saved-chat list, as a modal sheet over the chat.</summary>
    private async void OpenChats()
    {
        if (_listOpening || Navigation is not { } navigation) return;
        _listOpening = true;
        try { await navigation.PushModalAsync(new IntelligenceChatsPage(_chats, _conversation, _dialogs, RequestNewChat)); }
        catch { await _dialogs.AlertAsync("Couldn't open saved chats", "Try again."); }
        finally { _listOpening = false; }
    }

    private static int DetailIndex(string detail) => detail switch { "brief" => 0, "detailed" => 2, _ => 1 };

    private static string DetailValue(int index) => index switch { 0 => "brief", 2 => "detailed", _ => "balanced" };

    /// <summary>
    /// Report this answer (#574): choose a reason, then preview exactly what the
    /// email will carry. Ownership is checked again after each step, so a chat
    /// that changes meanwhile opens nothing.
    /// </summary>
    private async Task ReportAsync(IntelligenceTurn turn)
    {
        if (_reporting) return;
        _reporting = true;
        try
        {
            var report = await _reporter.ChooseReasonAsync(turn);
            if (report is null || Navigation is not { } navigation) return;
            if (!report.CheckOwnership())
            {
                await _dialogs.AlertAsync("This answer can't be reported", IntelligenceReportViewModel.ExpiredMessage);
                return;
            }
            await navigation.PushModalAsync(new IntelligenceReportPage(report));
        }
        catch { await _dialogs.AlertAsync("Couldn't open the report", "Try again."); }
        finally { _reporting = false; }
    }

    private void ShowActions()
    {
        if (!_showing || Shell.Current?.CurrentPage != this || Navigation.ModalStack.Count > 0) return;
#if IOS
        _place = ChatActionPlacement.Resolve(TabBarButtons.IsAvailable, Width, Height);
        GlassAction[] chatActions =
        [
            new("list.bullet", "Saved chats", OpenChats),
            new("square.and.pencil", "New chat", RequestNewChat, IsEnabled: !_conversation.IsRunning && !_newChatPending),
        ];
        var inHeader = _place == ChatActionPlace.HeaderGlass;
        _new.IsVisible = _list.IsVisible = _place == ChatActionPlace.HeaderIcons;
        _headerGlass.Actions = inHeader ? chatActions : [];
        _headerGlass.IsVisible = inHeader;
        if (TabBarButtons.IsAvailable)
        {
            // Back stays by the tab bar wherever the chat's actions are. Shown
            // even when empty, so this page owns the glass and no earlier
            // page's actions are left behind.
            var actions = new List<GlassAction>();
            if (PageHeader.IsPushed(this)) actions.Add(new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => _ = Shell.Current.GoToAsync(".."), IsBack: true));
            if (!inHeader) actions.AddRange(chatActions);
            TabBarButtons.Show(this, null, string.Empty, actions);
        }
#endif
    }

    private static Color ThemeColour(string key) => (Color)Application.Current!.Resources[key];

    private const string SendIcon = "M12 19V5 M6 11 12 5 18 11";
    private const string StopIcon = "M8 8H16V16H8Z";
    private const string ChevronDown = "M6 9 12 15 18 9";
    private const string ChevronUp = "M6 15 12 9 18 15";
    private const string TickIcon = "M5 12.5 10 17 19 7";
    private const string WarningIcon = "M12 4 21 20H3Z M12 10V14 M12 17V17.2";
    private const string StoppedIcon = "M8 8H16V16H8Z";

    private static Geometry? IconData(string data) => (Geometry?)new PathGeometryConverter().ConvertFromInvariantString(data);

    /// <summary>A decorative line icon on the 24px grid, drawn like <see cref="IconButton"/>'s.</summary>
    private static Microsoft.Maui.Controls.Shapes.Path Glyph(string data, double size)
    {
        var glyph = new Microsoft.Maui.Controls.Shapes.Path
        {
            Data = IconData(data), StrokeThickness = 2, StrokeLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
            WidthRequest = 24, HeightRequest = 24, Scale = size / 24, Aspect = Stretch.None, InputTransparent = true,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
        };
        AutomationProperties.SetIsInAccessibleTree(glyph, false);
        return glyph;
    }

    /// <summary>
    /// The answer detail as a compact outlined chip with a chevron, so it reads as
    /// a choice beside Send rather than loose text. The native picker stays the
    /// tap target and keeps its accessible name.
    /// </summary>
    private Border DetailChip()
    {
        var chevron = Glyph(ChevronDown, 16);
        chevron.HorizontalOptions = LayoutOptions.End;
        chevron.Stroke = ThemedBrush("TextSecondaryLight", "TextSecondaryDark");
        var content = new Grid { SafeAreaEdges = SafeAreaEdges.None };
        content.Children.Add(_detail);
        content.Children.Add(chevron);
        var chip = new Border
        {
            Content = content, Padding = new Thickness(12, 0, 8, 0), StrokeThickness = 1, MinimumHeightRequest = 44,
            StrokeShape = new RoundRectangle { CornerRadius = 22 }, HorizontalOptions = LayoutOptions.Start, VerticalOptions = LayoutOptions.Center,
            BackgroundColor = Colors.Transparent,
        };
        chip.Stroke = ThemedBrush("StrokeLight", "StrokeDark");
        return chip;
    }

    private static SolidColorBrush ThemedBrush(string light, string dark)
    {
        var brush = new SolidColorBrush();
        brush.SetAppThemeColor(SolidColorBrush.ColorProperty, ThemeColour(light), ThemeColour(dark));
        return brush;
    }

    private static Style ResourceStyle(string key) => (Style)Application.Current!.Resources[key];

    /// <summary>Keep disabled secondary actions readable in either theme.</summary>
    private sealed class ChatActionButton : Button
    {
        public ChatActionButton() => Style = ResourceStyle("SecondaryButton");

        protected override void OnPropertyChanged(string? propertyName = null)
        {
            base.OnPropertyChanged(propertyName);
            if (propertyName != nameof(IsEnabled)) return;
            if (IsEnabled)
            {
                ClearValue(TextColorProperty);
                ClearValue(BackgroundColorProperty);
            }
            else
            {
                var resources = Application.Current!.Resources;
                this.SetAppThemeColor(TextColorProperty, (Color)resources["Gray950"], (Color)resources["Gray200"]);
                this.SetAppThemeColor(BackgroundColorProperty, (Color)resources["Gray200"], (Color)resources["Gray600"]);
            }
        }
    }

    /// <summary>
    /// Answers render as Markdown spans and native views, never as HTML. Each retained
    /// attempt owns its subscriptions while visible. References, links and images act only
    /// while the turn still belongs to the current connection.
    /// </summary>
    private sealed class TurnCard : VerticalStackLayout, IDisposable
    {
        private readonly IntelligenceTurn _turn;
        private readonly IntelligencePage _page;
        private readonly Border _questionBubble;
        private readonly IconButton _activity = new() { Icon = ChevronDown, Kind = IconButtonKind.Filled, HorizontalOptions = LayoutOptions.Start };
        private readonly VerticalStackLayout _steps = new() { Spacing = 2, IsVisible = false };
        private readonly ActivityIndicator _working = new() { WidthRequest = 16, HeightRequest = 16, VerticalOptions = LayoutOptions.Center };
        private readonly HorizontalStackLayout _stateLine = new() { Spacing = 8 };
        private readonly Label _elapsed = new() { VerticalOptions = LayoutOptions.Center, IsVisible = false };
        private string? _stepsShown;
        private bool _motionReduced = ReducedMotion.IsOn;
        private bool _activityExpanded;
        private readonly IntelligenceMarkdownView _answer = new();
        private readonly FlexLayout _images = new() { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, IsVisible = false };
        private IReadOnlyList<DesktopNMS.Core.Intelligence.IntelligenceImage>? _shownImages;
        private readonly Label _state = new();
        private readonly Label _omitted = new() { Text = "Earlier turns were left out of this answer's context." };
        private readonly IconButton _copy = new() { Icon = "M9 5H7V21H21V5H19 M10 3H18V7H10Z", Kind = IconButtonKind.Filled };
        private readonly Button _retry = new ChatActionButton() { Text = "Retry" };
        private readonly IconButton _edit = new() { Icon = "M4 16 16 4 20 8 8 20H4Z M14 6 18 10", Kind = IconButtonKind.Filled };
        private readonly AccessibilityActions _reportAction;
        private bool _dirty = true;
        private readonly List<IntelligenceLookupProgress> _watched = [];

        public TurnCard(IntelligenceTurn turn, IntelligencePage page)
        {
            _turn = turn;
            _page = page;
            Spacing = 12;
            var question = new Label { Text = turn.Question, LineBreakMode = LineBreakMode.WordWrap };
            SemanticProperties.SetDescription(question, "You. " + turn.Question);
            _questionBubble = new Border
            {
                Content = question, Padding = new Thickness(14, 10), StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 16 }, HorizontalOptions = LayoutOptions.End,
            };
            _questionBubble.SetAppThemeColor(BackgroundColorProperty, ThemeColour("SurfaceAltLight"), ThemeColour("SurfaceAltDark"));
            Children.Add(_questionBubble);
            var content = new VerticalStackLayout { Spacing = 8 };
            _activity.Command = new Command(() => { _activityExpanded = !_activityExpanded; _dirty = true; Render(); });
            _activity.StrokeShape = new RoundRectangle { CornerRadius = 18 };
            content.Children.Add(_activity);
            content.Children.Add(_steps);
            content.Children.Add(_answer);
            content.Children.Add(_images);
            _answer.ReferenceRequested += async (_, reference) =>
            {
                try
                {
                    if (!await _page._actions.OpenAsync(_turn, reference))
                        SemanticScreenReader.Default.Announce("That item can't be opened from this chat.");
                }
                catch { await _page._dialogs.AlertAsync("Couldn't open the item", "Try again, or open its source page from More."); }
            };
            _answer.LinkRequested += async (_, link) =>
            {
                try
                {
                    if (!await _page._actions.OpenLinkAsync(_turn, link))
                        SemanticScreenReader.Default.Announce("That link can't be opened from this chat.");
                }
                catch { await _page._dialogs.AlertAsync("Couldn't open the link", "Try again, or copy the answer to open it elsewhere."); }
            };
            _state.Style = _omitted.Style = _elapsed.Style = ResourceStyle("Caption");
            _state.VerticalOptions = LayoutOptions.Center;
            _working.SetAppThemeColor(ActivityIndicator.ColorProperty, ThemeColour("PrimaryTextLight"), ThemeColour("PrimaryTextDark"));
            content.Children.Add(_omitted);
            _stateLine.Children.Add(_working);
            _stateLine.Children.Add(_state);
            content.Children.Add(_stateLine);
            var actions = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.End };
            _retry.MinimumHeightRequest = 44;
            actions.Children.Add(_elapsed);
            actions.Children.Add(_retry);
            actions.Children.Add(_copy);
            content.Children.Add(actions);
            // Edit belongs to the user's question rather than the generated answer.
            _edit.HorizontalOptions = LayoutOptions.End;
            Children.Insert(1, _edit);
            var reply = new Border
            {
                Content = content, Padding = 14, StrokeThickness = 1,
                StrokeShape = new RoundRectangle { CornerRadius = 14 }, Margin = new Thickness(0, 0, 0, 12),
            };
            reply.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#ECF3FC"), Color.FromArgb("#0A1526"));
            var replyStroke = new SolidColorBrush();
            replyStroke.SetAppThemeColor(SolidColorBrush.ColorProperty, Color.FromArgb("#CCDDF1"), Color.FromArgb("#20334B"));
            reply.Stroke = replyStroke;
            Children.Add(reply);
            // Touch reaches Copy and Report through a long press on the answer;
            // VoiceOver offers Report as a custom action on every part of it.
            LongPress.SetCommand(reply, new Command(async () => await ShowAnswerMenuAsync(), () => _page._conversation.OwnsTurn(_turn) && !string.IsNullOrWhiteSpace(_turn.Answer)));
            _reportAction = new AccessibilityActions(reply, "Report this answer", () =>
            {
                if (!_page._reporter.CanReport(_turn)) return false;
                _ = _page.ReportAsync(_turn);
                return true;
            });
            reply.HandlerChanged += (_, _) => RefreshReportAction();
            reply.SizeChanged += (_, _) => RefreshReportAction();
            SemanticProperties.SetDescription(_copy, "Copy answer to: " + turn.Question);
            SemanticProperties.SetDescription(_retry, "Retry: " + turn.Question);
            SemanticProperties.SetDescription(_edit, "Edit question: " + turn.Question);
            _copy.Command = new Command(async () => await CopyAsync(), () => _page._conversation.OwnsTurn(_turn) && !string.IsNullOrWhiteSpace(_turn.Answer));
            _retry.Clicked += async (_, _) => { if (_page._conversation.CanAsk) { _page._following = true; await _page._conversation.RetryAsync(_turn); } };
            _edit.Command = new Command(() =>
            {
                if (!_page._conversation.TryEditQuestion(_turn)) return;
                _page._question.Text = _page._conversation.Draft;
                _page._question.Focus();
            }, () => _page._conversation.CanEditQuestion(_turn));
            Resume();
            Render();
        }

        private async Task CopyAsync()
        {
            if (!_page._conversation.OwnsTurn(_turn) || string.IsNullOrWhiteSpace(_turn.Answer)) return;
            // The original Markdown, markers included, so pasting into desktop or notes keeps its structure.
            try { await Clipboard.Default.SetTextAsync(_turn.Answer); SemanticScreenReader.Default.Announce("Answer copied"); }
            catch { await _page._dialogs.AlertAsync("Couldn't copy the answer", "Try again."); }
        }

        /// <summary>The long-press menu: only what this answer can do right now.</summary>
        private async Task ShowAnswerMenuAsync()
        {
            if (!_page._conversation.OwnsTurn(_turn)) return;
            var options = new List<string>();
            if (!string.IsNullOrWhiteSpace(_turn.Answer)) options.Add("Copy answer");
            if (_page._reporter.CanReport(_turn)) options.Add("Report this answer");
            if (options.Count == 0) return;
            var choice = await _page._dialogs.ChooseAsync("Answer", options);
            if (choice == "Copy answer") await CopyAsync();
            else if (choice == "Report this answer") await _page.ReportAsync(_turn);
        }

        private void RefreshReportAction() => _reportAction.Refresh(_page._reporter.CanReport(_turn));

        internal IntelligenceTurn Turn => _turn;
        internal void Place(double width) => _questionBubble.MaximumWidthRequest = Math.Min(560, width * .85);
        internal void Resume()
        {
            _turn.PropertyChanged -= OnChanged;
            _turn.PropertyChanged += OnChanged;
            _turn.Lookups.CollectionChanged -= OnLookupsChanged;
            _turn.Lookups.CollectionChanged += OnLookupsChanged;
            WatchLookups();
            _dirty = true;
        }
        private void OnChanged(object? sender, PropertyChangedEventArgs e) => _dirty = true;
        private void OnLookupsChanged(object? sender, NotifyCollectionChangedEventArgs e) { WatchLookups(); _dirty = true; }
        private void WatchLookups()
        {
            foreach (var lookup in _watched) lookup.PropertyChanged -= OnChanged;
            _watched.Clear();
            foreach (var lookup in _turn.Lookups) { _watched.Add(lookup); lookup.PropertyChanged += OnChanged; }
        }
        internal void Render()
        {
            if (!_dirty) return;
            _dirty = false;
            _answer.Render(_turn.Answer, _turn.References, _page._preferences.ColourStatusIndicators);
            RenderImages();
            var running = _turn.State == IntelligenceTurnState.Running;
            _state.Text = running ? IntelligenceElapsed.Running(_turn.AskedAt, DateTimeOffset.UtcNow) : _turn.Message;
            // A spinner while it works; Reduce Motion keeps the counting text alone.
            _motionReduced = ReducedMotion.IsOn;
            _working.IsVisible = _working.IsRunning = running && !_motionReduced;
            _elapsed.Text = IntelligenceElapsed.Answered(_turn) ?? "";
            _elapsed.IsVisible = _elapsed.Text.Length > 0;
            var count = _turn.Lookups.Count;
            var working = _turn.Lookups.Any(lookup => lookup.State == IntelligenceLookupState.Running);
            var failed = _turn.Lookups.Any(lookup => lookup.State == IntelligenceLookupState.Failed);
            var stopped = _turn.Lookups.Any(lookup => lookup.State == IntelligenceLookupState.Stopped);
            _activity.IsVisible = count > 0;
            var outcome = working ? "Looking up…" : failed ? "Warning" : stopped ? "Stopped" : "Done";
            _activity.Text = $"{count} {(count == 1 ? "lookup" : "lookups")} · {outcome}";
            _activity.Icon = _activityExpanded ? ChevronUp : ChevronDown;
            SemanticProperties.SetDescription(_activity, $"{(_activityExpanded ? "Hide" : "Show")} lookup details. {count} {(count == 1 ? "lookup" : "lookups")}. {outcome}");
            _steps.IsVisible = count > 0 && _activityExpanded;
            if (_steps.IsVisible) RenderSteps();
            // The activity summary already conveys success. Keep exceptional outcomes explicit.
            _stateLine.IsVisible = _state.IsVisible = _turn.State != IntelligenceTurnState.Completed || _turn.Message != "Completed";
            _omitted.IsVisible = _turn.HistoryOmitted;
            UpdateActions();
        }
        internal void UpdateActions()
        {
            ((Command)_copy.Command).ChangeCanExecute();
            var retryable = _turn.State is IntelligenceTurnState.Failed or IntelligenceTurnState.Stopped or IntelligenceTurnState.Interrupted;
            _retry.IsVisible = retryable;
            _edit.IsVisible = true;
            _retry.IsEnabled = _page._conversation.CanAsk && _page._conversation.OwnsTurn(_turn);
            ((Command)_edit.Command).ChangeCanExecute();
            RefreshReportAction();
        }
        /// <summary>Counts a running answer's seconds; Render only redraws on a change from the turn.</summary>
        internal void Tick()
        {
            if (_turn.State != IntelligenceTurnState.Running) return;
            // Spinners only show while running, so that is when Reduce Motion matters;
            // it can change while the app is away without the card being resumed.
            if (ReducedMotion.IsOn != _motionReduced)
            {
                _dirty = true;
                Render();
            }
            var text = IntelligenceElapsed.Running(_turn.AskedAt, DateTimeOffset.UtcNow);
            if (_state.Text != text) _state.Text = text;
        }

        /// <summary>
        /// One row per lookup with an icon for its state, rebuilt only when a name or
        /// state changes. Each row is read as one element: "Find device: Completed".
        /// </summary>
        private void RenderSteps()
        {
            _motionReduced = ReducedMotion.IsOn;
            var signature = _motionReduced + "|" + string.Join("|", _turn.Lookups.Select(lookup => lookup.Name + ":" + lookup.State));
            if (signature == _stepsShown) return;
            _stepsShown = signature;
            _steps.Children.Clear();
            foreach (var lookup in _turn.Lookups)
            {
                var name = LookupName(lookup.Name);
                var (state, icon, light, dark) = lookup.State switch
                {
                    IntelligenceLookupState.Completed => ("Completed", TickIcon, "OkTextLight", "OkTextDark"),
                    IntelligenceLookupState.Failed => ("Failed", WarningIcon, "WarningTextLight", "WarningTextDark"),
                    IntelligenceLookupState.Stopped => ("Stopped", StoppedIcon, "TextSecondaryLight", "TextSecondaryDark"),
                    _ => ("Looking up…", (string?)null, "PrimaryTextLight", "PrimaryTextDark"),
                };
                var row = new Grid { ColumnDefinitions = [new(new GridLength(24)), new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 8, MinimumHeightRequest = 28 };
                View marker;
                if (icon is null)
                {
                    var spinner = new ActivityIndicator { WidthRequest = 14, HeightRequest = 14, IsRunning = !ReducedMotion.IsOn, IsVisible = !ReducedMotion.IsOn };
                    spinner.SetAppThemeColor(ActivityIndicator.ColorProperty, ThemeColour(light), ThemeColour(dark));
                    marker = spinner;
                }
                else
                {
                    var glyph = Glyph(icon, 16);
                    glyph.Stroke = ThemedBrush(light, dark);
                    marker = glyph;
                }
                AutomationProperties.SetIsInAccessibleTree(marker, false);
                row.Add(marker, 0, 0);
                var label = new Label { Text = name, FontSize = 14, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.WordWrap };
                row.Add(label, 1, 0);
                var stateLabel = new Label { Text = state, Style = ResourceStyle("Caption"), VerticalOptions = LayoutOptions.Center };
                row.Add(stateLabel, 2, 0);
                AutomationProperties.SetIsInAccessibleTree(label, false);
                AutomationProperties.SetIsInAccessibleTree(stateLabel, false);
                SemanticProperties.SetDescription(row, $"{name}: {state}");
                AutomationProperties.SetIsInAccessibleTree(row, true);
                _steps.Children.Add(row);
            }
        }

        /// <summary>Images come only from the answer's typed attachments, already size and type checked.</summary>
        private void RenderImages()
        {
            if (ReferenceEquals(_shownImages, _turn.Images)) return;
            _shownImages = _turn.Images;
            _images.Children.Clear();
            var images = _turn.Images;
            for (var i = 0; i < images.Count; i++)
            {
                var image = images[i];
                var label = images.Count == 1 ? "Graph image" : $"Graph image {i + 1} of {images.Count}";
                var button = new ImageButton
                {
                    Source = ImageSource.FromStream(() => new MemoryStream(image.Data)), Aspect = Aspect.AspectFit,
                    HeightRequest = 180, MinimumWidthRequest = 44, MaximumWidthRequest = 520, Margin = new Thickness(0, 0, 8, 8),
                    BackgroundColor = Colors.Transparent, CornerRadius = 8,
                };
                SemanticProperties.SetDescription(button, label + ". Enlarge");
                button.Clicked += async (_, _) =>
                {
                    if (_page._conversation.OwnsTurn(_turn) && _page.Navigation is { } navigation)
                        await navigation.PushModalAsync(new ImagePreviewPage(_page._conversation, _turn, image, label));
                };
                _images.Children.Add(button);
            }
            _images.IsVisible = images.Count > 0;
        }

        /// <summary>The tool's title from the shared catalogue, as MCP clients show it (#635).</summary>
        private static string LookupName(string name) => DashyNMS.Agent.Catalogue.ToolCatalogue.Find(name)?.Title ?? "Network lookup";
        public void Dispose()
        {
            _turn.PropertyChanged -= OnChanged;
            _turn.Lookups.CollectionChanged -= OnLookupsChanged;
            foreach (var lookup in _watched) lookup.PropertyChanged -= OnChanged;
            _watched.Clear();
        }
    }

    /// <summary>
    /// An enlarged graph from the answer's own bytes: no network fetch. Close is a
    /// 44pt button inside the safe area; the dark backdrop runs edge to edge.
    /// </summary>
    private sealed class ImagePreviewPage : ContentPage, IPrivateChatSurface
    {
        private readonly IntelligenceConversationViewModel _conversation;
        private readonly IntelligenceTurn _turn;
        private readonly Image _picture;
        private readonly Label _expired = new()
        {
            Text = "This chat is no longer available.", TextColor = Color.FromArgb("#E8EEF6"),
            Margin = 24, IsVisible = false, VerticalOptions = LayoutOptions.Center,
        };

        public ImagePreviewPage(IntelligenceConversationViewModel conversation, IntelligenceTurn turn,
            DesktopNMS.Core.Intelligence.IntelligenceImage image, string label)
        {
            _conversation = conversation;
            _turn = turn;
            Title = label;
            BackgroundColor = Color.FromArgb("#11141A");
            SafeAreaEdges = SafeAreaEdges.All;
            _picture = new Image { Source = ImageSource.FromStream(() => new MemoryStream(image.Data)), Aspect = Aspect.AspectFit };
            SemanticProperties.SetDescription(_picture, label);
            var close = new Button
            {
                Text = "Close", MinimumHeightRequest = 44, MinimumWidthRequest = 88, HorizontalOptions = LayoutOptions.End,
                Margin = new Thickness(16, 8), Style = ResourceStyle("SecondaryButton"),
            };
            SemanticProperties.SetDescription(close, "Close " + label);
            close.Clicked += async (_, _) => await Navigation.PopModalAsync();
            var grid = new Grid { RowDefinitions = [new(GridLength.Auto), new(GridLength.Star)] };
            grid.Add(close, 0, 0);
            grid.Add(_picture, 0, 1);
            grid.Add(_expired, 0, 1);
            Content = grid;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _conversation.PropertyChanged -= OnOwnershipChanged;
            _conversation.PropertyChanged += OnOwnershipChanged;
            CheckOwnership();
        }

        protected override void OnDisappearing()
        {
            _conversation.PropertyChanged -= OnOwnershipChanged;
            base.OnDisappearing();
        }

        private void OnOwnershipChanged(object? sender, PropertyChangedEventArgs e) => CheckOwnership();

        /// <summary>A modal must not retain readable evidence after its chat changes owner.</summary>
        private void CheckOwnership()
        {
            if (_conversation.OwnsTurn(_turn)) return;
            _picture.Source = null;
            _picture.IsVisible = false;
            _expired.IsVisible = true;
        }

        protected override bool OnBackButtonPressed()
        {
            _ = Navigation.PopModalAsync();
            return true;
        }
    }
}
