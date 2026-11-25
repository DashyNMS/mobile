using System.ComponentModel;
using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Intelligence;
using Microsoft.Maui.Controls.Shapes;

namespace DashyNMS.Mobile.Pages;

public sealed partial class IntelligencePage
{
    private string? _spotlightStamp;

    private Button ComposerChip(string text, string description, bool enabled, Action tapped)
    {
        var chip = new Button
        {
            Text = text,
            Style = ResourceStyle("Chip"),
            MinimumHeightRequest = 48,
            HeightRequest = -1,
            CornerRadius = 22,
            BorderWidth = 1.5,
            Padding = new Thickness(14, 0),
            IsEnabled = enabled,
            HorizontalOptions = LayoutOptions.Start,
        };
        chip.SetAppThemeColor(Button.BorderColorProperty, ThemeColour("ComposerChipStrokeLight"), ThemeColour("ComposerChipStrokeDark"));
        chip.SetAppThemeColor(Button.TextColorProperty, ThemeColour("TextLight"), ThemeColour("TextDark"));
        SemanticProperties.SetDescription(chip, description);
        chip.Clicked += (_, _) => tapped();
        return chip;
    }

    private void RefreshSpotlight()
    {
        var stamp = _conversation.ChatRevision + ":" + _conversation.CanAsk + ":" + _conversation.HasPreviousObjectChat + ":" + _conversation.SpotlightInstructions + ":" + string.Join("|", _conversation.Spotlight.Select(s => s.Name));
        if (stamp == _spotlightStamp) return;
        _spotlightStamp = stamp;
        _spotlightControls.Children.Clear();
        var choices = new HorizontalStackLayout { Spacing = 8 };
        choices.Children.Add(ComposerChip(
            "✦ Spotlight ▾",
            "Choose devices, a group or a location for Spotlight",
            _conversation.CanAsk,
            () => _ = PickSpotlightAsync()));
        foreach (var item in _conversation.Spotlight)
        {
            var captured = item;
            choices.Children.Add(ComposerChip(
                captured.Name + " ×",
                "Remove " + captured.Name + " from Spotlight",
                _conversation.CanAsk,
                () => _conversation.RemoveSpotlight(captured)));
        }
        _spotlightControls.Children.Add(new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            SafeAreaEdges = SafeAreaEdges.None,
            Content = choices,
        });
        if (_conversation.HasPreviousObjectChat)
        {
            _spotlightControls.Children.Add(ComposerChip(
                "↩ Switch to previous chat",
                "Switch to previous chat",
                _conversation.CanAsk,
                () => _conversation.SwitchPreviousObjectChat()));
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
            var dismissOnPick = key is "group" or "location";
            var picker = new IntelligenceSpotlightPickerPage(
                kind!,
                dismissOnPick,
                choices,
                _conversation,
                revision,
                async (choice, token) => await _conversation.SetSpotlightAsync(choice, revision, _spotlightSource.ResolveAsync, token: token));
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

/// <summary>
/// Header-action Spotlight modal: owns no glass, uses <see cref="SearchField"/> and
/// virtualised whole-row native buttons, dismisses immediately on single Group or
/// Location pick, and lets the presenting page reclaim glass on close.
/// </summary>
internal sealed class IntelligenceSpotlightPickerPage : ContentPage, IPrivateChatSurface
{
    private readonly IntelligenceConversationViewModel _conversation;
    private readonly long _revision;
    private readonly bool _dismissOnPick;
    private readonly HashSet<string> _selectedNames;
    private bool _valid = true;
    private bool _selecting;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Entry _searchEntry;
    private readonly Label _status;
    private readonly CollectionView _list = new() { SelectionMode = SelectionMode.None };

    public IntelligenceSpotlightPickerPage(
        string heading,
        bool dismissOnPick,
        IReadOnlyList<SpotlightChoice> choices,
        IntelligenceConversationViewModel conversation,
        long revision,
        Func<SpotlightChoice, CancellationToken, Task<bool>> select)
    {
        _conversation = conversation;
        _revision = revision;
        _dismissOnPick = dismissOnPick;
        _selectedNames = new HashSet<string>(conversation.Spotlight.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);
        Title = heading;
        SafeAreaEdges = SafeAreaEdges.All;
        this.SetAppThemeColor(BackgroundColorProperty, ThemeColour("BackgroundLight"), ThemeColour("BackgroundDark"));

        var title = new Label
        {
            Text = heading,
            Style = ResourceStyle("PageTitle"),
            LineBreakMode = LineBreakMode.WordWrap,
            VerticalOptions = LayoutOptions.Center,
        };
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level1);

        var done = new Button
        {
            Text = "Done",
            Style = ResourceStyle("SecondaryButton"),
            MinimumHeightRequest = 44,
            MinimumWidthRequest = 44,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center,
        };
        done.Clicked += async (_, _) => await CloseAsync();

        var header = new Grid
        {
            ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)],
            ColumnSpacing = 12,
            MinimumHeightRequest = TopBar.Height,
        };
        header.Add(title, 0, 0);
        header.Add(done, 1, 0);

