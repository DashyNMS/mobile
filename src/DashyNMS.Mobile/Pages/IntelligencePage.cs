using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// In-memory chat from More or a pinned tab. A finite transcript viewport and a
/// measured composer share the setup layout's keyboard and glass clearance.
/// Streaming labels are coalesced to ten updates a second rather than relaid out per token.
/// </summary>
public sealed class IntelligencePage : ContentPage, IActivePage
{
    private readonly IntelligenceConversationViewModel _conversation;
    private readonly IntelligencePreferences _preferences;
    private readonly IIntelligenceModel _model;
    private readonly INavigationService _navigation;
    private readonly IDialogService _dialogs;
    private readonly SetupPageLayout _placement;
    private readonly PageHeader _header = new() { Title = "Intelligence" };
    private readonly ScrollView _scroll = new() { SafeAreaEdges = SafeAreaEdges.None };
    private readonly VerticalStackLayout _transcript = new() { Spacing = 16, Padding = new Thickness(0, 20, 0, 16) };
    private readonly VerticalStackLayout _starters = new() { Spacing = 8 };
    private readonly Label _notice = new();
    private readonly Button _jump = new ChatActionButton() { Text = "Jump to latest", MinimumHeightRequest = 44, IsVisible = false };
    private readonly Editor _question = new() { Placeholder = "Ask about your network…", MinimumHeightRequest = 48, MaximumHeightRequest = 140, AutoSize = EditorAutoSizeOption.TextChanges, BackgroundColor = Colors.Transparent };
    private readonly Picker _detail = new() { Title = "Answer detail", ItemsSource = new[] { "Brief", "Balanced", "Detailed" } };
    private readonly Button _ask = new() { Text = "↑", FontSize = 24, MinimumHeightRequest = 48, MinimumWidthRequest = 48, CornerRadius = 24, Padding = new Thickness(12, 4) };
    private readonly Border _composer = new() { Padding = new Thickness(12, 8), StrokeThickness = 1, StrokeShape = new RoundRectangle { CornerRadius = 18 }, HorizontalOptions = LayoutOptions.Center };
    private readonly IconButton _settings = new() { Icon = Icons.Maintenance, Kind = IconButtonKind.Plain };
    private readonly IconButton _new = new() { Icon = "M12 4V20 M4 12H20", Kind = IconButtonKind.Plain };
    private readonly IDispatcherTimer _timer;
    private readonly List<TurnCard> _cards = [];
    private long _chatRevision;
    private bool _showing;
    private bool _following = true;
    private bool _scrollPending;
    private bool _scrolling;
    private bool _newChatPending;

