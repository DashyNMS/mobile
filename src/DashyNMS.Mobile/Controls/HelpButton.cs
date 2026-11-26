using DashyNMS.Mobile.Help;
using DashyNMS.Mobile.Services;
using Microsoft.Extensions.DependencyInjection;

namespace DashyNMS.Mobile.Controls;

/// <summary>A quiet link to the page of the user guide that explains this part of the app (#668).</summary>
public sealed class HelpButton : Button
{
    public HelpButton()
    {
        Style = (Style)Application.Current!.Resources["LinkButton"];
        MinimumHeightRequest = 44;

        // In line with the text above it, not the link style's inset.
        Padding = new Thickness(0, 0, 8, 0);
        HorizontalOptions = LayoutOptions.Start;
        LineBreakMode = LineBreakMode.WordWrap;
        Clicked += OnClicked;
        SemanticProperties.SetHint(this, "Opens in Help");
    }

    /// <summary>What to open; a link without one isn't shown.</summary>
    public HelpTopic? Topic { get; set; }

    private async void OnClicked(object? sender, EventArgs e)
    {
        if (Topic is { } topic && Handler?.MauiContext?.Services.GetService<INavigationService>() is { } navigation)
        {
            await navigation.OpenHelpAsync(topic);
        }
    }
}
