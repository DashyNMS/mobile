using System.Text.Json.Nodes;
using DashyNMS.Mobile.DeviceSections;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Intelligence;
using DesktopNMS.Services;

namespace DashyNMS.Mobile.Tests;

/// <summary>Formatted answers, verified chips and safe links (#228), from recorded synthetic answers.</summary>
public sealed class IntelligenceAnswerTests
{
    private static readonly DateTimeOffset Observed = new(2026, 11, 11, 9, 30, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<IntelligenceInlineReference> Evidence =
    [
        new("device:12", "edge-router-a", "Down", "down", Observed, "Device status at lookup time") { Object = new("device", 12) },
        new("alert:7", "Port down", "Critical", "critical", Observed, "edge-router-a · Active") { Object = new("fault", 7, 12, 70) },
        new("sensor:31", "Inlet temperature", "", "unknown", Observed, "temperature reading on edge-router-a") { Object = new("sensor", 31, 12) },
        new("group:3", "Core switches", "", "unknown", Observed, "Device group") { Object = new("group", 3) },
        new("location:4", "Example lab", "", "unknown", Observed, "Location") { Object = new("location", 4) },
    ];

    /// <summary>The same objects as lookups report them: groups and locations keep LibreNMS's exact name.</summary>
    private static readonly IReadOnlyList<IntelligenceEvidence> EvidenceItems = Evidence
        .Select(r => new IntelligenceEvidence(r, r.Object?.Kind is "group" or "location" ? r.Name : null)).ToArray();

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Intelligence", name));

    private static string Normalise(string text) => text.Replace("\r\n", "\n").TrimEnd();

    [Theory]
    [InlineData("table-and-markers")]
    [InlineData("malformed-markers")]
    [InlineData("links-and-blocks")]
    public void Recorded_answers_match_their_golden_rendering(string name)
    {
        var blocks = IntelligenceMarkdown.Parse(Fixture(name + ".md"), Evidence);
        Assert.Equal(Normalise(Fixture(name + ".golden")), Normalise(IntelligenceMarkdown.Describe(blocks)));
    }

    [Fact]
    public void A_table_keeps_every_cell_and_its_alignment()
    {
        var table = Assert.Single(IntelligenceMarkdown.Parse(Fixture("table-and-markers.md"), Evidence).OfType<MarkdownTableBlock>());
        Assert.Equal(3, table.Rows.Count);
        Assert.Equal(3, table.Columns);
        Assert.Equal(new[] { MarkdownAlignment.Start, MarkdownAlignment.Centre, MarkdownAlignment.End }, table.Alignments);
    }

    [Fact]
    public void Linked_objects_are_verified_navigable_and_listed_once()
    {
        var blocks = IntelligenceMarkdown.Parse(Fixture("table-and-markers.md"), Evidence);
        Assert.Equal(new[] { "device:12", "alert:7", "sensor:31" }, IntelligenceMarkdown.LinkedObjects(blocks).Select(r => r.Key));
    }

    [Fact]
    public void Markers_without_evidence_never_become_links()
    {
        var blocks = IntelligenceMarkdown.Parse("[[device:12]] and [[alert:7]]", []);
        Assert.Empty(IntelligenceMarkdown.LinkedObjects(blocks));
        Assert.Equal("paragraph: Device 12 and Alert 7", Normalise(IntelligenceMarkdown.Describe(blocks)));
    }

    [Fact]
    public void Status_markers_keep_their_words_and_carry_no_route()
    {
        var span = Assert.Single(IntelligenceMarkdown.Inlines("[[severity:warning]]", Evidence));
        Assert.Equal("Warning", span.Display);
        Assert.Equal("warning", span.Reference!.Tone);
        Assert.Null(span.Reference.Object);
    }

    [Fact]
    public void Answer_text_is_never_interpreted_as_html()
    {
        var blocks = IntelligenceMarkdown.Parse("<script>alert(1)</script> <img src=https://example.org/x.png>", Evidence);
        Assert.Equal("paragraph: <script>alert(1)</script> <img src=https://example.org/x.png>", Normalise(IntelligenceMarkdown.Describe(blocks)));
        Assert.Empty(IntelligenceMarkdown.Links(blocks));
    }

    [Theory]
    [InlineData(101, 2)]
    [InlineData(1, 13)]
    public void Oversized_tables_disclose_omissions(int rows, int columns)
    {
        var header = "| " + string.Join(" | ", Enumerable.Range(0, columns).Select(i => "Column " + i)) + " |";
        var separator = "| " + string.Join(" | ", Enumerable.Repeat("---", columns)) + " |";
        var row = "| " + string.Join(" | ", Enumerable.Repeat("Cell", columns)) + " |";
        var markdown = header + "\n" + separator + "\n" + string.Join("\n", Enumerable.Repeat(row, rows));
        var blocks = IntelligenceMarkdown.Parse(markdown, Evidence);
        var table = Assert.IsType<MarkdownTableBlock>(blocks[0]);
        Assert.True(table.Columns <= 12);
        Assert.True(table.Rows.Count <= 101);
        var notice = Assert.IsType<MarkdownTextBlock>(blocks[1]);
        Assert.Contains("omitted", string.Concat(notice.Spans.Select(span => span.Text)));
    }

    [Fact]
    public void A_streaming_answer_with_an_unclosed_fence_still_parses()
    {
        var blocks = IntelligenceMarkdown.Parse("Before\n```\nshow version", Evidence);
        Assert.Equal("paragraph: Before\ncode:\nshow version\n/code", Normalise(IntelligenceMarkdown.Describe(blocks)));
    }

    [Theory]
    [InlineData("device:12", Routes.DeviceDetail)]
    [InlineData("alert:7", Routes.AlertDetail)]
    [InlineData("sensor:31", Routes.DeviceSection)]
    [InlineData("group:3", Routes.Devices)]
    [InlineData("location:4", Routes.Devices)]
    public void Each_verified_kind_opens_its_existing_page(string key, string expected)
    {
        var reference = Evidence.Single(r => r.Key == key);
        Assert.True(IntelligenceAnswerActions.TryRoute(reference, EvidenceItems, out var route, out var parameters));
        Assert.Equal(expected, route);
        switch (key)
        {
            case "device:12":
                Assert.Equal(12, parameters[Routes.DeviceIdParameter]);
                break;
            case "alert:7":
                // Source-qualified, so fault 7 never opens legacy alert 7.
                Assert.Equal("fault:7", parameters[Routes.AlertKeyParameter]);
                Assert.Equal(12, parameters[Routes.DeviceIdParameter]);
                break;
            case "sensor:31":
                Assert.Equal(12, parameters[Routes.DeviceIdParameter]);
                Assert.Equal(DeviceSection.Sensors, parameters[Routes.SectionParameter]);
                Assert.Equal("edge-router-a", parameters[Routes.DeviceNameParameter]);
                break;
            case "group:3":
                Assert.Equal("Core switches", parameters[Routes.GroupParameter]);
                break;
            case "location:4":
                Assert.Equal("Example lab", parameters[Routes.LocationParameter]);
                break;
        }
    }

    [Fact]
    public void References_outside_the_evidence_or_with_mismatched_targets_open_nothing()
    {
        var forged = Evidence[0] with { Name = "forged" };
        Assert.False(IntelligenceAnswerActions.TryRoute(forged, EvidenceItems, out _, out _));
        var mismatched = new IntelligenceInlineReference("device:12", "edge-router-a", "Down", "down", Observed, "") { Object = new("device", 99) };
        Assert.False(IntelligenceAnswerActions.TryRoute(mismatched, [new(mismatched)], out _, out _));
        var invalid = new IntelligenceInlineReference("alert:8", "No device", "Warning", "warning", Observed, "") { Object = new("alert", 8) };
        Assert.False(IntelligenceAnswerActions.TryRoute(invalid, [new(invalid)], out _, out _));
        var port = new IntelligenceInlineReference("port:5", "Gi0/1", "", "unknown", Observed, "") { Object = new("port", 5, 12) };
        Assert.False(IntelligenceAnswerActions.TryRoute(port, [new(port)], out _, out _));
        var status = IntelligenceInlineMarkup.Resolve("[[status:up]]", Evidence)!;
        Assert.False(IntelligenceAnswerActions.TryRoute(status, [new(status)], out _, out _));
    }

    [Theory]
    [InlineData("group", Routes.GroupParameter)]
    [InlineData("location", Routes.LocationParameter)]
    public void Groups_and_locations_filter_by_the_exact_source_name_not_the_shortened_label(string kind, string parameter)
    {
        // Synthetic: a source name past the label limit, with a tab and marker brackets the label had to clean.
        var original = "Example campus\tbuilding [[device:1]] " + new string('x', 90);
        var label = CompactNetworkLookups.Clean(original);
        var reference = new IntelligenceInlineReference(kind + ":9", label, "", "unknown", Observed, "") { Object = new(kind, 9) };

        Assert.True(IntelligenceAnswerActions.TryRoute(reference, [new(reference, original)], out var route, out var parameters));
        Assert.Equal(Routes.Devices, route);
        Assert.Equal(original, parameters[parameter]);
        Assert.NotEqual(label, original);
        // Without the source name the chip stays text rather than opening an empty, misleading list.
        Assert.False(IntelligenceAnswerActions.TryRoute(reference, [new(reference)], out _, out _));
        // A chip whose text was altered to the source name is not the observed reference.
        var respelled = reference with { Name = original };
        Assert.False(IntelligenceAnswerActions.TryRoute(respelled, [new(reference, original)], out _, out _));
    }

    [Theory]
    [InlineData(true, "up", "Ok")]
    [InlineData(true, "down", "Critical")]
    [InlineData(true, "critical", "Critical")]
    [InlineData(true, "warning", "Warning")]
    [InlineData(true, "unknown", null)]
    [InlineData(false, "critical", null)]
    [InlineData(false, "up", null)]
    public void Status_colour_follows_the_colour_indicators_setting(bool enabled, string tone, string? expected) =>
        Assert.Equal(expected, IntelligenceStatusTone.ColourKey(tone, enabled));

    [Fact]
    public void Chip_names_state_the_target_and_status_in_words()
    {
        Assert.Equal("Open device edge-router-a, Down", IntelligenceStatusTone.Describe(Evidence[0]));
        Assert.Equal("Open alert Port down, Critical", IntelligenceStatusTone.Describe(Evidence[1]));
        Assert.Equal("Show devices at location Example lab", IntelligenceStatusTone.Describe(Evidence[4]));
    }

    [Fact]
    public void Answer_images_follow_Core_limits_and_never_need_a_network_fetch()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        var accepted = IntelligenceAnswerImages.Accept(
        [
            new(png, "image/png"),
            new([], "image/png"),
            new(new byte[IntelligenceImage.MaxBytes + 1], "image/png"),
            new(png, "image/svg+xml"),
            new(png, "text/html"),
        ]);
        Assert.Equal("image/png", Assert.Single(accepted).MimeType);
        Assert.Equal(IntelligenceAnswerImages.MaxImages, IntelligenceAnswerImages.Accept(Enumerable.Repeat(new IntelligenceImage(png, "image/png"), 20)).Count);
    }

