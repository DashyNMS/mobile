namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Keeps a list's search box out of the way until it's wanted: hidden at
/// first, shown as soon as the list is scrolled or pulled down, hidden again
/// when it's scrolled on down - as Safari's bars do. Never hidden while it's
/// being typed in or has something in it.
/// </summary>
/// <remarks>
/// The box sits above the list rather than in its header (#78). A header is
/// rebuilt whenever the list reloads - which a search does every time typing
/// pauses - and on iOS that took the keyboard away every few letters.
/// </remarks>
public static class SearchReveal
{
    /// <summary>How far a scroll has to move, in points, before the box shows or hides - so a wobble doesn't flicker it.</summary>
    private const double Threshold = 4;

    public static void Attach(CollectionView list, SearchBar search)
    {
        // Android lists don't bounce, so a short list could never be pulled
        // down to show it: there, it just stays.
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            return;
        }

        bool IsInUse() => search.IsFocused || !string.IsNullOrEmpty(search.Text);

        search.IsVisible = IsInUse();

        search.TextChanged += (_, _) =>
        {
            // Set from elsewhere too (the dashboard's Acknowledged count): show what's filtering the list.
            if (!string.IsNullOrEmpty(search.Text))
            {
                search.IsVisible = true;
            }
        };

        list.Scrolled += (_, e) =>
        {
            if (e.VerticalDelta < -Threshold)
            {
                search.IsVisible = true;
            }
            else if (e.VerticalDelta > Threshold && e.VerticalOffset > 0 && !IsInUse())
            {
                search.IsVisible = false;
            }
        };
    }
}
