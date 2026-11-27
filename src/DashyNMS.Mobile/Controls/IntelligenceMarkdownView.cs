using DashyNMS.Mobile.Intelligence;
using DesktopNMS.Core.Intelligence;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Draws an Intelligence answer from <see cref="IntelligenceMarkdown"/>'s blocks
/// (#228). Text is always set as plain spans, so nothing in an answer is parsed as
/// HTML or executed. Verified objects read as their name and status in text, with
/// optional status colour; tapping one asks
/// the host to open it. Tables adapt to the width of the answer.
/// Redraws only when the text, evidence or colour choice changes, and the page
/// already coalesces streaming updates to ten a second.
/// </summary>
public sealed class IntelligenceMarkdownView : VerticalStackLayout
{
    private string? _markdown;
    private IReadOnlyList<IntelligenceInlineReference>? _references;
    private bool? _colour;

    public IntelligenceMarkdownView() => Spacing = 10;

    /// <summary>A verified object was chosen; the host checks ownership again before opening it.</summary>
    public event EventHandler<IntelligenceInlineReference>? ReferenceRequested;
    public event EventHandler<IntelligenceInlineReference>? ReferenceMenuRequested;
    public event EventHandler<IntelligenceInlineReference>? AddSpotlightRequested;
    public event EventHandler<IntelligenceInlineReference>? OnlySpotlightRequested;

    /// <summary>A link that passed Core's web-link policy was chosen; the host opens it outside the app.</summary>
    public event EventHandler<Uri>? LinkRequested;

    public void Render(string markdown, IReadOnlyList<IntelligenceInlineReference> references, bool colourStatusIndicators)
    {
        if (_markdown == markdown && ReferenceEquals(_references, references) && _colour == colourStatusIndicators) return;
        _markdown = markdown;
        _references = references;
        _colour = colourStatusIndicators;
        Children.Clear();
        if (markdown.Length == 0) return;
        var blocks = IntelligenceMarkdown.Parse(markdown, references);
        foreach (var block in blocks) Children.Add(Block(block));

        var links = IntelligenceMarkdown.Links(blocks);
        if (links.Count > 0)
        {
            var sources = new VerticalStackLayout { Spacing = 0 };
            sources.Children.Add(new Label { Text = "Sources", Style = Resource<Style>("Caption"), FontFamily = "BodySemibold" });
            foreach (var link in links)
            {
                var uri = link.Link!;
                var button = new Button
                {
                    Text = $"{link.Text} ({uri.Host})", Style = Resource<Style>("LinkButton"), MinimumHeightRequest = 48,
                    HorizontalOptions = LayoutOptions.Start, LineBreakMode = LineBreakMode.WordWrap, Padding = new Thickness(0, 0, 8, 0),
                };
                SemanticProperties.SetDescription(button, $"Open link in browser: {link.Text}, {uri.Host}");
                button.Clicked += (_, _) => LinkRequested?.Invoke(this, uri);
                sources.Children.Add(button);
            }
            Children.Add(sources);
        }
    }

    private View Block(MarkdownBlock block) => block switch
    {
        MarkdownTextBlock { Kind: MarkdownTextKind.Heading } heading => Heading(heading),
        MarkdownTextBlock { Kind: MarkdownTextKind.Quote } quote => Barred(Text(quote.Spans, secondary: true), ThemePair("StrokeLight", "ChipStrokeDark")),
        MarkdownTextBlock { Kind: MarkdownTextKind.Recommendation } recommendation => Barred(Text(recommendation.Spans, prefix: "Recommendation. "), ThemePair("PrimaryTextLight", "PrimaryTextDark")),
        MarkdownTextBlock { Kind: MarkdownTextKind.Notes } notes => Notes(notes.Spans),
        MarkdownTextBlock paragraph => Text(paragraph.Spans),
        MarkdownListBlock list => List(list),
        MarkdownTableBlock table => Table(table),
        MarkdownCodeBlock code => Code(code.Text),
        _ => Line(new Thickness(0, 4)),
    };

