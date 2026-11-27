using System.Windows.Input;
using DashyNMS.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// One status filter choice shared between <see cref="StatusFilterBar"/> and
/// <see cref="StatusFilterSheet"/> so both reuse the page's existing predicates,
/// count labels and toggle commands (#708).
/// </summary>
public sealed class StatusFilterItem : BindableObject
{
    public static readonly BindableProperty LabelProperty = BindableProperty.Create(
        nameof(Label),
        typeof(string),
        typeof(StatusFilterItem),
        string.Empty,
        propertyChanged: (b, _, _) => ((StatusFilterItem)b).Changed?.Invoke((StatusFilterItem)b, EventArgs.Empty));

    public static readonly BindableProperty IsIncludedProperty = BindableProperty.Create(
        nameof(IsIncluded),
        typeof(bool),
        typeof(StatusFilterItem),
        true,
        propertyChanged: (b, _, _) => ((StatusFilterItem)b).Changed?.Invoke((StatusFilterItem)b, EventArgs.Empty));

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command),
        typeof(ICommand),
        typeof(StatusFilterItem),
        null,
        propertyChanged: (b, _, _) => ((StatusFilterItem)b).Changed?.Invoke((StatusFilterItem)b, EventArgs.Empty));

    /// <summary>Token prefix in Colors.xaml (Critical, Warning, Ok, Maintenance, Inactive or Primary).</summary>
    public string Tone { get; set; } = "Primary";

    /// <summary>Display text including count once loaded (e.g. "Critical 3").</summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>Whether this status is currently included in the list.</summary>
    public bool IsIncluded
    {
        get => (bool)GetValue(IsIncludedProperty);
        set => SetValue(IsIncludedProperty, value);
    }

    /// <summary>Toggle command bound to the page's view model.</summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    internal event EventHandler? Changed;
}

/// <summary>
/// Responsive status filter bar for Alerts and Devices (#708):
/// renders one row of four chips when all fit at rest, a 2x2 grid on narrow panes,
/// or a labelled Status button opening <see cref="StatusFilterSheet"/> when two rows
/// would crowd the viewport (for example at accessibility text sizes or short heights).
/// </summary>
[ContentProperty(nameof(Items))]
public sealed class StatusFilterBar : ContentView
{
    public static readonly BindableProperty SummaryTextProperty = BindableProperty.Create(
        nameof(SummaryText),
        typeof(string),
        typeof(StatusFilterBar),
        "All",
        propertyChanged: (b, _, _) => ((StatusFilterBar)b).RefreshPickerButton());

    public static readonly BindableProperty IsFilteredProperty = BindableProperty.Create(
        nameof(IsFiltered),
        typeof(bool),
        typeof(StatusFilterBar),
        false,
        propertyChanged: (b, _, _) => ((StatusFilterBar)b).RefreshPickerButton());

    private readonly List<StatusFilterItem> _items = [];
    private readonly List<ChipSlot> _slots = [];
    private readonly Grid _chipGrid;
    private readonly Grid _pickerContainer;
    private readonly Border _pickerBorder;
    private readonly Label _pickerValue;
    private readonly Button _pickerButton;
    private StatusFilterMode _mode = StatusFilterMode.TwoRows;
    private double _lastWidth;
    private double _lastHeight;
    private bool _built;
#if IOS
    private Foundation.NSObject? _contentSizeObserver;
#endif

    public StatusFilterBar()
    {
        SafeAreaEdges = SafeAreaEdges.None;
#if IOS
        Loaded += (_, _) =>
        {
            _contentSizeObserver ??= UIKit.UIApplication.Notifications.ObserveContentSizeCategoryChanged((_, _) =>
                Dispatcher.Dispatch(() => ApplyLayout(_lastWidth, _lastHeight)));
        };
        Unloaded += (_, _) =>
        {
            _contentSizeObserver?.Dispose();
            _contentSizeObserver = null;
        };
#endif

        _chipGrid = new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 8,
            SafeAreaEdges = SafeAreaEdges.None,
        };

