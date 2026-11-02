using DashyNMS.Mobile.Controls;
using DashyNMS.Mobile.Dashboard;
using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Pages;

/// <summary>
/// Shown via <see cref="Routes.AddCard"/>, from Edit dashboard or the welcome
/// card (#140) - with <see cref="Routes.PositionParameter"/> from a card's
/// "Add a card after" (#145).
/// </summary>
public partial class AddCardPage : ContentPage, IQueryAttributable, ISearchHost
{
    private readonly AddCardViewModel _viewModel;
    private bool _isVisible;
    private Window? _subscribedWindow;

    public AddCardPage(AddCardViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _ = new SetupPageLayout(this, Setup, Header, List, CategoryScroll);
    }

#if IOS
    private Foundation.NSObject? _textSizeObserver;
#endif

    protected override void OnDisappearing()
    {
        _isVisible = false;
        if (_subscribedWindow is { } window) window.Resumed -= OnResumed;
        _subscribedWindow = null;
#if IOS
        _textSizeObserver?.Dispose();
        _textSizeObserver = null;
#endif
        base.OnDisappearing();
    }

    public void OpenSearch() => Search.Focus();

    private void OnSearchCompleted(object? sender, EventArgs e) => KeyboardDismiss.Close(Search);

    private void OnCatalogueSizeChanged(object? sender, EventArgs e) => UpdateColumns();

    private void OnResumed(object? sender, EventArgs e) => UpdateColumns();

    private void UpdateColumns()
    {
        double scale = 1;
#if IOS
        using var font = UIKit.UIFont.PreferredBody;
        scale = font.PointSize / 17d;
#elif ANDROID
        scale = Android.App.Application.Context.Resources?.Configuration?.FontScale ?? 1;
#endif
        var columns = CatalogueColumns.Count(List.Width, scale);
        if (CatalogueLayout.Span != columns) CatalogueLayout.Span = columns;
    }

    private void OnChoiceCheckedChanged(object? sender, CheckedChangedEventArgs e)
    {
        if (sender is CheckBox { BindingContext: CardChoice choice } && choice.IsSelected != e.Value)
            _viewModel.PickCommand.Execute(choice);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query) =>
        _viewModel.Position = query.TryGetValue(Routes.PositionParameter, out var value) && value is int position ? position : null;

    /// <summary>What's on the dashboard may have changed since it was made.</summary>
    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_isVisible) return;
        _isVisible = true;
#if IOS
        _textSizeObserver = Foundation.NSNotificationCenter.DefaultCenter.AddObserver(
            new Foundation.NSString("UIContentSizeCategoryDidChangeNotification"), _ => Dispatcher.Dispatch(UpdateColumns));
#endif
        _subscribedWindow = Window;
        if (_subscribedWindow is { } window) window.Resumed += OnResumed;
        UpdateColumns();
        _viewModel.Load();
    }
}
