using DashyNMS.Mobile.Demo;
using DashyNMS.Mobile.Intelligence;
using DashyNMS.Mobile.Services;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Demo;
using DesktopNMS.Core.Security;
using DesktopNMS.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace DashyNMS.Mobile.Tests;

/// <summary>The demo's example conversation (#234), asked through the real conversation over Core's example network.</summary>
public sealed class DemoIntelligenceModelTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "dashynms-demo-chat-" + Guid.NewGuid().ToString("N"));
    private readonly SettingsStore _settings;
    private readonly LibreNmsTransport _transport = new(NullLogger<LibreNmsTransport>.Instance);
    private readonly LibreNmsClient _client;
    private readonly SessionService _session;
    private readonly DemoState _demo = new();
    private readonly InMemoryPreferences _saved = new();

    public DemoIntelligenceModelTests()
    {
        Directory.CreateDirectory(_folder);
        _settings = new SettingsStore(NullLogger<SettingsStore>.Instance, Path.Combine(_folder, "settings.json"));
        _client = new LibreNmsClient(_transport, NullLogger<LibreNmsClient>.Instance);
        _session = new SessionService(_client, _settings, Substitute.For<ITokenProtector>(), NullLogger<SessionService>.Instance);
        _demo.Set(true);
        _session.ConnectDemoAsync(new DemoResponder().Connection()).GetAwaiter().GetResult();
        _saved.Set(IntelligencePreferences.NoticeKey, "accepted");
    }

    public void Dispose()
    {
        _client.Dispose();
        _transport.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private IntelligenceConversationViewModel Chat(IIntelligenceModel model)
    {
        var lookups = new CompactNetworkLookups(_client, _settings, _session, deviceTransport: new IntelligenceDeviceTransports(_transport, _settings, _session).Open);
        return new IntelligenceConversationViewModel(model, new IntelligencePreferences(_settings, _saved), lookups, _session, action => action());
    }

    [Fact]
    public async Task With_no_model_on_the_phone_the_demo_offers_and_answers_its_examples()
    {
        var model = new DemoIntelligenceModel(new NoIntelligenceModel(), _demo);
        using var chat = Chat(model);

        Assert.Equal(IntelligenceAvailability.Available, model.Availability);
        Assert.Equal(DemoIntelligenceModel.Questions, chat.StarterQuestions);
        Assert.NotNull(model.ExampleNotice);
        Assert.True(await chat.AskStarterAsync(DemoIntelligenceModel.Questions[0], chat.ChatRevision));

        var turn = Assert.Single(chat.Turns);
        Assert.Equal(IntelligenceTurnState.Completed, turn.State);
        Assert.Contains("| Severity | Alert | Device | Open for |", turn.Answer, StringComparison.Ordinal);
        Assert.Contains("[[alert:", turn.Answer, StringComparison.Ordinal);
        Assert.Contains("[[device:", turn.Answer, StringComparison.Ordinal);
        Assert.EndsWith(DemoIntelligenceModel.Caption, turn.Answer, StringComparison.Ordinal);
        Assert.Contains(turn.Lookups, l => l.Name == "network_overview" && l.State == IntelligenceLookupState.Completed);
        Assert.NotEmpty(turn.Evidence);
    }

    [Fact]
    public async Task Unreachable_devices_come_as_a_table_of_documentation_addresses()
    {
        using var chat = Chat(new DemoIntelligenceModel(new NoIntelligenceModel(), _demo));

        Assert.True(await chat.AskAsync("which devices are currently unreachable"));

        var turn = Assert.Single(chat.Turns);
        Assert.Contains("**3 devices are unreachable.**", turn.Answer, StringComparison.Ordinal);
        Assert.Contains("| Device | Address | Location |", turn.Answer, StringComparison.Ordinal);
        Assert.Contains("203.0.113.", turn.Answer, StringComparison.Ordinal);
        Assert.Contains(turn.Evidence, e => e.Reference.Key.StartsWith("device:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_traffic_example_shows_the_demo_graph()
    {
        using var chat = Chat(new DemoIntelligenceModel(new NoIntelligenceModel(), _demo));

        Assert.True(await chat.AskAsync(DemoIntelligenceModel.Questions[2]));

        var turn = Assert.Single(chat.Turns);
        Assert.Equal(IntelligenceTurnState.Completed, turn.State);
        Assert.Contains("core-sw-01", turn.Answer, StringComparison.Ordinal);
        var image = Assert.Single(turn.Images);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, image.Data[..4]);
    }

    [Fact]
    public async Task Another_question_without_a_model_says_what_the_example_can_answer()
    {
        using var chat = Chat(new DemoIntelligenceModel(new NoIntelligenceModel(), _demo));

        Assert.True(await chat.AskAsync("Which ports are counting the most errors?"));

        var turn = Assert.Single(chat.Turns);
        Assert.Equal(IntelligenceTurnState.Completed, turn.State);
        Assert.Contains("Apple Intelligence", turn.Answer, StringComparison.Ordinal);
        Assert.Contains(DemoIntelligenceModel.Questions[1], turn.Answer, StringComparison.Ordinal);
    }

    [Fact]
    public void Outside_the_demo_the_phone_model_is_unchanged()
    {
        _demo.Set(false);
        var inner = Substitute.For<IIntelligenceModel>();
        inner.Availability.Returns(IntelligenceAvailability.NotEnabled);
        var model = new DemoIntelligenceModel(inner, _demo);

        Assert.Equal(IntelligenceAvailability.NotEnabled, model.Availability);
        Assert.Null(model.ExampleQuestions);
        Assert.Null(model.ExampleNotice);
        model.StartSession("instructions", []);
        inner.Received(1).StartSession("instructions", Arg.Any<IReadOnlyList<IntelligenceLookup>>());
    }

    [Fact]
    public void Recorded_questions_match_however_they_are_typed()
    {
        Assert.Equal(DemoIntelligenceModel.Questions[0], DemoIntelligenceModel.Recorded("Earlier question: x\nEarlier answer: y\n\nQuestion:\n  how is the network doing  "));
        Assert.Equal(DemoIntelligenceModel.Questions[2], DemoIntelligenceModel.Recorded("Question:\nShow me a traffic graph for core-sw-01 over the last day?"));
        Assert.Null(DemoIntelligenceModel.Recorded("Question:\nHow is the network doing in Leeds?"));
    }

    [Fact]
    public void Decorate_wraps_the_platform_model()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_demo);
        services.AddSingleton<IIntelligenceModel, NoIntelligenceModel>();

        DemoIntelligenceModel.Decorate(services);

        using var provider = services.BuildServiceProvider();
        Assert.IsType<DemoIntelligenceModel>(provider.GetRequiredService<IIntelligenceModel>());
        Assert.Single(services, d => d.ServiceType == typeof(IIntelligenceModel));
    }
}
