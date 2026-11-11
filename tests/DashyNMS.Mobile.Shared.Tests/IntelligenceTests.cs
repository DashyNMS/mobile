using DashyNMS.Mobile.Diagnostics;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

/// <summary>The network overview lookup and the on-device model check (#570).</summary>
public sealed class IntelligenceTests
{
    /// <summary>A model that calls every lookup once, then writes its answer in two steps.</summary>
    private sealed class FakeModel : IIntelligenceModel
    {
        public IntelligenceAvailability Availability { get; set; } = IntelligenceAvailability.Available;

        public int? ContextSize { get; set; } = 4096;

        public IntelligenceException? Failure { get; set; }

        public TaskCompletionSource? Hold { get; set; }

        public Action? BeforeFirstWords { get; set; }

        public string? Instructions { get; private set; }

        public string? LookupResult { get; private set; }

        public bool Disposed { get; private set; }

        /// <summary>A token for every four characters, as a rule of thumb.</summary>
        public Task<int?> CountTokensAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult<int?>(text.Length / 4);

        public IIntelligenceSession StartSession(string instructions, IReadOnlyList<IntelligenceLookup> lookups)
        {
            Instructions = instructions;
            return new Session(this, lookups);
        }

        private sealed class Session(FakeModel model, IReadOnlyList<IntelligenceLookup> lookups) : IIntelligenceSession
        {
            public async Task<IntelligenceAnswer> AskAsync(string question, Action<string> onText, CancellationToken cancellationToken = default)
            {
                foreach (var lookup in lookups)
                    model.LookupResult = await lookup.Run(string.Empty, cancellationToken);

                model.BeforeFirstWords?.Invoke();
                onText("All");
                if (model.Hold is { } hold)
                    await hold.Task.WaitAsync(cancellationToken);
                if (model.Failure is { } failure)
                    throw failure;

                onText("All is well.");
                return new IntelligenceAnswer("All is well.", "input 300, output 5");
            }

            public void Dispose() => model.Disposed = true;
        }
    }

    private static IntelligencePreferences Preferences(bool accepted = true)
    {
        var settings = Substitute.For<ICheckedSettingsStore>();
        settings.Current.Returns(new AppSettings());
        settings.TrySave().Returns(true);
        var preferences = new InMemoryPreferences();
        if (accepted) preferences.Set(IntelligencePreferences.NoticeKey, "accepted");
        return new IntelligencePreferences(settings, preferences);
    }

    private static IntelligenceCheckViewModel Check(
        FakeModel model,
        IReadOnlyList<Device>? devices = null,
        IReadOnlyList<Alert>? alerts = null,
        IShareService? share = null,
        TimeProvider? time = null) =>
        new(model, new NetworkOverviewLookup(Fakes.Client(devices, alerts), Fakes.Settings()), Preferences(), share,
            () => new MemoryReading(200 * 1048576L, 0), time);

    /// <summary>Without a context to post to, so the answer is in place the moment the run ends.</summary>
    private static async Task RunAsync(IntelligenceCheckViewModel check)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            await check.RunCommand.ExecuteAsync(null);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_disabled_or_unaccepted_diagnostic_cannot_query_or_start_a_model(bool accepted)
    {
        var preferences = Preferences(accepted);
        if (accepted) preferences.SetEnabled(false);
        var client = Fakes.Client();
        var model = new FakeModel();
        var check = new IntelligenceCheckViewModel(model, new NetworkOverviewLookup(client, Fakes.Settings()), preferences);
        Assert.True(check.IsAvailable); // Availability remains inspectable while the gate is off.
        Assert.Contains("Turn on Intelligence", check.AvailabilityText);
        Assert.False(check.RunCommand.CanExecute(null));
        await RunAsync(check); // Also covers direct ExecuteAsync calls bypassing CanExecute.
        await client.Devices.DidNotReceive().ListAsync(Arg.Any<CancellationToken>());
        await client.Alerts.DidNotReceive().ListAsync(Arg.Any<DesktopNMS.Core.Api.AlertQuery?>(), Arg.Any<CancellationToken>());
        Assert.Null(model.Instructions);
        preferences.SetEnabled(true);
        check.Refresh();
        Assert.True(check.RunCommand.CanExecute(null));
    }

    [Fact]
    public void The_overview_counts_everything_and_names_what_is_down()
    {
        var devices = new[]
        {
            Fakes.Device(1, "core-sw-1"),
            Fakes.Device(2, "edge-rtr-10", up: false),
            Fakes.Device(3, "edge-rtr-2", up: false),
            Fakes.Device(4, "lab-ap-1", disabled: true),
        };
        var alerts = new[]
        {
            Fakes.Alert(1, 1, "warning"),
            Fakes.Alert(2, 2, "critical"),
            Fakes.Alert(3, 3, "critical", acknowledged: true),
        };

        var text = NetworkOverviewLookup.Describe(devices, alerts, DeviceNameStyle.Hostname);

        Assert.Contains("Devices: 4 total, 1 up, 2 down, 1 disabled.", text);
        Assert.Contains("Down: edge-rtr-2; edge-rtr-10.", text);
        Assert.Contains("Open alerts: 3 total, 2 critical, 1 warning, 1 acknowledged.", text);
        Assert.Contains("Alerts: critical: Rule 2 on edge-rtr-10; critical: Rule 3 on edge-rtr-2; warning: Rule 1 on core-sw-1.", text);
    }

    [Fact]
    public void A_quiet_network_says_there_are_no_alerts()
    {
        var text = NetworkOverviewLookup.Describe([Fakes.Device(1, "core-sw-1")], [], DeviceNameStyle.Hostname);

        Assert.Equal("Devices: 1 total, 1 up, 0 down.\nOpen alerts: none.", text);
    }

