namespace DashyNMS.Mobile.Controls;

/// <summary>
/// The app's rounded search well. Keeping the Entry as page content preserves
/// compiled bindings, focus callbacks and keyboard shortcuts when reusing the chrome.
/// </summary>
[ContentProperty(nameof(Input))]
public sealed partial class SearchField : Border
{
    private Entry? _input;

    public SearchField() => InitializeComponent();

    public Entry? Input
    {
        get => _input;
        set
        {
            if (_input is { } previous)
            {
                previous.HandlerChanged -= OnHandlerChanged;
                previous.Loaded -= OnHandlerChanged;
                KeyboardDismiss.SetOnSearch(previous, false);
                FieldGrid.Remove(previous);
            }
            _input = value;
            if (value is null) return;
            value.ReturnType = ReturnType.Search;
            value.ClearButtonVisibility = ClearButtonVisibility.Never;
            value.IsSpellCheckEnabled = false;
            value.IsTextPredictionEnabled = false;
            value.MinimumHeightRequest = 40;
            value.VerticalOptions = LayoutOptions.Center;
            FieldGrid.SetColumn((IView)value, 1);
            FieldGrid.Add(value);
            KeyboardDismiss.SetOnSearch(value, true);
            value.HandlerChanged += OnHandlerChanged;
            value.Loaded += OnHandlerChanged;
            OnHandlerChanged(value, EventArgs.Empty);
        }
    }

    private void OnHandlerChanged(object? sender, EventArgs e)
    {
#if IOS
        if (_input?.Handler?.PlatformView is UIKit.UITextField field)
        {
            field.InputAccessoryView = new UIKit.UIView(CoreGraphics.CGRect.Empty);
            field.InputAssistantItem.LeadingBarButtonGroups = [];
            field.InputAssistantItem.TrailingBarButtonGroups = [];
        }
#endif
    }

    /// <summary>The well is the animated element; the Entry remains the focus target.</summary>
    internal static SearchField? Around(Entry input) => input.Parent?.Parent as SearchField;
}
