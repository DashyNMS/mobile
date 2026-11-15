using System.IO.Compression;
using System.Text;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Intelligence;

public sealed record ChatExportFile(string Name, string MimeType, byte[] Data);

/// <summary>Packages desktop-compatible Markdown and its relative graph files into one portable share.</summary>
public static class IntelligenceExportFiles
{
    public static ChatExportFile Markdown(ExportedChat chat)
    {
        var stem = IntelligenceChatExporter.FileStem(chat.Title);
        using var archiveBytes = new MemoryStream();
        var hasImages = chat.Messages.Any(m => m.Images.Count > 0);
        if (!hasImages) return new(stem + ".md", "text/markdown", Encoding.UTF8.GetBytes(IntelligenceChatMarkdown.Write(chat, (_, _, _) => "")));
        using (var archive = new ZipArchive(archiveBytes, ZipArchiveMode.Create, true))
        {
            var markdown = IntelligenceChatMarkdown.Write(chat, (answer, index, image) =>
            {
                var name = $"{stem}-images/answer-{answer}-graph-{index}.{image.Extension}";
                using var file = archive.CreateEntry(name).Open();
                file.Write(image.Data);
                return Uri.EscapeDataString(stem + "-images") + $"/answer-{answer}-graph-{index}.{image.Extension}";
            });
            using var writer = new StreamWriter(archive.CreateEntry(stem + ".md").Open(), new UTF8Encoding(false));
            writer.Write(markdown);
        }
        return new(stem + ".zip", "application/zip", archiveBytes.ToArray());
    }
}

public sealed class IntelligenceExportService(IShareService share, IIntelligencePdfPrinter pdf)
{
    public async Task<bool> ShareAsync(IntelligenceConversationViewModel conversation, string title, bool asPdf, long revision)
    {
        if (!conversation.IsTranscriptCurrent || revision != conversation.ChatRevision || conversation.Turns.Count == 0) return false;
        // Snapshot on the UI thread. No raw tool records, draft, credentials or diagnostic data enter it.
        var chat = ExportedChat.From(title, conversation.Turns, DateTimeOffset.UtcNow);
        var file = asPdf ? new ChatExportFile(IntelligenceChatExporter.FileStem(chat.Title) + ".pdf", "application/pdf", await pdf.PrintAsync(chat))
            : await Task.Run(() => IntelligenceExportFiles.Markdown(chat));
        if (!conversation.IsTranscriptCurrent || revision != conversation.ChatRevision) return false;
        try { await share.ShareBytesFileAsync(file.Name, file.Data, file.MimeType, "Export conversation"); return true; }
        finally { share.ClearExports(); }
    }
}
