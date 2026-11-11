namespace DashyNMS.Mobile.Intelligence;

/// <summary>Keep a reader's place once they move more than two lines from the end.</summary>
public static class ChatScrollFollow
{
    public static bool IsNearEnd(double offset, double viewport, double content) =>
        double.IsFinite(offset) && double.IsFinite(viewport) && double.IsFinite(content)
        && viewport > 0 && content - viewport - offset <= 64;
}
