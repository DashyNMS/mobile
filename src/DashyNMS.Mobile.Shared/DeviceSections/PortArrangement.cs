using System.Globalization;
using System.Text.RegularExpressions;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.DeviceSections;

/// <summary>What a port row is sorted and grouped by on the Ports page (#92).</summary>
public sealed record PortFacts(
    int? Index,
    string Name,
    int StatusRank,
    string StatusName,
    IReadOnlyList<int> Vlans,
    double Traffic,
    long? Speed,
    string? Alias,
    string Type)
{
    /// <summary>
    /// A port's facts, as LibreNMS reports them. <paramref name="statusRank"/>
    /// and <paramref name="statusName"/> come from <see cref="PortStatus"/>:
    /// 0 errored, 1 down, 2 up, 3 shut down, 4 not monitored, 5 unknown, so
    /// sorting by rank leads with the ports that need looking at.
    /// </summary>
    /// <param name="deviceReportsMemberships">Whether any of the device's ports report VLAN memberships; see <see cref="CarriedVlans"/>.</param>
    public static PortFacts From(Port port, int statusRank, string statusName, bool deviceReportsMemberships = false) => new(
        port.IfIndex,
        port.DisplayName,
        statusRank,
        statusName,
        CarriedVlans(port, deviceReportsMemberships).Numbers,
        port.IsUp ? SumValidRates(port.IfInOctetsRate, port.IfOutOctetsRate) : 0,
        port.IfSpeed is > 0 ? port.IfSpeed : null,
        string.IsNullOrWhiteSpace(port.IfAlias) || port.IfAlias == port.DisplayName ? null : port.IfAlias.Trim(),
        TypeName(port.IfType));

    /// <summary>
    /// The VLANs a port carries: its typed memberships, unique and in number
    /// order, and whether they came from memberships. Only when the port has
    /// none and the device reports none at all (Core's
    /// <see cref="VlanMembership"/> rule) does its direct ifVlan stand in.
    /// The card's VLANs line and Group by VLAN both read this one rule, so a
    /// trunk with ifVlan 1 carrying 10 and 20 can no longer sit under
    /// "VLAN 1" while its card lists 10 and 20 (DashyNMS/desktop#571).
    /// </summary>
    internal static (IReadOnlyList<int> Numbers, bool FromMemberships) CarriedVlans(Port port, bool deviceReportsMemberships)
    {
        var members = port.Vlans.Select(v => v.Vlan).Where(v => v > 0).Distinct().Order().ToList();
        if (members.Count > 0)
        {
            return (members, true);
        }

        return !deviceReportsMemberships && port.IfVlan is > 0 and var vlan
            ? ([vlan], false)
            : ([], false);
    }

    /// <summary>Invalid counters must not dominate traffic ranking or offset valid errors.</summary>
    internal static double SumValidRates(double? first, double? second)
    {
        static double Valid(double? rate) => rate is { } value && double.IsFinite(value) && value >= 0 ? value : 0;
        var sum = Valid(first) + Valid(second);
        return double.IsFinite(sum) ? sum : double.MaxValue;
    }

    /// <summary>ifType as a person would say it.</summary>
    internal static string TypeName(string? ifType) => ifType?.Trim() switch
    {
        null or "" => "Other",
        "ethernetCsmacd" or "gigabitEthernet" or "fastEther" => "Ethernet",
        "ieee8023adLag" => "Link aggregation",
        "l2vlan" or "l3ipvlan" => "VLAN interface",
        "tunnel" => "Tunnel",
        "softwareLoopback" => "Loopback",
        "propVirtual" => "Virtual",
        "ieee80211" => "Wireless",
        "mpls" => "MPLS",
        var other => other,
    };
}

public enum PortSort
{
    PortId,
    Name,
    Status,
    Vlan,
    Traffic,
    Speed,
    Description,
}

public enum PortGrouping
{
    None,
    Status,
    Vlan,
    Type,
}

public sealed record PortSortOption(PortSort Sort, string Label)
{
    public override string ToString() => Label;
}

