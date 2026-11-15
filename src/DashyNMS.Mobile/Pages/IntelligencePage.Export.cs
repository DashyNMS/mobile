namespace DashyNMS.Mobile.Pages;

public sealed partial class IntelligencePage
{
    private async Task ExportAsync()
    {
        if (_exporting || !_conversation.IsTranscriptCurrent || _conversation.Turns.Count == 0) return;
        _exporting = true;
        UpdateControls(); ShowActions();
        var revision = _conversation.ChatRevision;
        try
        {
            var format = await DisplayActionSheetAsync("Export conversation", "Cancel", null, "Markdown", "PDF");
            if (format is not ("Markdown" or "PDF") || revision != _conversation.ChatRevision || !_conversation.IsTranscriptCurrent) return;
            var title = _chats.Active?.Title ?? Intelligence.IntelligenceSavedChats.Title(_conversation.Turns[0].Question);
            if (!await _exporter.ShareAsync(_conversation, title, format == "PDF", revision))
                await _dialogs.AlertAsync("Nothing exported", "The chat or connection changed. Try again.");
        }
        catch { await _dialogs.AlertAsync("Couldn't export the conversation", "Try again."); }
        finally { _exporting = false; UpdateControls(); ShowActions(); }
    }
}
