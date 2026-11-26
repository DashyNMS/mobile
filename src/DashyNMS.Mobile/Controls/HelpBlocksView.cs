using DashyNMS.Mobile.Help;
using DesktopNMS.Core.Help;
using Microsoft.Maui.Controls.Shapes;

namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Draws a help page with the app's own type, colours and spacing (#668): no
/// web view, so the theme and text size apply as they do everywhere else.
/// </summary>
/// <remarks>
/// A link inside a sentence is too small to aim at, and VoiceOver can't reach
/// a tappable span. So text with one link is a button for that link as a
/// whole, and text with several is followed by a button for each.
/// </remarks>
public sealed class HelpBlocksView : VerticalStackLayout
{
    private const double BodySize = 15;

    private readonly Dictionary<string, View> _headings = new(StringComparer.OrdinalIgnoreCase);

    public HelpBlocksView() => Spacing = 12;

    /// <summary>Where a link goes, so one that leaves the app can say so and a broken one can be plain text.</summary>
    public Func<string, HelpLink>? ResolveLink { get; set; }

    /// <summary>A page's title, for naming a link to it.</summary>
    public Func<HelpPageLink, string?>? TitleOf { get; set; }

    /// <summary>An image's bytes, or null to show its description.</summary>
    public Func<string, HelpImage?>? ResolveImage { get; set; }

    /// <summary>A link was chosen; its target as written.</summary>
    public event EventHandler<string>? LinkActivated;

    /// <summary>The view at a heading, or null when the page has none by that name.</summary>
    public View? FindHeading(string? anchor) => anchor is null ? null : _headings.GetValueOrDefault(anchor);

    public void Render(HelpPage page)
    {
        Children.Clear();
        _headings.Clear();

        // The guide's own headings, in the same order, name each one's anchor.
        var anchors = new Queue<HelpHeading>(page.Headings);
        foreach (var block in HelpBlocks.Read(page.Markdown))
        {
            Children.Add(Draw(block, anchors));
        }
    }

    private View Draw(HelpBlock block, Queue<HelpHeading> anchors) => block switch
    {
        HelpHeadingBlock heading => Heading(heading, anchors),
        HelpParagraphBlock paragraph => Paragraph(paragraph.Spans),
        HelpListBlock list => List(list, anchors),
        HelpCodeBlock code => Code(code.Text),
        HelpTableBlock table => Table(table),
        HelpQuoteBlock quote => Quote(quote, anchors),
        HelpImageBlock image => Picture(image),
        _ => Line(),
    };

    private Label Heading(HelpHeadingBlock heading, Queue<HelpHeading> anchors)
    {
        var label = Text(heading.Spans, heading.Level <= 2 ? 18 : 16, bold: true);
        label.Margin = new Thickness(0, heading.Level <= 2 ? 12 : 4, 0, 0);
        SemanticProperties.SetHeadingLevel(label, heading.Level <= 2 ? SemanticHeadingLevel.Level2 : SemanticHeadingLevel.Level3);
        if (anchors.TryDequeue(out var named))
        {
            _headings[named.Anchor] = label;
        }

        return label;
    }

    private View Paragraph(IReadOnlyList<HelpSpan> spans, string? number = null)
    {
        var links = Links(spans);
        if (links.Count == 0)
        {
            return Text(spans);
        }

        if (links.Count == 1)
        {
            return LinkRow(spans, links[0], number);
        }

        var stack = new VerticalStackLayout { Spacing = 0 };
        var words = string.Concat(spans.Where(span => !links.Any(link => link.Target == span.Link)).Select(span => span.Text));
        if (words.Any(char.IsLetterOrDigit) || number is not null)
        {
            // The step's number is read with its words, since the marker beside them is hidden.
            var prose = Text(spans, colourLinks: false);
            if (number is not null) SemanticProperties.SetDescription(prose, Spoken(spans, number));
            stack.Add(prose);
        }

        foreach (var link in links)
        {
            var button = new Button
            {
                Text = link.Text,
                Style = Resource<Style>("LinkButton"),
                MinimumHeightRequest = 44,
                Padding = new Thickness(0, 6, 8, 6),
                HorizontalOptions = LayoutOptions.Start,
                LineBreakMode = LineBreakMode.WordWrap,
            };
            SemanticProperties.SetDescription(button, link.Text);
            SemanticProperties.SetHint(button, Hint(link));
            button.Clicked += (_, _) => LinkActivated?.Invoke(this, link.Target);
            stack.Add(button);
        }

        return stack;
    }

