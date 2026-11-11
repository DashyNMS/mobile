using DashyNMS.Mobile.Services;
using Foundation;

namespace DashyNMS.Mobile;

/// <summary>
/// Keeps saved chats out of iCloud and computer backups (#222):
/// <c>NSURLIsExcludedFromBackupKey</c> on the <c>chats</c> folder, which covers
/// everything saved inside it. The key is already "this device only" in the
/// keychain (see <see cref="Adapters.MauiSecureStorage"/>), so a backed-up file
/// couldn't be read, but it shouldn't leave the phone at all.
/// </summary>
public sealed class IosBackupExclusion : IBackupExclusion
{
    public void Exclude(string directory)
    {
        using var url = NSUrl.FromFilename(directory);
        if (!url.SetResource(NSUrl.IsExcludedFromBackupKey, NSNumber.FromBoolean(true), out var error))
        {
            // The caller treats this as "can't keep chats", never logs the text.
            throw new IOException(error?.LocalizedDescription ?? "The folder couldn't be excluded from backups.");
        }
    }
}
