using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DesktopNMS.Core.Intelligence;

namespace DashyNMS.Mobile.Intelligence;

[Flags]
public enum MarkdownSpanStyle { None = 0, Bold = 1, Italic = 2, Code = 4 }

public enum MarkdownAlignment { Start, Centre, End }

public enum MarkdownTextKind { Paragraph, Heading, Quote, Recommendation, Notes }

/// <summary>
/// A run of answer text. A link is present only when it passed Core's web-link
/// policy; a reference only when Core resolved its marker against lookup evidence.
/// </summary>
public sealed record MarkdownSpan(string Text, MarkdownSpanStyle Style = MarkdownSpanStyle.None, Uri? Link = null, IntelligenceInlineReference? Reference = null)
{
    /// <summary>The visible text, which always carries a status in words as well as any colour.</summary>
    public string Display => Reference is { Status.Length: > 0 } reference ? reference.Name + " · " + reference.Status : Text;
}

public abstract record MarkdownBlock;

/// <summary>A paragraph, heading, quote, or one of the agent's marked asides.</summary>
public sealed record MarkdownTextBlock(MarkdownTextKind Kind, IReadOnlyList<MarkdownSpan> Spans, int Level = 0) : MarkdownBlock;

/// <param name="Depth">Nesting level, from zero.</param>
/// <param name="Marker">"1." for a numbered item, "•" for a bullet.</param>
/// <param name="Tone">A status tone for the bullet (Core's [[bullet:…]] or the alert's severity), or null.</param>
public sealed record MarkdownListItem(int Depth, string Marker, string? Tone, IReadOnlyList<MarkdownSpan> Spans);

public sealed record MarkdownListBlock(IReadOnlyList<MarkdownListItem> Items) : MarkdownBlock;

/// <summary>Row zero is the header.</summary>
public sealed record MarkdownTableBlock(IReadOnlyList<IReadOnlyList<IReadOnlyList<MarkdownSpan>>> Rows, IReadOnlyList<MarkdownAlignment> Alignments) : MarkdownBlock
{
    public int Columns => Rows.Count == 0 ? 0 : Rows.Max(r => r.Count);
}

public sealed record MarkdownCodeBlock(string Text) : MarkdownBlock;

public sealed record MarkdownRuleBlock : MarkdownBlock;

/// <summary>
/// Parses an Intelligence answer's Markdown into display blocks (#228), following
/// desktop's MarkdownView: paragraphs, headings, lists, tables, fenced code,
/// quotes, rules, bold, italic, inline code, safe links and Core's inline markers.
/// The output is data only: nothing is executed, no HTML is interpreted, no image
/// is fetched, and a malformed marker stays literal text.
/// </summary>
public static partial class IntelligenceMarkdown
{
    /// <summary>Bounds layout work for a pathological or recorded answer.</summary>
    internal const int MaxTableRows = 100;
    internal const int MaxTableColumns = 12;