        // Compact Status control shown when two rows would consume too much viewport.
        _pickerBorder = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            StrokeThickness = 1,
            Padding = new Thickness(14, 10),
            MinimumHeightRequest = 44,
            InputTransparent = true,
        };
        ExcludeVisualFromAccessibility(_pickerBorder);

        var pickerInner = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            ColumnSpacing = 8,
            InputTransparent = true,
            SafeAreaEdges = SafeAreaEdges.None,
        };
        AutomationProperties.SetIsInAccessibleTree(pickerInner, false);

        var statusLabel = new Label
        {
            Text = "Status",
            FontFamily = "BodySemibold",
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true,
        };
        statusLabel.SetAppThemeColor(Label.TextColorProperty, StatusTheme.Color("TextLight"), StatusTheme.Color("TextDark"));
        AutomationProperties.SetIsInAccessibleTree(statusLabel, false);

        _pickerValue = new Label
        {
            Text = "All",
            FontFamily = "BodyRegular",
            FontSize = 14,
            LineBreakMode = LineBreakMode.WordWrap,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true,
        };
        AutomationProperties.SetIsInAccessibleTree(_pickerValue, false);

        var chevron = new Microsoft.Maui.Controls.Shapes.Path
        {
            Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString("M6 9 12 15 18 9")!,
            StrokeThickness = 2,
            StrokeLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            WidthRequest = 16,
            HeightRequest = 16,
            Aspect = Stretch.Uniform,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true,
        };
        chevron.SetAppThemeColor(Shape.StrokeProperty, StatusTheme.Color("TextSecondaryLight"), StatusTheme.Color("TextSecondaryDark"));
        AutomationProperties.SetIsInAccessibleTree(chevron, false);

        pickerInner.Add(statusLabel, 0, 0);
        pickerInner.Add(_pickerValue, 1, 0);
        pickerInner.Add(chevron, 2, 0);
        _pickerBorder.Content = pickerInner;

        _pickerButton = CreateOverlayButton(cornerRadius: 14, minHeight: 44);
        _pickerButton.Clicked += (_, _) => OpenPickerRequested?.Invoke(this, EventArgs.Empty);
        AttachPressFeedback(_pickerButton, _pickerBorder);

        _pickerContainer = new Grid
        {
            IsVisible = false,
            SafeAreaEdges = SafeAreaEdges.None,
        };
        _pickerContainer.Add(_pickerBorder);
        _pickerContainer.Add(_pickerButton);

        var root = new Grid { SafeAreaEdges = SafeAreaEdges.None };
        root.Add(_chipGrid);
        root.Add(_pickerContainer);
        Content = root;

        RefreshPickerButton();
    }

    /// <summary>The four status filter items displayed by this bar.</summary>
    public IList<StatusFilterItem> Items => _items;

    /// <summary>Summary shown inside the compact Status control (e.g. "All" or "Critical, Warning").</summary>
    public string SummaryText
    {
        get => (string)GetValue(SummaryTextProperty);
        set => SetValue(SummaryTextProperty, value);
    }

    /// <summary>True when the status filter is narrowed from the page default.</summary>
    public bool IsFiltered
    {
        get => (bool)GetValue(IsFilteredProperty);
        set => SetValue(IsFilteredProperty, value);
    }

    /// <summary>Current resolved layout mode.</summary>
    public StatusFilterMode Mode => _mode;

    /// <summary>Raised when the user activates the compact Status button.</summary>
    public event EventHandler? OpenPickerRequested;

    /// <summary>Raised when the layout mode changes (for example so a page can dismiss the sheet when widening).</summary>
    public event EventHandler<StatusFilterMode>? ModeChanged;

    /// <summary>Updates the layout mode from the measured list pane width, viewport height and text scale.</summary>
    public void ApplyLayout(double availableWidth, double viewportHeight, double? fontScale = null)
    {
        _lastWidth = availableWidth;
        _lastHeight = viewportHeight;
        EnsureBuilt();
        var next = StatusFilterLayout.Resolve(availableWidth, viewportHeight, fontScale ?? CurrentFontScale());
        if (next == _mode && _built)
        {
            return;
        }

        _mode = next;
        ApplyMode();
        ModeChanged?.Invoke(this, _mode);
    }

    /// <summary>Current Dynamic Type / system font scale factor.</summary>
    public static double CurrentFontScale()
    {
#if IOS
        using var font = UIKit.UIFont.PreferredBody;
        return (font?.PointSize ?? 17d) / 17d;
#elif ANDROID
        return Android.App.Application.Context.Resources?.Configuration?.FontScale ?? 1d;
#else
        return 1d;
#endif
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        foreach (var item in _items)
        {
            SetInheritedBindingContext(item, BindingContext);
        }

        EnsureBuilt();
        RefreshAllSlots();
    }

    private void EnsureBuilt()
    {
        if (_built || _items.Count == 0)
        {
            return;
        }

        _built = true;
        foreach (var item in _items)
        {
            SetInheritedBindingContext(item, BindingContext);
            var slot = CreateChipSlot(item);
            _slots.Add(slot);
            _chipGrid.Add(slot.Container);
            item.Changed += (_, _) => RefreshSlot(slot);
        }

        ApplyMode();
        RefreshAllSlots();
    }

    private void ApplyMode()
    {
        if (_mode == StatusFilterMode.CompactPicker)
        {
            _chipGrid.IsVisible = false;
            _pickerContainer.IsVisible = true;
            RefreshPickerButton();
            return;
        }

        _pickerContainer.IsVisible = false;
        _chipGrid.IsVisible = true;
        _chipGrid.ColumnDefinitions.Clear();
        _chipGrid.RowDefinitions.Clear();

        if (_mode == StatusFilterMode.SingleRow)
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                _chipGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            }

            _chipGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var i = 0; i < _slots.Count; i++)
            {
                Grid.SetRow(_slots[i].Container, 0);
                Grid.SetColumn(_slots[i].Container, i);
                _slots[i].SetSingleRow(true);
            }
        }
        else
        {
            _chipGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _chipGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            _chipGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _chipGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var i = 0; i < _slots.Count; i++)
            {
                Grid.SetRow(_slots[i].Container, i / 2);
                Grid.SetColumn(_slots[i].Container, i % 2);
                _slots[i].SetSingleRow(false);
            }
        }
    }

    private void RefreshAllSlots()
    {
        foreach (var slot in _slots)
        {
            RefreshSlot(slot);
        }
    }

    private static void RefreshSlot(ChipSlot slot)
    {
        var item = slot.Item;
        slot.Title.Text = item.Label;
        slot.State.Text = StatusFilterLayout.ChipStateText(item.IsIncluded);
        slot.Button.Command = item.Command;
        SemanticProperties.SetDescription(slot.Button, StatusFilterLayout.ChipDescription(item.Label, item.IsIncluded));

        if (item.IsIncluded)
        {
            slot.Border.BackgroundColor = StatusTheme.Color(item.Tone + "Tint");
            slot.Border.Stroke = new SolidColorBrush(StatusTheme.Color(item.Tone + "Stroke"));
            slot.Title.SetAppThemeColor(
                Label.TextColorProperty,
                StatusTheme.Color(item.Tone + "TextLight"),
                StatusTheme.Color(item.Tone + "TextDark"));
        }
        else
        {
            slot.Border.BackgroundColor = Colors.Transparent;
            slot.Border.SetAppThemeColor(Border.StrokeProperty, StatusTheme.Color("Gray200"), StatusTheme.Color("ChipStrokeDark"));
            slot.Title.SetAppThemeColor(Label.TextColorProperty, StatusTheme.Color("TextSecondaryLight"), StatusTheme.Color("TextSecondaryDark"));
        }
    }

    private void RefreshPickerButton()
    {
        var summary = string.IsNullOrWhiteSpace(SummaryText) ? "All" : SummaryText;
        _pickerValue.Text = summary;
        SemanticProperties.SetDescription(_pickerButton, $"Status, {summary}");
        SemanticProperties.SetHint(_pickerButton, "Opens status filter choices");

        if (IsFiltered)
        {
            _pickerBorder.BackgroundColor = StatusTheme.Color("PrimaryTint");
            _pickerBorder.Stroke = new SolidColorBrush(StatusTheme.Color("PrimaryStroke"));
            _pickerValue.SetAppThemeColor(Label.TextColorProperty, StatusTheme.Color("PrimaryTextLight"), StatusTheme.Color("PrimaryTextDark"));
        }
        else
        {
            _pickerBorder.SetAppThemeColor(Border.BackgroundColorProperty, StatusTheme.Color("SurfaceLight"), StatusTheme.Color("SurfaceDark"));
            _pickerBorder.SetAppThemeColor(Border.StrokeProperty, StatusTheme.Color("Gray200"), StatusTheme.Color("ChipStrokeDark"));
            _pickerValue.SetAppThemeColor(Label.TextColorProperty, StatusTheme.Color("TextSecondaryLight"), StatusTheme.Color("TextSecondaryDark"));
        }
    }

    private static ChipSlot CreateChipSlot(StatusFilterItem item)
    {
        var border = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = 14 },
            StrokeThickness = 1,
            Padding = new Thickness(12, 8),
            MinimumHeightRequest = 48,
            InputTransparent = true,
        };
        ExcludeVisualFromAccessibility(border);

        var title = new Label
        {
            FontFamily = "BodySemibold",
            FontSize = 14,
            LineBreakMode = LineBreakMode.WordWrap,
            InputTransparent = true,
        };
        AutomationProperties.SetIsInAccessibleTree(title, false);

        var state = new Label
        {
            FontFamily = "BodyRegular",
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap,
            InputTransparent = true,
        };
        state.SetAppThemeColor(Label.TextColorProperty, StatusTheme.Color("TextSecondaryLight"), StatusTheme.Color("TextSecondaryDark"));
        AutomationProperties.SetIsInAccessibleTree(state, false);

        var stack = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true,
            SafeAreaEdges = SafeAreaEdges.None,
        };
        AutomationProperties.SetIsInAccessibleTree(stack, false);
        stack.Add(title);
        stack.Add(state);
        border.Content = stack;

        var button = CreateOverlayButton(cornerRadius: 14, minHeight: 48);
        AttachPressFeedback(button, border);

        var container = new Grid { SafeAreaEdges = SafeAreaEdges.None };
        container.Add(border);
        container.Add(button);

        return new ChipSlot(item, container, border, title, state, button);
    }

    internal static Button CreateOverlayButton(int cornerRadius, double minHeight)
    {
        var button = new Button
        {
            Text = string.Empty,
            BackgroundColor = Colors.Transparent,
            BorderColor = Colors.Transparent,
            BorderWidth = 0,
            CornerRadius = cornerRadius,
            Padding = 0,
            HeightRequest = -1,
            MinimumHeightRequest = minHeight,
            MinimumWidthRequest = 44,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };

        var normal = new VisualState { Name = "Normal" };
        var pointerOver = new VisualState { Name = "PointerOver" };
        var pressed = new VisualState { Name = "Pressed" };
        var focused = new VisualState { Name = "Focused" };
        focused.Setters.Add(new Setter { Property = Button.BorderColorProperty, Value = StatusTheme.Color("PrimaryStroke") });
        focused.Setters.Add(new Setter { Property = Button.BorderWidthProperty, Value = 2d });
        var disabled = new VisualState { Name = "Disabled" };
        disabled.Setters.Add(new Setter { Property = VisualElement.OpacityProperty, Value = 0.45d });

        var group = new VisualStateGroup { Name = "CommonStates" };
        group.States.Add(normal);
        group.States.Add(pointerOver);
        group.States.Add(pressed);
        group.States.Add(focused);
        group.States.Add(disabled);
        VisualStateManager.SetVisualStateGroups(button, [group]);

        return button;
    }

    internal static void AttachPressFeedback(Button button, View visual)
    {
        button.Pressed += (_, _) => visual.Opacity = 0.72;
        button.Released += (_, _) => visual.Opacity = 1.0;
    }

    internal static void ExcludeVisualFromAccessibility(View view)
    {
        AutomationProperties.SetIsInAccessibleTree(view, false);
        AutomationProperties.SetExcludedWithChildren(view, true);
#if IOS
        void Apply()
        {
            if (view.Handler?.PlatformView is UIKit.UIView native)
            {
                native.IsAccessibilityElement = false;
                native.AccessibilityElementsHidden = true;
            }
        }

        view.HandlerChanged += (_, _) => Apply();
        view.Loaded += (_, _) => Apply();
#endif
    }

    private sealed class ChipSlot(
        StatusFilterItem item,
        Grid container,
        Border border,
        Label title,
        Label state,
        Button button)
    {
        public StatusFilterItem Item { get; } = item;
        public Grid Container { get; } = container;
        public Border Border { get; } = border;
        public Label Title { get; } = title;
        public Label State { get; } = state;
        public Button Button { get; } = button;

        public void SetSingleRow(bool singleRow)
        {
            State.IsVisible = !singleRow;
            Title.HorizontalTextAlignment = singleRow ? TextAlignment.Center : TextAlignment.Start;
            Border.StrokeShape = new RoundRectangle { CornerRadius = singleRow ? 22 : 14 };
            Border.Padding = singleRow ? new Thickness(12, 10) : new Thickness(12, 8);
            Border.MinimumHeightRequest = singleRow ? 44 : 48;
            Button.CornerRadius = singleRow ? 22 : 14;
            Button.MinimumHeightRequest = singleRow ? 44 : 48;
        }
    }
}

