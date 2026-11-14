using DashyNMS.Agent;
using DashyNMS.Mobile.Graylog;
using DashyNMS.Mobile.Unimus;
using DesktopNMS.Core.Api;
using DesktopNMS.Core.Configuration;
using DesktopNMS.Core.Models;
using DesktopNMS.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashyNMS.Mobile.Intelligence;

/// <summary>Prepares existing setup once, then captures immutable connections for each attempt.</summary>
public sealed class IntelligenceIntegrations(GraylogSetup graylog, UnimusSetup unimus, ISettingsStore settings)
{
    public async Task PrepareAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(graylog.EnsureConfiguredAsync(), unimus.EnsureConfiguredAsync()).WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    internal IntelligenceIntegrationClients Capture()
    {
        var logs = graylog.Captured;
        var configs = unimus.Captured;
        var current = settings.Current;
        var textMode = current.IntelligenceConfigText;
        var queryField = current.Graylog.QueryField;
        var anyAddress = current.Graylog.MatchAnyAddress;
        return new(logs.Connection, configs.Connection, current,
            () => graylog.Generation == logs.Generation && unimus.Revision == configs.Generation
                && settings.Current.IntelligenceConfigText == textMode
                && settings.Current.Graylog.QueryField == queryField
                && settings.Current.Graylog.MatchAnyAddress == anyAddress);
    }
}

/// <summary>Owned read-only clients: configuration and credentials cannot be changed by an operation.</summary>
internal sealed class IntelligenceIntegrationClients : IDisposable
{
    private readonly GraylogApi graylog = new(NullLogger<GraylogApi>.Instance);
    private readonly UnimusApi unimus = new(NullLogger<UnimusApi>.Instance);
    private int retired;
    private readonly Func<bool> stillCurrent;

    internal IntelligenceIntegrationClients(GraylogConnection? graylogConnection, UnimusConnection? unimusConnection, AppSettings settings, Func<bool>? stillCurrent = null)
    {
        this.stillCurrent = stillCurrent ?? (() => true);
        if (graylogConnection is not null) graylog.Configure(graylogConnection);
        if (unimusConnection is not null) unimus.Configure(unimusConnection);
        Graylog = new ReadOnlyGraylog(this);
        Unimus = new ReadOnlyUnimus(this);
        Options = new()
        {
            ServerTimestampsAreUtc = settings.ServerTimestampsAreUtc,
            GraylogQueryField = settings.Graylog.QueryField,
            GraylogMatchAnyAddress = settings.Graylog.MatchAnyAddress,
            ConfigText = settings.IntelligenceConfigText switch
            {
                AppSettings.IntelligenceConfigTextFull => ConfigText.Full,
                AppSettings.IntelligenceConfigTextOff => ConfigText.Off,
                _ => ConfigText.Summary,
            },
        };
    }
    internal IGraylogApi Graylog { get; }
    internal IUnimusApi Unimus { get; }
    internal AgentOptions Options { get; }
    internal void Current()
    {
        if (Volatile.Read(ref retired) != 0 || !stillCurrent()) throw new OperationCanceledException("The integration attempt was retired.");
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref retired, 1) != 0) return;
        graylog.Dispose();
        unimus.Dispose();
    }
    private sealed class ReadOnlyGraylog(IntelligenceIntegrationClients owner) : IGraylogApi
    {
        public bool IsConfigured { get { owner.Current(); return owner.graylog.IsConfigured; } }
        public event EventHandler? ConfigurationChanged { add => throw new ConnectionBoundClient.RefusedException(nameof(ConfigurationChanged)); remove { } }
        public void Configure(GraylogConnection connection) => throw new ConnectionBoundClient.RefusedException(nameof(Configure));
        public void Clear() => throw new ConnectionBoundClient.RefusedException(nameof(Clear));
        public Task<IReadOnlyList<GraylogStream>> GetStreamsAsync(CancellationToken cancellationToken = default)
        { owner.Current(); return owner.graylog.GetStreamsAsync(cancellationToken); }
        public Task<GraylogSearchResult> SearchAsync(string query, int rangeSeconds, int limit, int offset, string? sort = null, string? filter = null, CancellationToken cancellationToken = default)
        { owner.Current(); return owner.graylog.SearchAsync(query, rangeSeconds, limit, offset, sort, filter, cancellationToken); }
        public Task<int> TestConnectionAsync(CancellationToken cancellationToken = default) => throw new ConnectionBoundClient.RefusedException(nameof(TestConnectionAsync));
    }
    private sealed class ReadOnlyUnimus(IntelligenceIntegrationClients owner) : IUnimusApi
    {
        public bool IsConfigured { get { owner.Current(); return owner.unimus.IsConfigured; } }
        public void Configure(UnimusConnection connection) => throw new ConnectionBoundClient.RefusedException(nameof(Configure));
        public void Clear() => throw new ConnectionBoundClient.RefusedException(nameof(Clear));
        public Task<UnimusDevice?> FindDeviceAsync(string address, CancellationToken cancellationToken = default) => throw new ConnectionBoundClient.RefusedException(nameof(FindDeviceAsync));
        public Task<IReadOnlyList<UnimusDevice>> ListAllDevicesAsync(CancellationToken cancellationToken = default)
        { owner.Current(); return owner.unimus.ListAllDevicesAsync(cancellationToken); }
        public Task<UnimusBackup?> GetLatestBackupAsync(int unimusDeviceId, CancellationToken cancellationToken = default)
        { owner.Current(); return owner.unimus.GetLatestBackupAsync(unimusDeviceId, cancellationToken); }
        public Task<UnimusBackupPage> GetBackupsAsync(int unimusDeviceId, int page = 0, int size = 50, CancellationToken cancellationToken = default)
        { owner.Current(); return owner.unimus.GetBackupsAsync(unimusDeviceId, page, size, cancellationToken); }
        public Task<string?> GetBackupContentAsync(int unimusDeviceId, int backupId, int page = 0, int size = 50, CancellationToken cancellationToken = default)
        { owner.Current(); return owner.unimus.GetBackupContentAsync(unimusDeviceId, backupId, page, size, cancellationToken); }
        public Task<UnimusBackupDiff?> GetDiffAsync(int origBackupId, int revBackupId, CancellationToken cancellationToken = default)
        { owner.Current(); return owner.unimus.GetDiffAsync(origBackupId, revBackupId, cancellationToken); }
        public Task<UnimusBackupJobResult> TriggerBackupAsync(int unimusDeviceId, CancellationToken cancellationToken = default) => throw new ConnectionBoundClient.RefusedException(nameof(TriggerBackupAsync));
        public Task TestConnectionAsync(CancellationToken cancellationToken = default) => throw new ConnectionBoundClient.RefusedException(nameof(TestConnectionAsync));
    }
}
