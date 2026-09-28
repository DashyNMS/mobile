using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Adapters;

public sealed class ShareService : IShareService
{
    public async Task ShareTextFileAsync(string fileName, string contents, string contentType, string title)
    {
        // The cache folder: the share sheet needs a real file, but nothing
        // here needs keeping once it's been handed over.
        var path = Path.Combine(FileSystem.CacheDirectory, fileName);
        await File.WriteAllTextAsync(path, contents);

        await MainThread.InvokeOnMainThreadAsync(() =>
            Share.Default.RequestAsync(new ShareFileRequest(title, new ShareFile(path, contentType))));
    }
}