/// <summary>
/// Bottom sheet listing all four status filters as 48pt+ toggleable native buttons
/// when <see cref="StatusFilterBar"/> is in <see cref="StatusFilterMode.CompactPicker"/> mode (#708).
/// </summary>
public sealed class StatusFilterSheet : Border
{
    private readonly VerticalStackLayout _rows;
    private readonly ScrollView _scroller;
    private readonly List<SheetRow> _sheetRows = [];
    private StatusFilterBar? _bar;

    public StatusFilterSheet()
    {
        SafeAreaEdges = SafeAreaEdges.None;
        StrokeShape = new RoundRectangle { CornerRadius = 16 };
        StrokeThickness = 1;
        Padding = new Thickness(14);
        IsVisible = false;
        this.SetAppThemeColor(BackgroundColorProperty, StatusTheme.Color("SurfaceLight"), StatusTheme.Color("SurfaceDark"));
        this.SetAppThemeColor(StrokeProperty, StatusTheme.Color("StrokeLight"), StatusTheme.Color("StrokeDark"));

        var title = new Label
        {
            Text = "Status",
            FontFamily = "BodySemibold",
            FontSize = 15,
            VerticalOptions = LayoutOptions.Center,
        };
        SemanticProperties.SetHeadingLevel(title, SemanticHeadingLevel.Level2);

        var done = new Button
        {
            Text = "Done",
            BackgroundColor = Colors.Transparent,
            FontFamily = "BodySemibold",
            FontSize = 14,
            Padding = new Thickness(8, 0),
            MinimumHeightRequest = 44,
            MinimumWidthRequest = 44,
            VerticalOptions = LayoutOptions.Center,
        };
        done.SetAppThemeColor(Button.TextColorProperty, StatusTheme.Color("PrimaryTextLight"), StatusTheme.Color("PrimaryTextDark"));
        done.Clicked += (_, _) => CloseRequested?.Invoke(this, EventArgs.Empty);

        var header = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            SafeAreaEdges = SafeAreaEdges.None,
        };
        header.Add(title, 0, 0);
        header.Add(done, 1, 0);

