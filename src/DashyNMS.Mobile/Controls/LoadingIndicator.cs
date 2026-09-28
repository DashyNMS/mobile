namespace DashyNMS.Mobile.Controls;

/// <summary>
/// A spinner and "Loading…", as desktop shows while a view fetches. Pages
/// bind its IsVisible to <c>ViewModelBase.IsLoadingFirstTime</c> (issue #45),
/// so it only shows while there's nothing on the page yet.
/// </summary>
public sealed class LoadingIndicator : VerticalStackLayout
{
    public LoadingIndicator()
    {
        Spacing = 8;
        Margin = new Thickness(0, 32);
        HorizontalOptions = LayoutOptions.Center;
        VerticalOptions = LayoutOptions.Start;

        var spinner = new ActivityIndicator();
        spinner.SetBinding(ActivityIndicator.IsRunningProperty, new Binding(nameof(IsVisible), source: this));

        var label = new Label { Text = "Loading…", HorizontalOptions = LayoutOptions.Center };
        if (Application.Current?.Resources.TryGetValue("Caption", out var caption) == true && caption is Style style)
        {
            label.Style = style;
        }

        Children.Add(spinner);
        Children.Add(label);
        SemanticProperties.SetDescription(this, "Loading");
    }
}
