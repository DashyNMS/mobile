namespace DashyNMS.Mobile.Dashboard;

/// <summary>Keeps the dashboard and its editor at the same column count for the available width.</summary>
public static class DashboardColumns
{
    public static int Count(double width, double minimumWidth = 340, double gap = 12, int maximum = 3) =>
        Math.Clamp((int)((Math.Max(0, width) + gap) / (minimumWidth + gap)), 1, Math.Max(1, maximum));
}
