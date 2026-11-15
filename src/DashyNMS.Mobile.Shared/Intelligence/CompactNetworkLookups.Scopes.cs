using DashyNMS.Agent.Devices;

namespace DashyNMS.Mobile.Intelligence;

public sealed partial class CompactNetworkLookups
{
    /// <summary>Exact filter names stay in evidence; model text uses only shared redacted names.</summary>
    private static Reply ScopeRows(GroupsAndLocationsResult found, DateTimeOffset at)
    {
        var data = found.Data;
        var heading = data.Groups is null ? "Groups unavailable" : $"Groups: {Id(data.Groups.Count)}";
        heading += $"; locations: {Id(data.Locations.Count)}.";
        if (data.DevicesWithoutLocation is { } without) heading += $" Devices without location: {Id(without)}.";
        var rows = new List<Row>();
        foreach (var group in data.Groups ?? [])
        {
            var name = Clean(group.Name);
            var marker = "";
            var evidence = new List<IntelligenceEvidence>();
            if (group.GroupId > 0 && !string.IsNullOrWhiteSpace(group.FilterName))
            {
                marker = $" [[group:{Id(group.GroupId)}]]";
                evidence.Add(new(new("group:" + Id(group.GroupId), name, "", "unknown", at, "Device group")
                    { Object = new("group", group.GroupId) }, group.FilterName));
            }
            rows.Add(new("Group " + name + marker + (group.Type is null ? "" : "; type " + Clean(group.Type))
                + (group.Description is null ? "" : "; " + Clean(group.Description)), evidence));
        }
        foreach (var location in data.Locations)
        {
            var name = Clean(location.Name);
            var marker = "";
            var evidence = new List<IntelligenceEvidence>();
            if (location.LocationId is { } id && id > 0)
            {
                marker = $" [[location:{Id(id)}]]";
                evidence.Add(new(new("location:" + Id(id), name, $"{Id(location.Down)} down", location.Down > 0 ? "down" : "unknown", at, "Device location")
                    { Object = new("location", id) }, location.FilterName));
            }
            rows.Add(new($"Location {name}{marker}: {Id(location.Devices)} devices; {Id(location.Down)} down.", evidence));
        }
        return Rows(heading, rows);
    }
}