    private Label Heading(MarkdownTextBlock heading)
    {
        var label = Text(heading.Spans, heading.Level switch { 1 => 20, 2 => 18, _ => 16 }, bold: true);
        SemanticProperties.SetHeadingLevel(label, heading.Level switch
        {
            1 => SemanticHeadingLevel.Level1,
            2 => SemanticHeadingLevel.Level2,
            _ => SemanticHeadingLevel.Level3,
        });
        return label;
    }

    private Label Text(IReadOnlyList<MarkdownSpan> spans, double size = 15, bool bold = false, bool secondary = false, string? prefix = null)
    {
        var text = new FormattedString();
        if (prefix is not null) text.Spans.Add(new Span { Text = prefix, FontFamily = "BodySemibold" });
        foreach (var span in spans) text.Spans.Add(ToSpan(span, bold));
        var label = new Label { FormattedText = text, FontSize = size, LineBreakMode = LineBreakMode.WordWrap };
        if (bold) label.FontFamily = "BodySemibold";
        if (secondary) label.SetAppThemeColor(Label.TextColorProperty, Resource<Color>("TextSecondaryLight"), Resource<Color>("TextSecondaryDark"));
        OfferOpenActions(label, spans);
        return label;
    }

    /// <summary>
    /// Tappable spans are not reliable screen-reader targets, so the text that
    /// names a verified item offers its action ("Open device …", "Show devices
    /// in group …") in VoiceOver's Actions rotor instead
    /// of a separate list of referenced items under the answer.
    /// </summary>
    private void OfferOpenActions(Label label, IReadOnlyList<MarkdownSpan> spans)
    {
        var actions = spans.Select(span => span.Reference)
            .OfType<IntelligenceInlineReference>()
            .Where(reference => reference.Object is { IsValid: true })
            .DistinctBy(reference => reference.Key)
            .SelectMany(reference => new[]
            {
                new AccessibilityActions(label, IntelligenceStatusTone.Describe(reference), () => { ReferenceRequested?.Invoke(this, reference); return true; }),
                new AccessibilityActions(label, "Add " + reference.Name + " to Spotlight", () => { AddSpotlightRequested?.Invoke(this, reference); return true; }),
                new AccessibilityActions(label, "Spotlight only " + reference.Name, () => { OnlySpotlightRequested?.Invoke(this, reference); return true; }),
            })
            .ToArray();
        if (actions.Length == 0) return;
        label.HandlerChanged += (_, _) => { foreach (var action in actions) action.Refresh(true); };
    }