        _searchEntry = new Entry
        {
            Placeholder = "Search " + heading.ToLowerInvariant(),
            ReturnType = ReturnType.Search,
        };
        var searchField = new SearchField { Content = _searchEntry };

        _status = new Label
        {
            Text = dismissOnPick
                ? "Tap a choice to apply it and return to the chat."
                : "Selections apply straight away. Use Done to return to the chat.",
            Style = ResourceStyle("Caption"),
            LineBreakMode = LineBreakMode.WordWrap,
        };

        _searchEntry.TextChanged += (_, e) =>
            _list.ItemsSource = !_valid
                ? []
                : choices.Where(c => c.Name.Contains(e.NewTextValue ?? "", StringComparison.CurrentCultureIgnoreCase)).ToArray();

        _list.ItemsSource = choices;
        _list.Header = new BoxView { Color = Colors.Transparent, HeightRequest = 12, InputTransparent = true };
        _list.Footer = new BoxView { Color = Colors.Transparent, HeightRequest = 24, InputTransparent = true };
        _list.EmptyView = new Label
        {
            Text = "No matching choices.",
            Style = ResourceStyle("Caption"),
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(16, 32),
        };
        _list.ItemTemplate = new DataTemplate(() => BuildRow(choices, select));

        var listCard = new Border { Style = ResourceStyle("OpenListCard"), Content = _list };

