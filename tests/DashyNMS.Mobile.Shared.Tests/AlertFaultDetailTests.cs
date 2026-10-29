using System.Text.Json;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Alerting;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Models;
using System.Net;

namespace DashyNMS.Mobile.Tests;

public sealed class AlertFaultDetailTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0);

    private static JsonElement Payload(string port, int errors) => JsonSerializer.SerializeToElement(new
    {
        rule = new[] { new { ifName = port, ifInErrors_delta = errors } }
    });

    private static Alert Fault(bool payload = true, bool acknowledged = false)
    {
        var alert = Fakes.Alert(1, 7, "critical", acknowledged);
        alert.Source = AlertSource.Fault;
        alert.RuleId = 10;
        alert.EntityType = "port";
        alert.EntityKey = "port:1";
        alert.Details = payload ? Payload("port-one", 42) : null;
        return alert;
    }

    private static AlertLogEntry Log(int id, int? fault, int state, DateTime at, string port = "port-log") => new()
    {
        Id = id, RuleId = 10, DeviceId = 7, FaultId = fault, StateValue = state, TimeLogged = at, Details = Payload(port, id)
    };

    private static List<SectionGroup> Groups(Alert alert, AlertLogEntry[] entries, bool truncated = false) =>
        AlertDetailViewModel.BuildGroups(new AlertItem(alert, true), null, entries, true, now: Now, historyTruncated: truncated).ToList();

    [Fact]
    public void Current_payload_wins_over_newer_sibling_and_own_log_entries()
    {
        var groups = Groups(Fault(), [Log(99, 2, 1, Now, "sibling-port"), Log(12, 1, 1, Now.AddHours(-1), "older-port")]);
        var detail = Assert.Single(groups.Single(g => g.Name == "Why it fired"));
        Assert.Equal("port-one", detail.Title);
        Assert.Contains(detail.Fields, field => field.Value == "42");
        Assert.DoesNotContain(detail.Fields, field => field.Value == "99");
    }

    [Fact]
    public void A_fault_without_payload_uses_only_its_own_matching_log()
    {
        var groups = Groups(Fault(payload: false), [Log(99, 2, 1, Now, "sibling-port"), Log(12, 1, 1, Now.AddHours(-1), "own-port")]);
        Assert.Equal("own-port", Assert.Single(groups.Single(g => g.Name == "Why it fired")).Title);
        var history = groups.Single(g => g.Name == "History");
        Assert.DoesNotContain(history, row => row.Title == "Acknowledged");
        Assert.Equal(2, history.Count); // group recurrence summary and the selected fault's one entry
    }

    [Fact]
    public void An_entity_fault_never_falls_back_to_sibling_or_legacy_details()
    {
        var groups = Groups(Fault(payload: false), [Log(99, 2, 1, Now, "sibling-port"), Log(98, null, 1, Now, "legacy-port")]);
        Assert.DoesNotContain(groups, g => g.Name.StartsWith("Why it fired"));
    }

    [Fact]
    public void Eight_fault_events_are_one_previous_group_occurrence()
    {
        var entries = Enumerable.Range(1, 8).Select(id => Log(id, id, 1, Now.AddDays(-3)))
            .Concat(Enumerable.Range(1, 8).Select(id => Log(10 + id, id, 0, Now.AddDays(-2))))
            .Append(Log(30, 1, 1, Now.AddHours(-1))).Reverse().ToArray();
        var history = Groups(Fault(), entries).Single(g => g.Name == "History");
        Assert.StartsWith("Fired once in the last 30 days", history[0].Title);
        Assert.Equal(4, history.Count); // summary plus this fault's three events, not all 17
    }

    [Fact]
    public void Truncated_history_reports_a_minimum_and_an_explicit_warning()
    {
        var history = Groups(Fault(), [Log(3, 1, 1, Now.AddHours(-1)), Log(2, 1, 0, Now.AddDays(-1)), Log(1, 1, 1, Now.AddDays(-2))], truncated: true)
            .Single(g => g.Name == "History");
        Assert.StartsWith("Fired at least once", history[0].Title);
        Assert.Contains(history, row => row.Title == "History incomplete");
    }

    [Fact]
    public void A_recovery_without_a_retained_start_is_marked_incomplete()
    {
        var history = Groups(Fault(), [Log(2, 1, 1, Now.AddHours(-1)), Log(1, 1, 0, Now.AddDays(-1))]).Single(g => g.Name == "History");
        Assert.Contains(history, row => row.Title == "History incomplete");
    }

    [Fact]
    public void A_recovered_fault_without_a_logged_end_is_marked_incomplete()
    {
        var alert = Fault();
        alert.StateValue = 0;
        var history = Groups(alert, [Log(1, 1, 1, Now.AddHours(-1))]).Single(g => g.Name == "History");
        Assert.Contains(history, row => row.Title == "History incomplete");
    }

    [Fact]
    public async Task A_full_device_log_can_prove_the_period_complete_even_when_other_rules_fill_the_limit()
    {
        var now = DateTime.UtcNow;
        var alert = Fault();
        var entries = new List<AlertLogEntry>
        {
            Log(3, 1, 1, now.AddHours(-1)), Log(2, 1, 0, now.AddDays(-5)), Log(1, 1, 1, now.AddDays(-6))
        };
        entries.AddRange(Enumerable.Range(4, AlertDetailViewModel.LogDepth - 3).Select(id => new AlertLogEntry
        {
            Id = id, RuleId = 999, DeviceId = 7, StateValue = 1, TimeLogged = now.AddDays(-40)
        }));
        var client = Fakes.Client(alerts: [alert]);
        client.Logs.ListAlertLogAsync(7, AlertDetailViewModel.LogDepth, Arg.Any<CancellationToken>()).Returns(entries);
        var vm = new AlertDetailViewModel(client, Fakes.Settings(), Substitute.For<IDialogService>(), new RecordingNavigation(), Substitute.For<ISelfActionTracker>());
        await vm.LoadAsync(new AlertKey(AlertSource.Fault, 1));
        var history = vm.Groups.Single(g => g.Name == "History");
        Assert.StartsWith("Fired once in the last 30 days", history[0].Title);
        Assert.DoesNotContain(history, row => row.Title == "History incomplete");
    }

    [Fact]
    public async Task An_acknowledged_fault_keeps_its_payload_when_the_log_is_denied()
    {
        var alert = Fault(acknowledged: true);
        var client = Fakes.Client(alerts: [alert]);
        client.Logs.ListAlertLogAsync(7, Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns<IReadOnlyList<AlertLogEntry>>(_ => throw new LibreNmsApiException("Denied", HttpStatusCode.Forbidden));
        var vm = new AlertDetailViewModel(client, Fakes.Settings(), Substitute.For<IDialogService>(), new RecordingNavigation(), Substitute.For<ISelfActionTracker>());
        await vm.LoadAsync(new AlertKey(AlertSource.Fault, 1));
        Assert.True(vm.Alert!.IsAcknowledged);
        Assert.Equal("port-one", Assert.Single(vm.Groups.Single(g => g.Name == "Why it fired")).Title);
        Assert.Contains(vm.Groups.Single(g => g.Name == "History"), row => row.Title == "History unavailable");
        Assert.False(vm.HasError);
    }
}
