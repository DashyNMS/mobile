namespace DashyNMS.Mobile.Intelligence;

/// <summary>
/// The file operations saved-chat cleanup depends on (#222): removing the
/// <c>chats</c> folder, and the small note that says a sign-out's cleanup
/// hasn't finished. An interface so tests can make each one fail on cue - a
/// folder the platform refuses to delete can't be arranged reliably on a CI
/// runner - and so the storage's own handling is what's tested, not a copy.
/// </summary>
public interface IChatCleanupFiles
{
    /// <summary>Whether the file at <paramref name="path"/> exists; throws when that can't be told.</summary>
    bool Exists(string path);

    /// <summary>Writes the cleanup note to <paramref name="path"/>, flushed to disk so it survives a crash; throws if it can't.</summary>
    void WriteNote(string path);

    /// <summary>Deletes the file at <paramref name="path"/> if it's there; throws if it can't.</summary>
    void DeleteFile(string path);

    /// <summary>Deletes <paramref name="path"/> and everything in it if it's there; throws if it can't.</summary>
    void DeleteDirectory(string path);
}

/// <summary>The phone's file system.</summary>
public sealed class ChatCleanupFiles : IChatCleanupFiles
{
    public bool Exists(string path)
    {
        // Not File.Exists: that answers false when the file can't be checked,
        // and "can't tell" mustn't read as "no cleanup outstanding".
        try
        {
            File.GetAttributes(path);
            return true;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    public void WriteNote(string path)
    {
        // Fixed content: the note's presence is the whole message, and it
        // carries nothing about the chats, the server or the key.
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.WriteByte(1);
        stream.Flush(flushToDisk: true);
    }

    public void DeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (DirectoryNotFoundException)
        {
            // Its folder is gone, so it is too.
        }
    }

    public void DeleteDirectory(string path)
    {
        try
        {
            // Directory.Exists hides access errors. Only definite absence is
            // success; inaccessible files must retain the cleanup marker.
            Directory.Delete(path, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // Definitely absent, including a missing parent directory.
        }
    }
}