    /// <summary>
    /// Restores the second pair of brackets on an object marker the model wrote with one,
    /// <c>[alert:12]</c> for <c>[[alert:12]]</c>, which would otherwise stay literal text.
    /// Only a marker the lookups returned is repaired, so a single-bracket id the model made
    /// up gains nothing. Code spans, fenced blocks and Markdown links are left alone.
    /// </summary>
    public static string RepairMarkers(string? text, IReadOnlyList<IntelligenceInlineReference> references)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        if (references.Count == 0 || !text.Contains('[')) return text;
        return SingleBracketMarker().Replace(text, m =>
            m.Groups["marker"].Success && references.Any(r => r.Key == m.Groups["marker"].Value)
                ? "[" + m.Value + "]" : m.Value);
    }

    // Consume literal Markdown contexts first so only prose reaches the marker alternative.
    // An unfinished fence remains literal while the answer streams.
    [GeneratedRegex(@"(?m:^[ \t]*(?<fence>`{3,}|~{3,})[^\r\n]*(?:\r?\n|$)[\s\S]*?(?:^[ \t]*\k<fence>[^\r\n]*(?:\r?\n|$)|\z))|(?<ticks>`+)(?!`)[\s\S]*?(?<!`)\k<ticks>(?!`)|!?\[[^\]\r\n]*\]\([^\r\n)]*\)|(?<!\[)\[(?<marker>(?:device|alert|sensor|group|location):[1-9][0-9]{0,9})\](?![\](])")]
    private static partial Regex SingleBracketMarker();

    public static IReadOnlyList<MarkdownBlock> Parse(string? markdown, IReadOnlyList<IntelligenceInlineReference>? references)
    {
        var evidence = references ?? [];
        var blocks = new List<MarkdownBlock>();
        var lines = (markdown ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (trimmed.Length == 0) { i++; continue; }

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                // An unclosed fence (a streaming answer) runs to the end, as on desktop.
                var code = new StringBuilder();
                i++;
                while (i < lines.Length && !lines[i].Trim().StartsWith("```", StringComparison.Ordinal))
                {
                    if (code.Length > 0) code.Append('\n');
                    code.Append(lines[i]);
                    i++;
                }
                i++;
                blocks.Add(new MarkdownCodeBlock(code.ToString()));
                continue;
            }

            if (Heading().Match(trimmed) is { Success: true } heading)
            {
                blocks.Add(new MarkdownTextBlock(MarkdownTextKind.Heading, Inlines(heading.Groups[2].Value, evidence), heading.Groups[1].Value.Length));
                i++;
                continue;
            }

            if (RuleLine().IsMatch(trimmed))
            {
                blocks.Add(new MarkdownRuleBlock());
                i++;
                continue;
            }

            if (IsTableStart(lines, i))
            {
                var alignments = Cells(lines[i + 1].Trim()).Select(Alignment).Take(MaxTableColumns).ToList();
                var rows = new List<IReadOnlyList<IReadOnlyList<MarkdownSpan>>> { Row(trimmed, evidence) };
                var truncated = Cells(trimmed).Count > MaxTableColumns || Cells(lines[i + 1]).Count > MaxTableColumns;
                i += 2;
                while (i < lines.Length && lines[i].Trim().StartsWith('|'))
                {
                    if (Cells(lines[i]).Count > MaxTableColumns) truncated = true;
                    if (rows.Count <= MaxTableRows) rows.Add(Row(lines[i].Trim(), evidence));
                    else truncated = true;
                    i++;
                }
                blocks.Add(new MarkdownTableBlock(rows, alignments));
                if (truncated)
                    blocks.Add(new MarkdownTextBlock(MarkdownTextKind.Notes,
                        [new MarkdownSpan("Table shortened: additional rows or columns were omitted. Copy the answer to read the complete table.")]));
                continue;
            }

            if (trimmed.StartsWith('>'))
            {
                var quote = new StringBuilder();
                while (i < lines.Length && lines[i].Trim().StartsWith('>'))
                {
                    quote.Append(lines[i].Trim().TrimStart('>').Trim()).Append(' ');
                    i++;
                }
                blocks.Add(new MarkdownTextBlock(MarkdownTextKind.Quote, Inlines(quote.ToString().Trim(), evidence)));
                continue;
            }

            if (ListItem().IsMatch(line))
            {
                blocks.Add(List(lines, ref i, evidence));
                continue;
            }

            var prose = new StringBuilder(trimmed);
            i++;
            while (i < lines.Length && lines[i].Trim().Length > 0 && !StartsBlock(lines, i))
            {
                prose.Append(' ').Append(lines[i].Trim());
                i++;
            }
            blocks.Add(Prose(prose.ToString(), evidence));
        }
        return blocks;
    }

    /// <summary>Every verified, navigable object the answer mentions, once each, in reading order.</summary>
    public static IReadOnlyList<IntelligenceInlineReference> LinkedObjects(IReadOnlyList<MarkdownBlock> blocks) =>
        Spans(blocks).Select(s => s.Reference).OfType<IntelligenceInlineReference>()
            .Where(r => r.Object is { IsValid: true }).DistinctBy(r => r.Key).ToList();

    /// <summary>Every safe link the answer contains, once each, in reading order.</summary>
    public static IReadOnlyList<MarkdownSpan> Links(IReadOnlyList<MarkdownBlock> blocks) =>
        Spans(blocks).Where(s => s.Link is not null).DistinctBy(s => s.Link!.AbsoluteUri).ToList();

    private static IEnumerable<MarkdownSpan> Spans(IReadOnlyList<MarkdownBlock> blocks) => blocks.SelectMany(IEnumerable<MarkdownSpan> (block) => block switch
    {
        MarkdownTextBlock text => text.Spans,
        MarkdownListBlock list => list.Items.SelectMany(item => item.Spans),
        MarkdownTableBlock table => table.Rows.SelectMany(row => row.SelectMany(cell => cell)),
        _ => Enumerable.Empty<MarkdownSpan>(),
    });

    /// <summary>A stable plain description of the parsed blocks, for golden tests and diagnostics-free review.</summary>
    public static string Describe(IReadOnlyList<MarkdownBlock> blocks)
    {
        var text = new StringBuilder();
        foreach (var block in blocks)
        {
            switch (block)
            {
                case MarkdownTextBlock t:
                    text.Append(t.Kind == MarkdownTextKind.Heading ? "heading " + t.Level.ToString(CultureInfo.InvariantCulture) : t.Kind.ToString().ToLowerInvariant())
                        .Append(": ").Append(Describe(t.Spans)).Append('\n');
                    break;
                case MarkdownListBlock l:
                    foreach (var item in l.Items)
                        text.Append(new string(' ', item.Depth * 2)).Append(item.Marker)
                            .Append(item.Tone is null ? "" : "(" + item.Tone + ")").Append(' ').Append(Describe(item.Spans)).Append('\n');
                    break;
                case MarkdownTableBlock tb:
                    text.Append("table ").Append(string.Join(",", tb.Alignments.Select(a => a.ToString().ToLowerInvariant()))).Append('\n');
                    foreach (var row in tb.Rows)
                        text.Append("| ").Append(string.Join(" | ", row.Select(Describe))).Append(" |\n");
                    break;
                case MarkdownCodeBlock c:
                    text.Append("code:\n").Append(c.Text).Append("\n/code\n");
                    break;
                case MarkdownRuleBlock:
                    text.Append("rule\n");
                    break;
            }
        }
        return text.ToString();
    }

    private static string Describe(IReadOnlyList<MarkdownSpan> spans) => string.Concat(spans.Select(span =>
        span.Reference is { } r ? "{" + r.Key + (r.Object is { IsValid: true } o ? "→" + o.Kind + ":" + o.Id.ToString(CultureInfo.InvariantCulture) : "") + " " + span.Display + "}"
        : span.Link is { } link ? "<" + span.Text + "|" + link.AbsoluteUri + ">"
        : span.Style == MarkdownSpanStyle.None ? span.Text
        : "[" + StyleName(span.Style) + ":" + span.Text + "]"));

    private static string StyleName(MarkdownSpanStyle style) => string.Join("+",
        new[] { (MarkdownSpanStyle.Bold, "b"), (MarkdownSpanStyle.Italic, "i"), (MarkdownSpanStyle.Code, "code") }
            .Where(s => style.HasFlag(s.Item1)).Select(s => s.Item2));

    private static MarkdownTextBlock Prose(string prose, IReadOnlyList<IntelligenceInlineReference> evidence)
    {
        foreach (var prefix in NotePrefixes)
            if (prose.StartsWith(prefix, StringComparison.Ordinal))
                return new(MarkdownTextKind.Notes, Inlines(prose[prefix.Length..].TrimStart(), evidence));
        if (prose.StartsWith("[[recommendation]]", StringComparison.Ordinal))
            return new(MarkdownTextKind.Recommendation, Inlines(prose["[[recommendation]]".Length..].TrimStart(), evidence));
        return new(MarkdownTextKind.Paragraph, Inlines(prose, evidence));
    }

    private static readonly string[] NotePrefixes =
        ["[[update]]", "**Notes:**", "**Notes**:", "Notes:", "**Update:**", "**Update**:", "Update:"];

    private static bool IsTableStart(string[] lines, int i) =>
        lines[i].Trim().StartsWith('|') && i + 1 < lines.Length && TableSeparator().IsMatch(lines[i + 1].Trim());

    private static bool StartsBlock(string[] lines, int i)
    {
        var t = lines[i].Trim();
        return t.StartsWith("```", StringComparison.Ordinal) || Heading().IsMatch(t) || RuleLine().IsMatch(t) || t.StartsWith('>')
            || t.StartsWith("[[recommendation]]", StringComparison.Ordinal) || NotePrefixes.Any(p => t.StartsWith(p, StringComparison.Ordinal))
            || ListItem().IsMatch(lines[i]) || IsTableStart(lines, i);
    }

    private static MarkdownListBlock List(string[] lines, ref int i, IReadOnlyList<IntelligenceInlineReference> evidence)
    {
        var items = new List<MarkdownListItem>();
        var indents = new List<int>();
        var numbers = new List<int>();
        while (i < lines.Length && ListItem().Match(lines[i]) is { Success: true } item)
        {
            // Depth follows indentation, so a nested list sits under its parent item.
            var indent = item.Groups[1].Value.Replace("\t", "    ").Length;
            while (indents.Count > 0 && indent < indents[^1] - 1) { indents.RemoveAt(indents.Count - 1); numbers.RemoveAt(numbers.Count - 1); }
            if (indents.Count == 0 || indent > indents[^1] + 1) { indents.Add(indent); numbers.Add(0); }
            var depth = Math.Min(indents.Count - 1, 4);

            var text = new StringBuilder(item.Groups[3].Value.Trim());
            i++;
            while (i < lines.Length && lines[i].Trim().Length > 0 && !ListItem().IsMatch(lines[i]) && !StartsBlock(lines, i) && lines[i].StartsWith(' '))
            {
                text.Append(' ').Append(lines[i].Trim());
                i++;
            }

            var content = text.ToString();
            var prefix = IntelligenceInlineMarkup.BulletPrefix().Match(content);
            var tone = prefix.Success ? prefix.Groups[1].Value == "ok" ? "up" : prefix.Groups[1].Value : null;
            if (prefix.Success) content = content[prefix.Length..];

            // Prefer the verified alert's severity if a model-supplied bullet colour disagrees.
            var alert = IntelligenceInlineMarkup.Token().Matches(content).FirstOrDefault(m => m.Value.StartsWith("[[alert:", StringComparison.Ordinal));
            if (alert is not null && IntelligenceInlineMarkup.Resolve(alert.Value, evidence) is { } verified) tone = verified.Tone;

            var numbered = char.IsDigit(item.Groups[2].Value[0]);
            numbers[^1]++;
            items.Add(new(depth, numbered ? numbers[^1].ToString(CultureInfo.InvariantCulture) + "." : "•", tone, Inlines(content, evidence)));
        }
        return new MarkdownListBlock(items);
    }

    private static IReadOnlyList<IReadOnlyList<MarkdownSpan>> Row(string row, IReadOnlyList<IntelligenceInlineReference> evidence) =>
        Cells(row).Take(MaxTableColumns).Select(cell => Inlines(cell, evidence)).ToList();

    private static List<string> Cells(string row)
    {
        var inner = row.Trim();
        if (inner.StartsWith('|')) inner = inner[1..];
        if (inner.EndsWith('|')) inner = inner[..^1];
        return inner.Split('|').Select(c => c.Trim()).ToList();
    }

    private static MarkdownAlignment Alignment(string separator)
    {
        var s = separator.Trim();
        return (s.StartsWith(':'), s.EndsWith(':')) switch
        {
            (true, true) => MarkdownAlignment.Centre,
            (false, true) => MarkdownAlignment.End,
            _ => MarkdownAlignment.Start,
        };
    }

    /// <summary>Bold, italic, code, links, image alt text and markers within a line.</summary>
    internal static IReadOnlyList<MarkdownSpan> Inlines(string text, IReadOnlyList<IntelligenceInlineReference> evidence)
    {
        var spans = new List<MarkdownSpan>();
        AddInlines(spans, text, MarkdownSpanStyle.None, evidence);
        return Merge(spans);
    }

    private static void AddInlines(List<MarkdownSpan> spans, string text, MarkdownSpanStyle style, IReadOnlyList<IntelligenceInlineReference> evidence)
    {
        var position = 0;
        foreach (Match m in InlineToken().Matches(text))
        {
            if (m.Index > position) spans.Add(new(text[position..m.Index], style));
            var token = m.Value;
            if (token.StartsWith("[[", StringComparison.Ordinal))
                spans.Add(Reference(token, style, evidence));
            else if (token.StartsWith("**", StringComparison.Ordinal) || token.StartsWith("__", StringComparison.Ordinal))
                AddInlines(spans, token[2..^2], style | MarkdownSpanStyle.Bold, evidence);
            else if (token.StartsWith('`'))
                spans.Add(new(token[1..^1], style | MarkdownSpanStyle.Code));
            else if (token.StartsWith("![", StringComparison.Ordinal))
            {
                // Remote images are never fetched; the description stays readable.
                var alt = ImageToken().Match(token).Groups[1].Value.Trim();
                spans.Add(new(alt.Length > 0 ? "Image: " + alt : "Image", style | MarkdownSpanStyle.Italic));
            }
            else if (token.StartsWith('['))
            {
                var link = LinkToken().Match(token);
                var title = link.Groups[1].Value;
                spans.Add(WebSearchCitations.TryGetLink(link.Groups[2].Value, out var uri) ? new(title, style, uri) : new(title, style));
            }
            else
                AddInlines(spans, token[1..^1], style | MarkdownSpanStyle.Italic, evidence);
            position = m.Index + m.Length;
        }
        if (position < text.Length) spans.Add(new(text[position..], style));
    }

    /// <summary>
    /// A verified marker becomes a reference. A well-formed object marker whose id
    /// the lookups never returned reads as Core's fallback ("Device 12") and links
    /// nowhere. Anything else, such as an unknown status, stays literal.
    /// </summary>
    private static MarkdownSpan Reference(string marker, MarkdownSpanStyle style, IReadOnlyList<IntelligenceInlineReference> evidence)
    {
        if (IntelligenceInlineMarkup.Resolve(marker, evidence) is { } reference)
            return new(reference.Name, style, Reference: reference);
        var value = IntelligenceInlineMarkup.Token().Match(marker).Groups[2].Value;
        var unverifiedObject = ObjectMarker().IsMatch(marker) && value.Length > 0 && char.IsDigit(value[0]);
        return new(unverifiedObject ? IntelligenceInlineMarkup.Fallback(marker) : marker, style);
    }

    private static IReadOnlyList<MarkdownSpan> Merge(List<MarkdownSpan> spans)
    {
        var merged = new List<MarkdownSpan>();
        foreach (var span in spans)
        {
            if (span.Text.Length == 0 && span.Reference is null) continue;
            if (merged.Count > 0 && merged[^1] is { Link: null, Reference: null } last && span is { Link: null, Reference: null } && last.Style == span.Style)
                merged[^1] = last with { Text = last.Text + span.Text };
            else
                merged.Add(span);
        }
        return merged;
    }

    [GeneratedRegex(@"^(#{1,6})\s+(.*)$")]
    private static partial Regex Heading();

    [GeneratedRegex(@"^(-{3,}|\*{3,}|_{3,})$")]
    private static partial Regex RuleLine();

    [GeneratedRegex(@"^\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?$")]
    private static partial Regex TableSeparator();

    // Group 1 indent, 2 marker, 3 text.
    [GeneratedRegex(@"^(\s*)([-*+]|\d{1,3}[.)])\s+(.*)$")]
    private static partial Regex ListItem();

    [GeneratedRegex(@"^\[\[(?:device|alert|sensor|group|location):")]
    private static partial Regex ObjectMarker();

    // Desktop's tokens, plus image syntax so "![alt](url)" never becomes a link.
    // Bold before italic; single underscores only at word edges, so hostnames
    // like core_sw_01 stay as they are.
    [GeneratedRegex(@"\[\[(?:device|alert|sensor|group|location|status|severity|bullet):(?:[a-z]+|[1-9][0-9]{0,9})\]\]|\[\[(?:recommendation|update)\]\]|\*\*(?=\S).+?(?<=\S)\*\*|__(?=\S).+?(?<=\S)__|`[^`]+`|!\[[^\]]*\]\([^)\s]*\)|\[[^\]]+\]\([^)\s]+\)|(?<![\w*])\*(?=\S)[^*]+?(?<=\S)\*(?![\w*])|(?<!\w)_(?=\S)[^_]+?(?<=\S)_(?!\w)")]
    private static partial Regex InlineToken();

    [GeneratedRegex(@"^\[([^\]]+)\]\(([^)\s]+)\)$")]
    private static partial Regex LinkToken();

    [GeneratedRegex(@"^!\[([^\]]*)\]\(([^)\s]*)\)$")]
    private static partial Regex ImageToken();
}
