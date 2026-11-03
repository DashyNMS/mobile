namespace DashyNMS.Mobile.Controls;

/// <summary>Button-only list search; dismissal retains the bound query and filters.</summary>
internal sealed class ButtonSearch
{
    private readonly Grid _slot;
    private readonly SearchField _field;
    private readonly Entry _entry;
    private bool _open;

    public ButtonSearch(Grid slot, SearchField field, Entry entry, CollectionView list)
    {
        _slot = slot;
        _field = field;
        _entry = entry;
        slot.HeightRequest = 0;
        field.IsVisible = false;
        entry.Unfocused += (_, _) => Close();
        // #402: replacing filtered rows also raises Scrolled. Query native
        // state at delivery time; do not retain native views across handlers.
        list.Scrolled += (_, _) => { if (IsDragging(list)) Close(); };
    }

    private static bool IsDragging(CollectionView list)
    {
#if IOS
        static UIKit.UIScrollView? Find(UIKit.UIView view) =>
            view as UIKit.UIScrollView ?? view.Subviews.Select(Find).FirstOrDefault(v => v is not null);
        return list.Handler?.PlatformView is UIKit.UIView view
            && Find(view) is { } scroll && (scroll.Dragging || scroll.Tracking);
#elif ANDROID
        return list.Handler?.PlatformView is AndroidX.RecyclerView.Widget.RecyclerView recycler
            && recycler.ScrollState == AndroidX.RecyclerView.Widget.RecyclerView.ScrollStateDragging;
#else
        return false;
#endif
    }

    public void Open()
    {
        if (_open) { _entry.Focus(); return; }
        _open = true;
        Slide();
        _slot.Dispatcher.Dispatch(() => { if (_open) _entry.Focus(); });
    }

    public void Close()
    {
        if (!_open) return;
        _open = false;
        KeyboardDismiss.Close(_entry);
        Slide();
    }

    private void Slide()
    {
        const string name = "ButtonSearch";
        _slot.AbortAnimation(name);
        _field.IsVisible = true;
        var full = _field.HeightRequest + _field.Margin.VerticalThickness;
        var from = Math.Max(0, _slot.HeightRequest);
        var to = _open ? full : 0;
        void Place(double height)
        {
            _slot.HeightRequest = height;
            _field.TranslationY = height - full;
            _field.Opacity = full > 0 ? height / full : 1;
        }
        void Finish()
        {
            _slot.HeightRequest = _open ? full : 0;
            _field.IsVisible = _open;
            _field.TranslationY = 0;
            _field.Opacity = 1;
        }
        if (ReducedMotion.IsOn) { Finish(); return; }
        _slot.Animate(name, v => Place(from + (to - from) * v), length: 220,
            easing: _open ? Easing.CubicOut : Easing.CubicIn,
            finished: (_, cancelled) => { if (!cancelled) Finish(); });
    }
}
