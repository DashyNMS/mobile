using System.Text.Json;
using DashyNMS.Agent.Graphs;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Intelligence;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace DashyNMS.Mobile.Tests;

public sealed class SharedGraphLookupHttpTests
{
    internal static readonly DateTimeOffset Now = new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);
    internal static FixtureServer Server(bool denied = false, bool oversized = false, bool partial = false,
        Func<string, Answer?>? change = null, Action? imageRequested = null, bool hang = false)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Graphs", "routes.json")));
        var fixture = json.RootElement.Clone();
        var routes = fixture.GetProperty("routes").EnumerateObject().ToDictionary(p => p.Name, p => new Answer(200, p.Value.GetRawText()));
        return new(path => change?.Invoke(path) ?? (partial && path == "devices/1/wireless" ? new(403, "{}") : routes.GetValueOrDefault(path)), requestAnswer: request =>
        {
            if (request.QueryString["graph_type"] is not { } format) return null;
            imageRequested?.Invoke();
            if (hang) return Answer.Hang;
            var image = denied ? fixture.GetProperty("deniedSvg").GetString() : format == "svg" ? fixture.GetProperty("svg").GetString()
                : oversized ? new string('A', ((IntelligenceImage.MaxBytes + 2) / 3) * 4 + 4) : fixture.GetProperty("png").GetString();
            return new(200, JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["image"] = new Dictionary<string, object?> { ["image"] = image, ["content-type"] = denied || format == "svg" ? "image/svg+xml" : "image/png" },
            }));
        });
    }
    internal sealed class Mobile : IDisposable
    {
        private readonly LibreNmsTransport transport = new(NullLogger<LibreNmsTransport>.Instance);
        private readonly IntelligenceDeviceTransports transports;
        private readonly IntelligenceLookupSet set;
        internal Mobile(Uri root, FakeTimeProvider? time = null)
        {
            Session = Fakes.Session();
            Session.Connection.Returns(new LibreNmsConnection(root, FixtureServer.Token));
            var settings = Fakes.Settings();
            transports = new(transport, settings, Session);
            Provider = new(Fakes.Client(), settings, Session, time ?? new FakeTimeProvider(Now), deviceTransport: transports.Open);
            set = Provider.Open(Evidence.AddRange, Images.AddRange);
        }
        internal ISessionService Session { get; }
        internal CompactNetworkLookups Provider { get; }
        internal List<IntelligenceEvidence> Evidence { get; } = [];
        internal List<IntelligenceImage> Images { get; } = [];
        internal IntelligenceLookup Graph => set.Lookups.Single(l => l.Name == "graph");
        public void Dispose() { set.Dispose(); transports.Dispose(); transport.Dispose(); }
    }
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("cpu", null, null)]
    [InlineData("errors", "1/1/7", null)]
    [InlineData("temperature", null, "1")]
    public async Task Graph_metadata_pixels_and_HTTP_reads_match_the_shared_operation(string? type, string? port, string? sensor)
    {
        using var server = Server();
        using var expected = Server();
        using var mobile = new Mobile(server.Root);
        using var transport = new LibreNmsTransport(NullLogger<LibreNmsTransport>.Instance);
        using var client = new LibreNmsClient(transport, NullLogger<LibreNmsClient>.Instance);
        client.Connect(new LibreNmsConnection(expected.Root, FixtureServer.Token));
        using var host = HostGraphLookup.Create(client, transport, new FakeTimeProvider(Now));
        var found = (await host.ReadAsync(new(1, Type: type, Port: port, Sensor: sensor))).Result!;
        var text = await mobile.Graph.Run(JsonSerializer.Serialize(new { device = "1", type, port, sensor }), default);
        Assert.Equal(found.Image!.Data, Assert.Single(mobile.Images).Data);
        Assert.Equal("image/png", mobile.Images[0].MimeType);
        Assert.Contains(found.Data.Graph!.Type, text);
        Assert.Contains("model receives metadata only", text);
        Assert.Contains(mobile.Evidence, e => e.Reference.Object is { Kind: "device", Id: 1 });
        Assert.Equal(expected.HttpRequests.Where(r => r != "/api/v0/devices").Order(), server.HttpRequests.Where(r => r != "/api/v0/devices").Order());
        Assert.InRange(text.Length, 1, CompactNetworkLookups.MaxCharacters);
        Assert.Equal(DashyNMS.Agent.Catalogue.ToolCatalogue.Find("graph")!.Description, mobile.Graph.Purpose);
    }
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Denied_or_oversized_images_never_reach_the_answer(bool denied, bool oversized)
    {
        using var server = Server(denied: denied, oversized: oversized);
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Graph.Run("1", default);
        if (denied) Assert.Equal(CompactNetworkLookups.Refused, text);
        else Assert.StartsWith("No matching graph could be read.", text);
        Assert.Empty(mobile.Images);
        Assert.Empty(mobile.Evidence);
    }
    [Fact]
    public async Task Successful_image_keeps_partial_inventory_notice_before_detail_rows()
    {
        using var server = Server(partial: true);
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Graph.Run("1", default);
        Assert.StartsWith("Incomplete graph listings: wireless graphs.", text);
        Assert.Single(mobile.Images);
    }
    [Fact]
    public async Task Missing_type_with_a_failed_listing_stays_unknown_without_an_image()
    {
        using var server = Server(change: p => p == "devices/1/graphs" ? new(403, "{}") : null);
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Graph.Run("1", default);
        Assert.StartsWith("Incomplete graph listings: device graphs.", text);
        Assert.DoesNotContain("has no", text);
        Assert.Empty(mobile.Images);
    }
    [Fact]
    public async Task Custom_window_and_legend_use_the_shared_image_query()
    {
        using var server = Server();
        using var mobile = new Mobile(server.Root);
        var text = await mobile.Graph.Run("""{"device":"1","since":"2026-10-16T10:00:00Z","until":"2026-10-17T09:00:00Z","legend":false}""", default);
        Assert.Contains("from 16 Oct 2026 10:00; to 17 Oct 2026 09:00", text);
        Assert.Contains(server.HttpRequests, r => r.Contains("&legend=no&graph_type=png&output=base64&bg=FFFFFF&bbg=FFFFFF", StringComparison.Ordinal));
        Assert.Single(mobile.Images);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cancellation_or_deadline_releases_a_pending_graph_without_pixels_or_evidence(bool deadline)
    {
        var requested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var server = Server(imageRequested: () => requested.TrySetResult(), hang: true);
        var time = new FakeTimeProvider(Now);
        using var mobile = new Mobile(server.Root, time);
        using var cancel = new CancellationTokenSource();
        var pending = mobile.Graph.Run("1", cancel.Token);
        await requested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (deadline)
        {
            time.Advance(CompactNetworkLookups.Timeout);
            Assert.Equal(CompactNetworkLookups.Failed, await pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        else
        {
            cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(TimeSpan.FromSeconds(5)));
        }
        Assert.Empty(mobile.Images);
        Assert.Empty(mobile.Evidence);
    }
    [Fact]
    public async Task A_connection_switch_during_image_reading_discards_pixels_and_evidence()
    {
        Mobile? mobile = null;
        using var server = Server(imageRequested: () =>
        {
            mobile!.Session.Connection.Returns(new LibreNmsConnection(new Uri("https://other.example.net/"), "synthetic-token"));
            mobile.Session.StateChanged += Raise.Event();
        });
        using var owned = mobile = new Mobile(server.Root);
        Assert.Equal(CompactNetworkLookups.Changed, await mobile.Graph.Run("1", default));
        Assert.Empty(mobile.Images);
        Assert.Empty(mobile.Evidence);
    }
    private sealed class TextBridgeModel(bool supportsImages = false, bool imagesProvided = false, int? rejectedImage = null) : IIntelligenceModel
    {
        public IntelligenceAvailability Availability => IntelligenceAvailability.Available;
        public int? ContextSize => 8192;
        public bool SupportsLookupImages => supportsImages;
        public bool FailAfterLookup { get; init; }
        private bool ReportsImages => imagesProvided;
        private int? RejectedImage => rejectedImage;
        public IReadOnlyList<IntelligenceImage> Received { get; private set; } = [];
        public string? Result { get; private set; }
        public Task<int?> CountTokensAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);
        public IIntelligenceSession StartSession(string instructions, IReadOnlyList<IntelligenceLookup> lookups) => new ModelSession(this, lookups);
        private sealed class ModelSession(TextBridgeModel owner, IReadOnlyList<IntelligenceLookup> tools) : IIntelligenceSession
        {
            private readonly List<string> omitted = [];
            public IReadOnlyList<string> LookupImageIdsOmitted => omitted.ToArray();
            public async Task<IntelligenceAnswer> AskAsync(string question, Action<string> onText, CancellationToken cancellationToken = default)
            {
                var graph = tools.Single(t => t.Name == "graph");
                var provided = new List<string>();
                if (owner.SupportsLookupImages)
                {
                    var images = new List<IntelligenceImage>();
                    for (var i = 0; i < (owner.RejectedImage is null ? 1 : 2); i++)
                    {
                        var result = await graph.ReadAsync(i == 0 ? "1" : """{"device":"1","type":"cpu"}""", cancellationToken);
                        Assert.NotNull(result.MetadataOnlyText);
                        Assert.NotNull(result.ImageId);
                        Assert.InRange(result.MetadataOnlyText.Length, 1, CompactNetworkLookups.MaxCharacters);
                        var accepted = owner.ReportsImages && i != owner.RejectedImage;
                        owner.Result = accepted ? result.Text : result.MetadataOnlyText;
                        if (accepted) provided.Add(result.ImageId);
                        else omitted.Add(result.ImageId);
                        images.AddRange(result.Images);
                    }
                    owner.Received = images;
                }
                else owner.Result = await graph.Run("1", cancellationToken);
                if (owner.FailAfterLookup) throw new DashyNMS.Mobile.Intelligence.IntelligenceException("synthetic failure", "Partial answer");
                return new("The requested graph is attached.", null) { LookupImageIdsProvided = provided };
            }
            public void Dispose() { }
        }
    }
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Model_capability_and_confirmed_delivery_control_pixels_and_the_saved_chat_notice(bool supportsImages, bool provided)
    {
        using var server = Server();
        using var mobile = new Mobile(server.Root);
        var store = Substitute.For<ICheckedSettingsStore>();
        store.Current.Returns(new AppSettings());
        var saved = new InMemoryPreferences();
        saved.Set(IntelligencePreferences.NoticeKey, "accepted");
        var preferences = new IntelligencePreferences(store, saved);
        var model = new TextBridgeModel(supportsImages, provided);
        using var chat = new IntelligenceConversationViewModel(model, preferences, mobile.Provider, mobile.Session);
        Assert.True(await chat.AskAsync("Show me this device's traffic today"));
        var turn = Assert.Single(chat.Turns);
        Assert.Equal(IntelligenceTurnState.Completed, turn.State);
        Assert.Single(turn.Images);
        Assert.Equal(!supportsImages || !provided, turn.Answer.Contains(IntelligenceConversationViewModel.GraphImageNotice, StringComparison.Ordinal));
        if (supportsImages)
        {
            Assert.Equal(turn.Images[0].Data, Assert.Single(model.Received).Data);
            Assert.Contains(provided ? "Image input is offered" : "model receives metadata only", model.Result);
        }
        else Assert.Contains("model receives metadata only", model.Result);
        Assert.DoesNotContain("iVBOR", model.Result);
        var restored = Assert.Single(IntelligenceSavedChats.Turns(new SavedIntelligenceChat { Messages = IntelligenceSavedChats.Messages(chat.Turns) }));
        Assert.Equal(turn.Images[0].Data, Assert.Single(restored.Images).Data);
        Assert.Equal(turn.Answer, restored.Answer);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Partial_answers_report_only_confirmed_metadata_only_images(bool delivered)
    {
        using var server = Server();
        using var mobile = new Mobile(server.Root);
        var store = Substitute.For<ICheckedSettingsStore>();
        store.Current.Returns(new AppSettings());
        var saved = new InMemoryPreferences();
        saved.Set(IntelligencePreferences.NoticeKey, "accepted");
        var model = new TextBridgeModel(true, delivered) { FailAfterLookup = true };
        using var chat = new IntelligenceConversationViewModel(model, new IntelligencePreferences(store, saved), mobile.Provider, mobile.Session);
        await chat.AskAsync("Show me this device's traffic today");
        var turn = Assert.Single(chat.Turns);
        Assert.Equal(IntelligenceTurnState.Failed, turn.State);
        Assert.Single(turn.Images);
        Assert.Equal(!delivered, turn.Answer.Contains(IntelligenceConversationViewModel.GraphImageNotice, StringComparison.Ordinal));
        var restored = Assert.Single(IntelligenceSavedChats.Turns(new SavedIntelligenceChat { Messages = IntelligenceSavedChats.Messages(chat.Turns) }));
        Assert.Equal(turn.Answer, restored.Answer);
    }

    [Theory]
    [InlineData(0, "Graph image 1 was")]
    [InlineData(1, "Graph image 2 was")]
    public async Task Mixed_delivery_identifies_the_rejected_graph_even_when_another_image_reaches_the_model(int rejected, string notice)
    {
        using var server = Server();
        using var mobile = new Mobile(server.Root);
        var store = Substitute.For<ICheckedSettingsStore>();
        store.Current.Returns(new AppSettings());
        var saved = new InMemoryPreferences();
        saved.Set(IntelligencePreferences.NoticeKey, "accepted");
        var model = new TextBridgeModel(true, true, rejected);
        using var chat = new IntelligenceConversationViewModel(model, new IntelligencePreferences(store, saved), mobile.Provider, mobile.Session);
        Assert.True(await chat.AskAsync("Show me this device's traffic today"));
        var turn = Assert.Single(chat.Turns);
        Assert.Equal(IntelligenceTurnState.Completed, turn.State);
        Assert.Equal(2, turn.Images.Count);
        Assert.Contains(notice + " attached for you, but the model received metadata only and cannot read that image.", turn.Answer);
        Assert.DoesNotContain(IntelligenceConversationViewModel.GraphImageNotice, turn.Answer);
        var restored = Assert.Single(IntelligenceSavedChats.Turns(new SavedIntelligenceChat { Messages = IntelligenceSavedChats.Messages(chat.Turns) }));
        Assert.Equal(turn.Answer, restored.Answer);
        Assert.Equal(2, restored.Images.Count);
    }

    [Fact]
    public async Task The_attachment_limit_refuses_an_extra_graph_before_transport()
    {
        var requests = 0;
        using var server = Server(imageRequested: () => Interlocked.Increment(ref requests));
        using var mobile = new Mobile(server.Root);
        for (var i = 0; i < IntelligenceAnswerImages.MaxImages; i++) await mobile.Graph.Run("1", default);
        var before = requests;
        Assert.Equal(IntelligenceAnswerImages.MaxImages, mobile.Images.Count);
        Assert.StartsWith("The graph attachment limit", await mobile.Graph.Run("1", default));
        Assert.Equal(before, requests);
        Assert.Equal(IntelligenceAnswerImages.MaxImages, mobile.Images.Count);
    }

    private const string ImageQuery = "?from=-1day&width=800&height=250&graph_type=png&output=base64&bg=FFFFFF&bbg=FFFFFF";
    [Theory]
    [InlineData("devices/1/device_bits", true)]
    [InlineData("devices/1/graphs/health/device_temperature/1", true)]
    [InlineData("devices/1/ports/1%2F1%2F7/port_errors", true)]
    [InlineData("devices/0/device_bits", false)]
    [InlineData("devices/1/ports/..%2F..%2Fdevices/port_bits", false)]
    [InlineData("devices/1/ports/1%2f1/port_bits", false)]
    [InlineData("devices/1/ports/1/1/7/port_bits", false)]
    [InlineData("devices/1/delete", false)]
    [InlineData("devices/1/device_bits/../delete", false)]
    [InlineData("https://other.example.net/devices/1/device_bits", false)]
    public void Image_route_allowlist_accepts_only_canonical_render_paths(string path, bool expected)
        => Assert.Equal(expected, ConnectionBoundClient.IsGraphImageRoute(path + ImageQuery));
    [Fact]
    public async Task Graph_profile_refuses_writes_raw_text_and_unrelated_reads_before_transport()
    {
        var opened = false;
        using var client = new ConnectionBoundClient(new SessionScope(0, new LibreNmsConnection(new Uri("https://nms.example.net/"), "synthetic-token")),
            _ => { opened = true; throw new InvalidOperationException(); }, graphs: true);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Alerts);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.DeviceGroups);
        Assert.Throws<ConnectionBoundClient.RefusedException>(() => client.Logs);
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Devices.GetAsync("1"));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.Graphs.GetSvgAsync(1, "device_bits", DesktopNMS.Core.Models.GraphTimeRange.LastDay, 800, 250));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.GraphTransport.SendAsync(HttpMethod.Post, "devices/1/device_bits" + ImageQuery));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.GraphTransport.SendAsync(HttpMethod.Get, "devices/1/device_bits" + ImageQuery + "&extra=1"));
        await Assert.ThrowsAsync<ConnectionBoundClient.RefusedException>(() => client.GraphTransport.SendRawAsync("devices/1/device_bits" + ImageQuery));
        Assert.False(opened);
    }
}
