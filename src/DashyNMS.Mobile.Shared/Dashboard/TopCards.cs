using System.Globalization;
using DashyNMS.Mobile.DeviceSections;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Devices;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Dashboard;

/// <summary>
/// One row of a Top card: a device, perhaps one of its ports, its two
/// numbers, and a bar - for a port, how full it is against its speed; else
/// against the card's top row - as desktop's Top widgets draw them.
/// </summary>
public sealed record TopRow(int DeviceId, int? PortId, string Title, string? Subtitle, string InText, string OutText, double Bar)
{
    /// <summary>The device's state, for the row's dot.</summary>
    public DeviceState? DeviceState { get; init; }

    /// <summary>For Top errors: amber for any errors, red past <see cref="TopCards.CriticalErrorsPerSecond"/>.</summary>
    public RowStatus Severity { get; init; } = RowStatus.None;

    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);
}

/// <summary>
/// The Top interfaces, Top errors and Top devices cards (#103): Core's
/// <see cref="PortRankings"/> over every port in the network, turned into
/// rows the way desktop's widgets do, so both apps rank and colour alike.
/// </summary>
public static class TopCards
{
    /// <summary>Desktop's choices of row count; any other stored count shows as the default, as there.</summary>
    public static IReadOnlyList<int> CountChoices { get; } = [3, 5, 10];

    public const int DefaultCount = 3;

    /// <summary>Errors per second (in or out) at which a Top errors row turns red, as desktop.</summary>
    public const double CriticalErrorsPerSecond = 1;

    /// <summary>The widget's row count, if it's one desktop offers; else the default.</summary>
    public static int CountOf(DashboardWidget widget) => CountChoices.Contains(widget.TopCount) ? widget.TopCount : DefaultCount;

    public static bool IsTop(string? type) =>
        type is DashboardWidgetTypes.TopInterfaces or DashboardWidgetTypes.TopErrors or DashboardWidgetTypes.TopDevices;

    /// <summary>What a card with no rows says.</summary>
    public static string EmptyText(string type) => type switch
    {
        DashboardWidgetTypes.TopErrors => "No interface errors.",
        DashboardWidgetTypes.TopDevices => "No traffic on any device.",
        _ => "No traffic on any interface.",
    };

    /// <summary>
    /// Top devices adds each port's traffic, so traffic passing through a
    /// device counts twice - said under the card, so nobody reads it as throughput.
    /// </summary>
    public static string? Footnote(string type) => type == DashboardWidgetTypes.TopDevices
        ? "Total port traffic: traffic passing through a device counts on both ports."
        : null;

    /// <summary>The rows for one Top card, from the network's ports.</summary>
    public static IReadOnlyList<TopRow> Rows(DashboardWidget widget, IReadOnlyList<Port> ports, IReadOnlyDictionary<int, Device> devices, Func<Device, string> name)
    {
        var by = widget.TopRankBy;
        var count = CountOf(widget);

        string DeviceName(int id) => devices.TryGetValue(id, out var device) ? name(device) : "Device " + id.ToString(CultureInfo.CurrentCulture);
        DeviceState? StateOf(int id) => devices.TryGetValue(id, out var device) ? device.State : null;

        switch (widget.WidgetType)
        {
            case DashboardWidgetTypes.TopErrors:
            {
                var ranked = PortRankings.TopErrors(ports, by, count, includeZero: !widget.TopHideQuiet);
                var worst = ranked.Count == 0 ? 0 : ranked.Max(r => Math.Max(r.In, r.Out));
                return ranked
                    .Select(r => new TopRow(
                        r.Port.DeviceId, r.Port.PortId, DeviceName(r.Port.DeviceId), PortLabel(r.Port),
                        PerSecond(r.In), PerSecond(r.Out),
                        worst > 0 ? Math.Max(r.In, r.Out) / worst : 0)
                    {
                        DeviceState = StateOf(r.Port.DeviceId),
                        Severity = Math.Max(r.In, r.Out) >= CriticalErrorsPerSecond ? RowStatus.Critical
                            : r.Total > 0 ? RowStatus.Warning
                            : RowStatus.None,
                    })
                    .ToList();
            }

            case DashboardWidgetTypes.TopDevices:
            {
                var ranked = PortRankings.TopDevices(ports, by, count);
                double Value(RankedDevice d) => by switch { RankBy.In => d.In, RankBy.Out => d.Out, _ => d.Total };
                var top = ranked.Count == 0 ? 0 : ranked.Max(Value);
                return ranked
                    .Select(d => new TopRow(
                        d.DeviceId, null, DeviceName(d.DeviceId),
                        d.ActivePorts == 1 ? "1 active port" : $"{d.ActivePorts.ToString(CultureInfo.CurrentCulture)} active ports",
                        Bits(d.In), Bits(d.Out),
                        top > 0 ? Value(d) / top : 0)
                    {
                        DeviceState = StateOf(d.DeviceId),
                    })
                    .ToList();
            }

            default:
                return PortRankings.TopTraffic(ports, by, count)
                    .Select(r =>
                    {
                        var speed = r.Port.IfSpeed is > 0 ? r.Port.IfSpeed.Value : 0;
                        var busiest = by switch { RankBy.In => r.In, RankBy.Out => r.Out, _ => Math.Max(r.In, r.Out) };
                        return new TopRow(
                            r.Port.DeviceId, r.Port.PortId, DeviceName(r.Port.DeviceId), PortLabel(r.Port),
                            Bits(r.In), Bits(r.Out),
                            speed > 0 ? Math.Clamp(busiest / speed, 0, 1) : 0)
                        {
                            DeviceState = StateOf(r.Port.DeviceId),
                        };
                    })
                    .ToList();
        }
    }

    /// <summary>"Gi1/0/1 · Uplink to core" - the port's name, and its description when it says something more.</summary>
    internal static string PortLabel(Port port) =>
        string.IsNullOrWhiteSpace(port.IfAlias) || string.Equals(port.IfAlias, port.DisplayName, StringComparison.OrdinalIgnoreCase)
            ? port.DisplayName
            : $"{port.DisplayName} · {port.IfAlias}";

    /// <summary>"1.2 Gbps", "840 Kbps" - LibreNMS's own style, as desktop.</summary>
    public static string Bits(double bitsPerSecond)
    {
        if (bitsPerSecond <= 0)
        {
            return "0";
        }

        string[] units = ["bps", "Kbps", "Mbps", "Gbps", "Tbps"];
        var value = bitsPerSecond;
        var unit = 0;
        while (value >= 1000 && unit < units.Length - 1)
        {
            value /= 1000;
            unit++;
        }

        return value.ToString(unit == 0 ? "0" : value < 10 ? "0.#" : "0", CultureInfo.CurrentCulture) + " " + units[unit];
    }

    /// <summary>"0.4/s", "12/s".</summary>
    public static string PerSecond(double perSecond) =>
        perSecond <= 0 ? "0" : perSecond.ToString(perSecond < 10 ? "0.##" : "0", CultureInfo.CurrentCulture) + "/s";
}
