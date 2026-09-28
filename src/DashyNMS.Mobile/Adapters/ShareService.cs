using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Adapters;

public sealed class ShareService : IShareService
{
    /// <summary>A folder of its own, so clearing old exports can't touch anything else in the cache.</summary>
    private static string ExportsFolder => Path.Combine(FileSystem.CacheDirectory, "exports");

    public async Task ShareTextFileAsync(string fileName, string contents, string contentType, string title)
    {
        // The share sheet needs a real file, but nothing here needs keeping
        // once it's been handed over - the last export goes when the next is
        // written, or at the next start-up or sign-out.
        ClearExports();
        Directory.CreateDirectory(ExportsFolder);

        var path = Path.Combine(ExportsFolder, Path.GetFileName(fileName));
        await File.WriteAllTextAsync(path, contents);

        await MainThread.InvokeOnMainThreadAsync(() =>
            Share.Default.RequestAsync(new ShareFileRequest(title, new ShareFile(path, contentType))));
    }

    public void ClearExports()
    {
        try
        {
            if (Directory.Exists(ExportsFolder))
            {
                Directory.Delete(ExportsFolder, recursive: true);
            }
        }
        catch (IOException)
        {
            // Still open in another app's share extension; the next clear gets it.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