    /// <summary>Text whose one link is the whole row's button, at least 44pt high.</summary>
    private View LinkRow(IReadOnlyList<HelpSpan> spans, Link link, string? number)
    {
        var label = Text(spans);
        label.VerticalOptions = LayoutOptions.Start;
        label.Margin = new Thickness(0, 11);
        label.InputTransparent = true;
        AutomationProperties.SetIsInAccessibleTree(label, false);

        // The button says it all; the words under it are hidden with their container, which UIKit honours for labels.
        var words = new ContentView { Content = label, InputTransparent = true };
        AutomationProperties.SetExcludedWithChildren(words, true);

        var button = new Button
        {
            Style = new Style(typeof(Button)),
            BackgroundColor = Colors.Transparent,
            BorderWidth = 0,
            CornerRadius = 12,
            Padding = 0,
            Text = string.Empty,
            MinimumHeightRequest = 44,
        };
        var states = new VisualStateGroup { Name = "CommonStates" };
        foreach (var name in new[] { "Normal", "Pressed", "Focused", "Disabled" })
        {
            var state = new VisualState { Name = name };
            state.Setters.Add(new Setter
            {
                Property = BackgroundColorProperty,
                Value = name is "Pressed" or "Focused" ? Resource<Color>("PrimaryTint") : Colors.Transparent,
            });
            states.States.Add(state);
        }

        VisualStateManager.GetVisualStateGroups(button).Add(states);
        SemanticProperties.SetDescription(button, Spoken(spans, number));
        SemanticProperties.SetHint(button, Hint(link));
        button.Clicked += (_, _) => LinkActivated?.Invoke(this, link.Target);
        return new Grid { MinimumHeightRequest = 44, Children = { words, button } };
    }

    /// <summary>
    /// Each item is one element to VoiceOver, numbered as it reads; the marker
    /// itself is hidden from it.
    /// </summary>
    private View List(HelpListBlock list, Queue<HelpHeading> anchors)
    {
        var stack = new VerticalStackLayout { Spacing = 8 };
        for (var index = 0; index < list.Items.Count; index++)
        {
            var number = list.Start is { } start ? $"{start + index}." : null;
            var item = list.Items[index];
            var content = new VerticalStackLayout { Spacing = 8 };
            var first = item.Count > 0 && item[0] is HelpParagraphBlock paragraph ? Paragraph(paragraph.Spans, number) : null;
            if (first is not null)
            {
                content.Add(first);
            }

            foreach (var block in first is null ? item : item.Skip(1))
            {
                content.Add(Draw(block, anchors));
            }

            // A link's row centres its words in its 44pt, so the marker sits level with them.
            var inset = first is Grid { Children: [ContentView, ..] } ? 11 : 0;
            var row = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star)], ColumnSpacing = 10 };
            var marker = new ContentView { Content = Marker(number, inset), VerticalOptions = LayoutOptions.Start };
            AutomationProperties.SetExcludedWithChildren(marker, true);
            row.Add(marker, 0);
            row.Add(content, 1);
            if (item.Count == 1 && first is Label && item[0] is HelpParagraphBlock only)
            {
                SemanticProperties.SetDescription(row, Spoken(only.Spans, number));
            }
            else if (number is not null && first is Label leading && item[0] is HelpParagraphBlock opening)
            {
                // More under the step than its words: the number is read with them, the rest on its own.
                SemanticProperties.SetDescription(leading, Spoken(opening.Spans, number));
            }

            stack.Add(row);
        }

        return stack;
    }

    private static View Marker(string? number, double inset)
    {
        // Text, so it grows and sits on the first line at any text size; hidden from VoiceOver natively,
        // since IsInAccessibleTree alone can still leave a UIKit label readable.
        var label = new Label
        {
            Text = number ?? "•",
            FontSize = BodySize,
            MinimumWidthRequest = number is null ? 10 : 20,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, inset, 0, 0),
        };
        if (number is null)
        {
            label.SetAppThemeColor(Label.TextColorProperty, Resource<Color>("TextSecondaryLight"), Resource<Color>("TextSecondaryDark"));
        }

        AutomationProperties.SetIsInAccessibleTree(label, false);
#if IOS
        label.HandlerChanged += (_, _) =>
        {
            if (label.Handler?.PlatformView is UIKit.UIView native) native.IsAccessibilityElement = false;
        };
