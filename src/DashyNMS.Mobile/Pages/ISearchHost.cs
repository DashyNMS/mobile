namespace DashyNMS.Mobile.Pages;

/// <summary>
/// A page whose search is a field it slides in itself, opened from a button
/// (Devices), rather than a search bar waiting above its list - so ⌘F on an
/// iPad or a Mac keyboard asks the page to open it.
/// </summary>
public interface ISearchHost
{
    /// <summary>Opens search with the cursor in it, or puts the cursor back if it's open.</summary>
    void OpenSearch();
}
