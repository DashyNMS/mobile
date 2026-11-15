using System.IO.Compression;
using DashyNMS.Mobile.Intelligence;
using DesktopNMS.Core.Intelligence;

namespace DashyNMS.Mobile.Tests;

public sealed class IntelligenceChatExportTests
{
    [Fact]
    public void Recorded_chat_matches_desktop_markdown_and_pdf_print_layout_goldens()
    {
        var chat = new ExportedChat("Example", DateTimeOffset.UnixEpoch,
            [new(true, null, "Question", [], null), new(false, null, "# Heading\n\nAnswer", [], null)]);
        var folder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Intelligence", "Export");
        var markdown = IntelligenceChatMarkdown.Write(chat, (_, _, _) => "").Replace(chat.Summary, "<summary>");
        Assert.Equal(File.ReadAllText(Path.Combine(folder, "conversation.md.golden")).Replace("\r\n", "\n"), markdown);
        var html = IntelligenceChatPdfHtml.Write(chat);
        var body = html[(html.IndexOf("<body>", StringComparison.Ordinal) + 6)..html.IndexOf("</body>", StringComparison.Ordinal)]
            .Replace(System.Net.WebUtility.HtmlEncode(chat.Summary), "<summary>");
        Assert.Equal(File.ReadAllText(Path.Combine(folder, "conversation.pdf-layout.golden")).TrimEnd(), body);
    }

    [Fact]
    public void A_stopped_code_fence_cannot_capture_the_next_question()
    {
        var chat = new ExportedChat("Chat", DateTimeOffset.UnixEpoch,
            [new(false, null, "```text\nPartial code", [], "Stopped"),
             new(true, null, "Next question", [], null), new(false, null, "Next answer", [], null)]);
        var markdown = IntelligenceChatMarkdown.Write(chat, (_, _, _) => "");
        Assert.Contains("Partial code\n```\n\n---\n\n## You\n\nNext question", markdown);
        Assert.Contains("## Dashy\n\nNext answer", markdown);
    }

    [Fact]
    public void Snapshot_excludes_raw_lookups_and_names_verified_markers()
    {
        var turn = new IntelligenceTurn("# plain question", "brief") { Answer = "## Answer\n\nExample [[device:12]]", State = IntelligenceTurnState.Stopped };
        turn.Lookups.Add(new("find_device") { Result = "hidden synthetic tool history" });
        turn.AddEvidence([new(new("device:12", "Example switch", "Up", "up", DateTimeOffset.UnixEpoch, "Device") { Object = new("device", 12) })]);
        var chat = ExportedChat.From("Chat", [turn], DateTimeOffset.UnixEpoch);
        var markdown = IntelligenceChatMarkdown.Write(chat, (_, _, _) => "");
        Assert.Contains("Example switch", markdown);
        Assert.Contains("This answer was stopped before it finished.", markdown);
        Assert.Contains("\\# plain question", markdown);
        Assert.DoesNotContain("[[device:", markdown);
        Assert.DoesNotContain("hidden synthetic", markdown);
    }
    [Fact]
    public void Graphs_travel_with_the_markdown_in_a_relative_folder()
    {
        var chat = new ExportedChat("Example chat", DateTimeOffset.UnixEpoch,
            [new(false, null, "Answer", [new([1, 2, 3], "image/png")], null)]);
        var file = IntelligenceExportFiles.Markdown(chat);
        Assert.Equal("application/zip", file.MimeType);
        using var zip = new ZipArchive(new MemoryStream(file.Data));
        using var text = new StreamReader(zip.GetEntry("Example chat.md")!.Open());
        Assert.Contains("Example%20chat-images/answer-1-graph-1.png", text.ReadToEnd());
        using var image = zip.GetEntry("Example chat-images/answer-1-graph-1.png")!.Open();
        using var bytes = new MemoryStream(); image.CopyTo(bytes);
        Assert.Equal(new byte[] { 1, 2, 3 }, bytes.ToArray());
    }
    [Fact]
    public void Pdf_input_preserves_structure_links_and_escapes_untrusted_html()
    {
        var chat = new ExportedChat("<script>Example</script>", DateTimeOffset.UnixEpoch,
            [new(false, null, "## Heading\n\n| Port | State |\n| --- | --- |\n| A | Up |\n\n```\n<script>bad()</script>\n```\n\n[Source](https://example.net)", [], "Interrupted")]);
        var html = IntelligenceChatPdfHtml.Write(chat);
        Assert.Contains("<table>", html);
        Assert.Contains("<pre>&lt;script&gt;bad()&lt;/script&gt;", html);
        Assert.Contains("href=\"https://example.net/\"", html);
        Assert.Contains("Note: Interrupted", html);
        Assert.DoesNotContain("<script>", html);
    }
}
