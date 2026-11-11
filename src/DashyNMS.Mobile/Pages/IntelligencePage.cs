using System.Collections.Specialized;
using System.ComponentModel;
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
    private readonly Editor _question = new() { Placeholder = "Ask about your network", HeightRequest = 96, AutoSize = EditorAutoSizeOption.Disabled };
    private readonly Picker _detail = new() { Title = "Answer detail", ItemsSource = new[] { "Brief", "Balanced", "Detailed" } };
    private readonly Button _ask = new() { Text = "Ask", MinimumHeightRequest = 44 };
    private readonly IconButton _settings = new() { Icon = Icons.Maintenance, Kind = IconButtonKind.Plain };
    private readonly IconButton _new = new() { Icon = "M12 4V20 M4 12H20", Kind = IconButtonKind.Plain };
    private readonly IDispatcherTimer _timer;
    private readonly List<TurnCard> _cards = [];
    private bool _showing;
    private bool _following = true;
    private bool _scrollPending;
    private bool _scrolling;
    private bool _newChatPending;

    public IntelligencePage(IntelligenceConversationViewModel conversation, IntelligencePreferences preferences,
        IIntelligenceModel model, INavigationService navigation, IDialogService dialogs)
    {
        _conversation = conversation;
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
        _scroll.SizeChanged += (_, _) => { if (_following) _scrollPending = true; };
        _jump.Clicked += (_, _) => { _following = true; _scrollPending = true; Flush(); };
        _ask.Clicked += async (_, _) =>
        {
            if (_conversation.IsRunning) _conversation.Stop();
            else await AskAsync();
        };
        _question.TextChanged += (_, _) => UpdateControls();
        var composer = new VerticalStackLayout { SafeAreaEdges = SafeAreaEdges.None, Spacing = 4, Padding = new Thickness(0, 8, 0, 0) };
        composer.Children.Add(_detail);
        composer.Children.Add(_question);
        composer.Children.Add(_ask);
        var body = new Grid { SafeAreaEdges = SafeAreaEdges.None, RowDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)] };
        body.Add(_notice, 0, 0);
        body.Add(_scroll, 0, 1);
        body.Add(_jump, 0, 2);
        var start = new GradientStop { Offset = 0 };
        start.SetAppThemeColor(GradientStop.ColorProperty, Color.FromArgb("#F4F6F9"), Color.FromArgb("#11141A"));
        var end = new GradientStop { Offset = 1 };
        end.SetAppThemeColor(GradientStop.ColorProperty, Color.FromArgb("#00F4F6F9"), Color.FromArgb("#0011141A"));
        body.Add(new Border
        {
            HeightRequest = 20, VerticalOptions = LayoutOptions.Start, StrokeThickness = 0, InputTransparent = true,
            Background = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1), GradientStops = [start, end] },
        }, 0, 1);
        var setup = new SetupLayout { Header = _header, Body = body, PrimaryAction = composer };
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
        _starters.Children.Add(new Label { Text = "What would you like to know?", Style = ResourceStyle("SectionHeader") });
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
    }

    private void UpdateControls()
    {
        _ask.Text = _conversation.IsRunning ? "Stop" : "Ask";
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
    private sealed class TurnCard : Border, IDisposable
    {
        private readonly IntelligenceTurn _turn;
        private readonly IntelligencePage _page;
        private readonly Label _answer = new() { LineBreakMode = LineBreakMode.WordWrap };
        private readonly Label _state = new();
        private readonly Label _evidence = new();
        private readonly Label _omitted = new() { Text = "Earlier turns were left out of this answer's context." };
        private readonly Button _copy = new ChatActionButton() { Text = "Copy answer" };
        private readonly Button _retry = new ChatActionButton() { Text = "Retry" };
        private readonly Button _edit = new ChatActionButton() { Text = "Edit question" };
        private bool _dirty = true;
        private readonly List<IntelligenceLookupProgress> _watched = [];

        public TurnCard(IntelligenceTurn turn, IntelligencePage page)
        {
            _turn = turn;
            _page = page;
            Style = IntelligencePage.ResourceStyle("Card");
            var content = new VerticalStackLayout { Spacing = 8 };
            var question = new Label { Text = turn.Question, FontFamily = "BodySemibold", LineBreakMode = LineBreakMode.WordWrap };
            SemanticProperties.SetHeadingLevel(question, SemanticHeadingLevel.Level2);
            content.Children.Add(question);
            content.Children.Add(_answer);
            _state.Style = _evidence.Style = _omitted.Style = IntelligencePage.ResourceStyle("Caption");
            content.Children.Add(_evidence);
            content.Children.Add(_omitted);
            content.Children.Add(_state);
            // Wrapping actions remain at least 44pt and do not force a phone-wide row.
            var actions = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
            foreach (var button in new[] { _copy, _retry, _edit })
            {
                button.MinimumHeightRequest = 44;
                button.Margin = new Thickness(0, 0, 8, 0);
                actions.Children.Add(button);
            }
            content.Children.Add(actions);
            Content = content;
            SemanticProperties.SetDescription(_copy, "Copy answer to: " + turn.Question);
            SemanticProperties.SetDescription(_retry, "Retry: " + turn.Question);
            SemanticProperties.SetDescription(_edit, "Edit question: " + turn.Question);
            _copy.Clicked += async (_, _) =>
            {
                try { await Clipboard.Default.SetTextAsync(_turn.Answer); SemanticScreenReader.Default.Announce("Answer copied"); }
                catch { await _page._dialogs.AlertAsync("Couldn't copy the answer", "Try again."); }
            };
            _retry.Clicked += async (_, _) => { if (_page._conversation.CanAsk) { _page._following = true; await _page._conversation.RetryAsync(_turn); } };
            _edit.Clicked += (_, _) => { _page._question.Text = _page._conversation.EditQuestion(_turn); _page._question.Focus(); };
            Resume();
            Render();
        }

        internal IntelligenceTurn Turn => _turn;
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
            _evidence.IsVisible = _turn.Lookups.Count > 0;
            _omitted.IsVisible = _turn.HistoryOmitted;
            UpdateActions();
        }
        internal void UpdateActions()
        {
            _copy.IsEnabled = _turn.Answer.Length > 0;
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
