using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.DeviceSections;

/// <summary>
/// A port's status class, in the order the Ports page sorts and groups it:
/// the ones needing attention (errored, then down) before healthy, switched
/// off, unmonitored and unknown ports.
/// </summary>
public enum PortCondition
{
    Errored,
    Down,
    Up,
    ShutDown,
    NotMonitored,
    Unknown,
}

/// <summary>
/// How a port's status, and so its colour, label, sort rank and group, is
/// decided from what Core's <see cref="Port"/> carries (#472). One rule for
/// every port surface keeps the card, the overview and the Status sort in
/// agreement. The first match wins:
/// <list type="number">
/// <item><b>Shut down</b>, grey, labelled Disabled: ifAdminStatus is down. An
/// operator switched it off, so its link state is expected and says nothing.</item>
/// <item><b>Not monitored</b>, grey: LibreNMS's <see cref="Port.Disabled"/>
/// (not polled) or <see cref="Port.Ignore"/> (excluded from alerting). Its
/// readings may be stale, and nobody is meant to act on them.</item>
/// <item><b>Errored</b>, red: a positive error reading in either direction.
/// Core's device-ports request asks LibreNMS for ifInErrors_delta and
/// ifOutErrors_delta (errors since the previous poll) but not the _rate
/// columns, and LibreNMS returns only the columns requested. So a valid rate
/// is used when one is present and the delta otherwise. Relying on rates
/// alone meant no live port could ever show as errored. LibreNMS reports an
/// error-disabled port as down with no separate state, so the error counters
/// are the only errored signal Core exposes.</item>
/// <item><b>Up</b>, green: ifOperStatus up and zero errors on both sides.
/// With no error reading on a side, the port cannot be called healthy; it
/// stays grey as "Up · errors unavailable".</item>
/// <item><b>Down</b>, amber: enabled, but ifOperStatus down, lowerLayerDown or
/// notPresent.</item>
/// <item><b>Unknown</b>, grey: anything else (testing, dormant, unknown or
/// missing).</item>
/// </list>
/// Colour is never the only signal: every class has its own label, which the
/// card's accessible description speaks.
/// </summary>
public sealed record PortStatus(PortCondition Condition, bool IsUp, bool IsOperDown, double? ErrorsIn, double? ErrorsOut)
{
    public static PortStatus Of(Port port)
    {
        var errorsIn = ErrorReading(port.IfInErrorsRate, port.IfInErrorsDelta);
        var errorsOut = ErrorReading(port.IfOutErrorsRate, port.IfOutErrorsDelta);
        var operDown = port.IfOperStatus?.ToLowerInvariant() is "down" or "lowerlayerdown" or "notpresent";
        var condition =
            string.Equals(port.IfAdminStatus, "down", StringComparison.OrdinalIgnoreCase) ? PortCondition.ShutDown
            : port.Disabled || port.Ignore ? PortCondition.NotMonitored
            : errorsIn > 0 || errorsOut > 0 ? PortCondition.Errored
            : port.IsUp ? PortCondition.Up
            : operDown ? PortCondition.Down
            : PortCondition.Unknown;
        return new PortStatus(condition, port.IsUp, operDown, errorsIn, errorsOut);
    }

    /// <summary>Errors this side, per second or in the last poll; null when neither is a valid reading.</summary>
    internal static double? ErrorReading(double? rate, long? delta) =>
        rate is { } value && double.IsFinite(value) && value >= 0 ? value
        : delta is >= 0 ? (double?)delta.Value
        : null;

    /// <summary>Errored 0 and Down 1 first, so the Status sort and grouping lead with what needs looking at.</summary>
    public int Rank => (int)Condition;

    public bool ErrorsInPositive => ErrorsIn > 0;
    public bool ErrorsOutPositive => ErrorsOut > 0;
    public bool HasErrors => ErrorsInPositive || ErrorsOutPositive;

    /// <summary>Both sides have a reading, so zero errors can be claimed.</summary>
    public bool ErrorsKnown => ErrorsIn is not null && ErrorsOut is not null;

    /// <summary>The Status sort's group heading.</summary>
    public string Name => Condition switch
    {
        PortCondition.Errored => "Errors",
        PortCondition.Down => "Down",
        PortCondition.Up => "Up",
        PortCondition.ShutDown => "Shut down",
        PortCondition.NotMonitored => "Not monitored",
        _ => "Unknown",
    };

    /// <summary>
    /// The link state alone, for the overview counts and search text: an
    /// errored port is still up or down, and the tile counts it that way.
    /// </summary>
    public string StateName => Condition != PortCondition.Errored ? Name
        : IsUp ? "Up" : IsOperDown ? "Down" : "Unknown";

    /// <summary>The card's status label beside its coloured dot.</summary>
    public string Label => Condition switch
    {
        PortCondition.Errored => IsUp ? "Up · errors" : IsOperDown ? "Down · errors" : "Errors",
        PortCondition.Down => "Down",
        PortCondition.Up => ErrorsKnown ? "Up" : "Up · errors unavailable",
        PortCondition.ShutDown => "Disabled",
        PortCondition.NotMonitored => "Not monitored",
        _ => "Unknown",
    };

    /// <summary>
    /// The card's colour class: red errored, amber down, green up, and grey
    /// for everything nobody can or should act on. Every port gets one, so
    /// the edge and label are never left in the card's plain colours.
    /// </summary>
    public RowStatus Visual => Condition switch
    {
        PortCondition.Errored => RowStatus.Critical,
        PortCondition.Down => RowStatus.Warning,
        PortCondition.Up when ErrorsKnown => RowStatus.Ok,
        _ => RowStatus.Inactive,
    };
}