    [Fact]
    public void A_large_network_costs_about_the_same_as_a_small_one()
    {
        var devices = Enumerable.Range(1, 5000)
            .Select(i => Fakes.Device(i, $"distribution-switch-with-a-long-name-{i}.example.net", up: i % 5 != 0))
            .ToList();
        var alerts = Enumerable.Range(1, 3000).Select(i => Fakes.Alert(i, i, i % 2 == 0 ? "critical" : "warning")).ToList();

        var text = NetworkOverviewLookup.Describe(devices, alerts, DeviceNameStyle.Hostname);

        Assert.Contains("Devices: 5000 total, 4000 up, 1000 down.", text);
        Assert.Contains("and 992 more", text);
        Assert.Contains("and 2992 more", text);
        // Sixteen capped names and the counts: a few hundred tokens at most.
        Assert.InRange(text.Length, 1, 1400);
    }

    [Fact]
    public async Task The_check_gives_the_model_the_lookup_and_reports_what_it_cost()
    {
        var model = new FakeModel();
        var time = new FakeTimeProvider();
        model.BeforeFirstWords = () => time.Advance(TimeSpan.FromSeconds(1.5));
        var check = Check(model, [Fakes.Device(1, "core-sw-1")], time: time);

        await RunAsync(check);

        Assert.Equal("All is well.", check.Answer);
        Assert.Equal("Completed", check.Status);
        Assert.False(check.IsRunning);
        Assert.True(model.Disposed);
        Assert.Equal("Devices: 1 total, 1 up, 0 down.\nOpen alerts: none.", model.LookupResult);
        Assert.Contains(NetworkOverviewLookup.Name, model.Instructions);
        Assert.Contains("Context size: 4096 tokens", check.Report);
        Assert.Contains("Lookup result: 12 tokens (50 characters)", check.Report);
        Assert.Contains("Lookups run by the model: 1", check.Report);
        Assert.Contains("First words after: 1.50 s", check.Report);
        Assert.Contains("Model's own usage: input 300, output 5", check.Report);
        Assert.Contains("App memory: 200.0 MB before, 200.0 MB after", check.Report);
        Assert.Contains("Outcome: Completed", check.Report);
        Assert.EndsWith("Answer:" + Environment.NewLine + "All is well.", check.Report);
    }

    [Theory]
    [InlineData(IntelligenceAvailability.DeviceNotEligible, "doesn't support Apple Intelligence")]
    [InlineData(IntelligenceAvailability.NotEnabled, "turned off")]
    [InlineData(IntelligenceAvailability.ModelNotReady, "still downloading")]
    [InlineData(IntelligenceAvailability.Unsupported, "iOS 26 or later")]
    public void A_model_that_cannot_answer_says_why_and_cannot_be_asked(IntelligenceAvailability availability, string reason)
    {
        var check = Check(new FakeModel { Availability = availability });

        Assert.False(check.IsAvailable);
        Assert.Contains(reason, check.AvailabilityText);
        Assert.False(check.RunCommand.CanExecute(null));
    }

    [Fact]
    public async Task A_failed_answer_keeps_what_was_written_and_says_it_failed()
    {
        var model = new FakeModel { Failure = new IntelligenceException("contextSizeExceeded", "All") };
        var check = Check(model);

        await RunAsync(check);

        Assert.Equal("All", check.Answer);
        Assert.Equal("Failed: contextSizeExceeded", check.Status);
        Assert.Contains("Outcome: Failed: contextSizeExceeded", check.Report);
        Assert.False(check.IsRunning);
    }

    [Fact]
    public async Task Stop_ends_the_answer_as_stopped_not_completed()
    {
        var model = new FakeModel { Hold = new TaskCompletionSource() };
        var check = Check(model);

        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            var run = check.RunCommand.ExecuteAsync(null);
            Assert.True(check.IsRunning);
            Assert.False(check.RunCommand.CanExecute(null));

            check.StopCommand.Execute(null);
            await run;
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        Assert.Equal("Stopped", check.Status);
        Assert.Contains("Outcome: Stopped", check.Report);
        Assert.False(check.IsRunning);
    }

    [Fact]
    public async Task A_LibreNMS_failure_is_reported_without_asking_the_model()
    {
        var client = Fakes.Client();
        client.Devices.ListAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<Device>>(new HttpRequestException("refused")));
        var model = new FakeModel();
        var check = new IntelligenceCheckViewModel(model, new NetworkOverviewLookup(client, Fakes.Settings()), Preferences());

        await RunAsync(check);

        Assert.StartsWith("Failed: Couldn't reach the LibreNMS server", check.Status);
        Assert.Null(model.Instructions);
    }

    [Fact]
    public async Task The_report_is_shared_as_a_text_file()
    {
        var share = Substitute.For<IShareService>();
        var check = Check(new FakeModel(), share: share);
        Assert.False(check.ShareCommand.CanExecute(null));

        await RunAsync(check);
        await check.ShareCommand.ExecuteAsync(null);

        await share.Received(1).ShareTextFileAsync("dashynms-intelligence-check.txt", check.Report, "text/plain", "Intelligence check");
    }

    [Fact]
    public void A_phone_with_no_model_is_unsupported_and_refuses_a_session()
    {
        Assert.Equal(IntelligenceAvailability.Unsupported, new NoIntelligenceModel().Availability);
        Assert.Throws<IntelligenceException>(() => new NoIntelligenceModel().StartSession("x", []));
    }
}
