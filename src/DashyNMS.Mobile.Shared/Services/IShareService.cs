namespace DashyNMS.Mobile.Services;

/// <summary>
/// Hands a file to the platform share sheet (Files, Mail, AirDrop, Drive...) -
/// a phone's stand-in for desktop's Save dialog.
/// </summary>
public interface IShareService
{
    /// <param name="fileName">What the file is called wherever it's shared to.</param>
    /// <param name="contents">The file's text.</param>
    /// <param name="contentType">MIME type, e.g. "text/csv".</param>
    /// <param name="title">The share sheet's title, where the platform shows one.</param>
    Task ShareTextFileAsync(string fileName, string contents, string contentType, string title);

    /// <summary>
    /// Deletes files written for earlier shares - they hold device names,
    /// rules and notes, and nothing needs them once the share sheet has taken
    /// them (#9). Done before each export, at start-up and on sign-out.
    /// </summary>
    void ClearExports();
}
