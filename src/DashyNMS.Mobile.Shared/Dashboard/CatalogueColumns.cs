namespace DashyNMS.Mobile.Dashboard;

/// <summary>Catalogue choices grow with text rather than following live dashboard density.</summary>
public static class CatalogueColumns
{
    public static int Count(double width, double textScale = 1)
    {
        if (!double.IsFinite(width) || width <= 0) return 1;
        if (!double.IsFinite(textScale) || textScale < 1) textScale = 1;
        return (int)Math.Clamp(Math.Floor((width + 10) / (160 * textScale + 10)), 1, 4);
    }
}
