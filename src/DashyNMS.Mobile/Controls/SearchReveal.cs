namespace DashyNMS.Mobile.Controls;

/// <summary>
/// Keeps a list's search box out of the way until it's wanted: tucked away at
/// first, sliding out when the list is scrolled back up or pulled down, and
/// away again when it's scrolled on down - as Safari's bars do. Never hidden
/// while it's being typed in or has something in it.
/// </summary>
/// <remarks>
/// <para>The box sits above the list rather than in its header (#78). A
/// header is rebuilt whenever the list reloads - which a search does every
/// time typing pauses - and on iOS that took the keyboard away every few
/// letters.</para>
/// <para>It lives in a clipping slot whose height is animated, so it slides
/// rather than pops, and it only moves after a deliberate scroll one way, so
/// a wobble doesn't flicker it (#82). The gaps either side belong to the
/// neighbouring rows, not the box, so they're the same shown or hidden (#83).</para>
/// </remarks>
public static class SearchReveal
{
    /// <summary>How far a scroll has to travel one way, in points, before the box moves.</summary>
    private const double Travel = 40;

    private const uint SlideMilliseconds = 200;

    private const string AnimationName = "SearchReveal";

    /// <param name="slot">
    /// A Grid, clipped: a ContentView didn't clip on iOS, so at height 0 the
    /// box still drew over the chips below it rather than going away.
    /// </param>
    public static void Attach(CollectionView list, Grid slot, SearchBar search)
    {
        slot.IsClippedToBounds = true;
        search.VerticalOptions = LayoutOptions.Start;

        bool IsInUse() => search.IsFocused || !string.IsNullOrEmpty(search.Text);

        // Android lists don't bounce, so a short list could never be pulled
        // down to bring it out: there, it just stays.
        if (DeviceInfo.Platform == DevicePlatform.Android)
        {
            return;
        }

        var shown = IsInUse();
        slot.HeightRequest = shown ? -1 : 0;
        search.Opacity = shown ? 1 : 0;
        search.IsVisible = shown;
        var travelled = 0.0;

        double FullHeight()
        {
            var width = slot.Width > 0 ? slot.Width : list.Width;
            return search.Measure(width > 0 ? width : 400, double.PositiveInfinity).Height;
        }

        void Slide(bool show)
        {
            if (show == shown)
            {
                return;
            }

            shown = show;
            travelled = 0;
            slot.AbortAnimation(AnimationName);

            // Hidden, it measures as nothing: back in the layout first.
            if (show)
            {
                search.IsVisible = true;
            }

            var from = slot.Height >= 0 ? slot.Height : (show ? 0 : FullHeight());
            var to = show ? FullHeight() : 0;
            var full = Math.Max(from, to);

            // Height and a fade together, so it slides and fades rather than being cut off.
            slot.Animate(
                AnimationName,
                value =>
                {
                    slot.HeightRequest = value;
                    search.Opacity = full > 0 ? value / full : (show ? 1 : 0);
                },
                from,
                to,
                length: SlideMilliseconds,
                easing: show ? Easing.CubicOut : Easing.CubicIn,
                finished: (_, cancelled) =>
                {
                    if (cancelled)
                    {
                        return;
                    }

                    // Once out, let it size itself (larger text, rotation); once
                    // away, keep it unreachable for taps and VoiceOver as well.
                    slot.HeightRequest = show ? -1 : 0;
                    search.Opacity = show ? 1 : 0;
                    search.IsVisible = show;
                });
        }

        search.TextChanged += (_, _) =>
        {
            // Set from elsewhere too (the dashboard's Acknowledged count): show what's filtering the list.
            if (!string.IsNullOrEmpty(search.Text))
            {
                Slide(true);
            }
        };

        list.Scrolled += (_, e) =>
        {
            // Back at the top, or pulled down past it: out it comes.
            if (e.VerticalOffset <= 0)
            {
                Slide(true);
                return;
            }

            // At the bottom, iOS overshoots and springs back - which looked
            // like a scroll up, so the box popped out and away again (#94).
            // Nothing there counts; a real scroll up starts once off the end.
            if (list.ItemsSource is System.Collections.ICollection { Count: > 0 } items && e.LastVisibleItemIndex >= items.Count - 1)
            {
                travelled = 0;
                return;
            }

            // Travel one way only; turning round starts the count again.
            travelled = Math.Sign(e.VerticalDelta) == Math.Sign(travelled) ? travelled + e.VerticalDelta : e.VerticalDelta;

            if (travelled <= -Travel)
            {
                Slide(true);
            }
            else if (travelled >= Travel && !IsInUse())
            {
                Slide(false);
            }
        };
    }
}
