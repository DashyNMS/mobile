using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Help;

namespace DashyNMS.Mobile.Views;

/// <summary>
/// One help page's scrolling body (#668), in its own page or beside the
/// contents list. The host supplies only the clearance it doesn't own.
/// </summary>
public sealed class HelpArticleView : ContentView
{
    private readonly HelpBlocksView _blocks = new();
    private readonly ScrollView _scroller;
    private readonly VerticalStackLayout _column;
    private readonly Label _title;
    private readonly Label _missing;
    private readonly BoxView _fade;

    private double _readTo;
    private bool _restoring;

    /// <param name="showsTitle">Beside the list, where no page header names it.</param>
    /// <param name="header">A pushed page's header, which scrolls away with the text so a long title at a large text size doesn't hold half the screen.</param>
    public HelpArticleView(HelpArticleViewModel viewModel, bool showsTitle, View? header = null)
    {
        BindingContext = ViewModel = viewModel;
        _title = new Label { FontFamily = "BodySemibold", FontSize = 22, LineBreakMode = LineBreakMode.WordWrap, IsVisible = showsTitle, Margin = new Thickness(0, 0, 0, 12) };
        SemanticProperties.SetHeadingLevel(_title, SemanticHeadingLevel.Level1);
        _missing = new Label { Text = "This page isn't in the guide. Go back and choose another.", IsVisible = false };
        _blocks.ResolveLink = viewModel.Resolve;
        _blocks.ResolveImage = viewModel.Image;
        _blocks.TitleOf = viewModel.TitleOf;
        _blocks.LinkActivated += (_, target) => viewModel.ActivateCommand.Execute(target);

        _column = new VerticalStackLayout { Spacing = 0, Children = { _title, _missing, _blocks } };
        _header = header;
        if (header is not null)
        {
            _column.Children.Insert(0, header);
        }

        HasHeader = header is not null;
        _scroller = new ScrollView { Content = new ReadableColumn { Children = { _column } } };
        _fade = new BoxView
        {
            HeightRequest = 20,
            VerticalOptions = LayoutOptions.Start,
            InputTransparent = true,
            Background = TopFade(),
        };
        AutomationProperties.SetIsInAccessibleTree(_fade, false);
        _fade.IsVisible = !HasHeader;
        BottomFade = new FootFade();
        Content = new Grid { SafeAreaEdges = SafeAreaEdges.None, Children = { _scroller, _fade, BottomFade } };
#if IOS
        SafeAreaEdges = SafeAreaEdges.None;
        _scroller.SafeAreaEdges = SafeAreaEdges.None;
        _scroller.HandlerChanged += (_, _) => StopInsets();
        _scroller.Loaded += (_, _) => StopInsets();
#endif
        viewModel.Shown += OnShown;

        // Hidden beside a narrowed list and shown again, a scroll view starts at the top: put it back where it was read to.
        _scroller.Scrolled += (_, e) =>
        {
            if (!_restoring && _scroller.Height > 0)
            {
                // The reader scrolled: a restore still waiting would take them back.
                if (Math.Abs(e.ScrollY - _readTo) > 1) _restoreGeneration++;
                _readTo = e.ScrollY;
            }

            // With a header in the column, the fade only shows once the header has moved under it.
            if (HasHeader) _fade.IsVisible = e.ScrollY > 0;
        };
        _scroller.SizeChanged += async (_, _) =>
        {
            if (_restoring || _scroller.Height <= 0 || _readTo <= 0 || Math.Abs(_scroller.ScrollY - _readTo) < 1) return;
            _restoring = true;
            try { await _scroller.ScrollToAsync(0, _readTo, false); }
            catch (Exception) { }
            finally { _restoring = false; }
        };
    }

    private bool HasHeader { get; set; }

    private View? _header;

    /// <summary>Puts the page's header in the scrolling column, or takes it out (null) to be fixed by the page.</summary>
    public void SetScrollingHeader(View? header)
    {
        if (ReferenceEquals(header, _header)) return;
        if (_header is not null) _column.Children.Remove(_header);
        _header = header;
        if (header is not null) _column.Children.Insert(0, header);
        HasHeader = header is not null;
        _fade.IsVisible = !HasHeader || _scroller.ScrollY > 0;
    }

    private double _saved;
    private int _restoreGeneration;

