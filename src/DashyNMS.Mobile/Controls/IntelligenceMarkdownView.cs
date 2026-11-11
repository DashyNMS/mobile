using DashyNMS.Mobile.Intelligence;
using DesktopNMS.Core.Intelligence;
using Microsoft.Maui.Controls.Shapes;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Draws an Intelligence answer from <see cref="IntelligenceMarkdown"/>'s blocks
/// (#228). Text is always set as plain spans, so nothing in an answer is parsed as
/// HTML or executed. Verified objects read as their name and status in text, with
/// optional status colour; tapping one asks
/// the host to open it. Tables scroll sideways rather than squeezing columns.
/// Redraws only when the text, evidence or colour choice changes, and the page
/// already coalesces streaming updates to ten a second.
/// </summary>
public sealed class IntelligenceMarkdownView : VerticalStackLayout
{
    private string? _markdown;
    private IReadOnlyList<IntelligenceInlineReference>? _references;
    private bool? _colour;
    private bool _referencesExpanded;

    public IntelligenceMarkdownView() => Spacing = 10;

    /// <summary>A verified object was chosen; the host checks ownership again before opening it.</summary>
    public event EventHandler<IntelligenceInlineReference>? ReferenceRequested;

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

        // Spans alone are not reliable accessible activation targets on native labels.
        // A single disclosure offers full touch targets without persistent reference rows.
        var objects = IntelligenceMarkdown.LinkedObjects(blocks);
        if (objects.Count > 0)
        {
            var choices = new VerticalStackLayout { Spacing = 0, IsVisible = _referencesExpanded };
            var toggle = new Button { Text = _referencesExpanded ? "Hide referenced items" : "Open referenced item", MinimumHeightRequest = 44, Style = Resource<Style>("LinkButton") };
            SemanticProperties.SetDescription(toggle, _referencesExpanded ? "Hide referenced items, expanded" : "Open referenced item, collapsed");
            toggle.Clicked += (_, _) =>
            {
                choices.IsVisible = !choices.IsVisible;
                _referencesExpanded = choices.IsVisible;
                toggle.Text = choices.IsVisible ? "Hide referenced items" : "Open referenced item";
                SemanticProperties.SetDescription(toggle, choices.IsVisible ? "Hide referenced items, expanded" : "Open referenced item, collapsed");
            };
            foreach (var reference in objects)
            {
                var button = new Button { Text = IntelligenceStatusTone.Describe(reference), MinimumHeightRequest = 44, Style = Resource<Style>("LinkButton") };
                button.Clicked += (_, _) => ReferenceRequested?.Invoke(this, reference);
                choices.Children.Add(button);
            }
            Children.Add(toggle);
            Children.Add(choices);
        }

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
                    Text = $"{link.Text} ({uri.Host})", Style = Resource<Style>("LinkButton"), MinimumHeightRequest = 44,
                    HorizontalOptions = LayoutOptions.Start, LineBreakMode = LineBreakMode.TailTruncation, Padding = new Thickness(0, 0, 8, 0),
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
        return label;
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
                result.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => ReferenceRequested?.Invoke(this, reference)) });
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
        var grid = new Grid { ColumnSpacing = 1, RowSpacing = 1, Padding = 1 };
        grid.SetAppThemeColor(BackgroundColorProperty, Resource<Color>("StrokeLight"), Resource<Color>("ChipStrokeDark"));
        for (var c = 0; c < columns; c++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        for (var r = 0; r < table.Rows.Count; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var c = 0; c < columns; c++)
            {
                var cell = Text(c < table.Rows[r].Count ? table.Rows[r][c] : [], 14, bold: r == 0);
                cell.MaximumWidthRequest = 260;
                cell.HorizontalTextAlignment = c < table.Alignments.Count ? table.Alignments[c] switch
                {
                    MarkdownAlignment.Centre => TextAlignment.Center,
                    MarkdownAlignment.End => TextAlignment.End,
                    _ => TextAlignment.Start,
                } : TextAlignment.Start;
                var host = new ContentView { Content = cell, Padding = new Thickness(10, 6) };
                if (r == 0) host.SetAppThemeColor(BackgroundColorProperty, Resource<Color>("SurfaceAltLight"), Resource<Color>("SurfaceAltDark"));
                else host.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#ECF3FC"), Color.FromArgb("#0A1526"));
                grid.Add(host, c, r);
            }
        }
        var scroll = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal, Content = grid, SafeAreaEdges = SafeAreaEdges.None,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Default,
        };
        SemanticProperties.SetDescription(scroll, $"Table, {table.Rows.Count} rows and {columns} columns. Scroll sideways for more.");
        return scroll;
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