    private Span ToSpan(MarkdownSpan span, bool bold)
    {
        var result = new Span { Text = span.Display };
        if (span.Style.HasFlag(MarkdownSpanStyle.Bold) || bold || span.Reference is not null) result.FontFamily = "BodySemibold";
        if (span.Style.HasFlag(MarkdownSpanStyle.Italic)) result.FontAttributes = FontAttributes.Italic;
        if (span.Style.HasFlag(MarkdownSpanStyle.Code))
        {
            result.FontFamily = "Mono";
            result.SetAppThemeColor(Microsoft.Maui.Controls.Span.BackgroundColorProperty, Resource<Color>("SurfaceAltLight"), Resource<Color>("CodeDark"));
        }
        if (span.Link is { } uri)
        {
            result.TextDecorations = TextDecorations.Underline;
            result.SetAppThemeColor(Microsoft.Maui.Controls.Span.TextColorProperty, Resource<Color>("PrimaryTextLight"), Resource<Color>("PrimaryTextDark"));
            result.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => LinkRequested?.Invoke(this, uri)) });
        }
        else if (span.Reference is { } reference)
        {
            if (ToneColour(reference.Tone) is { } colour)
                result.SetAppThemeColor(Microsoft.Maui.Controls.Span.TextColorProperty, colour.Light, colour.Dark);
            if (reference.Object is { IsValid: true })
                result.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => ReferenceMenuRequested?.Invoke(this, reference)) });
        }
        return result;
    }

    private View List(MarkdownListBlock list)
    {
        var stack = new VerticalStackLayout { Spacing = 6 };
        foreach (var item in list.Items)
        {
            var row = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)], ColumnSpacing = 8, Margin = new Thickness(item.Depth * 18, 0, 0, 0) };
            var marker = new Label { Text = item.Marker, FontSize = 15, MinimumWidthRequest = item.Marker == "•" ? 0 : 20, VerticalOptions = LayoutOptions.Start };
            if (item.Tone is not null && ToneColour(item.Tone) is { } colour) marker.SetAppThemeColor(Label.TextColorProperty, colour.Light, colour.Dark);
            // The marker is decorative; the item text carries its status in words.
            SemanticProperties.SetDescription(marker, item.Marker == "•" ? "" : item.Marker);
            AutomationProperties.SetIsInAccessibleTree(marker, item.Marker != "•");
            row.Add(marker, 0, 0);
            row.Add(Text(item.Spans), 1, 0);
            stack.Children.Add(row);
        }
        return stack;
    }

    private View Table(MarkdownTableBlock table)
    {
        var columns = table.Columns;
        var grid = new Grid { ColumnSpacing = 20, HorizontalOptions = LayoutOptions.Start };
        var cells = new Label[table.Rows.Count, columns];
        for (var c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var r = 0; r < table.Rows.Count; r++)
        {
            if (r > 0) AddTableDivider(grid, columns, 2 * r - 1);
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var c = 0; c < columns; c++)
            {
                var cell = Text(c < table.Rows[r].Count ? table.Rows[r][c] : [], 14, bold: r == 0, secondary: r == 0);
                cell.MaximumWidthRequest = 260;
                cell.Margin = new Thickness(0, 7);
                cell.HorizontalTextAlignment = c < table.Alignments.Count ? table.Alignments[c] switch
                {
                    MarkdownAlignment.Centre => TextAlignment.Center,
                    MarkdownAlignment.End => TextAlignment.End,
                    _ => TextAlignment.Start,
                } : TextAlignment.Start;
                cells[r, c] = cell;
                grid.Add(cell, c, 2 * r);
            }
        }
        var scroll = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal, Content = grid, SafeAreaEdges = SafeAreaEdges.None,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Default,
        };
        var hint = new Label { Text = "Scroll sideways to see all columns.", Style = Resource<Style>("Caption"), LineBreakMode = LineBreakMode.WordWrap };
        return new ResponsiveTable(grid, cells, scroll, CompactTable(table), hint);
    }

    private static void AddTableDivider(Grid grid, int columns, int row)
    {
        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        var divider = Line(default);
        grid.Add(divider, 0, row);
        Grid.SetColumnSpan(divider, columns);
    }

    /// <summary>Use labelled rows only if each column has its own usable label.</summary>
    private View? CompactTable(MarkdownTableBlock table)
    {
        var headers = table.Rows[0];
        if (table.Rows.Count < 2 || headers.Count != table.Columns ||
            headers.Any(header => header.Any(span => span is not { Reference: null, Link: null }))) return null;
        var names = headers.Select(header => string.Concat(header.Select(span => span.Text)).Trim()).ToArray();
        if (names.Any(string.IsNullOrWhiteSpace) || names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Length)
            return null;

        var stack = new VerticalStackLayout { Spacing = 8 };
        foreach (var row in table.Rows.Skip(1))
        {
            if (stack.Children.Count > 0) stack.Children.Add(Line(default));
            var fields = new VerticalStackLayout { Spacing = 4 };
            for (var c = 0; c < names.Length; c++)
            {
                var spans = c < row.Count ? row[c] : [];
                fields.Children.Add(Text(spans.Count > 0 ? spans : [new("—")], 14, prefix: names[c] + ": "));
            }
            stack.Children.Add(fields);
        }
        return stack;
    }

    /// <summary>Measures native text against the current card width, including changes in text size and window geometry.</summary>
    private sealed class ResponsiveTable : Layout
    {
        private readonly Grid _grid;
        private readonly Label[,] _cells;
        private readonly ScrollView _scroll;
        private readonly View? _compact;
        private readonly Label _hint;

        public ResponsiveTable(Grid grid, Label[,] cells, ScrollView scroll, View? compact, Label hint)
        {
            _grid = grid;
            _cells = cells;
            _scroll = scroll;
            _compact = compact;
            _hint = hint;
            Children.Add(hint);
            Children.Add(scroll);
            if (compact is not null) Children.Add(compact);
        }

        protected override ILayoutManager CreateLayoutManager() => new TableLayoutManager(this);

        private sealed class TableLayoutManager(ResponsiveTable table) : LayoutManager(table)
        {
            private double _hintHeight;

            public override Size Measure(double widthConstraint, double heightConstraint)
            {
                var cells = table._cells;
                var columns = cells.GetLength(1);
                var naturalWidth = table._grid.ColumnSpacing * (columns - 1);
                for (var c = 0; c < columns; c++)
                {
                    var columnWidth = 0d;
                    // Measure the labels directly so a collapsed parent does not skip them.
                    for (var r = 0; r < cells.GetLength(0); r++)
                        columnWidth = Math.Max(columnWidth, ((IView)cells[r, c]).Measure(double.PositiveInfinity, double.PositiveInfinity).Width);
                    naturalWidth += Math.Ceiling(columnWidth);
                }

                var fits = naturalWidth <= widthConstraint;
                var showCompact = !fits && table._compact is not null;
                if (table._compact is { } compact) compact.IsVisible = showCompact;
                table._scroll.IsVisible = !showCompact;
                table._hint.IsVisible = !fits && !showCompact;
                _hintHeight = table._hint.IsVisible ? ((IView)table._hint).Measure(widthConstraint, heightConstraint).Height + 6 : 0;
                var content = showCompact ? table._compact! : table._scroll;
                var measured = ((IView)content).Measure(widthConstraint, heightConstraint);
                return new Size(Math.Min(widthConstraint, measured.Width), measured.Height + _hintHeight);
            }

            public override Size ArrangeChildren(Rect bounds)
            {
                if (table._hint.IsVisible) ((IView)table._hint).Arrange(new Rect(bounds.X, bounds.Y, bounds.Width, _hintHeight - 6));
                IView content = table._compact is { IsVisible: true } compact ? compact : table._scroll;
                content.Arrange(new Rect(bounds.X, bounds.Y + _hintHeight, bounds.Width, Math.Max(0, bounds.Height - _hintHeight)));
                return bounds.Size;
            }
        }
    }

    private View Code(string code)
    {
        var label = new Label { Text = code, FontFamily = "Mono", FontSize = 13, LineBreakMode = LineBreakMode.CharacterWrap };
        var border = new Border { Content = label, Padding = new Thickness(10, 8), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 6 } };
        border.SetAppThemeColor(BackgroundColorProperty, Resource<Color>("SurfaceAltLight"), Resource<Color>("CodeDark"));
        return border;
    }

    private static View Barred(View content, (Color Light, Color Dark) bar)
    {
        var grid = new Grid { ColumnDefinitions = [new(new GridLength(3)), new(GridLength.Star)], ColumnSpacing = 10 };
        var line = new BoxView();
        line.SetAppThemeColor(BoxView.ColorProperty, bar.Light, bar.Dark);
        grid.Add(line, 0, 0);
        grid.Add(content, 1, 0);
        return grid;
    }

    private View Notes(IReadOnlyList<MarkdownSpan> spans)
    {
        var stack = new VerticalStackLayout { Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        stack.Children.Add(Line(default));
        var text = Text(spans, 13, secondary: true, prefix: "Notes: ");
        stack.Children.Add(text);
        return stack;
    }

    private (Color Light, Color Dark)? ToneColour(string? tone) =>
        IntelligenceStatusTone.ColourKey(tone, _colour == true) is { } key ? (Resource<Color>(key + "TextLight"), Resource<Color>(key + "TextDark")) : null;

    private static BoxView Line(Thickness margin)
    {
        var line = new BoxView { HeightRequest = 1, Margin = margin };
        line.SetAppThemeColor(BoxView.ColorProperty, Resource<Color>("StrokeLight"), Resource<Color>("StrokeDark"));
        return line;
    }

    private static (Color Light, Color Dark) ThemePair(string light, string dark) => (Resource<Color>(light), Resource<Color>(dark));

    private static T Resource<T>(string key) => (T)Application.Current!.Resources[key];
}