    /// <summary>The pane is about to be hidden: remember how far it was read, and drop any restore still waiting.</summary>
    public void SaveReading()
    {
        // A restore still waiting hasn't moved the page yet: keep the place it was going to.
        if (!_restorePending) _saved = Math.Max(_readTo, _scroller.ScrollY);
        _restoreGeneration++;
        _restorePending = false;
    }

    private bool _restorePending;

    /// <summary>
    /// Shown again: back to where it was read to, once the pane has a size.
    /// A newer hide, page or scroll by the reader wins over a restore still waiting.
    /// </summary>
    public void RestoreReading()
    {
        var target = _saved;
        if (target <= 0) return;
        var generation = ++_restoreGeneration;
        _restorePending = true;
        void Apply()
        {
            if (generation != _restoreGeneration || _scroller.Height <= 0) return;
            _restoring = true;
            if (!Dispatcher.Dispatch(async () =>
            {
                try { if (generation == _restoreGeneration) { await _scroller.ScrollToAsync(0, target, false); _readTo = target; } }
                catch (Exception) { }
                finally { _restoring = false; if (generation == _restoreGeneration) _restorePending = false; }
            }))
            {
                _restoring = false;
            }
        }

        if (_scroller.Height > 0) Apply();
        else
        {
            void OnSized(object? s, EventArgs e) { _scroller.SizeChanged -= OnSized; Apply(); }
            _scroller.SizeChanged += OnSized;
        }
    }

    public HelpArticleViewModel ViewModel { get; }

    /// <summary>The scrolling body's own distance from the pane's edges, and what its last line clears.</summary>
    /// <param name="keyboard">How much of the pane a docked keyboard covers; the text stops above it.</param>
    public void PlaceContent(Thickness clear, double footer, double footFade, double keyboard = 0)
    {
        _scroller.Margin = new Thickness(0, 0, 0, keyboard);
        // A header in the column sits on the top line, as on Licences; the fade only matters once it's scrolled away.
        _column.Padding = new Thickness(16 + clear.Left, HasHeader ? 0 : 20, 16 + clear.Right, footer);
        _fade.Margin = new Thickness(clear.Left, 0, clear.Right, 0);
        BottomFade.SetCoveredHeight(footFade);
    }

    private FootFade BottomFade { get; }

    private void OnShown(object? sender, HelpArticleShown shown)
    {
        _missing.IsVisible = ViewModel.IsMissing;
        if (!shown.IsNewText) { _readTo = 0; _saved = 0; _restoreGeneration++; }
        if (!shown.IsNewText && _title.Text == shown.Page.Title && _blocks.Children.Count > 0)
        {
            ScrollTo(shown.Anchor, animated: !ReducedMotion.IsOn);
            return;
        }

        _title.Text = shown.Page.Title;
        _blocks.Render(shown.Page);
        if (shown.IsNewText)
        {
            return;
        }

        // Once it has a size to scroll within.
        if (shown.Anchor is not null)
        {
            void OnSized(object? s, EventArgs e)
            {
                if (_blocks.Height <= 0) return;
                _blocks.SizeChanged -= OnSized;
                Dispatcher.Dispatch(() => ScrollTo(shown.Anchor, animated: false));
            }

            _blocks.SizeChanged += OnSized;
        }
    }

    private async void ScrollTo(string? anchor, bool animated)
    {
        try
        {
            if (_blocks.FindHeading(anchor) is not { } heading)
            {
                await _scroller.ScrollToAsync(0, 0, animated);
                return;
            }

            // Under the top fade rather than beneath it.
            var y = _blocks.Y + _column.Y + heading.Y;
            await _scroller.ScrollToAsync(0, Math.Max(0, y - 20), animated);
            heading.SetSemanticFocus();
        }
        catch (Exception)
        {
            // Closed while scrolling; nothing is lost.
        }
    }

    private static LinearGradientBrush TopFade()
    {
        static GradientStop Stop(string light, string dark, float offset)
        {
            var stop = new GradientStop { Offset = offset };
            stop.SetAppThemeColor(GradientStop.ColorProperty, (Color)Application.Current!.Resources[light], (Color)Application.Current!.Resources[dark]);
            return stop;
        }

        return new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops = [Stop("BackgroundLight", "BackgroundDark", 0), Stop("BackgroundLightClear", "BackgroundDarkClear", 1)],
        };
    }

#if IOS
    private void StopInsets()
    {
        if (_scroller.Handler?.PlatformView is UIKit.UIScrollView scroll)
            scroll.ContentInsetAdjustmentBehavior = UIKit.UIScrollViewContentInsetAdjustmentBehavior.Never;
    }
#endif
}