    [Fact]
    public void Instructions_ask_for_names_and_markers_within_the_measured_budget()
    {
        foreach (var detail in new[] { "brief", "balanced", "detailed" })
        {
            var instructions = IntelligenceConversationViewModel.Instructions(detail);
            Assert.InRange(instructions.Length, 1, 400);
            Assert.Contains("[[kind:id]] marker", instructions);
            Assert.Contains("never write \"device\" and a number", instructions);
            Assert.Contains("Copy other names exactly", instructions);
        }
    }

    // A conversation over synthetic evidence, as the chat page sees it.

    private sealed class EvidenceLookups(IReadOnlyList<IntelligenceEvidence> references) : IIntelligenceLookups
    {
        public IReadOnlyList<IntelligenceLookup> Create() => Create(_ => { });

        public IReadOnlyList<IntelligenceLookup> Create(Action<IReadOnlyList<IntelligenceEvidence>> evidence) =>
            [new("network_overview", "Overview", (_, _) => { evidence(references); return Task.FromResult("edge-router-a [[device:12]]: Down"); })];
    }

    private sealed class Model(Func<IReadOnlyList<IntelligenceLookup>, Task<IntelligenceAnswer>> answer) : IIntelligenceModel
    {
        public IntelligenceAvailability Availability => IntelligenceAvailability.Available;
        public int? ContextSize => 4096;
        public Task<int?> CountTokensAsync(string text, CancellationToken cancellationToken = default) => Task.FromResult<int?>(null);
        public IIntelligenceSession StartSession(string instructions, IReadOnlyList<IntelligenceLookup> lookups) => new Session(lookups, answer);

