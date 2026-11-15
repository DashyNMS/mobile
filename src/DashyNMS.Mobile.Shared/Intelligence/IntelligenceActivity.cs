namespace DashyNMS.Mobile.Intelligence;

/// <summary>Desktop's activity words, with no raw tool text in the collapsed status.</summary>
public static class IntelligenceActivity
{
    public static string Describe(IntelligenceTurn turn)
    {
        if (turn.State is IntelligenceTurnState.Stopped or IntelligenceTurnState.Interrupted or IntelligenceTurnState.Failed)
            return turn.Message;
        if (turn.Lookups.LastOrDefault(l => l.State == IntelligenceLookupState.Running) is { } running)
            return Step(running.Name) + "…";
        var names = turn.Lookups.Select(l => Subject(l.Name)).Distinct().ToList();
        var failures = turn.Lookups.Count(l => l.State == IntelligenceLookupState.Failed);
        return names.Count > 0 ? "Looked at " + JoinNames(names) + (failures > 0 ? $" ({failures} couldn't be read)" : "")
            : turn.State == IntelligenceTurnState.Running ? "Thinking…" : "";
    }
    private static string JoinNames(List<string> names) => names.Count switch
    {
        1 => names[0],
        2 => names[0] + " and " + names[1],
        _ => string.Join(", ", names.Take(names.Count - 1)) + " and " + names[^1],
    };

    /// <summary>What a tool is doing, as a person would say it.</summary>
    public static string Step(string tool) => tool switch
    {
        "web_search" => "Searching the web",
        "active_alerts" => "Checking alerts",
        "alert_details" => "Reading the alert",
        "alert_rules" => "Reading alert rules",
        "config_backup" or "config_diff" or "recent_config_changes" => "Checking config backups",
        "device_details" => "Reading device details",
        "device_events" or "network_events" => "Reading the event log",
        "device_health" => "Checking device health",
        "device_neighbours" => "Finding neighbours",
        "device_sensors" or "health_overview" => "Reading sensors",
        "find_device" => "Finding the device",
        "graph" => "Drawing the graph",
        "list_groups_and_locations" => "Listing groups and locations",
        "network_overview" => "Getting the network overview",
        "port_status" => "Checking the port",
        "search_logs" => "Searching Graylog",
        "top_devices" or "top_interfaces" or "top_errors" => "Ranking devices and ports",
        "trace_endpoint" => "Tracing the address",
        "what_changed" => "Finding what changed",
        _ => "Looking it up",
    };

    /// <summary>What a tool looked at, for "Looked at …".</summary>
    public static string Subject(string tool) => tool switch
    {
        "web_search" => "the web",
        "active_alerts" or "alert_details" => "alerts",
        "alert_rules" => "alert rules",
        "config_backup" or "config_diff" or "recent_config_changes" => "config backups",
        "device_details" or "find_device" => "devices",
        "graph" => "graphs",
        "device_events" or "network_events" => "the event log",
        "device_health" => "device health",
        "device_neighbours" => "neighbours",
        "device_sensors" or "health_overview" => "sensors",
        "list_groups_and_locations" => "groups and locations",
        "network_overview" => "the network overview",
        "port_status" => "ports",
        "search_logs" => "Graylog",
        "top_devices" or "top_interfaces" or "top_errors" => "the busiest devices and ports",
        "trace_endpoint" => "where the address is plugged in",
        "what_changed" => "recent changes",
        _ => "the network",
    };
}
