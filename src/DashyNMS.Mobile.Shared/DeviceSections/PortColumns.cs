namespace DashyNMS.Mobile.DeviceSections;

/// <summary>Ports alone keep two portrait tracks and approximately the same width in landscape.</summary>
public static class PortColumns
{
    public static (int Count, double Width) Fit(double available, double portraitAvailable, bool landscape, double gap)
    {
        if (!double.IsFinite(available) || available <= 0)
            return (2, 0);
        if (!double.IsFinite(portraitAvailable) || portraitAvailable <= 0)
            return (2, available);
        var card = Math.Max(0, (Math.Min(available, portraitAvailable) - gap) / 2);
        if (!landscape) return (2, available);
        var count = Math.Max(2, (int)Math.Floor((available + gap) / (card + gap)));
        return (count, Math.Min(available, count * (card + gap) - gap));
    }
}