        var grid = new Grid
        {
            RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star)],
            Padding = new Thickness(16, 12, 16, 0),
            RowSpacing = 8,
        };
        grid.Add(header, 0, 0);
        grid.Add(searchField, 0, 1);
        grid.Add(_status, 0, 2);
        grid.Add(listCard, 0, 3);
        Content = grid;
    }

    private View BuildRow(IReadOnlyList<SpotlightChoice> choices, Func<SpotlightChoice, CancellationToken, Task<bool>> select)
    {
        var row = new Grid
        {
            RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto)],
            ColumnDefinitions = [new(GridLength.Star), new(GridLength.Auto)],
            MinimumHeightRequest = 48,
        };

        var line = new BoxView { Style = ResourceStyle("Divider") };
        Grid.SetColumnSpan(line, 2);
        row.Add(line, 0, 0);

        var button = new Button
        {
            BackgroundColor = Colors.Transparent,
            MinimumHeightRequest = 48,
            Padding = new Thickness(14, 10),
            CornerRadius = 0,
            BorderWidth = 0,
            FontFamily = "BodyRegular",
            FontSize = 15,
            LineBreakMode = LineBreakMode.WordWrap,
            HorizontalOptions = LayoutOptions.Fill,
            ContentLayout = new Button.ButtonContentLayout(Button.ButtonContentLayout.ImagePosition.Left, 0),
        };
        button.SetAppThemeColor(Button.TextColorProperty, ThemeColour("TextLight"), ThemeColour("TextDark"));
        VisualStateManager.SetVisualStateGroups(button, PickerRowStates());

        var tick = new Label
        {
            Text = "✓",
            FontFamily = "BodySemibold",
            FontSize = 16,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 14, 0),
            InputTransparent = true,
            IsVisible = false,
        };
        tick.SetAppThemeColor(Label.TextColorProperty, ThemeColour("PrimaryTextLight"), ThemeColour("PrimaryTextDark"));
        AutomationProperties.SetIsInAccessibleTree(tick, false);

        row.BindingContextChanged += (_, _) =>
        {
            if (row.BindingContext is not SpotlightChoice choice) return;
            var picked = _selectedNames.Contains(choice.Name);
            button.Text = choice.Name;
            tick.IsVisible = picked;
            SemanticProperties.SetDescription(button, picked ? $"{choice.Name}, selected" : choice.Name);
            line.IsVisible = choices.Count == 0 || !ReferenceEquals(choices[0], choice);
        };

        button.Clicked += async (_, _) =>
        {
            if (!_valid || _selecting || row.BindingContext is not SpotlightChoice choice) return;
            _selecting = true;
            _list.IsEnabled = false;
            try
            {
                var added = await select(choice, _lifetime.Token);
                if (_lifetime.IsCancellationRequested) return;
                if (added)
                {
                    _selectedNames.Add(choice.Name);
                    tick.IsVisible = true;
                    SemanticProperties.SetDescription(button, $"{choice.Name}, selected");
                    if (_dismissOnPick)
                    {
                        await CloseAsync();
                        return;
                    }
                    _status.Text = "Added to Spotlight.";
                }
                else
                {
                    _status.Text = _conversation.Notice;
                }
                SemanticScreenReader.Default.Announce(_status.Text);
            }
            finally
            {
                _selecting = false;
                _list.IsEnabled = _valid;
            }
        };

        row.Add(button, 0, 1);
        row.Add(tick, 1, 1);
        return row;
    }

    private static VisualStateGroupList PickerRowStates()
    {
        var primaryTint = ThemeColour("PrimaryTint");
        var group = new VisualStateGroup { Name = "CommonStates" };
        group.States.Add(new VisualState { Name = "Normal" });
        group.States.Add(new VisualState { Name = "Disabled" });
        group.States.Add(new VisualState
        {
            Name = "Pressed",
            Setters = { new Setter { Property = VisualElement.BackgroundColorProperty, Value = primaryTint } },
        });
        group.States.Add(new VisualState
        {
            Name = "Focused",
            Setters = { new Setter { Property = VisualElement.BackgroundColorProperty, Value = primaryTint } },
        });
        return [group];
    }

    private async Task CloseAsync()
    {
        KeyboardDismiss.Close(_searchEntry);
        if (Navigation.ModalStack.Contains(this))
        {
            await Navigation.PopModalAsync();
        }
        if (ActivePage.Current is IActivePage page)
        {
            page.Activate();
        }
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
        if (ActivePage.Current is IActivePage page)
        {
            page.Activate();
        }
    }

    private void ConversationChanged(object? sender, PropertyChangedEventArgs e) => InvalidateIfNeeded();

    private void InvalidateIfNeeded()
    {
        if (_conversation.ChatRevision == _revision && _conversation.IsTranscriptCurrent) return;
        _valid = false;
        _list.ItemsSource = null;
        _searchEntry.Text = "";
        _searchEntry.IsEnabled = false;
        _status.Text = "The chat or connection changed. Close this picker and try again.";
    }

    private static Color ThemeColour(string key) => (Color)Application.Current!.Resources[key];
    private static Style ResourceStyle(string key) => (Style)Application.Current!.Resources[key];
}