        _rows = new VerticalStackLayout
        {
            Spacing = 0,
            SafeAreaEdges = SafeAreaEdges.None,
        };
        _scroller = new ScrollView
        {
            Content = _rows,
            SafeAreaEdges = SafeAreaEdges.None,
        };

        var layout = new Grid
        {
            RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
            RowSpacing = 4,
            SafeAreaEdges = SafeAreaEdges.None,
        };
        layout.Add(header, 0, 0);
        layout.Add(_scroller, 0, 1);
        Content = layout;
    }

    /// <summary>Raised when the user taps Done.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Binds this sheet to the same four <see cref="StatusFilterItem"/> choices as <paramref name="bar"/>.</summary>
    public void Attach(StatusFilterBar bar)
    {
        if (_bar == bar)
        {
            return;
        }

        _bar = bar;
        _rows.Children.Clear();
        _sheetRows.Clear();

        for (var i = 0; i < bar.Items.Count; i++)
        {
            if (i > 0)
            {
                var divider = new BoxView { HeightRequest = 1 };
                divider.SetAppThemeColor(BoxView.ColorProperty, StatusTheme.Color("StrokeLight"), StatusTheme.Color("StrokeDark"));
                AutomationProperties.SetIsInAccessibleTree(divider, false);
                _rows.Add(divider);
            }

            var item = bar.Items[i];
            var row = CreateRow(item);
            _sheetRows.Add(row);
            _rows.Add(row.Container);
            item.Changed += (_, _) => RefreshRow(row);
            RefreshRow(row);
        }
    }

    /// <summary>Caps the sheet height so it stays scrollable on short windows or huge Dynamic Type.</summary>
    public void UpdateMaxHeight(double viewportHeight)
    {
        if (viewportHeight <= 0 || !double.IsFinite(viewportHeight))
        {
            return;
        }

        var maxBody = Math.Max(140, (viewportHeight * 0.52) - 72);
        if (Math.Abs(_scroller.MaximumHeightRequest - maxBody) > 1)
        {
            _scroller.MaximumHeightRequest = maxBody;
        }
    }

    private static SheetRow CreateRow(StatusFilterItem item)
    {
        var dot = new Ellipse
        {
            WidthRequest = 10,
            HeightRequest = 10,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true,
        };
        AutomationProperties.SetIsInAccessibleTree(dot, false);

        var label = new Label
        {
            FontFamily = "BodySemibold",
            FontSize = 15,
            LineBreakMode = LineBreakMode.WordWrap,
            InputTransparent = true,
        };
        AutomationProperties.SetIsInAccessibleTree(label, false);

        var state = new Label
        {
            FontFamily = "BodyRegular",
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap,
            InputTransparent = true,
        };
        state.SetAppThemeColor(Label.TextColorProperty, StatusTheme.Color("TextSecondaryLight"), StatusTheme.Color("TextSecondaryDark"));
        AutomationProperties.SetIsInAccessibleTree(state, false);

        var textStack = new VerticalStackLayout
        {
            Spacing = 1,
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true,
            SafeAreaEdges = SafeAreaEdges.None,
        };
        AutomationProperties.SetIsInAccessibleTree(textStack, false);
        textStack.Add(label);
        textStack.Add(state);

        var tick = new TickCircle
        {
            VerticalOptions = LayoutOptions.Center,
            InputTransparent = true,
        };
        AutomationProperties.SetIsInAccessibleTree(tick, false);

        var visual = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            ColumnSpacing = 12,
            Padding = new Thickness(4, 10),
            MinimumHeightRequest = 48,
            InputTransparent = true,
            SafeAreaEdges = SafeAreaEdges.None,
        };
        StatusFilterBar.ExcludeVisualFromAccessibility(visual);
        visual.Add(dot, 0, 0);
        visual.Add(textStack, 1, 0);
        visual.Add(tick, 2, 0);

        var button = StatusFilterBar.CreateOverlayButton(cornerRadius: 10, minHeight: 48);
        StatusFilterBar.AttachPressFeedback(button, visual);

        var container = new Grid { SafeAreaEdges = SafeAreaEdges.None };
        container.Add(visual);
        container.Add(button);

        return new SheetRow(item, container, dot, label, state, tick, button);
    }

    private static void RefreshRow(SheetRow row)
    {
        var item = row.Item;
        row.Label.Text = item.Label;
        row.State.Text = StatusFilterLayout.ChipStateText(item.IsIncluded);
        row.Tick.IsTicked = item.IsIncluded;
        row.Button.Command = item.Command;
        SemanticProperties.SetDescription(row.Button, StatusFilterLayout.ChipDescription(item.Label, item.IsIncluded));

        if (item.IsIncluded)
        {
            row.Dot.Fill = new SolidColorBrush(StatusTheme.Color(item.Tone == "Primary" ? "Primary" : item.Tone + "Color"));
            row.Label.SetAppThemeColor(
                Label.TextColorProperty,
                StatusTheme.Color(item.Tone + "TextLight"),
                StatusTheme.Color(item.Tone + "TextDark"));
        }
        else
        {
            row.Dot.Fill = new SolidColorBrush(StatusTheme.Color("InactiveColor"));
            row.Label.SetAppThemeColor(Label.TextColorProperty, StatusTheme.Color("TextSecondaryLight"), StatusTheme.Color("TextSecondaryDark"));
        }
    }

    private sealed class SheetRow(
        StatusFilterItem item,
        Grid container,
        Ellipse dot,
        Label label,
        Label state,
        TickCircle tick,
        Button button)
    {
        public StatusFilterItem Item { get; } = item;
        public Grid Container { get; } = container;
        public Ellipse Dot { get; } = dot;
        public Label Label { get; } = label;
        public Label State { get; } = state;
        public TickCircle Tick { get; } = tick;
        public Button Button { get; } = button;
    }
}

internal static class StatusTheme
{
    public static Color Color(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Colors.Transparent;
}