        private sealed class Session(IReadOnlyList<IntelligenceLookup> lookups, Func<IReadOnlyList<IntelligenceLookup>, Task<IntelligenceAnswer>> answer) : IIntelligenceSession
        {
            public Task<IntelligenceAnswer> AskAsync(string question, Action<string> onText, CancellationToken cancellationToken = default) => answer(lookups);
            public void Dispose() { }
        }
    }

    private static (IntelligenceConversationViewModel Conversation, ISessionService Session) Conversation(
        Func<IReadOnlyList<IntelligenceLookup>, Task<IntelligenceAnswer>> answer, Func<ISessionService, IIntelligenceLookups>? lookups = null)
    {
        var store = Substitute.For<ICheckedSettingsStore>();
        store.Current.Returns(new AppSettings());
        store.TrySave().Returns(true);
        var preferences = new InMemoryPreferences();
        preferences.Set(IntelligencePreferences.NoticeKey, "accepted");
        var session = Substitute.For<ISessionService>();
        session.Connection.Returns(new LibreNmsConnection(new Uri("https://nms.example.net/"), "test-token"));
        return (new IntelligenceConversationViewModel(new Model(answer), new IntelligencePreferences(store, preferences),
            lookups?.Invoke(session) ?? new EvidenceLookups(EvidenceItems), session), session);
    }