#endif
        return label;
    }

    private static string Spoken(IReadOnlyList<HelpSpan> spans, string? number)
        => (number is null ? string.Empty : number + " ") + string.Concat(spans.Select(span => span.Text));

    /// <summary>
    /// Each row as its own block - the first cell, then the others beneath - so
    /// nothing has to scroll sideways and VoiceOver reads it row by row.
    /// </summary>
    private View Table(HelpTableBlock table)
    {
        var stack = new VerticalStackLayout();
        var headers = table.Rows[0];
        foreach (var row in table.Rows.Skip(1))
        {
            if (stack.Count > 0)
            {
                stack.Add(Line());
            }

            var cells = new VerticalStackLayout { Padding = new Thickness(14, 11), Spacing = 4 };
            for (var column = 0; column < row.Count; column++)
            {
                if (column == 0)
                {
                    cells.Add(Text(row[0], BodySize, bold: true));
                    continue;
                }

                if (row.Count > 2 && column < headers.Count)
                {
                    cells.Add(new Label { Text = string.Concat(headers[column].Select(span => span.Text)), Style = Resource<Style>("Caption") });
                }

                cells.Add(Paragraph(row[column]));
            }

            stack.Add(cells);
        }

        return new Border { Style = Resource<Style>("ListCard"), Content = stack };
    }

    private static View Code(string code)
    {
        var label = new Label { Text = code, FontFamily = "Mono", FontSize = 13, LineBreakMode = LineBreakMode.CharacterWrap };
        var border = new Border { Content = label, Padding = new Thickness(12, 10), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 12 } };
        border.SetAppThemeColor(BackgroundColorProperty, Resource<Color>("SurfaceAltLight"), Resource<Color>("CodeDark"));
        return border;
    }

    private View Quote(HelpQuoteBlock quote, Queue<HelpHeading> anchors)
    {
        var content = new VerticalStackLayout { Spacing = 8 };
        foreach (var inner in quote.Blocks)
        {
            content.Add(Draw(inner, anchors));
        }

        var bar = new BoxView();
        bar.SetAppThemeColor(BoxView.ColorProperty, Resource<Color>("PrimaryTextLight"), Resource<Color>("PrimaryTextDark"));
        AutomationProperties.SetIsInAccessibleTree(bar, false);
        var grid = new Grid { ColumnDefinitions = [new(new GridLength(3)), new(GridLength.Star)], ColumnSpacing = 10 };
        grid.Add(bar, 0);
        grid.Add(content, 1);
        return grid;
    }

    private View Picture(HelpImageBlock image)
    {
        // Only what the platform's own decoders read; anything else is its description.
        if (ResolveImage?.Invoke(image.Target) is { } found
            && System.IO.Path.GetExtension(found.Path).ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".gif")
        {
            var content = found.Content;
            var picture = new Image { Source = ImageSource.FromStream(() => new MemoryStream(content)), Aspect = Aspect.AspectFit };
            SemanticProperties.SetDescription(picture, image.AltText);
            return new Border { Style = Resource<Style>("Card"), Padding = 0, Content = picture };
        }

        return new Label { Text = image.AltText, Style = Resource<Style>("Caption") };
    }

    private static BoxView Line()
    {
        var line = new BoxView { Style = Resource<Style>("Divider") };
        AutomationProperties.SetIsInAccessibleTree(line, false);
        return line;
    }

    private Label Text(IReadOnlyList<HelpSpan> spans, double size = BodySize, bool bold = false, bool colourLinks = true)
    {
        var text = new FormattedString();
        foreach (var span in spans)
        {
            var run = new Span { Text = span.Text };
            if (bold || span.IsBold) run.FontFamily = "BodySemibold";
            if (span.IsItalic) run.FontAttributes = FontAttributes.Italic;
            if (span.IsCode)
            {
                run.FontFamily = "Mono";
                run.SetAppThemeColor(Span.BackgroundColorProperty, Resource<Color>("SurfaceAltLight"), Resource<Color>("CodeDark"));
            }

            if (colourLinks && span.Link is { } target && Opens(target))
            {
                run.FontFamily = "BodySemibold";
                run.SetAppThemeColor(Span.TextColorProperty, Resource<Color>("PrimaryTextLight"), Resource<Color>("PrimaryTextDark"));
            }

            text.Spans.Add(run);
        }

        var label = new Label { FormattedText = text, FontSize = size, LineBreakMode = LineBreakMode.WordWrap };
        if (bold) label.FontFamily = "BodySemibold";
        return label;
    }

    private sealed record Link(string Target, string Text, HelpLink Goes);

    private List<Link> Links(IReadOnlyList<HelpSpan> spans) => spans
        .Where(span => span.Link is not null && Opens(span.Link))
        .GroupBy(span => span.Link!)
        .Select(group => new Link(group.Key, string.Concat(group.Select(span => span.Text)), ResolveLink!(group.Key)))
        .ToList();

    private bool Opens(string target) => ResolveLink?.Invoke(target) is { } link && HelpArticleViewModel.Opens(link);

    private string Hint(Link link) => link.Goes switch
    {
        HelpPageLink page when TitleOf?.Invoke(page) is { } title => "Opens " + title + " in Help",
        HelpExternalLink { Address.Scheme: "mailto" } => "Opens your email app",
        HelpExternalLink => "Opens in your browser",
        _ => "Opens in Help",
    };

    private static T Resource<T>(string key) => (T)Application.Current!.Resources[key];
}
