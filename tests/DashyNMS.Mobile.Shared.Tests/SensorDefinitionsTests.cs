using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DashyNMS.Mobile.Services;
using DashyNMS.Mobile.ViewModels;
using DesktopNMS.Core.Models;
using DesktopNMS.Core.Sensors;

namespace DashyNMS.Mobile.Tests;

/// <summary>Settings › Sensor definitions (#209), against a fake GitHub.</summary>
public sealed class SensorDefinitionsTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"dashynms-{Guid.NewGuid():N}");
    private readonly FakeGitHub _github = new();
    private readonly List<SensorDefinitions> _opened = [];

    public SensorDefinitionsTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        foreach (var definitions in _opened)
        {
            definitions.Dispose();
        }

        Directory.Delete(_folder, recursive: true);
    }

    private SensorDefinitions Open()
    {
        var definitions = new SensorDefinitions(_folder, new HttpClient(_github));
        _opened.Add(definitions);
        return definitions;
    }

    private static Sensor FanReading() =>
        new() { SensorClass = "state", SensorType = "exampleFanState", Oid = ".1.3.6.1.4.1.32473.1.7", Current = 2, DeviceId = 12 };

    private string DownloadsFolder => Path.Combine(_folder, "sensor-definitions");

    [Fact]
    public async Task A_first_start_turns_the_official_definitions_on_and_fetches_the_catalogue()
    {
        var definitions = Open();

        await definitions.StartAsync();

        Assert.True(definitions.Store.Options.Enabled);
        Assert.False(definitions.Store.Options.UseCustomRepository);
        Assert.Equal("1.0.0", definitions.Store.Version);
        Assert.Contains(_github.Requests, r => r.EndsWith("stable.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Turning_it_off_deletes_the_downloads_and_nothing_more_is_fetched()
    {
        var definitions = Open();
        await definitions.StartAsync();
        await definitions.Store.SynchroniseAsync(["example-os"], force: true);
        Assert.True(Directory.Exists(DownloadsFolder));
        var navigation = new RecordingNavigation();
        var vm = new SensorDefinitionsSettingsViewModel(definitions, navigation) { Enabled = false };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(Directory.Exists(DownloadsFolder));
        Assert.False(definitions.Store.Options.Enabled);
        Assert.Equal(Routes.Back, navigation.Visits.Single().Route);

        _github.Requests.Clear();
        await definitions.CheckAsync();
        await Open().StartAsync();
        Assert.Empty(_github.Requests);
    }

    [Fact]
    public async Task After_an_offline_restart_the_cached_definitions_still_resolve()
    {
        var first = Open();
        await first.StartAsync();
        await first.Store.SynchroniseAsync(["example-os"], force: true);
        first.Dispose();
        _github.Offline = true;

        var again = Open();
        await again.StartAsync();
        await again.Store.SynchroniseAsync(["example-os"]);

        Assert.Equal("Ready", again.Store.Resolve(FanReading(), "example-os")?.Label);
    }

    [Theory]
    [InlineData("http://github.com/example/definitions")]
    [InlineData("https://gitlab.example.net/example/definitions")]
    [InlineData("https://github.com/example/definitions/tree/main")]
    [InlineData("https://user:secret@github.com/example/definitions")]
    public async Task A_repository_that_isnt_a_public_https_GitHub_address_is_refused(string address)
    {
        var definitions = Open();
        await definitions.StartAsync();
        var vm = new SensorDefinitionsSettingsViewModel(definitions, new RecordingNavigation())
        {
            UseCustomRepository = true,
            CustomRepository = address,
        };

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.True(vm.HasError);
        Assert.False(definitions.Store.Options.UseCustomRepository);
    }

    [Fact]
    public async Task A_custom_repository_is_saved_as_its_plain_GitHub_address()
    {
        var definitions = Open();
        await definitions.StartAsync();
        var vm = new SensorDefinitionsSettingsViewModel(definitions, new RecordingNavigation())
        {
            UseCustomRepository = true,
            CustomRepository = " https://github.com/Example/Definitions.git ",
        };
        Assert.False(vm.CanUpdate); // a draft isn't updated until it's saved

        await vm.SaveCommand.ExecuteAsync(null);

        Assert.False(vm.HasError);
        Assert.Equal("https://github.com/example/definitions", definitions.Store.Options.Repository);
    }

    [Fact]
    public async Task Forgetting_everything_stops_requests_and_leaves_no_choice_behind()
    {
        var definitions = Open();
        await definitions.StartAsync();

        await definitions.ForgetAsync();
        _github.Requests.Clear();
        await definitions.CheckAsync();

        Assert.Empty(_github.Requests);
        Assert.False(File.Exists(Path.Combine(_folder, "installation-settings.json")));
        Assert.False(Directory.Exists(DownloadsFolder));
    }

    [Fact]
    public async Task Update_now_fetches_the_catalogue_again_and_shows_its_version()
    {
        var definitions = Open();
        await definitions.StartAsync();
        var vm = new SensorDefinitionsSettingsViewModel(definitions, new RecordingNavigation());
        _github.Version = "1.1.0";

        Assert.True(vm.CanUpdate);
        await vm.UpdateNowCommand.ExecuteAsync(null);

        Assert.Equal("1.1.0", vm.VersionText);
        Assert.NotEqual("Not checked", vm.LastCheckText);
    }
}

/// <summary>Core's own fixture (DashyNMS/core's SensorDefinitionTests): one OS, one fan state.</summary>
internal sealed class FakeGitHub : HttpMessageHandler
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string Version = "1.0.0";
    public bool Offline;
    public readonly List<string> Requests = [];

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static byte[] Compress(byte[] bytes)
    {
        using var buffer = new MemoryStream();
        using (var zip = new GZipStream(buffer, CompressionMode.Compress, leaveOpen: true))
        {
            zip.Write(bytes);
        }

        return buffer.ToArray();
    }

    private static byte[] Shard(string os) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        schemaVersion = 1,
        sources = new[] { new { id = "fixture", kind = "reviewed-override", location = "https://example.net/definitions", revision = "fixture-1", licence = "MIT" } },
        definitions = new[]
        {
            new
            {
                id = os + "/fan",
                match = new { os, sensorClass = "state", sensorType = "exampleFanState", oidPrefix = "1.3.6.1.4.1.32473.1" },
                states = new[] { new { value = "2", label = "Ready", genericState = "ok" } },
                sourceRefs = new[] { new { sourceId = "fixture", path = "fixture.json", symbol = "exampleFanState", suppliesSeverity = true } },
            },
        },
    });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        Requests.Add(path);
        if (Offline)
        {
            throw new HttpRequestException("Offline");
        }

        if (path.Contains("/feeds/v2/", StringComparison.Ordinal))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
        }

        var zip = Compress(Shard("example-os"));
        var notice = Encoding.UTF8.GetBytes("Synthetic fixture notices");
        byte[] bytes;
        if (path.EndsWith("stable.json", StringComparison.Ordinal))
        {
            bytes = JsonSerializer.SerializeToUtf8Bytes(new SensorDefinitionIndex
            {
                SchemaVersion = 1,
                CatalogueVersion = Version,
                Shards = [new SensorDefinitionShard("example-os", Hash(zip), zip.Length, Shard("example-os").Length, 1)],
                Notices = new SensorDefinitionAsset(Hash(notice), notice.Length),
            }, Json);
        }
        else if (path.EndsWith("notices.txt", StringComparison.Ordinal))
        {
            bytes = notice;
        }
        else
        {
            bytes = zip;
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes), RequestMessage = request });
    }
}
