using DashyNMS.Mobile.Services;

namespace DashyNMS.Mobile.Help;

/// <summary>Opens a page of the guide from anywhere in the app (#668).</summary>
public static class HelpNavigation
{
    /// <summary>Pushes the topic's page, so Back returns to whatever asked for it.</summary>
    public static Task OpenHelpAsync(this INavigationService navigation, HelpTopic topic)
        => navigation.GoToAsync(Routes.HelpArticle, new Dictionary<string, object> { [Routes.HelpTopicParameter] = topic });
}