public sealed record PortGroupingOption(PortGrouping Grouping, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// Sorts and groups a device's ports the way the Ports page asks (#92):
/// by port ID in one list unless the user picks otherwise, as a device's
/// own port list reads.
/// </summary>
public static class PortArrangement
{
    public static IReadOnlyList<PortSortOption> SortOptions { get; } =
    [
        new(PortSort.PortId, "Port ID"),
        new(PortSort.Name, "Name"),
        new(PortSort.Status, "Status (errors and down first)"),
        new(PortSort.Vlan, "VLAN"),
        new(PortSort.Traffic, "Traffic (busiest first)"),
        new(PortSort.Speed, "Speed (fastest first)"),
        new(PortSort.Description, "Description"),
    ];

    public static IReadOnlyList<PortGroupingOption> GroupingOptions { get; } =
    [
        new(PortGrouping.None, "No grouping"),
        new(PortGrouping.Status, "By status"),
        new(PortGrouping.Vlan, "By VLAN"),
        new(PortGrouping.Type, "By type"),
    ];

    public static IReadOnlyList<SectionGroup> Arrange(IEnumerable<SectionRow> rows, PortSort sort, PortGrouping grouping)
    {
        var ports = rows.Where(r => r.Port is not null).ToList();
        var sorted = Sort(ports, sort).ToList();

        return grouping switch
        {
            PortGrouping.Status => sorted
                .GroupBy(r => (r.Port!.StatusRank, r.Port.StatusName))
                .OrderBy(g => g.Key.StatusRank)
                .Select(g => new SectionGroup(g.Key.StatusName, g))
                .ToList(),
            PortGrouping.Vlan => sorted
                .GroupBy(r => VlanKey(r.Port!))
                .OrderBy(g => g.Key.Order)
                .ThenBy(g => g.Key.Vlan)
                .Select(g => new SectionGroup(g.Key.Name, g))
                .ToList(),
            PortGrouping.Type => sorted
                .GroupBy(r => r.Port!.Type)
                .OrderBy(g => g.Key == "Other" ? 1 : 0)
                .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new SectionGroup(g.Key, g))
                .ToList(),
            _ => sorted.Count == 0 ? [] : [new SectionGroup("Ports", sorted)],
        };
    }

    private static IEnumerable<SectionRow> Sort(IEnumerable<SectionRow> rows, PortSort sort)
    {
        var byIndex = Comparer<SectionRow>.Create((a, b) =>
        {
            var index = (a.Port!.Index ?? int.MaxValue).CompareTo(b.Port!.Index ?? int.MaxValue);
            return index != 0 ? index : NaturalComparer.Instance.Compare(a.Port.Name, b.Port.Name);
        });

        return sort switch
        {
            PortSort.Name => rows.OrderBy(r => r.Port!.Name, NaturalComparer.Instance).ThenBy(r => r, byIndex),
            PortSort.Status => rows.OrderBy(r => r.Port!.StatusRank).ThenBy(r => r, byIndex),
            PortSort.Vlan => rows.OrderBy(r => VlanKey(r.Port!).Order).ThenBy(r => VlanKey(r.Port!).Vlan).ThenBy(r => r, byIndex),
            PortSort.Traffic => rows.OrderByDescending(r => r.Port!.Traffic).ThenBy(r => r, byIndex),
            PortSort.Speed => rows.OrderByDescending(r => r.Port!.Speed ?? -1).ThenBy(r => r, byIndex),
            PortSort.Description => rows
                .OrderBy(r => r.Port!.Alias is null ? 1 : 0)
                .ThenBy(r => r.Port!.Alias, NaturalComparer.Instance)
                .ThenBy(r => r, byIndex),
            _ => rows.OrderBy(r => r, byIndex),
        };
    }

    /// <summary>
    /// A port's VLAN heading, following the VLANs its card lists
    /// (DashyNMS/desktop#571): "Trunk" first for a port carrying more than
    /// one, then "VLAN n" by number (2, 10, 100) for a port carrying just
    /// that one, so access and single-tagged ports sit together, and
    /// "No VLAN" last. Sorting by VLAN uses the same order, so the two agree.
    /// </summary>
    private static (int Order, int Vlan, string Name) VlanKey(PortFacts port) => port.Vlans switch
    {
        { Count: > 1 } => (0, 0, "Trunk"),
        [var vlan] => (1, vlan, string.Create(CultureInfo.InvariantCulture, $"VLAN {vlan}")),
        _ => (2, 0, "No VLAN"),
    };
}

/// <summary>Compares names with their numbers as numbers: Gi1/0/2 before Gi1/0/10.</summary>
public sealed partial class NaturalComparer : IComparer<string?>
{
    public static NaturalComparer Instance { get; } = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        var a = Parts().Matches(x);
        var b = Parts().Matches(y);
        for (var i = 0; i < Math.Min(a.Count, b.Count); i++)
        {
            var left = a[i].Value;
            var right = b[i].Value;
            var result = char.IsDigit(left[0]) && char.IsDigit(right[0]) && long.TryParse(left, out var l) && long.TryParse(right, out var r)
                ? l.CompareTo(r)
                : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
            if (result != 0)
            {
                return result;
            }
        }

        return a.Count.CompareTo(b.Count);
    }

    [GeneratedRegex(@"\d+|\D+")]
    private static partial Regex Parts();
}
