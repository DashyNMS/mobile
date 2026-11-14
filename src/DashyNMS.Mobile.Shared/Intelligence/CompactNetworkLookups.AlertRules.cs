using DashyNMS.Agent.Alerts;
using DesktopNMS.Core.Models;

namespace DashyNMS.Mobile.Intelligence;

public sealed partial class CompactNetworkLookups
{
    /// <summary>Source failures lead the reply, so a compact budget cannot hide them.</summary>
    private static string AlertUnavailable(IReadOnlyList<DashyNMS.Agent.Unavailable>? sources)
        => sources is { Count: > 0 } ? "Unavailable: " + string.Join(", ", sources.Select(s => Clean(s.Source))) + ". " : "";

    private static Reply AlertDetailRows(AlertDetailsResult found, DateTimeOffset at, TimeProvider time)
    {
        var data = found.Data;
        var alert = data.Alert;
        var rows = new List<Row>();
        var evidence = new List<IntelligenceEvidence>();
        var marker = "";
        if (data.Device is { DeviceId: > 0 } device)
        {
            var state = device.Status[..1].ToUpperInvariant() + device.Status[1..];
            marker = $" [[alert:{Id(alert.AlertId)}]]";
            evidence.Add(new(new("alert:" + Id(alert.AlertId), Clean(data.Rule.Name), alert.State, alert.Severity, at, "Alert detail at lookup time")
                { Object = new(alert.Source == "fault" ? "fault" : "alert", alert.AlertId, device.DeviceId) }));
            rows.Add(new($"Device {Clean(device.Display ?? device.Hostname)} [[device:{Id(device.DeviceId)}]]; hostname {Clean(device.Hostname)}; IP {Clean(device.Ip)}; location {Clean(device.Location)}; state {state}.",
                [new(new("device:" + Id(device.DeviceId), Clean(device.Display ?? device.Hostname), state, device.Status, at, "Device list for this alert")
                    { Object = new("device", device.DeviceId) })]));
        }
        var history = data.History;
        var heading = AlertUnavailable(data.Unavailable) + $"{Clean(alert.Source)} {Id(alert.AlertId)}{marker}: {Clean(alert.Severity)} {Clean(alert.State)}; {Clean(alert.Entity)}. "
            + (history.FiredBeforeIn30Days is { } count ? $"Previous firings in 30 days: {(history.AtLeast == true ? "at least " : "")}{Id(count)}. " : "Previous firings unknown. ");
        rows.Insert(0, new($"Rule {Id(data.Rule.RuleId)}: {Clean(data.Rule.Name)}; severity {Clean(data.Rule.Severity)}; condition {Clean(data.Rule.Condition)}."));
        if (alert.Since is { } since) rows.Add(new("Changed " + IntelligenceTime.AtAndAgo(since, at, time) + "."));
        if (alert.Note is { } note) rows.Add(new("Alert note: " + Clean(note)));
        if (data.Rule.Procedure is { } procedure) rows.Add(new("Procedure: " + Clean(procedure)));
        if (data.Rule.Notes is { } notes) rows.Add(new("Rule notes: " + Clean(notes)));
        if (data.Group is { } group)
        {
            rows.Add(new($"Open group: {Id(group.FaultCount)} faults; {Id(group.FaultCount - group.AcknowledgedCount)} active; {Id(group.AcknowledgedCount)} acknowledged; {Clean(group.State)} {Clean(group.Severity)}."));
            rows.AddRange((group.Faults ?? []).Select(f => new Row($"Group fault {Id(f.FaultId)}: {Clean(f.Entity)}; {Clean(f.State)}")));
        }
        rows.Add(new($"Matched {Id(data.Why.Matched)} items; rule condition {(data.Why.RuleConditionKnown ? "known" : "unknown")}."
            + (data.Why.RecordedAt is { } recorded ? " Recorded " + IntelligenceTime.AtAndAgo(recorded, at, time) + "." : "")));
        rows.AddRange(data.Why.Faults.Select(f => new Row("Trigger " + Clean(f.What) + ": " + string.Join("; ", f.TriggerValues.Select(v => Clean(v.Field) + "=" + Clean(v.Value))))));
        rows.AddRange(history.Recent.Select(t => new Row("Previous firing " + IntelligenceTime.AtAndAgo(t, at, time) + ".")));
        return Rows(heading, rows, evidence, alsoOmitted: data.Why.FaultsOmitted ?? 0);
    }

    private static Reply AlertRuleRows(AlertRulesResult found)
    {
        var data = found.Data;
        IEnumerable<Row> RuleRows()
        {
            foreach (var rule in data.Rules)
            {
                yield return new($"Rule {Id(rule.RuleId)}: {Clean(rule.Name)}; {Clean(rule.Severity)}; {(rule.Enabled ? "enabled" : "disabled")}; open {Count(rule.OpenAlerts)}; raised in 7 days {Count(rule.RaisedLast7Days)}.");
                yield return new($"Rule {Id(rule.RuleId)} condition: {Clean(rule.Condition)}");
                var applicability = rule.AppliesTo;
                if (applicability.AllDevices) yield return new($"Rule {Id(rule.RuleId)} applies to all devices.");
                else if (applicability.Targets is { } targets)
                {
                    yield return new($"Rule {Id(rule.RuleId)} applies to {(targets.Except == true ? "all except" : "only")} these targets:");
                    foreach (var (kind, items) in new[] { ("device", targets.Devices), ("group", targets.Groups), ("location", targets.Locations) })
                        foreach (var target in items ?? []) yield return new($"Rule {Id(rule.RuleId)} target {kind} {Id(target.Id)}: {Clean(target.Name)}");
                }
                if (rule.Template is { } template) yield return new($"Rule {Id(rule.RuleId)} template: {Clean(template)}");
                if (rule.NotifyPerEntity is { } perEntity) yield return new($"Rule {Id(rule.RuleId)} per-entity notification: {(perEntity ? "yes" : "no")}; max entities: {Count(rule.MaxEntities)}; inherited: {(rule.MaxEntitiesInherited == true ? "yes" : "no")}.");
                if (rule.Acknowledging is { } acknowledging) yield return new($"Rule {Id(rule.RuleId)} acknowledgement: {Clean(acknowledging)}");
                foreach (var removed in rule.RemovedColumns ?? []) yield return new($"Rule {Id(rule.RuleId)} removed column {Clean(removed.Column)} in {Clean(removed.RemovedIn)}; replacement {Clean(removed.Replacement)}; {Clean(removed.Advice)}");
                if (rule.Notes is { } notes) yield return new($"Rule {Id(rule.RuleId)} notes: {Clean(notes)}");
                if (rule.Procedure is { } procedure) yield return new($"Rule {Id(rule.RuleId)} procedure: {Clean(procedure)}");
            }
        }
        // Whole detail rows are counted too; rule totals and page bounds remain explicit.
        var page = data.Page;
        return Rows(AlertUnavailable(data.Unavailable) + found.Summary + $" Total {Id(page.Total)}; offset {Id(page.Offset)}; limit {Id(page.Limit)}; returned {Id(page.Returned)}."
            + (page.NextOffset is { } next ? $" Next offset {Id(next)}." : ""),
            RuleRows(), alsoOmitted: Math.Max(0, page.Total - page.Offset - page.Returned));
    }

    private static string Count(int? value) => value is { } count ? Id(count) : "unknown";
}
