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

    /// <param name="showsTitle">Beside the list, where no page header names it.</param>
    public HelpArticleView(HelpArticleViewModel viewModel, bool showsTitle)
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
        _scroller = new ScrollView { Content = new ReadableColumn { Children = { _column } } };
        _fade = new BoxView
        {
            HeightRequest = 20,
            VerticalOptions = LayoutOptions.Start,
            InputTransparent = true,
            Background = TopFade(),
        };
        AutomationProperties.SetIsInAccessibleTree(_fade, false);
        BottomFade = new FootFade();
        Content = new Grid { SafeAreaEdges = SafeAreaEdges.None, Children = { _scroller, _fade, BottomFade } };
#if IOS
        SafeAreaEdges = SafeAreaEdges.None;
        _scroller.SafeAreaEdges = SafeAreaEdges.None;
        _scroller.HandlerChanged += (_, _) => StopInsets();
        _scroller.Loaded += (_, _) => StopInsets();
#endif
        viewModel.Shown += OnShown;
    }

    public HelpArticleViewModel ViewModel { get; }

    /// <summary>The scrolling body's own distance from the pane's edges, and what its last line clears.</summary>
    public void PlaceContent(Thickness clear, double footer, double footFade)
    {
        _column.Padding = new Thickness(16 + clear.Left, 20, 16 + clear.Right, footer);
        _fade.Margin = new Thickness(clear.Left, 0, clear.Right, 0);
        BottomFade.SetCoveredHeight(footFade);
    }

    private FootFade BottomFade { get; }

    private void OnShown(object? sender, HelpArticleShown shown)
    {
        _missing.IsVisible = ViewModel.IsMissing;
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
