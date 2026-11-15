using System.Net;
using System.Text;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>Native PDF print input: escaped visible content only, no scripts, external images or styles.</summary>
public static class IntelligenceChatPdfHtml
{
    public static string Write(ExportedChat chat)
    {
        var html = new StringBuilder("<!doctype html><html><head><meta charset=\"utf-8\"><style>body{font:11pt sans-serif;color:#1b2430}h1{font-size:22pt}h2{font-size:16pt}h3{font-size:13pt}pre,code{font-family:monospace}pre{white-space:pre-wrap;overflow-wrap:anywhere;background:#f1f4f8;padding:8pt}table{border-collapse:collapse;width:100%;table-layout:fixed}td,th{border:1px solid #d5dce5;padding:4pt;overflow-wrap:anywhere}blockquote{border-left:3pt solid #e0a02e;padding-left:8pt}img{max-width:100%;max-height:650pt}a{color:#1a5fb4}p,li,td{overflow-wrap:anywhere}</style></head><body>");
        html.Append("<h1>").Append(E(chat.Title)).Append("</h1><p>").Append(E(chat.Summary)).Append("</p>");
        foreach (var message in chat.Messages)
        {
            html.Append("<hr><h2>").Append(E(ExportedChat.Heading(message))).Append("</h2>");
            if (message.Note is { } note) html.Append("<blockquote>Note: ").Append(E(note)).Append("</blockquote>");
            if (message.IsUser) html.Append("<p>").Append(E(message.Markdown).Replace("\n", "<br>")).Append("</p>");
            else foreach (var block in IntelligenceMarkdown.Parse(message.Markdown, [])) Block(html, block);
            for (var i = 0; i < message.Images.Count; i++)
            {
                var image = message.Images[i];
                if (image.MimeType is not ("image/png" or "image/jpeg")) continue;
                html.Append("<p>Graph ").Append(i + 1).Append("</p><img alt=\"Graph ").Append(i + 1)
                    .Append("\" src=\"data:").Append(image.MimeType).Append(";base64,").Append(Convert.ToBase64String(image.Data)).Append("\">");
            }
        }
        return html.Append("</body></html>").ToString();
    }
    private static string E(string value) => WebUtility.HtmlEncode(value);
    private static void Spans(StringBuilder html, IEnumerable<MarkdownSpan> spans)
    {
        foreach (var span in spans)
        {
            var text = E(span.Display);
            if (span.Style.HasFlag(MarkdownSpanStyle.Code)) text = "<code>" + text + "</code>";
            if (span.Style.HasFlag(MarkdownSpanStyle.Bold)) text = "<strong>" + text + "</strong>";
            if (span.Style.HasFlag(MarkdownSpanStyle.Italic)) text = "<em>" + text + "</em>";
            if (span.Link is { } uri) text = "<a href=\"" + E(uri.AbsoluteUri) + "\">" + text + "</a>";
            html.Append(text);
        }
    }
    private static void Block(StringBuilder html, MarkdownBlock block)
    {
        switch (block)
        {
            case MarkdownTextBlock text:
                var tag = text.Kind == MarkdownTextKind.Heading ? "h" + Math.Clamp(text.Level + 2, 3, 6)
                    : text.Kind == MarkdownTextKind.Quote ? "blockquote" : "p";
                html.Append('<').Append(tag).Append('>'); Spans(html, text.Spans); html.Append("</").Append(tag).Append('>');
                break;
            case MarkdownCodeBlock code: html.Append("<pre>").Append(E(code.Text)).Append("</pre>"); break;
            case MarkdownRuleBlock: html.Append("<hr>"); break;
            case MarkdownListBlock list:
                foreach (var item in list.Items)
                {
                    html.Append("<p style=\"margin-left:").Append(item.Depth * 16).Append("pt\">").Append(E(item.Marker)).Append(' ');
                    Spans(html, item.Spans); html.Append("</p>");
                }
                break;
            case MarkdownTableBlock table:
                html.Append("<table>");
                foreach (var row in table.Rows)
                {
                    html.Append("<tr>");
                    foreach (var cell in row) { html.Append("<td>"); Spans(html, cell); html.Append("</td>"); }
                    html.Append("</tr>");
                }
                html.Append("</table>"); break;
        }
    }
}

/// <summary>The platform prints the safe visible-content HTML as selectable PDF pages.</summary>
public interface IIntelligencePdfPrinter
{
    Task<byte[]> PrintAsync(ExportedChat chat);
}
