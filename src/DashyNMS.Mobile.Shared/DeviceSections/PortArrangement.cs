using System.Text.RegularExpressions;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.DeviceSections;

/// <summary>What a port row is sorted and grouped by on the Ports page (#92).</summary>
public sealed record PortFacts(
    int? Index,
    string Name,
    int StatusRank,
    string StatusName,
    int? Vlan,
    bool IsTrunk,
    double Traffic,
    long? Speed,
    string? Alias,
    string Type)
{
    /// <summary>A port's facts, as LibreNMS reports them; <paramref name="statusRank"/> 0 is down, 1 up, 2 shut down.</summary>
    public static PortFacts From(Port port, int statusRank, string statusName)
    {
        // The port's own (untagged) VLAN, from ifVlan or its untagged
        // membership; tagged memberships beyond that make it a trunk.
        var untagged = port.Vlans.Where(v => v.Untagged).Select(v => v.Vlan).FirstOrDefault(v => v > 0);
        var vlan = port.IfVlan is > 0 ? port.IfVlan : untagged > 0 ? untagged : null;
        var tagged = port.Vlans.Count(v => !v.Untagged && v.Vlan > 0);

        return new PortFacts(
            port.IfIndex,
            port.DisplayName,
            statusRank,
            statusName,
            vlan,
            tagged > 0,
            port.IsUp ? (port.IfInOctetsRate ?? 0) + (port.IfOutOctetsRate ?? 0) : 0,
            port.IfSpeed is > 0 ? port.IfSpeed : null,
            string.IsNullOrWhiteSpace(port.IfAlias) || port.IfAlias == port.DisplayName ? null : port.IfAlias.Trim(),
            TypeName(port.IfType));
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
        new(PortSort.Status, "Status (down first)"),
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
            PortSort.Vlan => rows.OrderBy(r => r.Port!.Vlan ?? int.MaxValue).ThenBy(r => r, byIndex),
            PortSort.Traffic => rows.OrderByDescending(r => r.Port!.Traffic).ThenBy(r => r, byIndex),
            PortSort.Speed => rows.OrderByDescending(r => r.Port!.Speed ?? -1).ThenBy(r => r, byIndex),
            PortSort.Description => rows
                .OrderBy(r => r.Port!.Alias is null ? 1 : 0)
                .ThenBy(r => r.Port!.Alias, NaturalComparer.Instance)
                .ThenBy(r => r, byIndex),
            _ => rows.OrderBy(r => r, byIndex),
        };
    }

    /// <summary>A port's VLAN heading: its own VLAN, "Trunk" for tagged ports with none of their own, or "No VLAN" - those two last.</summary>
    private static (int Order, int Vlan, string Name) VlanKey(PortFacts port) =>
        port.Vlan is { } vlan ? (0, vlan, $"VLAN {vlan}")
        : port.IsTrunk ? (1, 0, "Trunk")
        : (2, 0, "No VLAN");
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