    private static readonly string RecordedAnswer = Fixture("table-and-markers.md");

    [Fact]
    public async Task Lookup_evidence_reaches_the_turn_and_its_chips_open_through_the_app()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var (conversation, _) = Conversation(async tools =>
        {
            await tools[0].Run("", default);
            return new IntelligenceAnswer(RecordedAnswer, null) { Images = [new(png, "image/png"), new(png, "image/gif")] };
        });
        using var owned = conversation;
        var navigation = Substitute.For<INavigationService>();
        var actions = new IntelligenceAnswerActions(conversation, navigation, Substitute.For<ILauncherService>());

        Assert.True(await conversation.AskAsync("How is the edge?"));
        var turn = Assert.Single(conversation.Turns);

        Assert.Equal(Evidence.Select(r => r.Key), turn.References.Select(r => r.Key));
        Assert.Single(turn.Images);
        // Copy uses the original Markdown, markers and table included.
        Assert.Equal(RecordedAnswer, turn.Answer);
        var device = IntelligenceMarkdown.LinkedObjects(IntelligenceMarkdown.Parse(turn.Answer, turn.References))[0];
        Assert.True(actions.CanOpen(turn, device));
        Assert.True(await actions.OpenAsync(turn, device));
        await navigation.Received(1).GoToAsync(Routes.DeviceDetail, Arg.Is<IDictionary<string, object>>(p => (int)p[Routes.DeviceIdParameter] == 12));
    }

    [Fact]
    public async Task Later_answers_can_link_objects_from_earlier_lookups_in_the_same_chat()
    {
        var calls = 0;
        var (conversation, _) = Conversation(async tools =>
        {
            if (calls++ == 0) await tools[0].Run("", default);
            return new IntelligenceAnswer("[[device:12]] is still down.", null);
        });
        using var owned = conversation;
        await conversation.AskAsync("First");
        await conversation.AskAsync("And now?");
        Assert.Contains(conversation.Turns[1].References, r => r.Key == "device:12");
    }

    [Fact]
    public async Task A_connection_change_stops_chips_and_links_from_an_old_answer()
    {
        var (conversation, session) = Conversation(async tools =>
        {
            await tools[0].Run("", default);
            return new IntelligenceAnswer("[[device:12]], see [notes](https://docs.example.org/notes)", null);
        });
        using var owned = conversation;
        var navigation = Substitute.For<INavigationService>();
        var launcher = Substitute.For<ILauncherService>();
        var actions = new IntelligenceAnswerActions(conversation, navigation, launcher);
        await conversation.AskAsync("Question");
        var turn = Assert.Single(conversation.Turns);
        var device = turn.References.Single(r => r.Key == "device:12");
        var link = new Uri("https://docs.example.org/notes");

        session.Connection.Returns(new LibreNmsConnection(new Uri("https://other.example.net/"), "other-token"));
        session.StateChanged += Raise.EventWith(EventArgs.Empty);

        Assert.False(actions.CanOpen(turn, device));
        Assert.False(await actions.OpenAsync(turn, device));
        Assert.False(await actions.OpenLinkAsync(turn, link));
        await navigation.DidNotReceiveWithAnyArgs().GoToAsync(default!, default);
        await launcher.DidNotReceiveWithAnyArgs().OpenAsync(default!);
    }

    [Fact]
    public async Task Links_open_in_the_system_browser_only_when_safe_and_present_in_the_answer()
    {
        var (conversation, _) = Conversation(_ => Task.FromResult(new IntelligenceAnswer("See [notes](https://docs.example.org/notes).", null)));
        using var owned = conversation;
        var launcher = Substitute.For<ILauncherService>();
        var actions = new IntelligenceAnswerActions(conversation, Substitute.For<INavigationService>(), launcher);
        await conversation.AskAsync("Question");
        var turn = Assert.Single(conversation.Turns);

        Assert.True(await actions.OpenLinkAsync(turn, new Uri("https://docs.example.org/notes")));
        Assert.False(await actions.OpenLinkAsync(turn, new Uri("https://elsewhere.example.org/")));
        Assert.False(await actions.OpenLinkAsync(turn, new Uri("file:///etc/hosts")));
        await launcher.Received(1).OpenAsync(new Uri("https://docs.example.org/notes"));
    }

    [Fact]
    public async Task A_location_chip_from_a_real_lookup_opens_the_exact_source_name_in_this_and_later_turns()
    {
        var original = "Example campus\tbuilding [[device:1]] " + new string('x', 90);
        var device = Fakes.Device(12, "edge.example.net", sysName: "edge-router-a", location: original);
        device.LocationId = 4;
        var client = Fakes.Client([device]);
        client.Devices.GetAsync("12", Arg.Any<CancellationToken>()).Returns(device);
        var calls = 0;
        var (conversation, _) = Conversation(async tools =>
        {
            if (calls++ == 0) await tools.Single(t => t.Name == "device_summary").Run("12", default);
            return new IntelligenceAnswer("It is at [[location:4]].", null);
        }, session => new CompactNetworkLookups(client, Fakes.Settings(), session, deviceTransport: Fakes.DeviceTransport(client)));
        using var owned = conversation;
        var navigation = Substitute.For<INavigationService>();
        var actions = new IntelligenceAnswerActions(conversation, navigation, Substitute.For<ILauncherService>());

        await conversation.AskAsync("Where is the edge router?");
        await conversation.AskAsync("And where is it now?");

        Assert.Equal(2, conversation.Turns.Count);
        foreach (var turn in conversation.Turns)
        {
            var chip = Assert.Single(IntelligenceMarkdown.LinkedObjects(IntelligenceMarkdown.Parse(turn.Answer, turn.References)));
            // The chip shows the cleaned label; the route carries LibreNMS's name unchanged.
            Assert.Equal(CompactNetworkLookups.Clean(original), chip.Name);
            Assert.DoesNotContain("[[device:1]]", chip.Name);
            Assert.True(await actions.OpenAsync(turn, chip));
        }
        await navigation.Received(2).GoToAsync(Routes.Devices,
            Arg.Is<IDictionary<string, object>>(p => (string)p[Routes.LocationParameter] == original));
    }

    [Fact]
    public async Task A_recorded_cited_answer_reaches_the_turn_as_Core_formatted_Markdown_with_its_images()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        var message = JsonNode.Parse(Fixture("recorded-citation.json"))!.AsObject();
        var (conversation, _) = Conversation(_ => Task.FromResult(
            new IntelligenceAnswer("", null) { Output = [message], Images = [new(png, "image/png")] }));
        using var owned = conversation;
        var launcher = Substitute.For<ILauncherService>();
        var actions = new IntelligenceAnswerActions(conversation, Substitute.For<INavigationService>(), launcher);

        Assert.True(await conversation.AskAsync("What changed in 26.10?"));
        var turn = Assert.Single(conversation.Turns);

        // Copy keeps the visible source; the unsafe citation never became a link.
        Assert.Equal("LibreNMS 26.10 groups faults by rule. [Release notes](https://docs.example.org/releases/26.10)", turn.Answer);
        Assert.DoesNotContain("javascript", turn.Answer);
        Assert.Single(turn.Images);
        var blocks = IntelligenceMarkdown.Parse(turn.Answer, turn.References);
        Assert.Equal("paragraph: LibreNMS 26.10 groups faults by rule. <Release notes|https://docs.example.org/releases/26.10>",
            Normalise(IntelligenceMarkdown.Describe(blocks)));
        var link = Assert.Single(IntelligenceMarkdown.Links(blocks));
        Assert.Equal("Release notes", link.Text);
        Assert.True(await actions.OpenLinkAsync(turn, link.Link!));
        await launcher.Received(1).OpenAsync(new Uri("https://docs.example.org/releases/26.10"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Text_only_answers_are_unchanged_when_output_holds_no_message(bool withOtherOutput)
    {
        var other = new JsonObject { ["type"] = "web_search_call", ["status"] = "completed" };
        var (conversation, _) = Conversation(_ => Task.FromResult(
            new IntelligenceAnswer(RecordedAnswer, null) { Output = withOtherOutput ? [other] : [] }));
        using var owned = conversation;

        await conversation.AskAsync("How is the edge?");

        Assert.Equal(RecordedAnswer, Assert.Single(conversation.Turns).Answer);
    }
}