    public IntelligencePage(IntelligenceConversationViewModel conversation, IntelligencePreferences preferences,
        IIntelligenceModel model, INavigationService navigation, IDialogService dialogs)
    {
        _conversation = conversation;
        _chatRevision = conversation.ChatRevision;
        _preferences = preferences;
        _model = model;
        _navigation = navigation;
        _dialogs = dialogs;
        Title = "Intelligence";
        Shell.SetNavBarIsVisible(this, false);
        SemanticProperties.SetDescription(_question, "Question about your network");
        SemanticProperties.SetDescription(_detail, "Detail for the next answer");
        _detail.SelectedIndex = preferences.Detail switch { "brief" => 0, "detailed" => 2, _ => 1 };
        _notice.Style = ResourceStyle("Caption");
        _notice.HorizontalOptions = LayoutOptions.Center;
        SemanticProperties.SetDescription(_new, "New chat");
        SemanticProperties.SetDescription(_settings, "Intelligence settings");
        _new.Command = new Command(RequestNewChat, () => !_conversation.IsRunning && !_newChatPending);
        _settings.Command = new Command(async () => await _navigation.GoToAsync(Routes.IntelligenceSettings));
        _header.Actions = new HorizontalStackLayout { Children = { _new, _settings } };
        _scroll.Content = _transcript;
        _scroll.Scrolled += (_, e) =>
        {
            if (_scrolling) return;
            _following = ChatScrollFollow.IsNearEnd(e.ScrollY, _scroll.Height, _scroll.ContentSize.Height);
            _jump.IsVisible = !_following && _conversation.Turns.Count > 0;
        };
        _transcript.SizeChanged += (_, _) => { if (_following) _scrollPending = true; };
        _scroll.SizeChanged += (_, _) => { PlaceConversation(); if (_following) _scrollPending = true; };
        _jump.Clicked += (_, _) => { _following = true; _scrollPending = true; Flush(); };
        _ask.Clicked += async (_, _) =>
        {
            if (_conversation.IsRunning) _conversation.Stop();
            else await AskAsync();
        };
        _question.TextChanged += (_, _) => UpdateControls();
        _transcript.HorizontalOptions = LayoutOptions.Center;
        _composer.SetAppThemeColor(BackgroundColorProperty, ThemeColour("SurfaceLight"), ThemeColour("SurfaceAltDark"));
        var composerStroke = new SolidColorBrush();
        composerStroke.SetAppThemeColor(SolidColorBrush.ColorProperty, ThemeColour("StrokeLight"), ThemeColour("StrokeDark"));
        _composer.Stroke = composerStroke;
        _detail.BackgroundColor = Colors.Transparent;
        _detail.MinimumHeightRequest = 44;
        _detail.MaximumWidthRequest = 180;
        _detail.HorizontalOptions = LayoutOptions.Start;
        var composer = new Grid { SafeAreaEdges = SafeAreaEdges.None, RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto)], RowSpacing = 4 };
        composer.Add(_question, 0, 0);
        var choices = new Grid { SafeAreaEdges = SafeAreaEdges.None, ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 8 };
        choices.Add(_detail, 0, 0);
        choices.Add(_ask, 1, 0);
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
        body.Add(_jump, 0, 2);
        var setup = new SetupLayout { Header = _header, Body = body, PrimaryAction = _composer };
        Content = setup;
        _placement = new SetupPageLayout(this, setup, _header, _scroll) { ShowActions = ShowActions };
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
            if (_cards.Count != _conversation.Turns.Count || _cards.Where((card, index) => !ReferenceEquals(card.Turn, _conversation.Turns[index])).Any()
                || _transcript.Children.Count == 0) Rebuild();
            else foreach (var card in _cards) card.Resume();
            _timer.Start();
        }
        _placement.Activate();
        UpdateControls();
        ShowActions();
        PlaceConversation();
    }

    protected override void OnDisappearing()
    {
        _showing = false;
        _timer.Stop();
        _conversation.PropertyChanged -= OnConversationChanged;
        _conversation.Turns.CollectionChanged -= OnTurnsChanged;
        foreach (var card in _cards) card.Dispose();
        base.OnDisappearing();
    }

    private void OnConversationChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_showing) return;
        UpdateControls();
        if (e.PropertyName == nameof(_conversation.IsRunning)) ShowActions();
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
        _starters.Children.Clear();
        var heading = new Label { Text = "What would you like to know?", Style = ResourceStyle("SectionHeader") };
        // This heading sits on the accent wash rather than a neutral surface.
        heading.SetAppThemeColor(Label.TextColorProperty, ThemeColour("TextSecondaryLight"), ThemeColour("TextSecondaryDark"));
        _starters.Children.Add(heading);
        foreach (var starter in _conversation.StarterQuestions)
        {
            var button = new ChatActionButton { Text = starter, MinimumHeightRequest = 44 };
            button.Clicked += (_, _) => { _question.Text = starter; _question.Focus(); };
            _starters.Children.Add(button);
        }
        _starters.IsVisible = _conversation.Turns.Count == 0;
        _transcript.Children.Add(_starters);
        foreach (var turn in _conversation.Turns) AddTurn(turn);
        _scrollPending = _following;
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
        _composer.WidthRequest = width;
        foreach (var card in _cards) card.Place(width);
    }

    private void UpdateControls()
    {
        if (_chatRevision != _conversation.ChatRevision)
        {
            _chatRevision = _conversation.ChatRevision;
            _question.Text = "";
            _following = true;
            _scrollPending = true;
        }
        _transcript.IsVisible = _question.IsVisible = _detail.IsVisible = _conversation.IsTranscriptCurrent;
        _ask.Text = _conversation.IsRunning ? "■" : "↑";
        SemanticProperties.SetDescription(_ask, _conversation.IsRunning ? "Stop answer" : "Send question");
        _ask.IsEnabled = _conversation.IsRunning || (_conversation.CanAsk && !string.IsNullOrWhiteSpace(_question.Text));
        _question.IsEnabled = !_conversation.IsRunning;
        _detail.IsEnabled = !_conversation.IsRunning;
        _starters.IsEnabled = _conversation.CanAsk;
        _notice.Text = !string.IsNullOrEmpty(_conversation.Notice) ? _conversation.Notice
            : !_preferences.Enabled ? "Turn on Intelligence in Settings to ask a question."
            : _model.Availability != IntelligenceAvailability.Available ? IntelligenceCheckViewModel.Describe(_model.Availability)
            : "Answers can be wrong. Lookups may use cached data; check readings and their age in LibreNMS.";
        (_new.Command as Command)?.ChangeCanExecute();
        foreach (var card in _cards) card.UpdateActions();
    }

    private async Task AskAsync()
    {
        var question = _question.Text ?? "";
        var count = _conversation.Turns.Count;
        _following = true;
        var task = _conversation.AskAsync(question, _detail.SelectedIndex switch { 0 => "brief", 2 => "detailed", _ => "balanced" });
        if (_conversation.Turns.Count > count) _question.Text = "";
        await task;
        Flush();
    }

    private void Flush()
    {
        if (!_showing || Shell.Current?.CurrentPage != this) return;
        foreach (var card in _cards) card.Render();
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

    /// <summary>Clearing an in-memory transcript needs confirmation; Stop remains a separate action.</summary>
    public async void RequestNewChat()
    {
        if (_conversation.IsRunning || _newChatPending) return;
        _newChatPending = true;
        UpdateControls();
        ShowActions();
        try
        {
            if (_conversation.Turns.Count > 0 && !await _dialogs.ConfirmAsync("Start a new chat?", "This clears the current questions and answers. Chats are kept only while the app is running.", "New chat", "Cancel")) return;
            if (_conversation.NewChat())
            {
                _question.Text = "";
                _following = true;
                _scrollPending = true;
            }
        }
        finally { _newChatPending = false; UpdateControls(); ShowActions(); }
    }

    private void ShowActions()
    {
        if (!_showing || Shell.Current?.CurrentPage != this) return;
#if IOS
        _new.IsVisible = _settings.IsVisible = !TabBarButtons.IsAvailable;
        if (TabBarButtons.IsAvailable)
        {
            var actions = new List<GlassAction>();
            if (PageHeader.IsPushed(this)) actions.Add(new("chevron.backward", "Back to " + BackBar.PreviousTitle(this), () => _ = Shell.Current.GoToAsync(".."), IsBack: true));
            actions.Add(new("square.and.pencil", "New chat", RequestNewChat, IsEnabled: !_conversation.IsRunning && !_newChatPending));
            actions.Add(new("gearshape", "Intelligence settings", () => _ = _navigation.GoToAsync(Routes.IntelligenceSettings)));
            TabBarButtons.Show(this, null, string.Empty, actions);
        }
#endif
    }

    private static Color ThemeColour(string key) => (Color)Application.Current!.Resources[key];

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

    /// <summary>Plain text is never parsed as HTML. Each retained attempt owns its subscriptions while visible.</summary>
    private sealed class TurnCard : VerticalStackLayout, IDisposable
    {
        private readonly IntelligenceTurn _turn;
        private readonly IntelligencePage _page;
        private readonly Border _questionBubble;
        private readonly Button _activity = new ChatActionButton { MinimumHeightRequest = 44, CornerRadius = 18, HorizontalOptions = LayoutOptions.Start };
        private bool _activityExpanded;
        private readonly Label _answer = new() { LineBreakMode = LineBreakMode.WordWrap };
        private readonly Label _state = new();
        private readonly Label _evidence = new();
        private readonly Label _omitted = new() { Text = "Earlier turns were left out of this answer's context." };
        private readonly Button _copy = new ChatActionButton() { Text = "Copy" };
        private readonly Button _retry = new ChatActionButton() { Text = "Retry" };
        private readonly Button _edit = new ChatActionButton() { Text = "Edit question" };
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
            content.Children.Add(new Label { Text = "Intelligence", Style = ResourceStyle("Caption"), FontFamily = "BodySemibold" });
            _activity.Clicked += (_, _) => { _activityExpanded = !_activityExpanded; _dirty = true; Render(); };
            content.Children.Add(_activity);
            content.Children.Add(_evidence);
            content.Children.Add(_answer);
            _state.Style = _evidence.Style = _omitted.Style = ResourceStyle("Caption");
            content.Children.Add(_omitted);
            content.Children.Add(_state);
            var actions = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
            foreach (var button in new[] { _copy, _retry, _edit })
            {
                button.Style = ResourceStyle("LinkButton");
                button.MinimumHeightRequest = 44;
                button.Margin = new Thickness(0, 0, 8, 0);
                actions.Children.Add(button);
            }
            content.Children.Add(actions);
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
            SemanticProperties.SetDescription(_copy, "Copy answer to: " + turn.Question);
            SemanticProperties.SetDescription(_retry, "Retry: " + turn.Question);
            SemanticProperties.SetDescription(_edit, "Edit question: " + turn.Question);
            _copy.Clicked += async (_, _) =>
            {
                if (!_page._conversation.OwnsTurn(_turn)) return;
                try { await Clipboard.Default.SetTextAsync(_turn.Answer); SemanticScreenReader.Default.Announce("Answer copied"); }
                catch { await _page._dialogs.AlertAsync("Couldn't copy the answer", "Try again."); }
            };
            _retry.Clicked += async (_, _) => { if (_page._conversation.CanAsk) { _page._following = true; await _page._conversation.RetryAsync(_turn); } };
            _edit.Clicked += (_, _) => { _page._question.Text = _page._conversation.EditQuestion(_turn); _page._question.Focus(); };
            Resume();
            Render();
        }

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
            _answer.Text = _turn.Answer;
            _state.Text = _turn.State == IntelligenceTurnState.Running ? "Answering…" : _turn.Message;
            _evidence.Text = string.Join("\n", _turn.Lookups.Select(lookup => $"{LookupName(lookup.Name)}: {lookup.State switch { IntelligenceLookupState.Completed => "✓ Completed", IntelligenceLookupState.Failed => "⚠ Failed", IntelligenceLookupState.Stopped => "Stopped", _ => "Looking up…" }}"));
            var count = _turn.Lookups.Count;
            var working = _turn.Lookups.Any(lookup => lookup.State == IntelligenceLookupState.Running);
            var failed = _turn.Lookups.Any(lookup => lookup.State == IntelligenceLookupState.Failed);
            var stopped = _turn.Lookups.Any(lookup => lookup.State == IntelligenceLookupState.Stopped);
            _activity.IsVisible = count > 0;
            var outcome = working ? "Looking up…" : failed ? "Warning" : stopped ? "Stopped" : "Done";
            _activity.Text = $"{count} {(count == 1 ? "lookup" : "lookups")} · {outcome} {(_activityExpanded ? "▴" : "▾")}";
            SemanticProperties.SetDescription(_activity, $"{(_activityExpanded ? "Hide" : "Show")} lookup details. {count} {(count == 1 ? "lookup" : "lookups")}. {outcome}");
            _evidence.IsVisible = count > 0 && _activityExpanded;
            // The activity summary already conveys success. Keep exceptional outcomes explicit.
            _state.IsVisible = _turn.State != IntelligenceTurnState.Completed || _turn.Message != "Completed";
            _omitted.IsVisible = _turn.HistoryOmitted;
            UpdateActions();
        }
        internal void UpdateActions()
        {
            _copy.IsEnabled = _page._conversation.OwnsTurn(_turn) && _turn.Answer.Length > 0;
            var retryable = _turn.State is IntelligenceTurnState.Failed or IntelligenceTurnState.Stopped or IntelligenceTurnState.Interrupted;
            _retry.IsVisible = _edit.IsVisible = retryable;
            _retry.IsEnabled = _edit.IsEnabled = _page._conversation.CanAsk;
        }
        private static string LookupName(string name) => name switch
        {
            "network_overview" => "Network overview", "find_device" => "Find device", "device_summary" => "Device summary",
            "active_alerts" => "Active alerts", "device_sensors" => "Device sensors", _ => "Network lookup",
        };
        public void Dispose()
        {
            _turn.PropertyChanged -= OnChanged;
            _turn.Lookups.CollectionChanged -= OnLookupsChanged;
            foreach (var lookup in _watched) lookup.PropertyChanged -= OnChanged;
            _watched.Clear();
        }
    }
}
