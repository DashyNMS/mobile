namespace DashyNMS.Mobile.Services;

/// <summary>
/// Keeps a folder out of the phone's backups (#222). Saved chats are encrypted
/// under a key that never leaves the phone, so a backed-up copy couldn't be
/// read anyway - but it shouldn't reach iCloud or a computer at all.
/// </summary>
/// <remarks>
/// iOS marks the folder with <c>NSURLIsExcludedFromBackupKey</c>, which covers
/// everything inside it. Android needs nothing per folder: the manifest's
/// <c>allowBackup="false"</c> already keeps the whole app out of backups, so
/// it uses <see cref="AppBackupDisabled"/>.
/// </remarks>
public interface IBackupExclusion
{
    /// <summary>Marks <paramref name="directory"/>, which exists; throws if the platform refuses.</summary>
    void Exclude(string directory);
}

/// <summary>For a platform whose app is already out of backups altogether (Android's <c>allowBackup="false"</c>), and for tests.</summary>
public sealed class AppBackupDisabled : IBackupExclusion
{
    public void Exclude(string directory)
    {
    }
}
